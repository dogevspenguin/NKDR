using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.Localization;

using BDArmory.Control;
using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.Targeting;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.WeaponMounts;

namespace BDArmory.Radar
{
    public class ModuleIRST : PartModule
    {
        #region KSPFields (Part Configuration)

        #region General Configuration

        [KSPField]
        public string IRSTName;

        [KSPField]
        public int turretID = 0;

        [KSPField]
        public string rotationTransformName = string.Empty;
        Transform rotationTransform;

        [KSPField]
        public string irstTransformName = string.Empty;
        Transform irstTransform;

        #endregion General Configuration

        #region Capabilities

        [KSPField]
        public double resourceDrain = 0.825;        //resource (EC/sec) usage of active irst

        [KSPField]
        public bool omnidirectional = true;			//false=boresight only

        [KSPField]
        public float directionalFieldOfView = 90;	//relevant for omnidirectional only

        [KSPField]
        public float boresightFOV = 10;				//relevant for boresight only

        [KSPField]
        public float scanRotationSpeed = 120; 		//in degrees per second, relevant for omni and directional

        [KSPField]
        public bool showDirectionWhileScan = false; //irst can show direction indicator of contacts (false: can show contacts as blocks only)

        [KSPField]
        public bool canScan = true;                 //irst has detection capabilities

        [KSPField]
        public bool irstRanging = false;            //irst can get ranging info for target distance

        [KSPField]
        public FloatCurve DetectionCurve = new FloatCurve();		//FloatCurve setting default ranging capabilities of the IRST

        [KSPField]
        public FloatCurve TempSensitivityCurve = new FloatCurve();		//FloatCurve setting default IR spectrum capabilities of the IRST

        [KSPField]
        public FloatCurve atmAttenuationCurve = new FloatCurve();        //FloatCurve range increase/decrease based on atm density/temp, thinner/cooler air yields longer range returns


        [KSPField]
        public float GroundClutterFactor = 0.16f; //Factor defining how effective the irst is at detecting heatsigs against ambient ground temperature (0=ineffective, 1=fully effective)
                                                       //default to 0.16, IRSTs have about a 6th of the detection range for ground targets vs air targets.

        #endregion Capabilities

        #region Persisted State in flight

        [KSPField(isPersistant = true)]
        public string linkedVesselID;

        [KSPField(isPersistant = true)]
        public bool irstEnabled;

        [KSPField(isPersistant = true)]
        public int rangeIndex = 99;

        [KSPField(isPersistant = true)]
        public float currentAngle = 0;

        #endregion Persisted State in flight

        #endregion KSPFields (Part Configuration)

        #region KSP Events & Actions

