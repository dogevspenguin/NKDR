﻿using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

using BDArmory.Armor;
using BDArmory.Competition;
using BDArmory.Damage;
using BDArmory.Extensions;
using BDArmory.FX;
using BDArmory.Weapons;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using static BDArmory.Bullets.PooledBullet;

namespace BDArmory.Bullets
{
    public class PooledRocket : MonoBehaviour
    {
        public RocketInfo rocket; //get tracers, expFX urls moved to BulletInfo
                                  //Seeker/homing rocket code (Image-Recognition tracking?)

        public Transform spawnTransform;
        public Vessel sourceVessel;
        public Part sourceWeapon;
        public string sourceVesselName;
        public string team;
        public string rocketName;
        public float rocketMass;
        public float caliber;
        public float apMod;
        public float thrust;
        private Vector3 thrustVector;
        private Vector3 dragVector;
        public float thrustTime;
        public bool shaped;
        public float timeToDetonation;
        float armingTime;
        public bool flak;
        public bool concussion;
        public bool gravitic;
        public bool EMP;
        public bool choker;
        public bool thief;
        public float massMod = 0;
        public float impulse = 0;
        public bool incendiary;
        public float detonationRange;
        public float tntMass;
        public bool beehive;
        public string subMunitionType;
        bool explosive = true;
        public float bulletDmgMult = 1;
        public float dmgMult = 1;
        public float blastRadius = 0;
        public float randomThrustDeviation = 0.05f;
        public float massScalar = 0.012f;
        private float HERatio = 0.1f;
        public string explModelPath;
        public string explSoundPath;

        public bool nuclear;
        public string flashModelPath;
        public string shockModelPath;
        public string blastModelPath;
        public string plumeModelPath;
        public string debrisModelPath;
        public string blastSoundPath;

        public string rocketSoundPath;

        float startTime;
        public float lifeTime;

        Vector3 prevPosition;
        public Vector3 currentPosition { get { return _currentPosition; } set { _currentPosition = value; transform.position = value; } } // Local alias for transform.position speeding up access by around 100x. Only use during FixedUpdates, as it may not be up-to-date otherwise.
        Vector3 _currentPosition = default;
        Vector3 startPosition;
        bool startUnderwater = false;
        Ray RocketRay;
        private float impactVelocity;
        public Vector3 currentVelocity = Vector3.zero; // Current real velocity w/o offloading

