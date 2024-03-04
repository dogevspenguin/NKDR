using System.Linq;
using UnityEngine;

using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Extensions;

namespace BDArmory.FX
{
    class FuelLeakFX : MonoBehaviour
    {
        Part parentPart;
        // string parentPartName = "";
        // string parentVesselName = "";
        public static ObjectPool CreateLeakFXPool(string modelPath)
        {
            var template = GameDatabase.Instance.GetModel(modelPath);
            var decal = template.AddComponent<FuelLeakFX>();
            template.SetActive(false);
            return ObjectPool.CreateObjectPool(template, 10, true, true);
        }

        public float drainRate = 1;
        private int fuelLeft = 0;
        public float lifeTime = 20;
        private float startTime;
        private float disableTime = -1;
        private float _highestEnergy = 1;

        PartResource fuel;
        PartResource ox;
        PartResource mp;
        ModuleEngines engine;
        private bool isSRB = false;
        KSPParticleEmitter[] pEmitters;

        void OnEnable()
        {
            if (parentPart == null)
            {
                gameObject.SetActive(false);
                return;
            }
            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDArmory.LeakFX]: Leak added to {parentPart.name}" + (parentPart.vessel != null ? $" on {parentPart.vessel.vesselName}" : ""));

            engine = parentPart.FindModuleImplementing<ModuleEngines>();
            var solid = parentPart.Resources.Where(pr => pr.resourceName == "SolidFuel").FirstOrDefault();
            if (engine != null)
            {
                if (solid != null)
                {
                    isSRB = true;
                }
            }
            BDArmorySetup.numberOfParticleEmitters++;
            startTime = Time.time;
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            Vector3 Gravity = FlightGlobals.getGeeForceAtPosition(transform.position);
            using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;

