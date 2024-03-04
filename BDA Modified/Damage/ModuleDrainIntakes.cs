using UnityEngine;

using BDArmory.Utils;

namespace BDArmory.Damage
{
    public class ModuleDrainIntakes : PartModule
    {
        public float drainRate = 999;
        public float drainDuration = 20;
        private bool initialized = false;

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                drainDuration -= Time.deltaTime;
                if (drainDuration <= 0)
                {
                    using (var intake = VesselModuleRegistry.GetModules<ModuleResourceIntake>(vessel).GetEnumerator())
                        while (intake.MoveNext())
                        {
                            if (intake.Current == null) continue;
                            intake.Current.intakeEnabled = true;
                        }
                    part.RemoveModule(this);
                }
            }
            if (!initialized)
            {
                //Debug.Log("[BDArmory.ModuleDrainIntakes]: " + this.part.name + "choked!");
                initialized = true;
                using (var intake = VesselModuleRegistry.GetModules<ModuleResourceIntake>(vessel).GetEnumerator())
                    while (intake.MoveNext())
                    {
                        if (intake.Current == null) continue;
                        intake.Current.intakeEnabled = false;
                    }
            }
        }
    }
}

