using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniLinq;
using UnityEngine;

namespace BDArmory.Settings
{
    [AttributeUsage(AttributeTargets.Field)]
    public class BDAPersistentSettingsField : Attribute
    {
        public BDAPersistentSettingsField()
        {
        }

        /// <summary>
        /// Save the current settings to the specified path.
        /// </summary>
        /// <param name="path"></param>
        public static void Save(string path)
        {
            ConfigNode fileNode = ConfigNode.Load(path);
            if (fileNode == null) fileNode = new ConfigNode();
            fileNode.SetValue("VERSION", UI.BDArmorySetup.Version, true);

            if (!fileNode.HasNode("BDASettings"))
            {
                fileNode.AddNode("BDASettings");
            }

            ConfigNode settings = fileNode.GetNode("BDASettings");
            using (IEnumerator<FieldInfo> field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator())
                while (field.MoveNext())
                {
                    try
                    {
                        if (field.Current == null) continue;
                        if (!field.Current.IsDefined(typeof(BDAPersistentSettingsField), false)) continue;

                        var fieldValue = field.Current.GetValue(null);
                        if (fieldValue.GetType() == typeof(Vector3d))
                        {
                            settings.SetValue(field.Current.Name, ((Vector3d)fieldValue).ToString("G"), true);
                        }
                        else if (fieldValue.GetType() == typeof(Vector2d))
                        {
                            settings.SetValue(field.Current.Name, ((Vector2d)fieldValue).ToString("G"), true);
                        }
                        else if (fieldValue.GetType() == typeof(Vector2))
                        {
                            settings.SetValue(field.Current.Name, ((Vector2)fieldValue).ToString("G"), true);
                        }
                        else if (fieldValue.GetType() == typeof(List<string>))
                        {
                            settings.SetValue(field.Current.Name, string.Join("; ", (List<string>)fieldValue), true);
                        }
                        else
                        {
                            settings.SetValue(field.Current.Name, fieldValue.ToString(), true);
                        }
                    }
                    catch
                    {
                        Debug.LogError($"[BDArmory.BDAPersistentSettingsField]: Exception triggered while trying to save field {field.Current.Name} with value {field.Current.GetValue(null)}");
                        throw;
                    }
                }
            fileNode.Save(path);
        }

        /// <summary>
        /// Load the settings from the default path.
        /// </summary>
        public static void Load()
        {
            ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
            if (!fileNode.HasNode("BDASettings")) return;

            ConfigNode settings = fileNode.GetNode("BDASettings");

            using (IEnumerator<FieldInfo> field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator())
                while (field.MoveNext())
                {
                    if (field.Current == null) continue;
                    if (!field.Current.IsDefined(typeof(BDAPersistentSettingsField), false)) continue;

                    if (!settings.HasValue(field.Current.Name)) continue;
                    object parsedValue = ParseValue(field.Current.FieldType, settings.GetValue(field.Current.Name));
                    if (parsedValue != null)
                    {
                        field.Current.SetValue(null, parsedValue);
                    }
                }
        }

