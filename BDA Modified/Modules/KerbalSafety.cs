using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Competition;
using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Modules
{
    public enum KerbalSafetyLevel { Off, Partial, Full };
    // A class to manage the safety of kerbals in BDA.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalSafetyManager : MonoBehaviour
    {
        #region Definitions
        static public KerbalSafetyManager Instance; // static instance for dealing with global stuff.

        public Dictionary<string, KerbalSafety> kerbals = new Dictionary<string, KerbalSafety>(); // The kerbals being managed.
        List<KerbalEVA> evaKerbalsToMonitor = new List<KerbalEVA>();
        bool isEnabled = false;
        public Vessel activeVesselBeforeEject = null;
        public KerbalSafetyLevel safetyLevel { get { return (KerbalSafetyLevel)BDArmorySettings.KERBAL_SAFETY; } }
        #endregion

        public void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;
        }

        public void Start()
        {
            Debug.Log($"[BDArmory.KerbalSafety]: Safety manager started with level {safetyLevel}, but currently disabled.");
        }

        public void OnDestroy()
        {
            DisableKerbalSafety();
        }

        public void HandleSceneChange(GameEvents.FromToAction<GameScenes, GameScenes> fromTo)
        {
            if (fromTo.from == GameScenes.FLIGHT)
            {
                DisableKerbalSafety();
            }
        }

        public void EnableKerbalSafety()
        {
            if (safetyLevel == KerbalSafetyLevel.Off) return;
            if (isEnabled) return;
            isEnabled = true;
            Debug.Log("[BDArmory.KerbalSafety]: Enabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.AddHandlers();
            GameEvents.onVesselSOIChanged.Add(EatenByTheKraken);
            GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
            GameEvents.onVesselGoOffRails.Add(CheckVesselForKerbals);
            GameEvents.onVesselSwitching.Add(OnVesselSwitch);
            CheckAllVesselsForKerbals(); // Check for new vessels that were added while we weren't active.
        }

        public void DisableKerbalSafety()
        {
            if (!isEnabled) return;
            isEnabled = false;
            Debug.Log("[BDArmory.KerbalSafety]: Disabling kerbal safety.");
            foreach (var ks in kerbals.Values.ToList()) StopManagingKerbal(ks);
            kerbals.Clear();
            GameEvents.onVesselSOIChanged.Remove(EatenByTheKraken);
            GameEvents.onGameSceneSwitchRequested.Remove(HandleSceneChange);
            GameEvents.onVesselGoOffRails.Remove(CheckVesselForKerbals);
            GameEvents.onVesselSwitching.Remove(OnVesselSwitch);
        }

        public void CheckAllVesselsForKerbals()
        {
            if (isEnabled)
            {
                newKerbalsAwaitingCheck.Clear();
                evaKerbalsToMonitor.Clear();
                foreach (var vessel in FlightGlobals.Vessels)
                {
                    if (VesselModuleRegistry.ignoredVesselTypes.Contains(vessel.vesselType)) continue;
                    CheckVesselForKerbals(vessel);
                }
            }
            else
            {
                EnableKerbalSafety();
            }
        }

        public void CheckVesselForKerbals(Vessel vessel)
        {
            if (safetyLevel == KerbalSafetyLevel.Off) return;
            if (vessel == null) return;
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Checking {vessel.vesselName} for kerbals.");
            foreach (var part in vessel.parts)
            {
                if (part == null) continue;
                if (part.IsKerbalSeat()) continue; // Ignore the seat, which gives a false positive below.
                foreach (var crew in part.protoModuleCrew)
                {
                    if (crew == null) continue;
                    if (kerbals.ContainsKey(crew.displayName))
                    {
                        if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {crew.displayName} is already managed.");
                        continue; // Already managed.
                    }
                    KerbalSafety ks = null;
                    var ksList = part.gameObject.GetComponents<KerbalSafety>();
                    foreach (var k in ksList) { if (k.kerbalName == crew.name) { ks = k; break; } }
                    if (ks == null) { ks = part.gameObject.AddComponent<KerbalSafety>(); }
                    StartCoroutine(ks.Configure(crew, part));
                }
            }
        }

        public void StopManagingKerbal(KerbalSafety ks)
        {
            if (ks == null || !kerbals.ContainsKey(ks.kerbalName)) return;
            ks.recovered = true;
            kerbals.Remove(ks.kerbalName);
            Destroy(ks);
        }

        HashSet<KerbalEVA> newKerbalsAwaitingCheck = new HashSet<KerbalEVA>();
        public void ManageNewlyEjectedKerbal(KerbalEVA kerbal, Vector3 velocity)
        {
            if (newKerbalsAwaitingCheck.Contains(kerbal)) return;
            newKerbalsAwaitingCheck.Add(kerbal);
            StartCoroutine(ManageNewlyEjectedKerbalCoroutine(kerbal));
            StartCoroutine(ManuallyMoveKerbalEVACoroutine(kerbal, velocity, 2f));
            if (activeVesselBeforeEject != null && activeVesselBeforeEject != FlightGlobals.ActiveVessel) { LoadedVesselSwitcher.Instance.ForceSwitchVessel(activeVesselBeforeEject); }
        }

        IEnumerator ManageNewlyEjectedKerbalCoroutine(KerbalEVA kerbal)
        {
            var kerbalName = kerbal.vessel.vesselName;
            var wait = new WaitForFixedUpdate();
            while (kerbal != null && !kerbal.Ready) yield return wait;
            if (kerbal != null && kerbal.vessel != null)
            {
                CheckVesselForKerbals(kerbal.vessel);
                newKerbalsAwaitingCheck.Remove(kerbal);
            }
            else
            {
                Debug.LogWarning("[BDArmory.KerbalSafety]: " + kerbalName + " disappeared before we could start managing them.");
            }
        }

        /// <summary>
        /// The flight integrator doesn't seem to update the EVA kerbal's position or velocity for about 0.95s of real-time for some unknown reason (this seems fairly constant regardless of time-control or FPS).
        /// </summary>
        /// <param name="kerbal">The kerbal on EVA.</param>
        /// <param name="realTime">The amount of real-time to manually update for.</param>
        IEnumerator ManuallyMoveKerbalEVACoroutine(KerbalEVA kerbal, Vector3 velocity, float realTime = 1f)
        {
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.KerbalSafety]: Manually setting position of " + kerbal.vessel.vesselName + " for " + realTime + "s of real-time.");
            if (!evaKerbalsToMonitor.Contains(kerbal)) evaKerbalsToMonitor.Add(kerbal);
            var gee = (Vector3)FlightGlobals.getGeeForceAtPosition(kerbal.transform.position);
            var verticalSpeed = Vector3.Dot(-gee.normalized, velocity);
            float verticalSpeedAdjustment = 0f;
            var wait = new WaitForFixedUpdate();
            var position = kerbal.vessel.GetWorldPos3D();
            if (kerbal.vessel.radarAltitude + verticalSpeed * Time.fixedDeltaTime < 2f) // Crashed into terrain, explode upwards.
            {
                if (BDArmorySettings.DEBUG_OTHER) verticalSpeedAdjustment = 3f * (float)gee.magnitude - verticalSpeed;
                velocity = velocity.ProjectOnPlanePreNormalized(-gee.normalized) - 3f * (gee + UnityEngine.Random.onUnitSphere * 0.3f * gee.magnitude);
                position += (2f - (float)kerbal.vessel.radarAltitude) * -gee.normalized;
                kerbal.vessel.SetPosition(position); // Put the kerbal back at just above gound level.
                kerbal.vessel.Landed = false;
            }
            else
            {
                velocity += 1.5f * -(gee + UnityEngine.Random.onUnitSphere * 0.3f * gee.magnitude);
                if (BDArmorySettings.DEBUG_OTHER) verticalSpeedAdjustment = 1.5f * (float)gee.magnitude;
            }
            verticalSpeed = Vector3.Dot(-gee.normalized, velocity);
            kerbal.vessel.SetRotation(UnityEngine.Random.rotation);
            kerbal.vessel.rootPart.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1, 2));
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Setting {kerbal.vessel.vesselName}'s position to {position:F2} ({kerbal.vessel.GetWorldPos3D():F2}, altitude: {kerbal.vessel.radarAltitude:F2}, {kerbal.vessel.altitude:F2}) and velocity to {velocity.magnitude:F2} ({verticalSpeed:F2}m/s vertically, adjusted by {verticalSpeedAdjustment:F2}m/s)");
            var startTime = Time.realtimeSinceStartup;
            kerbal.vessel.rootPart.SetDetectCollisions(false);
            while (kerbal != null && kerbal.isActiveAndEnabled && kerbal.vessel != null && kerbal.vessel.isActiveAndEnabled && Time.realtimeSinceStartup - startTime < realTime)
            {
                // Note: 0.968f gives a reduction in speed to ~20% over 1s.
                if (verticalSpeed < 0f && kerbal.vessel.radarAltitude + verticalSpeed * (realTime - (Time.realtimeSinceStartup - startTime)) < 100f)
                {
                    velocity = velocity * 0.968f + gee * verticalSpeed / 10f * Time.fixedDeltaTime;
                    if (BDArmorySettings.DEBUG_OTHER) verticalSpeedAdjustment = Vector3.Dot(-gee.normalized, gee * verticalSpeed / 10f * Time.fixedDeltaTime);
                }
                else
                {
                    velocity = velocity * 0.968f + gee * Time.fixedDeltaTime;
                    if (BDArmorySettings.DEBUG_OTHER) verticalSpeedAdjustment = Vector3.Dot(-gee.normalized, gee * Time.fixedDeltaTime);
                }
                verticalSpeed = Vector3.Dot(-gee.normalized, velocity);
                position += velocity * Time.fixedDeltaTime;
                if (BDKrakensbane.IsActive)
                {
                    position -= BDKrakensbane.FloatingOriginOffsetNonKrakensbane;
                }
                kerbal.vessel.IgnoreGForces(1);
                kerbal.vessel.IgnoreSpeed(1);
                kerbal.vessel.SetPosition(position);
                kerbal.vessel.SetWorldVelocity(velocity);
                yield return wait;
                if (activeVesselBeforeEject != null && activeVesselBeforeEject != FlightGlobals.ActiveVessel) { LoadedVesselSwitcher.Instance.ForceSwitchVessel(activeVesselBeforeEject); }
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.KerbalSafety]: Setting " + kerbal.vessel.vesselName + "'s position to " + position.ToString("0.00") + " (" + kerbal.vessel.GetWorldPos3D().ToString("0.00") + ", altitude: " + kerbal.vessel.radarAltitude.ToString("0.00") + ") and velocity to " + velocity.magnitude.ToString("0.00") + " (" + kerbal.vessel.Velocity().magnitude.ToString("0.00") + ", " + verticalSpeed.ToString("0.00") + "m/s vertically, adjusted by " + verticalSpeedAdjustment.ToString("0.00") + "m/s)." + " (offset: " + !BDKrakensbane.FloatingOriginOffset.IsZero() + ", frameVel: " + !Krakensbane.GetFrameVelocity().IsZero() + ")" + " " + BDKrakensbane.FrameVelocityV3f.ToString("0.0") + ", corr: " + Krakensbane.GetLastCorrection().ToString("0.0"));
            }
            if (kerbal != null && kerbal.vessel != null)
            {
                kerbal.vessel.rootPart.SetDetectCollisions(true);
            }
            if (BDArmorySettings.DEBUG_OTHER)
            {
                for (int count = 0; kerbal != null && kerbal.isActiveAndEnabled && kerbal.vessel != null && kerbal.vessel.isActiveAndEnabled && count < 10; ++count)
                {
                    yield return wait;
                    Debug.Log("[BDArmory.KerbalSafety]: Tracking " + kerbal.vessel.vesselName + "'s position to " + kerbal.vessel.GetWorldPos3D().ToString("0.00") + " (altitude: " + kerbal.vessel.radarAltitude.ToString("0.00") + ") and velocity to " + kerbal.vessel.Velocity().magnitude.ToString("0.00") + " (" + kerbal.vessel.verticalSpeed.ToString("0.00") + "m/s vertically." + " (offset: " + !BDKrakensbane.FloatingOriginOffset.IsZero() + ", frameVel: " + !Krakensbane.GetFrameVelocity().IsZero() + ")" + " " + BDKrakensbane.FrameVelocityV3f.ToString("0.0") + ", corr: " + Krakensbane.GetLastCorrection().ToString("0.0"));
                }
            }
        }

        /// <summary>
        /// Register all the crew members as recovered, then recover the vessel.
        /// </summary>
        /// <param name="vessel">The vessel to recover.</param>
        public void RecoverVesselNow(Vessel vessel)
        {
            foreach (var part in vessel.parts.ToList())
            {
                foreach (var crew in part.protoModuleCrew.ToList())
                {
                    if (kerbals.ContainsKey(crew.displayName))
                    {
                        if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.KerbalSafety]: Recovering " + kerbals[crew.displayName].kerbalName + ".");
                        StopManagingKerbal(kerbals[crew.displayName]);
                    }
                }
            }
            if (vessel.protoVessel != null)
            {
                try
                {
                    foreach (var part in vessel.Parts.ToList()) part.OnJustAboutToBeDestroyed?.Invoke(); // Invoke any OnJustAboutToBeDestroyed events since RecoverVesselFromFlight calls DestroyImmediate, skipping the FX detachment triggers.
                    ShipConstruction.RecoverVesselFromFlight(vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BDArmory.KerbalSafety]: Exception thrown while removing vessel: {e.Message}");
                }
            }
        }

        void EatenByTheKraken(GameEvents.HostedFromToAction<Vessel, CelestialBody> fromTo)
        {
            if (!BDACompetitionMode.Instance.competitionIsActive) return;
            if (evaKerbalsToMonitor.Where(k => k != null).Select(k => k.vessel).Contains(fromTo.host))
            {
                var message = fromTo.host.vesselName + " got eaten by the Kraken!";
                Debug.LogWarning("[BDArmory.KerbalSafety]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                fromTo.host.gameObject.SetActive(false);
                evaKerbalsToMonitor.Remove(evaKerbalsToMonitor.Find(k => k.vessel == fromTo.host));
                fromTo.host.Die();
                LoadedVesselSwitcher.Instance.TriggerSwitchVessel(0);
            }
            else
            {
                if (fromTo.host != null && fromTo.host.loaded)
                {
                    Debug.LogWarning("[BDArmory.KerbalSafety]: " + fromTo.host + " got eaten by the Kraken!");
                    fromTo.host.gameObject.SetActive(false);
                    fromTo.host.Die();
                }
            }
        }

        void OnVesselSwitch(Vessel from, Vessel to)
        {
            var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
            if (to != null && weaponManagers.Contains(VesselModuleRegistry.GetMissileFire(to, true))) // New vessel is an active competitor.
            {
                activeVesselBeforeEject = to;
            }
            else if (from != null && weaponManagers.Contains(VesselModuleRegistry.GetMissileFire(from, true))) // Old vessel is an active competitor.
            {
                activeVesselBeforeEject = from;
            }
        }

        public void ReconfigureInventories()
        {
            if (isEnabled)
            {
                foreach (var kerbal in kerbals.Values)
                { kerbal.ReconfigureInventory(); }
            }
        }
    }

    public class KerbalSafety : MonoBehaviour
    {
        #region Definitions
        public string kerbalName; // The name of the kerbal/crew member.
        public KerbalEVA kerbalEVA; // For kerbals that have ejected or are sitting in command seats.
        public ProtoCrewMember crew; // For kerbals that are in cockpits.
        public Part part; // The part the proto crew member is in.
        public KerbalSeat seat; // The seat the kerbalEVA is in (if they're in one).
        public ModuleEvaChute chute; // The chute of the crew member.
        // public BDModulePilotAI ai; // The pilot AI.
        public bool recovering = false; // Whether they're scheduled for recovery or not.
        public bool recovered = false; // Whether they've been recovered or not.
        public bool deployingChute = false; // Whether they're scheduled for deploying their chute or not.
        public bool ejected = false; // Whether the kerbal has ejected or not.
        public bool leavingSeat = false; // Whether the kerbal is about to leave their seat.
        private string message;
        #endregion

        #region Field definitions
        // [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EjectOnImpendingDoom", // Eject if doomed
        //     groupName = "pilotAI_Ejection", groupDisplayName = "#LOC_BDArmory_PilotAI_Ejection", groupStartCollapsed = true),
        //     UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.02f, scene = UI_Scene.All)]
        // public float ejectOnImpendingDoom = 0.2f; // Time to impact at which to eject.
        #endregion

        /// <summary>
        /// Begin managing a crew member in a part.
        /// </summary>
        /// <param name="c">The proto crew member.</param>
        /// <param name="p">The part.</param>
        public IEnumerator Configure(ProtoCrewMember c, Part p)
        {
            if (c == null)
            {
                Debug.LogError("[BDArmory.KerbalSafety]: Cannot manage null crew.");
                Destroy(this);
                yield break;
            }
            if (p == null)
            {
                Debug.LogError("[BDArmory.KerbalSafety]: Crew cannot exist outside of a part.");
                Destroy(this);
                yield break;
            }
            var wait = new WaitForFixedUpdate();
            while (p.vessel != null && (!p.vessel.loaded || p.vessel.packed)) yield return wait; // Wait for the vessel to be loaded. (Avoids kerbals not being registered in seats.)
            if (p.vessel == null || c == null)
            {
                Debug.LogWarning($"[BDArmory.KerbalSafety]: Vessel or crew is null.");
                Destroy(this);
                yield break;
            }
            kerbalName = c.displayName;
            if (KerbalSafetyManager.Instance.kerbals.ContainsKey(kerbalName)) // Already managed
            {
                Debug.LogWarning($"[BDArmory.KerbalSafety]: {kerbalName} is already being managed!");
                Destroy(this);
                yield break;
            }
            crew = c;
            switch (BDArmorySettings.KERBAL_SAFETY_INVENTORY)
            {
                case 1:
                    crew.ResetInventory(true); // Reset the inventory to the default of a chute and a jetpack.
                    break;
                case 2:
                    crew.ResetInventory(false); // Reset the inventory to just a chute.
                    break;
            }
            part = p;
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Configuring KerbalSafety for {kerbalName} in {part.partInfo.name}");
            if (p.IsKerbalEVA())
            {
                kerbalEVA = p.GetComponent<KerbalEVA>();
                if (kerbalEVA.IsSeated())
                {
                    bool found = false;
                    foreach (var s in VesselModuleRegistry.GetModules<KerbalSeat>(p.vessel))
                    {
                        if (s.Occupant == p)
                        {
                            seat = s;
                            found = true;
                            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} in part {part.partInfo.name} of {part.vessel.vesselName} found in seat {seat.part.partInfo.name}");
                            break;
                        }
                    }
                    if (!found)
                    {
                        Debug.LogWarning("[BDArmory.KerbalSafety]: Failed to find the kerbal seat that " + kerbalName + " occupies.");
                        ejected = true;
                        StartCoroutine(DelayedChuteDeployment());
                        StartCoroutine(RecoverWhenPossible());
                    }
                }
                else // Free-falling EVA kerbal.
                {
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Found a free-falling kerbal {kerbalName}.");
                    ejected = true;
                    StartCoroutine(DelayedChuteDeployment());
                    StartCoroutine(RecoverWhenPossible());
                }
                ConfigureKerbalEVA(kerbalEVA);
            }
            AddHandlers();
            KerbalSafetyManager.Instance.kerbals.Add(kerbalName, this);
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.KerbalSafety]: Managing the safety of " + kerbalName + (ejected ? " on EVA" : " in " + p.vessel.vesselName) + ".");
            OnVesselModified(p.vessel); // Immediately check the vessel.
        }

        private void ConfigureKerbalEVA(KerbalEVA kerbalEVA)
        {
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Introduced in 1.11
                ConfigureKerbalEVA_1_11(kerbalEVA);
            chute = VesselModuleRegistry.GetModule<ModuleEvaChute>(kerbalEVA.vessel);
            if (chute != null)
            {
                chute.deploymentState = ModuleEvaChute.deploymentStates.STOWED; // Make sure the chute is stowed.
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Stowing parachute on {kerbalName}.");
            }
            else if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: No parachute found on {kerbalName}.");
        }

        void ConfigureKerbalEVA_1_11(KerbalEVA kerbalEVA)
        {
            DisableConstructionMode(kerbalEVA);
            if (BDArmorySettings.KERBAL_SAFETY_INVENTORY > 0) kerbalEVA.ModuleInventoryPartReference.SetInventoryDefaults();
            if (BDArmorySettings.KERBAL_SAFETY_INVENTORY == 2) RemoveJetpack(kerbalEVA);
        }

        public void ReconfigureInventory()
        {
            if (BDArmorySettings.KERBAL_SAFETY_INVENTORY == 0) return;
            if (crew != null) crew.ResetInventory(BDArmorySettings.KERBAL_SAFETY_INVENTORY == 1);
            if (kerbalEVA != null) ConfigureKerbalEVA(kerbalEVA);
        }

        private void DisableConstructionMode(KerbalEVA kerbalEVA)
        {
            if (kerbalEVA.InConstructionMode)
                kerbalEVA.InConstructionMode = false;
        }

        private void RemoveJetpack(KerbalEVA kerbalEVA)
        {
            var inventory = kerbalEVA.ModuleInventoryPartReference;
            if (inventory.ContainsPart("evaJetpack"))
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Removing jetpack from {kerbalName}.");
                inventory.RemoveNPartsFromInventory("evaJetpack", 1, false);
            }
            kerbalEVA.part.UpdateMass();
        }

        // void OnDisable() // Find out who's destroying us.
        // {
        //     if (gameObject.activeInHierarchy)
        //     {
        //         Debug.LogError($"DEBUG KerbalSafety {kerbalName} is being destroyed!");
        //     }
        // }

        public void OnDestroy()
        {
            StopAllCoroutines();
            if (KerbalSafetyManager.Instance.safetyLevel != KerbalSafetyLevel.Off && !recovered && BDArmorySettings.DEBUG_OTHER)
            {
                Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} is MIA. Ejected: {ejected}, deployed chute: {deployingChute}.");
            }
            if (KerbalSafetyManager.Instance) KerbalSafetyManager.Instance.StopManagingKerbal(this);
            RemoveHandlers(); // Make sure the handlers get removed.
        }

        /// <summary>
        /// Add various event handlers. 
        /// </summary>
        public void AddHandlers()
        {
            if (kerbalEVA)
            {
                if (seat && seat.part)
                    seat.part.OnJustAboutToDie += Eject;
            }
            else
            {
                if (part)
                    part.OnJustAboutToDie += Eject;
            }
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
            GameEvents.onVesselCreate.Add(OnVesselModified);
            GameEvents.onVesselGoOnRails.Add(OnGoOnRails);
        }

        /// <summary>
        /// Remove the event handlers. 
        /// </summary>
        public void RemoveHandlers()
        {
            if (part) part.OnJustAboutToDie -= Eject;
            if (seat && seat.part) seat.part.OnJustAboutToDie -= Eject;
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
            GameEvents.onVesselCreate.Remove(OnVesselModified);
            GameEvents.onVesselGoOnRails.Remove(OnGoOnRails);
        }

        public void OnGoOnRails(Vessel vessel)
        {
            if (vessel != part.vessel) return;
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {vessel.vesselName} went on rails, no longer managing {kerbalName}.");
            KerbalSafetyManager.Instance.StopManagingKerbal(this);
        }

        // FIXME to be part of an update loop (maybe)
        // void EjectOnImpendingDoom()
        // {
        //     if (!ejected && ejectOnImpendingDoom * (float)vessel.srfSpeed > ai.terrainAlertDistance)
        //     {
        //         KerbalSafety.Instance.Eject(vessel, this); // Abandon ship!
        //         ai.avoidingTerrain = false;
        //     }
        // }

        #region Ejection
        /// <summary>
        /// Eject from a vessel. 
        /// </summary>
        public void Eject()
        {
            if (ejected) return; // We've already ejected.
            if (part == null || part.vessel == null) return; // The vessel is gone, don't try to do anything.
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Ejection triggered for {kerbalName} in {part}.");
            if (kerbalEVA != null)
            {
                if (kerbalEVA.isActiveAndEnabled) // Otherwise, they've been killed already and are being cleaned up by KSP.
                {
                    if (seat != null && kerbalEVA.IsSeated()) // Leave the seat.
                    {
                        if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} is leaving their seat on {seat.part.vessel.vesselName}.");
                        seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                    }
                    else
                    {
                        if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} has already left their seat.");
                    }
                    StartCoroutine(DelayedChuteDeployment());
                    StartCoroutine(RecoverWhenPossible());
                }
            }
            else if (crew != null && part.protoModuleCrew.Contains(crew) && !FlightEVA.hatchInsideFairing(part)) // Eject from a cockpit.
            {
                if (KerbalSafetyManager.Instance.safetyLevel != KerbalSafetyLevel.Full) return;
                if (!ProcessEjection(part)) // All exits were blocked by something.
                {
                    // if (!EjectFromOtherPart()) // Look for other airlocks to spawn from.
                    // {
                    message = kerbalName + " failed to eject from " + part.vessel.vesselName + ", all exits were blocked. R.I.P.";
                    // BDACompetitionMode.Instance.competitionStatus.Add(message);
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.KerbalSafety]: " + message);
                    // }
                }
            }
            else
            {
                Debug.LogWarning("[BDArmory.KerbalSafety]: Ejection called without a kerbal present.");
            }
            ejected = true;
        }

        private bool EjectFromOtherPart()
        {
            Part fromPart = part;
            foreach (var toPart in part.vessel.parts)
            {
                if (toPart == part) continue;
                if (toPart.CrewCapacity > 0 && !FlightEVA.hatchInsideFairing(toPart) && !FlightEVA.HatchIsObstructed(toPart, toPart.airlock))
                {
                    var crewTransfer = CrewTransfer.Create(fromPart, crew, OnDialogDismiss);
                    if (crewTransfer != null && crewTransfer.validParts.Contains(toPart))
                    {
                        if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Transferring {kerbalName} from {fromPart} to {toPart} then ejecting.");
                        crewTransfer.MoveCrewTo(toPart);
                        if (ProcessEjection(toPart))
                            return true;
                        fromPart = toPart;
                    }
                }
            }
            return false;
        }

        private void OnDialogDismiss(PartItemTransfer.DismissAction arg1, Part arg2)
        {
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log(arg1);
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log(arg2);
        }

        private bool ProcessEjection(Part fromPart)
        {
            kerbalEVA = FlightEVA.fetch.spawnEVA(crew, fromPart, fromPart.airlock, true);
            if (KerbalSafetyManager.Instance.activeVesselBeforeEject != null && KerbalSafetyManager.Instance.activeVesselBeforeEject != FlightGlobals.ActiveVessel) { LoadedVesselSwitcher.Instance.ForceSwitchVessel(KerbalSafetyManager.Instance.activeVesselBeforeEject); }
            if (kerbalEVA != null && kerbalEVA.vessel != null)
            {
                CameraManager.Instance.SetCameraFlight();
                if (crew != null && crew.KerbalRef != null)
                {
                    crew.KerbalRef.state = Kerbal.States.BAILED_OUT;
                    fromPart.vessel.RemoveCrew(crew);
                }
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " ejected from " + fromPart.vessel.vesselName + " at " + fromPart.vessel.radarAltitude.ToString("0.00") + "m with velocity " + fromPart.vessel.Velocity().magnitude.ToString("0.00") + "m/s (vertical: " + fromPart.vessel.verticalSpeed + $")");
                kerbalEVA.autoGrabLadderOnStart = false; // Don't grab the vessel.
                kerbalEVA.StartNonCollidePeriod(5f, 1f, fromPart, fromPart.airlock);
                KerbalSafetyManager.Instance.ManageNewlyEjectedKerbal(kerbalEVA, fromPart.vessel.Velocity());
                recovered = true;
                OnDestroy();
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Check various conditions when this vessel gets modified.
        /// </summary>
        /// <param name="vessel">The vessel that was modified.</param>
        public void OnVesselModified(Vessel vessel)
        {
            if (this == null) return;
            if (part == null || vessel == null || !vessel.loaded || part.vessel != vessel) return;
            if (kerbalEVA != null)
            {
                if (kerbalEVA.isActiveAndEnabled)
                {
                    switch (vessel.parts.Count)
                    {
                        case 0: // He's dead, Jim.
                            Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} was killed!");
                            break;
                        case 1: // It's a falling kerbal.
                            if (!ejected)
                            {
                                ejected = true;
                                StartCoroutine(DelayedChuteDeployment());
                                StartCoroutine(RecoverWhenPossible());
                            }
                            break;
                        default: // It's a kerbal in a seat.
                            ejected = false;
                            if (vessel.parts.Count == 2) // Just a kerbal in a seat.
                            {
                                StartCoroutine(DelayedLeaveSeat());
                            }
                            else { } // FIXME What else?
                            break;
                    }
                }
                else
                {
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} was not active (probably dead and being cleaned up by KSP already).");
                    KerbalSafetyManager.Instance.StopManagingKerbal(this);
                }
            }
            else // It's a crew.
            {
                // FIXME Check if the crew needs to eject.
                ejected = false; // Reset ejected flag as failure to eject may have changed due to the vessel modification.
            }
        }

        /// <summary>
        /// Parachute deployment.
        /// </summary>
        /// <param name="delay">Delay before deploying the chute</param>
        IEnumerator DelayedChuteDeployment(float delay = 1f)
        {
            if (deployingChute)
            {
                yield break;
            }
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Deploying chute on {kerbalName} in {delay}s");
            deployingChute = true; // Indicate that we're deploying our chute.
            ejected = true; // Also indicate that we've ejected.
            yield return new WaitForSecondsFixed(delay);
            if (kerbalEVA == null) yield break;
            kerbalEVA.vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
            if (chute != null && !kerbalEVA.IsSeated() && !kerbalEVA.vessel.LandedOrSplashed) // Check that the kerbal hasn't regained their seat or already landed.
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} is falling, deploying halo parachute at {kerbalEVA.vessel.radarAltitude}m.");
                if (chute.deploymentState != ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    chute.deploymentState = ModuleParachute.deploymentStates.STOWED; // Reset the deployment state.
                chute.deployAltitude = 30f;
                chute.Deploy();
            }
            else
            {
                deployingChute = false;
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Not deploying {kerbalName}'s chute due to {(chute == null ? "chute is null" : "")}{(kerbalEVA.IsSeated() ? "still being seated" : "")}{(kerbalEVA.vessel.LandedOrSplashed ? "having already landed/splashed" : "")}.");
            }
            if (FlightGlobals.ActiveVessel == kerbalEVA.vessel)
                LoadedVesselSwitcher.Instance.TriggerSwitchVessel(1f);
        }

        /// <summary>
        /// Leave seat after a short delay.
        /// </summary>
        /// <param name="delay">Delay before leaving seat.</param>
        IEnumerator DelayedLeaveSeat(float delay = 3f)
        {
            if (leavingSeat)
            {
                yield break;
            }
            leavingSeat = true;
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: {kerbalName} is leaving seat in {delay}s.");
            yield return new WaitForSecondsFixed(delay);
            if (seat != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Found {kerbalName} in a combat chair just falling, ejecting.");
                seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                ejected = true;
                StartCoroutine(DelayedChuteDeployment());
                StartCoroutine(RecoverWhenPossible());
            }
        }

        /// <summary>
        /// Recover the kerbal when possible (has landed and isn't the active vessel).
        /// </summary>
        /// <param name="asap">Don't wait until the kerbal has landed.</param>
        public IEnumerator RecoverWhenPossible(bool asap = false)
        {
            if (asap)
            {
                if (KerbalSafetyManager.Instance.kerbals.ContainsKey(kerbalName))
                    KerbalSafetyManager.Instance.kerbals.Remove(kerbalName); // Stop managing this kerbal.
            }
            if (recovering)
            {
                yield break;
            }
            recovering = true;
            if (!asap)
            {
                yield return new WaitUntilFixed(() => kerbalEVA == null || kerbalEVA.vessel.LandedOrSplashed);
                yield return new WaitForSecondsFixed(5); // Give it around 5s after landing, then recover the kerbal
            }
            yield return new WaitUntilFixed(() => kerbalEVA == null || FlightGlobals.ActiveVessel != kerbalEVA.vessel);
            if (KerbalSafetyManager.Instance.kerbals.ContainsKey(kerbalName))
                KerbalSafetyManager.Instance.kerbals.Remove(kerbalName); // Stop managing this kerbal.
            if (kerbalEVA == null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.LogError($"[BDArmory.KerbalSafety]: {kerbalName} on EVA is MIA.");
                yield break;
            }
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.KerbalSafety]: Recovering {kerbalName}.");
            recovered = true;
            try
            {
                foreach (var part in kerbalEVA.vessel.Parts) part.OnJustAboutToBeDestroyed?.Invoke(); // Invoke any OnJustAboutToBeDestroyed events since RecoverVesselFromFlight calls DestroyImmediate, skipping the FX detachment triggers.
                ShipConstruction.RecoverVesselFromFlight(kerbalEVA.vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BDArmory.KerbalSafety]: Exception thrown while removing vessel: {e.Message}");
            }
        }
        #endregion
    }
}