using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Guidances;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Weapons;
using BDArmory.Weapons.Missiles;

namespace BDArmory.Control
{
    public class BDModuleVTOLAI : BDGenericAIBase, IBDAIControl
    {
        #region Declarations

        Vessel extendingTarget = null;
        Vessel bypassTarget = null;
        Vector3 bypassTargetPos;

        Vector3 targetDirection;
        float targetVelocity; // the forward/reverse velocity the craft should target, not the velocity of its target
        float targetLatVelocity; // the left/right velocity the craft should target, not the velocity of its target
        float targetAltitude; // the altitude the craft should hold, not the altitude of its target
        Vector3 rollTarget;
        bool aimingMode = false;

        int collisionDetectionTicker = 0;
        Vector3? dodgeVector;
        float weaveAdjustment = 0;
        float weaveDirection = 1;
        const float weaveLimit = 2.3f;

        Vector3 upDir;

        AIUtils.TraversabilityMatrix pathingMatrix;
        List<Vector3> pathingWaypoints = new List<Vector3>();
        bool leftPath = false;

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

        private BDVTOLSpeedControl altitudeControl; // Throttle is used to control altitude in most quadcopter control systems (position error feeds pitch/roll control), this works decently well for helicopters in BDA

        // Terrain avoidance and below minimum altitude globals.
        bool belowMinAltitude; // True when below minAltitude or avoiding terrain.
        bool avoidingTerrain = false; // True when avoiding terrain.
        bool initialTakeOff = true; // False after the initial take-off
        Vector3 terrainAlertNormal; // Approximate surface normal at the terrain intercept.

        // values currently hard-coded since VTOL AI is adapted from surface AI, but should be removed/changed as AI behavior is improved
        public string SurfaceTypeName = "Amphibious"; // hard code this for the moment until we have something better
        public bool PoweredSteering = true;
        public float MaxDrift = 180;
        public float AvoidMass = 0f;

