using KSP.Localization;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using UnityEngine;

using BDArmory.FX;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Modules
{
    class ModuleSelfSealingTank : PartModule, IPartMassModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation)
        {
            return FBmass + ArmorMass + FISmass;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true)]
        public bool SSTank = false;

        [KSPEvent(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_SSTank", active = true)]//Self-Sealing Tank
        public void ToggleTankOption()
        {
            SSTank = !SSTank;
            if (!SSTank)
            {
                Events["ToggleTankOption"].guiName = StringUtils.Localize("#LOC_BDArmory_SSTank_On");//"Enable self-sealing tank"

                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
                        resource.Current.maxAmount = Math.Floor(resource.Current.maxAmount * 1.11112);
                        resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                    }
            }
            else
            {
                Events["ToggleTankOption"].guiName = StringUtils.Localize("#LOC_BDArmory_SSTank_Off");//"Disable self-sealing tank"

                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
                        resource.Current.maxAmount *= 0.9;
                        resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                    }
            }
            GUIUtils.RefreshAssociatedWindows(part);
            using (List<Part>.Enumerator pSym = part.symmetryCounterparts.GetEnumerator())
                while (pSym.MoveNext())
                {
                    if (pSym.Current == null) continue;

                    var tank = pSym.Current.FindModuleImplementing<ModuleSelfSealingTank>();
                    if (tank == null) continue;

                    tank.SSTank = SSTank;

                    if (!SSTank)
                    {
                        tank.Events["ToggleTankOption"].guiName = StringUtils.Localize("#LOC_BDArmory_SSTank_On");//"Enable self-sealing tank"

                        using (IEnumerator<PartResource> resource = pSym.Current.Resources.GetEnumerator())
                            while (resource.MoveNext())
                            {
                                if (resource.Current == null) continue;
                                resource.Current.maxAmount = Math.Floor(resource.Current.maxAmount * 1.11112);
                                resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                            }
                    }
                    else
                    {
                        tank.Events["ToggleTankOption"].guiName = StringUtils.Localize("#LOC_BDArmory_SSTank_Off");//"Disable self-sealing tank"

                        using (IEnumerator<PartResource> resource = pSym.Current.Resources.GetEnumerator())
                            while (resource.MoveNext())
                            {
                                if (resource.Current == null) continue;
                                resource.Current.maxAmount *= 0.9;
                                resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                            }
                    }
                    GUIUtils.RefreshAssociatedWindows(pSym.Current);
                }
        }

        [KSPField(isPersistant = true)]
        public bool InertTank = false;

        [KSPEvent(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_FIS", active = true)]//Self-Sealing Tank
        public void ToggleInertOption()
        {
            InertTank = !InertTank;
            if (!InertTank)
            {
                Events["ToggleInertOption"].guiName = StringUtils.Localize("#LOC_BDArmory_FIS_On");//"Enable self-sealing tank"
                FISmass = 0;
                Fields["FireBottles"].guiActiveEditor = true;
                Fields["FBRemaining"].guiActive = true;
            }
            else
            {
                Events["ToggleInertOption"].guiName = StringUtils.Localize("#LOC_BDArmory_FIS_Off");//"Disable self-sealing tank"
                FISmass = 0.15f;
                FireBottles = 0;
                FBSetup(null, null);
                Fields["FireBottles"].guiActiveEditor = false;
                Fields["FBRemaining"].guiActive = false;
            }
            partmass = (FISmass + ArmorMass + FBmass);
            GUIUtils.RefreshAssociatedWindows(part);
            using (List<Part>.Enumerator pSym = part.symmetryCounterparts.GetEnumerator())
                while (pSym.MoveNext())
                {
                    if (pSym.Current == null) continue;

                    var tank = pSym.Current.FindModuleImplementing<ModuleSelfSealingTank>();
                    if (tank == null) continue;

                    tank.InertTank = InertTank;

                    if (!InertTank)
                    {
                        tank.Events["ToggleInertOption"].guiName = StringUtils.Localize("#LOC_BDArmory_FIS_On");//"Add Fuel Inerting System"
                        tank.FISmass = 0;
                        tank.Fields["FireBottles"].guiActiveEditor = true;
                        tank.Fields["FBRemaining"].guiActive = true;
                    }
                    else
                    {
                        tank.Events["ToggleInertOption"].guiName = StringUtils.Localize("#LOC_BDArmory_FIS_Off");//"Remove Fuel Inerting System"
                        tank.FISmass = 0.15f;
                        tank.Fields["FireBottles"].guiActiveEditor = false;
                        tank.Fields["FBRemaining"].guiActive = false;
                    }
                    tank.partmass = (tank.FISmass + tank.ArmorMass + tank.FBmass);
                    GUIUtils.RefreshAssociatedWindows(pSym.Current);
                }
            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        [KSPField(isPersistant = true)]
        public bool armoredCockpit = false;

        [KSPEvent(advancedTweakable = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_Armorcockpit_On", active = true)]//"Add Armored Cockpit"
        public void TogglecockpitArmor()
        {
            armoredCockpit = !armoredCockpit;
            if (!armoredCockpit)
            {
                Events["TogglecockpitArmor"].guiName = StringUtils.Localize("#LOC_BDArmory_Armorcockpit_On");//"Add Armored Cockpit"
                ArmorMass = 0;
            }
            else
            {
                Events["TogglecockpitArmor"].guiName = StringUtils.Localize("#LOC_BDArmory_Armorcockpit_Off");//"Remove Armored Cockpit"
                ArmorMass = 0.2f * part.CrewCapacity;
            }
            partmass = (FISmass + ArmorMass + FBmass);
            GUIUtils.RefreshAssociatedWindows(part);
            using (List<Part>.Enumerator pSym = part.symmetryCounterparts.GetEnumerator())
                while (pSym.MoveNext())
                {
                    if (pSym.Current == null) continue;

                    var tank = pSym.Current.FindModuleImplementing<ModuleSelfSealingTank>();
                    if (tank == null) continue;

                    tank.armoredCockpit = armoredCockpit;

                    if (!armoredCockpit)
                    {
                        tank.Events["TogglecockpitArmor"].guiName = StringUtils.Localize("#LOC_BDArmory_Armorcockpit_On");//"Enable self-sealing tank"
                        tank.ArmorMass = 0;
                    }
                    else
                    {
                        tank.Events["TogglecockpitArmor"].guiName = StringUtils.Localize("#LOC_BDArmory_Armorcockpit_Off");//"Disable self-sealing tank"
                        tank.ArmorMass = 0.2f * part.CrewCapacity;
                    }
                    tank.partmass = (tank.FISmass + tank.ArmorMass + tank.FBmass);
                    GUIUtils.RefreshAssociatedWindows(pSym.Current);
                }
            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }


        [KSPField(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_AddedMass")]//safety systems mass
        public float partmass = 0f;

        public float FBmass { get; private set; } = 0f;
        public float FISmass { get; private set; } = 0f;
        private float ArmorMass = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_FireBottles"),//Fire Bottles
        UI_FloatRange(minValue = 0, maxValue = 5, stepIncrement = 1, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float FireBottles = 0;

        [KSPField(advancedTweakable = true, isPersistant = true, guiActive = true, guiName = "#LOC_BDArmory_FB_Remaining", guiActiveEditor = false), UI_Label(scene = UI_Scene.Flight)]
        public float FBRemaining;

        Coroutine firebottleRoutine;

        PartResource fuel;
        PartResource monoprop;
        PartResource solid;
        public bool isOnFire = false;
        bool procPart = false;
        public bool externallyCalled = false;
        ModuleEngines engine;
        ModuleCommand cockpit;
        private float enginerestartTime = -1;
        public void Start()
        {
            if (part.name.Contains("B9.Aero.Wing.Procedural") || part.name.Contains("procedural")) //could add other proc parts here for similar support
            {
                procPart = true;
            }
            else
            {
                if (part.Modules.Contains("ModuleB9PartSwitch"))
                {
                    var B9FuelSwitch = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "ModuleB9PartSwitch", "baseVolume");
                    if (B9FuelSwitch != null) procPart = true;
                }
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                UI_FloatRange FBEditor = (UI_FloatRange)Fields["FireBottles"].uiControlEditor;
                FBEditor.onFieldChanged = FBSetup;
            }
            cockpit = part.FindModuleImplementing<ModuleCommand>();
            if (cockpit != null)
            {
                if (cockpit.minimumCrew >= 1)
                {
                    Events["TogglecockpitArmor"].guiActiveEditor = true;
                    Events["ToggleTankOption"].guiActiveEditor = false;
                    Events["ToggleInertOption"].guiActiveEditor = false;
                    Fields["FireBottles"].guiActiveEditor = false;
                    Fields["FBRemaining"].guiActive = false;
                    FireBottles = 0;
                    if (!armoredCockpit)
                    {
                        Events["TogglecockpitArmor"].guiName = StringUtils.Localize("#LOC_BDArmory_Armorcockpit_On");//"Add Armored Cockpit"
                    }
                    else
                    {
                        Events["TogglecockpitArmor"].guiName = StringUtils.Localize("#LOC_BDArmory_Armorcockpit_Off");//"Remove Armored Cockpit"
                        ArmorMass = 0.2f * part.CrewCapacity;
                    }
                }
                else part.RemoveModule(this); //don't assign to drone cores
            }
            else
            {
                fuel = part.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
                monoprop = part.Resources.Where(pr => pr.resourceName == "MonoPropellant").FirstOrDefault();
                solid = part.Resources.Where(pr => pr.resourceName == "SolidFuel").FirstOrDefault();

                engine = part.FindModuleImplementing<ModuleEngines>();
                if (engine != null)
                {
                    Events["ToggleTankOption"].guiActiveEditor = false;
                    Events["ToggleInertOption"].guiActiveEditor = false;
                    if (solid != null && engine.throttleLocked && !engine.allowShutdown) //SRB?
                    {
                        if (fuel == null && monoprop == null || ((fuel != null && solid.maxAmount > fuel.maxAmount) || (monoprop != null && solid.maxAmount > monoprop.maxAmount)))
                        {
                            part.RemoveModule(this); //don't add firebottles to SRBs, but allow for the S1.5.5 MH soyuz tank with integrated seperatrons
                        }
                        else
                        {
                            Events["ToggleTankOption"].guiActiveEditor = true; //tank with integrated seperatrons?
                            Events["ToggleInertOption"].guiActiveEditor = true;
                            InertTank = false;
                        }
                    }
                }
                else if (monoprop != null)
                {
                    Events["ToggleInertOption"].guiActiveEditor = false; //inerting isn't going to do anything against a substance that contains its own oxidizer
                }
                else if (fuel == null && monoprop == null && solid == null)
                {
                    Events["ToggleTankOption"].guiActiveEditor = false;
                    Events["ToggleInertOption"].guiActiveEditor = false;
                    Fields["FireBottles"].guiActiveEditor = false;
                    Fields["FBRemaining"].guiActive = false;
                    Fields["partmass"].guiActiveEditor = false;
                    FireBottles = 0;
                }
            }
            if (!SSTank)
            {
                Events["ToggleTankOption"].guiName = StringUtils.Localize("#LOC_BDArmory_SSTank_On");//"Enable self-sealing tank"
            }
            else
            {
                Events["ToggleTankOption"].guiName = StringUtils.Localize("#LOC_BDArmory_SSTank_Off");//"Disable self-sealing tank"
            }
            if (!InertTank)
            {
                Events["ToggleInertOption"].guiName = StringUtils.Localize("#LOC_BDArmory_FIS_On");//"Enable self-sealing tank"
                FISmass = 0;
            }
            else
            {
                Events["ToggleInertOption"].guiName = StringUtils.Localize("#LOC_BDArmory_FIS_Off");//"Disable self-sealing tank"
                FISmass = 0.15f;
                Fields["FireBottles"].guiActiveEditor = false;
                Fields["FBRemaining"].guiActive = false;
            }
            GUIUtils.RefreshAssociatedWindows(part);
            partmass = (FISmass + ArmorMass + FBmass);
            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (cockpit == null && engine == null && (fuel == null && monoprop == null)) part.RemoveModule(this); //PWing with no tank
            }
            FBSetup(null, null);
            //Debug.Log("[BDArmory.SelfSealingTank]: SST: " + SSTank + "; Inerting: " + InertTank + "; armored cockpit: " + armoredCockpit);
        }
        /*
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;

            if (part.partInfo != null)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    FBSetup(null, null);
                }
                else
                {
                    if (part.vessel != null)
                    {
                        FBSetup(null, null);
                        var SSTString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "ModuleSelfSealingTank", "SSTank");
                        if (!string.IsNullOrEmpty(SSTString))
                        {
                            try
                            {
                                SSTank = bool.Parse(SSTString);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.ModuleSelfSealingTank]: Exception parsing SSTank: " + e.Message);
                            }
                        }
                        else
                        {
                            SSTank = false;
                        }
                        var InertString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "ModuleSelfSealingTank", "InertTank");
                        if (!string.IsNullOrEmpty(InertString))
                        {
                            try
                            {
                                InertTank = bool.Parse(InertString);
                                FISmass = InertTank ? 0.15f : 0;
                                //partmass += FISmass;
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.ModuleSelfSealingTank]: Exception parsing InertTank: " + e.Message);
                            }
                        }
                        else
                        {
                            InertTank = false;
                            FISmass = 0;
                        }
                        var cockpitString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "ModuleSelfSealingTank", "armoredCockpit");
                        if (!string.IsNullOrEmpty(cockpitString))
                        {
                            try
                            {
                                armoredCockpit = bool.Parse(InertString);
                                ArmorMass = armoredCockpit ? 0.2f : 0;
                                //partmass += ArmorMass;
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.ModuleSelfSealingTank]: Exception parsing armoredCockpit: " + e.Message);
                            }
                        }
                        else
                        {
                            armoredCockpit = false;
                            ArmorMass = 0;
                        }
                        if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                    }
                    else
                    {
                        enabled = false;
                    }
                }
            }
        }
        */
        void FBSetup(BaseField field, object obj)
        {
            if (externallyCalled) return;

            FBmass = (0.01f * FireBottles);
            FBRemaining = FireBottles;
            partmass = FBmass + FISmass + ArmorMass;
            //part.transform.localScale = (Vector3.one * (origScale + (CASELevel/10)));
            //Debug.Log("[BDArmory.ModuleCASE] part.mass = " + part.mass + "; CASElevel = " + CASELevel + "; CASEMass = " + CASEmass + "; Scale = " + part.transform.localScale);

            using (List<Part>.Enumerator pSym = part.symmetryCounterparts.GetEnumerator())
                while (pSym.MoveNext())
                {
                    if (pSym.Current == null) continue;

                    var tank = pSym.Current.FindModuleImplementing<ModuleSelfSealingTank>();
                    if (tank == null) continue;
                    tank.externallyCalled = true;
                    tank.FBmass = FBmass;
                    tank.FBRemaining = FBRemaining;
                    tank.partmass = partmass + FISmass + ArmorMass;
                    tank.externallyCalled = false;
                    GUIUtils.RefreshAssociatedWindows(pSym.Current);
                }
            GUIUtils.RefreshAssociatedWindows(part);
        }

        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.AppendLine($" Can outfit part with Fire Suppression Systems."); //localize this at some point, future me
            var engine = part.FindModuleImplementing<ModuleEngines>();
            if (engine == null)
            {
                output.AppendLine($" Can upgrade to Self-Sealing Tank.");
            }
            output.AppendLine("");

            return output.ToString();
        }
        public void Extinguishtank()
        {
            isOnFire = true;
            if (FireBottles > 0 || InertTank) //shouldn't be catching fire in the first place if interted, but just in case
            {
                //if (engine != null && engine.EngineIgnited && engine.allowRestart)
                //{
                //    engine.Shutdown();
                //    enginerestartTime = Time.time;
                //}
                if (firebottleRoutine == null)
                {
                    if (InertTank)
                    {
                        firebottleRoutine = StartCoroutine(ExtinguishRoutine(0, false));
                    }
                    else
                    {
                        firebottleRoutine = StartCoroutine(ExtinguishRoutine(4, true));
                    }
                    //Debug.Log("[BDArmory.SelfSealingTank]: Fire detected; beginning ExtinguishRoutine. Firebottles remaining: " + FireBottles);
                }
            }
            else
            {
                if (engine != null && engine.EngineIgnited && engine.allowRestart)
                {
                    if (part.vessel.verticalSpeed < 30) //not diving/trying to climb. With the vessel registry, could also grab AI state to add a !evading check
                    {
                        engine.Shutdown();
                        enginerestartTime = Time.time + 10;
                        if (firebottleRoutine == null)
                        {
                            firebottleRoutine = StartCoroutine(ExtinguishRoutine(10, false));
                            //Debug.Log("[BDArmory.SelfSealingTank]: Fire detected; beginning ExtinguishRoutine. Toggling Engine");
                        }
                    }
                    //though if it is diving, then there isn't a second call to cycle engines. Add an Ienumerator to check once every couple sec?
                }
            }
        }
        IEnumerator ExtinguishRoutine(float time, bool useBottle)
        {
            //Debug.Log("[BDArmory.SelfSealingTank]: ExtinguishRoutine started. Time left: " + time);
            yield return new WaitForSecondsFixed(time);
            //Debug.Log("[BDArmory.SelfSealingTank]: Timer finished. Extinguishing");
            foreach (var existingFire in part.GetComponentsInChildren<FireFX>())
            {
                if (!existingFire.surfaceFire) existingFire.burnTime = 0.05f; //kill all fires
            }
            if (useBottle)
            {
                FireBottles--;
                FBRemaining = FireBottles;
                GUIUtils.RefreshAssociatedWindows(part);
                //Debug.Log("[BDArmory.SelfSealingTank]: Consuming firebottle. FB remaining: " + FireBottles);
                isOnFire = false;
            }
            ResetCoroutine();
        }
        private void ResetCoroutine()
        {
            if (firebottleRoutine != null)
            {
                StopCoroutine(firebottleRoutine);
                firebottleRoutine = null;
            }
        }
        private float updateTimer = 0;
        void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (procPart)
                {
                    updateTimer -= Time.deltaTime;
                    if (updateTimer < 0)
                    {
                        fuel = part.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
                        monoprop = part.Resources.Where(pr => pr.resourceName == "MonoPropellant").FirstOrDefault();
                        if (fuel != null || monoprop != null)
                        {
                            Events["ToggleTankOption"].guiActiveEditor = true;
                            Events["ToggleInertOption"].guiActiveEditor = fuel != null; //I don't think inerting would work on something containing its own oxidizer...
                            if (InertTank && !Events["ToggleInertOption"].guiActiveEditor) ToggleInertOption(); // If inerting was somehow enabled previously, but is now not valid, disable it.
                            if (!InertTank)
                            {
                                Fields["FireBottles"].guiActiveEditor = true;
                                Fields["FBRemaining"].guiActive = true;
                            }
                            else
                            {
                                Fields["FireBottles"].guiActiveEditor = false;
                                Fields["FBRemaining"].guiActive = false;
                            }
                            Fields["partmass"].guiActiveEditor = true;
                        }
                        else
                        {
                            Events["ToggleTankOption"].guiActiveEditor = false;
                            Events["ToggleInertOption"].guiActiveEditor = false;
                            Fields["FireBottles"].guiActiveEditor = false;
                            Fields["FBRemaining"].guiActive = false;
                            Fields["partmass"].guiActiveEditor = false;
                            InertTank = false;
                            FireBottles = 0;
                            FBmass = 0;
                            FBRemaining = 0;
                        }
                        updateTimer = 0.5f; //doing it this way since PAW buttons don't seem to trigger onShipModified
                    }
                }
            }
        }
        void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || BDArmorySetup.GameIsPaused) return; // Not in flight scene, not ready or paused.
            if (vessel == null || vessel.packed || part == null) return; // Vessel or part is dead or packed.
            if (!BDArmorySettings.BATTLEDAMAGE || BDArmorySettings.PEACE_MODE) return;
            if (!BDArmorySettings.BD_FIRES_ENABLED || !BDArmorySettings.BD_FIRE_HEATDMG) return; // Disabled.

            if (BDArmorySettings.BD_FIRES_ENABLED && BDArmorySettings.BD_FIRE_HEATDMG)
            {
                if (InertTank) return;
                if (!isOnFire)
                {
                    if (((fuel != null && fuel.amount > 0) || (monoprop != null && monoprop.amount > 0)) && part.temperature > 493) //autoignition temp of kerosene is 220 c. hydrazine is 24-270, so this works for monoprop as well
                    {
                        string fireStarter;
                        var vesselFire = part.vessel.GetComponentInChildren<FireFX>();
                        if (vesselFire != null)
                        {
                            fireStarter = vesselFire.SourceVessel;
                        }
                        else
                        {
                            fireStarter = part.vessel.GetName();
                        }
                        BulletHitFX.AttachFire(transform.position, part, 50, fireStarter);
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDarmory.SelfSealingTank]: Fuel auto-ignition! " + part.name + " is on fire! Fuel quantity: " + fuel.amount + "; temperature: " + part.temperature);
                        Extinguishtank();
                        isOnFire = true;
                    }
                }
                if (engine != null)
                {
                    if (enginerestartTime > 0 && (Time.time > enginerestartTime))
                    {
                        enginerestartTime = -1;
                        engine.Activate();
                    }
                }
            }
        }
    }
}
