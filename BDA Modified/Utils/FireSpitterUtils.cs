using System;
using System.Reflection;
using UnityEngine;

using BDArmory.Settings;

namespace BDArmory.Utils
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FireSpitter : MonoBehaviour
    {
        public static FireSpitter Instance;
        public static bool hasFireSpitter = false;
        private static bool hasCheckedForFS = false;
        public static bool hasFSEngine = false;
        private static bool hasCheckedForFSEngine = false;

        public static Assembly FSAssembly;
        public static Type FSEngineType;


        void Awake()
        {
            if (Instance != null) return; // Don't replace existing instance.
            Instance = new FireSpitter();
        }

        void Start()
        {
            CheckForFireSpitter();
            if (hasFireSpitter) CheckForFSEngine();
        }

        public static bool CheckForFireSpitter()
        {
            if (hasCheckedForFS) return hasFireSpitter;
            hasCheckedForFS = true;
            foreach (var assy in AssemblyLoader.loadedAssemblies)
            {
                if (assy.assembly.FullName.StartsWith("Firespitter"))
                {
                    FSAssembly = assy.assembly;
                    hasFireSpitter = true;
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FireSpitter]: Found FireSpitter Assembly: {FSAssembly.FullName}");
                }
            }
            return hasFireSpitter;
        }

        public static bool CheckForFSEngine()
        {
            if (!hasFireSpitter) return false;
            if (hasCheckedForFSEngine) return hasFSEngine;
            hasCheckedForFSEngine = true;
            foreach (var type in FSAssembly.GetTypes())
            {
                if (type.Name == "FSengine")
                {
                    FSEngineType = type;
                    hasFSEngine = true;
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FireSpitter]: Found FSengine type.");
                }
            }
            return hasFSEngine;
        }

        public static void ActivateFSEngines(Vessel vessel, bool activate = true)
        {
            if (!hasFSEngine) return;
            foreach (var part in vessel.Parts)
            {
                foreach (var module in part.Modules)
                {
                    if (module.GetType() == FSEngineType || module.GetType().IsSubclassOf(FSEngineType))
                    {
                        if (activate)
                        {
                            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FireSpitter]: Found {module} on {vessel.vesselName}, attempting to call 'Activate'.");
                            FSEngineType.InvokeMember("Activate", BindingFlags.InvokeMethod, null, module, new object[] { }); // Note: this activates the engines, but the throttle on the engines aren't controlled unless they're on the active vessel.
                        }
                        else
                        {
                            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FireSpitter]: Found {module} on {vessel.vesselName}, attempting to call 'Shutdown'.");
                            FSEngineType.InvokeMember("Shutdown", BindingFlags.InvokeMethod, null, module, new object[] { });
                        }
                    }
                }
            }
        }

        public static int CountActiveEngines(Vessel vessel)
        {
            if (!hasFSEngine) return 0;
            int activeEngines = 0;
            foreach (var part in vessel.Parts)
            {
                foreach (var module in part.Modules)
                {
                    if (module.GetType() == FSEngineType || module.GetType().IsSubclassOf(FSEngineType))
                    {
                        if ((bool)FSEngineType.GetField("EngineIgnited", BindingFlags.Public | BindingFlags.Instance).GetValue(module))
                            ++activeEngines;
                    }
                }
            }
            return activeEngines;
        }

        public static void CheckStatus(Vessel vessel)
        {
            if (!hasFSEngine) return;
            foreach (var part in vessel.Parts)
            {
                foreach (var module in part.Modules)
                {
                    if (module.GetType() == FSEngineType || module.GetType().IsSubclassOf(FSEngineType))
                    {
                        FSEngineType.InvokeMember("updateStatus", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance, null, module, new object[] { });
                        Debug.Log($"DEBUG status of {module} on {vessel.vesselName}: {(string)FSEngineType.GetField("status", BindingFlags.Public | BindingFlags.Instance).GetValue(module)}");
                        Debug.Log($"DEBUG thrust of {module} on {vessel.vesselName}: {(float)FSEngineType.GetField("thrustInfo", BindingFlags.Public | BindingFlags.Instance).GetValue(module)}");
                        Debug.Log($"DEBUG RPM of {module} on {vessel.vesselName}: {(float)FSEngineType.GetField("RPM", BindingFlags.Public | BindingFlags.Instance).GetValue(module)}");
                        Debug.Log($"DEBUG requestedThrottle of {module} on {vessel.vesselName}: {(float)FSEngineType.GetField("requestedThrottle", BindingFlags.Public | BindingFlags.Instance).GetValue(module)}");
                        Debug.Log($"DEBUG finalThrust of {module} on {vessel.vesselName}: {(float)FSEngineType.GetField("finalThrust", BindingFlags.Public | BindingFlags.Instance).GetValue(module)}");
                    }
                }
            }
        }
    }
}
