using BDArmory.UI;
using System.Collections;
using System.Text;
using UnityEngine;

namespace BDArmory.CounterMeasure
{
    public class ModuleCloakingDevice : PartModule
    {
        Coroutine cloakRoutine;
        Coroutine decloakRoutine;

        [KSPField] public bool OpticalCloaking = true;

        [KSPField] public bool ThermalCloaking = false;

        [KSPField] public float opticalReductionFactor = 0.05f; //for Optic camo to reduce enemy view range

        [KSPField] public float thermalReductionFactor = 1f; //for thermoptic camo to reduce apparent thermal sig

        [KSPField] public double resourceDrain = 5;

        [KSPField] public string resourceName = "ElectricCharge";

        [KSPField] public float CloakTime = 1;

        [KSPField] public bool alwaysOn = false;

        [KSPField] public float cooldownInterval = -1;

        [KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_BDArmory_Enabled")]//Enabled 
        public bool cloakEnabled = false;

        bool enabling = false;

        bool disabling = false;

        float cloakTimer = 0;

        float cooldownTimer = 0;

        private BDStagingAreaGauge gauge;

        private int resourceID;

        //part anim support?

        VesselCloakInfo vesselCloak;

        [KSPAction("Enable")]
        public void AGEnable(KSPActionParam param)
        {
            if (!cloakEnabled)
            {
                EnableCloak();
            }
        }

        [KSPAction("Disable")]
        public void AGDisable(KSPActionParam param)
        {
            if (cloakEnabled)
            {
                DisableCloak();
            }
        }

        [KSPAction("Toggle")]
        public void AGToggle(KSPActionParam param)
        {
            Toggle();
        }

        [KSPEvent(guiActiveEditor = false, guiActive = true, guiName = "#LOC_BDArmory_Toggle")]//Toggle
        public void Toggle()
        {
            if (cloakEnabled)
            {
                DisableCloak();
            }
            else
            {
                EnableCloak();
            }
        }
        void Start()
        {
            resourceID = PartResourceLibrary.Instance.GetDefinition(resourceName).id;
        }
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!HighLogic.LoadedSceneIsFlight) return;
            part.force_activate();

            gauge = (BDStagingAreaGauge)part.AddModule("BDStagingAreaGauge");
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            EnsureVesselCloak();
        }

        void OnDestroy()
        {
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            cloakEnabled = false;
            if (part != null && part.vessel != null)
            {
                using (var Part = part.vessel.Parts.GetEnumerator())
                    while (Part.MoveNext())
                    {
                        if (Part.Current == null) continue;
                        Part.Current.SetOpacity(1);
                    }
            }
        }

        void OnVesselCreate(Vessel v)
        {
            if (v == vessel)
                EnsureVesselCloak();
        }

        public void EnableCloak()
        {
            if (enabling || cloakEnabled) return;
            if (cooldownTimer > 0) return;
            EnsureVesselCloak();

            StopCloakDecloakRoutines();
            cloakTimer = 0;
            cloakRoutine = StartCoroutine(CloakRoutine());
        }

        public void DisableCloak()
        {
            if (disabling || !cloakEnabled) return;
            EnsureVesselCloak();

            vesselCloak.RemoveCloak(this);
            cloakEnabled = false;

            StopCloakDecloakRoutines();
            cloakTimer = CloakTime;
            decloakRoutine = StartCoroutine(DecloakRoutine());
        }

        void StopCloakDecloakRoutines()
        {
            if (decloakRoutine != null)
            {
                StopCoroutine(DecloakRoutine());
                decloakRoutine = null;
            }

            if (cloakRoutine != null)
            {
                StopCoroutine(CloakRoutine());
                cloakRoutine = null;
            }
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (alwaysOn && !cloakEnabled)
            {
                EnableCloak();
            }

            if (cloakEnabled)
            {
                EnsureVesselCloak();

                DrainElectricity();
            }
        }

        void EnsureVesselCloak()
        {
            if (!vesselCloak || vesselCloak.vessel != vessel)
            {
                vesselCloak = vessel.gameObject.GetComponent<VesselCloakInfo>();
                if (!vesselCloak)
                {
                    vesselCloak = vessel.gameObject.AddComponent<VesselCloakInfo>();
                }
            }

            vesselCloak.DelayedCleanCloakList();
        }

        void DrainElectricity()
        {
            if (resourceDrain <= 0)
            {
                return;
            }

            double drainAmount = resourceDrain * TimeWarp.fixedDeltaTime;
            double chargeAvailable = part.RequestResource(resourceID, drainAmount, ResourceFlowMode.ALL_VESSEL);
            if (chargeAvailable < drainAmount * 0.95f)
            {
                DisableCloak();
            }
            //look into having cost scale with vessel size?
        }

        IEnumerator CloakRoutine()
        {
            var wait = new WaitForFixedUpdate();
            enabling = true;
            while (cloakTimer < CloakTime)
            {
                yield return wait;
            }
            enabling = false;
            vesselCloak.AddCloak(this);
            cloakEnabled = true;
        }

        IEnumerator DecloakRoutine()
        {
            var wait = new WaitForFixedUpdate();
            disabling = true;
            while (cloakTimer > 0)
            {
                yield return wait;
            }
            disabling = false;
            cooldownTimer = cooldownInterval;
        }

        void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (BDArmorySetup.GameIsPaused) return;

            if (enabling || disabling)
            {
                if (opticalReductionFactor < 1)
                {
                    using (var Part = this.part.vessel.Parts.GetEnumerator())
                        while (Part.MoveNext())
                        {
                            if (Part.Current == null) continue;
                            Part.Current.SetOpacity(Mathf.Lerp(1, opticalReductionFactor, (cloakTimer / CloakTime)));
                        }
                    if (enabling)
                    {
                        cloakTimer += TimeWarp.fixedDeltaTime;
                    }
                    if (disabling)
                    {
                        cloakTimer -= TimeWarp.fixedDeltaTime;
                    }
                }
                //Debug.Log("[CloakingDevice] " + (enabling ? "cloaking" : "decloaking") + ": cloakTimer: " + cloakTimer);
            }
            if (cooldownTimer > 0)
            {
                cooldownTimer -= TimeWarp.fixedDeltaTime;
                if (vessel.isActiveVessel)
                {
                    gauge.UpdateHeatMeter(cooldownTimer / cooldownInterval);
                }
            }
        }

        // RMB info in editor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine(OpticalCloaking ? ThermalCloaking ? "Thermoptic Cloak" : "Optical Cloak" : "ThermalCloak");
            if (OpticalCloaking)
            {
                output.AppendLine($" -View range reduction: {(1 - opticalReductionFactor) * 100}%");
            }
            if (ThermalCloaking)
            {
                output.AppendLine($" - Heat signature reduction: {(1 - thermalReductionFactor * 100)}%");
            }

            output.AppendLine($"Always on: {alwaysOn}");
            output.AppendLine($"EC/sec: {resourceDrain}");

            return output.ToString();
        }
    }
}