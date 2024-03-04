using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Extensions;
using BDArmory.GameModes;
using BDArmory.Modules;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Weapons.Missiles;
using BDArmory.Weapons;
using BDArmory.Damage;
using BDArmory.FX;

namespace BDArmory.VesselSpawning
{
    public enum SpawnFailureReason { None, NoCraft, NoTerrain, InvalidVessel, VesselLostParts, VesselFailedToSpawn, TimedOut, Cancelled, DependencyIssues };

    public static class SpawnUtils
    {
        // Cancel all spawning modes.
        public static void CancelSpawning()
        {
            VesselSpawnerStatus.spawnFailureReason = SpawnFailureReason.Cancelled;

            // Single spawn
            if (CircularSpawning.Instance && CircularSpawning.Instance.vesselsSpawning || CircularSpawning.Instance.vesselsSpawningOnceContinuously)
            { CircularSpawning.Instance.CancelSpawning(); }

            // Continuous spawn
            if (ContinuousSpawning.Instance && ContinuousSpawning.Instance.vesselsSpawningContinuously)
            { ContinuousSpawning.Instance.CancelSpawning(); }

            SpawnUtils.RevertSpawnLocationCamera(true);
        }

        /// <summary>
        /// If the VESSELNAMING tag exists in the craft file, then KSP renames the vessel at some point after spawning.
        /// This function checks for renamed vessels and sets the name back to what it was.
        /// This must be called once after a yield, before using vessel.vesselName as an index in spawnedVessels.Keys.
        /// </summary>
        /// <param name="vessels"></param>
        public static void CheckForRenamedVessels(Dictionary<string, Vessel> vessels)
        {
            foreach (var vesselName in vessels.Keys.ToList())
            {
                if (vesselName != vessels[vesselName].vesselName)
                {
                    vessels[vesselName].vesselName = vesselName;
                }
            }
        }

        public static int PartCount(Vessel vessel, bool ignoreEVA = true)
        {
            if (vessel == null) return 0;
            if (!ignoreEVA) return vessel.parts.Count;
            int count = 0;
            using (var part = vessel.parts.GetEnumerator())
                while (part.MoveNext())
                {
                    if (part.Current == null) continue;
                    if (ignoreEVA && part.Current.IsKerbalEVA()) continue; // Ignore EVA kerbals, which get added at some point after spawning.
                    ++count;
                }
            return count;
        }

        public static Dictionary<string, int> PartCrewCounts
        {
            get
            {
                if (_partCrewCounts == null)
                {
                    _partCrewCounts = new Dictionary<string, int>();
                    foreach (var part in PartLoader.LoadedPartsList)
                    {
                        if (part == null || part.partPrefab == null || part.partPrefab.CrewCapacity < 1) continue;
                        if (BDArmorySettings.DEBUG_SPAWNING) Debug.Log($"[BDArmory.SpawnUtils]: {part.name} has crew capacity {part.partPrefab.CrewCapacity}.");
                        if (!_partCrewCounts.ContainsKey(part.name))
                        { _partCrewCounts.Add(part.name, part.partPrefab.CrewCapacity); }
                        else // Duplicate part name!
                        {
                            if (part.partPrefab.CrewCapacity != _partCrewCounts[part.name])
                            {
                                Debug.LogWarning($"[BDArmory.SpawnUtils]: Found a duplicate part {part.name} with a different crew capacity! {_partCrewCounts[part.name]} vs {part.partPrefab.CrewCapacity}, using the minimum.");
                                _partCrewCounts[part.name] = Mathf.Min(_partCrewCounts[part.name], part.partPrefab.CrewCapacity);
                            }
                            else
                            {
                                Debug.LogWarning($"[BDArmory.SpawnUtils]: Found a duplicate part {part.name} with the same crew capacity!");
                            }
                        }
                    }
                }
                return _partCrewCounts;
            }
        }
        static Dictionary<string, int> _partCrewCounts;

        #region Camera
        public static void ShowSpawnPoint(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 0, bool spawning = false) { if (SpawnUtilsInstance.Instance) SpawnUtilsInstance.Instance.ShowSpawnPoint(worldIndex, latitude, longitude, altitude, distance, spawning); } // Note: this may launch a coroutine when not spawning and there's no active vessel!
        public static void RevertSpawnLocationCamera(bool keepTransformValues = true, bool revertIfDead = false) { if (SpawnUtilsInstance.Instance) SpawnUtilsInstance.Instance.RevertSpawnLocationCamera(keepTransformValues, revertIfDead); }
        #endregion

        #region Teams
        public static Dictionary<string, string> originalTeams = new Dictionary<string, string>();
        public static void SaveTeams()
        {
            originalTeams.Clear();
            foreach (var weaponManager in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList())
            {
                originalTeams[weaponManager.vessel.vesselName] = weaponManager.Team.Name;
            }
        }
        #endregion

        #region Engine Activation
        public static int CountActiveEngines(Vessel vessel, bool andOperational = false)
        {
            return VesselModuleRegistry.GetModuleEngines(vessel).Where(engine => engine.EngineIgnited && (!andOperational || engine.isOperational)).ToList().Count + FireSpitter.CountActiveEngines(vessel);
        }