        public bool hasPenetrated = false;
        public bool hasDetonated = false;
        public int penTicker = 0;
        private Part CurrentPart = null;
        private const int collisionLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.EVA | LayerMasks.Unknown19 | LayerMasks.Unknown23 | LayerMasks.Wheels); // Why 19 and 23?
        private const int explosionLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.Unknown19 | LayerMasks.Wheels); // Why 19 and not EVA?

        private float distanceFromStart = 0;

        //bool isThrusting = true;
        public bool isAPSprojectile = false;
        public bool isSubProjectile = false;
        public PooledRocket tgtRocket = null;
        public PooledBullet tgtShell = null;

        Rigidbody rb;
        public Rigidbody parentRB;

        KSPParticleEmitter[] pEmitters;
        BDAGaplessParticleEmitter[] gpEmitters;

        float randThrustSeed;

        public AudioSource audioSource;

        static RaycastHit[] hits = new RaycastHit[10];
        static Collider[] proximityOverlapSphereColliders = new Collider[10];
        static Collider[] detonateOverlapSphereColliders = new Collider[10];
        void OnEnable()
        {
            BDArmorySetup.numberOfParticleEmitters++;
            currentPosition = transform.position; // In case something sets transform.position instead of currentPosition.

            rb = gameObject.AddOrGetComponent<Rigidbody>();

            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();

            using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    if (FlightGlobals.getStaticPressure(currentPosition) == 0 && pe.Current.useWorldSpace)
                    {
                        pe.Current.emit = false;
                    }

                    else if (pe.Current.useWorldSpace)
                    {
                        BDAGaplessParticleEmitter gpe = pe.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                        gpe.rb = rb;
                        gpe.emit = true;
                    }

                    else
                    {
                        pe.Current.emit = true;
                        EffectBehaviour.AddParticleEmitter(pe.Current);
                    }
                }
            gpEmitters = gameObject.GetComponentsInChildren<BDAGaplessParticleEmitter>();

            prevPosition = currentPosition;
            startPosition = currentPosition;
            transform.rotation = transform.parent.rotation;
            startTime = Time.time;
            armingTime = isSubProjectile ? 0 : BDAMath.Sqrt(4 * blastRadius * rocketMass / thrust); // d = a/2 * t^2 for initial 0 relative velocity
            if (FlightGlobals.getAltitudeAtPos(currentPosition) < 0)
            {
                startUnderwater = true;
            }
            else
                startUnderwater = false;
            massScalar = 0.012f / rocketMass;

            rb.mass = rocketMass;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            currentVelocity = Vector3.zero;
            if (!FlightGlobals.RefFrameIsRotating) rb.useGravity = false;

            rb.useGravity = false;

            randThrustSeed = UnityEngine.Random.Range(0f, 100f);
            thrustVector = new Vector3(0, 0, thrust);
            dragVector = new Vector3();
            SetupAudio();

            // Log rockets fired.
            if (sourceVessel)
            {
                sourceVesselName = sourceVessel.GetName(); // Set the source vessel name as the vessel might have changed its name or died by the time the rocket hits.
                BDACompetitionMode.Instance.Scores.RegisterRocketFired(sourceVesselName);
            }
            else
            {
                sourceVesselName = null;
            }
            if (tntMass <= 0)
            {
                explosive = false;
            }
            if (explosive)
            {
                HERatio = Mathf.Clamp(tntMass / ((rocketMass * 1000) < tntMass ? tntMass * 1.25f : (rocketMass * 1000)), 0.01f, 0.95f);
            }
            else
            {
                HERatio = 0;
            }
            if (caliber >= BDArmorySettings.APS_THRESHOLD) //if (caliber > 60)
            {
                BDATargetManager.FiredRockets.Add(this);
            }
            if (nuclear)
            {
                var nuke = sourceWeapon.FindModuleImplementing<BDModuleNuke>();
                if (nuke == null)
                {
                    flashModelPath = BDModuleNuke.defaultflashModelPath;
                    shockModelPath = BDModuleNuke.defaultShockModelPath;
                    blastModelPath = BDModuleNuke.defaultBlastModelPath;
                    plumeModelPath = BDModuleNuke.defaultPlumeModelPath;
                    debrisModelPath = BDModuleNuke.defaultDebrisModelPath;
                    blastSoundPath = BDModuleNuke.defaultBlastSoundPath;
                }
                else
                {
                    flashModelPath = nuke.flashModelPath;
                    shockModelPath = nuke.shockModelPath;
                    blastModelPath = nuke.blastModelPath;
                    plumeModelPath = nuke.plumeModelPath;
                    debrisModelPath = nuke.debrisModelPath;
                    blastSoundPath = nuke.blastSoundPath;
                }
            }
        }

        void OnDisable()
        {
            BDArmorySetup.OnVolumeChange -= UpdateVolume;
            BDArmorySetup.numberOfParticleEmitters--;
            foreach (var gpe in gpEmitters)
                if (gpe != null)
                {
                    gpe.emit = false;
                }
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
            sourceVessel = null;
            sourceVesselName = null;
            spawnTransform = null;
            CurrentPart = null;
            if (caliber >= BDArmorySettings.APS_THRESHOLD) //if (caliber > 60)
            {
                BDATargetManager.FiredRockets.Remove(this);
            }
            isAPSprojectile = false;
            tgtRocket = null;
            tgtShell = null;
            rb.isKinematic = true;
        }

        void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            currentPosition = transform.position; // Adjust our local copy for any adjustments that the physics engine has made.
            //floating origin and velocity offloading corrections
            if (BDKrakensbane.IsActive)
            {
                currentPosition -= BDKrakensbane.FloatingOriginOffsetNonKrakensbane;
                prevPosition -= BDKrakensbane.FloatingOriginOffsetNonKrakensbane;
                startPosition -= BDKrakensbane.FloatingOriginOffsetNonKrakensbane;
            }
            distanceFromStart = Vector3.Distance(currentPosition, startPosition);

            if (transform.parent != null && parentRB)
            {
                transform.parent = null;
                rb.isKinematic = false;
                rb.velocity = parentRB.velocity + BDKrakensbane.FrameVelocityV3f;
            }

            if (rb && !rb.isKinematic)
            {
                //physics
                if (FlightGlobals.RefFrameIsRotating)
                {
                    rb.velocity += FlightGlobals.getGeeForceAtPosition(currentPosition) * Time.fixedDeltaTime;
                }

                //guidance and attitude stabilisation scales to atmospheric density.
                float atmosMultiplier =
                    Mathf.Clamp01((float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currentPosition),
                                      FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody));

                //model transform. always points prograde
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(rb.velocity, transform.up),
                    Mathf.Clamp01(atmosMultiplier * 2.5f) * (0.5f * (Time.time - startTime)) * 50 * Time.fixedDeltaTime);


                if (Time.time - startTime < thrustTime)
                {
                    thrustVector.x = randomThrustDeviation * (1 - (Mathf.PerlinNoise(4 * Time.time, randThrustSeed) * 2)) / massScalar;//this needs to scale w/ rocket mass, or light projectiles will be 
                    thrustVector.y = randomThrustDeviation * (1 - (Mathf.PerlinNoise(randThrustSeed, 4 * Time.time) * 2)) / massScalar;//far more affected than heavier ones
                    rb.AddRelativeForce(thrustVector);
                }//0.012/rocketmass - use .012 as baseline, it's the mass of the hydra, which the randomTurstdeviation was originally calibrated for
                if (BDArmorySettings.BULLET_WATER_DRAG)
                {
                    if (FlightGlobals.getAltitudeAtPos(currentPosition) < 0)
                    {
                        //atmosMultiplier *= 83.33f;
                        dragVector.z = -(0.5f * 1 * (rb.velocity.magnitude * rb.velocity.magnitude) * 0.5f * ((Mathf.PI * caliber * caliber * 0.25f) / 1000000)) * TimeWarp.fixedDeltaTime;
                        rb.AddRelativeForce(dragVector); //this is going to throw off aiming code, but you aren't going to hit anything with rockets underwater anyway
                    }
                    //dragVector.z = -(0.5f * (atmosMultiplier * 0.012f) * (rb.velocity.magnitude * rb.velocity.magnitude) * 0.5f * ((Mathf.PI * caliber * caliber * 0.25f) / 1000000))*TimeWarp.fixedDeltaTime;
                    //rb.AddRelativeForce(dragVector);
                    //Debug.Log("[ROCKETDRAG] current vel: " + rb.velocity.magnitude.ToString("0.0") + "; current dragforce: " + dragVector.magnitude + "; current atm density: " + atmosMultiplier.ToString("0.00"));
                }
                currentVelocity = rb.velocity; // The rb.velocity is w/o offloading here, since rockets aren't vessels.
            }

            if (Time.time - startTime > thrustTime)
            {
                using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                    while (pe.MoveNext())
                    {
                        if (pe.Current == null) continue;
                        pe.Current.emit = false;
                    }
                using (var gpe = gpEmitters.AsEnumerable().GetEnumerator())
                    while (gpe.MoveNext())
                    {
                        if (gpe.Current == null) continue;
                        gpe.Current.emit = false;
                    }
                if (audioSource)
                {
                    audioSource.loop = false;
                    audioSource.Stop();
                }
            }

            #region Collision detection
            // Note: This is performing hit detection for the previous frame, not the current one.
            hasPenetrated = true;
            hasDetonated = false;
            penTicker = 0;

            float dist = (currentPosition - prevPosition).magnitude;
            RocketRay = new Ray(prevPosition, currentPosition - prevPosition);
            var hitCount = Physics.RaycastNonAlloc(RocketRay, hits, dist, collisionLayerMask);
            if (hitCount == hits.Length) // If there's a whole bunch of stuff in the way (unlikely), then we need to increase the size of our hits buffer.
            {
                hits = Physics.RaycastAll(RocketRay, dist, collisionLayerMask);
                hitCount = hits.Length;
            }
            if (hitCount > 0)
            {
                var orderedHits = hits.Take(hitCount).OrderBy(x => x.distance);

                using (var hitsEnu = orderedHits.GetEnumerator())
                {
                    while (hitsEnu.MoveNext())
                    {
                        if (!hasPenetrated || hasDetonated) break;

                        RaycastHit hit = hitsEnu.Current;
                        Part hitPart = null;
                        KerbalEVA hitEVA = null;

                        try
                        {
                            hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                            hitEVA = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                        }
                        catch (NullReferenceException e)
                        {
                            Debug.LogWarning("[BDArmory.PooledRocket]:NullReferenceException for Kinetic Hit: " + e.Message);
                            return;
                        }

                        if (hitPart != null && ProjectileUtils.IsIgnoredPart(hitPart)) continue; // Ignore ignored parts.
                        if (hitPart != null && (hitPart == CurrentPart && ProjectileUtils.IsArmorPart(CurrentPart))) continue; //only have bullet hit armor panels once - no back armor to hit if penetration

                        CurrentPart = hitPart;
                        if (hitEVA != null)
                        {
                            hitPart = hitEVA.part;
                            // relative velocity, separate from the below statement, because the hitpart might be assigned only above
                            if (hitPart.rb != null)
                                impactVelocity = (rb.velocity - (hitPart.rb.velocity + BDKrakensbane.FrameVelocityV3f)).magnitude;
                            else
                                impactVelocity = rb.velocity.magnitude;
                            if (dmgMult < 0)
                            {
                                hitPart.AddInstagibDamage();
                            }
                            else
                            {
                                ProjectileUtils.ApplyDamage(hitPart, hit, dmgMult, 1, caliber, rocketMass * 1000, impactVelocity, bulletDmgMult, distanceFromStart, explosive, incendiary, false, sourceVessel, rocketName, team, ExplosionSourceType.Rocket, true, true, true);
                            }
                            ResourceUtils.StealResources(hitPart, sourceVessel, thief);
                            Detonate(hit.point, false, hitPart);
                            return;
                        }

                        if (hitPart != null && hitPart.vessel == sourceVessel) continue;  //avoid autohit;

                        Vector3 impactVector = rb.velocity;
                        if (hitPart != null && hitPart.rb != null)
                            // using relative velocity vector instead of just rocket velocity
                            // since KSP vessels can easily be moving faster than rockets
                            impactVector = rb.velocity - (hitPart.rb.velocity + BDKrakensbane.FrameVelocityV3f);

                        float hitAngle = Vector3.Angle(impactVector, -hit.normal);

                        if (ProjectileUtils.CheckGroundHit(hitPart, hit, caliber))
                        {
                            if (!BDArmorySettings.PAINTBALL_MODE) ProjectileUtils.CheckBuildingHit(hit, rocketMass * 1000, rb.velocity, bulletDmgMult);
                            Detonate(hit.point, false);
                            return;
                        }

                        impactVelocity = impactVector.magnitude;
                        if (gravitic)
                        {
                            var ME = hitPart.FindModuleImplementing<ModuleMassAdjust>();
                            if (ME == null)
                            {
                                ME = (ModuleMassAdjust)hitPart.AddModule("ModuleMassAdjust");
                            }
                            ME.massMod += massMod;
                            ME.duration += BDArmorySettings.WEAPON_FX_DURATION;
                        }
                        if (concussion && hitPart.rb != null || BDArmorySettings.PAINTBALL_MODE)
                        {
                            if (concussion && hitPart.rb != null)
                            {
                                hitPart.rb.AddForceAtPosition(impactVector.normalized * impulse, hit.point, ForceMode.Acceleration);
                            }
                            BDACompetitionMode.Instance.Scores.RegisterRocketStrike(sourceVesselName, hitPart.vessel.GetName());
                            Detonate(hit.point, false, hitPart);
                            hasDetonated = true;
                            return; //impulse rounds shouldn't penetrate/do damage
                        }
                        float anglemultiplier = (float)Math.Cos(Math.PI * hitAngle / 180.0);

                        float thickness = ProjectileUtils.CalculateThickness(hitPart, anglemultiplier);
                        float penetration = 0;
                        float penetrationFactor = 0;
                        var Armor = hitPart.FindModuleImplementing<HitpointTracker>();
                        if (Armor != null)
                        {
                            float Ductility = Armor.Ductility;
                            float hardness = Armor.Hardness;
                            float Strength = Armor.Strength;
                            float safeTemp = Armor.SafeUseTemp;
                            float Density = Armor.Density;
                            float vFactor = Armor.vFactor;
                            float muParam1 = Armor.muParam1;
                            float muParam2 = Armor.muParam2;
                            float muParam3 = Armor.muParam3;

                            if (hitPart.skinTemperature > safeTemp) //has the armor started melting/denaturing/whatever?
                            {
                                //vFactor *= 1/(1.25f*0.75f-0.25f*0.75f*0.75f);
                                vFactor *= 1.25490196078f; // Uses the above equation but just calculated out.
                                                           // The equation 1/(1.25*x-0.25*x^2) approximates the effect of changing yield strength
                                                           // by a factor of x
                                if (hitPart.skinTemperature > safeTemp * 1.5f)
                                {
                                    vFactor *= 1.77777777778f; // Same as used above, but here with x = 0.5. Maybe this should be
                                                               // some kind of a curve?
                                }
                            }

                            int armorType = (int)Armor.ArmorTypeNum;
                            if (BDArmorySettings.DEBUG_ARMOR)
                            {
                                Debug.Log("[BDArmory.PooledRocket]: ArmorVars found: Strength : " + Strength + "; Ductility: " + Ductility + "; Hardness: " + hardness + "; MaxTemp: " + safeTemp + "; Density: " + Density);
                            }
                            float bulletEnergy = ProjectileUtils.CalculateProjectileEnergy(rocketMass * 1000, impactVelocity);
                            float armorStrength = ProjectileUtils.CalculateArmorStrength(caliber, thickness, Ductility, Strength, Density, safeTemp, hitPart);
                            //calculate bullet deformation
                            float newCaliber = ProjectileUtils.CalculateDeformation(armorStrength, bulletEnergy, caliber, impactVelocity, hardness, Density, HERatio, 1, false);
                            //calculate penetration
                            /*if (Ductility > 0.05)
                            {*/
                            penetration = ProjectileUtils.CalculatePenetration(caliber, impactVelocity, rocketMass * 1000f, apMod, Strength, vFactor, muParam1, muParam2, muParam3);
                            /*}
                            else
                            {
                                penetration = ProjectileUtils.CalculateCeramicPenetration(caliber, newCaliber, rocketMass * 1000, impactVelocity, Ductility, Density, Strength, thickness, 1);
                            }*/

                            caliber = newCaliber; //update bullet with new caliber post-deformation(if any)
                            penetrationFactor = ProjectileUtils.CalculateArmorPenetration(hitPart, penetration, thickness);

                            var RA = hitPart.FindModuleImplementing<ModuleReactiveArmor>();
                            if (RA != null)
                            {
                                if (penetrationFactor > 1)
                                {
                                    float thicknessModifier = RA.armorModifier;
                                    {
                                        if (RA.NXRA) //non-explosive RA, always active
                                        {
                                            thickness *= thicknessModifier;
                                        }
                                        else
                                        {
                                            if (caliber >= RA.sensitivity) //big enough round to trigger RA
                                            {
                                                thickness *= thicknessModifier;
                                                if (tntMass <= 0) //non-explosive impact
                                                {
                                                    RA.UpdateSectionScales(); //detonate RA section
                                                                              //explosive impacts handled in ExplosionFX
                                                }
                                            }
                                        }
                                    }
                                }
                                penetrationFactor = ProjectileUtils.CalculateArmorPenetration(hitPart, penetration, thickness); //RA stop round?
                            }
                            else ProjectileUtils.CalculateArmorDamage(hitPart, penetrationFactor, caliber, hardness, Ductility, Density, impactVelocity, sourceVessel.GetName(), ExplosionSourceType.Rocket, armorType);

                            //calculate return bullet post-pen vel
                            //calculate armor damage
                            //FIXME later - if doing bullet style armor penetrtion, then immplement armor penetration, and let AP/kinetic warhead rockets (over?)penetrate parts
                        }
                        else
                        {
                            Debug.Log("[BDArmory.PooledRocket]: ArmorVars not found; hitPart null");
                        }
                        if (penetration > thickness)
                        {
                            rb.velocity = rb.velocity * (float)Math.Sqrt(thickness / penetration);
                            if (penTicker > 0) rb.velocity *= 0.55f;
                        }

                        if (penetrationFactor > 1)
                        {
                            hasPenetrated = true;

                            bool viableBullet = ProjectileUtils.CalculateBulletStatus(rocketMass * 1000, caliber);
                            if (dmgMult < 0)
                            {
                                hitPart.AddInstagibDamage();
                            }
                            else
                            {
                                float cockpitPen = (float)(16f * impactVelocity * BDAMath.Sqrt(rocketMass) / BDAMath.Sqrt(caliber));
                                if (cockpitPen > Mathf.Max(20 / anglemultiplier, 1))
                                    ProjectileUtils.ApplyDamage(hitPart, hit, dmgMult, penetrationFactor, caliber, rocketMass * 1000, impactVelocity, bulletDmgMult, distanceFromStart, explosive, incendiary, false, sourceVessel, rocketName, team, ExplosionSourceType.Rocket, penTicker > 0 ? false : true, penTicker > 0 ? false : true, (cockpitPen > Mathf.Max(20 / anglemultiplier, 1)) ? true : false);
                                if (!explosive)
                                {
                                    BDACompetitionMode.Instance.Scores.RegisterRocketStrike(sourceVesselName, hitPart.vessel.GetName()); //if non-explosive hit, add rocketstrike, else ExplosionFX adds rocketstrike from HE detonation
                                }
                            }
                            ResourceUtils.StealResources(hitPart, sourceVessel, thief);

                            penTicker += 1;
                            //ProjectileUtils.CheckPartForExplosion(hitPart);

                            if (explosive || !viableBullet)
                            {
                                currentPosition += (rb.velocity * Time.fixedDeltaTime) / 3;

                                Detonate(currentPosition, false, hitPart); //explode inside part
                                hasDetonated = true;
                            }
                        }
                        else // stopped by armor
                        {
                            if (hitPart.rb != null && hitPart.rb.mass > 0)
                            {
                                float forceAverageMagnitude = impactVelocity * impactVelocity *
                                                      (1f / hit.distance) * (rocketMass * 1000);

                                float accelerationMagnitude =
                                    forceAverageMagnitude / (hitPart.vessel.GetTotalMass() * 1000);

                                hitPart.rb.AddForceAtPosition(impactVector.normalized * accelerationMagnitude, hit.point, ForceMode.Acceleration);

                                if (BDArmorySettings.DEBUG_WEAPONS)
                                    Debug.Log("[BDArmory.PooledRocket]: Force Applied " + Math.Round(accelerationMagnitude, 2) + "| Vessel mass in kgs=" + hitPart.vessel.GetTotalMass() * 1000 + "| rocket effective mass =" + rocketMass * 1000);
                            }

                            hasPenetrated = false;
                            //ProjectileUtils.ApplyDamage(hitPart, hit, 1, penetrationFactor, caliber, rocketMass * 1000, impactVelocity, bulletDmgMult, distanceFromStart, explosive, incendiary, false, sourceVessel, rocketName, team);
                            //not going to do ballistic damage if stopped by armor
                            ProjectileUtils.CalculateShrapnelDamage(hitPart, hit, caliber, tntMass, 0, sourceVesselName, ExplosionSourceType.Rocket, (rocketMass * 1000), penetrationFactor);
                            //the warhead exploding, on the other hand...
                            Detonate(hit.point, false, hitPart);
                            hasDetonated = true;
                        }

                        if (penTicker >= 2)
                        {
                            Detonate(hit.point, false, hitPart);
                            return;
                        }

                        if (rb.velocity.magnitude <= 100 && hasPenetrated && (Time.time - startTime > thrustTime))
                        {
                            if (BDArmorySettings.DEBUG_WEAPONS)
                            {
                                Debug.Log("[BDArmory.PooledRocket]: Rocket ballistic velocity too low, stopping");
                            }
                            Detonate(hit.point, false, hitPart);
                            return;
                        }
                        if (!hasPenetrated || hasDetonated) break;
                    }
                }
            }
            #endregion

            if (BDArmorySettings.BULLET_WATER_DRAG)
            {
                if (FlightGlobals.getAltitudeAtPos(currentPosition) > 0 && startUnderwater)
                {
                    startUnderwater = false;
                    FXMonger.Splash(currentPosition, caliber);
                }
                if (FlightGlobals.getAltitudeAtPos(currentPosition) <= 0 && !startUnderwater)
                {
                    if (tntMass > 0) //look into fuze options similar to bullets?
                    {
                        Detonate(currentPosition, false);
                    }
                    FXMonger.Splash(currentPosition, caliber);
                }
            }

            if (Time.time - startTime > lifeTime)
            {
                Detonate(currentPosition, true, airDetonation: true);
            }
            if (beehive && Time.time - startTime >= timeToDetonation - 1)
            {
                Detonate(currentPosition, false, airDetonation: true);
            }
            if (ProximityAirDetonation())
            {
                Detonate(currentPosition, false, airDetonation: true);
            }
            prevPosition = currentPosition;
        }

        private bool ProximityAirDetonation()
        {
            if (isAPSprojectile && (tgtShell != null || tgtRocket != null))
            {
                if (currentPosition.CloserToThan(tgtShell != null ? tgtShell.currentPosition : tgtRocket.currentPosition, detonationRange / 2))
                {
                    if (BDArmorySettings.DEBUG_WEAPONS)
                        Debug.Log("[BDArmory.PooledRocket]: rocket proximity to APS target | Distance overlap = " + detonationRange + "| tgt name = " + tgtShell != null ? tgtShell.name : tgtRocket.name);
                    return true;
                }
            }

            if (Time.time - startTime < armingTime) return false;

            if (!(((explosive || nuclear) && tntMass > 0) || beehive)) return false;

            if (flak)
            {
                var overlapSphereColliderCount = Physics.OverlapSphereNonAlloc(currentPosition, detonationRange, proximityOverlapSphereColliders, explosionLayerMask);
                if (overlapSphereColliderCount == proximityOverlapSphereColliders.Length)
                {
                    proximityOverlapSphereColliders = Physics.OverlapSphere(currentPosition, detonationRange, explosionLayerMask);
                    overlapSphereColliderCount = proximityOverlapSphereColliders.Length;
                }
                using (var hitsEnu = proximityOverlapSphereColliders.Take(overlapSphereColliderCount).GetEnumerator())
                {
                    while (hitsEnu.MoveNext())
                    {
                        if (hitsEnu.Current == null) continue;
                        try
                        {
                            Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
                            if (partHit == null || partHit.vessel == null) continue;
                            if (partHit.vessel == sourceVessel) continue;
                            if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                            var aName = sourceVesselName; //proxi detonated rocket scoring
                            var tName = partHit.vessel.GetName();

                            BDACompetitionMode.Instance.Scores.RegisterRocketHit(aName, tName, 1);

                            if (BDArmorySettings.DEBUG_WEAPONS)
                                Debug.Log($"[BDArmory.PooledRocket]: rocket proximity sphere hit | Distance overlap = {detonationRange} | Part name = {partHit.name} on {partHit.vessel.vesselName}");
                            return true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[BDArmory.PooledRocket]: Exception thrown in ProximityAirDetonation: " + e.Message + "\n" + e.StackTrace);
                        }
                    }
                }
            }
            return false;
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (BDArmorySetup.GameIsPaused || (Time.time - startTime > thrustTime))
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
                else
                {
                    if (!audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                }
            }
        }

        void Detonate(Vector3 pos, bool missed, Part hitPart = null, bool airDetonation = false)
        {
            if (!missed)
            {
                if (beehive)
                {
                    BeehiveDetonation();
                }
                else
                {
                    if (tntMass > 0)
                    {
                        Vector3 direction = default(Vector3);
                        if (shaped)
                        {
                            direction = rb.velocity.normalized;
                            //direction = transform.forward //ideal, but no guarantee that mod rockets have correct transform orientation
                        }
                        if (gravitic)
                        {
                            var overlapSphereColliderCount = Physics.OverlapSphereNonAlloc(currentPosition, blastRadius, detonateOverlapSphereColliders, explosionLayerMask);
                            if (overlapSphereColliderCount == detonateOverlapSphereColliders.Length)
                            {
                                detonateOverlapSphereColliders = Physics.OverlapSphere(currentPosition, blastRadius, explosionLayerMask);
                                overlapSphereColliderCount = detonateOverlapSphereColliders.Length;
                            }
                            using (var hitsEnu = detonateOverlapSphereColliders.Take(overlapSphereColliderCount).GetEnumerator())
                            {
                                while (hitsEnu.MoveNext())
                                {
                                    if (hitsEnu.Current == null) continue;

                                    Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
                                    if (partHit == null) continue;
                                    if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                                    float distance = Vector3.Distance(currentPosition, partHit.transform.position);
                                    if (gravitic)
                                    {
                                        if (partHit.mass > 0)
                                        {
                                            var ME = partHit.vessel.rootPart.FindModuleImplementing<ModuleMassAdjust>();
                                            if (ME == null)
                                            {
                                                ME = (ModuleMassAdjust)partHit.vessel.rootPart.AddModule("ModuleMassAdjust");
                                            }
                                            ME.massMod += (massMod * (1 - (distance / blastRadius))); //this way craft at edge of blast might only get disabled instead of bricked
                                            ME.duration += (BDArmorySettings.WEAPON_FX_DURATION * (1 - (distance / blastRadius))); //can bypass EMP damage cap
                                        }
                                    }
                                }
                            }
                        }
                        if (incendiary)
                        {
                            for (int f = 0; f < 20; f++) //throw 20 random raytraces out in a sphere and see what gets tagged
                            {
                                Ray LoSRay = new Ray(currentPosition, VectorUtils.GaussianDirectionDeviation(transform.forward, 170));
                                RaycastHit hit;
                                if (Physics.Raycast(LoSRay, out hit, blastRadius * 1.2f, collisionLayerMask)) // only add fires to parts in LoS of blast
                                {
                                    KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                    Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                                    if (p != null)
                                    {
                                        float distance = Vector3.Distance(currentPosition, hit.point);
                                        BulletHitFX.AttachFire(hit.point, p, caliber, sourceVesselName, BDArmorySettings.WEAPON_FX_DURATION * (1 - (distance / blastRadius)), 1, true); //else apply fire to occluding part
                                        if (BDArmorySettings.DEBUG_WEAPONS)
                                            Debug.Log("[BDArmory.PooledRocket]: Applying fire to " + p.name + " at distance " + distance + "m, for " + BDArmorySettings.WEAPON_FX_DURATION * (1 - (distance / blastRadius)) + " seconds"); ;
                                    }
                                    if (BDArmorySettings.DEBUG_WEAPONS)
                                        Debug.Log("[BDArmory.PooledRocket] incendiary raytrace: " + hit.point.x + "; " + hit.point.y + "; " + hit.point.z);
                                }
                            }
                        }
                        if (concussion || EMP || choker)
                        {
                            var overlapSphereColliderCount = Physics.OverlapSphereNonAlloc(currentPosition, 25, detonateOverlapSphereColliders, explosionLayerMask);
                            if (overlapSphereColliderCount == detonateOverlapSphereColliders.Length)
                            {
                                detonateOverlapSphereColliders = Physics.OverlapSphere(currentPosition, 25, explosionLayerMask);
                                overlapSphereColliderCount = detonateOverlapSphereColliders.Length;
                            }
                            using (var hitsEnu = detonateOverlapSphereColliders.Take(overlapSphereColliderCount).GetEnumerator())
                            {
                                var craftHit = new HashSet<Vessel>();
                                while (hitsEnu.MoveNext())
                                {
                                    if (hitsEnu.Current == null) continue;
                                    if (hitsEnu.Current.gameObject == FlightGlobals.currentMainBody.gameObject) continue; // Ignore terrain hits.
                                    Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
                                    if (partHit == null) continue;
                                    if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                                    if (craftHit.Contains(partHit.vessel)) continue; // Don't hit the same craft multiple times.
                                    craftHit.Add(partHit.vessel);

                                    if (partHit != null)
                                    {
                                        float distance = Vector3.Distance(partHit.transform.position, currentPosition);
                                        if (concussion && partHit.mass > 0)
                                        {
                                            partHit.rb.AddForceAtPosition((partHit.transform.position - currentPosition).normalized * impulse, partHit.transform.position, ForceMode.Acceleration);
                                        }
                                        if (EMP)
                                        {
                                            var MDEC = partHit.vessel.rootPart.FindModuleImplementing<ModuleDrainEC>();
                                            if (MDEC == null)
                                            {
                                                MDEC = (ModuleDrainEC)partHit.vessel.rootPart.AddModule("ModuleDrainEC");
                                            }
                                            MDEC.incomingDamage = (25 - distance) * 5; //this way craft at edge of blast might only get disabled instead of bricked
                                            MDEC.softEMP = false; //can bypass EMP damage cap                                            
                                        }
                                        if (choker)
                                        {
                                            var ash = partHit.vessel.rootPart.FindModuleImplementing<ModuleDrainIntakes>();
                                            if (ash == null)
                                            {
                                                ash = (ModuleDrainIntakes)partHit.vessel.rootPart.AddModule("ModuleDrainIntakes");
                                            }
                                            ash.drainDuration += BDArmorySettings.WEAPON_FX_DURATION * (1 - (distance / 25)); //reduce intake knockout time based on distance from epicenter                                        
                                        }
                                    }
                                }
                            }
                            ExplosionFx.CreateExplosion(pos, tntMass, explModelPath, explSoundPath, ExplosionSourceType.Rocket, caliber, null, sourceVesselName, null, null, direction, -1, true, Hitpart: hitPart, sourceVelocity: airDetonation ? currentVelocity : default);
                        }
                        else
                        {
                            if (nuclear)
                                NukeFX.CreateExplosion(pos, ExplosionSourceType.Rocket, sourceVesselName, rocket.DisplayName, 0, tntMass * 200, tntMass, tntMass, EMP, blastSoundPath, flashModelPath, shockModelPath, blastModelPath, plumeModelPath, debrisModelPath, "", "", hitPart: hitPart, sourceVelocity: airDetonation ? currentVelocity : default);
                            else
                                ExplosionFx.CreateExplosion(pos, tntMass, explModelPath, explSoundPath, ExplosionSourceType.Rocket, caliber, null, sourceVesselName, null, null, direction, -1, false, rocketMass * 1000, -1, dmgMult, shaped ? "shapedcharge" : "standard", hitPart, apMod, ProjectileUtils.isReportingWeapon(sourceWeapon) ? (float)distanceFromStart : -1, sourceVelocity: airDetonation ? currentVelocity : default);
                        }
                    }
                }
            }
            gameObject.SetActive(false);
        }

        public void BeehiveDetonation()
        {
            if (subMunitionType == null)
            {
                Debug.Log("[BDArmory.PooledRocket] Beehive round not configured with subMunitionType!");
                return;
            }
            string[] subMunitionData = subMunitionType.Split(new char[] { ';' });
            string projType = subMunitionData[0];
            if (subMunitionData.Length < 2 || !int.TryParse(subMunitionData[1], out int count)) count = 1;
            if (BulletInfo.bulletNames.Contains(projType))
            {
                BulletInfo sBullet = BulletInfo.bullets[projType];
                string fuze = sBullet.fuzeType;
                fuze.ToLower();
                BulletFuzeTypes sFuze;
                switch (fuze)
                {
                    case "timed":
                        sFuze = PooledBullet.BulletFuzeTypes.Timed;
                        break;
                    case "proximity":
                        sFuze = PooledBullet.BulletFuzeTypes.Proximity;
                        break;
                    case "flak":
                        sFuze = PooledBullet.BulletFuzeTypes.Flak;
                        break;
                    case "delay":
                        sFuze = PooledBullet.BulletFuzeTypes.Delay;
                        break;
                    case "penetrating":
                        sFuze = PooledBullet.BulletFuzeTypes.Penetrating;
                        break;
                    case "impact":
                        sFuze = PooledBullet.BulletFuzeTypes.Impact;
                        break;
                    case "none":
                        sFuze = PooledBullet.BulletFuzeTypes.None;
                        break;
                    default:
                        sFuze = PooledBullet.BulletFuzeTypes.Impact;
                        break;
                }
                float relVelocity = (thrust / rocketMass) * Mathf.Clamp(Time.time - startTime, 0, thrustTime); //currVel is rocketVel + orbitalvel, if in orbit, which will dramatically increase dispersion cone angle, so using accel * time instad
                float incrementVelocity = 1000 / (relVelocity + sBullet.bulletVelocity); //using 1km/s as a reference Unit 
                float dispersionAngle = sBullet.subProjectileDispersion > 0 ? sBullet.subProjectileDispersion : BDAMath.Sqrt(count) / 2; //fewer fragments/pellets are going to be larger-> move slower, less dispersion
                float dispersionVelocityforAngle = 1000 / incrementVelocity * Mathf.Sin(dispersionAngle * Mathf.Deg2Rad); // convert m/s despersion to angle, accounting for vel of round

                for (int s = 0; s < count; s++)
                {
                    GameObject Bullet = ModuleWeapon.bulletPool.GetPooledObject();
                    PooledBullet pBullet = Bullet.GetComponent<PooledBullet>();
                    pBullet.currentPosition = currentPosition;

                    pBullet.caliber = sBullet.caliber;
                    pBullet.bulletVelocity = sBullet.bulletVelocity + rb.velocity.magnitude;
                    pBullet.bulletMass = sBullet.bulletMass;
                    pBullet.incendiary = sBullet.incendiary;
                    pBullet.apBulletMod = sBullet.apBulletMod;
                    pBullet.bulletDmgMult = bulletDmgMult;
                    pBullet.ballisticCoefficient = sBullet.bulletMass / (((Mathf.PI * 0.25f * sBullet.caliber * sBullet.caliber) / 1000000f) * 0.295f);
                    pBullet.timeElapsedSinceCurrentSpeedWasAdjusted = 0;
                    pBullet.timeToLiveUntil = Mathf.Max(sBullet.projectileTTL, detonationRange / pBullet.bulletVelocity * 1.1f) + Time.time;
                    //Vector3 firedVelocity = VectorUtils.GaussianDirectionDeviation(currentVelocity.normalized, sBullet.subProjectileDispersion > 0 ? sBullet.subProjectileDispersion : (sBullet.subProjectileCount / BDAMath.Sqrt(currentVelocity.magnitude / 100))) * sBullet.bulletVelocity; //more subprojectiles = wider spread, higher base velocity = tighter spread
                    Vector3 firedVelocity = currentVelocity + UnityEngine.Random.onUnitSphere * dispersionVelocityforAngle;
                    pBullet.currentVelocity = firedVelocity;
                    pBullet.sourceWeapon = sourceWeapon;
                    pBullet.sourceVessel = sourceVessel;
                    pBullet.team = team;
                    pBullet.bulletTexturePath = "BDArmory/Textures/bullet";
                    pBullet.projectileColor = GUIUtils.ParseColor255(sBullet.projectileColor);
                    pBullet.startColor = GUIUtils.ParseColor255(sBullet.startColor);
                    pBullet.fadeColor = sBullet.fadeColor;
                    pBullet.tracerStartWidth = sBullet.caliber / 300;
                    pBullet.tracerEndWidth = sBullet.caliber / 750;
                    pBullet.tracerLength = 0;
                    pBullet.tracerDeltaFactor = 2.65f;
                    pBullet.tracerLuminance = 1.75f;
                    pBullet.bulletDrop = true;

                    if (sBullet.tntMass > 0)// || sBullet.beehive)
                    {
                        pBullet.explModelPath = explModelPath;
                        pBullet.explSoundPath = explSoundPath;
                        pBullet.tntMass = sBullet.tntMass;
                        string HEtype = sBullet.explosive;
                        HEtype.ToLower();
                        switch (HEtype)
                        {
                            case "standard":
                                pBullet.HEType = PooledBullet.PooledBulletTypes.Explosive;
                                break;
                            //legacy support for older configs that are still explosive = true
                            case "true":
                                pBullet.HEType = PooledBullet.PooledBulletTypes.Explosive;
                                break;
                            case "shaped":
                                pBullet.HEType = PooledBullet.PooledBulletTypes.Shaped;
                                break;
                        }
                        pBullet.detonationRange = detonationRange;
                        pBullet.defaultDetonationRange = 1000;
                        pBullet.timeToDetonation = detonationRange / Mathf.Max(1, currentVelocity.magnitude); // Only a short time remaining to the target.
                        pBullet.fuzeType = sFuze;
                    }
                    else
                    {
                        pBullet.fuzeType = PooledBullet.BulletFuzeTypes.None;
                        pBullet.sabot = (((((sBullet.bulletMass * 1000) / ((sBullet.caliber * sBullet.caliber * Mathf.PI / 400) * 19) + 1) * 10) > sBullet.caliber * 4)) ? true : false;
                        pBullet.HEType = PooledBullet.PooledBulletTypes.Slug;
                    }
                    pBullet.EMP = sBullet.EMP;
                    pBullet.nuclear = sBullet.nuclear;
                    //pBullet.beehive = sBullet.beehive;
                    //pBullet.subMunitionType = BulletInfo.bullets[sBullet.subMunitionType]; //submunitions of submunitions is a bit silly, and they'd be detonating immediately, due to inherited detonationRange
                    //pBullet.homing = BulletInfo.homing;
                    pBullet.impulse = sBullet.impulse;
                    pBullet.massMod = sBullet.massMod;
                    switch (sBullet.bulletDragTypeName)
                    {
                        case "None":
                            pBullet.dragType = PooledBullet.BulletDragTypes.None;
                            break;
                        case "AnalyticEstimate":
                            pBullet.dragType = PooledBullet.BulletDragTypes.AnalyticEstimate;
                            break;
                        case "NumericalIntegration":
                            pBullet.dragType = PooledBullet.BulletDragTypes.NumericalIntegration;
                            break;
                        default:
                            pBullet.dragType = PooledBullet.BulletDragTypes.AnalyticEstimate;
                            break;
                    }
                    pBullet.bullet = BulletInfo.bullets[sBullet.name];
                    pBullet.stealResources = thief;
                    pBullet.dmgMult = dmgMult;
                    pBullet.isAPSprojectile = isAPSprojectile;
                    pBullet.isSubProjectile = true;
                    pBullet.tgtShell = tgtShell;
                    pBullet.tgtRocket = tgtRocket;
                    pBullet.gameObject.SetActive(true);
                    pBullet.SetTracerPosition();
                }
            }
            else
            {
                RocketInfo sRocket = RocketInfo.rockets[projType];
                for (int s = 0; s < count; s++)
                {
                    GameObject rocketObj = ModuleWeapon.rocketPool[sRocket.name].GetPooledObject();
                    rocketObj.transform.position = currentPosition;
                    //rocketObj.transform.rotation = currentRocketTfm.rotation;
                    rocketObj.transform.rotation = transform.rotation;
                    rocketObj.transform.localScale = transform.localScale;
                    PooledRocket rocket = rocketObj.GetComponent<PooledRocket>();
                    rocket.explModelPath = explModelPath;
                    rocket.explSoundPath = explSoundPath;
                    rocket.caliber = sRocket.caliber;
                    rocket.apMod = sRocket.apMod;
                    rocket.rocketMass = sRocket.rocketMass;
                    rocket.blastRadius = blastRadius = BlastPhysicsUtils.CalculateBlastRange(sRocket.tntMass);
                    rocket.thrust = sRocket.thrust;
                    rocket.thrustTime = sRocket.thrustTime;
                    rocket.flak = sRocket.flak;
                    rocket.detonationRange = detonationRange;
                    rocket.timeToDetonation = detonationRange / Mathf.Max(1, currentVelocity.magnitude); // Only a short time remaining to the target.
                    rocket.tntMass = sRocket.tntMass;
                    rocket.shaped = sRocket.shaped;
                    rocket.concussion = sRocket.impulse;
                    rocket.gravitic = sRocket.gravitic;
                    rocket.EMP = sRocket.EMP;
                    rocket.nuclear = sRocket.nuclear;
                    rocket.beehive = sRocket.beehive;
                    //if (beehive) //no submunitions of submunitions, not while detoantionRange remains the sasme (sub-submunitions would instantly spawn)
                    //{
                    //    rocket.subMunitionType = sRocket.subMunitionType;
                    //}
                    rocket.choker = choker;
                    rocket.impulse = sRocket.force;
                    rocket.massMod = sRocket.massMod;
                    rocket.incendiary = sRocket.incendiary;
                    rocket.randomThrustDeviation = sRocket.thrustDeviation;
                    rocket.bulletDmgMult = bulletDmgMult;
                    rocket.sourceVessel = sourceVessel;
                    rocket.sourceWeapon = sourceWeapon;
                    rocketObj.transform.SetParent(transform);
                    rocket.rocketName = rocketName + " submunition";
                    rocket.team = team;
                    rocket.parentRB = parentRB;
                    rocket.rocket = RocketInfo.rockets[sRocket.name];
                    rocket.rocketSoundPath = rocketSoundPath;
                    rocket.thief = thief; //currently will only steal on direct hit
                    rocket.dmgMult = dmgMult;
                    if (isAPSprojectile)
                    {
                        rocket.isAPSprojectile = true;
                        rocket.tgtShell = tgtShell;
                        rocket.tgtRocket = tgtRocket;
                    }
                    rocket.isSubProjectile = true;
                    rocketObj.SetActive(true);
                }
            }
        }

        void SetupAudio()
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); }
            audioSource.loop = true;
            audioSource.minDistance = 1;
            audioSource.maxDistance = 2000;
            audioSource.dopplerLevel = 0.5f;
            audioSource.volume = 0.9f * BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            audioSource.pitch = 1f;
            audioSource.priority = 255;
            audioSource.spatialBlend = 1;
            audioSource.clip = SoundUtils.GetAudioClip(rocketSoundPath);

            UpdateVolume();
            BDArmorySetup.OnVolumeChange += UpdateVolume;
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }
        void OnGUI()
        {
            if (((HighLogic.LoadedSceneIsFlight && BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDTISettings.TEAMICONS) || HighLogic.LoadedSceneIsFlight && !BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDTISettings.TEAMICONS && BDTISettings.PERSISTANT) && BDTISettings.MISSILES)
            {
                if (distanceFromStart > 100)
                {
                    GUIUtils.DrawTextureOnWorldPos(transform.position, BDTISetup.Instance.TextureIconRocket, new Vector2(20, 20), 0);
                }
            }
        }
    }
}
