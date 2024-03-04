using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.GameModes
{
    public class BattleDamageTracker : MonoBehaviour
    {
        public float oldDamagePercent = 1;
        public double origIntakeArea = -1;

        public bool isSRB = false;
        public bool SRBFuelled = false;
        public Part Part { get; set; }
        // {
        //     get
        //     {
        //         return Part; // FIXME If you want custom getters and setters then you need your own backing variable, e.g., private Part _Part;, auto-getters and setters will make the backing variable automatically.
        //     }
        //     set
        //     {
        //         Part = value;
        //     }
        // }

        void Awake()
        {
            if (!Part)
            {
                Part = GetComponent<Part>();
            }
            if (!Part)
            {
                //Debug.Log ("[BDArmory]: BDTracker attached to non-part, removing");
                Destroy(this);
                return;
            }
            //destroy this there's already one attached
            foreach (var prevTracker in Part.gameObject.GetComponents<BattleDamageTracker>())
            {
                if (prevTracker != this)
                {
                    Destroy(this);
                    return;
                }
            }
            foreach (var engine in Part.GetComponentsInChildren<ModuleEngines>())
            {
                if (!engine.allowShutdown && engine.throttleLocked)
                {
                    isSRB = true;
                    using (IEnumerator<PartResource> resources = Part.Resources.GetEnumerator())
                        while (resources.MoveNext())
                        {
                            if (resources.Current == null) continue;
                            if (resources.Current.resourceName.Contains("SolidFuel"))
                            {
                                if (resources.Current.amount > 1d)
                                {
                                    SRBFuelled = true;
                                }
                            }
                        }
                }
            }
            Part.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
        }
        void OnDestroy()
        {
            Part.OnJustAboutToBeDestroyed -= AboutToBeDestroyed;
        }
        void AboutToBeDestroyed()
        {
            Destroy(this);
        }

    }
}