        [KSPAction("Toggle IRST")]
        public void AGEnable(KSPActionParam param)
        {
            if (irstEnabled)
            {
                DisableIRST();
            }
            else
            {
                EnableIRST();
            }
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_ToggleIRST")]//Toggle IRST - FIXME - Localize
        public void Toggle()
        {
            if (irstEnabled)
            {
                DisableIRST();
            }
            else
            {
                EnableIRST();
            }
        }

        #endregion KSP Events & Actions

        #region Part members

        public float irstMinDistanceDetect
        {
            get { return DetectionCurve.minTime; }
        }

        //[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "Detection Range")]
        public float irstMaxDistanceDetect
        {
            get { return DetectionCurve.maxTime; }
        }

        //GUI
        private bool drawGUI;
        public float signalPersistTime;

        //scanning
        public Transform referenceTransform;
        private float radialScanDirection = 1;

        public bool boresightScan;

        //locking
        public bool slaveTurrets;
        public ModuleTurret lockingTurret;
        public bool lockingPitch = true;
        public bool lockingYaw = true;

        //vessel
        private MissileFire wpmr;

        public MissileFire weaponManager
        {
            get
            {
                if (wpmr != null && wpmr.vessel == vessel) return wpmr;
                wpmr = VesselModuleRegistry.GetMissileFire(vessel, true);
                return wpmr;
            }
            set { wpmr = value; }
        }

        public VesselRadarData vesselRadarData;
        private string myVesselID;

        // part state
        private bool startupComplete;
        public float leftLimit;
        public float rightLimit;

        #endregion Part members

        void UpdateToggleGuiName()
        {
            Events["Toggle"].guiName = irstEnabled ? StringUtils.Localize("#autoLOC_bda_1000036") : StringUtils.Localize("#autoLOC_bda_1000037");		// fixme - fix localizations
        }

        public void EnsureVesselRadarData() 
        {
            if (vessel == null) return;
            //myVesselID = vessel.id.ToString();

            if (vesselRadarData != null && vesselRadarData.vessel == vessel) return;
            vesselRadarData = vessel.gameObject.GetComponent<VesselRadarData>();

            if (vesselRadarData == null)
            {
                vesselRadarData = vessel.gameObject.AddComponent<VesselRadarData>();
                vesselRadarData.weaponManager = weaponManager;
            }
        }

        public void EnableIRST()
        {
            EnsureVesselRadarData();
            irstEnabled = true;

            var mf = VesselModuleRegistry.GetMissileFire(vessel, true);
            UpdateToggleGuiName();
            vesselRadarData.AddIRST(this);
        }

        public void DisableIRST()
        {
            irstEnabled = false;
            UpdateToggleGuiName();

            if (vesselRadarData)
            {
                vesselRadarData.RemoveIRST(this);
            }
            using (var loadedvessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedvessels.MoveNext())
                {
                    BDATargetManager.ClearRadarReport(loadedvessels.Current, weaponManager); //reset radar contact status
                }
        }

        void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (vesselRadarData)
                {
                    vesselRadarData.RemoveIRST(this);
                    vesselRadarData.RemoveDataFromIRST(this);
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsFlight)
            {
                myVesselID = vessel.id.ToString();

                if (string.IsNullOrEmpty(IRSTName))
                {
                    IRSTName = part.partInfo.title;
                }

                signalPersistTime = omnidirectional
    ? 360 / (scanRotationSpeed + 5)
    : directionalFieldOfView / (scanRotationSpeed + 5);

                if (rotationTransformName != string.Empty)
                {
                    rotationTransform = part.FindModelTransform(rotationTransformName);
                }
                irstTransform = irstTransformName != string.Empty ? part.FindModelTransform(irstTransformName) : part.transform;
                referenceTransform = (new GameObject()).transform;
                referenceTransform.parent = irstTransform;
                referenceTransform.localPosition = Vector3.zero;

                // fill TempSensitivityCurve with default values if not set by part config:
                if (TempSensitivityCurve.minTime == float.MaxValue)
                    TempSensitivityCurve.Add(0f, 1f);

                List<ModuleTurret>.Enumerator turr = part.FindModulesImplementing<ModuleTurret>().GetEnumerator();
                while (turr.MoveNext())
                {
                    if (turr.Current == null) continue;
                    if (turr.Current.turretID != turretID) continue;
                    lockingTurret = turr.Current;
                    break;
                }
                turr.Dispose();

                //GameEvents.onVesselGoOnRails.Add(OnGoOnRails);    //not needed
                EnsureVesselRadarData();
                StartCoroutine(StartUpRoutine());
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                //Editor only:
                List<ModuleTurret>.Enumerator tur = part.FindModulesImplementing<ModuleTurret>().GetEnumerator();
                while (tur.MoveNext())
                {
                    if (tur.Current == null) continue;
                    if (tur.Current.turretID != turretID) continue;
                    lockingTurret = tur.Current;
                    break;
                }
                tur.Dispose();
                if (lockingTurret)
                {
                    lockingTurret.Fields["minPitch"].guiActiveEditor = false;
                    lockingTurret.Fields["maxPitch"].guiActiveEditor = false;
                    lockingTurret.Fields["yawRange"].guiActiveEditor = false;
                }
            }
        }

        IEnumerator StartUpRoutine()
        {
            if (BDArmorySettings.DEBUG_RADAR)
                Debug.Log("[BDArmory.ModuleIRST]: StartupRoutine: " + IRSTName + " enabled: " + irstEnabled);
            yield return new WaitWhile(() => !FlightGlobals.ready || (vessel is not null && (vessel.packed || !vessel.loaded)));
            yield return new WaitForFixedUpdate();
            UpdateToggleGuiName();
            startupComplete = true;
        }

        void Update()
        {
            drawGUI = (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !vessel.packed && irstEnabled &&
                       vessel.isActiveVessel && BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled);
        }

