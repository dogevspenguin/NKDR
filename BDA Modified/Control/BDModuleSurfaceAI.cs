using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Weapons;
using BDArmory.Weapons.Missiles;
using BDArmory.GameModes;

namespace BDArmory.Control
{
    public class BDModuleSurfaceAI : BDGenericAIBase, IBDAIControl
    {
        #region Declarations

        Vessel extendingTarget = null;
        Vessel bypassTarget = null;
        Vector3 bypassTargetPos;

        Vector3 targetDirection;
        float targetVelocity; // the velocity the ship should target, not the velocity of its target
        bool aimingMode = false;

        int collisionDetectionTicker = 0;
        Vector3? dodgeVector;
        float weaveAdjustment = 0;
        float weaveDirection = 1;
        const float weaveLimit = 2.3f; // Scale factor for the limit of the WeaveFactor (original was 6.5 factor and 15 limit).

        Vector3 upDir;

        AIUtils.TraversabilityMatrix pathingMatrix;
        List<Vector3> pathingWaypoints = new List<Vector3>();
        bool leftPath = false;

        bool doExtend = false;
        bool doReverse = false;
        protected override Vector3d assignedPositionGeo
        {
            get { return intermediatePositionGeo; }
            set
            {
                finalPositionGeo = value;
                leftPath = true;
            }
        }

        Vector3d finalPositionGeo;
        Vector3d intermediatePositionGeo;
        public override Vector3d commandGPS => finalPositionGeo;

        private BDLandSpeedControl motorControl;

        //settings
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_VehicleType"),//Vehicle type
            UI_ChooseOption(options = new string[5] { "Stationary", "Land", "Water", "Amphibious", "Submarine" })]
        public string SurfaceTypeName = "Land";

        bool isHovercraft = false;

