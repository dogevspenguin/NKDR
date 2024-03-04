using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Armor;
using BDArmory.Competition;
using BDArmory.Damage;
using BDArmory.Extensions;
using BDArmory.GameModes;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.Weapons;

namespace BDArmory.FX
{
    public class ExplosionFx : MonoBehaviour
    {
        public static Dictionary<string, ObjectPool> explosionFXPools = new Dictionary<string, ObjectPool>();
        public static Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(); // Pool the audio clips separately. Note: this is really a shallow copy of the AudioClips in SoundUtils, but with invalid AudioClips replaced by the default explosion AudioClip.
        public KSPParticleEmitter[] pEmitters { get; set; }
        public Light LightFx { get; set; }
        public float StartTime { get; set; }
        // public string ExSound { get; set; }
        public string SoundPath { get; set; }
        public AudioSource audioSource { get; set; }
        private float MaxTime { get; set; }
        public float Range { get; set; }
        public float SCRange { get; set; }
        public float penetration { get; set; }
        public float Caliber { get; set; }
        public float ProjMass { get; set; }
        public ExplosionSourceType ExplosionSource { get; set; }
        public string SourceVesselName { get; set; }
        public string SourceVesselTeam { get; set; }
        public string SourceWeaponName { get; set; }
        public float Power { get; set; }
        public Vector3 Position { get { return _position; } set { _position = value; transform.position = _position; } }
        Vector3 _position;
        public Vector3 Direction { get; set; }
        public Vector3 Velocity { get; set; }
        public float cosAngleOfEffect { get; set; }
        public Part ExplosivePart { get; set; }
        public bool isFX { get; set; }
        public float CASEClamp { get; set; }
        public float dmgMult { get; set; }
        public float apMod { get; set; }
        public float travelDistance { get; set; }

        public Part projectileHitPart { get; set; }
        public float TimeIndex => Time.time - StartTime;

        private bool disabled = true;

        float blastRange;
        const int explosionLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.EVA | LayerMasks.Unknown19 | LayerMasks.Unknown23 | LayerMasks.Wheels); // Why 19 and 23?

        Queue<BlastHitEvent> explosionEvents = new Queue<BlastHitEvent>();
        List<BlastHitEvent> explosionEventsPreProcessing = new List<BlastHitEvent>();
        List<Part> explosionEventsPartsAdded = new List<Part>();
        List<DestructibleBuilding> explosionEventsBuildingAdded = new List<DestructibleBuilding>();
        Dictionary<string, int> explosionEventsVesselsHit = new Dictionary<string, int>();


        static RaycastHit[] lineOfSightHits;
        static RaycastHit[] reverseHits;
        static RaycastHit[] sortedLoSHits;
        static RaycastHit[] shapedChargeHits;
        static RaycastHit miss = new RaycastHit();
        static Collider[] overlapSphereColliders;
        public static List<Part> IgnoreParts;
        public static List<DestructibleBuilding> IgnoreBuildings;
        internal static readonly float ExplosionVelocity = 422.75f;
        internal static float KerbinSeaLevelAtmDensity
        {
            get
            {
                if (_KerbinSeaLevelAtmDensity == 0) _KerbinSeaLevelAtmDensity = (float)FlightGlobals.GetBodyByName("Kerbin").atmDensityASL;
                return _KerbinSeaLevelAtmDensity;
            }
        }
        internal static float _KerbinSeaLevelAtmDensity = 0;

        private float particlesMaxEnergy;
        internal static HashSet<ExplosionSourceType> ignoreCasingFor = new HashSet<ExplosionSourceType> { ExplosionSourceType.Missile, ExplosionSourceType.Rocket };
        public enum WarheadTypes
        {
            Standard,
            ShapedCharge,
            ContinuousRod
        }

        public WarheadTypes warheadType;

        static List<ValueTuple<float, float, float>> LoSIntermediateParts = new List<ValueTuple<float, float, float>>(); // Worker list for LoS checks to avoid reallocations.

        void Awake()
        {
            if (lineOfSightHits == null) { lineOfSightHits = new RaycastHit[100]; }
            if (reverseHits == null) { reverseHits = new RaycastHit[100]; }
            if (sortedLoSHits == null) { sortedLoSHits = new RaycastHit[100]; }
            if (shapedChargeHits == null) { shapedChargeHits = new RaycastHit[100]; }
            if (overlapSphereColliders == null) { overlapSphereColliders = new Collider[1000]; }
            if (IgnoreParts == null) { IgnoreParts = new List<Part>(); }
            if (IgnoreBuildings == null) { IgnoreBuildings = new List<DestructibleBuilding>(); }
        }

        private void OnEnable()
        {
            StartTime = Time.time;
            disabled = false;
            MaxTime = BDAMath.Sqrt((Range / ExplosionVelocity) * 3f) * 2f; // Scale MaxTime to get a reasonable visualisation of the explosion.
            blastRange = warheadType == WarheadTypes.Standard ? Range * 2 : Range; //to properly account for shrapnel hits when compiling list of hit parts from the spherecast
            if (!isFX)
            {
                CalculateBlastEvents();
            }
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    if (pe.maxEnergy > particlesMaxEnergy)
                        particlesMaxEnergy = pe.maxEnergy;
                    pe.emit = true;
                    pe.useWorldSpace = false; // Don't use worldspace, so that we can move the FX properly.
                    var emission = pe.ps.emission;
                    emission.enabled = true;
                    EffectBehaviour.AddParticleEmitter(pe);
                }

            LightFx = gameObject.GetComponent<Light>();
            LightFx.range = Range * 3f;
            LightFx.intensity = 8f; // Reset light intensity.

            audioSource = gameObject.GetComponent<AudioSource>();
            // if (ExSound == null)
            // {
            //     ExSound = SoundUtils.GetAudioClip(SoundPath);