        public static void ActivateAllEngines(Vessel vessel, bool activate = true, bool ignoreModularMissileEngines = true)
        {
            foreach (var engine in VesselModuleRegistry.GetModuleEngines(vessel))
            {
                if (ignoreModularMissileEngines && IsModularMissilePart(engine.part)) continue; // Ignore modular missile engines.
                if (BDArmorySettings.RUNWAY_PROJECT && BDArmorySettings.RUNWAY_PROJECT_ROUND == 55) engine.independentThrottle = false;
                var mme = engine.part.FindModuleImplementing<MultiModeEngine>();
                if (mme == null)
                {
                    if (activate) engine.Activate();
                    else engine.Shutdown();
                }
                else
                {
                    if (mme.runningPrimary)
                    {
                        if (activate && !mme.PrimaryEngine.EngineIgnited)
                        {
                            mme.PrimaryEngine.Activate();
                        }
                        else if (!activate && mme.PrimaryEngine.EngineIgnited)
                        {
                            mme.PrimaryEngine.Shutdown();
                        }
                    }
                    else
                    {
                        if (activate && !mme.SecondaryEngine.EngineIgnited)
                        {
                            mme.SecondaryEngine.Activate();
                        }
                        else if (!activate && mme.SecondaryEngine.EngineIgnited)
                        {
                            mme.SecondaryEngine.Shutdown();
                        }
                    }
                }
            }
            FireSpitter.ActivateFSEngines(vessel, activate);
        }

        public static bool IsModularMissilePart(Part part)
        {
            if (part is not null)
            {
                var firstDecoupler = BDModularGuidance.FindFirstDecoupler(part.parent, null);
                if (firstDecoupler is not null && HasMMGInChildren(firstDecoupler.part)) return true;
            }
            return false;
        }
        static bool HasMMGInChildren(Part part)
        {
            if (part is null) return false;
            if (part.FindModuleImplementing<BDModularGuidance>() is not null) return true;
            foreach (var child in part.children)
                if (HasMMGInChildren(child)) return true;
            return false;
        }
        #endregion

        #region Intake hacks
        public static void HackIntakesOnNewVessels(bool enable) => SpawnUtilsInstance.Instance.HackIntakesOnNewVessels(enable);
        public static void HackIntakes(Vessel vessel, bool enable) => SpawnUtilsInstance.Instance.HackIntakes(vessel, enable);
        #endregion

        #region ControlSurface hacks
        public static void HackActuatorsOnNewVessels(bool enable) => SpawnUtilsInstance.Instance.HackActuatorsOnNewVessels(enable);
        public static void HackActuators(Vessel vessel, bool enable) => SpawnUtilsInstance.Instance.HackActuators(vessel, enable);
        #endregion

        #region Space hacks
        public static void SpaceFrictionOnNewVessels(bool enable) => SpawnUtilsInstance.Instance.SpaceFrictionOnNewVessels(enable);
        public static void SpaceHacks(Vessel vessel) => SpawnUtilsInstance.Instance.SpaceHacks(vessel);
        #endregion

        #region Mutators
        public static void ApplyMutatorsOnNewVessels(bool enable) => SpawnUtilsInstance.Instance.ApplyMutatorsOnNewVessels(enable);
        public static void ApplyMutators(Vessel vessel, bool enable) => SpawnUtilsInstance.Instance.ApplyMutators(vessel, enable);
        #endregion

        #region RWP Stuff
        public static void ApplyRWPonNewVessels(bool enable) => SpawnUtilsInstance.Instance.ApplyRWPonNewVessels(enable);
        public static void ApplyRWP(Vessel vessel) => SpawnUtilsInstance.Instance.ApplyRWP(vessel); // Applying RWP can't be undone
        #endregion

        #region HallOfShame
        public static void ApplyHOSOnNewVessels(bool enable) => SpawnUtilsInstance.Instance.ApplyHOSOnNewVessels(enable);
        public static void ApplyHOS(Vessel vessel) => SpawnUtilsInstance.Instance.ApplyHOS(vessel); // Applying HOS can't be undone.
        #endregion

        #region KAL
        public static void RestoreKALGlobally(bool restore = true) { foreach (var vessel in FlightGlobals.VesselsLoaded) SpawnUtilsInstance.Instance.RestoreKAL(vessel, restore); }
        public static void RestoreKAL(Vessel vessel, bool restore = true) => SpawnUtilsInstance.Instance.RestoreKAL(vessel, restore);
        #endregion

        #region Post-Spawn
        public static void OnVesselReady(Vessel vessel) => SpawnUtilsInstance.Instance.OnVesselReady(vessel);
        #endregion

        #region Vessel Removal
        public static bool removingVessels => SpawnUtilsInstance.Instance.removeVesselsPending > 0;
        public static void RemoveVessel(Vessel vessel) => SpawnUtilsInstance.Instance.RemoveVessel(vessel);
        public static IEnumerator RemoveAllVessels() => SpawnUtilsInstance.Instance.RemoveAllVessels();
        public static void DisableAllBulletsAndRockets() => SpawnUtilsInstance.Instance.DisableAllBulletsAndRockets();
        #endregion

        #region AI/WM stuff for RWP
        public static bool CheckAIWMPlacement(Vessel vessel)
        {
            var message = "";
            List<string> failureStrings = new List<string>();
            var AI = VesselModuleRegistry.GetModule<BDGenericAIBase>(vessel, true);
            var WM = VesselModuleRegistry.GetMissileFire(vessel, true);
            if (AI == null) message = " has no AI";
            if (WM == null) message += (AI == null ? " or WM" : " has no WM");
            if (AI != null || WM != null)
            {
                int count = 0;

                if (AI != null && !(AI.part == AI.part.vessel.rootPart || AI.part.parent == AI.part.vessel.rootPart))
                {
                    message += (WM == null ? " and its AI" : "'s AI");
                    ++count;
                }
                if (WM != null && !(WM.part == WM.part.vessel.rootPart || WM.part.parent == WM.part.vessel.rootPart))
                {
                    message += (AI == null ? " and its WM" : (count > 0 ? " and WM" : "'s WM"));
                    ++count;
                };
                if (count > 0) message += (count > 1 ? " are" : " is") + " not attached to its root part";
            }

            if (!string.IsNullOrEmpty(message))
            {
                message = $"{vessel.vesselName}" + message + ".";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.LogWarning("[BDArmory.SpawnUtils]: " + message);
                return false;
            }
            return true;
        }

