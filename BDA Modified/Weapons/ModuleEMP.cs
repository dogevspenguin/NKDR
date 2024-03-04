using System.Text;
using UnityEngine;

using BDArmory.Damage;
using BDArmory.Settings;
using BDArmory.Utils;

namespace BDArmory.Weapons
{
    public class ModuleEMP : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_EMPBlastRadius"),//EMP Blast Radius
         UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float proximity = 5000;

        [KSPField]
        public bool AllowReboot = false;

        public bool Armed = false;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
                part.OnJustAboutToBeDestroyed += DetonateEMPRoutine;
            }
            base.OnStart(state);
        }

        public void DetonateEMPRoutine()
        {
            if (!Armed) return;
            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDArmory.ModuleEMP]: Detonating EMP from {part.partInfo.name} with blast range {proximity}m.");
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v == null || !v.loaded || v.packed) continue;
                if (VesselModuleRegistry.ignoredVesselTypes.Contains(v.vesselType)) continue;
                if (!v.HoldPhysics)
                {
                    double targetDistance = Vector3d.Distance(this.vessel.GetWorldPos3D(), v.GetWorldPos3D());

                    if (targetDistance <= proximity)
                    {
                        var emp = v.rootPart.FindModuleImplementing<ModuleDrainEC>();
                        if (emp == null)
                        {
                            emp = (ModuleDrainEC)v.rootPart.AddModule("ModuleDrainEC");
                        }
                        emp.incomingDamage += ((proximity - (float)targetDistance) * 10); //this way craft at edge of blast might only get disabled instead of bricked
                        emp.softEMP = AllowReboot; //can bypass DMP damage cap
                    }
                }
            }
        }

        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(System.Environment.NewLine);
            output.AppendLine($"- EMP Blast Radius: {proximity} m");
            return output.ToString();
        }
    }
}