                    pe.Current.emit = true;
                    _highestEnergy = pe.Current.maxEnergy;
                    pe.Current.force = Gravity;
                    if (parentPart.vessel.InVacuum())
                    {
                        pe.Current.localVelocity = new Vector3(0, (float)parentPart.vessel.obt_speed, 0);
                        pe.Current.force = Vector3.zero;
                    }
                    EffectBehaviour.AddParticleEmitter(pe.Current);
                }
        }

        void OnDisable()
        {
            if (pEmitters != null) // Getting enabled when the parent part is null immediately disables it again before setting any of this up.
            {
                BDArmorySetup.numberOfParticleEmitters--;
                foreach (var pe in pEmitters)
                    if (pe != null)
                    {
                        pe.emit = false;
                        EffectBehaviour.RemoveParticleEmitter(pe);
                    }
            }
            parentPart = null;
            fuel = null;
            ox = null;
            mp = null;
            drainRate = 1;
        }

        void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy || !HighLogic.LoadedSceneIsFlight || BDArmorySetup.GameIsPaused)
            {
                return;
            }           
            if (parentPart.vessel.InVacuum()) transform.rotation = Quaternion.FromToRotation(Vector3.up, parentPart.vessel.obt_velocity.normalized);
            else transform.rotation = Quaternion.FromToRotation(Vector3.up, -FlightGlobals.getGeeForceAtPosition(transform.position));
            fuel = parentPart.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
            if (disableTime < 0) //only have fire do its stuff while burning and not during FX timeout
            {
                if (engine != null)
                {
                    if (engine.EngineIgnited && !isSRB)
                    {
                        if (fuel != null)
                        {
                            if (fuel.amount > 0)
                            {
                                parentPart.RequestResource("LiquidFuel", (double)(drainRate * Time.fixedDeltaTime));
                                fuelLeft++;
                            }
                        }
                    }
                }
                else
                {
                    if (fuel != null)
                    {
                        if (fuel.amount > 0)
                        {
                            //part.RequestResource("LiquidFuel", ((double)drainRate * Mathf.Clamp((float)fuel.amount, 40, 400) / Mathf.Clamp((float)fuel.maxAmount, 400, (float)fuel.maxAmount)) * Time.deltaTime);
                            //This draining from across vessel?  Trying alt method
                            fuel.amount -= ((double)drainRate * Mathf.Clamp((float)fuel.amount, 40, 400) / Mathf.Clamp((float)fuel.maxAmount, 400, (float)fuel.maxAmount)) * Time.fixedDeltaTime;
                            fuel.amount = Mathf.Clamp((float)fuel.amount, 0, (float)fuel.maxAmount);
                            fuelLeft++;
                        }
                    }
                    ox = parentPart.Resources.Where(pr => pr.resourceName == "Oxidizer").FirstOrDefault();
                    if (ox != null)
                    {
                        if (ox.amount > 0)
                        {
                            //part.RequestResource("Oxidizer", ((double)drainRate * Mathf.Clamp((float)ox.amount, 40, 400) / Mathf.Clamp((float)ox.maxAmount, 400, (float)ox.maxAmount) ) *  Time.deltaTime);
                            //more fuel = higher pressure, clamped at 400 since flow rate is constrained by outlet aperture, not fluid pressure
                            ox.amount -= ((double)drainRate * Mathf.Clamp((float)fuel.amount, 40, 400) / Mathf.Clamp((float)fuel.maxAmount, 400, (float)fuel.maxAmount)) * Time.fixedDeltaTime;
                            ox.amount = Mathf.Clamp((float)ox.amount, 0, (float)ox.maxAmount);
                            fuelLeft++;
                        }
                    }
                    mp = parentPart.Resources.Where(pr => pr.resourceName == "MonoPropellant").FirstOrDefault();
                    if (mp != null)
                    {
                        if (mp.amount >= 0)
                        {
                            //part.RequestResource("MonoPropellant", ((double)drainRate * Mathf.Clamp((float)mp.amount, 40, 400) / Mathf.Clamp((float)mp.maxAmount, 400, (float)mp.maxAmount)) * Time.deltaTime);
                            mp.amount -= ((double)drainRate * Mathf.Clamp((float)mp.amount, 40, 400) / Mathf.Clamp((float)mp.maxAmount, 400, (float)mp.maxAmount)) * Time.fixedDeltaTime;
                            mp.amount = Mathf.Clamp((float)mp.amount, 0, (float)mp.maxAmount);
                            fuelLeft++;
                        }
                    }
                }
                //if we want a vacuum BattleDamage option to produce (small amounts of) thrust from leaking tanks
                //if (disableTime < 0 && (fuelLeft > 0 && (lifeTime >= 0 && Time.time - startTime < lifeTime))) parentPart.Rigidbody.AddForce(transform.up * (drainRate / 5), ForceMode.Acceleration); //needs a quaternion to reverse per-frame rotation to face prograde/gravity
            }

            if (disableTime < 0 && (fuelLeft <= 0 || (lifeTime >= 0 && Time.time - startTime > lifeTime)))
            {
                disableTime = Time.time; //grab time when emission stops
                foreach (var pe in pEmitters)
                    if (pe != null)
                        pe.emit = false;
            }
            fuelLeft = 0;
            if (disableTime > 0 && Time.time - disableTime > _highestEnergy) //wait until last emitted particle has finished
            {
                Deactivate();
            }
        }
        public void AttachAt(Part hitPart, RaycastHit hit, Vector3 offset)
        {
            if (hitPart is null) return;
            parentPart = hitPart;
            // parentPartName = parentPart.name;
            // parentVesselName = parentPart.vessel.vesselName;
            transform.SetParent(hitPart.transform);
            transform.position = hit.point + offset;
            transform.rotation = (parentPart.vessel.situation == Vessel.Situations.ORBITING || parentPart.vessel.situation == Vessel.Situations.SUB_ORBITAL) ? Quaternion.FromToRotation(Vector3.up, parentPart.vessel.obt_velocity.normalized) : Quaternion.FromToRotation(Vector3.up, -FlightGlobals.getGeeForceAtPosition(transform.position));
            parentPart.OnJustAboutToDie += OnParentDestroy;
            parentPart.OnJustAboutToBeDestroyed += OnParentDestroy;
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // onVesselUnloaded event introduced in 1.11
                OnVesselUnloaded_1_11(true); // Catch unloading events too.
            gameObject.SetActive(true);
        }

        void OnParentDestroy()
        {
            if (parentPart is not null)
            {
                parentPart.OnJustAboutToDie -= OnParentDestroy;
                parentPart.OnJustAboutToBeDestroyed -= OnParentDestroy;
                Deactivate();
            }
        }

        void OnVesselUnloaded(Vessel vessel)
        {
            if (parentPart is not null && (parentPart.vessel is null || parentPart.vessel == vessel))
            {
                OnParentDestroy();
            }
            else if (parentPart is null)
            {
                Deactivate(); // Sometimes (mostly when unloading a vessel) the parent becomes null without triggering OnParentDestroy.
            }
        }

        void OnVesselUnloaded_1_11(bool addRemove) // onVesselUnloaded event introduced in 1.11
        {
            if (addRemove)
                GameEvents.onVesselUnloaded.Add(OnVesselUnloaded);
            else
                GameEvents.onVesselUnloaded.Remove(OnVesselUnloaded);
        }

        void Deactivate()
        {
            if (gameObject is not null && gameObject.activeSelf) // Deactivate even if a parent is already inactive.
            {
                disableTime = -1;
                parentPart = null;
                transform.parent = null; // Detach ourselves from the parent transform so we don't get destroyed if it does.
                gameObject.SetActive(false);
            }
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // onVesselUnloaded event introduced in 1.11
                OnVesselUnloaded_1_11(false);
        }

        void OnDestroy() // This shouldn't be happening except on exiting KSP, but sometimes they get destroyed instead of disabled!
        {
            // if (HighLogic.LoadedSceneIsFlight) Debug.LogError($"[BDArmory.FuelLeakFX]: FuelLeakFX on {parentPartName} ({parentVesselName}) was destroyed!");
            // Clean up emitters.
            if (pEmitters is not null && pEmitters.Any(pe => pe is not null))
            {
                BDArmorySetup.numberOfParticleEmitters--;
                foreach (var pe in pEmitters)
                    if (pe != null)
                    {
                        pe.emit = false;
                        EffectBehaviour.RemoveParticleEmitter(pe);
                    }
            }
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // onVesselUnloaded event introduced in 1.11
                OnVesselUnloaded_1_11(false);
        }
    }
}
