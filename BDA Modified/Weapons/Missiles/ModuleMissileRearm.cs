using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

using BDArmory.WeaponMounts;
using BDArmory.Settings;
using System.Text;

namespace BDArmory.Weapons.Missiles
{
    public class ModuleMissileRearm : PartModule, IPartMassModifier, IPartCostModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => Mathf.Max((isMultiLauncher ? ammoCount : ammoCount - 1), 0) * missileMass;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => Mathf.Max((isMultiLauncher ? ammoCount : ammoCount - 1), 0) * missileCost;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        private float missileMass = 0;
        private float missileCost = 0;

        public bool isMultiLauncher = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_OrdinanceAvailable"),//Ordinance Available
UI_FloatRange(minValue = 1f, maxValue = 4, stepIncrement = 1f, scene = UI_Scene.Editor)]
        public float ammoCount = 1; //need to figure out where ammo is stored, for mass addition/subtraction - in the missile? external missile ammo bin? CoM?

        [KSPField(isPersistant = true)]
        public string MissileName = "bahaAim120";

        [KSPField] public float reloadTime = 5f;
        [KSPField] public bool AccountForAmmo = true;
        [KSPField] public float maxAmmo = 20;
        //public float tntmass = 1;
        AvailablePart missilePart;
        public Part SpawnedMissile;
        public bool SpawnMissile(Transform MissileTransform, float offset = 0, bool deductAmmo = true)
        {
            if (ammoCount >= 1 || BDArmorySettings.INFINITE_ORDINANCE)
            {
                if (missilePart != null)
                {
                    if (MissileTransform == null) MissileTransform = part.partTransform;

                    foreach (PartModule m in missilePart.partPrefab.Modules)
                    {
                        if (m.moduleName == "MissileLauncher")
                        {
                            var partNode = new ConfigNode();
                            PartSnapshot(missilePart.partPrefab).CopyTo(partNode);
                            //SpawnedMissile = CreatePart(partNode, MissileTransform.transform.position - MissileTransform.TransformDirection(missilePart.partPrefab.srfAttachNode.originalPosition),
                            SpawnedMissile = CreatePart(partNode, offset > 0 ? (MissileTransform.position + MissileTransform.forward * offset) : MissileTransform.transform.position, MissileTransform.rotation, this.part);
                            ModuleMissileRearm MMR = SpawnedMissile.FindModuleImplementing<ModuleMissileRearm>();
                            if (MMR != null) SpawnedMissile.RemoveModule(MMR);
                            if (!BDArmorySettings.INFINITE_ORDINANCE && deductAmmo) ammoCount--;
                            if (BDArmorySettings.DEBUG_MISSILES) Debug.Log("[BDArmory.ModuleMissileRearm] spawned " + SpawnedMissile.name + "; ammo remaining: " + ammoCount);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override void OnStart(PartModule.StartState state)
        {
            this.enabled = true;
            this.part.force_activate();
            var MML = part.FindModuleImplementing<MultiMissileLauncher>();
            if (MML == null || MML && MML.isClusterMissile) MissileName = part.name;
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
                StartCoroutine(GetMissileValues());
            //GameEvents.onEditorShipModified.Add(ShipModified);
            UI_FloatRange Ammo = (UI_FloatRange)Fields["ammoCount"].uiControlEditor;
            Ammo.maxValue = maxAmmo;
        }
        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(ShipModified);
        }

        public void ShipModified(ShipConstruct data)
        {
            if (part.parent)
            {
                /*
                if (part.parent.FindModuleImplementing<MissileTurret>()) //turrets work... sorta. Missiles are reloading where they should be, but there's some massive force being imparted on the turret every launch
                {// test UpdateMissileChildren fix used for rotary rails?
                    ammoCount = 1;
                    Fields["ammoCount"].guiActiveEditor = false;
                }
                else
                {*/
                Fields["ammoCount"].guiActiveEditor = true;
                //}

            }
        }

        public void UpdateMissileValues()
        {
            StartCoroutine(GetMissileValues());
        }
        IEnumerator GetMissileValues()
        {
            yield return new WaitForFixedUpdate();
            MissileLauncher ml = part.FindModuleImplementing<MissileLauncher>();
            ml.reloadableRail = this;
            using (var parts = PartLoader.LoadedPartsList.GetEnumerator())
                while (parts.MoveNext())
                {
                    if (parts.Current.partConfig == null || parts.Current.partPrefab == null)
                        continue;
                    if (parts.Current.partPrefab.partInfo.name != MissileName) continue;
                    missilePart = parts.Current;
                    if (BDArmorySettings.DEBUG_MISSILES) Debug.Log($"[BDArmory.ModuleMissileRearm]: found {missilePart.partPrefab.partInfo.name}");
                    break;
                }
            if (missilePart == null)
            {
                Debug.LogWarning($"[BDArmory.ModuleMissileRearm]: Failed to find missile part on {part.partInfo.name}");
                missileCost = 0;
                missileMass = 0;
                yield break;
            }
            if (AccountForAmmo)
            {
                missileCost = missilePart.partPrefab.partInfo.cost;
                missileMass = missilePart.partPrefab.mass;
            }
            else
            {
                missileCost = 0;
                missileMass = 0;
            }
        }

        static IEnumerator FinalizeMissile(Part missile, Part launcher)
        {
            //Debug.Log("[BDArmory.ModuleMissileRearm]: Creating " + missile);
            string originatingVesselName = missile.vessel.vesselName;
            missile.physicalSignificance = Part.PhysicalSignificance.NONE;
            missile.PromoteToPhysicalPart();
            var childColliders = missile.GetComponentsInChildren<Collider>(includeInactive: false);
            CollisionManager.IgnoreCollidersOnVessel(launcher.vessel, childColliders);
            missile.Unpack();
            missile.InitializeModules();
            Vessel newVessel = missile.gameObject.AddComponent<Vessel>();
            newVessel.id = Guid.NewGuid();
            if (newVessel.Initialize(false))
            {
                newVessel.vesselName = Vessel.AutoRename(newVessel, originatingVesselName);
                newVessel.IgnoreGForces(10);
                newVessel.currentStage = StageManager.RecalculateVesselStaging(newVessel);
                missile.setParent(null);
            }
            yield return new WaitWhile(() => !missile.started && missile.State != PartStates.DEAD);
            if (missile.State == PartStates.DEAD)
            {
                Debug.Log("[BDArmory.ModuleMissileRearm]: Error; " + missile + " died before being fully initialized");
                yield break;
            }
        }

        public static ConfigNode PartSnapshot(Part part)
        {
            var node = new ConfigNode("PART");
            var snapshot = new ProtoPartSnapshot(part, null);

            snapshot.attachNodes = new List<AttachNodeSnapshot>();
            snapshot.srfAttachNode = new AttachNodeSnapshot("attach,-1");
            snapshot.symLinks = new List<ProtoPartSnapshot>();
            snapshot.symLinkIdxs = new List<int>();
            snapshot.Save(node);

            // Prune unimportant data
            node.RemoveValues("parent");
            node.RemoveValues("position");
            node.RemoveValues("rotation");
            node.RemoveValues("istg");
            node.RemoveValues("dstg");
            node.RemoveValues("sqor");
            node.RemoveValues("sidx");
            node.RemoveValues("attm");
            node.RemoveValues("srfN");
            node.RemoveValues("attN");
            node.RemoveValues("connected");
            node.RemoveValues("attached");
            node.RemoveValues("flag");
            node.RemoveNodes("ACTIONS");

            var module_nodes = node.GetNodes("MODULE");
            var prefab_modules = part.partInfo.partPrefab.GetComponents<PartModule>();
            node.RemoveNodes("MODULE");

            for (int i = 0; i < prefab_modules.Length && i < module_nodes.Length; i++)
            {
                var module = module_nodes[i];
                var name = module.GetValue("name") ?? "";

                node.AddNode(module);
                module.RemoveNodes("ACTIONS");
            }
            return node;
        }

        public delegate void OnPartReady(Part affectedPart);

        /// <summary>Creates a new part from the config.</summary>
        /// <param name="partConfig">Config to read part from.</param>
        /// <param name="position">Initial position of the new part.</param>
        /// <param name="rotation">Initial rotation of the new part.</param>
        /// <param name="fromPart"></param>

        public static Part CreatePart(
            ConfigNode partConfig,
            Vector3 position,
            Quaternion rotation,
            Part launcherPart)
        {
            var refVessel = launcherPart.vessel;
            var partNodeCopy = new ConfigNode();
            partConfig.CopyTo(partNodeCopy);
            var snapshot =
                new ProtoPartSnapshot(partNodeCopy, refVessel.protoVessel, HighLogic.CurrentGame);
            if (HighLogic.CurrentGame.flightState.ContainsFlightID(snapshot.flightID)
                || snapshot.flightID == 0)
            {
                snapshot.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
            }
            snapshot.parentIdx = 0;
            snapshot.position = position;
            snapshot.rotation = rotation;
            snapshot.stageIndex = 0;
            snapshot.defaultInverseStage = 0;
            snapshot.seqOverride = -1;
            snapshot.inStageIndex = -1;
            snapshot.attachMode = (int)AttachModes.SRF_ATTACH;
            snapshot.attached = false;

            var newPart = snapshot.Load(refVessel, false);
            newPart.transform.position = position;
            newPart.transform.rotation = rotation;
            if (newPart.rb != null)
            {
                newPart.rb.velocity = launcherPart.Rigidbody.velocity;
                newPart.rb.angularVelocity = launcherPart.Rigidbody.angularVelocity;
            }
            newPart.missionID = launcherPart.missionID;
            newPart.UpdateOrgPosAndRot(newPart.vessel.rootPart);

            newPart.StartCoroutine(FinalizeMissile(newPart, launcherPart));
            return newPart;
        }
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();

            output.Append(Environment.NewLine);
            output.AppendLine($"Missile Rearming");
            output.AppendLine($"- Reload Time: {reloadTime} s");
            output.AppendLine($"- Maximum Ordinance: {maxAmmo}");
            output.AppendLine($"- Ammo Mass/Cost: {AccountForAmmo}");
            return output.ToString();
        }
    }
}