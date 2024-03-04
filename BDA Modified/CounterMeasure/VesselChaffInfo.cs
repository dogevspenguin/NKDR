using BDArmory.Extensions;
using UnityEngine;

namespace BDArmory.CounterMeasure
{
    public class VesselChaffInfo : MonoBehaviour
    {
        Vessel vessel;

        const float chaffMax = 500;
        const float chaffSubtractor = 120;
        const float speedRegenMult = 0.6f;
        const float minRegen = 40;
        const float maxRegen = 500;
        const float minMult = 0.1f;
        float chaffScalar = 500;

        void Start()
        {
            vessel = GetComponent<Vessel>();
            if (!vessel)
            {
                Debug.Log("[BDArmory.VesselChaffInfo]: VesselChaffInfo was added to an object with no vessel component");
                Destroy(this);
                return;
            }
            vessel.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
        }

        void OnDestroy()
        {
            if (vessel) vessel.OnJustAboutToBeDestroyed -= AboutToBeDestroyed;
        }

        void AboutToBeDestroyed()
        {
            Destroy(this);
        }

        public float GetChaffMultiplier()
        {
            return Mathf.Clamp(chaffScalar / chaffMax, minMult, 1f);
        }

        public void Chaff()
        {
            chaffScalar = Mathf.Clamp(chaffScalar - chaffSubtractor, 0, chaffMax);
        }

        void FixedUpdate()
        {
            float speedOrAccel = (!vessel.InVacuum()) ? (float)vessel.srfSpeed : Mathf.Abs((float)vessel.acceleration_immediate.magnitude);
            chaffScalar = Mathf.MoveTowards(chaffScalar, chaffMax,
                Mathf.Clamp(speedRegenMult * speedOrAccel, minRegen, maxRegen) * Time.fixedDeltaTime);
        }
    }
}
