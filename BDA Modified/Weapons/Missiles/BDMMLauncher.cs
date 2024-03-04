using UnityEngine;

using BDArmory.Control;
using BDArmory.Utils;

namespace BDArmory.Weapons.Missiles
{
    public class BDMMLauncher : PartModule
    {
        public override void OnStart(StartState state)
        {
            part.force_activate();
        }

        [KSPEvent(name = "Fire", guiActive = true, active = true)]
        public void Fire()
        {
            GameObject target = null;
            if (vessel.targetObject != null) target = vessel.targetObject.GetVessel().gameObject;

            part.decouple(0);

            foreach (BDModularGuidance bdmm in VesselModuleRegistry.GetModules<BDModularGuidance>(vessel))
            {
                bdmm.HasFired = true;
                //bdmm.target = target;
            }
            // foreach (BDExplosivePart bde in VesselModuleRegistry.GetModules<BDExplosivePart>(vessel))
            // {
            //     //bde.target = target;
            // }
        }
    }
}
