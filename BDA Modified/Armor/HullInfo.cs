using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using BDArmory.Utils;

namespace BDArmory.Armor
{
    public class HullInfo
    {
        public string name { get; private set; } //internal name
        public string localizedName { get; private set; } //display name
        public float massMod { get; private set; } //mass modifier
        public float costMod { get; private set; } //cost modifier
        public float healthMod { get; private set; } //health modifier
        public float ignitionTemp { get; private set; } //can material catch fire?
        public float maxTemp { get; private set; } //In Kelvin, determines max temp material can sustain before part is destroyed
        public float ImpactMod { get; private set; } //impact tolerance modifier
        public float radarMod { get; private set; } //radar reflectivity modifier, if no armor/radar-transparent armor

        public static HullInfos materials;
        public static List<string> materialNames;
        public static HullInfo defaultMaterial;

        public HullInfo(string name, string localizedName, float massMod, float costMod, float healthMod, float ignitionTemp, float maxTemp, float ImpactMod, float radarMod)
        {
            this.name = name;
            this.localizedName = localizedName;
            this.massMod = massMod;
            this.costMod = costMod;
            this.healthMod = healthMod;
            this.ignitionTemp = ignitionTemp;
            this.maxTemp = maxTemp;
            this.ImpactMod = ImpactMod;
            this.radarMod = radarMod;
            this.radarMod = radarMod;
        }

        public static void Load()
        {
            if (materials != null) return; // Only load the armor defs once on startup.
            materials = new HullInfos();
            if (materialNames == null) materialNames = new List<string>();
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("MATERIAL");
            ConfigNode node;

            // First locate BDA's default armor definition so we can fill in missing fields.
            if (defaultMaterial == null)
                for (int i = 0; i < nodes.Length; ++i)
                {
                    if (nodes[i].parent.name != "BD_Materials") continue; // Ignore other config files.
                    node = nodes[i].config;
                    if (!node.HasValue("name") || (string)ParseField(nodes[i].config, "name", typeof(string)) != "def") continue; // Ignore other configs.
                    Debug.Log("[BDArmory.MaterialInfo]: Parsing default material definition from " + nodes[i].parent.name);
                    defaultMaterial = new HullInfo(
                        "def",
                        (string)ParseField(node, "localizedName", typeof(string)),
                        (float)ParseField(node, "massMod", typeof(float)),
                        (float)ParseField(node, "costMod", typeof(float)),
                        (float)ParseField(node, "healthMod", typeof(float)),
                        (float)ParseField(node, "ignitionTemp", typeof(float)),
                        (float)ParseField(node, "maxTemp", typeof(float)),
                        1,
                        1 //(float)ParseField(node, "ImpactMod", typeof(float))
                    );
                    materials.Add(defaultMaterial);
                    materialNames.Add("def");
                    break;
                }
            if (defaultMaterial == null) throw new ArgumentException("Failed to find BDArmory's default material definition.", "defaultMaterial");

            // Now add in the rest of the materials.
            for (int i = 0; i < nodes.Length; i++)
            {
                string name_ = "";
                try
                {
                    node = nodes[i].config;
                    name_ = (string)ParseField(node, "name", typeof(string));
                    if (materialNames.Contains(name_)) // Avoid duplicates.
                    {
                        if (nodes[i].parent.name != "BD_Materials" || name_ != "def") // Don't report the default bullet definition as a duplicate.
                            Debug.LogError("[BDArmory.MaterialInfo]: Material definition " + name_ + " from " + nodes[i].parent.name + " already exists, skipping.");
                        continue;
                    }
                    Debug.Log("[BDArmory.MaterialInfo]: Parsing definition of material " + name_ + " from " + nodes[i].parent.name);
                    materials.Add(
                        new HullInfo(
                            name_,
                        (string)ParseField(node, "localizedName", typeof(string)),
                        (float)ParseField(node, "massMod", typeof(float)),
                        (float)ParseField(node, "costMod", typeof(float)),
                        (float)ParseField(node, "healthMod", typeof(float)),
                        (float)ParseField(node, "ignitionTemp", typeof(float)),
                        (float)ParseField(node, "maxTemp", typeof(float)),
                        (float)ParseField(node, "ImpactMod", typeof(float)),
                        (float)ParseField(node, "radarMod", typeof(float))
                        )
                    );
                    materialNames.Add(name_);
                }
                catch (Exception e)
                {
                    Debug.LogError("[BDArmory.aterialInfo]: Error Loading Material Config '" + name_ + "' | " + e.ToString());
                }
            }
            //once armors are loaded, remove the def armor so it isn't found in later list parsings by HitpointTracker when updating parts armor
            materials.Remove(defaultMaterial);
            materialNames.Remove("def");
        }

        private static object ParseField(ConfigNode node, string field, Type type)
        {
            try
            {
                if (!node.HasValue(field))
                    throw new ArgumentNullException(field, "Field '" + field + "' is missing.");
                var value = node.GetValue(field);
                try
                {
                    if (type == typeof(string))
                    { return value; }
                    else if (type == typeof(bool))
                    { return bool.Parse(value); }
                    else if (type == typeof(int))
                    { return int.Parse(value); }
                    else if (type == typeof(float))
                    { return float.Parse(value); }
                    else
                    { throw new ArgumentException("Invalid type specified."); }
                }
                catch (Exception e)
                { throw new ArgumentException("Field '" + field + "': '" + value + "' could not be parsed as '" + type.ToString() + "' | " + e.ToString(), field); }
            }
            catch (Exception e)
            {
                if (defaultMaterial != null)
                {
                    // Give a warning about the missing or invalid value, then use the default value using reflection to find the field.
                    var defaultValue = typeof(HullInfo).GetProperty(field, BindingFlags.Public | BindingFlags.Instance).GetValue(defaultMaterial);
                    Debug.LogError("[BDArmory.MaterialInfo]: Using default value of " + defaultValue.ToString() + " for " + field + " | " + e.ToString());
                    return defaultValue;
                }
                else
                    throw;
            }
        }
    }

    public class HullInfos : List<HullInfo>
    {
        public HullInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}