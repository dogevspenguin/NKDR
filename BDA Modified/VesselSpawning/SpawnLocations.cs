using System;
using System.IO;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace BDArmory.VesselSpawning
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SpawnLocations : MonoBehaviour
    {
        private static SpawnLocations Instance;

        // Interesting spawn locations on Kerbin.
        [VesselSpawnerField] public static bool UpdateSpawnLocations = true;
        [VesselSpawnerField] public static List<SpawnLocation> spawnLocations;

        void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;

            VesselSpawnerField.Load();
        }

        void OnDestroy()
        {
            VesselSpawnerField.Save();
        }
    }

    public class SpawnLocation
    {
        public static string oldSpawnLocationsCfg = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/spawn_locations.cfg");
        public static string spawnLocationsCfg = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/spawn_locations.cfg");

        public string name;
        public Vector2d location;
        public int worldIndex;

        public SpawnLocation(string _name, Vector2d _location, int _worldIndex) { name = _name; location = _location; worldIndex = _worldIndex; }
        public override string ToString() { return name + "; " + location.ToString("G6") + "; " + worldIndex.ToString(); }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class VesselSpawnerField : Attribute
    {
        public VesselSpawnerField() { }
        //static Dictionary<String, SpawnLocation> defaultLocations = new Dictionary<string, SpawnLocation>{
        static List<SpawnLocation> defaultLocations = new List<SpawnLocation>{
            new SpawnLocation("KSC", new Vector2d(-0.04762, -74.8593), 1),
            new SpawnLocation("Inland KSC", new Vector2d(20.5939, -146.567), 1),
            new SpawnLocation("Desert Runway", new Vector2d(-6.44958, -144.038), 1),
            new SpawnLocation("Kurgan's spot", new Vector2d(-28.4595, -9.15156), 1),
            new SpawnLocation("Alpine Lake", new Vector2d(-23.48, 119.83), 1),
            new SpawnLocation("Big Canyon", new Vector2d(6.97865, -170.804), 1),
            new SpawnLocation("Bowl 1", new Vector2d(35.6559, -77.4941), 1),
            new SpawnLocation("Bowl 2", new Vector2d(3.8744, -78.0039), 1),
            new SpawnLocation("Bowl 3", new Vector2d(0.268284, -80.5195), 1),
            new SpawnLocation("Bowl 4", new Vector2d(-2.962, 179.91), 1),
            new SpawnLocation("Bowl 5", new Vector2d(47.16, 134.08), 1),
            new SpawnLocation("Canyon", new Vector2d(-52.7592, -4.71081), 1),
            new SpawnLocation("Colorado", new Vector2d(41.715, 82.29), 1),
            new SpawnLocation("Crater Isle", new Vector2d(8.159, 179.65), 1),
            new SpawnLocation("Crater Lake", new Vector2d(-18.86, 66.47), 1),
            new SpawnLocation("Crater Sea", new Vector2d(7.213, -177.34), 1),
            new SpawnLocation("East Peninsula", new Vector2d(-1.57, -39.12), 1),
            new SpawnLocation("Great Lake", new Vector2d(-31.958, 81.654), 1),
            new SpawnLocation("Half-pipe", new Vector2d(-21.1388, 72.6437), 1),
            new SpawnLocation("Ice field", new Vector2d(80.3343, -32.0119), 1),
            new SpawnLocation("Kermau-Sur-Mer", new Vector2d(33.911, -172.26), 1),
            new SpawnLocation("Land Bridge", new Vector2d(-48.055, 13.33), 1),
            new SpawnLocation("Lonely Mt", new Vector2d(24.48, -116.444), 1),
            new SpawnLocation("Manley Delta", new Vector2d(39.0705, -136.193), 1),
            new SpawnLocation("Manley Valley", new Vector2d(45.6, -137.3), 1),
            new SpawnLocation("Marshlands", new Vector2d(16.83, -162.813), 1),
            new SpawnLocation("Mountain Bowl", new Vector2d(21.772, -112.569), 1),
            new SpawnLocation("Mtn. Springs", new Vector2d(30.6516, -40.6589), 1),
            new SpawnLocation("Oasis", new Vector2d(10.3, -121.2), 1),
            new SpawnLocation("Oyster Bay", new Vector2d(8.342, 85.613), 1),
            new SpawnLocation("Penninsula", new Vector2d(-1.2664, -106.896), 1),
            new SpawnLocation("Pyramids", new Vector2d(-6.4743, -141.662), 1),
            new SpawnLocation("Src of deNile", new Vector2d(28.8112, -134.795), 1),
            new SpawnLocation("Suez", new Vector2d(10.6178, -97.0315), 1),
            new SpawnLocation("The Scar", new Vector2d(16.88, 50.48), 1),
            new SpawnLocation("Western Approach", new Vector2d(0.2, -84.26), 1),
            new SpawnLocation("White Cliffs", new Vector2d(25.689, -144.14), 1),
            new SpawnLocation("Ice Floe 1", new Vector2d(-73.0986, -114.983), 1),
            new SpawnLocation("Ice Floe 2", new Vector2d(-71.0594, 60.3108), 1),
            new SpawnLocation("Joolian Skies", new Vector2d(0.05096, -74.8016), 8),
            new SpawnLocation("Great Sea", new Vector2d(0.05096, -74.8016), 9),
            new SpawnLocation("Impact Basin", new Vector2d(15.667, -65.1566), 9),
            new SpawnLocation("Crater Cove", new Vector2d(34.7921, 161.095), 9),
            new SpawnLocation("Battle Pond", new Vector2d(1.33667, 150.643), 9),
            new SpawnLocation("Tri-Eye Isle", new Vector2d(5.16308, -169.655), 9),
            new SpawnLocation("Bayou", new Vector2d(33.306, -130.172), 5),
            new SpawnLocation("Poison Pond", new Vector2d(33.3616, -67.2242), 5),
            new SpawnLocation("Crater Isle", new Vector2d(6.03858, 2.62539), 5),
            new SpawnLocation("Sunken Crater", new Vector2d(23.1178, -42.8307), 5),
            new SpawnLocation("Bowl 1", new Vector2d(36.0241, 105.294), 5),
            new SpawnLocation("Polar Bowl", new Vector2d(45.9978, 115.843), 6),
            new SpawnLocation("Grand Canyon", new Vector2d(9.32963, 167.071), 6),
            new SpawnLocation("Grand Canal", new Vector2d(-0.08521, -60.6124), 6),
            new SpawnLocation("Polar Bowl 2", new Vector2d(-53.655, -32.4155), 6),
        };
        public static void Save()
        {
            ConfigNode fileNode = ConfigNode.Load(SpawnLocation.spawnLocationsCfg);
            if (fileNode == null)
                fileNode = new ConfigNode();
            if (!fileNode.HasNode("Config"))
                fileNode.AddNode("Config");

            ConfigNode settings = fileNode.GetNode("Config");
            foreach (var field in typeof(SpawnLocations).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (field == null || !field.IsDefined(typeof(VesselSpawnerField), false)) continue;
                if (field.Name == "spawnLocations") continue; // We'll do the spawn locations separately.
                var fieldValue = field.GetValue(null);
                settings.SetValue(field.Name, field.GetValue(null).ToString(), true);
            }

            if (!fileNode.HasNode("BDASpawnLocations"))
                fileNode.AddNode("BDASpawnLocations");

            ConfigNode spawnLocations = fileNode.GetNode("BDASpawnLocations");

            spawnLocations.ClearValues();
            foreach (var spawnLocation in SpawnLocations.spawnLocations)
                spawnLocations.AddValue("LOCATION", spawnLocation.ToString());

            if (!Directory.GetParent(SpawnLocation.spawnLocationsCfg).Exists)
            { Directory.GetParent(SpawnLocation.spawnLocationsCfg).Create(); }
            var success = fileNode.Save(SpawnLocation.spawnLocationsCfg);
            if (success && File.Exists(SpawnLocation.oldSpawnLocationsCfg)) // Remove the old settings if it exists and the new settings were saved.
            { File.Delete(SpawnLocation.oldSpawnLocationsCfg); }
        }

        public static void Load()
        {
            ConfigNode fileNode = ConfigNode.Load(SpawnLocation.spawnLocationsCfg);
            if (fileNode == null)
            {
                fileNode = ConfigNode.Load(SpawnLocation.oldSpawnLocationsCfg); // Try the old location.
            }
            SpawnLocations.spawnLocations = new List<SpawnLocation>();
            if (fileNode != null)
            {
                if (fileNode.HasNode("Config"))
                {
                    ConfigNode settings = fileNode.GetNode("Config");
                    foreach (var field in typeof(SpawnLocations).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (field == null || !field.IsDefined(typeof(VesselSpawnerField), false)) continue;
                        if (field.Name == "spawnLocations") continue; // We'll do the spawn locations separately.
                        if (!settings.HasValue(field.Name)) continue;
                        object parsedValue = ParseValue(field.FieldType, settings.GetValue(field.Name));
                        if (parsedValue != null)
                        {
                            field.SetValue(null, parsedValue);
                        }
                    }
                }

                if (fileNode.HasNode("BDASpawnLocations"))
                {
                    ConfigNode settings = fileNode.GetNode("BDASpawnLocations");
                    foreach (var spawnLocation in settings.GetValues("LOCATION"))
                    {
                        var parsedValue = (SpawnLocation)ParseValue(typeof(SpawnLocation), spawnLocation);
                        if (parsedValue != null)
                        {
                            SpawnLocations.spawnLocations.Add(parsedValue);
                        }
                    }
                }
            }

            // Add defaults if they're missing and we're not instructed not to.
            if (SpawnLocations.UpdateSpawnLocations)
            {
                foreach (var location in defaultLocations.ToList())
                    if (!SpawnLocations.spawnLocations.Select(l => l.name).ToList().Contains(location.name))
                        SpawnLocations.spawnLocations.Add(location);
            }
        }

        public static object ParseValue(Type type, string value)
        {
            try
            {
                if (type == typeof(string))
                {
                    return value;
                }
                else if (type == typeof(bool))
                {
                    return bool.Parse(value);
                }
                else if (type == typeof(int))
                {
                    return int.Parse(value);
                }
                else if (type == typeof(Vector2d))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    if (strings.Length == 2)
                    {
                        double x = double.Parse(strings[0]);
                        double y = double.Parse(strings[1]);
                        return new Vector2d(x, y);
                    }
                }
                else if (type == typeof(SpawnLocation))
                {
                    string[] parts;
                    if (!value.Contains(';')) parts = value.Split(new char[] { ',' }, 2); // Old spawn location format.
                    else parts = value.Split(new char[] { ';' }); // New spawn location format.
                    if (parts.Length > 1)
                    {
                        var name = (string)ParseValue(typeof(string), parts[0]);
                        var location = (Vector2d)ParseValue(typeof(Vector2d), parts[1]);
                        var worldIndex = parts.Length > 2 ? (int)ParseValue(typeof(int), parts[2]) : 1; // Default to Kerbin for upgrading old spawn locations.
                        if (name != null && location != null)
                            return new SpawnLocation(name, location, worldIndex);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.LogError("[BDArmory.SpawnLocations]: Failed to parse settings field of type " + type + " and value " + value);
            return null;
        }
    }
}
