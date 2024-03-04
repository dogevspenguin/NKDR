using UnityEngine;

using BDArmory.Settings;
using BDArmory.Utils;

namespace BDArmory.FX
{
    public class ShellCasing : MonoBehaviour
    {
        public float startTime;
        public Vector3 initialV;

        Vector3 velocity;
        Vector3 angularVelocity;

        float atmDensity;
        const int collisionLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.Unknown19 | LayerMasks.Wheels); // Why 19?

        void OnEnable()
        {
            startTime = Time.time;
            velocity = initialV;
            velocity += transform.rotation *
                        new Vector3(Random.Range(-.1f, .1f), Random.Range(-.1f, .1f),
                            Random.Range(6f, 8f));
            angularVelocity =
                new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f),
                    Random.Range(-10f, 10f)) * 10;

            atmDensity =
                (float)
                FlightGlobals.getAtmDensity(
                    FlightGlobals.getStaticPressure(transform.position, FlightGlobals.currentMainBody),
                    FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody);
        }

        void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;
            if (Time.time - startTime > 2)
            {
                gameObject.SetActive(false);
                return;
            }

            //gravity
            velocity += FlightGlobals.getGeeForceAtPosition(transform.position) * TimeWarp.fixedDeltaTime
                + Krakensbane.GetLastCorrection();

            //drag
            velocity -= 0.005f * (velocity + BDKrakensbane.FrameVelocityV3f) * atmDensity;

            transform.rotation *= Quaternion.Euler(angularVelocity * TimeWarp.fixedDeltaTime);
            transform.position += velocity * TimeWarp.deltaTime;

            if (BDArmorySettings.SHELL_COLLISIONS)
            {
                RaycastHit hit;
                if (Physics.Linecast(transform.position, transform.position + velocity * Time.fixedDeltaTime, out hit, collisionLayerMask))
                {
                    velocity = Vector3.Reflect(velocity, hit.normal);
                    velocity *= 0.55f;
                    velocity = Quaternion.AngleAxis(Random.Range(0f, 90f), Random.onUnitSphere) *
                               velocity;
                }
            }
        }
    }
}
