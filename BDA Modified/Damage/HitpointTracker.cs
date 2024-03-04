﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using BDArmory.Armor;
using BDArmory.Extensions;
using BDArmory.Modules;
using BDArmory.Settings;
using BDArmory.Utils;

namespace BDArmory.Damage
{
    public class HitpointTracker : PartModule, IPartMassModifier, IPartCostModifier
    {
        #region KSP Fields
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => armorMass + HullMassAdjust;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => armorCost + HullCostAdjust;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_Hitpoints"),//Hitpoints
        UI_ProgressBar(affectSymCounterparts = UI_Scene.None, controlEnabled = false, scene = UI_Scene.All, maxValue = 100000, minValue = 0, requireFullControl = false)]
        public float Hitpoints;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorThickness"),//Armor Thickness
        UI_FloatRange(minValue = 0f, maxValue = 200, stepIncrement = 1f, scene = UI_Scene.All)]
        public float Armor = -1f; //settable Armor thickness availible for editing in the SPH?VAB

        [KSPField(advancedTweakable = true, guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_ArmorThickness")]//armor Thickness
        public float Armour = 10f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_ArmorRemaining"),//Armor intregity
        UI_ProgressBar(affectSymCounterparts = UI_Scene.None, controlEnabled = false, scene = UI_Scene.Flight, maxValue = 100, minValue = 0, requireFullControl = false)]
        public float ArmorRemaining = 100;

        public float StartingArmor;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_Armor_ArmorType"),//Armor Types
        UI_FloatRange(minValue = 1, maxValue = 999, stepIncrement = 1, scene = UI_Scene.All)]
        public float ArmorTypeNum = 1; //replace with prev/next buttons? //or a popup GUI box with a list of selectable types...

        //Add a part material type setting, so parts can be selected to be made out of wood/aluminium/steel to adjust base partmass/HP?
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_Armor_HullType"),//hull material Types
        UI_FloatRange(minValue = 1, maxValue = 3, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float HullTypeNum = 2;
        private float OldHullType = -1;

        [KSPField(isPersistant = true)]
        public string hullType = "Aluminium";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_Armor_HullMat")]//Status
        public string guiHullTypeString;

        public float HullMassAdjust = 0f;
        public float HullCostAdjust = 0f;
        double resourceCost = 0;

        private bool IgnoreForArmorSetup = false;

        private bool isAI = false;

        private bool isProcWing = false;
        private bool isProcPart = false;
        private bool isProcWheel = false;
        private bool waitingForHullSetup = false;
        private float OldArmorType = -1;

        [KSPField(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorMass")]//armor mass
        public float armorMass = 0f;

        private float totalArmorQty = 0f;

        [KSPField(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorCost")]//armor cost
        public float armorCost = 0f;

        [KSPField(isPersistant = true)]
        public string SelectedArmorType = "None"; //presumably Aubranium can use this to filter allowed/banned types

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorCurrent")]//Status
        public string guiArmorTypeString = "def";

        private ArmorInfo armorInfo;
        private HullInfo hullInfo;

        private bool armorReset = false;

        [KSPField(isPersistant = true)]
        public float maxHitPoints = -1f;

        [KSPField(isPersistant = true)]
        public float ArmorThickness = -1f;

        [KSPField(isPersistant = true)]
        public bool ArmorSet;

        [KSPField(isPersistant = true)]
        public string ExplodeMode = "Never";

        [KSPField(isPersistant = true)]
        public bool FireFX = true;

        [KSPField(isPersistant = true)]
        public float FireFXLifeTimeInSeconds = 5f;

        //Armor Vars
        [KSPField(isPersistant = true)]
        public float Density;
        [KSPField(isPersistant = true)]
        public float Diffusivity;
        [KSPField(isPersistant = true)]
        public float Ductility;
        [KSPField(isPersistant = true)]
        public float Hardness;
        [KSPField(isPersistant = true)]
        public float Strength;
        [KSPField(isPersistant = true)]
        public float SafeUseTemp;
        [KSPField(isPersistant = true)]
        public float radarReflectivity;
        [KSPField(isPersistant = true)]
        public float Cost;

        [KSPField(isPersistant = true)]
        public float vFactor;
        [KSPField(isPersistant = true)]
        public float muParam1;
        [KSPField(isPersistant = true)]
        public float muParam2;
        [KSPField(isPersistant = true)]
        public float muParam3;
        [KSPField(isPersistant = true)]
        public float muParam1S;
        [KSPField(isPersistant = true)]
        public float muParam2S;
        [KSPField(isPersistant = true)]
        public float muParam3S;

        [KSPField(isPersistant = true)]
        public float HEEquiv;
        [KSPField(isPersistant = true)]
        public float HEATEquiv;

        [KSPField(isPersistant = true)]
        public float maxForce;
        [KSPField(isPersistant = true)]
        public float maxTorque;
        [KSPField(isPersistant = true)]
        public double maxG;

        private bool startsArmored = false;
        public bool ArmorPanel = false;

        //Part vars
        private float partMass = 0f;
        public Vector3 partSize;
        [KSPField(isPersistant = true)]
        public float maxSupportedArmor = -1; //upper cap on armor per part, overridable in MM/.cfg
        [KSPField(isPersistant = true)]
        public float armorVolume = -1;
        private float sizeAdjust;

        AttachNode bottom;
        AttachNode top;

        private float hullRadarReturnFactor = 1;
        private float armorRadarReturnFactor = 1;

        public Dictionary<int, Shader> defaultShader = new Dictionary<int, Shader>();
        public Dictionary<int, Color> defaultColor = new Dictionary<int, Color>();
        public bool RegisterProcWingShader = false;

        public float defenseMutator = 1;

        #endregion KSP Fields

        #region Heart Bleed
        private double nextHeartBleedTime = 0;
        #endregion Heart Bleed

        private readonly float hitpointMultiplier = BDArmorySettings.HITPOINT_MULTIPLIER;

        private float previousHitpoints = -1;
        private bool previousEdgeLift = false;
        private bool _updateHitpoints = false;
        private bool _forceUpdateHitpointsUI = false;
        private const int HpRounding = 25;
        private bool _updateMass = false;
        private bool _armorModified = false;
        private bool _hullModified = false;
        private bool _armorConfigured = false;
        private bool _hullConfigured = false;
        private bool _hpConfigured = false;
        private bool _finished_setting_up = false;
        public bool Ready => (_finished_setting_up || !HighLogic.LoadedSceneIsFlight) && _hpConfigured && _hullConfigured && _armorConfigured;
        public string Why
        {
            get
            {
                if (Ready) return "Ready";
                else
                {
                    List<string> reasons = new List<string>();
                    if (!_finished_setting_up && HighLogic.LoadedSceneIsFlight) reasons.Add("still setting up");
                    if (!_hpConfigured) reasons.Add("HP not configured");
                    if (!_hullConfigured) reasons.Add("hull not configured");
                    if (!_armorConfigured) reasons.Add("armor not configured");
                    return string.Join(", ", reasons);
                }
            }
        }

        public bool isOnFire = false;

        [KSPField(isPersistant = true)]
        public float ignitionTemp = -1;
        private double skinskinConduction = 1;
        private double skinInternalConduction = 1;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;

            if (part.partInfo == null)
            {
                // Loading of the prefab from the part config
                _updateHitpoints = true;
            }
            else
            {
                // Loading of the part from a saved craft
                if (HighLogic.LoadedSceneIsEditor)
                {
                    _updateHitpoints = true;
                    ArmorSet = false;
                }
                else // Loading of the part from a craft in flight mode
                {
                    if (BDArmorySettings.RESET_HP && part.vessel != null) // Reset Max HP
                    {
                        var maxHPString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "HitpointTracker", "maxHitPoints");
                        if (!string.IsNullOrEmpty(maxHPString)) // Use the default value from the MM patch.
                        {
                            try
                            {
                                maxHitPoints = float.Parse(maxHPString);
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log("[BDArmory.HitpointTracker]: setting maxHitPoints of " + part + " on " + part.vessel.vesselName + " to " + maxHitPoints);
                                _updateHitpoints = true;
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.HitpointTracker]: Failed to parse maxHitPoints configNode: " + e.Message);
                            }
                        }
                        else // Use the stock default value.
                        {
                            maxHitPoints = -1f;
                        }
                    }
                    else // Don't.
                    {
                        // enabled = false; // We'll disable this later once things are set up.
                    }
                }
            }
        }

        public void SetupPrefab()
        {
            if (part != null)
            {
                ArmorRemaining = 100;
                var maxHitPoints_ = CalculateTotalHitpoints();

                if (!_forceUpdateHitpointsUI && previousHitpoints == maxHitPoints_) return;

                //Add Hitpoints
                if (!ArmorPanel)
                {
                    UI_ProgressBar damageFieldFlight = (UI_ProgressBar)Fields["Hitpoints"].uiControlFlight;
                    damageFieldFlight.maxValue = maxHitPoints_;
                    damageFieldFlight.minValue = 0f;
                    UI_ProgressBar damageFieldEditor = (UI_ProgressBar)Fields["Hitpoints"].uiControlEditor;
                    damageFieldEditor.maxValue = maxHitPoints_;
                    damageFieldEditor.minValue = 0f;
                }
                else
                {
                    Fields["Hitpoints"].guiActive = false;
                    Fields["Hitpoints"].guiActiveEditor = false;
                }
                Hitpoints = maxHitPoints_;
                if (!ArmorSet) overrideArmorSetFromConfig();

                previousHitpoints = maxHitPoints_;
                part.RefreshAssociatedWindows();
            }
            else
            {
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log("[BDArmory.HitpointTracker]: OnStart part is null");
            }
        }

        public override void OnStart(StartState state)
        {
            if (part == null) return;
            isEnabled = true;
            oldmaxHitpoints = maxHitPoints;
            if (part.name.Contains("B9.Aero.Wing.Procedural"))
            {
                isProcWing = true;
            }
            if (part.name.Contains("procedural"))
            {
                isProcPart = true;
            }
            if (part.Modules.Contains("KSPWheelBase"))
            {
                isProcWheel = true;
            }
            StartingArmor = Armor;
            if (ProjectileUtils.IsArmorPart(this.part))
            {
                ArmorPanel = true;
            }
            else
            {
                ArmorPanel = false;
            }
            if (!((HullTypeNum == 1 || HullTypeNum == 3) && hullType == "Aluminium")) //catch for legacy .craft files
            {
                HullTypeNum = HullInfo.materials.FindIndex(t => t.name == hullType) + 1;
            }
            if (SelectedArmorType == "Legacy Armor")
                ArmorTypeNum = ArmorInfo.armors.FindIndex(t => t.name == "None");
            else
                ArmorTypeNum = ArmorInfo.armors.FindIndex(t => t.name == SelectedArmorType) + 1;
            guiArmorTypeString = SelectedArmorType;
            guiHullTypeString = StringUtils.Localize(HullInfo.materials[HullInfo.materialNames[(int)HullTypeNum - 1]].localizedName);

            if (part.partInfo != null && part.partInfo.partPrefab != null) // PotatoRoid, I'm looking at you.
            {
                skinskinConduction = part.partInfo.partPrefab.skinSkinConductionMult;
                skinInternalConduction = part.partInfo.partPrefab.skinSkinConductionMult;
            }
            if (ArmorThickness < 0) ArmorThickness = part.IsMissile() ? 2 : 10;
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (BDArmorySettings.RESET_ARMOUR)
                {
                    ArmorSetup(null, null);
                }
                if (BDArmorySettings.RESET_HULL || ArmorPanel)
                {
                    IgnoreForArmorSetup = true;
                    HullTypeNum = HullInfo.materials.FindIndex(t => t.name == "Aluminium") + 1;
                }
                SetHullMass();
                part.RefreshAssociatedWindows();
            }
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                int armorCount = 0;
                for (int i = 0; i < ArmorInfo.armorNames.Count; i++)
                {
                    armorCount++;
                }
                UI_FloatRange ATrangeEditor = (UI_FloatRange)Fields["ArmorTypeNum"].uiControlEditor;
                ATrangeEditor.onFieldChanged = ArmorModified;
                ATrangeEditor.maxValue = (float)armorCount;
                int hullCount = 0;
                for (int i = 0; i < HullInfo.materialNames.Count; i++)
                {
                    hullCount++;
                }
                UI_FloatRange HTrangeEditor = (UI_FloatRange)Fields["HullTypeNum"].uiControlEditor;
                HTrangeEditor.onFieldChanged = HullModified;
                HTrangeEditor.maxValue = (float)hullCount;
                if (ProjectileUtils.IsIgnoredPart(this.part))
                {
                    isAI = true;
                    Fields["ArmorTypeNum"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActive = false;
                    Fields["guiHullTypeString"].guiActiveEditor = false;
                    Fields["guiHullTypeString"].guiActive = false;
                    Fields["armorCost"].guiActiveEditor = false;
                    Fields["armorMass"].guiActiveEditor = false;
                    //UI_ProgressBar Armorleft = (UI_ProgressBar)Fields["ArmorRemaining"].uiControlFlight;
                    //Armorleft.scene = UI_Scene.None;
                }
                if (part.IsMissile())
                {
                    Fields["ArmorTypeNum"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActiveEditor = false;
                    Fields["armorCost"].guiActiveEditor = false;
                    Fields["armorMass"].guiActiveEditor = false;
                }
                if (isAI || part.IsMissile())
                {
                    Fields["ArmorTypeNum"].guiActiveEditor = false;
                    ATrangeEditor.maxValue = 1;
                }
                if (BDArmorySettings.LEGACY_ARMOR || BDArmorySettings.RESET_ARMOUR)
                {
                    Fields["ArmorTypeNum"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActive = false;
                    Fields["armorCost"].guiActiveEditor = false;
                    Fields["armorMass"].guiActiveEditor = false;
                    ATrangeEditor.maxValue = 1;
                }

                //if part is an engine/fueltank don't allow wood construction/mass reduction
                if (part.IsMissile() || part.IsWeapon() || ArmorPanel || isAI || BDArmorySettings.LEGACY_ARMOR || BDArmorySettings.RESET_HULL || ProjectileUtils.isMaterialBlackListpart(this.part))
                {
                    HullTypeNum = HullInfo.materials.FindIndex(t => t.name == "Aluminium") + 1;
                    HTrangeEditor.minValue = HullTypeNum;
                    HTrangeEditor.maxValue = HullTypeNum;
                    Fields["HullTypeNum"].guiActiveEditor = false;
                    Fields["HullTypeNum"].guiActive = false;
                    Fields["guiHullTypeString"].guiActiveEditor = false;
                    Fields["guiHullTypeString"].guiActive = false;
                    IgnoreForArmorSetup = true;
                    SetHullMass();
                }

                if (ArmorThickness > 10 || ArmorPanel) //Mod part set to start with armor, or armor panel. > 10, since less than 10mm of armor can't be considered 'startsArmored'
                {
                    startsArmored = true;
                    if (Armor < 0) // armor amount modified in SPH/VAB and does not = either the default nor the .cfg thickness
                        Armor = ArmorThickness;//set Armor amount to .cfg value
                    //See also ln 1183-1186
                }
                else
                {
                    if (Armor < 0) Armor = ArmorThickness; //10 for parts, 2 for missiles, from ln 347
                    Fields["Armor"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActive = false;
                    Fields["armorCost"].guiActiveEditor = false;
                    Fields["armorMass"].guiActiveEditor = false;
                }
            }
            GameEvents.onEditorShipModified.Add(ShipModified);
            GameEvents.onPartDie.Add(OnPartDie);
            bottom = part.FindAttachNode("bottom");
            top = part.FindAttachNode("top");
            //if (armorVolume < 0) //check already occurs 429, doubling it results in the PartSize vector3 returning null
            calcPartSize();
            SetupPrefab();
            Armour = Armor;
            StartCoroutine(DelayedOnStart()); // Delay updating mass, armour, hull and HP so mods like proc wings and tweakscale get the right values.
                                              //if (HighLogic.LoadedSceneIsFlight)
                                              //{
                                              //if (BDArmorySettings.DEBUG_ARMOR) 
                                              //Debug.Log("[BDArmory.HitpointTracker]: ARMOR: part mass is: " + (part.mass - armorMass) + "; Armor mass is: " + armorMass + "; hull mass adjust: " + HullMassAdjust + "; total: " + part.mass);
                                              //}
            CalculateDryCost();
        }

        void calcPartSize()
        {
            partSize = Vector3.zero;
            int topSize = 0;
            int bottomSize = 0;
            try
            {
                if (top != null)
                {
                    topSize = top.size;
                }
                if (bottom != null)
                {
                    bottomSize = bottom.size;
                }
            }
            catch
            {
                Debug.Log("[BDArmoryHitpointTracker]: no node size detected");
            }
            //if attachnode top != bottom, then cone. is nodesize Attachnode.radius or Attachnode.size?
            //getSize returns size of a rectangular prism; most parts are circular, some are conical; use sizeAdjust to compensate
            partSize = CalcPartBounds(this.part, this.transform).size;
            if (bottom != null && top != null) //cylinder
            {
                sizeAdjust = 0.783f;
            }
            else if ((bottom == null && top != null) || (bottom != null && top == null) || (topSize > bottomSize || bottomSize > topSize)) //cone
            {
                sizeAdjust = 0.422f;
            }
            else //no bottom or top nodes, assume srf attached part; these are usually panels of some sort. Will need to determine method of ID'ing triangular panels/wings
            {
                //Wings at least could use WingLiftArea as a workaround for approx. surface area...
                if (part.IsAero())
                {
                    if (!isProcWing) //procWings handled elsewhere
                    {
                        if (!FerramAerospace.CheckForFAR())
                        {
                            if ((float)part.Modules.GetModule<ModuleLiftingSurface>().deflectionLiftCoeff < (Mathf.Max(partSize.x, partSize.y) * Mathf.Max(partSize.y, partSize.z) / 3.52f))
                            {
                                sizeAdjust = 0.5f; //wing is triangular
                            }
                        }
                        else
                        {
                            if (FerramAerospace.GetFARWingSweep(part) > 0) sizeAdjust = 0.5f; //wing isn't rectangular
                        }
                    }
                }
                else
                    sizeAdjust = 0.5f; //armor on one side, otherwise will have armor thickness on both sides of the panel, nonsensical + double weight
            }
            if (armorVolume < 0 || HighLogic.LoadedSceneIsEditor && isProcPart) //make this persistant to get around diffeences in part bounds between SPH/Flight. Also reset if in editor and a procpart to account for resizing
            {
                armorVolume =  // thickness * armor mass; moving it to Start since it only needs to be calc'd once
                    ((((partSize.x * partSize.y) * 2) + ((partSize.x * partSize.z) * 2) + ((partSize.y * partSize.z) * 2)) * sizeAdjust);  //mass * surface area approximation of a cylinder, where H/W are unknown
                if (HighLogic.LoadedSceneIsFlight) //Value correction for loading legacy craft via VesselMover spawner/tournament autospawn that haven't got a armorvolume value in their .craft file.
                {
                    armorVolume *= 0.63f; //part bounds dimensions when calced in Flight are consistantly 1.6-1.7x larger than correct SPH dimensions. Won't be exact, but good enough for legacy craft support
                }
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log("[BDArmory.HitpointTracker]: ARMOR: part size is (X: " + partSize.x + ";, Y: " + partSize.y + "; Z: " + partSize.z);
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log("[BDArmory.HitpointTracker]: ARMOR: size adjust mult: " + sizeAdjust + "; part srf area: " + armorVolume);
            }
        }

        IEnumerator DelayedOnStart()
        {
            yield return new WaitForFixedUpdate();
            if (part == null) yield break;
            if (part.GetComponent<ModuleAsteroid>())
            {
                var tic = Time.time;
                yield return new WaitUntilFixed(() => part == null || part.mass > 0 || Time.time - tic > 5); // Give it 5s to get the part info.
                if (part != null)
                {
                    partMass = part.mass;
                    calcPartSize(); // Re-calculate the size.
                    SetupPrefab(); // Re-setup the prefab.
                }
            }
            if (!isProcWing) //moving this here so any dynamic texture adjustment post spawn (TURD/TUFX/etc) will be grabbed by the defaultShader census
            {
                var r = part.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < r.Length; i++)
                {
                    if (r[i].GetComponentInParent<Part>() != part) continue; // Don't recurse to child parts.
                    int key = r[i].material.GetInstanceID(); // The instance ID is unique for each object (not just component or gameObject).
                    defaultShader.Add(key, r[i].material.shader);
                    if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: ARMOR: part shader on {r[i].GetComponentInParent<Part>().partInfo.name} is {r[i].material.shader.name}");
                    if (r[i].material.HasProperty("_Color"))
                    {
                        if (!defaultColor.ContainsKey(key)) defaultColor.Add(key, r[i].material.color);
                    }
                }
            }
            if (part.partInfo != null && part.partInfo.partPrefab != null) partMass = part.partInfo.partPrefab.mass;
            _updateMass = true;
            _armorModified = true;
            _hullModified = true;
            _updateHitpoints = true;
        }

        private void OnDestroy()
        {
            if (bottom != null) bottom = null;
            if (top != null) top = null;
            GameEvents.onEditorShipModified.Remove(ShipModified);
            GameEvents.onPartDie.Remove(OnPartDie);
        }

        void OnPartDie() { OnPartDie(part); }

        void OnPartDie(Part p)
        {
            if (p == part)
            {
                Destroy(this); // Force this module to be removed from the gameObject as something is holding onto part references and causing a memory leak.
            }
        }

        public void ShipModified(ShipConstruct data)
        {
            // Note: this triggers if the ship is modified, but really we only want to run this when the part is modified.
            if (isProcWing || isProcPart || isProcWheel)
            {
                if (!_delayedShipModifiedRunning)
                {
                    StartCoroutine(DelayedShipModified());
                    if (!part.name.Contains("B9.Aero.Wing.Procedural.Panel") && !previousEdgeLift) ProceduralWing.ResetPWing(part);
                    previousEdgeLift = true;
                }

            }
            else
            {
                _updateHitpoints = true;
                _updateMass = true;
            }
        }

        private bool _delayedShipModifiedRunning = false;
        IEnumerator DelayedShipModified() // Wait a frame before triggering to allow proc wings to update it's mass properly.
        {
            _delayedShipModifiedRunning = true;
            yield return new WaitForFixedUpdate();
            _delayedShipModifiedRunning = false;
            if (part == null) yield break;
            _updateHitpoints = true;
            _updateMass = true;
        }

        public void ArmorModified(BaseField field, object obj)
        {
            _armorModified = true;
            foreach (var p in part.symmetryCounterparts)
            {
                var hp = p.GetComponent<HitpointTracker>();
                if (hp == null) continue;
                hp._armorModified = true;
            }
        }
        public void HullModified(BaseField field, object obj)
        {
            _hullModified = true;
            foreach (var p in part.symmetryCounterparts)
            {
                var hp = p.GetComponent<HitpointTracker>();
                if (hp == null) continue;
                hp._hullModified = true;
            }
        }

        void Update()
        {
            if (_finished_setting_up) // Only gets set in flight mode.
            {
                RefreshHitPoints();
                return;
            }
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight) // Also needed in flight mode for initial setup of mass, hull and HP, but shouldn't be triggered afterwards as ShipModified is only for the editor.
            {
                if (_armorModified)
                {
                    _armorModified = false;
                    ArmorSetup(null, null);
                }
                if (_hullModified && !_updateMass) // Wait for the mass to update first.
                {
                    _hullModified = false;
                    HullSetup(null, null);
                }
                if (!_updateMass) // Wait for the mass to update first.
                    RefreshHitPoints();
                if (HighLogic.LoadedSceneIsFlight && _armorConfigured && _hullConfigured && _hpConfigured) // No more changes, we're done.
                {
                    _finished_setting_up = true;
                }
            }
        }

        void FixedUpdate()
        {
            if (_updateMass)
            {
                _updateMass = false;
                var oldPartMass = partMass;
                var oldHullMassAdjust = HullMassAdjust; // We need to temporarily remove the HullmassAdjust and update the part.mass to get the correct value as KSP clamps the mass to > 1e-4.
                HullMassAdjust = 0;
                part.UpdateMass();
                //partMass = part.mass - armorMass - HullMassAdjust; //part mass is taken from the part.cfg val, not current part mass; this overrides that
                //need to get ModuleSelfSealingTank mass adjustment. Could move the SST module to BDA.Core
                if (isProcWing || isProcPart || isProcWheel)
                {
                    float Safetymass = 0;
                    var SST = part.GetComponent<ModuleSelfSealingTank>();
                    if (SST != null)
                    { Safetymass = SST.FBmass + SST.FISmass; }
                    partMass = part.mass - armorMass - HullMassAdjust - Safetymass;
                }
                CalculateDryCost(); //recalc if modify event added a fueltank -resource swap, etc
                HullMassAdjust = oldHullMassAdjust; // Put the HullmassAdjust back so we can test against it when we update the hull mass.
                if (oldPartMass != partMass)
                {
                    if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} updated mass at {Time.time}: part.mass {part.mass}, partMass {oldPartMass}->{partMass}, armorMass {armorMass}, hullMassAdjust {HullMassAdjust}");
                    if (isProcPart || isProcWheel)
                    {
                        calcPartSize();
                        _armorModified = true;
                    }
                    _hullModified = true; // Modifying the mass modifies the hull.
                    _updateHitpoints = true;
                }
            }

            if (HighLogic.LoadedSceneIsFlight && !UI.BDArmorySetup.GameIsPaused)
            {
                if (BDArmorySettings.HEART_BLEED_ENABLED && ShouldHeartBleed())
                {
                    HeartBleed();
                }
                //if (ArmorTypeNum > 1 || ArmorPanel)
                if (ArmorTypeNum != (ArmorInfo.armors.FindIndex(t => t.name == "None") + 1) || ArmorPanel)
                {
                    if (part.skinTemperature > SafeUseTemp * 1.5f)
                    {
                        ReduceArmor((armorVolume * ((float)part.skinTemperature / SafeUseTemp)) * TimeWarp.fixedDeltaTime); //armor's melting off ship
                    }
                }
                if (!BDArmorySettings.BD_FIRES_ENABLED || !BDArmorySettings.BD_FIRE_HEATDMG) return; // Disabled.

                if (BDArmorySettings.BD_FIRES_ENABLED && BDArmorySettings.BD_FIRE_HEATDMG)
                {
                    if (!isOnFire)
                    {
                        if (ignitionTemp > 0 && part.temperature > ignitionTemp)
                        {
                            string fireStarter;
                            var vesselFire = part.vessel.GetComponentInChildren<FX.FireFX>();
                            if (vesselFire != null)
                            {
                                fireStarter = vesselFire.SourceVessel;
                            }
                            else
                            {
                                fireStarter = part.vessel.GetName();
                            }
                            FX.BulletHitFX.AttachFire(transform.position, part, 50, fireStarter);
                            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDarmory.HitPointTracker]: Hull auto-ignition! {part.name} is on fire!; temperature: {part.temperature}");
                            isOnFire = true;
                        }
                    }
                }
            }
        }
        private void RefreshHitPoints()
        {
            if (_updateHitpoints)
            {
                _updateHitpoints = false;
                _forceUpdateHitpointsUI = false;
                SetupPrefab();
            }
        }

        #region HeartBleed
        private bool ShouldHeartBleed()
        {
            // wait until "now" exceeds the "next tick" value
            double dTime = Planetarium.GetUniversalTime();
            if (dTime < nextHeartBleedTime)
            {
                //Debug.Log(string.Format("[BDArmory.HitpointTracker]: TimeSkip ShouldHeartBleed for {0} on {1}", part.name, part.vessel.vesselName));
                return false;
            }

            // assign next tick time
            double interval = BDArmorySettings.HEART_BLEED_INTERVAL;
            nextHeartBleedTime = dTime + interval;

            return true;
        }

        private void HeartBleed()
        {
            float rate = BDArmorySettings.HEART_BLEED_RATE;
            float deduction = Hitpoints * rate;
            if (Hitpoints - deduction < BDArmorySettings.HEART_BLEED_THRESHOLD)
            {
                // can't die from heart bleed
                return;
            }
            // deduct hp base on the rate
            //Debug.Log(string.Format("[BDArmory.HitpointTracker]: Heart bleed {0} on {1} by {2:#.##} ({3:#.##}%)", part.name, part.vessel.vesselName, deduction, rate*100.0));
            AddDamage(deduction);
        }
        #endregion

        #region Hitpoints Functions

        //[KSPField(isPersistant = true)]
        //public bool HPMode = false;
        float oldmaxHitpoints;
        /*
        [KSPEvent(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "Toggle HP Calc", active = true)]//Self-Sealing Tank
        public void ToggleHPOption()
        {
            HPMode = !HPMode;
            if (!HPMode)
            {
                Events["ToggleHPOption"].guiName = StringUtils.Localize("Revert to Legacy HP calc");
                maxHitPoints = oldmaxHitpoints;
            }
            else
            {
                Events["ToggleHPOption"].guiName = StringUtils.Localize("Test Refactored Calc");
                oldmaxHitpoints = maxHitPoints;
                maxHitPoints = -1;
            }
            SetupPrefab();
            GUIUtils.RefreshAssociatedWindows(part);
        }
        */
        public float CalculateTotalHitpoints()
        {
            float hitpoints;// = -1;

            if (!part.IsMissile())
            {
                if (!ArmorPanel)
                {
                    if (maxHitPoints <= 0)
                    {
                        bool clampHP = false;
                        float structuralMass = 100;
                        float structuralVolume = 1;
                        float density = 1;
                        //if (!HPMode)
                        {
                            var averageSize = part.GetAverageBoundSize();
                            var sphereRadius = averageSize * 0.5f;
                            var sphereSurface = 4 * Mathf.PI * sphereRadius * sphereRadius;
                            var thickness = 0.1f;// * part.GetTweakScaleMultiplier(); // Tweakscale scales mass as r^3 insted of 0.1*r^2, however it doesn't take the increased volume of the hull into account when scaling resource amounts.
                            structuralVolume = Mathf.Max(sphereSurface * thickness, 1e-3f); // Prevent 0 volume, just in case. structural volume is 10cm * surface area of equivalent sphere.
                                                                                            //bool clampHP = false;

                            density = (partMass * 1000f) / structuralVolume;
                            if (density > 1e5f || density < 10)
                            {
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} extreme density detected: {density}! Trying alternate approach based on partSize.");
                                //structuralVolume = (partSize.x * partSize.y + partSize.x * partSize.z + partSize.y * partSize.z) * 2f * sizeAdjust * Mathf.PI / 6f * 0.1f; // Box area * sphere/cube ratio * 10cm. We use sphere/cube ratio to get similar results as part.GetAverageBoundSize().
                                structuralVolume = armorVolume * Mathf.PI / 6f * 0.1f; //part bounds change between editor and flight, so use existing persistant size value
                                density = (partMass * 1000f) / structuralVolume;
                                if (density > 1e5f || density < 10)
                                {
                                    if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} still has extreme density: {density}! Setting HP based only on mass instead.");
                                    clampHP = true;
                                }
                            }
                            density = Mathf.Clamp(density, 1000, 10000);
                            //if (BDArmorySettings.DEBUG_LABELS)
                            //Debug.Log("[BDArmory.HitpointTracker]: Hitpoint Calc" + part.name + " | structuralVolume : " + structuralVolume);
                            // if (BDArmorySettings.DEBUG_LABELS) Debug.Log("[BDArmory.HitpointTracker]: Hitpoint Calc" + part.name + " | Density : " + density);

                            structuralMass = density * structuralVolume; //this just means hp = mass if the density is within the limits.

                            //bigger things need more hp; but things that are denser, should also have more hp, so it's a bit more complicated than have hp = volume * hp mult
                            //hp = (volume * Hp mult) * density mod?
                            //lets take some examples; 3 identical size parts, mk1 cockpit(930kg), mk1 stuct tube (100kg), mk1 LF tank (250kg)
                            //if, say, a Hp mod of 300, so 2.55m3 * 300 = 765 -> 800hp
                            //cockpit has a density of ~364, fueltank of 98, struct tube of 39
                            //density can't be linear scalar. Cuberoot? would need to reduce hp mult.
                            //2.55 * 100* 364^1/3 = 1785, 2.55 * 100 * 98^1/3 = 1157, 2.55 * 100 * 39^1/3 = 854

                            // if (BDArmorySettings.DEBUG_LABELS) Debug.Log("[BDArmory.HitpointTracker]: " + part.name + " structural Volume: " + structuralVolume + "; density: " + density);
                            //3. final calculations
                            hitpoints = structuralMass * hitpointMultiplier * 0.333f;

                        }
                        /*
                        else //revised HP calc, commented out for now until we get feedback on new method and decide to switch over
                        {
                            //var averageSize = part.GetVolume(); // this grabs x/y/z dimensions from PartExtensions.cs 
                            var averageSize = partSize.x * partSize.y * partSize.z;
                            structuralVolume = averageSize * sizeAdjust; //a cylinder diameter X length y is ~78.5% the volume of a rectangle of h/w x, length y. 
                                                                         //(mk2 parts are ~66% volume of equivilent rectangle, but are reinforced hulls, so..
                                                                         //cones are ~36-37% volume
                                                                         //parts that aren't cylinders or close enough and need exceptions: Wings, control surfaces, radiators/solar panels
                                                                         //var dryPartmass = part.mass - part.resourceMass;
                            var dryPartmass = part.mass;
                            density = (dryPartmass * 1000) / structuralVolume;
                            //var structuralMass = density * structuralVolume; // this means HP is solely determined my part mass, after assuming all parts have min density of 1000kg/m3
                            //Debug.Log("[BDArmory]: Hitpoint Calc" + part.name + " | structuralVolume : " + structuralVolume);

                            if (!part.IsAero() && !isProcPart && !isProcWing)
                            {
                                if (part.IsMotor())
                                {
                                    //hitpoints = ((dryPartmass * density) * 4) * hitpointMultiplier * 0.33f; // engines in KSP are very dense - leads to massive HP due to large mass, small volume. Engines also don't respond well to being shot, so...
                                    //juno vol: 0.105, density: 2370;      Ideal HP: ~300?
                                    //wheesley: 0.843, 1777;                        ~1000
                                    //panther: 1.181, 1015;                         ~800 //low-bypass turbofans are going to be denser, have more of their volume susseptable to damage
                                    //goliath: 16.38?, 274                          ~2000? //massive turbofans would be less vulnerable to lead injestion, depending on how hardened the engine is against birdstrikes/FOD; they're also something like 50% open space
                                    //hitpoints = structuralVolume * 100 * Mathf.Pow(density, 1/3) * hitpointMultiplier * 0.33f;
                                    //gives 150 for the juno, 1025 for the wheesley, 1200 for the panther, 10625/goliath
                                    //(drymass + volume) * (density / 2)?
                                    //Juno - 420; wheesley: 2100; panther: 1225; goliath: 2875
                                    //volume * density
                                    //...that's just HP = partmass
                                    //that said, that could work... Juno: 250HP; wheesley: 1500HP; panther; 1200HP; goliath: 4500 HP; M3X Wyvern: 8000 HP; those numbers *do* look reasonable for engines...
                                    //whiplash/rapier would be 1.8/2k HP, which is pushing it a bit... look into a clamp of some sort
                                    //Rapier vol/density is ~0.92, 2171. clamp density to partmass? 2000?
                                    //volume * mathf.clamp(density, 100, 1750) ?
                                    hitpoints = structuralVolume * Mathf.Clamp(density, 100, 1750) * hitpointMultiplier * 0.33f;
                                    if (hitpoints > (dryPartmass * 2000) || hitpoints < (dryPartmass * 750))
                                    {
                                        hitpoints = Mathf.Clamp(hitpoints, (dryPartmass * 750), (dryPartmass * 2000)); // if HP is 10x more or 10x than 1/10th drymass in kg, clamp to 10x more/less
                                    }
                                }
                                else
                                {
                                    if (dryPartmass < 1)
                                    {
                                        density = Mathf.Clamp(density, 60, 150);// things like crew cabins are heavy, but most of that mass isn't going to be structural plating, so lets limit structural density
                                                                                // important to note: a lot of the HP values in the old system came from the calculation assuming everytihng had a minimum density of 1000kg/m3
                                                                                //hitpoints = ((dryPartmass * density) * 20) * hitpointMultiplier * 0.33f; //multiplying mass by density extrapolates volume, so parts with the same vol, but different mass appropriately affected (eg Mk1 strucural fuselage vs mk1 LF tank
                                                                                //as well as parts of different vol, but same density - all fueltanks - similarly affected
                                                                                //2.55 * 100* 364^1/3 = 1785, 2.55 * 100 * 98^1/3 = 1157, 2.55 * 100 * 39^1/3 = 854
                                        hitpoints = structuralVolume * 60 * Mathf.Pow(density, 0.333f) * hitpointMultiplier * 0.33f;
                                        if (hitpoints > (dryPartmass * 3500) || hitpoints < (dryPartmass * 350))
                                        {
                                            //Debug.Log($"[BDArmory]: HitpointTracker::Clamping hitpoints for part {part.name}");
                                            hitpoints = Mathf.Clamp(hitpoints, (dryPartmass * 350), (dryPartmass * 3500)); // if HP is 10x more or 10x than 1/10th drymass in kg, clamp to 10x more/less
                                        }
                                    }
                                    else
                                    {
                                        density = Mathf.Clamp(density, 40, 120); //lower stuctural density on very large parts to prevent HP bloat
                                        hitpoints = structuralVolume * 40 * Mathf.Pow(density, 0.333f) * hitpointMultiplier * 0.33f;
                                        //logarithmic scaling past a threshold (2k...?) investigate how this affects S2/3/4 tanks/Mk3 parts, etc
                                        if (hitpoints > (dryPartmass * 2500) || hitpoints < (dryPartmass * 250))
                                        {
                                            //Debug.Log($"[BDArmory]: HitpointTracker::Clamping hitpoints for part {part.name}");
                                            hitpoints = Mathf.Clamp(hitpoints, (dryPartmass * 250), (dryPartmass * 2500)); // if HP is 10x more or 10x than 1/10th drymass in kg, clamp to 10x more/less
                                        }
                                    }
                                }
                            }
                            if (part.IsAero() && !isProcWing)
                            {
                                //hitpoints = dryPartmass * 7000 * hitpointMultiplier * 0.333f; //stock wing parts are 700 HP per unit of Lift, 10 lift/1000kg
                                hitpoints = (float)part.Modules.GetModule<ModuleLiftingSurface>().deflectionLiftCoeff * 700 * hitpointMultiplier * 0.333f; //stock wings are 700 HP per lifting surface area; using lift instead of mass (110 Lift/ton) due to control surfaces weighing more
                            }
                        }
                        */
                        if (part.IsAero() && !isProcWing)
                        {
                            if (FerramAerospace.CheckForFAR())
                            {
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: Found {part.name} (FAR); HP: {Hitpoints}->{hitpoints} at time {Time.time}, partMass: {partMass}, FAR massMult: {FerramAerospace.GetFARMassMult(part)}");
                                hitpoints = (partMass * 14000) * FerramAerospace.GetFARMassMult(part); //FAR massMult doubles stock masses (stock mass at 0.5 Mass-Strength; stock wings 700 HP per unit of Lift
                            }
                            else
                                hitpoints = (float)part.Modules.GetModule<ModuleLiftingSurface>().deflectionLiftCoeff * 700 * hitpointMultiplier * 0.333f; //stock wings are 700 HP per lifting surface area; using lift instead of mass (110 Lift/ton) due to control surfaces weighing more
                        }
                        if (isProcPart || isProcWheel)
                        {
                            structuralVolume = armorVolume * Mathf.PI / 6f * 0.1f; // Box area * sphere/cube ratio * 10cm. We use sphere/cube ratio to get similar results as part.GetAverageBoundSize().
                            density = (partMass * 1000f) / structuralVolume;
                            //if (density > 1e5f || density < 10)
                            if (density > 1e5f || density < 145) //this should cause HP clamping for hollow parts when they reach stock Struct tube thickness or therabouts
                            {
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: procPart {part.name} still has extreme density: {density}! Setting HP based only on mass instead.");
                                clampHP = true;
                            }
                            //density = Mathf.Clamp(density, 500, 10000);
                            density = Mathf.Clamp(density, 250, 10000);
                            structuralMass = density * structuralVolume;
                            //might instead need to grab Procpart mass/size vars via reflection
                            hitpoints = (structuralMass * hitpointMultiplier * 0.333f) * (isProcWheel ? 2.6f : 5.2f);
                        }
                        if (clampHP)
                        {
                            if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: Clamping hitpoints for Procpart {part.name} from {hitpoints} to {hitpointMultiplier * (partMass * 100) * 333f}");
                            //hitpoints = hitpointMultiplier * partMass * 333f; 
                            hitpoints = hitpointMultiplier * (partMass * 10) * 250; //to not have Hp immediately get clamped to 25
                        }
                        //hitpoints = (structuralVolume * Mathf.Pow(density, .333f) * Mathf.Clamp(80 - (structuralVolume / 2), 80 / 4, 80)) * hitpointMultiplier * 0.333f; //volume * cuberoot of density * HP mult scaled by size

                        if (isProcWing)
                        {
                            hitpoints = -1;
                            armorVolume = -1;
                            if (ProceduralWing.CheckForB9ProcWing() && ProceduralWing.CheckForPWModule())
                            {
                                float aeroVolume = ProceduralWing.GetPWingVolume(part); //PWing  0.7 * length * (widthRoot + WidthTip) + (thicknessRoot + ThicknessTip) / 4; yields 1.008 for a stock dimension 2*4*.18 board, so need mult of 1400 for parity with stock wing boards
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: Found {part.name}; HP: {Hitpoints}->{hitpoints} at time {Time.time}, partMass: {partMass}, Pwing Aerovolume: {aeroVolume}");
                                //hitpoints should scale with stock wings correctly (and if used as thicker structural elements, should scale with tanks of similar size)
                                armorVolume = ProceduralWing.GetPWingArea(part);
                                if (!part.name.Contains("B9.Aero.Wing.Procedural.Panel"))
                                {
                                    previousEdgeLift = false;
                                    if (FerramAerospace.CheckForFAR())
                                    {
                                        if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: Found {part.name} (FAR); HP: {Hitpoints}->{hitpoints} at time {Time.time}, partMass: {partMass}, FAR massMult: {FerramAerospace.GetFARMassMult(part)}");
                                        hitpoints = (aeroVolume * 1400) * FerramAerospace.GetFARMassMult(part); //PWing HP no longer mass dependant, so lets have FAR's structural strengthening/weakening have an effect on HP. you want light wings? they're going to be fragile, and vice versa
                                    }
                                    else
                                        hitpoints = (float)Math.Round(part.Modules.GetModule<ModuleControlSurface>() ? part.Modules.GetModule<ModuleLiftingSurface>().deflectionLiftCoeff * 700 : (aeroVolume * 1400), 2) * hitpointMultiplier * 0.333f; //use volume for wings (since they may have lift toggled off), use lift area for control surfaces
                                }
                                else
                                {
                                    hitpoints = aeroVolume * 1200;
                                    if (HighLogic.LoadedSceneIsFlight)
                                    {
                                        var lift = part.FindModuleImplementing<ModuleLiftingSurface>();
                                        if (lift != null) lift.deflectionLiftCoeff = 0;
                                        DragCube DragCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
                                        part.DragCubes.ClearCubes();
                                        part.DragCubes.Cubes.Add(DragCube);
                                        part.DragCubes.ResetCubeWeights();
                                        part.DragCubes.ForceUpdate(true, true, false);
                                        part.DragCubes.SetDragWeights();
                                    }
                                }
                                if (BDArmorySettings.RUNWAY_PROJECT_ROUND == 60) hitpoints = Mathf.Min(500, hitpoints);
                            }
                            if (hitpoints < 0) //sanity checks
                            {
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: Aerovolume not found, reverting to lift/mass HP Calc!");
                                hitpoints = (float)Math.Round(part.Modules.GetModule<ModuleControlSurface>() ? part.Modules.GetModule<ModuleLiftingSurface>().deflectionLiftCoeff : partMass * 10, 2) * 700 * hitpointMultiplier * 0.333f; //use mass*10 for wings (since they may have lift toggled off), use lift area for control surfaces
                            }
                            if (armorVolume < 0)
                            {
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: AeroArea not found, reverting to Hitpoint Armorvolume calc!");
                                armorVolume = (float)Math.Round(hitpoints / hitpointMultiplier / 0.333 / 350, 1); //stock is 0.25 lift/m2, so...
                            }
                            ArmorModified(null, null);
                        }
                        if (BDArmorySettings.HP_THRESHOLD >= 100 && hitpoints > BDArmorySettings.HP_THRESHOLD)
                        {
                            var scale = BDArmorySettings.HP_THRESHOLD / (Mathf.Exp(1) - 1);
                            hitpoints = Mathf.Min(hitpoints, BDArmorySettings.HP_THRESHOLD * Mathf.Log(hitpoints / scale + 1));
                        }
                        hitpoints = BDAMath.RoundToUnit(hitpoints, HpRounding);
                        //hitpoints = Mathf.Round(hitpoints);//?
                        if (hitpoints < 100) hitpoints = 100;
                        hitpoints *= HullInfo.materials[hullType].healthMod; // Apply health mod after rounding and lower limit.
                        if (BDArmorySettings.DEBUG_ARMOR && maxHitPoints <= 0 && Hitpoints != hitpoints) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} updated HP: {Hitpoints}->{hitpoints} at time {Time.time}, partMass: {partMass}, density: {density}, structuralVolume: {structuralVolume}, structuralMass {structuralMass}");
                    }
                    else // Override based on part configuration for custom parts
                    {
                        hitpoints = maxHitPoints * HullInfo.materials[hullType].healthMod;
                        //hitpoints = Mathf.Round(hitpoints); // / HpRounding) * HpRounding;

                        if (BDArmorySettings.DEBUG_ARMOR && maxHitPoints <= 0 && Hitpoints != hitpoints) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} updated HP: {Hitpoints}->{hitpoints} at time {Time.time}");
                    }
                }
                else
                {
                    hitpoints = ArmorRemaining; // * armorVolume * 10;
                                                //hitpoints = Mathf.Round(hitpoints / HpRounding) * HpRounding;
                                                //armorpanel HP is panel integrity, as 'HP' is the slab of armor; having a secondary unused HP pool will only make armor massively more effective against explosions than it should due to how isInLineOfSight calculates intermediate parts
                }
            }
            else
            {
                hitpoints = maxHitPoints > 0 ? maxHitPoints : 5;
            }
            if (!_finished_setting_up && _armorConfigured && _hullConfigured) _hpConfigured = true;
            if (BDArmorySettings.HP_CLAMP >= 100)
                hitpoints = Mathf.Min(hitpoints, BDArmorySettings.HP_CLAMP);
            return hitpoints;
        }

        public void DestroyPart()
        {
            if ((part.mass - armorMass) <= 2f) part.explosionPotential *= 0.85f;

            PartExploderSystem.AddPartToExplode(part);
        }

        public float GetMaxArmor()
        {
            UI_FloatRange armorField = (UI_FloatRange)Fields["Armor"].uiControlEditor;
            return armorField.maxValue;
        }

        public float GetMaxHitpoints()
        {
            UI_ProgressBar hitpointField = (UI_ProgressBar)Fields["Hitpoints"].uiControlEditor;
            return hitpointField.maxValue;
        }

        public bool GetFireFX()
        {
            return FireFX;
        }

        public void SetDamage(float partdamage)
        {
            Hitpoints = partdamage; //given the sole reference is from destroy, with damage = -1, shouldn't this be =, not -=?

            if (Hitpoints <= 0)
            {
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitPointTracker] Setting HP of {part.name} to {Hitpoints}, destroying");
                DestroyPart();
            }
        }

        public void AddDamage(float partdamage, bool overcharge = false)
        {
            if (isAI) return;
            if (ArmorPanel)
            {
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log("[BDArmory.HitPointTracker] AddDamage(), hit part is armor panel, returning");
                return;
            }

            partdamage = Mathf.Max(partdamage, 0f) * -1;
            Hitpoints += (partdamage / defenseMutator); //why not just go -= partdamage?
            if (BDArmorySettings.BATTLEDAMAGE && BDArmorySettings.BD_PART_STRENGTH)
            {
                part.breakingForce = maxForce * (Hitpoints / maxHitPoints);
                part.breakingTorque = maxTorque * (Hitpoints / maxHitPoints);
                part.gTolerance = maxG * (Hitpoints / maxHitPoints);
            }
            if (Hitpoints <= 0)
            {
                DestroyPart();
            }
        }

        public void AddHealth(float partheal, bool overcharge = false)
        {
            if (isAI) return;
            if (Hitpoints + partheal < BDArmorySettings.HEART_BLEED_THRESHOLD) //in case of negative regen value (for HP drain)
            {
                return;
            }
            Hitpoints += partheal;

            Hitpoints = Mathf.Clamp(Hitpoints, -1, overcharge ? Mathf.Min(previousHitpoints * 2, previousHitpoints + 1000) : previousHitpoints); //Allow vampirism to overcharge HP
        }

        public void AddDamageToKerbal(KerbalEVA kerbal, float damage)
        {
            damage = Mathf.Max(damage, 0f) * -1;
            Hitpoints += damage;

            if (Hitpoints <= 0)
            {
                // oh the humanity!
                PartExploderSystem.AddPartToExplode(kerbal.part);
            }
        }
        #endregion Hitpoints Functions

        #region Armor

        public void ReduceArmor(float massToReduce) //incoming massToreduce should be cm3
        {
            if (BDArmorySettings.DEBUG_ARMOR)
            {
                Debug.Log("[HPTracker] armor mass: " + armorMass + "; mass to reduce: " + (massToReduce * Math.Round((Density / 1000000), 3)) * BDArmorySettings.ARMOR_MASS_MOD + "kg"); //g/m3
            }
            float reduceMass = (massToReduce * (Density / 1000000000)); //g/cm3 conversion to yield tons
            if (totalArmorQty > 0)
            {
                //Armor -= ((reduceMass * 2) / armorMass) * Armor; //armor that's 50% air isn't going to stop anything and could be considered 'destroyed' so lets reflect that by doubling armor loss (this will also nerf armor panels from 'god-tier' to merely 'very very good'
                Armor -= ((reduceMass * 1.5f) / totalArmorQty) * Armor;
                if (Armor < 0)
                {
                    Armor = 0;
                    ArmorRemaining = 0;
                }
                else ArmorRemaining = Armor / StartingArmor * 100;
                Armour = Armor;
            }
            else
            {
                if (Armor < 0)
                {
                    Armor = 0;
                    ArmorRemaining = 0;
                    Armour = Armor;
                }
            }
            if (ArmorPanel)
            {
                Hitpoints = ArmorRemaining; // * armorVolume * 10;
                if (Armor <= 0)
                {
                    DestroyPart();
                }
            }
            totalArmorQty -= reduceMass;
            armorMass = totalArmorQty * BDArmorySettings.ARMOR_MASS_MOD; //tons
            if (armorMass <= 0)
            {
                armorMass = 0;
            }
        }

        public void overrideArmorSetFromConfig()
        {
            ArmorSet = true;

            if (ArmorThickness > 10 || ArmorPanel) //Mod part set to start with armor, or armor panel
            {
                startsArmored = true;
                if (Armor < 0) // armor amount modified in SPH/VAB and does not = either the default nor the .cfg thickness
                    Armor = ArmorThickness;//set Armor amount to .cfg value
                                           //See also ln 1183-1186
            }
            if (maxSupportedArmor < 0) //hasn't been set in cfg
            {
                if (part.IsAero())
                {
                    if (isProcWing)
                        maxSupportedArmor = ProceduralWing.getPwingThickness(part);
                    else
                        maxSupportedArmor = 20;
                }
                else
                {
                    maxSupportedArmor = ((Mathf.Min(partSize.x, partSize.y, partSize.z) / 20) * 1000); //~62mm for Size1, 125mm for S2, 185mm for S3
                    maxSupportedArmor /= 5;
                    maxSupportedArmor = Mathf.Round(maxSupportedArmor);
                    maxSupportedArmor *= 5;
                }
                if (ArmorThickness > 10 && ArmorThickness > maxSupportedArmor)//part has custom armor value, use that
                {
                    maxSupportedArmor = ArmorThickness;
                }
            }
            if (BDArmorySettings.DEBUG_ARMOR)
            {
                Debug.Log("[ARMOR] max supported armor for " + part.name + " is " + maxSupportedArmor);
            }
            //if maxSupportedArmor > 0 && < armorThickness, that's entirely the fault of the MM patcher
            UI_FloatRange armorFieldFlight = (UI_FloatRange)Fields["Armor"].uiControlFlight;
            armorFieldFlight.minValue = 0f;
            armorFieldFlight.maxValue = maxSupportedArmor;
            UI_FloatRange armorFieldEditor = (UI_FloatRange)Fields["Armor"].uiControlEditor;
            armorFieldEditor.maxValue = maxSupportedArmor;
            armorFieldEditor.minValue = 1f;
            armorFieldEditor.onFieldChanged = ArmorModified;
            part.RefreshAssociatedWindows();
        }

        public void ArmorSetup(BaseField field, object obj)
        {
            if (OldArmorType != ArmorTypeNum)
            {
                if ((ArmorTypeNum - 1) > ArmorInfo.armorNames.Count) //in case of trying to load a craft using a mod armor type that isn't installed and having a armorTypeNum larger than the index size
                {
                    //ArmorTypeNum = 1; //reset to 'None'
                    ArmorTypeNum = ArmorInfo.armors.FindIndex(t => t.name == "None") + 1;
                }
                if (isAI || part.IsMissile() || BDArmorySettings.RESET_ARMOUR)
                {
                    ArmorTypeNum = ArmorInfo.armors.FindIndex(t => t.name == "None") + 1;
                }
                armorInfo = ArmorInfo.armors[ArmorInfo.armorNames[(int)ArmorTypeNum - 1]]; //what does this return if armorname cannot be found (mod armor removed/not present in install?)

                //if (SelectedArmorType != ArmorInfo.armorNames[(int)ArmorTypeNum - 1]) //armor selection overridden by Editor widget
                //{
                //	armorInfo = ArmorInfo.armors[SelectedArmorType];
                //    ArmorTypeNum = ArmorInfo.armors.FindIndex(t => t.name == SelectedArmorType); //adjust part's current armor setting to match
                //}
                guiArmorTypeString = armorInfo.name; //FIXME - Localize these
                SelectedArmorType = armorInfo.name;
                Density = armorInfo.Density;
                Diffusivity = armorInfo.Diffusivity;
                Ductility = armorInfo.Ductility;
                Hardness = armorInfo.Hardness;
                Strength = armorInfo.Strength;
                SafeUseTemp = armorInfo.SafeUseTemp;
                armorRadarReturnFactor = 1;

                vFactor = armorInfo.vFactor;
                muParam1 = armorInfo.muParam1;
                muParam2 = armorInfo.muParam2;
                muParam3 = armorInfo.muParam3;
                muParam1S = armorInfo.muParam1S;
                muParam2S = armorInfo.muParam2S;
                muParam3S = armorInfo.muParam3S;
                HEEquiv = armorInfo.HEEquiv;
                HEATEquiv = armorInfo.HEATEquiv;

                SetArmor();
            }
            if (BDArmorySettings.LEGACY_ARMOR)
            {
                guiArmorTypeString = guiArmorTypeString = StringUtils.Localize("#LOC_BDArmory_Steel");
                SelectedArmorType = "Legacy Armor";
                Density = 7850;
                Diffusivity = 48.5f;
                Ductility = 0.15f;
                Hardness = 1176;
                Strength = 940;

                // Calculated using yield = 700 MPa and youngModulus = 200 GPA
                vFactor = 9.47761748e-07f;
                muParam1 = 0.656060636f;
                muParam2 = 1.20190930f;
                muParam3 = 1.77791929f;
                muParam1S = 0.947031140f;
                muParam2S = 1.55575776f;
                muParam3S = 2.75371552f;
                HEEquiv = 1f;
                HEATEquiv = 1f;

                SafeUseTemp = 2500;
                if (BDArmorySettings.DEBUG_ARMOR)
                {
                    Debug.Log("[ARMOR] Armor of " + part.name + " reset  by LEGACY_ARMOUR");
                }
            }
            else if (BDArmorySettings.RESET_ARMOUR) //don't reset armor panels
            {
                guiArmorTypeString = guiArmorTypeString = StringUtils.Localize("#LOC_BDArmory_WMWindow_NoneWeapon"); //"none"
                SelectedArmorType = "None";
                Density = 2700;
                Diffusivity = 237f;
                Ductility = 0.6f;
                Hardness = 300;
                Strength = 200;

                // Calculated using yield = 110 MPa and youngModulus = 70 GPA
                vFactor = 1.82712211e-06f;
                muParam1 = 1.37732446f;
                muParam2 = 2.04939008f;
                muParam3 = 4.53333330f;
                muParam1S = 1.92650831f;
                muParam2S = 2.65274119f;
                muParam3S = 7.37037039f;
                HEEquiv = 0.1601427673f;
                HEATEquiv = 0.5528789891f;

                SafeUseTemp = 993;
                Armor = part.IsMissile() ? 2 : 10;
                if (ArmorPanel)
                {
                    ArmorTypeNum = ArmorInfo.armors.FindIndex(t => t.name == "Steel") + 1;
                    Armor = 25;
                    Density = 7850;
                    Diffusivity = 48.5f;
                    Ductility = 0.15f;
                    Hardness = 1176;
                    Strength = 940;

                    // Calculated using yield = 700 MPa and youngModulus = 200 GPA
                    vFactor = 9.47761748e-07f;
                    muParam1 = 0.656060636f;
                    muParam2 = 1.20190930f;
                    muParam3 = 1.77791929f;
                    muParam1S = 0.947031140f;
                    muParam2S = 1.55575776f;
                    muParam3S = 2.75371552f;
                }
                else
                {
                    Fields["Armor"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActiveEditor = false;
                    Fields["guiArmorTypeString"].guiActive = false;
                    Fields["armorCost"].guiActiveEditor = false;
                    Fields["armorMass"].guiActiveEditor = false;
                }
                if (BDArmorySettings.DEBUG_ARMOR)
                {
                    Debug.Log("[ARMOR] Armor of " + part.name + " reset to defaults by RESET_ARMOUR");
                }
            }
            var oldArmorMass = armorMass;
            part.skinInternalConductionMult = skinskinConduction; //reset to .cfg value
            part.skinSkinConductionMult = skinInternalConduction; //reset to .cfg value
            part.skinMassPerArea = 1; //default value
            armorMass = 0;
            armorCost = 0;
            totalArmorQty = 0;
            if (ArmorTypeNum != (ArmorInfo.armors.FindIndex(t => t.name == "None") + 1) && (!BDArmorySettings.LEGACY_ARMOR || (!BDArmorySettings.RESET_ARMOUR || (BDArmorySettings.RESET_ARMOUR && ArmorThickness > 10)))) //don't apply cost/mass to None armor type
            {
                armorMass = (Armor / 1000) * armorVolume * Density / 1000; //armor mass in tons
                armorCost = (Armor / 1000) * armorVolume * armorInfo.Cost; //armor cost, tons

                part.skinInternalConductionMult = skinInternalConduction * BDAMath.Sqrt(Diffusivity / 237); //how well does the armor allow external heat to flow into the part internals?
                part.skinSkinConductionMult = skinskinConduction * BDAMath.Sqrt(Diffusivity / 237); //how well does the armor conduct heat to connected part skins?
                part.skinMassPerArea = (Density / 1000) * ArmorThickness;
                armorRadarReturnFactor = armorInfo.radarReflectivity;
            }
            if (ArmorTypeNum == (ArmorInfo.armors.FindIndex(t => t.name == "None") + 1) && ArmorPanel)
            {
                armorMass = (Armor / 1000) * armorVolume * Density / 1000;
                guiArmorTypeString = StringUtils.Localize("#LOC_BDArmory_Aluminium");
                SelectedArmorType = "None";
                armorCost = (Armor / 1000) * armorVolume * armorInfo.Cost;
                part.skinInternalConductionMult = skinInternalConduction * BDAMath.Sqrt(Diffusivity / 237); //how well does the armor allow external heat to flow into the part internals?
                part.skinSkinConductionMult = skinskinConduction * BDAMath.Sqrt(Diffusivity / 237); //how well does the armor conduct heat to connected part skins?
                part.skinMassPerArea = (Density / 1000) * ArmorThickness;
                armorRadarReturnFactor = armorInfo.radarReflectivity;
            }
            CalculateRCSreduction();
            totalArmorQty = armorMass; //grabbing a copy of unmodified armorMAss so it can be used in armorMass' place for armor reduction without having to un/re-modify the mass before and after armor hits
            armorMass *= BDArmorySettings.ARMOR_MASS_MOD;
            //part.RefreshAssociatedWindows(); //having this fire every time a change happens prevents sliders from being used. Add delay timer?
            if (OldArmorType != ArmorTypeNum || oldArmorMass != armorMass)
            {
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} updated armour mass {oldArmorMass}->{armorMass} or type {OldArmorType}->{ArmorTypeNum} at time {Time.time}");
                OldArmorType = ArmorTypeNum;
                _updateMass = true;
                part.UpdateMass();
                if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                    GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            _armorConfigured = true;
        }

        public void SetArmor()
        {
            //if (isAI) return; //replace with newer implementation
            if (BDArmorySettings.LEGACY_ARMOR || BDArmorySettings.RESET_ARMOUR) return;
            if (part.IsMissile()) return;
            if (ArmorTypeNum != (ArmorInfo.armors.FindIndex(t => t.name == "None") + 1) || ArmorPanel)
            {
                /*
                UI_FloatRange armorFieldFlight = (UI_FloatRange)Fields["Armor"].uiControlFlight;
                if (armorFieldFlight.maxValue != maxSupportedArmor)
                {
                    armorReset = false;
                    armorFieldFlight.minValue = 0f;
                    armorFieldFlight.maxValue = maxSupportedArmor;
                }
                */
                Fields["Armor"].guiActiveEditor = true;
                Fields["guiArmorTypeString"].guiActiveEditor = true;
                Fields["guiArmorTypeString"].guiActive = true;
                Fields["armorCost"].guiActiveEditor = true;
                Fields["armorMass"].guiActiveEditor = true;
                UI_FloatRange armorFieldEditor = (UI_FloatRange)Fields["Armor"].uiControlEditor;
                if (isProcWing)
                    maxSupportedArmor = ProceduralWing.getPwingThickness(part);
                if (armorFieldEditor.maxValue != maxSupportedArmor)
                {
                    armorReset = false;
                    armorFieldEditor.maxValue = maxSupportedArmor;
                    armorFieldEditor.minValue = 1f;
                }
                armorFieldEditor.onFieldChanged = ArmorModified;
                if (!armorReset)
                {
                    part.RefreshAssociatedWindows();
                }
                armorReset = true;
            }
            else
            {
                Armor = 10;
                Fields["Armor"].guiActiveEditor = false;
                Fields["guiArmorTypeString"].guiActiveEditor = false;
                Fields["guiArmorTypeString"].guiActive = false;
                Fields["armorCost"].guiActiveEditor = false;
                Fields["armorMass"].guiActiveEditor = false;
                //UI_FloatRange armorFieldEditor = (UI_FloatRange)Fields["Armor"].uiControlEditor;
                //armorFieldEditor.maxValue = 10; //max none armor to 10 (simulate part skin of alimunium)
                //armorFieldEditor.minValue = 10;

                part.RefreshAssociatedWindows();
                //GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }
        private static Bounds CalcPartBounds(Part p, Transform t)
        {
            Bounds result = new Bounds(t.position, Vector3.zero);
            Bounds[] bounds = p.GetRendererBounds(); //slower than getColliderBounds, but it only runs once, and doesn't have to deal with culling isTrgger colliders (airlocks, ladders, etc)
                                                     //Err... not so sure about that, me. This is yielding different resutls in SPH/flight. SPH is proper dimensions, flight is giving bigger x/y/z
                                                     // a mk1 cockpit (x: 1.25, y: 1.6, z: 1.9, area 11 in SPh becomes x: 2.5, y: 1.25, z: 2.5, area 19
            {
                if (!p.Modules.Contains("LaunchClamp"))
                {
                    for (int i = 0; i < bounds.Length; i++)
                    {
                        result.Encapsulate(bounds[i]);
                    }
                }
            }
            return result;
        }

        public void HullSetup(BaseField field, object obj) //no longer needed for realtime HP calcs, but does need to be updated occasionally to give correct vessel mass
        {
            if (isProcWing)
            {
                StartCoroutine(WaitForHullSetup());
            }
            else
            {
                SetHullMass();
            }
        }
        IEnumerator WaitForHullSetup()
        {
            if (waitingForHullSetup) yield break;  // Already waiting.
            waitingForHullSetup = true;
            yield return new WaitForFixedUpdate();
            waitingForHullSetup = false;
            if (part == null) yield break; // The part disappeared!

            SetHullMass();
        }
        void SetHullMass()
        {
            if (IgnoreForArmorSetup)
            {
                _hullConfigured = true;
                return;
            }
            if (isAI || ArmorPanel || ProjectileUtils.isMaterialBlackListpart(this.part))
            {
                _hullConfigured = true;
                return;
                //HullTypeNum = HullInfo.materials.FindIndex(t => t.name == "Aluminium");
            }

            if (OldHullType != HullTypeNum || (BDArmorySettings.RESET_HULL || BDArmorySettings.LEGACY_ARMOR))

            {
                if ((HullTypeNum - 1) > HullInfo.materialNames.Count || (BDArmorySettings.RESET_HULL || BDArmorySettings.LEGACY_ARMOR)) //in case of trying to load a craft using a mod hull type that isn't installed and having a hullTypeNum larger than the index size
                {
                    if (!HullInfo.materialNames.Contains("Aluminium")) Debug.LogError("[BDArmory.HitpointTracker] BD_Materials.cfg missing! Please fix your BDA insteall");
                    HullTypeNum = HullInfo.materials.FindIndex(t => t.name == "Aluminium") + 1;
                }

                if ((part.isEngine() || part.IsWeapon()) && HullInfo.materials[HullInfo.materialNames[(int)HullTypeNum - 1]].massMod < 1) //can armor engines, but not make them out of wood.
                {
                    HullTypeNum = HullInfo.materials.FindIndex(t => t.name == "Aluminium") + 1;
                    part.maxTemp = part.partInfo.partPrefab.maxTemp;
                }

                hullInfo = HullInfo.materials[HullInfo.materialNames[(int)HullTypeNum - 1]];
            }
            var OldHullMassAdjust = HullMassAdjust;
            HullMassAdjust = (partMass * hullInfo.massMod) - partMass;
            guiHullTypeString = string.IsNullOrEmpty(hullInfo.localizedName) ? hullInfo.name : StringUtils.Localize(hullInfo.localizedName);
            if (hullInfo.maxTemp > 0)
            {
                part.maxTemp = hullInfo.maxTemp;
                part.skinMaxTemp = hullInfo.maxTemp;
            }
            else
            {
                part.maxTemp = part.partInfo.partPrefab.maxTemp > 0 ? part.partInfo.partPrefab.maxTemp : 2500; //kerbal flags apparently starting with -1 maxtemp
                part.skinMaxTemp = part.partInfo.partPrefab.skinMaxTemp > 0 ? part.partInfo.partPrefab.skinMaxTemp : 2500;
            }
            ignitionTemp = hullInfo.ignitionTemp;
            part.crashTolerance = part.partInfo.partPrefab.crashTolerance * hullInfo.ImpactMod;
            maxForce = part.partInfo.partPrefab.breakingForce * hullInfo.ImpactMod;
            part.breakingForce = maxForce;
            maxTorque = part.partInfo.partPrefab.breakingTorque * hullInfo.ImpactMod;
            part.breakingTorque = maxTorque;
            maxG = part.partInfo.partPrefab.gTolerance * hullInfo.ImpactMod;
            part.gTolerance = maxG;
            hullRadarReturnFactor = hullInfo.radarMod;
            hullType = hullInfo.name;
            CalculateRCSreduction();
            float partCost = part.partInfo.cost + part.partInfo.variant.Cost;
            if (hullInfo.costMod < 1) HullCostAdjust = Mathf.Max((partCost - (float)resourceCost) * hullInfo.costMod, partCost - (1000 - (hullInfo.costMod * 1000))) - (partCost - (float)resourceCost);//max of 1000 funds discount on cheaper materials
            else HullCostAdjust = Mathf.Min((partCost - (float)resourceCost) * hullInfo.costMod, (partCost - (float)resourceCost) + (hullInfo.costMod * 1000)) - (partCost - (float)resourceCost); //Increase costs if costMod => 1                                                                                                                                                             
            //this returns cost of base variant, yielding part variant that are discounted by 50% or 500 of base variant cost, not current variant. method to get currently selected variant?

            if (OldHullType != HullTypeNum || OldHullMassAdjust != HullMassAdjust)
            {
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker]: {part.name} updated hull mass {OldHullMassAdjust}->{HullMassAdjust} (part mass {partMass}, total mass {part.mass + HullMassAdjust - OldHullMassAdjust}) or type {OldHullType}->{HullTypeNum} at time {Time.time}");
                OldHullType = HullTypeNum;
                _updateMass = true;
                part.UpdateMass();
                if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                    GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            _hullConfigured = true;
        }
        private void CalculateRCSreduction()
        {
            if (ArmorTypeNum > 1 && Armor > 0) //if ArmorType != None and armor thickness != 0
            {
                //float radarReflected = 1 - (armorRadarReturnFactor * (1 + Mathf.Log(Mathf.Max(Armor, 1), 100f))) //FIXME - this is busted, needs review
                //vv less than ideal, but works for v1.0
                float radarReflected = Armor < 10 ? armorRadarReturnFactor + ((1 - armorRadarReturnFactor) / 10) * (10 - Armor) : armorRadarReturnFactor;//armor < 10 will have reduced radar absorbsion, else
                //reflector armor/ translucent armor reflecting subsurface structure/RAM thicker than it needs to be, no change;
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.HitpointTracker] radarReflectivity for {part.name} is {armorRadarReturnFactor}; radarRefected {radarReflected}");
                radarReflectivity = radarReflected; //radar return based on armor material
                if (radarReflected > 1) //radar-translucent armor...
                {
                    if (hullRadarReturnFactor < 1) // w/ radar absorbent structural elements
                        radarReflectivity = 1 - (hullRadarReturnFactor * (radarReflected - 1));
                    if (hullRadarReturnFactor < 1) // w/ radar reflective structural elements
                        radarReflectivity = 1 - (hullRadarReturnFactor * (1 - radarReflected));
                }
            }
            else //(ArmorTypeNum < 1 || Armor < 1) //no armor, radar return based on hull material
            {
                radarReflectivity = hullRadarReturnFactor;
            }
            if (radarReflectivity > 2 || radarReflectivity < 0) // goes up to 2 in case of radar reflectors/anti-stealth coatings, etc
            {
                radarReflectivity = Mathf.Clamp(radarReflectivity, 0, 2);
            }
            if (BDArmorySettings.DEBUG_ARMOR) Debug.Log("[ARMOR]: Radar return rating is " + radarReflectivity);
        }
        private List<PartResource> GetResources()
        {
            List<PartResource> resources = new List<PartResource>();

            foreach (PartResource resource in part.Resources)
            {
                if (!resources.Contains(resource)) { resources.Add(resource); }
            }
            return resources;
        }
        private void CalculateDryCost()
        {
            resourceCost = 0;
            foreach (PartResource resource in GetResources())
            {
                var resources = part.Resources.ToList();
                using (IEnumerator<PartResource> res = resources.GetEnumerator())
                    while (res.MoveNext())
                    {
                        if (res.Current == null) continue;
                        if (res.Current.resourceName == resource.resourceName)
                        {
                            resourceCost += res.Current.info.unitCost * res.Current.maxAmount; //turns out parts subtract res cost even if the tank starts empty
                        }
                    }
            }
        }
        #endregion Armor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            if (startsArmored || ArmorPanel)
            {
                output.AppendLine($"Starts Armored");
                output.AppendLine($" - Armor Mass: {armorMass}");
            }
            return output.ToString();
        }
    }
}
