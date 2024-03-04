using UnityEngine;
using System.Collections;

using BDArmory.Utils;

namespace BDArmory.Extensions
{
    public class ClawExtension : PartModule
    {
        [KSPAction("Toggle Free Pivot")]
        public void AGToggleFreePivot(KSPActionParam param)
        {
            SetFreePivot(Toggle.Toggle);
        }

        [KSPAction("Enable Free Pivot")]
        public void AGEnableFreePivot(KSPActionParam param)
        {
            SetFreePivot(Toggle.On);
        }

        [KSPAction("Disble Free Pivot")]
        public void AGDisableFreePivot(KSPActionParam param)
        {
            SetFreePivot(Toggle.Off);
        }

        void SetFreePivot(Toggle state)
        {
            var claw = part.GetComponent<ModuleGrappleNode>();
            if (claw == null) return;
            if (claw.state != "Grappled") return;
            switch (state)
            {
                case Toggle.Toggle:
                    if (claw.IsLoose())
                        claw.LockPivot();
                    else
                        claw.SetLoose();
                    break;
                case Toggle.On:
                    claw.SetLoose();
                    break;
                case Toggle.Off:
                    claw.LockPivot();
                    break;
            }
        }

        [KSPAction("Enable Free Pivot When Grappled (10s)")]
        public void AGEnableFreePivotWhenGrappled(KSPActionParam param)
        {
            StartCoroutine(EnableFreePivotWhenGrappled());
        }

        IEnumerator EnableFreePivotWhenGrappled()
        {
            var claw = part.GetComponent<ModuleGrappleNode>();
            var wait = new WaitForFixedUpdate();
            var tic = Time.time;
            while (claw != null && (claw.state != "Grappled" || !claw.IsLoose()) && Time.time - tic < 10) // Abort after 10s
            {
                if (claw.state == "Grappled" && !claw.IsLoose()) claw.SetLoose();
                yield return wait;
            }
        }

        [KSPAction("Unlimited Pivot Range")]
        public void AGUnlimitedPivotRange(KSPActionParam param)
        {
            var claw = part.GetComponent<ModuleGrappleNode>();
            claw.pivotRange = 180f;
        }
    }
}