        public static void CheckAIWMCounts(Vessel vessel)
        {
            var numberOfAIs = VesselModuleRegistry.GetModuleCount<BDGenericAIBase>(vessel);
            var numberOfWMs = VesselModuleRegistry.GetModuleCount<MissileFire>(vessel);
            string message = null;
            if (numberOfAIs != 1 && numberOfWMs != 1) message = $"{vessel.vesselName} has {numberOfAIs} AIs and {numberOfWMs} WMs";
            else if (numberOfAIs != 1) message = $"{vessel.vesselName} has {numberOfAIs} AIs";
            else if (numberOfWMs != 1) message = $"{vessel.vesselName} has {numberOfWMs} WMs";
            if (message != null)
            {
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.LogWarning("[BDArmory.SpawnUtils]: " + message);
            }
        }
        #endregion
    }

    /// <summary>
    /// Non-static MonoBehaviour version to be able to call coroutines.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SpawnUtilsInstance : MonoBehaviour
    {
        public static SpawnUtilsInstance Instance;

        void Awake()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
            spawnLocationCamera = new GameObject("StationaryCameraParent");
            spawnLocationCamera = (GameObject)Instantiate(spawnLocationCamera, Vector3.zero, Quaternion.identity);
            spawnLocationCamera.SetActive(false);
        }

        void Start()
        {
            if (BDArmorySettings.HACK_INTAKES) HackIntakesOnNewVessels(true);
            if (BDArmorySettings.SPACE_HACKS) SpaceFrictionOnNewVessels(true);
            if (BDArmorySettings.RUNWAY_PROJECT) HackActuatorsOnNewVessels(true);
        }

        void OnDestroy()
        {
            VesselSpawnerField.Save();
            Destroy(spawnLocationCamera);
            HackIntakesOnNewVessels(false);
            HackActuatorsOnNewVessels(false);
            SpaceFrictionOnNewVessels(false);
        }

        #region Post-Spawn
        public void OnVesselReady(Vessel vessel) => StartCoroutine(OnVesselReadyCoroutine(vessel));
        /// <summary>
        /// Perform adjustments to spawned craft once they're loaded and unpacked.
        /// </summary>
        /// <param name="vessel"></param>
        IEnumerator OnVesselReadyCoroutine(Vessel vessel)
        {
            var wait = new WaitForFixedUpdate();
            while (vessel != null && (!vessel.loaded || vessel.packed)) yield return wait;
            if (vessel == null) yield break;
            // EVA Kerbals get their Assigned status reverted to Available for some reason. This fixes that.
            foreach (var kerbal in VesselModuleRegistry.GetKerbalEVAs(vessel)) foreach (var crew in kerbal.part.protoModuleCrew) crew.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
        }
        #endregion

        #region Vessel Removal
        public int removeVesselsPending = 0;
        // Remove a vessel and clean up any remaining parts. This fixes the case where the currently focussed vessel refuses to die properly.
        public void RemoveVessel(Vessel vessel)
        {
            if (vessel == null) return;
            if (VesselSpawnerWindow.Instance.Observers.Contains(vessel)) return; // Don't remove observers.
            if (BDArmorySettings.ASTEROID_RAIN && vessel.vesselType == VesselType.SpaceObject) return; // Don't remove asteroids we're using.
            if (BDArmorySettings.ASTEROID_FIELD && vessel.vesselType == VesselType.SpaceObject) return; // Don't remove asteroids we're using.
            StartCoroutine(RemoveVesselCoroutine(vessel));
        }
        public IEnumerator RemoveVesselCoroutine(Vessel vessel)
        {
            if (vessel == null) yield break;
            ++removeVesselsPending;
            if (vessel != FlightGlobals.ActiveVessel && vessel.vesselType != VesselType.SpaceObject)
            {
                try
                {
                    if (KerbalSafetyManager.Instance.safetyLevel != KerbalSafetyLevel.Off)
                        KerbalSafetyManager.Instance.RecoverVesselNow(vessel);
                    else
                    {
                        foreach (var part in vessel.Parts) part.OnJustAboutToBeDestroyed?.Invoke(); // Invoke any OnJustAboutToBeDestroyed events since RecoverVesselFromFlight calls DestroyImmediate, skipping the FX detachment triggers.
                        ShipConstruction.RecoverVesselFromFlight(vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BDArmory.SpawnUtils]: Exception thrown while removing vessel: {e.Message}");
                }
            }
            else
            {
                if (vessel.vesselType == VesselType.SpaceObject)
                {
                    if ((BDArmorySettings.ASTEROID_RAIN && AsteroidRain.IsManagedAsteroid(vessel))
                        || (BDArmorySettings.ASTEROID_FIELD && AsteroidField.IsManagedAsteroid(vessel))) // Don't remove asteroids when we're using them.
                    {
                        --removeVesselsPending;
                        yield break;
                    }
                    if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Comets introduced in 1.11
                        RemoveComet_1_11(vessel);
                }
                vessel.Die(); // Kill the vessel
                yield return waitForFixedUpdate;
                if (vessel != null)
                {
                    var partsToKill = vessel.parts.ToList(); // If it left any parts, kill them. (This occurs when the currently focussed vessel gets killed.)
                    foreach (var part in partsToKill)
                        part.Die();
                }
                yield return waitForFixedUpdate;
            }
            --removeVesselsPending;
        }

        void RemoveComet_1_11(Vessel vessel)
        {
            var cometVessel = vessel.FindVesselModuleImplementing<CometVessel>();
            if (cometVessel) { Destroy(cometVessel); }
        }

        /// <summary>
        /// Remove all the vessels.
        /// This works by spawning in a spawnprobe at the current camera coordinates so that we can clean up the other vessels properly.
        /// </summary>
        /// <returns></returns>
        public IEnumerator RemoveAllVessels()
        {
            DisableAllBulletsAndRockets(); // First get rid of any bullets and rockets flying around (missiles count as vessels).
            var vesselsToKill = FlightGlobals.Vessels.ToList();
            // Spawn in the SpawnProbe at the camera position.
            var spawnProbe = VesselSpawner.SpawnSpawnProbe();
            if (spawnProbe != null) // If the spawnProbe is null, then just try to kill everything anyway.
            {
                spawnProbe.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
                yield return new WaitWhile(() => spawnProbe != null && (!spawnProbe.loaded || spawnProbe.packed));
                // Switch to the spawn probe.
                while (spawnProbe != null && FlightGlobals.ActiveVessel != spawnProbe)
                {
                    LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnProbe);
                    yield return waitForFixedUpdate;
                }
            }
            // Kill all other vessels (including debris).
            foreach (var vessel in vesselsToKill)
                RemoveVessel(vessel);
            // Finally, remove the SpawnProbe.
            RemoveVessel(spawnProbe);

            // Now, clear the teams and wait for everything to be removed.
            SpawnUtils.originalTeams.Clear();
            yield return new WaitWhile(() => removeVesselsPending > 0);
        }

