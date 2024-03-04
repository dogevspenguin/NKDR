using BDArmory.Settings;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BDArmory.Utils
{
    public static class OtherUtils // FIXME Suggestions for a better name?
    {
        /// <summary>
        /// Parses the string to a curve.
        /// Format: "key:pair,key:pair"
        /// </summary>
        /// <returns>The curve.</returns>
        /// <param name="curveString">Curve string.</param>
        public static FloatCurve ParseCurve(string curveString)
        {
            string[] pairs = curveString.Split(new char[] { ',' });
            Keyframe[] keys = new Keyframe[pairs.Length];
            for (int p = 0; p < pairs.Length; p++)
            {
                string[] pair = pairs[p].Split(new char[] { ':' });
                keys[p] = new Keyframe(float.Parse(pair[0]), float.Parse(pair[1]));
            }

            FloatCurve curve = new FloatCurve(keys);

            return curve;
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            // TODO: Argument validation
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        private const int lineOfSightLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.EVA | LayerMasks.Unknown19 | LayerMasks.Unknown23 | LayerMasks.Wheels);
        public static bool CheckSightLine(Vector3 origin, Vector3 target, float maxDistance, float threshold,
            float startDistance)
        {
            float dist = maxDistance;
            Ray ray = new Ray(origin, target - origin);
            ray.origin += ray.direction * startDistance;
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, dist, lineOfSightLayerMask))
            {
                if ((target - rayHit.point).sqrMagnitude < threshold * threshold)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public static bool CheckSightLineExactDistance(Vector3 origin, Vector3 target, float maxDistance,
            float threshold, float startDistance)
        {
            float dist = maxDistance;
            Ray ray = new Ray(origin, target - origin);
            ray.origin += ray.direction * startDistance;
            RaycastHit rayHit;

            if (Physics.Raycast(ray, out rayHit, dist, lineOfSightLayerMask))
            {
                if ((target - rayHit.point).sqrMagnitude < threshold * threshold)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static float[] ParseToFloatArray(string floatString)
        {
            string[] floatStrings = floatString.Split(new char[] { ',' });
            float[] floatArray = new float[floatStrings.Length];
            for (int i = 0; i < floatStrings.Length; i++)
            {
                floatArray[i] = float.Parse(floatStrings[i]);
            }

            return floatArray;
        }

        public static KeyBinding AGEnumToKeybinding(KSPActionGroup group)
        {
            string groupName = group.ToString();
            if (groupName.Contains("Custom"))
            {
                groupName = groupName.Substring(6);
                int customNumber = int.Parse(groupName);
                groupName = "CustomActionGroup" + customNumber;
            }
            else
            {
                return null;
            }

            FieldInfo field = typeof(GameSettings).GetField(groupName);
            return (KeyBinding)field.GetValue(null);
        }

        public static string JsonCompat(string json)
        {
            return json.Replace('{', '<').Replace('}', '>');
        }

        public static string JsonDecompat(string json)
        {
            return json.Replace('<', '{').Replace('>', '}');
        }

        public static void SetTimeOverride(bool enabled)
        {
            BDArmorySettings.TIME_OVERRIDE = enabled;
            Time.timeScale = enabled ? BDArmorySettings.TIME_SCALE : 1f;
        }

        // LINQ.All() returns true for empty collections. Sometimes we want it to be false in those cases.
        public static bool AllAndNotEmpty<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source.Any() && source.All(predicate);
        }
    }

    /// <summary>
    /// Custom yield instruction that allows waiting for a number of seconds based on the FixedUpdate cycle instead of the Update cycle.
    /// Based on http://answers.unity.com/comments/1910230/view.html
    /// 
    /// Note: All Unity yield instructions other than WaitForFixedUpdate wait until the next Update cycle to check their conditions, including "yield return null".
    ///       For any yielding that is physics related, use WaitForFixedUpdate (use a single instance and yield it multiple times) or one of the classes below.
    /// </summary>
    public class WaitForSecondsFixed : IEnumerator
    {
        private WaitForFixedUpdate wait = new WaitForFixedUpdate();
        public virtual object Current => this.wait;
        float endTime, seconds;

        public WaitForSecondsFixed(float seconds)
        {
            this.seconds = seconds;
            this.Reset();
        }

        public bool MoveNext() => this.keepWaiting;
        public virtual bool keepWaiting => (Time.fixedTime < endTime);
        public virtual void Reset() => this.endTime = Time.fixedTime + this.seconds;
    }

    /// <summary>
    /// Custom yield instruction that allows yielding until a predicate is satisfied based on the FixedUpdate cycle instead of the Update cycle.
    /// </summary>
    public class WaitUntilFixed : IEnumerator
    {
        private WaitForFixedUpdate wait = new WaitForFixedUpdate();
        public virtual object Current => wait;
        Func<bool> predicate;

        public WaitUntilFixed(Func<bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool MoveNext() => !predicate();
        public virtual void Reset() { }
    }

    /// <summary>
    /// Custom yield instruction that allows yielding while a predicate is satisfied based on the FixedUpdate cycle instead of the Update cycle.
    /// </summary>
    public class WaitWhileFixed : IEnumerator
    {
        private WaitForFixedUpdate wait = new WaitForFixedUpdate();
        public virtual object Current => wait;
        Func<bool> predicate;

        public WaitWhileFixed(Func<bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool MoveNext() => predicate();
        public virtual void Reset() { }
    }

    public enum Toggle { On, Off, Toggle, NoChange }; // Turn something on, off, toggle it or leave it as it is.


    /// <summary>
    /// For serializing List<List<string>>
    /// </summary>
    [Serializable]
    public struct StringList
    {
        public List<string> ls;
    }
}