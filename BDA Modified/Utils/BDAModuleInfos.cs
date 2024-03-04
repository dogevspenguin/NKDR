using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Utils
{
    /// <summary>
    /// This class supports reloading the partModule info blocks when the editor loads.  This allows us to obtain current data on the module configurations.
    /// due to changes in the environment after part loading at game start.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    internal class BDAModuleInfos : MonoBehaviour
    {
        public static Dictionary<string, string> Modules = new Dictionary<string, string>()
        {
            //{"WeaponModule", "Weapon"},
            { "BDModuleSurfaceAI", "BDModule Surface AI"},
            { "BDModulePilotAI", "BDModule Pilot AI"},
            { "BDModuleVTOLAI", "BDModule VTOL AI"},
            { "BDModuleOrbitalAI", "BDModule Orbital AI"},
        };

        public void Start()
        {
            StartCoroutine(ReloadModuleInfos());
        }

        internal static IEnumerator ReloadModuleInfos()
        {
            yield return new WaitWhile(() => Bullets.BulletInfo.bullets == null || Bullets.RocketInfo.rockets == null); // Wait for the field to be non-null to avoid crashes on startup in ModuleWeapon.GetInfo().

            IEnumerator<AvailablePart> loadedParts = PartLoader.LoadedPartsList.GetEnumerator();
            while (loadedParts.MoveNext())
            {
                if (loadedParts.Current == null) continue;
                foreach (string key in Modules.Keys)
                {
                    if (!loadedParts.Current.partPrefab.Modules.Contains(key)) continue;
                    IEnumerator<PartModule> partModules = loadedParts.Current.partPrefab.Modules.GetEnumerator();
                    while (partModules.MoveNext())
                    {
                        if (partModules.Current == null) continue;
                        if (partModules.Current.moduleName != key) continue;
                        string info = partModules.Current.GetInfo();
                        for (int y = 0; y < loadedParts.Current.moduleInfos.Count; y++)
                        {
                            Debug.Log($"[BDArmory.BDAModuleInfos]: moduleName:  {loadedParts.Current.moduleInfos[y].moduleName}");
                            Debug.Log($"[BDArmory.BDAModuleInfos]: KeyValue:  {Modules[key]}");
                            if (loadedParts.Current.moduleInfos[y].moduleName != Modules[key]) continue;
                            loadedParts.Current.moduleInfos[y].info = info;
                            break;
                        }
                    }
                    partModules.Dispose();
                }
            }

            loadedParts.Dispose();
        }
    }
}
