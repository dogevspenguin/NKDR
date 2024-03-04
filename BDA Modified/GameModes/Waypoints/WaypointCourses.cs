using System;
using System.IO;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using BDArmory.Settings;

namespace BDArmory.GameModes.Waypoints
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class WaypointCourses : MonoBehaviour
    {
        private static WaypointCourses Instance;

        // Interesting spawn locations on Kerbin.
        [WaypointField] public static bool UpdateCourseLocations = true;
        [WaypointField] public static List<WaypointCourse> CourseLocations;
        public static int highestWaypointIndex = 0;

        void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;
            WaypointField.Load();
        }

        void Start()
        {
            BDArmorySettings.WAYPOINT_COURSE_INDEX = Mathf.Clamp(BDArmorySettings.WAYPOINT_COURSE_INDEX, 0, CourseLocations.Count - 1); // Ensure the waypoint index is within limits.
            highestWaypointIndex = CourseLocations.Max(c => c.waypoints.Count) - 1;
        }

        void OnDestroy()
        {
            WaypointField.Save();
        }
    }

    public class Waypoint
    {
        public string name;
        public Vector3 location;
        public float scale;

        public Waypoint(string _name, Vector3 _location, float _scale) { name = _name; location = _location; scale = _scale; }
        public override string ToString() { return name + "| " + location.ToString("G6") + "| " + scale.ToString() + ": "; }
    }

    public class WaypointCourse
    {
        public static string waypointLocationsCfg = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/Waypoint_locations.cfg");
        public string name;
        public int worldIndex;
        public Vector2 spawnPoint;
        public List<Waypoint> waypoints;
        private string waypointList;
        string GetWaypointList()
        {
            waypointList = string.Empty;
            for (int i = 0; i < waypoints.Count; i++)
            {
                waypointList += waypoints[i].ToString();
            }
            return waypointList;
        }
        //COURSE = TestCustom; 1; (23, 23); Start| (23.2, 23.2, 100)| 500: Funnel| (23.2, 23.7, 50)| 250: Ascent| (23.5, 23.6, 250)| 100: Apex| (23.2, 23.4 500)| 500:
        public WaypointCourse(string _name, int _worldIndex, Vector2 _spawnPoint, List<Waypoint> _waypoints) { name = _name; worldIndex = _worldIndex; spawnPoint = _spawnPoint; waypoints = _waypoints; }
        public override string ToString() { return name + "; " + worldIndex + "; " + spawnPoint.ToString("G6") + "; " + GetWaypointList(); }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class WaypointField : Attribute
    {
        public WaypointField() { }
        static List<WaypointCourse> defaultLocations = new List<WaypointCourse>{
            new WaypointCourse("Canyon", 1, new Vector2(27.97f, -39.35f), new List<Waypoint> {
                        new Waypoint("Start", new Vector3(28.33f, -39.11f, 50), 200),
                        new Waypoint("Run-off Corner", new Vector3(28.83f, -38.06f, 50), 200),
                        new Waypoint("Careful River", new Vector3(29.54f, -38.68f, 50), 200),
                        new Waypoint("Lake of Mercy", new Vector3(30.15f, -38.6f, 50), 200),
                        new Waypoint("Danger Zone Narrows", new Vector3(30.83f, -38.87f, 50), 200),
                        new Waypoint("Chicane of Pain", new Vector3(30.73f, -39.6f, 50), 200),
                        new Waypoint("Bumpy Boi Lane", new Vector3(30.9f, -40.23f, 50), 200),
                        new Waypoint("Blaring Straights", new Vector3(30.83f, -41.26f, 50), 200)
                    }),
            new WaypointCourse("Slalom", 1, new Vector2(-21.0158f, 72.2085f), new List<Waypoint> {
                        new Waypoint("Waypoint 0", new Vector3(-21.0763f, 72.7194f, 100), 200),
                        new Waypoint("Waypoint 1", new Vector3(-21.3509f, 73.7466f, 100), 200),
                        new Waypoint("Waypoint 2", new Vector3(-20.8125f, 73.8125f, 100), 200),
                        new Waypoint("Waypoint 3", new Vector3(-20.6478f, 74.8177f, 100), 200),
                        new Waypoint("Waypoint 4", new Vector3(-20.2468f, 74.5046f, 100), 200),
                        new Waypoint("Waypoint 5", new Vector3(-19.7469f, 75.1252f, 100), 200),
                        new Waypoint("Waypoint 6", new Vector3(-19.2360f, 75.1363f, 100), 200),
                        new Waypoint("Waypoint 7", new Vector3(-18.8954f, 74.6530f, 100), 200)
                    }),
            new WaypointCourse("Coast Circuit", 1, new Vector2(-7.7134f, -42.7633f), new List<Waypoint> {
                        new Waypoint("Waypoint 0", new Vector3(-8.1628f, -42.7478f, 50), 200),
                        new Waypoint("Waypoint 1", new Vector3(-8.6737f, -42.7423f, 50), 200),
                        new Waypoint("Waypoint 2", new Vector3(-9.2230f, -42.5208f, 50), 200),
                        new Waypoint("Waypoint 3", new Vector3(-9.6624f, -43.3355f, 50), 200),
                        new Waypoint("Waypoint 4", new Vector3(-10.6732f, -43.3410f, 50), 200),
                        new Waypoint("Waypoint 5", new Vector3(-11.3379f, -42.9236f, 50), 200),
                        new Waypoint("Waypoint 6", new Vector3(-10.9415f, -42.3449f, 50), 200),
                        new Waypoint("Waypoint 7", new Vector3(-10.8591f, -41.8670f, 50), 200),
                        new Waypoint("Waypoint 8", new Vector3(-10.5515f, -41.6198f, 50), 200),
                        new Waypoint("Waypoint 9", new Vector3(-10.4746f, -41.2133f, 50), 200),
                        new Waypoint("Waypoint 10", new Vector3(-9.6945f, -41.2847f, 50), 200),
                        new Waypoint("Waypoint 11", new Vector3(-9.5407f, -42.1911f, 50), 200),
                        new Waypoint("Waypoint 12", new Vector3(-9.1342f, -42.0757f, 50), 200)
                    })
        };

        public static void Save()
        {
            ConfigNode fileNode = ConfigNode.Load(WaypointCourse.waypointLocationsCfg);
            if (fileNode == null)
                fileNode = new ConfigNode();
            if (!fileNode.HasNode("Config"))
                fileNode.AddNode("Config");

            ConfigNode settings = fileNode.GetNode("Config");
            foreach (var field in typeof(WaypointCourses).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (field == null || !field.IsDefined(typeof(WaypointField), false)) continue;
                if (field.Name == "CourseLocations") continue; // We'll do the spawn locations separately.
                var fieldValue = field.GetValue(null);
                settings.SetValue(field.Name, field.GetValue(null).ToString(), true);
            }

            if (!fileNode.HasNode("BDACourseLocations"))
                fileNode.AddNode("BDACourseLocations");

            ConfigNode CourseNode = fileNode.GetNode("BDACourseLocations");

            CourseNode.ClearValues();
            foreach (var course in WaypointCourses.CourseLocations)
            {
                CourseNode.AddValue("COURSE", course.ToString());
            }

            if (!Directory.GetParent(WaypointCourse.waypointLocationsCfg).Exists)
            { Directory.GetParent(WaypointCourse.waypointLocationsCfg).Create(); }
            fileNode.Save(WaypointCourse.waypointLocationsCfg);
        }

        public static void Load()
        {
            ConfigNode fileNode = ConfigNode.Load(WaypointCourse.waypointLocationsCfg);

            WaypointCourses.CourseLocations = new List<WaypointCourse>();
            if (fileNode != null)
            {
                if (fileNode.HasNode("Config"))
                {
                    ConfigNode settings = fileNode.GetNode("Config");
                    foreach (var field in typeof(WaypointCourses).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (field == null || !field.IsDefined(typeof(WaypointField), false)) continue;
                        if (field.Name == "CourseLocations") continue; // We'll do the spawn locations separately.
                        if (!settings.HasValue(field.Name)) continue;
                        object parsedValue = ParseValue(field.FieldType, settings.GetValue(field.Name));
                        if (parsedValue != null)
                        {
                            field.SetValue(null, parsedValue);
                        }
                    }
                }

                if (fileNode.HasNode("BDACourseLocations"))
                {
                    ConfigNode settings = fileNode.GetNode("BDACourseLocations");
                    foreach (var courseLocation in settings.GetValues("COURSE"))
                    {
                        var parsedValue = (WaypointCourse)ParseValue(typeof(WaypointCourse), courseLocation);
                        if (parsedValue != null)
                        {
                            WaypointCourses.CourseLocations.Add(parsedValue);
                        }
                    }
                }
            }

            // Add defaults if they're missing and we're not instructed not to.
            if (WaypointCourses.UpdateCourseLocations)
            {
                foreach (var location in defaultLocations.ToList())
                    if (!WaypointCourses.CourseLocations.Select(l => l.name).ToList().Contains(location.name))
                        WaypointCourses.CourseLocations.Add(location);
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
                else if (type == typeof(float))
                {
                    return float.Parse(value);
                }
                else if (type == typeof(Vector2))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    if (strings.Length == 2)
                    {
                        float x = float.Parse(strings[0]);
                        float y = float.Parse(strings[1]);
                        return new Vector2(x, y);
                    }
                }
                else if (type == typeof(Vector3))
                {
                    char[] charsToTrim = { '[', ']', '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    float x = float.Parse(strings[0]);
                    float y = float.Parse(strings[1]);
                    float z = float.Parse(strings[2]);
                    return new Vector3(x, y, z);
                }
                else if (type == typeof(WaypointCourse))
                {
                    string[] parts;

                    parts = value.Split(new char[] { ';' });
                    if (parts.Length > 1)
                    {
                        var name = (string)ParseValue(typeof(string), parts[0]);
                        var worldIndex = (int)ParseValue(typeof(int), parts[1]);
                        var spawnPoint = (Vector2)ParseValue(typeof(Vector2), parts[2]);
                        string[] waypoints = parts[3].Split(new char[] { ':' });
                        List<Waypoint> waypointList = new List<Waypoint>();
                        for (int i = 0; i < waypoints.Length - 1; i++)
                        {
                            string[] datavars;
                            datavars = waypoints[i].Split(new char[] { '|' });
                            string WPname = (string)ParseValue(typeof(string), datavars[0]);
                            WPname = WPname.Trim(' ');
                            if (string.IsNullOrEmpty(WPname)) WPname = $"Waypoint {i}";
                            var location = (Vector3)ParseValue(typeof(Vector3), datavars[1]);
                            var scale = (float)ParseValue(typeof(float), datavars[2]);
                            if (name != null && location != null)
                                waypointList.Add(new Waypoint(WPname, location, scale));
                        }

                        if (name != null && spawnPoint != null && waypointList.Count > 0)
                            return new WaypointCourse(name, worldIndex, spawnPoint, waypointList);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.LogError("[BDArmory.WaypointCourses]: Failed to parse settings field of type " + type + " and value " + value);
            return null;
        }
    }
}