            //     if (ExSound == null)
            //     {
            //         Debug.LogError("[BDArmory.ExplosionFX]: " + SoundPath + " was not found, using the default sound instead. Please fix your model.");
            //         ExSound = SoundUtils.GetAudioClip(ModuleWeapon.defaultExplSoundPath);
            //     }
            // }
            if (!string.IsNullOrEmpty(SoundPath))
            {
                audioSource.PlayOneShot(audioClips[SoundPath]);
            }
            if (BDArmorySettings.DEBUG_DAMAGE)
            {
                Debug.Log("[BDArmory.ExplosionFX]: Explosion started tntMass: {" + Power + "}  BlastRadius: {" + Range + "} StartTime: {" + StartTime + "}, Duration: {" + MaxTime + "}");
            }
            /*
            if (BDArmorySettings.PERSISTENT_FX && Caliber > 30 && BodyUtils.GetRadarAltitudeAtPos(Position) > Caliber / 60)
            {
                if (FlightGlobals.getAltitudeAtPos(Position) > Caliber / 60)
                {
                    FXEmitter.CreateFX(Position, (Caliber / 30), "BDArmory/Models/explosion/flakSmoke", "", 0.3f, Caliber / 6);                   
                }
            }
            */
        }

        void OnDisable()
        {
            foreach (var pe in pEmitters)
            {
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
            }
            ExplosivePart = null; // Clear the Part reference.
            explosionEvents.Clear(); // Make sure we don't have any left over events leaking memory.
            explosionEventsPreProcessing.Clear();
            explosionEventsPartsAdded.Clear();
            explosionEventsBuildingAdded.Clear();
            explosionEventsVesselsHit.Clear();
        }

        private void CalculateBlastEvents()
        {
            //Let's convert this temporal list on a ordered queue
            // using (var enuEvents = temporalEventList.OrderBy(e => e.TimeToImpact).GetEnumerator())
            using (var enuEvents = ProcessingBlastSphere().OrderBy(e => e.TimeToImpact).GetEnumerator())
            {
                while (enuEvents.MoveNext())
                {
                    if (enuEvents.Current == null) continue;

                    if (BDArmorySettings.DEBUG_DAMAGE)
                    {
                        Debug.Log("[BDArmory.ExplosionFX]: Enqueueing Blast Event");
                    }

                    explosionEvents.Enqueue(enuEvents.Current);
                }
            }
        }

        private List<BlastHitEvent> ProcessingBlastSphere()
        {
            explosionEventsPreProcessing.Clear();
            explosionEventsPartsAdded.Clear();
            explosionEventsBuildingAdded.Clear();
            explosionEventsVesselsHit.Clear();

            SCRange = 0;
            if (warheadType == WarheadTypes.ShapedCharge)
            {
                // Based on shaped charge standoff penetration falloff, set equal to 10% and solved for the range
                // Equation is from https://www.diva-portal.org/smash/get/diva2:643824/FULLTEXT01.pdf and gives an
                // answer in the same units as caliber, thus we divide by 1000 to get the range in meters. The long
                // number is actually 2*sqrt(19), however for speed this has been pre-calculated and rounded to 8 sig
                // figs behind the decimal point and turned into a floating point number (which in theory should drop it
                // to 8 sig figs and should be indistinguishable from if we had actually calculated it at runtime). We then
                // use this range to raycast those hits if it is greater than Range. This will currently overpredict for
                // small missiles and underpredict for large ones since they don't have a caliber associated with them
                // and as such will use 120 mm by default (since caliber == 0, thus it'll take 6f as the jet size which
                // corresponds to a 120 mm charge. Perhaps think about including a caliber field?
                //SCRange = (7f * (8.71779789f * 20f * Caliber + 20f * Caliber))* 0.001f; // 5%
                // Decided to swap it to 10% since 5% gave pretty big ranges on the order of several meters and 10% actually
                // simplifies down to a linear equation
                SCRange = (49f * Caliber * 20f) * 0.001f;

                //if (BDArmorySettings.DEBUG_WEAPONS && (warheadType == WarheadTypes.ShapedCharge))
                //{
                //    Debug.Log("[BDArmory.ExplosionFX] SCRange: " + SCRange + "m. Normalized Direction: " + Direction.normalized.ToString("G4"));
                //}

                Ray SCRay = new Ray(Position, Direction);
                //Ray SCRay = new Ray(Position, (Direction.normalized * Range));
                var hitCount = Physics.RaycastNonAlloc(SCRay, shapedChargeHits, SCRange > Range ? SCRange : Range, explosionLayerMask);
                if (hitCount == shapedChargeHits.Length) // If there's a whole bunch of stuff in the way (unlikely), then we need to increase the size of our hits buffer.
                {
                    shapedChargeHits = Physics.RaycastAll(SCRay, SCRange > Range ? SCRange : Range, explosionLayerMask);
                    hitCount = shapedChargeHits.Length;
                }
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.ExplosionFX]: SC plasmaJet raycast hits: {hitCount}");
                if (hitCount > 0)
                {
                    var orderedHits = shapedChargeHits.Take(hitCount).OrderBy(x => x.distance);

                    using (var hitsEnu = orderedHits.GetEnumerator())
                    {
                        while (hitsEnu.MoveNext())
                        {
                            RaycastHit SChit = hitsEnu.Current;
                            Part hitPart = null;

                            hitPart = SChit.collider.gameObject.GetComponentInParent<Part>();

                            if (hitPart != null)
                            {
                                if (ProjectileUtils.IsIgnoredPart(hitPart)) continue; // Ignore ignored parts.
                                if (hitPart.vessel.vesselName == SourceVesselName) continue;  //avoid autohit;
                                if (hitPart.mass > 0 && !explosionEventsPartsAdded.Contains(hitPart))
                                {
                                    var damaged = ProcessPartEvent(hitPart, SChit.distance, SourceVesselName, explosionEventsPreProcessing, explosionEventsPartsAdded, true);
                                    // If the explosion derives from a missile explosion, count the parts damaged for missile hit scores.
                                    if (damaged && BDACompetitionMode.Instance)
                                    {
                                        bool registered = false;
                                        var damagedVesselName = hitPart.vessel != null ? hitPart.vessel.GetName() : null;
                                        switch (ExplosionSource)
                                        {
                                            case ExplosionSourceType.Rocket:
                                                if (BDACompetitionMode.Instance.Scores.RegisterRocketHit(SourceVesselName, damagedVesselName, 1))
                                                    registered = true;
                                                break;
                                            case ExplosionSourceType.Missile:
                                                if (BDACompetitionMode.Instance.Scores.RegisterMissileHit(SourceVesselName, damagedVesselName, 1))
                                                    registered = true;
                                                break;
                                        }
                                        if (registered)
                                        {
                                            if (explosionEventsVesselsHit.ContainsKey(damagedVesselName))
                                                ++explosionEventsVesselsHit[damagedVesselName];
                                            else
                                                explosionEventsVesselsHit[damagedVesselName] = 1;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (!BDArmorySettings.PAINTBALL_MODE)
                                {
                                    DestructibleBuilding building = SChit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
                                    if (building != null)
                                    {
                                        ProjectileUtils.CheckBuildingHit(SChit, Power * 0.0555f, Direction.normalized * 4000f, 1);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            var overlapSphereColliderCount = Physics.OverlapSphereNonAlloc(Position, blastRange, overlapSphereColliders, explosionLayerMask);
            if (overlapSphereColliderCount == overlapSphereColliders.Length)
            {
                overlapSphereColliders = Physics.OverlapSphere(Position, blastRange, explosionLayerMask);
                overlapSphereColliderCount = overlapSphereColliders.Length;
            }
            using (var hitCollidersEnu = overlapSphereColliders.Take(overlapSphereColliderCount).GetEnumerator())
            {
                while (hitCollidersEnu.MoveNext())
                {
                    if (hitCollidersEnu.Current == null) continue;
                    try
                    {
                        Part partHit = hitCollidersEnu.Current.gameObject.GetComponentInParent<Part>();
                        if (partHit != null)
                        {
                            if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                            if (ExplosivePart != null && partHit.name == ExplosivePart.name)
                            {
                                var partHitExplosivePart = partHit.GetComponent<BDExplosivePart>();
                                if (partHitExplosivePart != null && SourceVesselTeam == partHitExplosivePart.Team.Name && !string.IsNullOrEmpty(SourceVesselTeam)) continue; //don't fratricide fellow missiles/bombs in a launched salvo when the first detonates
                            }
                            if (partHit.mass > 0 && !explosionEventsPartsAdded.Contains(partHit))
                            {
                                var damaged = ProcessPartEvent(partHit, Vector3.Distance(hitCollidersEnu.Current.ClosestPoint(Position), Position), SourceVesselName, explosionEventsPreProcessing, explosionEventsPartsAdded);
                                // If the explosion derives from a missile explosion, count the parts damaged for missile hit scores.
                                if (damaged && BDACompetitionMode.Instance)
                                {
                                    bool registered = false;

                                    var damagedVesselName = partHit.vessel != null ? partHit.vessel.GetName() : null;
                                    switch (ExplosionSource)
                                    {
                                        case ExplosionSourceType.Rocket:
                                            if (BDACompetitionMode.Instance.Scores.RegisterRocketHit(SourceVesselName, damagedVesselName, 1))
                                                registered = true;
                                            break;
                                        case ExplosionSourceType.Missile:
                                            if (BDACompetitionMode.Instance.Scores.RegisterMissileHit(SourceVesselName, damagedVesselName, 1))
                                                registered = true;
                                            break;
                                        case ExplosionSourceType.Bullet:
                                            if (travelDistance > 0)
                                                registered = true;
                                            break;
                                    }
                                    if (registered)
                                    {
                                        if (explosionEventsVesselsHit.ContainsKey(damagedVesselName))
                                            ++explosionEventsVesselsHit[damagedVesselName];
                                        else
                                            explosionEventsVesselsHit[damagedVesselName] = 1;
                                    }
                                }
                            }
                        }
                        else
                        {
                            DestructibleBuilding building = hitCollidersEnu.Current.GetComponentInParent<DestructibleBuilding>();

                            if (building != null)
                            {
                                if (!explosionEventsBuildingAdded.Contains(building))
                                {
                                    //ProcessBuildingEvent(building, explosionEventsPreProcessing, explosionEventsBuildingAdded);
                                    Ray ray = new Ray(Position, building.transform.position - Position);
                                    var distance = Vector3.Distance(building.transform.position, Position);
                                    RaycastHit rayHit;
                                    if (Physics.Raycast(ray, out rayHit, Range * 2, explosionLayerMask))
                                    {
                                        //DestructibleBuilding destructibleBuilding = rayHit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
                                        distance = Vector3.Distance(Position, rayHit.point);
                                        //if (destructibleBuilding != null && destructibleBuilding.Equals(building) && building.IsIntact)
                                        if (building.IsIntact)
                                        {
                                            explosionEventsPreProcessing.Add(new BuildingBlastHitEvent() { Distance = distance, Building = building, TimeToImpact = distance / ExplosionVelocity });
                                            explosionEventsBuildingAdded.Add(building);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[BDArmory.ExplosionFX]: Exception in overlapSphereColliders processing: {e.Message}\n{e.StackTrace}");
                    }
                }
            }
            if (explosionEventsVesselsHit.Count > 0)
            {
                string message = "";
                foreach (var vesselName in explosionEventsVesselsHit.Keys)
                    //message += (message == "" ? "" : " and ") + vesselName + " had " + explosionEventsVesselsHit[vesselName];
                    switch (ExplosionSource)
                    {
                        case ExplosionSourceType.Missile:
                            message += (message == "" ? "" : " and ") + vesselName + " had " + explosionEventsVesselsHit[vesselName];
                            message += " parts damaged due to missile strike";
                            message += (SourceWeaponName != null ? $" ({SourceWeaponName})" : "") + (SourceVesselName != null ? $" from {SourceVesselName}" : "") + ".";
                            break;
                        case ExplosionSourceType.Bullet:
                            message += (message == "" ? "" : " and ") + vesselName + " had " + explosionEventsVesselsHit[vesselName] + " parts damaged from";
                            message += (SourceVesselName != null ? $" from {SourceVesselName}'s" : "") + (SourceWeaponName != null ? $" ({SourceWeaponName})" : "shell hit") + ($" at {travelDistance:F3}m") + ".";
                            break;
                        case ExplosionSourceType.Rocket:
                            {
                                if (travelDistance > 0)
                                {
                                    message += (message == "" ? "" : " and ") + vesselName + " had " + explosionEventsVesselsHit[vesselName] + " parts damaged from";
                                    message += (SourceVesselName != null ? $" from {SourceVesselName}'s" : "") + (SourceWeaponName != null ? $" ({SourceWeaponName})" : "rocket hit") + ($" at {travelDistance:F3}m") + ".";
                                }
                                break;
                            }
                    }
                if (!string.IsNullOrEmpty(message)) BDACompetitionMode.Instance.competitionStatus.Add(message);
                // Note: damage hasn't actually been applied to the parts yet, just assigned as events, so we can't know if they survived.
                foreach (var vesselName in explosionEventsVesselsHit.Keys) // Note: sourceVesselName is already checked for being in the competition before damagedVesselName is added to explosionEventsVesselsHitByMissiles, so we don't need to check it here.
                {
                    switch (ExplosionSource)
                    {
                        case ExplosionSourceType.Rocket:
                            BDACompetitionMode.Instance.Scores.RegisterRocketStrike(SourceVesselName, vesselName);
                            break;
                        case ExplosionSourceType.Missile:
                            BDACompetitionMode.Instance.Scores.RegisterMissileStrike(SourceVesselName, vesselName);
                            break;
                    }
                }
            }
            return explosionEventsPreProcessing;
        }

        private void ProcessBuildingEvent(DestructibleBuilding building, List<BlastHitEvent> eventList, List<DestructibleBuilding> buildingAdded)
        {
            Ray ray = new Ray(Position, building.transform.position - Position);
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, Range, explosionLayerMask))
            {
                //TODO: Maybe we are not hitting building because we are hitting explosive parts.

                DestructibleBuilding destructibleBuilding = rayHit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();

                // Is not a direct hit, because we are hitting a different part
                if (destructibleBuilding != null && destructibleBuilding.Equals(building) && building.IsIntact)
                {
                    var distance = Vector3.Distance(Position, rayHit.point);
                    eventList.Add(new BuildingBlastHitEvent() { Distance = Vector3.Distance(Position, rayHit.point), Building = building, TimeToImpact = distance / ExplosionVelocity });
                    buildingAdded.Add(building);
                    explosionEventsBuildingAdded.Add(building);
                }
            }
        }

        private bool ProcessPartEvent(Part part, float hitDist, string sourceVesselName, List<BlastHitEvent> eventList, List<Part> partsAdded, bool angleOverride = false)
        {
            RaycastHit hit;
            float distance;
            if (IsInLineOfSight(part, ExplosivePart, hitDist, out hit, out distance))
            {
                //if (IsAngleAllowed(Direction, hit))
                //{
                //Adding damage hit
                if (distance <= (blastRange > SCRange ? blastRange : SCRange))//part within total range of shrapnel + blast?
                {
                    eventList.Add(new PartBlastHitEvent()
                    {
                        Distance = distance,
                        Part = part,
                        TimeToImpact = distance / ExplosionVelocity,
                        HitPoint = hit.point,
                        Hit = hit,
                        SourceVesselName = sourceVesselName,
                        withinAngleofEffect = angleOverride ? true : (IsAngleAllowed(Direction, hit, part)),
                        IntermediateParts = LoSIntermediateParts // A copy is made internally.
                    });
                }
                partsAdded.Add(part);

                return true;
                //}
            }
            return false;
        }

        private bool IsAngleAllowed(Vector3 direction, RaycastHit hit, Part p)
        {
            if (direction == default(Vector3))
            {
                //if (BDArmorySettings.DEBUG_LABELS) Debug.Log("[BDArmory.ExplosionFX]: Default Direction param! " + p.name + " angle from explosion dir irrelevant!");
                return true;
            }
            if (warheadType == WarheadTypes.ContinuousRod)
            {
                if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDArmory.ExplosionFX]: {p.name} at {Vector3.Angle(direction, (hit.point - Position).normalized)} angle from CR explosion direction");
                //if (Vector3.Angle(direction, (hit.point - Position).normalized) >= 60 && Vector3.Angle(direction, (hit.point - Position).normalized) <= 90)
                if (Vector3.Dot(direction, (hit.point - Position).normalized) <= 0.5 && Vector3.Dot(direction, (hit.point - Position).normalized) >= 0)
                {
                    return true;
                }
                else return false;
            }
            else
            {
                if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDArmory.ExplosionFX]: {p.name} at {Vector3.Angle(direction, (hit.point - Position).normalized)} angle from {warheadType} explosion direction");
                return (Vector3.Dot(direction, (hit.point - Position).normalized) >= cosAngleOfEffect);
            }
        }

        /// <summary>
        /// This method will calculate if there is valid line of sight between the explosion origin and the specific Part
        /// In order to avoid collisions with the same missile part, It will not take into account those parts belonging to same vessel that contains the explosive part
        /// </summary>
        /// <param name="part"></param>
        /// <param name="explosivePart"></param>
        /// <param name="hit">The raycast hit</param>
        /// <param name="distance">The distance of the hit</param>
        /// <param name="intermediateParts">Update the LoSIntermediateParts list</param>
        /// <returns></returns>
        private bool IsInLineOfSight(Part part, Part explosivePart, float startDist, out RaycastHit hit, out float distance, bool intermediateParts = true)
        {
            var partPosition = part.transform.position; //transition over to part.Collider.ClosestPoint(Position);? Test later
            Ray partRay = new Ray(Position, partPosition - Position);
            float range = blastRange > SCRange ? blastRange : SCRange;

            var hitCount = Physics.RaycastNonAlloc(partRay, lineOfSightHits, range, explosionLayerMask);
            if (hitCount == lineOfSightHits.Length) // If there's a whole bunch of stuff in the way (unlikely), then we need to increase the size of our hits buffer.
            {
                lineOfSightHits = Physics.RaycastAll(partRay, range, explosionLayerMask);
                hitCount = lineOfSightHits.Length;
            }
            //check if explosion is originating inside a part
            Ray reverseRay = new Ray(partRay.origin + range * partRay.direction, -partRay.direction);
            int reverseHitCount = Physics.RaycastNonAlloc(reverseRay, reverseHits, range, explosionLayerMask);
            if (reverseHitCount == reverseHits.Length)
            {
                reverseHits = Physics.RaycastAll(reverseRay, range, explosionLayerMask);
                reverseHitCount = reverseHits.Length;
            }
            for (int i = 0; i < reverseHitCount; ++i)
            {
                reverseHits[i].distance = range - reverseHits[i].distance;
                reverseHits[i].normal = -reverseHits[i].normal;
            }

            LoSIntermediateParts.Clear();
            var totalHitCount = CollateHits(ref lineOfSightHits, hitCount, ref reverseHits, reverseHitCount); // This is the most expensive part of this method and the cause of most of the slow-downs with explosions.
            float factor = 1.0f;
            for (int i = 0; i < totalHitCount; ++i)
            {
                hit = sortedLoSHits[i];
                Part partHit = hit.collider.GetComponentInParent<Part>();
                if (partHit == null) continue;
                if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                //if (startDist > -100)
                //{
                if (partHit == projectileHitPart) distance = 0.05f; //HE bullet slamming into armor/penning and detonating inside part
                else distance = Mathf.Max(startDist, 0.05f);
                //}
                //if (startDist < 0) distance = hit.distance;

                if (partHit == part)
                {
                    return true;
                }
                if (partHit != part)
                {
                    // ignoring collisions against the explosive, or explosive vessel for certain explosive types (e.g., missile/rocket casing)
                    if (partHit == explosivePart || (explosivePart != null && ignoreCasingFor.Contains(ExplosionSource) && partHit.vessel == explosivePart.vessel))
                    {
                        continue;
                    }
                    if (FlightGlobals.currentMainBody != null && hit.collider.gameObject == FlightGlobals.currentMainBody.gameObject) return false; // Terrain hit. Full absorption. Should avoid NREs in the following. FIXME This doesn't seem correct anymore: "Kerbin Zn1232223233" vs "Kerbin", but doesn't seem to cause issues either.
                    if (intermediateParts)
                    {
                        var partHP = partHit.Damage();
                        if (ProjectileUtils.IsArmorPart(partHit)) partHP = BDArmorySettings.EXP_PEN_RESIST_MULT * 100;
                        //var partArmour = partHit.GetArmorThickness();
                        float partArmour = 0f;
                        var Armor = partHit.FindModuleImplementing<HitpointTracker>();
                        if (Armor != null && partHit.Rigidbody != null)
                        {
                            float armorCos = Mathf.Abs(Vector3.Dot((hit.point + partHit.Rigidbody.velocity * TimeIndex - Position).normalized, -hit.normal));
                            partArmour = ProjectileUtils.CalculateThickness(part, armorCos);

                            if (warheadType == WarheadTypes.ShapedCharge)
                            {
                                partArmour *= Armor.HEATEquiv;
                            }
                            else
                            {
                                partArmour *= Armor.HEEquiv;
                            }

                            //if (BDArmorySettings.DEBUG_WEAPONS)
                            //{
                            //    Debug.Log($"[BDArmory.ExplosionFX] Part: {partHit.name}; Thickness: {partArmour}mm; Angle: {Mathf.Rad2Deg * Mathf.Acos(armorCos)}; Contributed: {factor * Mathf.Max(partArmour / armorCos, 1)}mm; Distance: {hit.distance};");
                            //}

                            partArmour *= factor;

                            factor *= 1.05f;
                        }
                        var RA = partHit.FindModuleImplementing<ModuleReactiveArmor>();
                        if (RA != null)
                        {
                            if (RA.NXRA)
                            {
                                partArmour *= RA.armorModifier;
                            }
                            else
                            {
                                if (((ExplosionSource == ExplosionSourceType.Bullet || ExplosionSource == ExplosionSourceType.Rocket) && (Caliber > RA.sensitivity && partHit == projectileHitPart)) ||   //bullet/rocket hit
                                    ((ExplosionSource == ExplosionSourceType.Missile || ExplosionSource == ExplosionSourceType.BattleDamage) && (distance < Power / 2))) //or close range detonation likely to trigger ERA
                                {
                                    partArmour = 300 * RA.armorModifier;
                                }
                            }
                        }
                        if (partHP > 0) // Ignore parts that are already dead but not yet removed from the game.
                            LoSIntermediateParts.Add(new ValueTuple<float, float, float>(hit.distance, partHP, partArmour));
                    }
                }
            }

            hit = miss;
            distance = float.PositiveInfinity;
            return false;
        }

        int CollateHits(ref RaycastHit[] forwardHits, int forwardHitCount, ref RaycastHit[] reverseHits, int reverseHitCount)
        {
            var totalHitCount = forwardHitCount + reverseHitCount;
            if (sortedLoSHits.Length < totalHitCount) Array.Resize(ref sortedLoSHits, totalHitCount);
            Array.Copy(forwardHits, sortedLoSHits, forwardHitCount);
            Array.Copy(reverseHits, 0, sortedLoSHits, forwardHitCount, reverseHitCount);
            Array.Sort(sortedLoSHits, 0, totalHitCount, RaycastHitComparer.raycastHitComparer); // This generates garbage, but less than other methods using Linq or Lists.
            return totalHitCount;
        }

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || !gameObject.activeInHierarchy) return;

            if (LightFx != null) LightFx.intensity -= 12 * Time.deltaTime;

            if (!disabled && TimeIndex > 0.3f && pEmitters != null) // 0.3s seems to be enough to always show the explosion, but 0.2s isn't for some reason.
            {
                foreach (var pe in pEmitters)
                {
                    if (pe == null) continue;
                    pe.emit = false;
                }
                disabled = true;
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !gameObject.activeInHierarchy) return;

            if (UI.BDArmorySetup.GameIsPaused)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                return;
            }

            //floating origin and velocity offloading corrections
            if (BDKrakensbane.IsActive)
            {
                Position -= BDKrakensbane.FloatingOriginOffsetNonKrakensbane;
            }
            { // Explosion centre velocity depends on atmospheric density relative to Kerbin sea level.
                var atmDensity = (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(Position), FlightGlobals.getExternalTemperature(Position));
                Velocity /= 1 + atmDensity / KerbinSeaLevelAtmDensity;
                var deltaPos = Velocity * TimeWarp.fixedDeltaTime; // Krakensbane is already accounted for above.
                Position += deltaPos;
            }

            if (!isFX)
            {
                while (explosionEvents.Count > 0 && explosionEvents.Peek().TimeToImpact <= TimeIndex)
                {
                    BlastHitEvent eventToExecute = explosionEvents.Dequeue();

                    var partBlastHitEvent = eventToExecute as PartBlastHitEvent;
                    if (partBlastHitEvent != null)
                    {
                        ExecutePartBlastEvent(partBlastHitEvent);
                    }
                    else
                    {
                        ExecuteBuildingBlastEvent((BuildingBlastHitEvent)eventToExecute);
                    }
                }
            }

            if (disabled && explosionEvents.Count == 0 && TimeIndex > MaxTime)
            {
                if (BDArmorySettings.DEBUG_DAMAGE)
                {
                    Debug.Log("[BDArmory.ExplosionFX]: Explosion Finished");
                }

                gameObject.SetActive(false);
                return;
            }
        }
        /*
        /////////
        // Debugging for Continuous rod/shaped charge orientation, unnecessary unless something gets changed at somepoint, so commented out for now.
        ///////////
        void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight && BDArmorySettings.DEBUG_LINES)
            {
                if (warheadType == WarheadTypes.ContinuousRod)
                {
                    if (explosionEventsPartsAdded.Count > 0)
                    {
                        RaycastHit hit;
                        float distance;
                        for (int i = 0; i < explosionEventsPartsAdded.Count; i++)
                        {
                            try
                            {
                                Part part = explosionEventsPartsAdded[i];
                                if (IsInLineOfSight(part, null, -1, out hit, out distance, false))
                                {
                                    if (IsAngleAllowed(Direction, hit, explosionEventsPartsAdded[i]))
                                    {
                                        GUIUtils.DrawLineBetweenWorldPositions(Position, hit.point, 2, Color.blue);
                                    }
                                    else if (distance < Range / 2)
                                    {
                                        GUIUtils.DrawLineBetweenWorldPositions(Position, hit.point, 2, Color.red);
                                    }
                                }
                            }
                            catch
                            {
                                Debug.Log("[BDArmory.ExplosioNFX] nullref in ContinuousRod Debug lines in  onGUI");
                            }
                        }
                    }
                }
                if (warheadType == WarheadTypes.ShapedCharge)
                {
                    GUIUtils.DrawLineBetweenWorldPositions(Position, (Position + (Direction.normalized * Range)), 4, Color.green);
                }
            }
        }
        */

        private void ExecuteBuildingBlastEvent(BuildingBlastHitEvent eventToExecute)
        {
            if (BDArmorySettings.BUILDING_DMG_MULTIPLIER == 0) return;
            //TODO: Review if the damage is sensible after so many changes
            //buildings
            DestructibleBuilding building = eventToExecute.Building;
            //building.damageDecay = 600f;

            if (building && building.IsIntact && !BDArmorySettings.PAINTBALL_MODE)
            {
                var distanceFactor = Mathf.Clamp01((Range - eventToExecute.Distance) / Range);
                float blastMod = 1;
                switch (ExplosionSource)
                {
                    case ExplosionSourceType.Bullet:
                        blastMod = BDArmorySettings.EXP_DMG_MOD_BALLISTIC_NEW;
                        break;
                    case ExplosionSourceType.Rocket:
                        blastMod = BDArmorySettings.EXP_DMG_MOD_ROCKET;
                        break;
                    case ExplosionSourceType.Missile:
                        blastMod = BDArmorySettings.EXP_DMG_MOD_MISSILE;
                        break;
                    case ExplosionSourceType.BattleDamage:
                        blastMod = BDArmorySettings.EXP_DMG_MOD_BATTLE_DAMAGE;
                        break;
                }
                float damageToBuilding = (BDArmorySettings.DMG_MULTIPLIER / 100) * blastMod * (Power * distanceFactor);
                damageToBuilding /= 2;
                damageToBuilding *= BDArmorySettings.BUILDING_DMG_MULTIPLIER;
                //building.AddDamage(damageToBuilding); 
                BuildingDamage.RegisterDamage(building);
                building.FacilityDamageFraction += damageToBuilding;
                //based on testing, I think facilityDamageFraction starts at values between 5 and 100, and demolished the building if it hits 0 - which means it will work great as a HP value in the other direction
                if (building.FacilityDamageFraction > building.impactMomentumThreshold * 2)
                {
                    if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDArmory.ExplosionFX]: Building {building.name} demolished due to Explosive damage! Dmg to building: {building.Damage}");
                    building.Demolish();
                }
                if (BDArmorySettings.DEBUG_DAMAGE)
                {
                    Debug.Log($"[BDArmory.ExplosionFX]: Explosion hit destructible building {building.name}! Hitpoints Applied: {damageToBuilding:F3}, Building Damage: {building.FacilityDamageFraction}, Building Threshold : {building.impactMomentumThreshold * 2}, (Range: {Range}, Distance: {eventToExecute.Distance}, Factor: {distanceFactor}, Power: {Power})");
                }
            }
        }

        private void ExecutePartBlastEvent(PartBlastHitEvent eventToExecute)
        {
            if (eventToExecute.Part == null || eventToExecute.Part.Rigidbody == null || eventToExecute.Part.vessel == null || eventToExecute.Part.partInfo == null) { eventToExecute.Finished(); return; }

            Part part = eventToExecute.Part;
            Rigidbody rb = part.Rigidbody;
            var realDistance = eventToExecute.Distance;
            var vesselMass = part.vessel.totalMass;
            if (vesselMass == 0) vesselMass = part.mass; // Sometimes if the root part is the only part of the vessel, then part.vessel.totalMass is 0, despite the part.mass not being 0.
            bool shapedEffect = ((warheadType == WarheadTypes.ShapedCharge || warheadType == WarheadTypes.ContinuousRod) && eventToExecute.withinAngleofEffect);


            if (BDArmorySettings.DEBUG_WEAPONS && shapedEffect)
            {
                Debug.Log($"[BDArmory.ExplosionFX] Part: {part.name}; Real Distance: {realDistance}m; SCRange: {SCRange}m;");
            }

            if ((realDistance <= Range) || (realDistance <= SCRange)) //within radius of Blast
            {
                if (!eventToExecute.IsNegativePressure)
                {
                    BlastInfo blastInfo;

                    if (eventToExecute.withinAngleofEffect) //within AoE of shaped warheads, or otherwise standard blast
                    {
                        blastInfo = BlastPhysicsUtils.CalculatePartBlastEffects(part, realDistance, vesselMass * 1000f, Power, Range);
                    }
                    else //majority of force concentrated in blast AoE for shaped warheads, not going to apply much force to stuff outside 
                    {
                        if (realDistance < Range / 2) //further away than half the blast range, falloff blast effect outside primary AoE
                        {
                            blastInfo = BlastPhysicsUtils.CalculatePartBlastEffects(part, realDistance, vesselMass * 1000f, Power / 3, Range / 2);
                        }
                        else { eventToExecute.Finished(); return; }
                    }
                    //if (BDArmorySettings.DEBUG_LABELS) Debug.Log("[BDArmory.ExplosionFX]: " + part.name + " Within AoE of detonation: " + eventToExecute.withinAngleofEffect);
                    // Overly simplistic approach: simply reduce damage by amount of HP/2 and Armor in the way. (HP/2 to simulate weak parts not fully blocking damage.) Does not account for armour reduction or angle of incidence of intermediate parts.
                    // A better approach would be to properly calculate the damage and pressure in CalculatePartBlastEffects due to the series of parts in the way.

                    var cumulativeHPOfIntermediateParts = eventToExecute.IntermediateParts.Select(p => p.Item2).Sum();
                    var cumulativeArmorOfIntermediateParts = eventToExecute.IntermediateParts.Select(p => p.Item3).Sum();
                    var damageWithoutIntermediateParts = blastInfo.Damage;

                    blastInfo.Damage = Mathf.Max(0f, blastInfo.Damage - 0.5f * cumulativeHPOfIntermediateParts - cumulativeArmorOfIntermediateParts);

                    if (CASEClamp > 0)
                    {
                        if (CASEClamp < 1000)
                        {
                            blastInfo.Damage = Mathf.Clamp(blastInfo.Damage, 0, Mathf.Min((part.Modules.GetModule<HitpointTracker>().GetMaxHitpoints() * 0.9f), CASEClamp));
                        }
                        else
                        {
                            blastInfo.Damage = Mathf.Clamp(blastInfo.Damage, 0, CASEClamp);
                        }
                    }

                    if (blastInfo.Damage > 0 || shapedEffect)
                    {
                        if (BDArmorySettings.DEBUG_DAMAGE)
                        {
                            Debug.Log(
                            $"[BDArmory.ExplosionFX]: Executing blast event Part: [{part.name}], VelocityChange: [{blastInfo.VelocityChange}], Distance: [{realDistance}]," +
                            $" TotalPressure: [{blastInfo.TotalPressure}], Damage: [{blastInfo.Damage}] (reduced from {damageWithoutIntermediateParts} by {eventToExecute.IntermediateParts.Count} parts)," +
                            $" EffectiveArea: [{blastInfo.EffectivePartArea}], Positive Phase duration: [{blastInfo.PositivePhaseDuration}]," +
                            $" Vessel mass: [{Math.Round(vesselMass * 1000f)}], TimeIndex: [{TimeIndex}], TimePlanned: [{eventToExecute.TimeToImpact}], NegativePressure: [{eventToExecute.IsNegativePressure}]");
                        }

                        // Add Reverse Negative Event
                        explosionEvents.Enqueue(new PartBlastHitEvent()
                        {
                            Distance = Range - realDistance,
                            Part = part,
                            TimeToImpact = 2 * (Range / ExplosionVelocity) + (Range - realDistance) / ExplosionVelocity,
                            IsNegativePressure = true,
                            NegativeForce = blastInfo.VelocityChange * 0.25f
                        });

                        if (rb != null && rb.mass > 0 && !BDArmorySettings.PAINTBALL_MODE)
                        {
                            AddForceAtPosition(rb,
                                (eventToExecute.HitPoint + rb.velocity * TimeIndex - Position).normalized *
                                blastInfo.VelocityChange *
                                BDArmorySettings.EXP_IMP_MOD,
                                eventToExecute.HitPoint + rb.velocity * TimeIndex);
                        }
                        var damage = 0f;
                        float penetrationFactor = 0.5f;
                        if (dmgMult < 0)
                        {
                            part.AddInstagibDamage();
                            //if (BDArmorySettings.DEBUG_LABELS) Debug.Log("[BDArmory.ExplosionFX]: applying instagib!");
                        }
                        var RA = part.FindModuleImplementing<ModuleReactiveArmor>();

                        if (RA != null && !RA.NXRA && (ExplosionSource == ExplosionSourceType.Bullet || ExplosionSource == ExplosionSourceType.Rocket) && (Caliber > RA.sensitivity && realDistance <= 0.1f)) //bullet/rocket hit
                        {
                            RA.UpdateSectionScales();
                        }
                        else
                        {
                            if (shapedEffect && ((warheadType == WarheadTypes.ShapedCharge) ? (realDistance <= SCRange) : warheadType == WarheadTypes.ContinuousRod))
                            {
                                //float HitAngle = Vector3.Angle((eventToExecute.HitPoint + rb.velocity * TimeIndex - Position).normalized, -eventToExecute.Hit.normal);
                                //float anglemultiplier = (float)Math.Cos(Math.PI * HitAngle / 180.0);
                                float anglemultiplier = Mathf.Abs(Vector3.Dot((eventToExecute.HitPoint + rb.velocity * TimeIndex - Position).normalized, -eventToExecute.Hit.normal));
                                float thickness = ProjectileUtils.CalculateThickness(part, anglemultiplier);
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.ExplosionFX]: Part {part.name} hit by {warheadType}; {Mathf.Rad2Deg * Mathf.Acos(anglemultiplier)} deg hit, armor thickness: {thickness}");
                                //float thicknessBetween = eventToExecute.IntermediateParts.Select(p => p.Item3).Sum(); //add armor thickness of intervening parts, if any
                                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.ExplosionFX]: Effective Armor thickness from intermediate parts: {thickness}");
                                //float penetration = 0;
                                float standoffTemp = realDistance / (14f * Caliber * 20f * 0.001f);
                                float standoffFactor = 1f / (1f + standoffTemp * standoffTemp);

                                float remainingPen = penetration * standoffFactor - cumulativeArmorOfIntermediateParts;

                                var Armor = part.FindModuleImplementing<HitpointTracker>();
                                if (Armor != null)
                                {
                                    float Ductility = Armor.Ductility;
                                    float hardness = Armor.Hardness;
                                    float Strength = Armor.Strength;
                                    float safeTemp = Armor.SafeUseTemp;
                                    float Density = Armor.Density;
                                    float armorEquiv = warheadType == WarheadTypes.ShapedCharge ? Armor.HEATEquiv : Armor.HEEquiv;
                                    //float vFactor = Armor.vFactor;
                                    //float muParam1 = Armor.muParam1;
                                    //float muParam2 = Armor.muParam2;
                                    //float muParam3 = Armor.muParam3;
                                    int type = (int)Armor.ArmorTypeNum;

                                    //penetration = ProjectileUtils.CalculatePenetration(Caliber, Caliber, warheadType == WarheadTypes.ShapedCharge ? Power / 2 : ProjMass, ExplosionVelocity, Ductility, Density, Strength, thickness, 1);
                                    // Moved penetration since it's now calculated off of a universal material rather than specific materials

                                    penetrationFactor = ProjectileUtils.CalculateArmorPenetration(part, remainingPen, thickness * armorEquiv);

                                    if (BDArmorySettings.DEBUG_WEAPONS)
                                    {
                                        Debug.Log($"[BDArmory.ExplosionFX] Penetration: {penetration} mm; Thickness: {thickness * armorEquiv} mm; armorEquiv: {armorEquiv}; Intermediate Armor: {penetration * standoffFactor - remainingPen} mm; Remaining Penetration: {remainingPen} mm; Penetration Factor: {penetrationFactor}; Standoff Factor: {standoffFactor}");
                                    }

                                    if (RA != null)
                                    {
                                        if (penetrationFactor > 1)
                                        {
                                            float thicknessModifier = RA.armorModifier;
                                            if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.ExplosionFX]: Beginning Reactive Armor Hit; NXRA: {RA.NXRA}; thickness Mod: {RA.armorModifier}");
                                            if (RA.NXRA) //non-explosive RA, always active
                                            {
                                                thickness *= thicknessModifier;
                                            }
                                            else
                                            {
                                                RA.UpdateSectionScales();
                                                eventToExecute.Finished();
                                                return;
                                            }
                                        }
                                        penetrationFactor = ProjectileUtils.CalculateArmorPenetration(part, remainingPen, thickness * armorEquiv); //RA stop round?
                                    }
                                    //else ProjectileUtils.CalculateArmorDamage(part, penetrationFactor, Caliber, hardness, Ductility, Density, ExplosionVelocity, SourceVesselName, ExplosionSourceType.Missile, type);
                                    else if (penetrationFactor > 0)
                                    {
                                        ProjectileUtils.CalculateArmorDamage(part, penetrationFactor, Caliber * 2.5f, hardness, Ductility, Density, warheadType == WarheadTypes.ShapedCharge ? 5000f : ExplosionVelocity, SourceVesselName, ExplosionSourceType.Missile, type);
                                    }
                                }
                                else
                                {
                                    // Based on 10 mm of aluminium
                                    penetrationFactor = 10f * (warheadType == WarheadTypes.ShapedCharge ? 0.5528789891f : 0.1601427673f) / (remainingPen);
                                }

                                if (penetrationFactor > 0)
                                {
                                    BulletHitFX.CreateBulletHit(part, eventToExecute.HitPoint, eventToExecute.Hit, eventToExecute.Hit.normal, true, Caliber, penetrationFactor > 0 ? penetrationFactor : 0f, null);
                                    damage = part.AddBallisticDamage(warheadType == WarheadTypes.ShapedCharge ? Power * 0.0555f : ProjMass, Caliber, 1f, penetrationFactor, dmgMult, warheadType == WarheadTypes.ShapedCharge ? 5000f : ExplosionVelocity, ExplosionSourceType.Missile);
                                }

                                if (penetrationFactor > 1)
                                {
                                    if (blastInfo.Damage > 0)
                                    {
                                        damage += part.AddExplosiveDamage(blastInfo.Damage, Caliber, ExplosionSource, dmgMult);
                                    }

                                    if (float.IsNaN(damage)) Debug.LogError("DEBUG NaN damage!");
                                }
                            }
                            else
                            {
                                if ((part == projectileHitPart && ProjectileUtils.IsArmorPart(part)) || !ProjectileUtils.CalculateExplosiveArmorDamage(part, blastInfo.TotalPressure, realDistance, SourceVesselName, eventToExecute.Hit, ExplosionSource, Range - realDistance)) //false = armor blowthrough or bullet detonating inside part
                                {
                                    if (RA != null && !RA.NXRA) //blast wave triggers RA; detonate all remaining RA sections
                                    {
                                        for (int i = 0; i < RA.sectionsRemaining; i++)
                                        {
                                            RA.UpdateSectionScales();
                                        }
                                    }
                                    else
                                    {
                                        damage = part.AddExplosiveDamage(blastInfo.Damage, Caliber, ExplosionSource, dmgMult);
                                        if (part == projectileHitPart && ProjectileUtils.IsArmorPart(part)) //deal armor damage to armor panel, since we didn't do that earlier
                                        {
                                            ProjectileUtils.CalculateExplosiveArmorDamage(part, blastInfo.TotalPressure, realDistance, SourceVesselName, eventToExecute.Hit, ExplosionSource, Range - realDistance);
                                        }
                                        penetrationFactor = damage / 10; //closer to the explosion/greater magnitude of the explosion at point blank, the greater the blowthrough
                                        if (float.IsNaN(damage)) Debug.LogError("DEBUG NaN damage!");
                                    }
                                }
                            }
                            if (damage > 0) //else damage from spalling done in CalcExplArmorDamage
                            {
                                if (BDArmorySettings.BATTLEDAMAGE)
                                {
                                    BattleDamageHandler.CheckDamageFX(part, Caliber, penetrationFactor, true, warheadType == WarheadTypes.ShapedCharge ? true : false, SourceVesselName, eventToExecute.Hit);
                                }
                                // Update scoring structures
                                //damage = Mathf.Clamp(damage, 0, part.Damage()); //if we want to clamp overkill score inflation
                                var aName = eventToExecute.SourceVesselName; // Attacker
                                var tName = part.vessel.GetName(); // Target
                                switch (ExplosionSource)
                                {
                                    case ExplosionSourceType.Bullet:
                                        BDACompetitionMode.Instance.Scores.RegisterBulletDamage(aName, tName, damage);
                                        break;
                                    case ExplosionSourceType.Rocket:
                                        BDACompetitionMode.Instance.Scores.RegisterRocketDamage(aName, tName, damage);
                                        break;
                                    case ExplosionSourceType.Missile:
                                        BDACompetitionMode.Instance.Scores.RegisterMissileDamage(aName, tName, damage);
                                        break;
                                    case ExplosionSourceType.BattleDamage:
                                        BDACompetitionMode.Instance.Scores.RegisterBattleDamage(aName, part.vessel, damage);
                                        break;
                                }
                            }
                        }
                    }
                    else if (BDArmorySettings.DEBUG_DAMAGE)
                    {
                        Debug.Log($"[BDArmory.ExplosionFX]: Part {part.name} at distance {realDistance}m took no damage due to parts with {cumulativeHPOfIntermediateParts} HP and {cumulativeArmorOfIntermediateParts} Armor in the way.");
                    }
                }
                else
                {
                    if (BDArmorySettings.DEBUG_DAMAGE)
                    {
                        Debug.Log(
                                $"[BDArmory.ExplosionFX]: Executing blast event Part: [{part.name}], VelocityChange: [{eventToExecute.NegativeForce}], Distance: [{realDistance}]," +
                                $" Vessel mass: [{Math.Round(vesselMass * 1000f)}], TimeIndex: [{TimeIndex}], TimePlanned: [{eventToExecute.TimeToImpact}], NegativePressure: [{eventToExecute.IsNegativePressure}]");
                    }
                    if (rb != null && rb.mass > 0 && !BDArmorySettings.PAINTBALL_MODE)
                        AddForceAtPosition(rb, (Position - part.transform.position).normalized * eventToExecute.NegativeForce * BDArmorySettings.EXP_IMP_MOD * 0.25f, part.transform.position);
                }
                eventToExecute.Finished();
            }
            if (warheadType == WarheadTypes.Standard && ProjMass > 0 && realDistance <= blastRange)
            {
                //float HitAngle = Vector3.Angle((eventToExecute.HitPoint + rb.velocity * TimeIndex - Position).normalized, -eventToExecute.Hit.normal);
                //float anglemultiplier = (float)Math.Cos(Math.PI * HitAngle / 180.0);
                float anglemultiplier = Mathf.Abs(Vector3.Dot((eventToExecute.HitPoint + rb.velocity * TimeIndex - Position).normalized, -eventToExecute.Hit.normal));
                float thickness = ProjectileUtils.CalculateThickness(part, anglemultiplier);
                var Armor = part.FindModuleImplementing<HitpointTracker>();
                if (Armor != null)
                {
                    thickness *= Armor.HEEquiv;
                }
                thickness += eventToExecute.IntermediateParts.Select(p => p.Item3).Sum(); //add armor thickness of intervening parts, if any
                if (BDArmorySettings.DEBUG_ARMOR) Debug.Log($"[BDArmory.ExplosiveFX]: Part {part.name} hit by shrapnel; {Mathf.Rad2Deg * Mathf.Acos(anglemultiplier)} deg hit, cumulative armor thickness: {thickness}");

                ProjectileUtils.CalculateShrapnelDamage(part, eventToExecute.Hit, Caliber, Power, realDistance, SourceVesselName, ExplosionSource, ProjMass, -1, thickness); //part hit by shrapnel, but not pressure wave
            }
        }

        // We use an ObjectPool for the ExplosionFx instances as they leak KSPParticleEmitters otherwise.
        static void CreateObjectPool(string explModelPath, string soundPath)
        {
            if (!string.IsNullOrEmpty(soundPath) && (!audioClips.ContainsKey(soundPath) || audioClips[soundPath] is null))
            {
                var audioClip = SoundUtils.GetAudioClip(soundPath);
                if (audioClip is null)
                {
                    Debug.LogError("[BDArmory.ExplosionFX]: " + soundPath + " was not found, using the default sound instead. Please fix your model.");
                    audioClip = SoundUtils.GetAudioClip(ModuleWeapon.defaultExplSoundPath);
                }
                audioClips.Add(soundPath, audioClip);
            }

            if (!explosionFXPools.ContainsKey(explModelPath) || explosionFXPools[explModelPath] == null)
            {
                var explosionFXTemplate = GameDatabase.Instance.GetModel(explModelPath);
                if (explosionFXTemplate == null)
                {
                    Debug.LogError("[BDArmory.ExplosionFX]: " + explModelPath + " was not found, using the default explosion instead. Please fix your model.");
                    explosionFXTemplate = GameDatabase.Instance.GetModel(ModuleWeapon.defaultExplModelPath);
                }
                var eFx = explosionFXTemplate.AddComponent<ExplosionFx>();
                eFx.audioSource = explosionFXTemplate.AddComponent<AudioSource>();
                eFx.audioSource.minDistance = 200;
                eFx.audioSource.maxDistance = 5500;
                eFx.audioSource.spatialBlend = 1;
                eFx.LightFx = explosionFXTemplate.AddComponent<Light>();
                eFx.LightFx.color = GUIUtils.ParseColor255("255,238,184,255");
                eFx.LightFx.intensity = 8;
                eFx.LightFx.shadows = LightShadows.None;
                explosionFXTemplate.SetActive(false);
                explosionFXPools[explModelPath] = ObjectPool.CreateObjectPool(explosionFXTemplate, 10, true, true, 0f, false);
            }
        }

        public static void CreateExplosion(Vector3 position, float tntMassEquivalent, string explModelPath, string soundPath, ExplosionSourceType explosionSourceType,
            float caliber = 120, Part explosivePart = null, string sourceVesselName = null, string sourceVesselTeam = null, string sourceWeaponName = null, Vector3 direction = default,
            float angle = 100f, bool isfx = false, float projectilemass = 0, float caseLimiter = -1, float dmgMutator = 1, string type = "standard", Part Hitpart = null,
            float apMod = 1f, float distancetravelled = -1, Vector3 sourceVelocity = default)
        {
            if (BDArmorySettings.DEBUG_MISSILES && explosionSourceType == ExplosionSourceType.Missile && (!explosionFXPools.ContainsKey(explModelPath) || !audioClips.ContainsKey(soundPath)))
            { Debug.Log($"[BDArmory.ExplosionFX]: Setting up object pool for explosion of type {explModelPath} with audio {soundPath}{(sourceWeaponName != null ? $" for {sourceWeaponName}" : "")}"); }
            CreateObjectPool(explModelPath, soundPath);

            Quaternion rotation;
            if (direction == default(Vector3))
            {
                rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            }
            else
            {
                rotation = Quaternion.LookRotation(direction);
            }

            GameObject newExplosion = explosionFXPools[explModelPath].GetPooledObject();
            newExplosion.transform.SetPositionAndRotation(position, rotation);
            ExplosionFx eFx = newExplosion.GetComponent<ExplosionFx>();
            eFx.Range = BlastPhysicsUtils.CalculateBlastRange(tntMassEquivalent);
            eFx.Position = position;
            eFx.Power = tntMassEquivalent;
            eFx.ExplosionSource = explosionSourceType;
            eFx.SourceVesselName = !string.IsNullOrEmpty(sourceVesselName) ? sourceVesselName : explosionSourceType == ExplosionSourceType.Missile ? (explosivePart != null && explosivePart.vessel != null ? explosivePart.vessel.GetName() : null) : null; // Use the sourceVesselName if specified, otherwise get the sourceVesselName from the missile if it is one.
            eFx.SourceVesselTeam = sourceVesselTeam;
            eFx.SourceWeaponName = sourceWeaponName;
            eFx.Caliber = caliber;
            eFx.ExplosivePart = explosivePart;
            eFx.Direction = direction;
            sourceVelocity = sourceVelocity != default ? sourceVelocity : (explosivePart != null && explosivePart.rb != null) ? explosivePart.rb.velocity + BDKrakensbane.FrameVelocityV3f : default; // Use the explosive part's velocity if the sourceVelocity isn't specified.
            eFx.Velocity = Hitpart != null ? Hitpart.vessel.Velocity() : sourceVelocity; // sourceVelocity is the real velocity w/o offloading.
            eFx.isFX = isfx;
            eFx.ProjMass = projectilemass;
            eFx.CASEClamp = caseLimiter;
            eFx.dmgMult = dmgMutator;
            eFx.projectileHitPart = Hitpart;
            eFx.pEmitters = newExplosion.GetComponentsInChildren<KSPParticleEmitter>();
            eFx.audioSource = newExplosion.GetComponent<AudioSource>();
            eFx.SoundPath = soundPath;
            type = type.ToLower();
            switch (type)
            {
                case "continuousrod":
                    eFx.warheadType = WarheadTypes.ContinuousRod;
                    //eFx.AngleOfEffect = 165;
                    eFx.Caliber = caliber > 0 ? caliber / 4 : 30;
                    eFx.ProjMass = 0.3f + (tntMassEquivalent / 75);
                    break;
                case "shapedcharge":
                    eFx.warheadType = WarheadTypes.ShapedCharge;
                    //eFx.AngleOfEffect = 10f;
                    //eFx.AngleOfEffect = 5f;
                    eFx.cosAngleOfEffect = Mathf.Cos(Mathf.Deg2Rad * 5f); // cos(5 degrees)
                    eFx.Caliber = caliber > 0 ? caliber * 0.05f : 6f;

                    // Hypervelocity jet caliber determined by rule of thumb equation for the caliber based on
                    // "The Hollow Charge Effect" Bulletin of the Institution of Mining and Metallurgy. No. 520, March 1950
                    // by W. M. Evans. Jet is approximately 20% of the caliber.

                    eFx.apMod = apMod;
                    eFx.travelDistance = distancetravelled;
                    break;
                default:
                    eFx.warheadType = WarheadTypes.Standard;
                    eFx.cosAngleOfEffect = angle >= 0f ? Mathf.Clamp(angle, 0f, 180f) : 100f;
                    eFx.cosAngleOfEffect = Mathf.Cos(Mathf.Deg2Rad * eFx.cosAngleOfEffect);
                    break;
            }

            if (type == "shapedcharge" || type == "continuousrod")
            {
                eFx.penetration = ProjectileUtils.CalculatePenetration(eFx.Caliber, type == "shapedcharge" ? 5000f : ExplosionVelocity, type == "shapedcharge" ? tntMassEquivalent * 0.0555f : eFx.ProjMass, apMod);
                // Approximate fitting of mass to tntMass for modern shaped charges was done,
                // giving the estimate of 0.0555*tntMass which works surprisingly well for modern
                // warheads. 5000 m/s is around the average velocity of the jet. In reality, the
                // jet has a velocity which linearly decreases from the tip to the tail, with the
                // velocity being O(detVelocity) at the tip and O(1/4*detVelocity) at the tail.
                // The linear estimate is also from "The Hollow Charge Effect", however this is
                // too complex for the non-numerical penetration model used. Note that the density
                // of the liner is far overestimated here, however this is accounted for in the
                // estimate of the liner mass and the simple fit for liner mass of modern warheads
                // is surprisingly good using the above formula.
            }
            else
            {
                eFx.penetration = 0;
            }

            if (direction == default(Vector3) && explosionSourceType == ExplosionSourceType.Missile)
            {
                eFx.warheadType = WarheadTypes.Standard;
                if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.ExplosionFX]: No direction param specified, defaulting warhead type!");
            }
            if (tntMassEquivalent <= 5)
            {
                eFx.audioSource.minDistance = 4f;
                eFx.audioSource.maxDistance = 3000;
                eFx.audioSource.priority = 9999;
            }
            newExplosion.SetActive(true);
        }

        public static void AddForceAtPosition(Rigidbody rb, Vector3 force, Vector3 position)
        {
            //////////////////////////////////////////////////////////
            // Add The force to part
            //////////////////////////////////////////////////////////
            if (rb == null || rb.mass == 0) return;
            rb.AddForceAtPosition(force, position, ForceMode.VelocityChange);
            if (BDArmorySettings.DEBUG_DAMAGE)
            {
                Debug.Log($"[BDArmory.ExplosionFX]: Force Applied | Explosive : {Math.Round(force.magnitude, 2)}");
            }
        }

        public static void DisableAllExplosionFX()
        {
            if (explosionFXPools == null) return;
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.ExplosionFx]: Setting {explosionFXPools.Values.Where(pool => pool != null && pool.pool != null).Sum(pool => pool.pool.Count(fx => fx != null && fx.activeInHierarchy))} explosion FX inactive.");
            foreach (var pool in explosionFXPools.Values)
            {
                if (pool == null || pool.pool == null) continue;
                foreach (var fx in pool.pool)
                {
                    if (fx == null) continue;
                    fx.SetActive(false);
                }
            }
        }
    }

    public abstract class BlastHitEvent
    {
        public float Distance { get; set; }
        public float TimeToImpact { get; set; }
        public bool IsNegativePressure { get; set; }
    }

    internal class PartBlastHitEvent : BlastHitEvent
    {
        public Part Part { get; set; }
        public Vector3 HitPoint { get; set; }
        public RaycastHit Hit { get; set; }
        public float NegativeForce { get; set; }
        public string SourceVesselName { get; set; }
        public bool withinAngleofEffect { get; set; }
        public List<(float, float, float)> IntermediateParts
        {
            get
            {
                if (_intermediateParts is not null && _intermediateParts.inUse)
                    return _intermediateParts.value;
                else // It's a blank or null pool entry, set things up.
                {
                    _intermediateParts = intermediatePartsPool.GetPooledObject();
                    if (_intermediateParts.value is null) _intermediateParts.value = new List<(float, float, float)>();
                    _intermediateParts.value.Clear();
                    return _intermediateParts.value;
                }
            }
            set // Note: this doesn't set the _intermediateParts.value to value, but rather copies the elements into the existing list. This should avoid excessive GC allocations.
            {
                if (_intermediateParts is null || !_intermediateParts.inUse) _intermediateParts = intermediatePartsPool.GetPooledObject();
                _intermediateParts.value.Clear();
                _intermediateParts.value.AddRange(value);
            }
        } // distance, HP, armour

        ObjectPoolEntry<List<(float, float, float)>> _intermediateParts;

        public void Finished() // Return the IntermediateParts list back to the pool and free up memory.
        {
            if (_intermediateParts is null) return;
            _intermediateParts.inUse = false;
            if (_intermediateParts.value is null) return;
            _intermediateParts.value.Clear();
        }
        static ObjectPoolNonUnity<List<(float, float, float)>> intermediatePartsPool = new ObjectPoolNonUnity<System.Collections.Generic.List<(float, float, float)>>(); // Pool the IntermediateParts lists to avoid GC alloc.
    }


    internal class BuildingBlastHitEvent : BlastHitEvent
    {
        public DestructibleBuilding Building { get; set; }
    }

    /// <summary>
    /// Comparer for raycast hit sorting.
    /// </summary>
    internal class RaycastHitComparer : IComparer<RaycastHit>
    {
        int IComparer<RaycastHit>.Compare(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
        public static RaycastHitComparer raycastHitComparer = new RaycastHitComparer();
    }
}
