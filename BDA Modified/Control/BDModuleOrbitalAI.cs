using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Targeting;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Weapons;
using BDArmory.Guidances;

namespace BDArmory.Control
{
    public class BDModuleOrbitalAI : BDGenericAIBase, IBDAIControl
    {
        // Code contained within this file is adapted from Hatbat, Spartwo and MiffedStarfish's Kerbal Combat Systems Mod https://github.com/Halbann/StockCombatAI/tree/dev/Source/KerbalCombatSystems.
        // Code is distributed under CC-BY-SA 4.0: https://creativecommons.org/licenses/by-sa/4.0/

        #region Declarations

        // Orbiter AI variables.
        public float updateInterval;
        public float emergencyUpdateInterval = 0.5f;
        public float combatUpdateInterval = 2.5f;
        private bool allowWithdrawal = true;
        public float firingAngularVelocityLimit = 1; // degrees per second

        private BDOrbitalControl fc;

        public IBDWeapon currentWeapon;

        private float trackedDeltaV;
        private Vector3 attitudeCommand;
        private PilotCommands lastUpdateCommand = PilotCommands.Free;
        private float maneuverTime;
        private float minManeuverTime;
        private bool maneuverStateChanged = false;
        private bool belowSafeAlt = false;
        private bool wasDescendingUnsafe = false;
        private bool hasPropulsion;
        private bool hasWeapons;
        private float maxAcceleration;
        private Vector3 maxAngularAcceleration;
        private Vector3 availableTorque;
        private double minSafeAltitude;
        private CelestialBody safeAltBody = null;

        // Evading
        bool evadingGunfire = false;
        float evasiveTimer;
        Vector3 threatRelativePosition;
        Vector3 evasionNonLinearityDirection;
        string evasionString = " & Evading Gunfire";

        // User parameters changed via UI.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEngagementRange"),//Min engagement range
            UI_FloatSemiLogRange(minValue = 10f, maxValue = 10000f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ManeuverRCS"),//RCS active
            UI_Toggle(enabledText = "#LOC_BDArmory_ManeuverRCS_enabledText", disabledText = "#LOC_BDArmory_ManeuverRCS_disabledText", scene = UI_Scene.All),]//Maneuvers--Combat
        public bool ManeuverRCS = false;

