using UnityEngine;
using System;
using System.Collections;

using BDArmory.Settings;

namespace BDArmory.Utils
{
    public class BDInputUtils
    {
        public static string GetInputString()
        {
            //keyCodes
            string[] names = System.Enum.GetNames(typeof(KeyCode));
            int numberOfKeycodes = names.Length;

            for (int i = 0; i < numberOfKeycodes; i++)
            {
                string output = names[i];
                if (output.ToLower().StartsWith("mouse") || output.ToLower().StartsWith("joystick")) continue; // Handle mouse and joystick separately.

                if (output.Contains("Keypad"))
                {
                    output = "[" + output.Substring(6).ToLower() + "]";
                }
                else if (output.Contains("Alpha"))
                {
                    output = output.Substring(5);
                }
                else //lower case key
                {
                    output = output.ToLower();
                }

                //modifiers
                if (output.Contains("control"))
                {
                    output = output.Split('c')[0] + " ctrl";
                }
                else if (output.Contains("alt"))
                {
                    output = output.Split('a')[0] + " alt";
                }
                else if (output.Contains("shift"))
                {
                    output = output.Split('s')[0] + " shift";
                }
                else if (output.Contains("command"))
                {
                    output = output.Split('c')[0] + " cmd";
                }

                //special keys
                else if (output == "backslash")
                {
                    output = @"\";
                }
                else if (output == "backquote")
                {
                    output = "`";
                }
                else if (output == "[period]")
                {
                    output = "[.]";
                }
                else if (output == "[plus]")
                {
                    output = "[+]";
                }
                else if (output == "[multiply]")
                {
                    output = "[*]";
                }
                else if (output == "[divide]")
                {
                    output = "[/]";
                }
                else if (output == "[minus]")
                {
                    output = "[-]";
                }
                else if (output == "[enter]")
                {
                    output = "enter";
                }
                else if (output.Contains("page"))
                {
                    output = output.Insert(4, " ");
                }
                else if (output.Contains("arrow"))
                {
                    output = output.Split('a')[0];
                }
                else if (output == "capslock")
                {
                    output = "caps lock";
                }
                else if (output == "minus")
                {
                    output = "-";
                }

                //test if input is valid
                try
                {
                    if (Input.GetKey(output))
                    {
                        return output;
                    }
                }
                catch (System.Exception e)
                {
                    if (!e.Message.EndsWith("is unknown")) // Ignore unknown keys
                        Debug.LogWarning("[BDArmory.BDInputUtils]: Exception thrown in GetInputString: " + e.Message + "\n" + e.StackTrace);
                }
            }

            //mouse
            for (int m = 0; m < 6; m++)
            {
                string inputString = "mouse " + m;
                try
                {
                    if (Input.GetKey(inputString))
                    {
                        return inputString;
                    }
                }
                catch (UnityException e)
                {
                    Debug.Log("[BDArmory.BDInputUtils]: Invalid mouse: " + inputString);
                    Debug.LogWarning("[BDArmory.BDInputUtils]: Exception thrown in GetInputString: " + e.Message + "\n" + e.StackTrace);
                }
            }

            //joysticks
            for (int j = 1; j < 12; j++)
            {
                for (int b = 0; b < 20; b++)
                {
                    string inputString = "joystick " + j + " button " + b;
                    try
                    {
                        if (Input.GetKey(inputString))
                        {
                            return inputString;
                        }
                    }
                    catch (UnityException e)
                    {
                        Debug.LogWarning("[BDArmory.BDInputUtils]: Exception thrown in GetInputString: " + e.Message + "\n" + e.StackTrace);
                        return string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        public static bool GetKey(BDInputInfo input)
        {
            return !string.IsNullOrEmpty(input.inputString) && Input.GetKey(input.inputString);
        }

        public static bool GetKeyDown(BDInputInfo input)
        {
            return !string.IsNullOrEmpty(input.inputString) && Input.GetKeyDown(input.inputString);
        }
    }

    /// <summary>
    /// A class for more easily inputting numeric values in TextFields.
    /// There's a configurable delay after the last keystroke before attempting to interpret the string as a double. Default: 0.5s.
    /// Explicit cast to lower precision types may be needed when assigning the current value.
    /// </summary>
    public class NumericInputField : MonoBehaviour
    {
        public static GUIStyle InputFieldStyle;
        public static GUIStyle InputFieldBadStyle;
        static void ConfigureStyles()
        {
            InputFieldStyle = new GUIStyle(GUI.skin.textField);
            InputFieldStyle.alignment = TextAnchor.MiddleRight;
            InputFieldBadStyle = new GUIStyle(InputFieldStyle);
            InputFieldBadStyle.normal.textColor = Color.red;
            InputFieldBadStyle.focused.textColor = Color.red;
        }

        public NumericInputField Initialise(double l, double v, double minV = double.MinValue, double maxV = double.MaxValue)
        { lastUpdated = l; currentValue = v; minValue = minV; maxValue = maxV; return this; }
        public double lastUpdated;
        public string possibleValue = string.Empty;
        private double _value;
        public double currentValue // Note: setting the current value doesn't necessarily update the displayed string. Use SetCurrentValue to force updating the displayed value.
        {
            get { return _value; }
            private set
            {
                _value = value;
                if (string.IsNullOrEmpty(possibleValue))
                {
                    possibleValue = _value.ToString("G6");
                    valid = true;
                }
            }
        }
        public double minValue;
        public double maxValue;
        private bool coroutineRunning = false;
        private Coroutine coroutine;
        public bool valid = true;

        // Set the current value and force the display to update.
        public void SetCurrentValue(double value)
        {
            possibleValue = null; // Clear the possibleValue first so that it gets updated.
            currentValue = value;
            lastUpdated = Time.time;
        }

        public void tryParseValue(string v)
        {
            if (v != possibleValue)
            {
                lastUpdated = !string.IsNullOrEmpty(v) ? Time.time : Time.time + BDArmorySettings.NUMERIC_INPUT_DELAY; // Give the empty string an extra delay.
                possibleValue = v;
                if (!coroutineRunning)
                {
                    coroutine = StartCoroutine(UpdateValueCoroutine());
                }
            }
        }

        IEnumerator UpdateValueCoroutine()
        {
            var wait = new WaitForFixedUpdate();
            coroutineRunning = true;
            valid = true; // Flag the value as valid until we've tried parsing it.
            while (Time.time - lastUpdated < BDArmorySettings.NUMERIC_INPUT_DELAY)
                yield return wait;
            tryParseCurrentValue(BDArmorySettings.NUMERIC_INPUT_SELF_UPDATE);
            coroutineRunning = false;
            yield return wait;
        }

        void tryParseCurrentValue(bool updatePossible = false)
        {
            double newValue;
            if (double.TryParse(possibleValue, out newValue))
            {
                currentValue = Math.Min(Math.Max(newValue, minValue), Math.Max(maxValue, currentValue)); // Clamp the new value between the min and max, but not if it's been set higher with the unclamped tuning option. This still allows reducing the value while still above the clamp limit.
                if (newValue != currentValue) // The value got clamped.
                    possibleValue = currentValue.ToString("G6");
                lastUpdated = Time.time;
                valid = true;
            }
            else
            {
                valid = false;
            }
            if (updatePossible)
            {
                possibleValue = currentValue.ToString("G6");
                valid = true;
            }
        }

        // Parse the current possible value immediately.
        public void tryParseValueNow()
        {
            tryParseCurrentValue(true);
            if (coroutineRunning)
            {
                StopCoroutine(coroutine);
                coroutineRunning = false;
            }
        }

        public GUIStyle style
        {
            get
            {
                if (InputFieldStyle == null || InputFieldBadStyle == null) ConfigureStyles();
                return valid ? InputFieldStyle : InputFieldBadStyle;
            }
        }
    }
}