        public void DisableAllBulletsAndRockets()
        {
            if (ModuleWeapon.bulletPool != null && ModuleWeapon.bulletPool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.SpawnUtils]: Setting {ModuleWeapon.bulletPool.pool.Count(b => b != null && b.activeInHierarchy)} bullets inactive.");
                foreach (var bullet in ModuleWeapon.bulletPool.pool)
                {
                    if (bullet == null) continue;
                    bullet.SetActive(false);
                }
            }
            if (ModuleWeapon.shellPool != null && ModuleWeapon.shellPool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.SpawnUtils]: Setting {ModuleWeapon.shellPool.pool.Count(s => s != null && s.activeInHierarchy)} shells inactive.");
                foreach (var shell in ModuleWeapon.shellPool.pool)
                {
                    if (shell == null) continue;
                    shell.SetActive(false);
                }
            }
            if (ModuleWeapon.rocketPool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.SpawnUtils]: Setting {ModuleWeapon.rocketPool.Values.Where(rocketPool => rocketPool != null && rocketPool.pool != null).Sum(rocketPool => rocketPool.pool.Count(s => s != null && s.activeInHierarchy))} rockets inactive.");
                foreach (var rocketPool in ModuleWeapon.rocketPool.Values)
                {
                    if (rocketPool == null || rocketPool.pool == null) continue;
                    foreach (var rocket in rocketPool.pool)
                    {
                        if (rocket == null) continue;
                        rocket.SetActive(false);
                    }
                }
            }
        }
        #endregion

        #region Camera Adjustment
        GameObject spawnLocationCamera;
        Transform originalCameraParentTransform;
        float originalCameraNearClipPlane;
        float originalCameraDistance;
        Coroutine delayedShowSpawnPointCoroutine;
        private readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
        /// <summary>
        /// Show the spawn point.
        /// Note: When not spawning and there's no active vessel, this may launch a coroutine to perform the actual shift.
        /// Note: If spawning is true, then the spawnLocationCamera takes over the camera and RevertSpawnLocationCamera should be called at some point to allow KSP to do its own camera stuff.
        /// </summary>
        /// <param name="worldIndex">The body the spawn point is on.</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        /// <param name="altitude">Altitude</param>
        /// <param name="distance">Distance to view the point from.</param>
        /// <param name="spawning">Whether spawning is actually happening.</param>
        /// <param name="recurse">State parameter for when we need to spawn a probe first.</param>
        public void ShowSpawnPoint(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 0, bool spawning = false, bool recurse = true)
        {
            if (BDArmorySettings.DEBUG_SPAWNING) Debug.Log($"[BDArmory.SpawnUtils]: Showing spawn point ({latitude:G3}, {longitude:G3}, {altitude:G3}) on {FlightGlobals.Bodies[worldIndex].name}");
            if (BDArmorySettings.ASTEROID_RAIN) { AsteroidRain.Instance.Reset(); }
            if (BDArmorySettings.ASTEROID_FIELD) { AsteroidField.Instance.Reset(); }
            if (!spawning && (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD))
            {
                if (!recurse)
                {
                    Debug.LogWarning($"[BDArmory.SpawnUtils]: No active vessel, unable to show spawn point.");
                    return;
                }
                Debug.LogWarning($"[BDArmory.SpawnUtils]: Active vessel is dead or packed, spawning a new one.");
                if (delayedShowSpawnPointCoroutine != null) { StopCoroutine(delayedShowSpawnPointCoroutine); delayedShowSpawnPointCoroutine = null; }
                delayedShowSpawnPointCoroutine = StartCoroutine(DelayedShowSpawnPoint(worldIndex, latitude, longitude, altitude, distance, spawning));
                return;
            }
            var flightCamera = FlightCamera.fetch;
            var cameraHeading = FlightCamera.CamHdg;
            var cameraPitch = FlightCamera.CamPitch;
            if (distance == 0) distance = flightCamera.Distance;
            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.PatchedConicsAttached) FlightGlobals.ActiveVessel.DetachPatchedConicsSolver();
            if (!spawning)
            {
                var overLand = (worldIndex != -1 ? FlightGlobals.Bodies[worldIndex] : FlightGlobals.currentMainBody).TerrainAltitude(latitude, longitude) > 0;
                var easeToSurface = altitude <= 10;
                FlightGlobals.fetch.SetVesselPosition(worldIndex != -1 ? worldIndex : FlightGlobals.currentMainBody.flightGlobalsIndex, latitude, longitude, overLand ? Math.Max(5, altitude) : altitude, FlightGlobals.ActiveVessel.vesselType == VesselType.Plane ? 0 : 90, 0, true, easeToSurface, easeToSurface ? 0.1 : 1); // FIXME This should be using the vessel reference transform to determine the inclination. Also below.
                FloatingOrigin.SetOffset(FlightGlobals.ActiveVessel.transform.position); // This adjusts local coordinates, such that the vessel position is (0,0,0).
                VehiclePhysics.Gravity.Refresh();
            }
            else
            {
                FlightGlobals.fetch.SetVesselPosition(worldIndex != -1 ? worldIndex : FlightGlobals.currentMainBody.flightGlobalsIndex, latitude, longitude, altitude, 0, 0, true);
                var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(latitude, longitude);
                var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitude, longitude, terrainAltitude + altitude);
                FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0).
                var radialUnitVector = -FlightGlobals.currentMainBody.transform.position.normalized;
                var cameraPosition = Vector3.RotateTowards(distance * radialUnitVector, Quaternion.AngleAxis(cameraHeading * Mathf.Rad2Deg, radialUnitVector) * -VectorUtils.GetNorthVector(spawnPoint, FlightGlobals.currentMainBody), 70f * Mathf.Deg2Rad, 0);
                if (!spawnLocationCamera.activeSelf)
                {
                    spawnLocationCamera.SetActive(true);
                    originalCameraParentTransform = flightCamera.transform.parent;
                    originalCameraNearClipPlane = GUIUtils.GetMainCamera().nearClipPlane;
                    originalCameraDistance = flightCamera.Distance;
                }
                spawnLocationCamera.transform.position = Vector3.zero;
                spawnLocationCamera.transform.rotation = Quaternion.LookRotation(-cameraPosition, radialUnitVector);
                flightCamera.transform.parent = spawnLocationCamera.transform;
                flightCamera.SetTarget(spawnLocationCamera.transform);
                flightCamera.transform.localPosition = cameraPosition;
                flightCamera.transform.localRotation = Quaternion.identity;
                flightCamera.ActivateUpdate();
            }
            flightCamera.SetDistanceImmediate(distance);
            FlightCamera.CamHdg = cameraHeading;
            FlightCamera.CamPitch = cameraPitch;
        }

        IEnumerator DelayedShowSpawnPoint(int worldIndex, double latitude, double longitude, double altitude = 0, float distance = 0, bool spawning = false)
        {
            Vessel spawnProbe = VesselSpawner.SpawnSpawnProbe();
            if (spawnProbe != null)
            {
                spawnProbe.Landed = false;
                yield return new WaitWhile(() => spawnProbe != null && (!spawnProbe.loaded || spawnProbe.packed));
                FlightGlobals.ForceSetActiveVessel(spawnProbe);
                while (spawnProbe != null && FlightGlobals.ActiveVessel != spawnProbe)
                {
                    spawnProbe.SetWorldVelocity(Vector3d.zero);
                    LoadedVesselSwitcher.Instance.ForceSwitchVessel(spawnProbe);
                    yield return waitForFixedUpdate;
                }
            }
            ShowSpawnPoint(worldIndex, latitude, longitude, altitude, distance, spawning, false);
        }

        public void RevertSpawnLocationCamera(bool keepTransformValues = true, bool revertIfDead = false)
        {
            if (spawnLocationCamera == null || !spawnLocationCamera.activeSelf) return;
            if (BDArmorySettings.DEBUG_SPAWNING) Debug.Log($"[BDArmory.SpawnUtils]: Reverting spawn location camera.");
            if (delayedShowSpawnPointCoroutine != null) { StopCoroutine(delayedShowSpawnPointCoroutine); delayedShowSpawnPointCoroutine = null; }
            var flightCamera = FlightCamera.fetch;
            if (originalCameraParentTransform != null)
            {
                var mainCamera = GUIUtils.GetMainCamera();
                if (keepTransformValues && flightCamera.transform != null && flightCamera.transform.parent != null)
                {
                    originalCameraParentTransform.position = flightCamera.transform.parent.position;
                    originalCameraParentTransform.rotation = flightCamera.transform.parent.rotation;
                    if (mainCamera) originalCameraNearClipPlane = mainCamera.nearClipPlane;
                    originalCameraDistance = flightCamera.Distance;
                }
                flightCamera.transform.parent = originalCameraParentTransform;
                if (mainCamera) mainCamera.nearClipPlane = originalCameraNearClipPlane;
                flightCamera.SetDistanceImmediate(originalCameraDistance);
                flightCamera.SetTargetNone();
                flightCamera.EnableCamera();
            }
            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.state != Vessel.State.DEAD)
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(FlightGlobals.ActiveVessel); // Update the camera.
            else if (revertIfDead) // Spawn a spawn probe to avoid KSP breaking the camera.
            {
                var spawnProbe = VesselSpawner.SpawnSpawnProbe(flightCamera.Distance * flightCamera.mainCamera.transform.forward);
                if (spawnProbe != null)
                {
                    spawnProbe.Landed = false;
                    StartCoroutine(LoadedVesselSwitcher.Instance.SwitchToVesselWhenPossible(spawnProbe, 10));
                }
            }
            spawnLocationCamera.SetActive(false);
        }
        #endregion

        #region Intake hacks
        public void HackIntakesOnNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(HackIntakesEventHandler);
                GameEvents.OnVesselRollout.Add(HackIntakes);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(HackIntakesEventHandler);
                GameEvents.OnVesselRollout.Remove(HackIntakes);
            }
        }
        void HackIntakesEventHandler(Vessel vessel) => HackIntakes(vessel, true);

        public void HackIntakes(Vessel vessel, bool enable)
        {
            if (vessel == null || !vessel.loaded) return;
            if (enable)
            {
                foreach (var intake in VesselModuleRegistry.GetModules<ModuleResourceIntake>(vessel))
                    intake.checkForOxygen = false;
            }
            else
            {
                foreach (var intake in VesselModuleRegistry.GetModules<ModuleResourceIntake>(vessel))
                {
                    var checkForOxygen = ConfigNodeUtils.FindPartModuleConfigNodeValue(intake.part.partInfo.partConfig, "ModuleResourceIntake", "checkForOxygen");
                    if (!string.IsNullOrEmpty(checkForOxygen)) // Use the default value from the part.
                    {
                        try
                        {
                            intake.checkForOxygen = bool.Parse(checkForOxygen);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BDArmory.BDArmorySetup]: Failed to parse checkForOxygen configNode of {intake.name}: {e.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[BDArmory.BDArmorySetup]: No default value for checkForOxygen found in partConfig for {intake.name}, defaulting to true.");
                        intake.checkForOxygen = true;
                    }
                }
            }
        }
        public void HackIntakes(ShipConstruct ship) // This version only needs to enable the hack.
        {
            if (ship == null) return;
            foreach (var part in ship.Parts)
            {
                var intakes = part.FindModulesImplementing<ModuleResourceIntake>();
                if (intakes.Count() > 0)
                {
                    foreach (var intake in intakes)
                        intake.checkForOxygen = false;
                }
            }
        }
        #endregion

        #region Control Surface Actuator hacks
        public void HackActuatorsOnNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(HackActuatorsEventHandler);
                GameEvents.OnVesselRollout.Add(HackActuators);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(HackActuatorsEventHandler);
                GameEvents.OnVesselRollout.Remove(HackActuators);
            }
        }
        void HackActuatorsEventHandler(Vessel vessel) => HackActuators(vessel, true);

        public void HackActuators(Vessel vessel, bool enable)
        {
            if (vessel == null || !vessel.loaded) return;
            if (enable)
            {
                foreach (var ctrlSrf in VesselModuleRegistry.GetModules<ModuleControlSurface>(vessel))
                {
                    ctrlSrf.actuatorSpeed = 30;
                    if (BDArmorySettings.DEBUG_SPAWNING) Debug.Log($"[BDArmory.ActuatorHacks]: Setting {ctrlSrf.name} actuation speed to : {ctrlSrf.actuatorSpeed}");
                }
            }
            else
            {
                foreach (var ctrlSrf in VesselModuleRegistry.GetModules<ModuleControlSurface>(vessel))
                {
                    var actuatorSpeed = ConfigNodeUtils.FindPartModuleConfigNodeValue(ctrlSrf.part.partInfo.partConfig, "ModuleControlSurface", "actuatorSpeed");
                    if (!string.IsNullOrEmpty(actuatorSpeed)) // Use the default value from the part.
                    {
                        try
                        {
                            ctrlSrf.actuatorSpeed = float.Parse(actuatorSpeed);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BDArmory.BDArmorySetup]: Failed to parse actuatorSpeed configNode of {ctrlSrf.name}: {e.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[BDArmory.BDArmorySetup]: No default value for actuatorSpeed found in partConfig for {ctrlSrf.name}, defaulting to true.");
                        ctrlSrf.actuatorSpeed = 30;
                    }
                }
            }
        }
        public void HackActuators(ShipConstruct ship) // This version only needs to enable the hack.
        {
            if (ship == null) return;
            foreach (var part in ship.Parts)
            {
                var ctrlSrf = part.FindModulesImplementing<ModuleControlSurface>();
                if (ctrlSrf.Count() > 0)
                {
                    foreach (var srf in ctrlSrf)
                        srf.actuatorSpeed = 30;
                }
            }
        }
        #endregion

        #region Space hacks
        public void SpaceFrictionOnNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(SpaceHacksEventHandler);
                GameEvents.OnVesselRollout.Add(SpaceHacks);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(SpaceHacksEventHandler);
                GameEvents.OnVesselRollout.Remove(SpaceHacks);
            }
        }
        void SpaceHacksEventHandler(Vessel vessel) => SpaceHacks(vessel);

        public void SpaceHacks(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded) return;

            if (VesselModuleRegistry.GetMissileFire(vessel, true) != null && vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>() == null)
            {
                vessel.rootPart.AddModule("ModuleSpaceFriction");
            }
        }
        public void SpaceHacks(ShipConstruct ship) // This version only needs to enable the hack.
        {
            if (ship == null) return;
            ship.Parts[0].AddModule("ModuleSpaceFriction");
        }
        #endregion

        #region KAL
        public void RestoreKAL(Vessel vessel, bool restore) => StartCoroutine(RestoreKALCoroutine(vessel, restore));
        /// <summary>
        /// This goes through the vessel's part modules and fixes the mismatched part persistentId on the KAL's controlled axes with the correct ones in the ProtoPartModuleSnapshot then reloads the module from the ProtoPartModuleSnapshot.
        /// </summary>
        /// <param name="vessel">The vessel to modify.</param>
        /// <param name="restore">Restore or wipe any KALs found.</param>
        IEnumerator RestoreKALCoroutine(Vessel vessel, bool restore)
        {
            var tic = Time.time;
            yield return new Utils.WaitUntilFixed(() => vessel == null || vessel.Parts.Count != 0 || Time.time - tic > 10); // Wait for up to 10s for the vessel's parts to be populated (usually it takes 2 frames after spawning).
            if (vessel == null || vessel.Parts.Count == 0) yield break;
            if (!restore) // Wipe all KAL modules on the vessel.
            {
                foreach (var kal in vessel.FindPartModulesImplementing<Expansions.Serenity.ModuleRoboticController>())
                {
                    if (kal == null) continue;
                    kal.ControlledAxes.Clear();
                }
                yield break;
            }
            foreach (var protoPartSnapshot in vessel.protoVessel.protoPartSnapshots) // The protoVessel contains the original ProtoPartModuleSnapshots with the info we need.
                foreach (var protoPartModuleSnapshot in protoPartSnapshot.modules)
                    if (protoPartModuleSnapshot.moduleName == "ModuleRoboticController") // Found a KAL
                    {
                        var kal = protoPartModuleSnapshot.moduleRef as Expansions.Serenity.ModuleRoboticController;
                        var controlledAxes = protoPartModuleSnapshot.moduleValues.GetNode("CONTROLLEDAXES");
                        kal.ControlledAxes.Clear(); // Clear the existing axes (they should be clear already due to mismatching part persistent IDs, but better safe than sorry).
                        int rowIndex = 0;
                        foreach (var axisNode in controlledAxes.GetNodes("AXIS")) // For each axis to be controlled, locate the part in the spawned vessel that has the correct module.
                            if (uint.TryParse(axisNode.GetValue("moduleId"), out uint moduleId)) // Get the persistentId of the module it's supposed to be affecting, which is correctly set in some part.
                            {
                                foreach (var part in vessel.Parts)
                                    foreach (var partModule in part.Modules)
                                        if (partModule.PersistentId == moduleId) // Found a corresponding part with the correct moduleId. Note: there could be multiple parts with this module due to symmetry, so we check them all.
                                        {
                                            var fieldName = axisNode.GetValue("axisName");
                                            foreach (var field in partModule.Fields)
                                                if (field.name == fieldName) // Found the axis field in a module in a part being controlled by this KAL.
                                                {
                                                    axisNode.SetValue("persistentId", part.persistentId.ToString()); // Update the ConfigNode in the ProtoPartModuleSnapshot
                                                    axisNode.SetValue("partNickName", part.partInfo.title); // Set the nickname to the part title (note: this will override custom nicknames).
                                                    axisNode.SetValue("rowIndex", rowIndex++);
                                                    var axis = new Expansions.Serenity.ControlledAxis(part, partModule, field as BaseAxisField, kal); // Link the part, module, field and KAL together.
                                                    axis.Load(axisNode); // Load the new config into the axis.
                                                    kal.ControlledAxes.Add(axis); // Add the axis to the KAL.
                                                    break;
                                                }
                                        }
                            }
                    }
        }
        #endregion

        #region Mutators
        public void ApplyMutatorsOnNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(ApplyMutatorEventHandler);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(ApplyMutatorEventHandler);
            }
        }
        void ApplyMutatorEventHandler(Vessel vessel) => ApplyMutators(vessel, true);

        public Dictionary<string, int> gunGameProgress = new Dictionary<string, int>();

        public void ApplyMutators(Vessel vessel, bool enable)
        {
            if (vessel == null || !vessel.loaded) return;
            var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
            if (enable && BDArmorySettings.MUTATOR_MODE && BDArmorySettings.MUTATOR_LIST.Count > 0)
            {
                if (MM == null)
                {
                    MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                }
                if (BDArmorySettings.MUTATOR_APPLY_GUNGAME) //gungame
                {
                    if (!BDArmorySettings.GG_CYCLE_LIST && MM.progressionIndex > BDArmorySettings.MUTATOR_LIST.Count - 1) return; // Already at the end of the list.
                    if (BDArmorySettings.GG_PERSISTANT_PROGRESSION) MM.progressionIndex = gunGameProgress.GetValueOrDefault(vessel.vesselName, 0);
                    if (MM.progressionIndex > BDArmorySettings.MUTATOR_LIST.Count - 1) MM.progressionIndex = BDArmorySettings.GG_CYCLE_LIST ? 0 : BDArmorySettings.MUTATOR_LIST.Count - 1;
                    Debug.Log($"[BDArmory.SpawnUtils]: Applying mutator {BDArmorySettings.MUTATOR_LIST[MM.progressionIndex]} to {vessel.vesselName}");
                    MM.EnableMutator(BDArmorySettings.MUTATOR_LIST[MM.progressionIndex]); // Apply the mutator.
                    MM.progressionIndex++; //increment to next mutator on list
                    if (BDArmorySettings.GG_PERSISTANT_PROGRESSION) gunGameProgress[vessel.vesselName] = MM.progressionIndex;
                }
                else
                {
                    if (BDArmorySettings.MUTATOR_APPLY_GLOBAL) //selected mutator applied globally
                    {
                        MM.EnableMutator(BDACompetitionMode.Instance.currentMutator);
                    }
                    else //mutator applied on a per-craft basis, APPLY_TIMER/APPLY_KILL
                    {
                        MM.EnableMutator(); //random mutator
                    }
                }
                BDACompetitionMode.Instance.competitionStatus.Add($"{vessel.vesselName} gains {MM.mutatorName}{(BDArmorySettings.MUTATOR_DURATION > 0 ? $" for {BDArmorySettings.MUTATOR_DURATION * 60} seconds!" : "!")}");
            }
            else if (MM != null)
            {
                MM.DisableMutator();
            }
        }
        #endregion

        #region HOS
        public void ApplyHOSOnNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(ApplyHOSEventHandler);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(ApplyHOSEventHandler);
            }
        }
        void ApplyHOSEventHandler(Vessel vessel) => ApplyHOS(vessel);

        public void ApplyHOS(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded) return;
            if (BDArmorySettings.ENABLE_HOS && BDArmorySettings.HALL_OF_SHAME_LIST.Count > 0)
            {
                if (BDArmorySettings.HALL_OF_SHAME_LIST.Contains(vessel.GetName()))
                {
                    using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                        while (part.MoveNext())
                        {
                            if (BDArmorySettings.HOS_FIRE > 0.1f)
                            {
                                BulletHitFX.AttachFire(part.Current.transform.position, part.Current, BDArmorySettings.HOS_FIRE * 50, "GM", BDArmorySettings.COMPETITION_DURATION * 60, 1, true);
                            }
                            if (BDArmorySettings.HOS_MASS != 0)
                            {
                                var MM = part.Current.FindModuleImplementing<ModuleMassAdjust>();
                                if (MM == null)
                                {
                                    MM = (ModuleMassAdjust)part.Current.AddModule("ModuleMassAdjust");
                                }
                                MM.duration = BDArmorySettings.COMPETITION_DURATION * 60;
                                MM.massMod += (float)(BDArmorySettings.HOS_MASS / vessel.Parts.Count); //evenly distribute mass change across entire vessel
                            }
                            if (BDArmorySettings.HOS_DMG != 1)
                            {
                                var HPT = part.Current.FindModuleImplementing<HitpointTracker>();
                                HPT.defenseMutator = (float)(1 / BDArmorySettings.HOS_DMG);
                            }
                            if (BDArmorySettings.HOS_SAS)
                            {
                                if (part.Current.GetComponent<ModuleReactionWheel>() != null)
                                {
                                    ModuleReactionWheel SAS;
                                    SAS = part.Current.GetComponent<ModuleReactionWheel>();
                                    //if (part.Current.CrewCapacity == 0)
                                    part.Current.RemoveModule(SAS); //don't strip reaction wheels from cockpits, as those are allowed
                                }
                            }
                            if (BDArmorySettings.HOS_THRUST != 100)
                            {
                                using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                                    while (engine.MoveNext())
                                    {
                                        engine.Current.thrustPercentage = BDArmorySettings.HOS_THRUST;
                                    }
                            }
                            if (!string.IsNullOrEmpty(BDArmorySettings.HOS_MUTATOR))
                            {
                                var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
                                if (MM == null)
                                {
                                    MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                                    if (BDArmorySettings.DEBUG_COMPETITION) Debug.Log($"[BDArmory.BDACompetitionMode]: adding Mutator module {vessel.vesselName}");
                                }
                                if (BDArmorySettings.DEBUG_COMPETITION) Debug.Log($"[BDArmory.BDACompetitionMode]: Applying ({BDArmorySettings.HOS_MUTATOR})");
                                MM.EnableMutator(BDArmorySettings.HOS_MUTATOR, true);
                            }
                        }
                }
            }
        }
        #endregion

        #region RWP Specific
        public void ApplyRWPonNewVessels(bool enable)
        {
            if (enable)
            {
                GameEvents.onVesselLoaded.Add(ApplyRWPEventHandler);
            }
            else
            {
                GameEvents.onVesselLoaded.Remove(ApplyRWPEventHandler);
            }
        }
        void ApplyRWPEventHandler(Vessel vessel) => ApplyRWP(vessel);

        public void ApplyRWP(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded) return;
            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                float torqueQuantity = 0;
                int APSquantity = 0;
                SpawnUtils.HackActuators(vessel, true);

                using (List<Part>.Enumerator part = vessel.Parts.GetEnumerator())
                    while (part.MoveNext())
                    {
                        if (part.Current.GetComponent<ModuleReactionWheel>() != null)
                        {
                            ModuleReactionWheel SAS;
                            SAS = part.Current.GetComponent<ModuleReactionWheel>();
                            if (part.Current.CrewCapacity == 0 || BDArmorySettings.RUNWAY_PROJECT_ROUND == 60)
                            {
                                torqueQuantity += ((SAS.PitchTorque + SAS.RollTorque + SAS.YawTorque) / 3) * (SAS.authorityLimiter / 100);
                                if (torqueQuantity > BDArmorySettings.MAX_SAS_TORQUE)
                                {
                                    float excessTorque = torqueQuantity - BDArmorySettings.MAX_SAS_TORQUE;
                                    SAS.authorityLimiter = 100 - Mathf.Clamp(((excessTorque / ((SAS.PitchTorque + SAS.RollTorque + SAS.YawTorque) / 3)) * 100), 0, 100);
                                }
                            }
                        }
                        if (part.Current.GetComponent<ModuleCommand>() != null)
                        {
                            ModuleCommand MC;
                            MC = part.Current.GetComponent<ModuleCommand>();
                            if (part.Current.CrewCapacity == 0 && MC.minimumCrew == 0 && !SpawnUtils.IsModularMissilePart(part.Current)) //Non-MMG drone core, nuke it
                                part.Current.RemoveModule(MC);
                        }
                        if (BDArmorySettings.RUNWAY_PROJECT_ROUND == 59)
                        {
                            if (part.Current.GetComponent<ModuleWeapon>() != null)
                            {
                                ModuleWeapon gun;
                                gun = part.Current.GetComponent<ModuleWeapon>();
                                if (gun.isAPS) APSquantity++;
                                if (APSquantity > 4)
                                {
                                    part.Current.RemoveModule(gun);
                                    IEnumerator<PartResource> resource = part.Current.Resources.GetEnumerator();
                                    while (resource.MoveNext())
                                    {
                                        if (resource.Current == null) continue;
                                        if (resource.Current.flowState)
                                        {
                                            resource.Current.flowState = false;
                                        }
                                    }
                                    resource.Dispose();
                                }
                            }
                        }
                    }
                if (BDArmorySettings.RUNWAY_PROJECT_ROUND == 60)
                {
                    var nuke = vessel.rootPart.FindModuleImplementing<BDModuleNuke>();
                    if (nuke == null)
                    {
                        nuke = (BDModuleNuke)vessel.rootPart.AddModule("BDModuleNuke");
                        nuke.engineCore = true;
                        nuke.meltDownDuration = 15;
                        nuke.thermalRadius = 200;
                        if (BDArmorySettings.DEBUG_COMPETITION) Debug.Log("[BDArmory.BDACompetitionMOde]: Adding Nuke Module to " + vessel.GetName());
                    }
                    BDModulePilotAI pilotAI = VesselModuleRegistry.GetModule<BDModulePilotAI>(vessel);
                    if (pilotAI != null)
                    {
                        pilotAI.minAltitude = Mathf.Max(pilotAI.minAltitude, 750);
                        pilotAI.defaultAltitude = BDArmorySettings.VESSEL_SPAWN_ALTITUDE;
                        pilotAI.maxAllowedAoA = 2.5f;
                        pilotAI.postStallAoA = 5;
                        pilotAI.maxSpeed = Mathf.Min(250, pilotAI.maxSpeed);
                        if (BDArmorySettings.DEBUG_COMPETITION) Debug.Log("[BDArmory.BDACompetitionMOde]: Setting SpaceMode Ai settings on " + vessel.GetName());
                    }
                }
            }
        }
        #endregion
    }
}