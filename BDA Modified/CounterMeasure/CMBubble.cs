using System.Collections;
using UnityEngine;

using BDArmory.Utils;

namespace BDArmory.CounterMeasure
{
    public class CMBubble : MonoBehaviour
    {
        public Vector3 velocity;

        void OnEnable()
        {
            StartCoroutine(BubbleRoutine());
        }

        IEnumerator BubbleRoutine()
        {
            yield return new WaitForSecondsFixed(10);

            gameObject.SetActive(false);
        }

        void FixedUpdate()
        {
            //physics
            //atmospheric drag (stock)
            float simSpeedSquared = velocity.sqrMagnitude;
            Vector3 currPos = transform.position;
            float mass = 0.01f;
            float drag = 5f;
            Vector3 dragForce = (0.008f * mass) * drag * 0.5f * simSpeedSquared *
                                ((float)
                                FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPos),
                                    FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody) * 83) *
                                velocity.normalized;

            velocity -= (dragForce / mass) * Time.fixedDeltaTime;
            if (FlightGlobals.getAltitudeAtPos(transform.position) < -8) velocity -= (FlightGlobals.getGeeForceAtPosition(transform.position) / 2) * Time.fixedDeltaTime;
            transform.position += velocity * Time.fixedDeltaTime;
        }
        public static float RaycastBubblescreen(Ray ray)
        {
            float fieldStrength = 1;
            if (!CMDropper.bubblePool)
            {
                return fieldStrength;
            }
            float falloffFactor = 0.4f;
            for (int i = 0; i < CMDropper.bubblePool.size; i++)
            {
                Transform bubbleTf = CMDropper.bubblePool.GetPooledObject(i).transform;
                if (bubbleTf.gameObject.activeInHierarchy)
                {
                    Plane bubblePlane = new Plane((ray.origin - bubbleTf.position).normalized, bubbleTf.position);
                    float enter;
                    if (bubblePlane.Raycast(ray, out enter))
                    {
                        float dist = (ray.GetPoint(enter) - bubbleTf.position).sqrMagnitude;
                        if (dist < 24 * 24)
                        {
                            fieldStrength -= 1 * falloffFactor;
                            falloffFactor *= 0.7f;
                        }
                    }
                }
            }

            return Mathf.Clamp01(fieldStrength);
        }
    }
}