        public AIUtils.VehicleMovementType SurfaceType
            => (AIUtils.VehicleMovementType)Enum.Parse(typeof(AIUtils.VehicleMovementType), SurfaceTypeName);

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxSlopeAngle"),//Max slope angle
            UI_FloatRange(minValue = 1f, maxValue = 30f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxSlopeAngle = 10f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CombatAltitude"), //Combat Alt.
            UI_FloatRange(minValue = -200, maxValue = -15, stepIncrement = 5, scene = UI_Scene.All)]
        public float CombatAltitude = -75;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CruiseSpeed"),//Cruise speed
            UI_FloatRange(minValue = 5f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float CruiseSpeed = 20;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxSpeed"),//Max speed
            UI_FloatRange(minValue = 5f, maxValue = 80f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxSpeed = 30;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxDrift"),//Max drift
            UI_FloatRange(minValue = 1f, maxValue = 180f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxDrift = 10;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_TargetPitch"),//Moving pitch
            UI_FloatRange(minValue = -10f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float TargetPitch = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BankAngle"),//Bank angle
            UI_FloatRange(minValue = -45f, maxValue = 45f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float BankAngle = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_WeaveFactor"),//Weave Factor
            UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float WeaveFactor = 6.5f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerFactor"),//Steer Factor
            UI_FloatRange(minValue = 0.2f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerMult = 6;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerDamping"),//Steer Damping
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerDamping = 3;

        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steering"),
        //	UI_Toggle(enabledText = "Powered", disabledText = "Passive")]
        public bool PoweredSteering = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BroadsideAttack"),//Attack vector
            UI_Toggle(enabledText = "#LOC_BDArmory_BroadsideAttack_enabledText", disabledText = "#LOC_BDArmory_BroadsideAttack_disabledText")]//Broadside--Bow
        public bool BroadsideAttack = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEngagementRange"),//Min engagement range
            UI_FloatRange(minValue = 0f, maxValue = 6000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxEngagementRange"),//Max engagement range
            UI_FloatRange(minValue = 500f, maxValue = 8000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MaxEngagementRange = 4000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaintainEngagementRange"),//Maintain min Range
    UI_Toggle(enabledText = "#LOC_BDArmory_true", disabledText = "#LOC_BDArmory_false")]//true; false
        public bool maintainMinRange = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ManeuverRCS"),//RCS active
            UI_Toggle(enabledText = "#LOC_BDArmory_ManeuverRCS_enabledText", disabledText = "#LOC_BDArmory_ManeuverRCS_disabledText", scene = UI_Scene.All),]//Maneuvers--Combat
        public bool ManeuverRCS = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinObstacleMass", advancedTweakable = true),//Min obstacle mass
            UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All),]
        public float AvoidMass = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_PreferredBroadsideDirection", advancedTweakable = true),//Preferred broadside direction
            UI_ChooseOption(options = new string[3] { "Port", "Either", "Starboard" }, scene = UI_Scene.All),]
        public string OrbitDirectionName = "Either";
        public readonly string[] orbitDirections = new string[3] { "Port", "Either", "Starboard" };

        [KSPField(isPersistant = true)]
        int sideSlipDirection = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_GoesUp", advancedTweakable = true),//Goes up to 
            UI_Toggle(enabledText = "#LOC_BDArmory_GoesUp_enabledText", disabledText = "#LOC_BDArmory_GoesUp_disabledText", scene = UI_Scene.All),]//eleven--ten
        public bool UpToEleven = false;
        bool toEleven = false;

        const float AttackAngleAtMaxRange = 30f;

        Dictionary<string, float> altMaxValues = new Dictionary<string, float>
        {
            { nameof(MaxSlopeAngle), 90f },
            { nameof(CruiseSpeed), 300f },
            { nameof(MaxSpeed), 400f },
            { nameof(steerMult), 200f },
            { nameof(steerDamping), 100f },
            { nameof(MinEngagementRange), 20000f },
            { nameof(MaxEngagementRange), 30000f },
            { nameof(AvoidMass), 1000000f },
        };

        #endregion Declarations

        #region RMB info in editor

        // <color={XKCDColors.HexFormat.Lime}>Yes</color>
        public override string GetInfo()
        {
            // known bug - the game caches the RMB info, changing the variable after checking the info
            // does not update the info. :( No idea how to force an update.
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Available settings</b>:");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Vehicle type</color> - can this vessel operate on land/sea/both");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max slope angle</color> - what is the steepest slope this vessel can negotiate");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Cruise speed</color> - the default speed at which it is safe to maneuver");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max speed</color> - the maximum combat speed");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max drift</color> - maximum allowed angle between facing and velocity vector");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Moving pitch</color> - the pitch level to maintain when moving at cruise speed");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Bank angle</color> - the limit on roll when turning, positive rolls into turns");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Factor</color> - higher will make the AI apply more control input for the same desired rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Damping</color> - higher will make the AI apply more control input when it wants to stop rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Attack vector</color> - does the vessel attack from the front or the sides");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min engagement range</color> - AI will try to move away from oponents if closer than this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max engagement range</color> - AI will prioritize getting closer over attacking when beyond this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- RCS active</color> - Use RCS during any maneuvers, or only in combat ");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min obstacle mass</color> - Obstacles of a lower mass than this will be ignored instead of avoided");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Goes up to</color> - Increases variable limits, no direct effect on behaviour");
            }

            return sb.ToString();
        }

        #endregion RMB info in editor

        #region events

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SetChooseOptions();
            ChooseOptionsUpdated(null, null);
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            pathingMatrix = new AIUtils.TraversabilityMatrix();

            if (!motorControl)
            {
                motorControl = gameObject.AddComponent<BDLandSpeedControl>();
                motorControl.vessel = vessel;
            }
            motorControl.Activate();

            if (BroadsideAttack && sideSlipDirection == 0)
            {
                SetBroadsideDirection(OrbitDirectionName);
            }

            leftPath = true;
            extendingTarget = null;
            bypassTarget = null;
            collisionDetectionTicker = 6;
            if (VesselModuleRegistry.GetModules<ModuleSpaceFriction>(vessel).Count > 0) isHovercraft = true;
        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (motorControl)
                motorControl.Deactivate();
        }

        public void SetChooseOptions()
        {
            UI_ChooseOption broadisdeEditor = (UI_ChooseOption)Fields["OrbitDirectionName"].uiControlEditor;
            UI_ChooseOption broadisdeFlight = (UI_ChooseOption)Fields["OrbitDirectionName"].uiControlFlight;
            UI_ChooseOption SurfaceEditor = (UI_ChooseOption)Fields["SurfaceTypeName"].uiControlEditor;
            UI_ChooseOption SurfaceFlight = (UI_ChooseOption)Fields["SurfaceTypeName"].uiControlFlight;
            broadisdeEditor.onFieldChanged = ChooseOptionsUpdated;
            broadisdeFlight.onFieldChanged = ChooseOptionsUpdated;
            SurfaceEditor.onFieldChanged = ChooseOptionsUpdated;
            SurfaceFlight.onFieldChanged = ChooseOptionsUpdated;
        }

        public void ChooseOptionsUpdated(BaseField field, object obj)
        {
            // Hide/display the AI fields
            var fieldEnabled = SurfaceType != AIUtils.VehicleMovementType.Stationary;
            foreach (var fieldName in new List<string>{
                    "MaxSlopeAngle",
                    "CruiseSpeed",
                    "MaxSpeed",
                    "MaxDrift",
                    "TargetPitch",
                    "BankAngle",
                    // "steerMult",
                    // "steerDamping",
                    "BroadsideAttack",
                    // "MinEngagementRange",
                    // "MaxEngagementRange",
                    // "ManeuverRCS",
                    "AvoidMass",
                    "OrbitDirectionName"
                })
            {
                Fields[fieldName].guiActive = fieldEnabled;
                Fields[fieldName].guiActiveEditor = fieldEnabled;
            }
            Fields["CombatAltitude"].guiActive = (SurfaceType == AIUtils.VehicleMovementType.Submarine);
            Fields["CombatAltitude"].guiActiveEditor = (SurfaceType == AIUtils.VehicleMovementType.Submarine);
            Fields["maintainMinRange"].guiActive = (SurfaceType == AIUtils.VehicleMovementType.Land);
            Fields["maintainMinRange"].guiActiveEditor = (SurfaceType == AIUtils.VehicleMovementType.Land);
            this.part.RefreshAssociatedWindows();
            if (BDArmoryAIGUI.Instance != null)
            {
                BDArmoryAIGUI.Instance.SetChooseOptionSliders();
            }
        }

        public void SetBroadsideDirection(string direction)
        {
            if (!orbitDirections.Contains(direction)) return;
            OrbitDirectionName = direction;
            sideSlipDirection = orbitDirections.IndexOf(OrbitDirectionName) - 1;
            if (sideSlipDirection == 0)
                sideSlipDirection = UnityEngine.Random.value > 0.5f ? 1 : -1;
        }

        void Update()
        {
            // switch up the alt values if up to eleven is toggled
            if (UpToEleven != toEleven)
            {
                using (var s = altMaxValues.Keys.ToList().GetEnumerator())
                    while (s.MoveNext())
                    {
                        UI_FloatRange euic = (UI_FloatRange)
                            (HighLogic.LoadedSceneIsFlight ? Fields[s.Current].uiControlFlight : Fields[s.Current].uiControlEditor);
                        float tempValue = euic.maxValue;
                        euic.maxValue = altMaxValues[s.Current];
                        altMaxValues[s.Current] = tempValue;
                        // change the value back to what it is now after fixed update, because changing the max value will clamp it down
                        // using reflection here, don't look at me like that, this does not run often
                        StartCoroutine(setVar(s.Current, (float)typeof(BDModuleSurfaceAI).GetField(s.Current).GetValue(this)));
                    }
                toEleven = UpToEleven;
            }
        }

        IEnumerator setVar(string name, float value)
        {
            yield return new WaitForFixedUpdate();
            typeof(BDModuleSurfaceAI).GetField(name).SetValue(this, value);
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DEBUG_LINES) return;
            if (command == PilotCommands.Follow)
            {
                GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, assignedPositionWorld, 2, Color.red);
            }

            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 2, Color.blue);
            //GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right), 2, Color.green);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vessel.srf_vel_direction.ProjectOnPlanePreNormalized(upDir) * 10f, 2, Color.green);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 10f, 5, Color.red);
            if (SurfaceType != AIUtils.VehicleMovementType.Stationary)
                pathingMatrix.DrawDebug(vessel.CoM, pathingWaypoints);
        }

        #endregion events

        #region Actual AI Pilot

        protected override void AutoPilot(FlightCtrlState s)
        {
            if (!vessel.Autopilot.Enabled)
                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            targetVelocity = 0;
            targetDirection = vesselTransform.up;
            aimingMode = false;
            upDir = vessel.up;
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine("");

            // check if we should be panicking
            if (SurfaceType == AIUtils.VehicleMovementType.Stationary || !PanicModes()) // Stationary vehicles don't panic (so, free-fall stationary turrets are a possibility).
            {
                // pilot logic figures out what we're supposed to be doing, and sets the base state
                PilotLogic();
                // situational awareness modifies the base as best as it can (evasive mainly)
                Tactical();
            }

            AttitudeControl(s); // move according to our targets
            AdjustThrottle(targetVelocity); // set throttle according to our targets and movement
        }

        void PilotLogic()
        {
            doReverse = false;
            if (SurfaceType != AIUtils.VehicleMovementType.Stationary)
            {
                // check for collisions, but not every frame
                if (collisionDetectionTicker == 0)
                {
                    collisionDetectionTicker = 20;
                    float predictMult = Mathf.Clamp(10 / MaxDrift, 1, 10);

                    dodgeVector = null;

                    using (var vs = BDATargetManager.LoadedVessels.GetEnumerator())
                        while (vs.MoveNext())
                        {
                            if (vs.Current == null || vs.Current == vessel || vs.Current.GetTotalMass() < AvoidMass) continue;
                            if (!VesselModuleRegistry.ignoredVesselTypes.Contains(vs.Current.vesselType))
                            {
                                var ibdaiControl = VesselModuleRegistry.GetModule<IBDAIControl>(vs.Current);
                                if (!vs.Current.LandedOrSplashed || (ibdaiControl != null && ibdaiControl.commandLeader != null && ibdaiControl.commandLeader.vessel == vessel))
                                    continue;
                            }
                            dodgeVector = PredictCollisionWithVessel(vs.Current, 5f * predictMult, 0.5f);
                            if (dodgeVector != null) break;
                        }
                }
                else
                    collisionDetectionTicker--;

                // avoid collisions if any are found
                if (dodgeVector != null)
                {
                    targetVelocity = PoweredSteering ? MaxSpeed : CruiseSpeed;
                    targetDirection = (Vector3)dodgeVector;
                    SetStatus($"Avoiding Collision");
                    leftPath = true;
                    return;
                }
            }
            else { collisionDetectionTicker = 0; }

            // if bypass target is no longer relevant, remove it
            if (bypassTarget != null && ((bypassTarget != targetVessel && bypassTarget != (commandLeader != null ? commandLeader.vessel : null))
                || (VectorUtils.GetWorldSurfacePostion(bypassTargetPos, vessel.mainBody) - bypassTarget.CoM).sqrMagnitude > 500000))
            {
                bypassTarget = null;
            }

            if (bypassTarget == null)
            {
                // check for enemy targets and engage
                // not checking for guard mode, because if guard mode is off now you can select a target manually and if it is of opposing team, the AI will try to engage while you can man the turrets
                if (weaponManager && targetVessel != null && !BDArmorySettings.PEACE_MODE)
                {
                    leftPath = true;
                    if (collisionDetectionTicker == 5)
                        checkBypass(targetVessel);

                    Vector3 vecToTarget = targetVessel.CoM - vessel.CoM;
                    float distance = vecToTarget.magnitude;
                    // lead the target a bit, where 1km/s is a ballpark estimate of the average bullet velocity
                    float shotSpeed = 1000f;
                    if ((weaponManager != null ? weaponManager.selectedWeapon : null) is ModuleWeapon wep)
                        shotSpeed = wep.bulletVelocity;
                    var timeToCPA = targetVessel.TimeToCPA(vessel.CoM, vessel.Velocity() + vesselTransform.up * shotSpeed, FlightGlobals.getGeeForceAtPosition(vessel.CoM), MaxEngagementRange / shotSpeed);
                    vecToTarget = targetVessel.PredictPosition(timeToCPA) - vessel.CoM;

                    if (SurfaceType == AIUtils.VehicleMovementType.Stationary)
                    {
                        if (distance >= MinEngagementRange && distance <= MaxEngagementRange)
                        {
                            targetDirection = vecToTarget;
                            aimingMode = true;
                        }
                        else
                        {
                            SetStatus("On Alert");
                            return;
                        }
                    }
                    else if (BroadsideAttack)
                    {
                        Vector3 sideVector = Vector3.Cross(vecToTarget, upDir); //find a vector perpendicular to direction to target
                        if (collisionDetectionTicker == 10
                                && !pathingMatrix.TraversableStraightLine(
                                        VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                        VectorUtils.WorldPositionToGeoCoords(vessel.PredictPosition(10), vessel.mainBody),
                                        vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass))
                            sideSlipDirection = -Math.Sign(Vector3.Dot(vesselTransform.up, sideVector)); // switch sides if we're running ashore
                        sideVector *= sideSlipDirection;

                        float sidestep = distance >= MaxEngagementRange ? Mathf.Clamp01((MaxEngagementRange - distance) / (CruiseSpeed * Mathf.Clamp(90 / MaxDrift, 0, 10)) + 1) * AttackAngleAtMaxRange / 90 : // direct to target to attackAngle degrees if over maxrange
                            (distance <= MinEngagementRange ? 1.5f - distance / (MinEngagementRange * 2) : // 90 to 135 degrees if closer than minrange
                            (MaxEngagementRange - distance) / (MaxEngagementRange - MinEngagementRange) * (1 - AttackAngleAtMaxRange / 90) + AttackAngleAtMaxRange / 90); // attackAngle to 90 degrees from maxrange to minrange
                        targetDirection = Vector3.LerpUnclamped(vecToTarget.normalized, sideVector.normalized, sidestep); // interpolate between the side vector and target direction vector based on sidestep
                        targetVelocity = MaxSpeed;
                        if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"Broadside attack angle {sidestep}");
                    }
                    else // just point at target and go
                    {
                        if (!maintainMinRange && (((targetVessel.horizontalSrfSpeed < 10) || Vector3.Dot(targetVessel.vesselTransform.up, vessel.vesselTransform.up) < 0) //if target is stationary or we're facing in opposite directions
                            && (distance < MinEngagementRange || (distance < (MinEngagementRange * 3 + MaxEngagementRange) / 4 //and too close together
                            && extendingTarget != null && targetVessel != null && extendingTarget == targetVessel))))
                        {
                            extendingTarget = targetVessel;
                            // not sure if this part is very smart, potential for improvement
                            targetDirection = -vecToTarget; //extend
                            targetVelocity = MaxSpeed;
                            SetStatus($"Extending");
                            return;
                        }
                        else
                        {
                            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"velAngle: {Vector3.Angle(vessel.srf_vel_direction.ProjectOnPlanePreNormalized(vessel.up), vesselTransform.up)}");
                            extendingTarget = null;
                            targetDirection = vecToTarget.ProjectOnPlanePreNormalized(upDir);
                            if (weaponManager != null && weaponManager.selectedWeapon != null)
                            {
                                switch (weaponManager.selectedWeapon.GetWeaponClass())
                                {
                                    case WeaponClasses.Gun:
                                    case WeaponClasses.Rocket:
                                    case WeaponClasses.DefenseLaser:
                                        var gun = (ModuleWeapon)weaponManager.selectedWeapon;
                                        if (gun != null && (gun.yawRange == 0 || gun.maxPitch == gun.minPitch) && gun.FiringSolutionVector != null)
                                        {
                                            aimingMode = true;
                                            if (Vector3.Angle((Vector3)gun.FiringSolutionVector, vessel.transform.up) < 20)
                                                targetDirection = (Vector3)gun.FiringSolutionVector;
                                        }
                                        break;
                                }
                            }
                            if (distance >= MaxEngagementRange || distance <= MinEngagementRange * 1.25f)
                            {
                                if (distance >= MaxEngagementRange)
                                    targetVelocity = MaxSpeed;//out of engagement range, engines ahead full
                                if (distance <= MinEngagementRange * 1.25f) //coming within minEngagement range
                                {
                                    if (maintainMinRange) //for some reason ignored if both vessel and targetvessel using Mk2roverCans?
                                    {
                                        if (targetVessel.srfSpeed < 10)
                                        {
                                            targetVelocity = 0;
                                            SetStatus($"Braking");
                                        }
                                        if (distance <= MinEngagementRange) //rolled to a stop inside minRange/target has encroached
                                        {
                                            //if (Vector3.Dot(vessel.vesselTransform.up, vessel.srf_vel_direction.ProjectOnPlanePreNormalized(upDir)) > 0) //we're still moving forward
                                            //brakes = true;
                                            //else brakes = false;//come to a stop and reversing, stop braking
                                            doReverse = true;
                                            targetVelocity = -MaxSpeed;
                                            SetStatus($"Reversing");
                                            return;
                                        }
                                        return;
                                    }
                                    else
                                        targetVelocity = MaxSpeed;
                                }
                            }
                            else //within engagement envelope
                            {
                                targetVelocity = !maintainMinRange ? MaxSpeed : CruiseSpeed / 10 + (MaxSpeed - CruiseSpeed / 10) * (distance - MinEngagementRange) / (MaxEngagementRange - MinEngagementRange); //slow down if inside engagement range to extend shooting opportunities
                            }
                            targetVelocity = Mathf.Clamp(targetVelocity, PoweredSteering ? CruiseSpeed / 5 : (doReverse ? -MaxSpeed : 0), MaxSpeed); // maintain a bit of speed if using powered steering
                        }
                    }
                    SetStatus($"Engaging target");
                    return;
                }

                // follow
                if (command == PilotCommands.Follow && SurfaceType != AIUtils.VehicleMovementType.Stationary)
                {
                    leftPath = true;
                    if (collisionDetectionTicker == 5)
                        checkBypass(commandLeader.vessel);

                    Vector3 targetPosition = GetFormationPosition();
                    Vector3 targetDistance = targetPosition - vesselTransform.position;
                    if (Vector3.Dot(targetDistance, vesselTransform.up) < 0
                        && targetDistance.ProjectOnPlanePreNormalized(upDir).sqrMagnitude < 250f * 250f
                        && Vector3.Angle(vesselTransform.up, commandLeader.vessel.srf_velocity) < 0.8f)
                    {
                        targetDirection = Vector3.RotateTowards(commandLeader.vessel.srf_vel_direction.ProjectOnPlanePreNormalized(upDir), targetDistance, 0.2f, 0);
                    }
                    else
                    {
                        targetDirection = targetDistance.ProjectOnPlanePreNormalized(upDir);
                    }
                    targetVelocity = (float)(commandLeader.vessel.horizontalSrfSpeed + (vesselTransform.position - targetPosition).magnitude / 15);
                    if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
                    SetStatus($"Following");
                    return;
                }
            }

            if (SurfaceType != AIUtils.VehicleMovementType.Stationary)
            {
                // goto
                if (leftPath && bypassTarget == null)
                {
                    Pathfind(finalPositionGeo);
                    leftPath = false;
                }

                const float targetRadius = 250f;
                targetDirection = (assignedPositionWorld - vesselTransform.position).ProjectOnPlanePreNormalized(upDir);

                if (targetDirection.sqrMagnitude > targetRadius * targetRadius)
                {
                    if (bypassTarget != null)
                        targetVelocity = MaxSpeed;
                    else if (pathingWaypoints.Count > 1)
                        targetVelocity = command == PilotCommands.Attack ? MaxSpeed : CruiseSpeed;
                    else
                        targetVelocity = Mathf.Clamp((targetDirection.magnitude - targetRadius / 2) / 5f,
                        0, command == PilotCommands.Attack ? MaxSpeed : CruiseSpeed);

                    if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
                    SetStatus(bypassTarget ? "Repositioning" : "Moving");
                    return;
                }

                cycleWaypoint();
            }

            SetStatus($"Not doing anything in particular");
            targetDirection = vesselTransform.up;
        }

        void Tactical()
        {
            // enable RCS if we're in combat
            vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, weaponManager && targetVessel && !BDArmorySettings.PEACE_MODE
                && (weaponManager.selectedWeapon != null || (vessel.CoM - targetVessel.CoM).sqrMagnitude < MaxEngagementRange * MaxEngagementRange)
                || weaponManager.underFire || weaponManager.missileIsIncoming);

            // if weaponManager thinks we're under fire, do the evasive dance
            if (SurfaceType != AIUtils.VehicleMovementType.Stationary && (weaponManager.underFire || weaponManager.missileIsIncoming))
            {
                if (weaponManager.isDecoying) //incoming passive sonar torpedo, reduce craft noise
                    targetVelocity = CruiseSpeed / 2;
                else
                    if (!maintainMinRange) targetVelocity = MaxSpeed;
                if (weaponManager.underFire || weaponManager.incomingMissileDistance < 2500)
                {
                    if (Mathf.Abs(weaveAdjustment) + Time.deltaTime * WeaveFactor > weaveLimit * WeaveFactor) weaveDirection *= -1;
                    weaveAdjustment += WeaveFactor * weaveDirection * Time.deltaTime;
                }
                else
                {
                    weaveAdjustment = 0;
                }
            }
            else
            {
                weaveAdjustment = 0;
            }
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"underFire {weaponManager.underFire}, weaveAdjustment {weaveAdjustment}");
        }

        bool PanicModes()
        {
            if (!vessel.LandedOrSplashed && (!isHovercraft || isHovercraft && vessel.radarAltitude > MaxSlopeAngle * 3)) //FIXME - unlink hoverAlt from maxSlope, else low hover alt may prevent navigating steeper terrain
            {
                targetVelocity = 0;
                targetDirection = vessel.srf_velocity.ProjectOnPlanePreNormalized(upDir);
                SetStatus("Airtime!");
                return true;
            }
            else if (vessel.Landed
                && !vessel.Splashed // I'm looking at you, Kerbal Konstructs. (When launching directly into water, KK seems to set both vessel.Landed and vessel.Splashed to true.)
                && (SurfaceType & AIUtils.VehicleMovementType.Land) == 0)
            {
                targetVelocity = 0;
                SetStatus("Stranded");
                return true;
            }
            else if (vessel.Splashed && (SurfaceType & AIUtils.VehicleMovementType.Water) == 0)
            {
                targetVelocity = 0;
                SetStatus("Floating");
                return true;
            }
            else if (vessel.IsUnderwater() && (SurfaceType & AIUtils.VehicleMovementType.Submarine) == 0)
            {
                targetVelocity = 0;
                SetStatus("Sunk");
                return true;
            }
            return false;
        }

        void AdjustThrottle(float targetSpeed)
        {
            targetVelocity = Mathf.Clamp(targetVelocity, doReverse ? -MaxSpeed : 0, MaxSpeed);
            targetSpeed = Mathf.Clamp(targetSpeed, doReverse ? -MaxSpeed : 0, MaxSpeed);
            float velocitySignedSrfSpeed = Vector3.Angle(vessel.srf_vel_direction.ProjectOnPlanePreNormalized(upDir), vesselTransform.up) < 110 ? (float)vessel.srfSpeed : -(float)vessel.srfSpeed;

            if (float.IsNaN(targetSpeed)) //because yeah, I might have left division by zero in there somewhere
            {
                targetSpeed = CruiseSpeed;
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine("Target velocity NaN, set to CruiseSpeed.");
            }
            else
            {
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"Target velocity: {targetSpeed}; signed Velocity: {velocitySignedSrfSpeed}; brakeVel: {targetSpeed * velocitySignedSrfSpeed}; use brakes: {(targetSpeed * velocitySignedSrfSpeed < -5)}");
            }
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"engine thrust: {speedController.debugThrust}, motor zero: {motorControl.zeroPoint}");

            speedController.targetSpeed = motorControl.targetSpeed = targetSpeed;
            motorControl.signedSrfSpeed = velocitySignedSrfSpeed;
            speedController.useBrakes = motorControl.preventNegativeZeroPoint = speedController.debugThrust > 0;
        }

        Vector3 directionIntegral;
        float pitchIntegral = 0;

        void AttitudeControl(FlightCtrlState s)
        {
            const float terrainOffset = 5;

            Vector3 yawTarget = targetDirection.ProjectOnPlanePreNormalized(vesselTransform.forward);

            // limit "aoa" if we're moving
            float driftMult = 1;
            if (SurfaceType != AIUtils.VehicleMovementType.Stationary && vessel.horizontalSrfSpeed * 10 > CruiseSpeed)
            {
                driftMult = Mathf.Max(Vector3.Angle(vessel.srf_velocity, yawTarget) / MaxDrift, 1);
                yawTarget = Vector3.RotateTowards(vessel.srf_velocity, yawTarget, MaxDrift * Mathf.Deg2Rad, 0);
            }
            bool invertCtrlPoint = Vector3.Angle(vessel.srf_vel_direction.ProjectOnPlanePreNormalized(vessel.up), vesselTransform.up) > 90 && Math.Round(vessel.srfSpeed, 1) > 1; //need to flip vessel 'forward' when reversing for proper steerage
            float yawError = VectorUtils.SignedAngle(invertCtrlPoint ? -vesselTransform.up : vesselTransform.up, yawTarget, vesselTransform.right) + (aimingMode ? 0 : weaveAdjustment);
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI)
            {
                DebugLine($"yaw target: {yawTarget}, yaw error: {yawError}");
                DebugLine($"drift multiplier: {driftMult}");
            }

            float pitchError = 0;
            if (SurfaceType != AIUtils.VehicleMovementType.Stationary)
            {
                if (SurfaceType == AIUtils.VehicleMovementType.Submarine)
                {
                    float targetAlt = CombatAltitude;
                    if (weaponManager != null && weaponManager.currentTarget != null && weaponManager.selectedWeapon != null)
                    {
                        switch (weaponManager.selectedWeapon.GetWeaponClass())
                        {
                            case WeaponClasses.Missile:
                                {
                                    targetAlt = -10; //come to periscope depth for missile launch
                                    break;
                                }
                            case WeaponClasses.Gun:
                                {
                                    if (BDArmorySettings.BULLET_WATER_DRAG)
                                    {
                                        if (weaponManager.currentTarget.isSplashed || ((weaponManager.currentTarget.isFlying || weaponManager.currentTarget.Vessel.situation == Vessel.Situations.LANDED) && weaponManager.currentGun.turret))
                                        {
                                            if (vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, weaponManager.selectedWeapon.GetEngageRange()))
                                                targetAlt = -10; //come to periscope depth in preparation for surface attack when in range
                                            else
                                                targetAlt = 1;//in range, surface to engage with deck guns
                                        }
                                    }
                                    else
                                    {
                                        if (weaponManager.currentTarget.Vessel.situation == Vessel.Situations.LANDED && weaponManager.currentGun.turret) //surface for shooting land targets with turrets
                                        {
                                            if (vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, weaponManager.selectedWeapon.GetEngageRange()))
                                                targetAlt = -10; //come to periscope depth in preparation for surface attack when in range
                                            else
                                                targetAlt = 1;//in range, surface to engage with deck guns
                                        }
                                        if (!weaponManager.currentGun.turret && weaponManager.currentTarget.isSplashed)
                                        {
                                            if (!doExtend)
                                            {
                                                if (weaponManager.currentTarget.Vessel.altitude < CombatAltitude / 4 && vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, 200)) //200m
                                                {
                                                    targetAlt = (float)weaponManager.currentTarget.Vessel.altitude; //engaging enemy sub or ship, but break off when too close to target or surface
                                                }
                                                else
                                                    doExtend = true;
                                            }
                                            else
                                            {
                                                if (vessel.altitude < (CombatAltitude * .66f) || vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, 1000)) doExtend = false;
                                            }
                                        }
                                        //else remain at combat depth and engage with turrets.
                                    }
                                    break;
                                }
                            case WeaponClasses.Rocket:
                            case WeaponClasses.DefenseLaser:
                                {
                                    if (weaponManager.currentTarget.Vessel.situation == Vessel.Situations.LANDED || weaponManager.currentTarget.isFlying && weaponManager.currentGun.turret)
                                    {
                                        if (vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, weaponManager.selectedWeapon.GetEngageRange()))
                                            targetAlt = -10; //come to periscope depth in preparation for surface attack when in range
                                        else
                                            targetAlt = 1; //surface to engage with turrets
                                    }
                                    if (weaponManager.currentTarget.isSplashed)
                                    {
                                        if (!doExtend)
                                        {
                                            if (weaponManager.currentTarget.Vessel.altitude < CombatAltitude / 4 && vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, 200))
                                            {
                                                targetAlt = (float)weaponManager.currentTarget.Vessel.altitude; //engaging enemy sub or ship, but break off when too close
                                            }
                                            else
                                                doExtend = true;
                                        }
                                        else
                                        {
                                            if (vessel.altitude < (CombatAltitude * .66f) || vessel.CoM.FurtherFromThan(weaponManager.currentTarget.Vessel.CoM, 1000)) doExtend = false;
                                        }
                                    }
                                    break;
                                }
                            default: //SLW
                                break;
                        }
                        //if (weaponManager.missileIsIncoming && !weaponManager.incomingMissileVessel.LandedOrSplashed && targetAlt > -10) targetAlt = -10; //this might make subs too hard to kill?
                    }
                    //look into some sort of crash dive routine if under fire from enemies dropping depthcharges/air-dropped torps?
                    float pitchAngle = 0;
                    if ((float)vessel.altitude > targetAlt) pitchAngle = -MaxSlopeAngle * (1 - ((float)vessel.altitude / targetAlt)); //may result in not reaching target depth, depending on how neutrally buoyant the sub is. Clamp to maxSlopeAngle if Dist(vessel.altitude, targetAlt) > combatAlt * 0.25 or similar?
                    else pitchAngle = MaxSlopeAngle * (1 - (targetAlt / (float)vessel.altitude));
                    float pitch = 90 - Vector3.Angle(vesselTransform.up, upDir);

                    pitchError = pitchAngle - pitch;
                    if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"Target Alt: {targetAlt.ToString("F3")}: PitchAngle: {pitchAngle.ToString("F3")}, Pitch: {pitch.ToString("F3")}, PitchError: {pitchError.ToString("F3")}");

                    directionIntegral = (directionIntegral + (pitchError * -vesselTransform.forward + yawError * vesselTransform.right) * Time.deltaTime).ProjectOnPlanePreNormalized(vesselTransform.up);
                    if (directionIntegral.sqrMagnitude > 1f) directionIntegral = directionIntegral.normalized;
                    pitchIntegral = 0.4f * Vector3.Dot(directionIntegral, -vesselTransform.forward);
                }
                else
                {
                    Vector3 baseForward = vessel.transform.up * terrainOffset;
                    float basePitch = Mathf.Atan2(
                        AIUtils.GetTerrainAltitude(vessel.CoM + baseForward, vessel.mainBody, false)
                        - AIUtils.GetTerrainAltitude(vessel.CoM - baseForward, vessel.mainBody, false),
                        terrainOffset * 2) * Mathf.Rad2Deg;
                    float pitchAngle = basePitch + TargetPitch * Mathf.Clamp01((float)vessel.horizontalSrfSpeed / CruiseSpeed);
                    if (aimingMode)
                        pitchAngle = VectorUtils.SignedAngle(vesselTransform.up, targetDirection.ProjectOnPlanePreNormalized(vesselTransform.right), -vesselTransform.forward);
                    if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"terrain fw slope: {basePitch}, target pitch: {pitchAngle}");
                    float pitch = 90 - Vector3.Angle(vesselTransform.up, upDir);
                    pitchError = pitchAngle - pitch;
                }
            }
            else
            {
                pitchError = VectorUtils.SignedAngle(vesselTransform.up, targetDirection.ProjectOnPlanePreNormalized(vesselTransform.right), -vesselTransform.forward);
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"pitch error: {pitchError}");
            }
            float rollError = 0;
            if (SurfaceType != AIUtils.VehicleMovementType.Stationary)
            {
                Vector3 baseLateral = vessel.transform.right * terrainOffset;
                float baseRoll = Mathf.Atan2(
                    AIUtils.GetTerrainAltitude(vessel.CoM + baseLateral, vessel.mainBody, false)
                    - AIUtils.GetTerrainAltitude(vessel.CoM - baseLateral, vessel.mainBody, false),
                    terrainOffset * 2) * Mathf.Rad2Deg;
                float drift = VectorUtils.SignedAngle(vesselTransform.up, vessel.GetSrfVelocity().ProjectOnPlanePreNormalized(upDir), vesselTransform.right);
                float bank = VectorUtils.SignedAngle(-vesselTransform.forward, upDir, -vesselTransform.right);
                float targetRoll = baseRoll + BankAngle * Mathf.Clamp01(drift / MaxDrift) * Mathf.Clamp01((float)vessel.srfSpeed / CruiseSpeed);
                rollError = targetRoll - bank;
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"terrain sideways slope: {baseRoll}, target roll: {targetRoll}");
            }
            else
            {
                rollError = VectorUtils.SignedAngle(-vesselTransform.forward, upDir, vesselTransform.right);
            }

            Vector3 localAngVel = vessel.angularVelocity;
            SetFlightControlState(s,
                Mathf.Clamp(((aimingMode ? 0.02f : 0.015f) * steerMult * pitchError) + pitchIntegral - (steerDamping * -localAngVel.x), -2, 2), // pitch
                Mathf.Clamp((((aimingMode ? 0.007f : 0.005f) * steerMult * yawError) - (steerDamping * 0.2f * -localAngVel.z)) * driftMult, -2, 2), // yaw
                steerMult * 0.006f * rollError - 0.4f * steerDamping * -localAngVel.y, // roll
                -Mathf.Clamp((((aimingMode ? 0.005f : 0.003f) * steerMult * yawError) - (steerDamping * 0.1f * -localAngVel.z)), -2, 2) // wheel steer
            );

            if (ManeuverRCS && (Mathf.Abs(s.roll) >= 1 || Mathf.Abs(s.pitch) >= 1 || Mathf.Abs(s.yaw) >= 1))
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        protected void SetFlightControlState(FlightCtrlState s, float pitch, float yaw, float roll, float wheelSteer)
        {
            base.SetFlightControlState(s, pitch, yaw, roll);
            s.wheelSteer = wheelSteer;
            if (hasAxisGroupsModule)
            {
                axisGroupsModule.UpdateAxisGroup(KSPAxisGroup.WheelSteer, wheelSteer);
            }

        }
        #endregion Actual AI Pilot

        #region Autopilot helper functions

        public override bool CanEngage()
        {
            if (SurfaceType == AIUtils.VehicleMovementType.Stationary) // Stationary can shoot at whatever it can see without moving.
            {
                return true;
            }
            else if (vessel.Splashed && ((SurfaceType & AIUtils.VehicleMovementType.Water) == 0 || (SurfaceType & AIUtils.VehicleMovementType.Submarine) == 0))
            {
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine(vessel.vesselName + " cannot engage: land vehicle in water");
            }
            else if (vessel.Landed && (SurfaceType & AIUtils.VehicleMovementType.Land) == 0)
            {
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine(vessel.vesselName + " cannot engage: water vehicle on land");
            }
            else if (!vessel.LandedOrSplashed && !isHovercraft)
            {
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine(vessel.vesselName + " cannot engage: vessel not on surface");
            }
            // the motorControl part fails sometimes, and guard mode then decides not to select a weapon
            // figure out what is wrong with motor control before uncommenting :D
            // else if (speedController.debugThrust + (motorControl?.MaxAccel ?? 0) <= 0)
            // {
            //     if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine(vessel.vesselName + " cannot engage: no engine power");
            // }
            else
                return true;
            return false;
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
            => !BroadsideAttack &&
            (((target != null && target.Splashed) && (SurfaceType & AIUtils.VehicleMovementType.Water) != 0) //boat targeting boat
            || ((target != null && target.Landed) && (SurfaceType & AIUtils.VehicleMovementType.Land) != 0) //vee targeting vee
            || (((target != null && !target.LandedOrSplashed) && (SurfaceType & AIUtils.VehicleMovementType.Amphibious) != 0) && isHovercraft)) //repulsorcraft targeting repulsorcraft
            ; //valid if can traverse the same medium and using bow fire

        /// <returns>null if no collision, dodge vector if one detected</returns>
        Vector3? PredictCollisionWithVessel(Vessel v, float maxTime, float interval)
        {
            //evasive will handle avoiding missiles
            if (v == weaponManager.incomingMissileVessel
                || v.rootPart.FindModuleImplementing<MissileBase>() != null)
                return null;

            float time = Mathf.Min(0.5f, maxTime);
            while (time < maxTime)
            {
                Vector3 tPos = v.PredictPosition(time);
                Vector3 myPos = vessel.PredictPosition(time);
                if (Vector3.SqrMagnitude(tPos - myPos) < 2500f)
                {
                    return Vector3.Dot(tPos - myPos, vesselTransform.right) > 0 ? -vesselTransform.right : vesselTransform.right;
                }

                time = Mathf.MoveTowards(time, maxTime, interval);
            }

            return null;
        }

        void checkBypass(Vessel target)
        {
            if (!pathingMatrix.TraversableStraightLine(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass))
            {
                bypassTarget = target;
                bypassTargetPos = VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody);
                pathingWaypoints = pathingMatrix.Pathfind(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass);
                if (VectorUtils.GeoDistance(pathingWaypoints[pathingWaypoints.Count - 1], bypassTargetPos, vessel.mainBody) < 200)
                    pathingWaypoints.RemoveAt(pathingWaypoints.Count - 1);
                if (pathingWaypoints.Count > 0)
                    intermediatePositionGeo = pathingWaypoints[0];
                else
                    bypassTarget = null;
            }
        }

        private void Pathfind(Vector3 destination)
        {
            pathingWaypoints = pathingMatrix.Pathfind(
                                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                    destination, vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass);
            intermediatePositionGeo = pathingWaypoints[0];
        }

        void cycleWaypoint()
        {
            if (pathingWaypoints.Count > 1)
            {
                pathingWaypoints.RemoveAt(0);
                intermediatePositionGeo = pathingWaypoints[0];
            }
            else if (bypassTarget != null)
            {
                pathingWaypoints.Clear();
                bypassTarget = null;
                leftPath = true;
            }
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