        /// <summary>
        /// Check for settings that have been upgraded since the previously run version.
        /// </summary>
        public static void Upgrade()
        {
            ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
            ConfigNode oldDefaults = ConfigNode.Load(Path.ChangeExtension(BDArmorySettings.settingsConfigURL, ".default"));

            string version = "Unknown";
            if (fileNode.HasValue("VERSION"))
            {
                version = (string)fileNode.GetValue("VERSION");
                if (version == UI.BDArmorySetup.Version) return; // Already up to date. Do nothing.
            }
            Save(Path.ChangeExtension(BDArmorySettings.settingsConfigURL, ".default")); // Save the new defaults to settings.default

            if (!fileNode.HasNode("BDASettings")) return; // No settings, so they'll get generated on the first save.

            // Save the current settings to settings.old.
            Debug.LogWarning($"[BDArmory.Settings]: BDArmory version differs from previous run: {UI.BDArmorySetup.Version} vs {version}. Saving previous config to {Path.ChangeExtension(BDArmorySettings.settingsConfigURL, ".old")} and upgrading settings.");
            fileNode.Save(Path.ChangeExtension(BDArmorySettings.settingsConfigURL, ".old"));

            ConfigNode oldSettings = oldDefaults != null ? oldDefaults.GetNode("BDASettings") : null;
            if (oldSettings == null)
            {
                Debug.LogWarning($"[BDArmory.Settings]: No previous default settings found, unable to check for default vs user changes.");
                return;
            }

            var excludedFields = new HashSet<string> { "LAST_USED_SAVEGAME", }; // A bunch of other stuff is also excluded below.
            ConfigNode settings = fileNode.GetNode("BDASettings");
            using (var field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator())
                while (field.MoveNext())
                {
                    if (field.Current == null) continue;
                    if (!field.Current.IsDefined(typeof(BDAPersistentSettingsField), false)) continue;

                    bool skip = false;
                    if (excludedFields.Contains(field.Current.Name)) skip = true; // Skip excluded fields.
                    if (field.Current.Name.StartsWith("REMOTE_")) skip = true; // Skip remote API stuff.
                    if (field.Current.Name.StartsWith("EVOLUTION_")) skip = true; // Skip evolution stuff.
                    if (field.Current.Name.EndsWith("_WIDTH")) skip = true; // Skip window width stuff.
                    if (field.Current.Name.EndsWith("_OPTIONS")) skip = true; // Skip various section toggles.
                    if (field.Current.Name.EndsWith("_SETTINGS_TOGGLE")) skip = true; // Skip various section toggles.

                    if (!settings.HasValue(field.Current.Name)) continue;
                    object currentValue = ParseValue(field.Current.FieldType, settings.GetValue(field.Current.Name));
                    if (currentValue == null) continue;
                    var defaultValue = field.Current.GetValue(null);
                    if (!skip && currentValue is IComparable && ((IComparable)defaultValue).CompareTo((IComparable)currentValue) != 0) // The current value doesn't match the default. Note: Vector2d, Vector3d and List are not IComparable.
                    {
                        if (oldSettings.HasValue(field.Current.Name))
                        {
                            object oldDefaultValue = ParseValue(field.Current.FieldType, oldSettings.GetValue(field.Current.Name));
                            if (((IComparable)oldDefaultValue).CompareTo((IComparable)currentValue) == 0) // The current value matches the old default => upgrade it.
                            {
                                Debug.Log($"[BDArmory.Settings]: Upgrading {field.Current.Name} to the default {defaultValue}, from {currentValue}.");
                                field.Current.SetValue(null, defaultValue);
                                continue;
                            }
                            else Debug.Log($"[BDArmory.Settings]: {field.Current.Name} with value {currentValue} doesn't match either of the current or previous defaults, assuming it was modified by the user.");
                        }
                        else Debug.Log($"[BDArmory.Settings]: {field.Current.Name} with value {currentValue} doesn't match the current default and didn't exist in the previous defaults, assuming it was modified by the user.");
                    }
                    field.Current.SetValue(null, currentValue); // Use the current value.
                }
            Save(BDArmorySettings.settingsConfigURL); // Overwrite the settings with the modified ones.
        }

        public static object ParseValue(Type type, string value)
        {
            try
            {
                if (type == typeof(string))
                {
                    return value;
                }

                if (type == typeof(bool))
                {
                    return bool.Parse(value);
                }
                else if (type.IsEnum)
                {
                    return System.Enum.Parse(type, value);
                }
                else if (type == typeof(float))
                {
                    return float.Parse(value);
                }
                else if (type == typeof(int))
                {
                    return int.Parse(value);
                }
                else if (type == typeof(float))
                {
                    return float.Parse(value);
                }
                else if (type == typeof(Rect))
                {
                    string[] strings = value.Split(',');
                    int xVal = int.Parse(strings[0].Split(':')[1].Split('.')[0]);
                    int yVal = int.Parse(strings[1].Split(':')[1].Split('.')[0]);
                    int wVal = int.Parse(strings[2].Split(':')[1].Split('.')[0]);
                    int hVal = int.Parse(strings[3].Split(':')[1].Split('.')[0]);
                    Rect rectVal = new Rect
                    {
                        x = xVal,
                        y = yVal,
                        width = wVal,
                        height = hVal
                    };
                    return rectVal;
                }
                else if (type == typeof(Vector2))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    float x = float.Parse(strings[0]);
                    float y = float.Parse(strings[1]);
                    return new Vector2(x, y);
                }
                else if (type == typeof(Vector2d))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    double x = double.Parse(strings[0]);
                    double y = double.Parse(strings[1]);
                    return new Vector2d(x, y);
                }
                else if (type == typeof(Vector3d))
                {
                    char[] charsToTrim = { '[', ']', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    double x = double.Parse(strings[0]);
                    double y = double.Parse(strings[1]);
                    double z = double.Parse(strings[2]);
                    return new Vector3d(x, y, z);
                }
                else if (type == typeof(Vector2Int))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    int x = int.Parse(strings[0]);
                    int y = int.Parse(strings[1]);
                    return new Vector2Int(x, y);
                }
                else if (type == typeof(List<string>))
                {
                    return value.Split(new string[] { "; " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BDArmory.BDAPersistantSettingsField]: Failed to parse '" + value + "' as a " + type.ToString() + ": " + e.Message);
                return null;
            }
            Debug.LogError("[BDArmory.BDAPersistantSettingsField]: BDAPersistantSettingsField to parse settings field of type " + type + " and value " + value);
            return null;
        }
    }
}