        public float vesselStandoffDistance = 200f; // try to avoid getting closer than 200m

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "#LOC_BDArmory_ManeuverSpeed",
            guiUnits = " m/s"),
            UI_FloatSemiLogRange(
                minValue = 10f,
                maxValue = 10000f,
                stepIncrement = 10f,
                scene = UI_Scene.All
            )]
        public float ManeuverSpeed = 100f;

        [KSPField(isPersistant = true,
            guiActive = true,
            guiActiveEditor = true,
            guiName = "#LOC_BDArmory_StrafingSpeed",
            guiUnits = " m/s"),
            UI_FloatSemiLogRange(
                minValue = 2f,
                maxValue = 1000f,
                scene = UI_Scene.All
            )]
        public float firingSpeed = 20f;

        #region Evade
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEvasionTime", advancedTweakable = true, // Min Evasion Time
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)]
        public float minEvasionTime = 0.2f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionThreshold", advancedTweakable = true, //Evasion Distance Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float evasionThreshold = 25f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionTimeThreshold", advancedTweakable = true, // Evasion Time Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float evasionTimeThreshold = 0.1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionErraticness", advancedTweakable = true, // Evasion Erraticness
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float evasionErraticness = 0.1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionMinRangeThreshold", advancedTweakable = true, // Evasion Min Range Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatSemiLogRange(minValue = 10f, maxValue = 10000f, sigFig = 1)]
        public float evasionMinRangeThreshold = 10f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionIgnoreMyTargetTargetingMe", advancedTweakable = true,//Ignore my target targeting me
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_Toggle(enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled", scene = UI_Scene.All),]
        public bool evasionIgnoreMyTargetTargetingMe = false;
        #endregion


        // Debugging
        internal float nearInterceptBurnTime;
        internal float nearInterceptApproachTime;
        internal float lateralVelocity;
        internal Vector3 debugPosition;


        /// <summary>
        /// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>

        Vector3 upDir;

        #endregion

        #region Status Mode
        public enum StatusMode { Idle, Evading, CorrectingOrbit, Withdrawing, Firing, Maneuvering, Stranded, Commanded, Custom }
        public StatusMode currentStatusMode = StatusMode.Idle;
        StatusMode lastStatusMode = StatusMode.Idle;
        protected override void SetStatus(string status)
        {
            if (evadingGunfire)
                status += evasionString;

            base.SetStatus(status);
            if (status.StartsWith("Idle")) currentStatusMode = StatusMode.Idle;
            else if (status.StartsWith("Correcting Orbit")) currentStatusMode = StatusMode.CorrectingOrbit;
            else if (status.StartsWith("Evading")) currentStatusMode = StatusMode.Evading;
            else if (status.StartsWith("Withdrawing")) currentStatusMode = StatusMode.Withdrawing;
            else if (status.StartsWith("Firing")) currentStatusMode = StatusMode.Firing;
            else if (status.StartsWith("Maneuvering")) currentStatusMode = StatusMode.Maneuvering;
            else if (status.StartsWith("Stranded")) currentStatusMode = StatusMode.Stranded;
            else if (status.StartsWith("Commanded")) currentStatusMode = StatusMode.Commanded;
            else currentStatusMode = StatusMode.Custom;
        }
        #endregion

        #region RMB info in editor

        // <color={XKCDColors.HexFormat.Lime}>Yes</color>
        public override string GetInfo()
        {
            // known bug - the game caches the RMB info, changing the variable after checking the info
            // does not update the info. :( No idea how to force an update.
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Available settings</b>:");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Engagement Range</color> - AI will try to move away from oponents if closer than this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- RCS Active</color> - Use RCS during any maneuvers, or only in combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Maneuver Speed</color> - Max speed relative to target during intercept maneuvers");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Strafing Speed</color> - Max speed relative to target during gun firing");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Evasion Time</color> - Minimum seconds AI will evade for");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Distance Threshold</color> - How close incoming gunfire needs to come to trigger evasion");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Time Threshold</color> - How many seconds the AI needs to be under fire to begin evading");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Min Range Threshold</color> - Attacker needs to be beyond this range to trigger evasion");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Don't Evade My Target</color> - Whether gunfire from the current target is ignored for evasion");
            }
            return sb.ToString();
        }

        #endregion RMB info in editor

        #region events

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
                GameEvents.onVesselPartCountChanged.Add(CalculateAvailableTorque);
            CalculateAvailableTorque(vessel);
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(CalculateAvailableTorque);
            base.OnDestroy();
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            if (!fc)
            {
                fc = gameObject.AddComponent<BDOrbitalControl>();
                fc.vessel = vessel;

                fc.alignmentToleranceforBurn = 7.5f;
                fc.throttleLerpRate = 3;
            }
            fc.Activate();
        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (fc)
            {
                fc.Deactivate();
                fc = null;
            }

            evadingGunfire = false;
            SetStatus("");
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DEBUG_LINES) return;
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugPosition, 5, Color.red); // Target intercept position
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.attitude * 100, 5, Color.green); // Attitude command
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.RCSVector * 100, 5, Color.cyan); // RCS command
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, fc.RCSVectorLerped * 100, 5, Color.blue); // RCS lerped command

            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 1000, 3, Color.white);
        }

        #endregion events

        #region Actual AI Pilot
        protected override void AutoPilot(FlightCtrlState s)
        {
            // Update vars
            InitialFrameUpdates();

            UpdateStatus(); // Combat decisions, evasion, maneuverStateChanged = true and set new statusMode, etc.

            maneuverTime += Time.fixedDeltaTime;
            if (maneuverStateChanged || maneuverTime > minManeuverTime)
            {
                maneuverTime = 0;
                evasionNonLinearityDirection = UnityEngine.Random.onUnitSphere;
                fc.lerpAttitude = true;
                minManeuverTime = combatUpdateInterval;
                switch (currentStatusMode)
                {
                    case StatusMode.Evading:
                        minManeuverTime = emergencyUpdateInterval;
                        break;
                    case StatusMode.CorrectingOrbit:
                        break;
                    case StatusMode.Withdrawing:
                        {
                            // Determine the direction.
                            Vector3 averagePos = Vector3.zero;
                            using (List<TargetInfo>.Enumerator target = BDATargetManager.TargetList(weaponManager.Team).GetEnumerator())
                                while (target.MoveNext())
                                {
                                    if (target.Current == null) continue;
                                    if (target.Current && target.Current.Vessel && weaponManager.CanSeeTarget(target.Current))
                                    {
                                        averagePos += FromTo(vessel, target.Current.Vessel).normalized;
                                    }
                                }

                            Vector3 direction = -averagePos.normalized;
                            Vector3 orbitNormal = vessel.orbit.Normal(Planetarium.GetUniversalTime());
                            bool facingNorth = Vector3.Dot(direction, orbitNormal) > 0;
                            trackedDeltaV = 200;
                            attitudeCommand = (orbitNormal * (facingNorth ? 1 : -1)).normalized;
                        }
                        break;
                    case StatusMode.Commanded:
                        {
                            lastUpdateCommand = currentCommand;
                            if (maneuverStateChanged)
                            {
                                if (currentCommand == PilotCommands.Follow)
                                    attitudeCommand = commandLeader.transform.up;
                                else
                                    attitudeCommand = (assignedPositionWorld - vessel.transform.position).normalized;
                            }
                            minManeuverTime = 30f;
                            trackedDeltaV = 200;
                        }
                        break;
                    case StatusMode.Firing:
                        fc.lerpAttitude = false;
                        break;
                    case StatusMode.Maneuvering:
                        break;
                    case StatusMode.Stranded:
                        break;
                    default: // Idle
                        break;
                }
            }
            Maneuver(); // Set attitude, alignment tolerance, throttle, update RCS if needed

            AddDebugMessages();
        }

        void InitialFrameUpdates()
        {
            upDir = vessel.up;
            CalculateAngularAcceleration();
            maxAcceleration = GetMaxAcceleration(vessel);
            debugPosition = Vector3.zero;
            fc.alignmentToleranceforBurn = 5;
            fc.throttle = 0;
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, ManeuverRCS);
            maneuverStateChanged = false;
        }

        void Maneuver()
        {
            Vector3 rcsVector = Vector3.zero;
            switch (currentStatusMode)
            {
                case StatusMode.Evading:
                    {
                        SetStatus("Evading Missile");
                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        Vector3 incomingVector = FromTo(vessel, weaponManager.incomingMissileVessel);
                        Vector3 dodgeVector = Vector3.ProjectOnPlane(vessel.ReferenceTransform.up, incomingVector.normalized);

                        fc.attitude = dodgeVector;
                        fc.alignmentToleranceforBurn = 45;
                        fc.throttle = 1;
                        rcsVector = dodgeVector;
                    }
                    break;
                case StatusMode.CorrectingOrbit:
                    {
                        Orbit o = vessel.orbit;
                        double UT = Planetarium.GetUniversalTime();
                        if (!belowSafeAlt && (o.ApA < 0 && o.timeToPe < -60))
                        {
                            // Vessel is on an escape orbit and has passed the periapsis by over 60s, burn retrograde
                            SetStatus("Correcting Orbit (On escape trajectory)");
                            fc.attitude = -o.Prograde(UT);
                            fc.throttle = 1;
                        }
                        else if (!belowSafeAlt && (o.ApA >= minSafeAltitude) && (o.altitude >= minSafeAltitude))
                        {
                            // We are outside the atmosphere but our periapsis is inside the atmosphere.
                            // Execute a burn to circularize our orbit at the current altitude.
                            SetStatus("Correcting Orbit (Circularizing)");

                            Vector3d fvel = Math.Sqrt(o.referenceBody.gravParameter / o.GetRadiusAtUT(UT)) * o.Horizontal(UT);
                            Vector3d deltaV = fvel - vessel.GetObtVelocity();

                            fc.attitude = deltaV.normalized;
                            fc.throttle = Mathf.Lerp(0, 1, (float)(deltaV.sqrMagnitude / 100));
                        }
                        else
                        {
                            belowSafeAlt = true;
                            var descending = o.timeToPe > 0 && o.timeToPe < o.timeToAp;
                            if (o.ApA < minSafeAltitude * 1.1)
                            {
                                // Entirety of orbit is inside atmosphere, perform gravity turn burn until apoapsis is outside atmosphere by a 10% margin.

                                SetStatus("Correcting Orbit (Apoapsis too low)");

                                double gravTurnAlt = 0.1;
                                float turn;

                                if (o.altitude < gravTurnAlt * minSafeAltitude || descending || wasDescendingUnsafe) // At low alts or when descending, burn straight up
                                {
                                    turn = 1f;
                                    fc.alignmentToleranceforBurn = 45f; // Use a wide tolerance as aero forces could make it difficult to align otherwise.
                                    wasDescendingUnsafe = descending || o.timeToAp < 10; // Hysteresis for upwards vs gravity turn burns.
                                }
                                else // At higher alts, gravity turn towards horizontal orbit vector
                                {
                                    turn = Mathf.Clamp((float)((1.1 * minSafeAltitude - o.ApA) / (minSafeAltitude * (1.1 - gravTurnAlt))), 0.1f, 1f);
                                    turn = Mathf.Clamp(Mathf.Log10(turn) + 1f, 0.33f, 1f);
                                    fc.alignmentToleranceforBurn = Mathf.Clamp(15f * turn, 5f, 15f);
                                    wasDescendingUnsafe = false;
                                }

                                fc.attitude = Vector3.Lerp(o.Horizontal(UT), upDir, turn);
                                fc.throttle = 1;
                            }
                            else if (o.altitude < minSafeAltitude * 1.1 && descending)
                            {
                                // Our apoapsis is outside the atmosphere but we are inside the atmosphere and descending.
                                // Burn up until we are ascending and our apoapsis is outside the atmosphere by a 10% margin.

                                SetStatus("Correcting Orbit (Falling inside atmo)");

                                fc.attitude = o.Radial(UT);
                                fc.alignmentToleranceforBurn = 45f; // Use a wide tolerance as aero forces could make it difficult to align otherwise.
                                fc.throttle = 1;
                            }
                            else
                            {
                                SetStatus("Correcting Orbit (Drifting)");
                                belowSafeAlt = false;
                            }
                        }
                    }
                    break;
                case StatusMode.Commanded:
                    {
                        // We have been given a command from the WingCommander to fly/follow/attack in a general direction
                        // Burn for 200 m/s then coast remainder of 30s period
                        switch (currentCommand)
                        {
                            case PilotCommands.Follow:
                                SetStatus("Commanded to Follow Leader");
                                break;
                            case PilotCommands.Attack:
                                SetStatus("Commanded to Attack");
                                break;
                            default: // Fly To
                                SetStatus("Commanded to Position");
                                break;
                        }
                        trackedDeltaV -= Vector3.Project(vessel.acceleration, attitudeCommand).magnitude * TimeWarp.fixedDeltaTime;
                        fc.attitude = attitudeCommand;
                        fc.throttle = (trackedDeltaV > 10) ? 1 : 0;
                    }
                    break;
                case StatusMode.Withdrawing:
                    {
                        SetStatus("Withdrawing");

                        // Withdraw sequence. Locks behaviour while burning 200 m/s of delta-v either north or south.
                        trackedDeltaV -= Vector3.Project(vessel.acceleration, attitudeCommand).magnitude * TimeWarp.fixedDeltaTime;
                        fc.attitude = attitudeCommand;
                        fc.throttle = (trackedDeltaV > 10) ? 1 : 0;
                    }
                    break;
                case StatusMode.Firing:
                    {
                        // Aim at appropriate point to launch missiles that aren't able to launch now
                        SetStatus("Firing Missiles");
                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        fc.lerpAttitude = false;
                        Vector3 firingSolution = FromTo(vessel, targetVessel).normalized;

                        if (weaponManager.currentGun && GunReady(weaponManager.currentGun))
                        {
                            SetStatus("Firing Guns");
                            firingSolution = weaponManager.currentGun.FiringSolutionVector ?? Vector3.zero;
                        }
                        else if (weaponManager.CurrentMissile && !weaponManager.GetLaunchAuthorization(targetVessel, weaponManager, weaponManager.CurrentMissile))
                        {
                            SetStatus("Firing Missiles");
                            firingSolution = MissileGuidance.GetAirToAirFireSolution(weaponManager.CurrentMissile, targetVessel);
                        }
                        else
                            SetStatus("Firing");


                        fc.attitude = firingSolution;
                        fc.throttle = 0;
                        rcsVector = -Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), FromTo(vessel, targetVessel));
                    }
                    break;
                case StatusMode.Maneuvering:
                    {
                        // todo: implement for longer range movement.
                        // https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/MechJebModuleRendezvousAutopilot.cs
                        // https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/OrbitalManeuverCalculator.cs
                        // https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/MechJebLib/Maths/Gooding.cs

                        float minRange = Mathf.Max(MinEngagementRange, targetVessel.GetRadius() + vesselStandoffDistance);
                        float maxRange = Mathf.Max(weaponManager.gunRange, minRange * 1.2f);

                        float minRangeProjectile = minRange;
                        bool complete = false;
                        bool usingProjectile = true;

                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

                        if (weaponManager != null && weaponManager.selectedWeapon != null)
                        {
                            currentWeapon = weaponManager.selectedWeapon;
                            minRange = Mathf.Max((currentWeapon as EngageableWeapon).engageRangeMin, minRange);
                            maxRange = Mathf.Min((currentWeapon as EngageableWeapon).engageRangeMax, maxRange);
                            usingProjectile = weaponManager.selectedWeapon.GetWeaponClass() != WeaponClasses.Missile;
                        }

                        float currentRange = VesselDistance(vessel, targetVessel);
                        bool nearInt = false;
                        Vector3 relVel = RelVel(vessel, targetVessel);

                        if (currentRange < (!usingProjectile ? minRange : minRangeProjectile) && AwayCheck(minRange))
                        {
                            SetStatus("Maneuvering (Away)");
                            fc.throttle = 1;
                            fc.alignmentToleranceforBurn = 135;
                            fc.attitude = FromTo(targetVessel, vessel).normalized;
                            fc.throttle = Vector3.Dot(RelVel(vessel, targetVessel), fc.attitude) < ManeuverSpeed ? 1 : 0;
                        }
                        // Reduce near intercept time by accounting for target acceleration
                        // It should be such that "near intercept" is so close that you would go past them after you stop burning despite their acceleration
                        // Also a chase timeout after which both parties should just use their weapons regardless of range.
                        else if (hasPropulsion
                            && currentRange > maxRange
                            && !(nearInt = NearIntercept(relVel, minRange))
                            && CanInterceptShip(targetVessel))
                        {
                            SetStatus("Maneuvering (Intercept Target)"); // FIXME Josue If possible, it would be nice to see the approximate min separation and time to min separation in the status here and in the Kill Velocity status.
                            Vector3 toTarget = FromTo(vessel, targetVessel);
                            relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

                            toTarget = ToClosestApproach(toTarget, -relVel, minRange);
                            debugPosition = toTarget;

                            // Burn the difference between the target and current velocities.
                            Vector3 desiredVel = toTarget.normalized * ManeuverSpeed;
                            Vector3 burn = desiredVel + relVel;

                            // Bias towards eliminating lateral velocity early on.
                            Vector3 lateral = Vector3.ProjectOnPlane(burn, toTarget.normalized);
                            burn = Vector3.Slerp(burn.normalized, lateral.normalized,
                                Mathf.Clamp01(lateral.magnitude / (maxAcceleration * 10))) * burn.magnitude;

                            lateralVelocity = lateral.magnitude;

                            float throttle = Vector3.Dot(RelVel(vessel, targetVessel), toTarget.normalized) < ManeuverSpeed ? 1 : 0;
                            if (burn.magnitude / maxAcceleration < 1 && fc.throttle == 0)
                                throttle = 0;

                            fc.throttle = throttle * Mathf.Clamp(burn.magnitude / maxAcceleration, 0.2f, 1);

                            if (fc.throttle > 0)
                                fc.attitude = burn.normalized;
                            else
                                fc.attitude = toTarget.normalized;
                        }
                        else
                        {
                            if (hasPropulsion && (relVel.sqrMagnitude > firingSpeed * firingSpeed || nearInt))
                            {
                                SetStatus("Maneuvering (Kill Velocity)");
                                relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
                                complete = relVel.sqrMagnitude < firingSpeed * firingSpeed / 9;
                                fc.attitude = (relVel + targetVessel.acceleration).normalized;
                                fc.throttle = !complete ? 1 : 0;
                            }
                            else if (hasPropulsion && targetVessel != null && AngularVelocity(vessel, targetVessel) > firingAngularVelocityLimit)
                            {
                                SetStatus("Maneuvering (Kill Angular Velocity)");
                                complete = AngularVelocity(vessel, targetVessel) < firingAngularVelocityLimit / 2;
                                fc.attitude = -Vector3.ProjectOnPlane(RelVel(vessel, targetVessel), FromTo(vessel, targetVessel)).normalized;
                                fc.throttle = !complete ? 1 : 0;
                            }
                            else // Drifting
                            {
                                fc.throttle = 0;
                                fc.attitude = FromTo(vessel, targetVessel).normalized;
                                if (currentRange < minRange)
                                    SetStatus("Maneuvering (Drift Away)");
                                else
                                    SetStatus("Maneuvering (Drift)");
                            }
                        }
                    }
                    break;
                case StatusMode.Stranded:
                    {
                        SetStatus("Stranded");

                        fc.attitude = FromTo(vessel, targetVessel).normalized;
                        fc.throttle = 0;
                    }
                    break;
                default: // Idle
                    {
                        if (hasWeapons)
                            SetStatus("Idle");
                        else
                            SetStatus("Idle (Unarmed)");

                        fc.attitude = Vector3.zero;
                        fc.throttle = 0;
                    }
                    break;
            }
            UpdateRCSVector(rcsVector);
        }

        void AddDebugMessages()
        {
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI)
            {
                debugString.AppendLine($"Current Status: {currentStatus}");
                debugString.AppendLine($"Has Propulsion: {hasPropulsion}");
                debugString.AppendLine($"Has Weapons: {hasWeapons}");
                if (targetVessel)
                {
                    Vector3 relVel = RelVel(vessel, targetVessel);
                    float minRange = Mathf.Max(MinEngagementRange, targetVessel.GetRadius() + vesselStandoffDistance);
                    debugString.AppendLine($"Target Vessel: {targetVessel.GetDisplayName()}");
                    debugString.AppendLine($"Can Intercept: {CanInterceptShip(targetVessel)}");
                    debugString.AppendLine($"Near Intercept: {NearIntercept(relVel, minRange)}");
                    debugString.AppendLine($"Near Intercept Burn Time: {nearInterceptBurnTime:G3}");
                    debugString.AppendLine($"Near Intercept Approach Time: {nearInterceptApproachTime:G3}");
                    debugString.AppendLine($"Lateral Velocity: {lateralVelocity:G3}");
                }
                debugString.AppendLine($"Evasive {evasiveTimer}s");
                if (weaponManager) debugString.AppendLine($"Threat Sqr Distance: {weaponManager.incomingThreatDistanceSqr}");
            }
        }

        void UpdateStatus()
        {
            // Update propulsion and weapon status
            bool hasRCSFore = VesselModuleRegistry.GetModules<ModuleRCS>(vessel).Any(e => e.rcsEnabled && !e.flameout && e.useThrottle);
            hasPropulsion = hasRCSFore || VesselModuleRegistry.GetModuleEngines(vessel).Any(e => (e.EngineIgnited && e.isOperational));
            hasWeapons = (weaponManager != null) && weaponManager.HasWeaponsAndAmmo();

            // Check on command status
            UpdateCommand();

            // FIXME Josue There seems to be a fair bit of oscillation between circularising, intercept velocity and kill velocity in my tests, with the craft repeatedly rotating 180° to perform burns in opposite directions.
            // In particular, this happens a lot when the craft's periapsis is at the min safe altitude, which occurs frequently if the spawn distance is large enough to give significant inclinations.
            // I think there needs to be some manoeuvre logic to better handle this condition, such as modifying burns that would bring the periapsis below the min safe altitude, which might help with inclination shifts.
            // Also, maybe some logic to ignore targets that will fall below the min safe altitude before they can be reached could be useful.

            // Update status mode
            if (weaponManager && weaponManager.missileIsIncoming && weaponManager.incomingMissileVessel && weaponManager.incomingMissileTime <= weaponManager.evadeThreshold) // Needs to start evading an incoming missile.
                currentStatusMode = StatusMode.Evading;
            else if (CheckOrbitUnsafe() || belowSafeAlt)
                currentStatusMode = StatusMode.CorrectingOrbit;
            else if (currentCommand == PilotCommands.FlyTo || currentCommand == PilotCommands.Follow || currentCommand == PilotCommands.Attack)
            {
                currentStatusMode = StatusMode.Commanded;
                if (currentCommand != lastUpdateCommand)
                    maneuverStateChanged = true;
            }
            else if (weaponManager)
            {
                if (allowWithdrawal && hasPropulsion && !hasWeapons && CheckWithdraw())
                    currentStatusMode = StatusMode.Withdrawing;
                else if (targetVessel != null && weaponManager.currentGun && GunReady(weaponManager.currentGun))
                    currentStatusMode = StatusMode.Firing; // Guns
                else if (targetVessel != null && weaponManager.CurrentMissile && !weaponManager.GetLaunchAuthorization(targetVessel, weaponManager, weaponManager.CurrentMissile))
                    currentStatusMode = StatusMode.Firing; // Missiles
                else if (targetVessel != null && weaponManager.CurrentMissile && weaponManager.guardFiringMissile && currentStatusMode == StatusMode.Firing)
                    currentStatusMode = StatusMode.Firing; // Post-launch authorization missile firing underway, don't change status from Firing
                else if (targetVessel != null && hasWeapons)
                {
                    if (hasPropulsion)
                        currentStatusMode = StatusMode.Maneuvering;
                    else
                        currentStatusMode = StatusMode.Stranded;
                }
                else
                    currentStatusMode = StatusMode.Idle;
            }
            else
                currentStatusMode = StatusMode.Idle;

            // Flag changed status if necessary
            if (lastStatusMode != currentStatusMode || maneuverStateChanged)
            {
                maneuverStateChanged = true;
                lastStatusMode = currentStatusMode;
                if (BDArmorySettings.DEBUG_AI)
                    Debug.Log("[BDArmory.BDModuleOrbitalAI]: Status of " + vessel.vesselName + " changed from " + lastStatusMode + " to " + currentStatus);
            }

            // Temporarily inhibit maneuvers if not evading a missile and waiting for a launched missile to fly to a safe distance
            if (currentStatusMode != StatusMode.Evading && weaponManager && weaponManager.PreviousMissile)
            {
                if ((vessel.CoM - weaponManager.PreviousMissile.vessel.transform.position).sqrMagnitude < vessel.vesselSize.sqrMagnitude)
                    fc.Stability(true);
                else
                    fc.Stability(false);
            }
            else
                fc.Stability(false);

            // Check for incoming gunfire
            EvasionStatus();

            // Set target as UI target
            if (vessel.isActiveVessel && targetVessel && !targetVessel.IsMissile() && (vessel.targetObject == null || vessel.targetObject.GetVessel() != targetVessel))
            {
                FlightGlobals.fetch.SetVesselTarget(targetVessel, true);
            }
        }

        void UpdateCommand()
        {
            if (command == PilotCommands.Follow && commandLeader is null)
            {
                ReleaseCommand();
                return;
            }
            else if (command == PilotCommands.Attack)
            {
                if (targetVessel != null)
                {
                    ReleaseCommand(false);
                    return;
                }
                else if (weaponManager.underAttack || weaponManager.underFire)
                {
                    ReleaseCommand(false);
                    return;
                }
            }
        }

        void EvasionStatus()
        {
            evadingGunfire = false;

            // Return if evading missile
            if (currentStatusMode == StatusMode.Evading)
            {
                evasiveTimer = 0;
                return;
            }

            // Check if we should be evading gunfire, missile evasion is handled separately
            float threatRating = evasionThreshold + 1f; // Don't evade by default
            if (weaponManager != null && weaponManager.underFire)
            {
                if (weaponManager.incomingMissTime >= evasionTimeThreshold && weaponManager.incomingThreatDistanceSqr >= evasionMinRangeThreshold * evasionMinRangeThreshold) // If we haven't been under fire long enough or they're too close, ignore gunfire
                    threatRating = weaponManager.incomingMissDistance;
            }
            // If we're currently evading or a threat is significant
            if ((evasiveTimer < minEvasionTime && evasiveTimer != 0) || threatRating < evasionThreshold)
            {
                if (evasiveTimer < minEvasionTime)
                {
                    threatRelativePosition = vessel.Velocity().normalized + vesselTransform.right;
                    if (weaponManager)
                    {
                        if (weaponManager.underFire)
                            threatRelativePosition = weaponManager.incomingThreatPosition - vesselTransform.position;
                    }
                }
                evadingGunfire = true;
                evasionNonLinearityDirection = (evasionNonLinearityDirection + evasionErraticness * UnityEngine.Random.onUnitSphere).normalized;
                evasiveTimer += Time.fixedDeltaTime;

                if (evasiveTimer >= minEvasionTime)
                    evasiveTimer = 0;
            }
        }
        #endregion Actual AI Pilot

        #region Utility Functions

        private bool CheckWithdraw()
        {
            var nearest = BDATargetManager.GetClosestTarget(weaponManager);
            if (nearest == null) return false;

            return RelVel(vessel, nearest.Vessel).sqrMagnitude < 200 * 200;
        }

        private bool CheckOrbitUnsafe()
        {
            Orbit o = vessel.orbit;
            if (o.referenceBody != safeAltBody) // Body has been updated, update min safe alt
            {
                minSafeAltitude = o.referenceBody.MinSafeAltitude();
                safeAltBody = o.referenceBody;
            }

            return (o.PeA < minSafeAltitude && o.timeToPe < o.timeToAp) || (o.ApA < minSafeAltitude && (o.ApA >= 0 || o.timeToPe < -60)); // Match conditions in PilotLogic
        }

        private bool NearIntercept(Vector3 relVel, float minRange)
        {
            float timeToKillVelocity = relVel.magnitude / Mathf.Max(maxAcceleration, 0.01f);

            float rotDistance = Vector3.Angle(vessel.ReferenceTransform.up, -relVel.normalized) * Mathf.Deg2Rad;
            float timeToRotate = BDAMath.SolveTime(rotDistance * 0.75f, maxAngularAcceleration.magnitude) / 0.75f;

            Vector3 toTarget = FromTo(vessel, targetVessel);
            Vector3 toClosestApproach = ToClosestApproach(toTarget, relVel, minRange);

            // Return false if we aren't headed towards the target.
            float velToClosestApproach = Vector3.Dot(relVel, toTarget.normalized);
            if (velToClosestApproach < 10)
                return false;

            float timeToClosestApproach = AIUtils.TimeToCPA(toClosestApproach, -relVel, Vector3.zero, 9999);
            if (timeToClosestApproach == 0)
                return false;

            nearInterceptBurnTime = timeToKillVelocity + timeToRotate;
            nearInterceptApproachTime = timeToClosestApproach;

            return timeToClosestApproach < (timeToKillVelocity + timeToRotate);
        }

        private bool CanInterceptShip(Vessel target)
        {
            bool canIntercept = false;

            // Is it worth us chasing a withdrawing ship?
            BDModuleOrbitalAI targetAI = VesselModuleRegistry.GetModule<BDModuleOrbitalAI>(target);

            if (targetAI)
            {
                Vector3 toTarget = target.CoM - vessel.CoM;
                bool escaping = targetAI.currentStatusMode == StatusMode.Withdrawing;

                canIntercept = !escaping || // It is not trying to escape.
                    toTarget.sqrMagnitude < weaponManager.gunRange * weaponManager.gunRange || // It is already in range.
                    maxAcceleration * maxAcceleration > targetAI.vessel.acceleration_immediate.sqrMagnitude || //  We are faster (currently).
                    Vector3.Dot(target.GetObtVelocity() - vessel.GetObtVelocity(), toTarget) < 0; // It is getting closer.
            }
            return canIntercept;
        }

        private bool GunReady(ModuleWeapon gun)
        {
            if (gun == null) return false;

            // Check gun/laser can fire soon, we are within guard and weapon engagement ranges, and we are under the firing speed
            float targetSqrDist = FromTo(vessel, targetVessel).sqrMagnitude;
            return RelVel(vessel, targetVessel).sqrMagnitude < firingSpeed * firingSpeed &&
                gun.CanFireSoon() &&
                (targetSqrDist <= gun.GetEngagementRangeMax() * gun.GetEngagementRangeMax()) &&
                (targetSqrDist <= weaponManager.gunRange * weaponManager.gunRange);
        }

        private bool AwayCheck(float minRange)
        {
            // Check if we need to manually burn away from an enemy that's too close or
            // if it would be better to drift away.

            Vector3 toTarget = FromTo(vessel, targetVessel);
            Vector3 toEscape = -toTarget.normalized;
            Vector3 relVel = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();

            float rotDistance = Vector3.Angle(vessel.ReferenceTransform.up, toEscape) * Mathf.Deg2Rad;
            float timeToRotate = BDAMath.SolveTime(rotDistance / 2, maxAngularAcceleration.magnitude) * 2;
            float timeToDisplace = BDAMath.SolveTime(minRange - toTarget.magnitude, maxAcceleration, Vector3.Dot(-relVel, toEscape));
            float timeToEscape = timeToRotate * 2 + timeToDisplace;

            Vector3 drift = AIUtils.PredictPosition(toTarget, relVel, Vector3.zero, timeToEscape);
            bool manualEscape = drift.sqrMagnitude < minRange * minRange;

            return manualEscape;
        }

        private void UpdateRCSVector(Vector3 inputVec = default(Vector3))
        {
            if (evadingGunfire) // Quickly move RCS vector
            {
                inputVec = Vector3.ProjectOnPlane(evasionNonLinearityDirection, threatRelativePosition);
                fc.rcsLerpRate = 15f;
                fc.rcsRotate = true;
            }
            else // Slowly lerp RCS vector
            {
                fc.rcsLerpRate = 5f;
                fc.rcsRotate = false;
            }

            fc.RCSVector = inputVec;
        }

        private Vector3 ToClosestApproach(Vector3 toTarget, Vector3 relVel, float minRange)
        {
            Vector3 relVelInverse = targetVessel.GetObtVelocity() - vessel.GetObtVelocity();
            float timeToIntercept = AIUtils.TimeToCPA(toTarget, relVelInverse, Vector3.zero, 9999);

            // Minimising the target closest approach to the current closest approach prevents
            // ships that are targeting each other from fighting over the closest approach based on their min ranges.
            // todo: allow for trajectory fighting if fuel is high.
            Vector3 actualClosestApproach = toTarget + Displacement(relVelInverse, Vector3.zero, timeToIntercept);
            float actualClosestApproachDistance = actualClosestApproach.magnitude;

            // Get a position that is laterally offset from the target by our desired closest approach distance.
            Vector3 rotatedVector = Vector3.ProjectOnPlane(relVel, toTarget.normalized).normalized;

            // Lead if the target is accelerating away from us.
            if (Vector3.Dot(targetVessel.acceleration.normalized, toTarget.normalized) > 0)
                toTarget += Displacement(Vector3.zero, toTarget.normalized * Vector3.Dot(targetVessel.acceleration, toTarget.normalized), Mathf.Min(timeToIntercept, 999));

            Vector3 toClosestApproach = toTarget + (rotatedVector * Mathf.Clamp(actualClosestApproachDistance, minRange, weaponManager ? weaponManager.gunRange * 0.5f : actualClosestApproachDistance));

            // Need a maximum angle so that we don't end up going further away at close range.
            toClosestApproach = Vector3.RotateTowards(toTarget, toClosestApproach, 22.5f * Mathf.Deg2Rad, float.MaxValue);

            return toClosestApproach;
        }

        #endregion

        #region Utils
        public static Vector3 FromTo(Vessel v1, Vessel v2)
        {
            return v2.transform.position - v1.transform.position;
        }

        public static Vector3 RelVel(Vessel v1, Vessel v2)
        {
            return v1.GetObtVelocity() - v2.GetObtVelocity();
        }

        public static Vector3 AngularAcceleration(Vector3 torque, Vector3 MoI)
        {
            return new Vector3(MoI.x.Equals(0) ? float.MaxValue : torque.x / MoI.x,
                MoI.y.Equals(0) ? float.MaxValue : torque.y / MoI.y,
                MoI.z.Equals(0) ? float.MaxValue : torque.z / MoI.z);
        }

        public static float AngularVelocity(Vessel v, Vessel t)
        {
            Vector3 tv1 = FromTo(v, t);
            Vector3 tv2 = tv1 + RelVel(v, t);
            return Vector3.Angle(tv1.normalized, tv2.normalized);
        }

        public static float VesselDistance(Vessel v1, Vessel v2)
        {
            return (v1.transform.position - v2.transform.position).magnitude;
        }

        public static Vector3 Displacement(Vector3 velocity, Vector3 acceleration, float time)
        {
            return velocity * time + 0.5f * acceleration * time * time;
        }

        private void CalculateAngularAcceleration()
        {
            maxAngularAcceleration = AngularAcceleration(availableTorque, vessel.MOI);
        }

        private void CalculateAvailableTorque(Vessel v)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (v != vessel) return;

            availableTorque = Vector3.zero;
            var reactionWheels = VesselModuleRegistry.GetModules<ModuleReactionWheel>(v);
            foreach (var wheel in reactionWheels)
            {
                wheel.GetPotentialTorque(out Vector3 pos, out pos);
                availableTorque += pos;
            }
        }

        public static float GetMaxAcceleration(Vessel v)
        {
            return GetMaxThrust(v) / v.GetTotalMass();
        }

        public static float GetMaxThrust(Vessel v)
        {
            float thrust = VesselModuleRegistry.GetModuleEngines(v).Where(e => e != null && e.EngineIgnited && e.isOperational).Sum(e => e.MaxThrustOutputVac(true));
            thrust += VesselModuleRegistry.GetModules<ModuleRCS>(v).Where(rcs => rcs != null && rcs.useThrottle).Sum(rcs => rcs.thrusterPower);
            return thrust;
        }
        #endregion

        #region Autopilot helper functions

        public override bool CanEngage()
        {
            return !vessel.LandedOrSplashed && vessel.InOrbit();
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
        {
            if (!vessel) return false;

            return true;
        }

        #endregion Autopilot helper functions

        #region WingCommander

        Vector3 GetFormationPosition()
        {
            return commandLeader.vessel.CoM + Quaternion.LookRotation(commandLeader.vessel.up, upDir) * this.GetLocalFormationPosition(commandFollowIndex);
        }

        #endregion WingCommander
    }
}