        void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && startupComplete)
            {
                if (!vessel.IsControllable && irstEnabled)
                {
                    DisableIRST();
                }

                if (irstEnabled)
                {
                    DrainElectricity(); //physics behaviour, thus moved here from update

                    if (boresightScan)
                    {
                        BoresightScan();
                    }
                    else if (canScan)
                    {
                        Scan();
                    }
                }

                if (!vessel.packed && irstEnabled)
                {
                    if (omnidirectional)
                    {
                        referenceTransform.position = part.transform.position;
                        referenceTransform.rotation =
                            Quaternion.LookRotation(VectorUtils.GetNorthVector(irstTransform.position, vessel.mainBody),
                                VectorUtils.GetUpDirection(transform.position));
                    }
                    else
                    {
                        referenceTransform.position = part.transform.position;
                        referenceTransform.rotation = Quaternion.LookRotation(irstTransform.up,
                            VectorUtils.GetUpDirection(referenceTransform.position));
                    }
                    //UpdateInputs();
                }
            }
        }

        void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && canScan)
            {
                UpdateModel();
            }
        }

        void UpdateModel()
        {
            //model rotation
            if (irstEnabled)
            {
                if (rotationTransform && canScan)
                {
                    Vector3 direction;

                        direction = Quaternion.AngleAxis(currentAngle, referenceTransform.up) * referenceTransform.forward;

                    Vector3 localDirection = rotationTransform.parent.InverseTransformDirection(direction).ProjectOnPlanePreNormalized(Vector3.up);
                    if (localDirection != Vector3.zero)
                    {
                        rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation,
                            Quaternion.LookRotation(localDirection, Vector3.up), 10 * TimeWarp.fixedDeltaTime);
                    }
                }
            }
            else
            {
                if (rotationTransform)
                {
                    rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation,
                        Quaternion.identity, 5 * TimeWarp.fixedDeltaTime);
                }
            }
        }

        void Scan()
        {
            float angleDelta = scanRotationSpeed * Time.fixedDeltaTime;
            RadarUtils.IRSTUpdateScan(weaponManager, currentAngle, referenceTransform, boresightFOV, referenceTransform.position, this);

            if (omnidirectional)
            {
                currentAngle = Mathf.Repeat(currentAngle + angleDelta, 360);
            }
            else
            {
                currentAngle += radialScanDirection * angleDelta;

                if (Mathf.Abs(currentAngle) > directionalFieldOfView / 2)
                {
                    currentAngle = Mathf.Sign(currentAngle) * directionalFieldOfView / 2;
                    radialScanDirection = -radialScanDirection;
                }
            }
        }        

        void BoresightScan()
        {
            currentAngle = Mathf.Lerp(currentAngle, 0, 0.08f);
            RadarUtils.IRSTUpdateScan(weaponManager, currentAngle, referenceTransform, boresightFOV, referenceTransform.position, this);
        }

        public void ReceiveContactData(TargetSignatureData contactData, float _magnitude)
        {
            if (vesselRadarData)
            {
                vesselRadarData.AddIRSTContact(this, contactData, _magnitude);
            }
        }


        void OnGUI()
        {
            if (drawGUI)
            {
                if (boresightScan)
                {
                    GUIUtils.DrawTextureOnWorldPos(transform.position + (3500 * transform.up),
                        BDArmorySetup.Instance.dottedLargeGreenCircle, new Vector2(156, 156), 0);
                }
            }
        }

        // RMB info in editor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000008", omnidirectional ? StringUtils.Localize("#autoLOC_bda_1000019") : StringUtils.Localize("#autoLOC_bda_1000020")));

            output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000021", resourceDrain)); //Ec/sec

                output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000022", directionalFieldOfView)); //Field of View

                output.Append(Environment.NewLine);
                output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000024")); //Capabilities
                output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000025", canScan)); //-Scanning

                output.Append(Environment.NewLine);
                output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000030")); //Performance

                if (canScan)
                    output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000031", DetectionCurve.Evaluate(irstMaxDistanceDetect)-273, irstMaxDistanceDetect)); //Detection x.xx deg C @ n km
                else
                    output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000032"));

                    output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000034"));
                output.AppendLine(StringUtils.Localize("#autoLOC_bda_1000035", GroundClutterFactor));


            return output.ToString();
        }

        void DrainElectricity()
        {
            if (resourceDrain <= 0)
            {
                return;
            }

            double drainAmount = resourceDrain * TimeWarp.fixedDeltaTime;
            double chargeAvailable = part.RequestResource("ElectricCharge", drainAmount, ResourceFlowMode.ALL_VESSEL);
            if (chargeAvailable < drainAmount * 0.95f)
            {
                ScreenMessages.PostScreenMessage(StringUtils.Localize("#autoLOC_bda_1000016"), 5.0f, ScreenMessageStyle.UPPER_CENTER);		// #autoLOC_bda_1000016 = Radar Requires EC
                DisableIRST();
            }
        }
    }
}