        public AIUtils.VehicleMovementType SurfaceType
            => (AIUtils.VehicleMovementType)Enum.Parse(typeof(AIUtils.VehicleMovementType), SurfaceTypeName);

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerFactor"),//Steer Factor
            UI_FloatRange(minValue = 0.2f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerMult = 6;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerKi"), //Steer Ki
            UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float steerKiAdjust = 0.4f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerDamping"),//Steer Damping
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerDamping = 3;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DefaultAltitude"), //Default Alt.
            UI_FloatRange(minValue = 25f, maxValue = 5000f, stepIncrement = 50f, scene = UI_Scene.All)]
        public float defaultAltitude = 300;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CombatAltitude"), //Combat Alt.
            UI_FloatRange(minValue = 25f, maxValue = 5000f, stepIncrement = 50f, scene = UI_Scene.All)]
        public float CombatAltitude = 150;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinAltitude"), //Min Altitude
            UI_FloatRange(minValue = 10f, maxValue = 1000, stepIncrement = 10f, scene = UI_Scene.All)]
        public float minAltitude = 100f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxSpeed"),//Max speed
            UI_FloatRange(minValue = 5f, maxValue = 200f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxSpeed = 80;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CombatSpeed"),//Combat speed
            UI_FloatRange(minValue = 5f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float CombatSpeed = 40;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxPitchAngle"),//Max pitch angle
            UI_FloatRange(minValue = 1f, maxValue = 90f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxPitchAngle = 30f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxBankAngle"),// Max Bank angle
            UI_FloatRange(minValue = 0f, maxValue = 90f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxBankAngle = 30;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_WeaveFactor"),//Weave Factor
    UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float WeaveFactor = 6.5f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEngagementRange"),//Min engagement range
            UI_FloatRange(minValue = 0f, maxValue = 6000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MinEngagementRange = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxEngagementRange"),//Max engagement range
            UI_FloatRange(minValue = 500f, maxValue = 8000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float MaxEngagementRange = 4000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BroadsideAttack"),//Attack vector
            UI_Toggle(enabledText = "#LOC_BDArmory_BroadsideAttack_enabledText", disabledText = "#LOC_BDArmory_BroadsideAttack_disabledText")]//Broadside--Bow
        public bool BroadsideAttack = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ManeuverRCS"),//RCS active
            UI_Toggle(enabledText = "#LOC_BDArmory_ManeuverRCS_enabledText", disabledText = "#LOC_BDArmory_ManeuverRCS_disabledText", scene = UI_Scene.All),]//Maneuvers--Combat
        public bool ManeuverRCS = false;

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
            { nameof(defaultAltitude), 100000f },
            { nameof(CombatAltitude), 100000f },
            { nameof(minAltitude), 100000f },
            { nameof(steerMult), 200f },
            { nameof(steerKiAdjust), 20f },
            { nameof(steerDamping), 100f },
            { nameof(MaxPitchAngle), 90f },
            { nameof(CombatSpeed), 300f },
            { nameof(MaxSpeed), 400f },
            { nameof(MinEngagementRange), 20000f },
            { nameof(MaxEngagementRange), 30000f },
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
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Factor</color> - higher will make the AI apply more control input for the same desired rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Ki</color> - higher will make the AI apply control trim faster");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Damping</color> - higher will make the AI apply more control input when it wants to stop rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Default Alt.</color> - AI will fly at this altitude outside of combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Combat Altitude</color> - AI will fly at this altitude during combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Altitude</color> - below this altitude AI will prioritize gaining altitude over combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max Speed</color> - the maximum combat speed");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Combat Speed</color> - the default speed at which it is safe to maneuver");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max pitch angle</color> - the limit on pitch when moving");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Bank angle</color> - the limit on roll when turning, positive rolls into turns");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Attack vector</color> - does the vessel attack from the front or the sides");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min engagement range</color> - AI will try to move away from oponents if closer than this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max engagement range</color> - AI will prioritize getting closer over attacking when beyond this range");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- RCS active</color> - Use RCS during any maneuvers, or only in combat ");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
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
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            pathingMatrix = new AIUtils.TraversabilityMatrix();

            if (!altitudeControl)
            {
                altitudeControl = gameObject.AddComponent<BDVTOLSpeedControl>();
                altitudeControl.vessel = vessel;
            }
            altitudeControl.Activate();

            if (initialTakeOff && !vessel.LandedOrSplashed) // In case we activate pilot after taking off manually.
                initialTakeOff = false;

            if (BroadsideAttack && sideSlipDirection == 0)
            {
                SetBroadsideDirection(OrbitDirectionName);
            }

            leftPath = true;
            extendingTarget = null;
            bypassTarget = null;
            collisionDetectionTicker = 6;
        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (altitudeControl)
                altitudeControl.Deactivate();
        }

        public void SetChooseOptions()
        {
            UI_ChooseOption broadisdeEditor = (UI_ChooseOption)Fields["OrbitDirectionName"].uiControlEditor;
            UI_ChooseOption broadisdeFlight = (UI_ChooseOption)Fields["OrbitDirectionName"].uiControlFlight;
            broadisdeEditor.onFieldChanged = ChooseOptionsUpdated;
            broadisdeFlight.onFieldChanged = ChooseOptionsUpdated;
            //UI_ChooseOption SurfaceEditor = (UI_ChooseOption)Fields["SurfaceTypeName"].uiControlEditor; // If SurfaceTypeName is ever switched from hard-coded to Amphibious, change this
            //UI_ChooseOption SurfaceFlight = (UI_ChooseOption)Fields["SurfaceTypeName"].uiControlFlight;
            //SurfaceEditor.onFieldChanged = ChooseOptionsUpdated;
            //SurfaceFlight.onFieldChanged = ChooseOptionsUpdated;
        }

        public void ChooseOptionsUpdated(BaseField field, object obj)
        {
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
                        StartCoroutine(setVar(s.Current, (float)typeof(BDModuleVTOLAI).GetField(s.Current).GetValue(this)));
                    }
                toEleven = UpToEleven;
            }
        }

        IEnumerator setVar(string name, float value)
        {
            yield return new WaitForFixedUpdate();
            typeof(BDModuleVTOLAI).GetField(name).SetValue(this, value);
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

            //GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 2, Color.blue);
            //GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right), 2, Color.green);

            // Vel vectors
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + Vector3.Project(vessel.Velocity(), vesselTransform.up.ProjectOnPlanePreNormalized(upDir)).normalized * 10f, 2, Color.cyan); //forward/rev
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + Vector3.Project(vessel.Velocity(), vesselTransform.right.ProjectOnPlanePreNormalized(upDir)).normalized * 10f, 3, Color.yellow); //lateral


            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 5, Color.red);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 1000, 3, Color.white);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + -vesselTransform.forward * 100, 3, Color.yellow);
            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vessel.Velocity().normalized * 100, 3, Color.magenta);

            GUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + rollTarget, 2, Color.blue);

            pathingMatrix.DrawDebug(vessel.CoM, pathingWaypoints);
        }

        #endregion events

        #region Actual AI Pilot

        protected override void AutoPilot(FlightCtrlState s)
        {
            if (!vessel.Autopilot.Enabled)
                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            targetVelocity = 0;
            targetLatVelocity = 0;
            targetDirection = vesselTransform.up;
            targetAltitude = defaultAltitude;
            aimingMode = false;
            upDir = vessel.up;
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine("");

            if (initialTakeOff)
            {
                Takeoff();
            }
            // pilot logic figures out what we're supposed to be doing, and sets the base state
            PilotLogic(); // TODO: pitch based on targetVelocity, roll always 0
            // situational awareness modifies the base as best as it can (evasive mainly)
            Tactical();
            CheckLandingGear();
            //CommandAttitude(); // Determine pitch/roll/yaw for movement
            AttitudeControl(s); // move according to commanded movement
            AdjustThrottle(targetVelocity); // set throttle according to our targets and movement
        }

        void PilotLogic() // Surface AI-based with byass target disabled
        {
            // check for belowMinAlt
            belowMinAltitude = (float)vessel.radarAltitude < minAltitude;

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
                targetVelocity = PoweredSteering ? MaxSpeed : CombatSpeed;
                targetDirection = (Vector3)dodgeVector;
                SetStatus($"Avoiding Collision");
                leftPath = true;
                return;
            }

            // check for enemy targets and engage
            // not checking for guard mode, because if guard mode is off now you can select a target manually and if it is of opposing team, the AI will try to engage while you can man the turrets
            if (weaponManager && targetVessel != null && !BDArmorySettings.PEACE_MODE)
            {
                leftPath = true;

                Vector3 vecToTarget = targetVessel.CoM - vessel.CoM;
                float distance = vecToTarget.magnitude;
                // lead the target a bit, where 1km/s is a ballpark estimate of the average bullet velocity
                float shotSpeed = 1000f;
                if ((weaponManager != null ? weaponManager.selectedWeapon : null) is ModuleWeapon wep)
                    shotSpeed = wep.bulletVelocity;
                vecToTarget = targetVessel.PredictPosition(distance / shotSpeed) - vessel.CoM;

                if (BroadsideAttack)
                {
                    Vector3 sideVector = Vector3.Cross(vecToTarget, upDir); //find a vector perpendicular to direction to target
                    if (collisionDetectionTicker == 10
                            && !pathingMatrix.TraversableStraightLine(
                                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                    VectorUtils.WorldPositionToGeoCoords(vessel.PredictPosition(10), vessel.mainBody),
                                    vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass))
                        sideSlipDirection = -Math.Sign(Vector3.Dot(vesselTransform.up, sideVector)); // switch sides if we're running ashore
                    sideVector *= sideSlipDirection;

                    float sidestep = distance >= MaxEngagementRange ? Mathf.Clamp01((MaxEngagementRange - distance) / (CombatSpeed * Mathf.Clamp(90 / MaxDrift, 0, 10)) + 1) * AttackAngleAtMaxRange / 90 : // direct to target to attackAngle degrees if over maxrange
                        (distance <= MinEngagementRange ? 1.5f - distance / (MinEngagementRange * 2) : // 90 to 135 degrees if closer than minrange
                        (MaxEngagementRange - distance) / (MaxEngagementRange - MinEngagementRange) * (1 - AttackAngleAtMaxRange / 90) + AttackAngleAtMaxRange / 90); // attackAngle to 90 degrees from maxrange to minrange
                    targetDirection = Vector3.LerpUnclamped(vecToTarget.normalized, sideVector.normalized, sidestep); // interpolate between the side vector and target direction vector based on sidestep
                    targetVelocity = MaxSpeed;
                    targetAltitude = CombatAltitude;
                    if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"Broadside attack angle {sidestep}");
                }
                else // just point at target and go
                {
                    targetAltitude = CombatAltitude;
                    if ((targetVessel.horizontalSrfSpeed < 10 || Vector3.Dot(targetVessel.srf_vel_direction.ProjectOnPlanePreNormalized(upDir), vessel.up) < 0) //if target is stationary or we're facing in opposite directions
                        && (distance < MinEngagementRange || (distance < (MinEngagementRange * 3 + MaxEngagementRange) / 4 //and too close together
                        && extendingTarget != null && targetVessel != null && extendingTarget == targetVessel)))
                    {
                        extendingTarget = targetVessel;
                        // not sure if this part is very smart, potential for improvement
                        targetDirection = -vecToTarget; //extend
                        targetVelocity = MaxSpeed;
                        targetAltitude = CombatAltitude;
                        SetStatus($"Extending");
                        return;
                    }
                    else
                    {
                        extendingTarget = null;
                        targetDirection = vecToTarget.ProjectOnPlanePreNormalized(upDir);
                        if (Vector3.Dot(targetDirection, vesselTransform.up) < 0)
                            targetVelocity = PoweredSteering ? MaxSpeed : 0; // if facing away from target
                        else if (distance >= MaxEngagementRange || distance <= MinEngagementRange)
                            targetVelocity = MaxSpeed;
                        else
                        {
                            targetVelocity = CombatSpeed / 10 + (MaxSpeed - CombatSpeed / 10) * (distance - MinEngagementRange) / (MaxEngagementRange - MinEngagementRange); //slow down if inside engagement range to extend shooting opportunities
                            if (weaponManager != null && weaponManager.selectedWeapon != null)
                            {
                                switch (weaponManager.selectedWeapon.GetWeaponClass())
                                {
                                    case WeaponClasses.Missile:
                                        MissileBase missile = weaponManager.CurrentMissile;
                                        if (missile != null)
                                        {
                                            if (missile.TargetingMode == MissileBase.TargetingModes.Heat && !weaponManager.heatTarget.exists)
                                            {
                                                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"Attempting heat lock");
                                                aimingMode = true;
                                                targetDirection = MissileGuidance.GetAirToAirFireSolution(missile, targetVessel);
                                            }
                                            else
                                            {
                                                if (!weaponManager.GetLaunchAuthorization(targetVessel, weaponManager, missile) && (Vector3.SqrMagnitude(targetVessel.vesselTransform.position - vesselTransform.position) < (missile.engageRangeMax * missile.engageRangeMax)))
                                                {
                                                    aimingMode = true;
                                                    targetDirection = MissileGuidance.GetAirToAirFireSolution(missile, targetVessel);
                                                }
                                            }
                                        }
                                        break;
                                    case WeaponClasses.Gun:
                                    case WeaponClasses.Rocket:
                                    case WeaponClasses.DefenseLaser:
                                        var gun = (ModuleWeapon)weaponManager.selectedWeapon;
                                        if (gun != null && (gun.yawRange == 0 || gun.maxPitch == gun.minPitch) && gun.FiringSolutionVector != null)
                                        {
                                            aimingMode = true;
                                            if (Vector3.Angle(vesselTransform.up, ((Vector3)gun.FiringSolutionVector).ProjectOnPlanePreNormalized(vesselTransform.right)) < MaxPitchAngle)
                                                targetDirection = (Vector3)gun.FiringSolutionVector;
                                        }
                                        break;
                                }
                            }
                        }
                        targetVelocity = Mathf.Clamp(targetVelocity, PoweredSteering ? CombatSpeed / 5 : 0, MaxSpeed); // maintain a bit of speed if using powered steering
                    }
                }
                SetStatus($"Engaging target");
                return;
            }

            // follow
            if (command == PilotCommands.Follow)
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


            // goto
            if (leftPath)
            {
                Pathfind(finalPositionGeo);
                leftPath = false;
            }

            const float targetRadius = 250f;
            targetDirection = (assignedPositionWorld - vesselTransform.position).ProjectOnPlanePreNormalized(upDir);

            if (targetDirection.sqrMagnitude > targetRadius * targetRadius)
            {

                targetVelocity = MaxSpeed;

                if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
                SetStatus("Moving");
                return;
            }

            cycleWaypoint();

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
            if (weaponManager.underFire || weaponManager.missileIsIncoming)
            {
                targetVelocity = MaxSpeed;
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
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"underFire {weaponManager.underFire}, aimingMode {aimingMode}, weaveAdjustment {weaveAdjustment}");
        }

        void AdjustThrottle(float targetSpeed)
        {
            altitudeControl.targetAltitude = targetAltitude;
        }

        //Controller Integral
        Vector3 directionIntegral;
        float pitchIntegral;
        float yawIntegral;
        float rollIntegral;

        void AttitudeControl(FlightCtrlState s)
        {
            Vector3 yawTarget = targetDirection.ProjectOnPlanePreNormalized(vesselTransform.forward);

            float yawError = VectorUtils.SignedAngle(vesselTransform.up, yawTarget, vesselTransform.right) + (aimingMode ? 0 : weaveAdjustment);
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"yaw target: {yawTarget}, yaw error: {yawError}");

            float forwardVel = Vector3.Dot(vessel.Velocity(), vesselTransform.up.ProjectOnPlanePreNormalized(upDir).normalized);
            float forwardAccel = Vector3.Dot(vessel.acceleration_immediate, vesselTransform.up.ProjectOnPlanePreNormalized(upDir).normalized);
            float velError = targetVelocity - forwardVel;
            float pitchAngle = Mathf.Clamp(0.015f * -steerMult * velError - 0.33f * -steerDamping * forwardAccel, -MaxPitchAngle, MaxPitchAngle); //Adjust pitchAngle for desired speed

            if (aimingMode)
                pitchAngle = VectorUtils.SignedAngle(vesselTransform.up, targetDirection.ProjectOnPlanePreNormalized(vesselTransform.right), -vesselTransform.forward);
            else if (belowMinAltitude || targetVelocity == 0f)
                pitchAngle = 0f;
            else if (avoidingTerrain)
                pitchAngle = 90 - Vector3.Angle(targetDirection.ProjectOnPlanePreNormalized(vesselTransform.right), upDir);


            float pitch = 90 - Vector3.Angle(vesselTransform.up, upDir);

            float pitchError = pitchAngle - pitch;
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"target vel: {targetVelocity}, forward vel: {forwardVel}, vel error: {velError}, target pitch: {pitchAngle}, pitch: {pitch}, pitch error: {pitchError}");

            float bank = VectorUtils.SignedAngle(-vesselTransform.forward, upDir, -vesselTransform.right);
            float latVel = Vector3.Dot(vessel.Velocity(), vesselTransform.right.ProjectOnPlanePreNormalized(upDir).normalized);
            float latAccel = Vector3.Dot(vessel.acceleration_immediate, vesselTransform.right.ProjectOnPlanePreNormalized(upDir).normalized);
            float latError = targetLatVelocity - latVel;
            float targetRoll = Mathf.Clamp(0.015f * steerMult * latError - 0.1f * steerDamping * latAccel, -MaxBankAngle, MaxBankAngle); //Adjust pitchAngle for desired speed
            if (belowMinAltitude || initialTakeOff)
            {
                if (avoidingTerrain)
                {
                    terrainAlertNormal = upDir; // FIXME Terrain avoidance isn't implemented for this AI yet.
                    rollTarget = terrainAlertNormal * 100;
                }
                else
                    rollTarget = upDir * 100;
                targetRoll = VectorUtils.SignedAngle(rollTarget, upDir, -vesselTransform.right);
            }
            else
                rollTarget = Vector3.RotateTowards(upDir, -vesselTransform.right, targetRoll * Mathf.PI / 180f, 0f);

            float rollError = targetRoll - bank;
            if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_AI) DebugLine($"target lat vel: {targetLatVelocity}, lateral vel: {latVel}, lat vel error: {latError}, target roll: {targetRoll}, bank: {bank}, roll error: {rollError}");

            Vector3 localAngVel = vessel.angularVelocity;
            #region PID calculations
            // FIXME Why are there various constants in here that mess with the scaling of the PID in the various axes? Ratios between the axes are 1:0.33:0.1
            float pitchProportional = 0.015f * steerMult * pitchError;
            float yawProportional = 0.005f * steerMult * yawError;
            float rollProportional = 0.0015f * steerMult * rollError;

            float pitchDamping = steerDamping * -localAngVel.x;
            float yawDamping = 0.33f * steerDamping * -localAngVel.z;
            float rollDamping = 0.1f * steerDamping * -localAngVel.y;

            // For the integral, we track the vector of the pitch and yaw in the 2D plane of the vessel's forward pointing vector so that the pitch and yaw components translate between the axes when the vessel rolls.
            directionIntegral = (directionIntegral + (pitchError * -vesselTransform.forward + yawError * vesselTransform.right) * Time.deltaTime).ProjectOnPlanePreNormalized(vesselTransform.up);
            if (directionIntegral.sqrMagnitude > 1f) directionIntegral = directionIntegral.normalized;
            pitchIntegral = steerKiAdjust * Vector3.Dot(directionIntegral, -vesselTransform.forward);
            yawIntegral = 0.33f * steerKiAdjust * Vector3.Dot(directionIntegral, vesselTransform.right);
            rollIntegral = 0.1f * steerKiAdjust * Mathf.Clamp(rollIntegral + rollError * Time.deltaTime, -1f, 1f);

            SetFlightControlState(s,
                s.pitch = pitchProportional + pitchIntegral - pitchDamping,
                s.yaw = yawProportional + yawIntegral - yawDamping,
                s.roll = rollProportional + rollIntegral - rollDamping
            );
            #endregion

            if (ManeuverRCS && (Mathf.Abs(s.roll) >= 1 || Mathf.Abs(s.pitch) >= 1 || Mathf.Abs(s.yaw) >= 1))
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        #endregion Actual AI Pilot

        #region Autopilot helper functions

        public override bool CanEngage()
        {
            return !vessel.LandedOrSplashed;
        }

        GameObject vobj;

        Transform velocityTransform
        {
            get
            {
                if (!vobj)
                {
                    vobj = new GameObject("velObject");
                    vobj.transform.position = vessel.ReferenceTransform.position;
                    vobj.transform.parent = vessel.ReferenceTransform;
                }

                return vobj.transform;
            }
        }

        void CheckLandingGear()
        {
            if (!vessel.LandedOrSplashed)
            {
                if (vessel.radarAltitude > Mathf.Min(50f, minAltitude / 2f))
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);
                else
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
            }
        }

        void Takeoff()
        {
            belowMinAltitude = (float)vessel.radarAltitude < minAltitude;
            if (vessel.Landed && (float)vessel.radarAltitude > 1)
            {
                vessel.Landed = false; // KSP sometimes isn't updating this correctly after spawning.
                vessel.Splashed = vessel.altitude < 0; // Radar altitude could be > 1, while the craft is still underwater due to the way radarAlt works...
            }
            if (!belowMinAltitude)
                initialTakeOff = false;
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
        {
            if (!vessel) return false;

            return true;
        }

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
                    vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass))
            {
                bypassTarget = target;
                bypassTargetPos = VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody);
                pathingWaypoints = pathingMatrix.Pathfind(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass);
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
                                    destination, vessel.mainBody, SurfaceType, MaxPitchAngle, AvoidMass);
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
