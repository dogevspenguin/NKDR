using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Competition;
using BDArmory.Damage;
using BDArmory.Extensions;
using BDArmory.GameModes;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.Weapons;
using KSPAssets;
namespace BDArmory.FX
{
    public class NukeFX : MonoBehaviour
    {
        public static Dictionary<string, ObjectPool> nukePool = new Dictionary<string, ObjectPool>();
        public static Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(); // Pool the audio clips separately.

        private bool hasDetonated = false;
        private float startTime;
        float yieldCubeRoot;
        private float lastValidAtmDensity = 0f;

        HashSet<Part> partsHit = new HashSet<Part>();
        public Light LightFx { get; set; }
        public KSPParticleEmitter[] pEmitters { get; set; }
        public float StartTime { get; set; }
        public string SoundPath { get; set; }
        public AudioSource audioSource { get; set; }
        public float thermalRadius { get; set; } //clamped blast range
        public float fluence { get; set; } //thermal magnitude

        public float detonationTimer { get; set; } //seconds to delay before detonation
        public bool isEMP { get; set; } //do EMP effects?
        public bool scaleByYield { get; set; } //do scale by yield

        public float fluenceTime { get; set; } //do scale by yield
        public float effectLifetime { get; set; } //do scale by yield
        public float emitTime { get; set; } //do scale by yieldfireballEmitTime
        public float fireballEmitTime { get; set; }
        private float MaxTime { get; set; }
        public ExplosionSourceType ExplosionSource { get; set; }
        public string SourceVesselName { get; set; }
        public string ReportingName { get; set; }
        public float yield { get; set; } //kilotons
        public Vector3 Position { get { return _position; } set { _position = value; transform.position = _position; } }
        Vector3 _position;
        public Vector3 Velocity { get; set; }
        public Part ExplosivePart { get; set; }
        public float TimeIndex => Time.time - StartTime;
        public string flashModelPath { get; set; }
        public string shockModelPath { get; set; }
        public string blastModelPath { get; set; }
        public string plumeModelPath { get; set; }
        public string debrisModelPath { get; set; }
        public string blastSoundPath { get; set; }

        public string explModelPath = "BDArmory/Models/explosion/explosion";

        public string explSoundPath = "BDArmory/Sounds/explode1";

        Queue<NukeHitEvent> explosionEvents = new Queue<NukeHitEvent>();
        List<NukeHitEvent> explosionEventsPreProcessing = new List<NukeHitEvent>();
        List<Part> explosionEventsPartsAdded = new List<Part>();
        List<DestructibleBuilding> explosionEventsBuildingAdded = new List<DestructibleBuilding>();
        Dictionary<string, int> explosionEventsVesselsHit = new Dictionary<string, int>();

        private float EMPRadius = 100;
        private float scale = 1;
        const int explosionLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.EVA | LayerMasks.Unknown19 | LayerMasks.Unknown23 | LayerMasks.Wheels); // Why 19 and 23?

        static RaycastHit[] lineOfSightHits;
        static RaycastHit[] reverseHits;
        Collider[] blastHitColliders = new Collider[100];
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
        List<FXEmitter> fxEmitters = new();

        internal static HashSet<ExplosionSourceType> ignoreCasingFor = new HashSet<ExplosionSourceType> { ExplosionSourceType.Missile, ExplosionSourceType.Rocket };

        void Awake()
        {
            if (lineOfSightHits == null) { lineOfSightHits = new RaycastHit[100]; }
            if (reverseHits == null) { reverseHits = new RaycastHit[100]; }
            if (IgnoreParts == null) { IgnoreParts = new List<Part>(); }
            if (IgnoreBuildings == null) { IgnoreBuildings = new List<DestructibleBuilding>(); }
        }

        private void OnEnable()
        {
            StartTime = Time.time;
            MaxTime = effectLifetime; //BDAMath.Sqrt((thermalRadius / ExplosionVelocity) * 3f) * 2f; // Scale MaxTime to get a reasonable visualisation of the explosion.
            scale = 1;//BDAMath.Sqrt(400 * (6 * yield)) / 219;
            if (BDArmorySettings.DEBUG_DAMAGE)
            {
                Debug.Log($"[BDArmory.NukeFX]: Explosion started! yield: {yield}  BlastRadius: {thermalRadius} StartTime: {StartTime}, Duration: {MaxTime}");
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                yieldCubeRoot = Mathf.Pow(yield, 1f / 3f);
                startTime = Time.time;
                if (FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(Position),
                                   FlightGlobals.getExternalTemperature(Position)) > 0)
                    lastValidAtmDensity = (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(Position),
                                       FlightGlobals.getExternalTemperature(Position));
                hasDetonated = false;

                //EMP output increases as the sqrt of yield (determined power) and prompt gamma output (~0.5% of yield)
                //srf detonation is capped to about 16km, < 10km alt electrons qucikly absorbed by atmo.
                //above 10km, emp radius can easily reach 100s of km. But that's no fun, so...
                if (FlightGlobals.getAltitudeAtPos(Position) < 10000)
                {
                    EMPRadius = BDAMath.Sqrt(yield) * 500;
                }
                else
                {
                    EMPRadius = BDAMath.Sqrt(yield) * 1000;
                }

                fxEmitters.Clear();
                pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
                foreach (var pe in pEmitters)
                    if (pe != null)
                    {
                        pe.emit = true;
                        pe.useWorldSpace = false; // Don't use worldspace, so that we can move the FX properly.
                        var emission = pe.ps.emission;
                        emission.enabled = true;
                        EffectBehaviour.AddParticleEmitter(pe);
                    }
                LightFx = gameObject.GetComponent<Light>();
                LightFx.range = 0;
                audioSource = gameObject.GetComponent<AudioSource>();
                if (!string.IsNullOrEmpty(SoundPath))
                {
                    audioSource.PlayOneShot(audioClips[SoundPath]);
                }
            }
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
            fxEmitters.Clear();
            ExplosivePart = null; // Clear the Part reference.
            explosionEvents.Clear(); // Make sure we don't have any left over events leaking memory.
            explosionEventsPreProcessing.Clear();
            explosionEventsPartsAdded.Clear();
            explosionEventsBuildingAdded.Clear();
            explosionEventsVesselsHit.Clear();
        }

        private void CalculateBlastEvents()
        {
            using (var enuEvents = ProcessingBlastSphere().OrderBy(e => e.TimeToImpact).GetEnumerator())
            {
                while (enuEvents.MoveNext())
                {
                    if (enuEvents.Current == null) continue;

                    explosionEvents.Enqueue(enuEvents.Current);
                }
            }
        }

        private List<NukeHitEvent> ProcessingBlastSphere()
        {
            explosionEventsPreProcessing.Clear();
            explosionEventsPartsAdded.Clear();
            explosionEventsBuildingAdded.Clear();
            explosionEventsVesselsHit.Clear();

            var hitCount = Physics.OverlapSphereNonAlloc(Position, thermalRadius * 2f, blastHitColliders, explosionLayerMask);
            if (hitCount == blastHitColliders.Length)
            {
                blastHitColliders = Physics.OverlapSphere(Position, thermalRadius * 2f, explosionLayerMask);
                hitCount = blastHitColliders.Length;
            }
            using (var hitCollidersEnu = blastHitColliders.Take(hitCount).GetEnumerator())
            {
                while (hitCollidersEnu.MoveNext())
                {
                    if (hitCollidersEnu.Current == null) continue;
                    try
                    {
                        Part partHit = hitCollidersEnu.Current.GetComponentInParent<Part>();
                        if (partHit != null)
                        {
                            if (ProjectileUtils.IsIgnoredPart(partHit)) continue; // Ignore ignored parts.
                            if (partHit.mass > 0 && !explosionEventsPartsAdded.Contains(partHit))
                            {
                                var damaged = ProcessPartEvent(partHit, SourceVesselName, explosionEventsPreProcessing, explosionEventsPartsAdded);
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
                                    if (Physics.Raycast(ray, out rayHit, thermalRadius, explosionLayerMask))
                                    {
                                        //DestructibleBuilding destructibleBuilding = rayHit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();

                                        distance = Vector3.Distance(Position, rayHit.point);
                                        if (building.IsIntact)
                                        {
                                            explosionEventsPreProcessing.Add(new BuildingNukeHitEvent() { Distance = distance, Building = building, TimeToImpact = distance / ExplosionVelocity });
                                            explosionEventsBuildingAdded.Add(building);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[BDArmory.NukeFX]: Exception in overlapSphere collider processing: {e.Message}\n{e.StackTrace}");
                    }
                }
            }
            if (explosionEventsVesselsHit.Count > 0)
            {
                if (ExplosionSource != ExplosionSourceType.Bullet || ExplosionSource != ExplosionSourceType.Rocket)
                {
                    string message = "";
                    foreach (var vesselName in explosionEventsVesselsHit.Keys)
                        message += (message == "" ? "" : " and ") + vesselName + " had " + explosionEventsVesselsHit[vesselName];
                    if (ExplosionSource == ExplosionSourceType.Missile)
                    {
                        message += " parts damaged due to missile strike";
                    }
                    else //ExplosionType BattleDamage || Other
                    {
                        message += " parts damaged due to explosion";
                    }
                    message += (ReportingName != null ? " (" + ReportingName + ")" : "") + (SourceVesselName != null ? " from " + SourceVesselName : "") + ".";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                }
                // Note: damage hasn't actually been applied to the parts yet, just assigned as events, so we can't know if they survived.
                foreach (var vesselName in explosionEventsVesselsHit.Keys) // Note: sourceVesselName is already checked for being in the competition before damagedVesselName is added to explosionEventsVesselsHitByMissiles, so we don't need to check it here.
                {
                    switch (ExplosionSource)
                    {
                        case ExplosionSourceType.Missile:
                            BDACompetitionMode.Instance.Scores.RegisterMissileStrike(SourceVesselName, vesselName);
                            break;
                    }
                }
            }
            return explosionEventsPreProcessing;
        }

        private bool ProcessPartEvent(Part part, string sourceVesselName, List<NukeHitEvent> eventList, List<Part> partsAdded)
        {
            Ray LoSRay = new Ray(Position, part.transform.position - Position);
            RaycastHit hit;
            var distToG0 = Math.Max((Position - part.transform.position).magnitude, 1f);
            if (Physics.Raycast(LoSRay, out hit, distToG0, explosionLayerMask)) // only add impulse to parts with line of sight to detonation
            {
                KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                if (lastValidAtmDensity < 0.1)
                {
                    if (p == part) //if exoatmo, impulse/thermal bloom only to parts in LoS
                    {
                        eventList.Add(new PartNukeHitEvent()
                        {
                            Distance = distToG0,
                            Part = part,
                            TimeToImpact = distToG0 / ExplosionVelocity,
                            HitPoint = hit.point,
                            Hit = hit,
                            SourceVesselName = sourceVesselName,
                        });

                        partsAdded.Add(part);
                        return true;
                    }
                    return false;
                }
                else
                {
                    eventList.Add(new PartNukeHitEvent() //else everything heated/hit by shockwave
                    {
                        Distance = distToG0,
                        Part = part,
                        TimeToImpact = distToG0 / ExplosionVelocity,
                        HitPoint = hit.point,
                        Hit = hit,
                        SourceVesselName = sourceVesselName,
                    });

                    partsAdded.Add(part);
                    return true;
                }
            }

            return false;
        }

        public void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (hasDetonated)
                {
                    if (LightFx != null) LightFx.intensity -= 0.020f*fluence/fluenceTime;
                    if (TimeIndex > emitTime && pEmitters != null) // 0.3s seems to be enough to always show the explosion, but 0.2s isn't for some reason.
                    {
                        if (TimeIndex > emitTime && pEmitters != null) // 0.3s seems to be enough to always show the explosion, but 0.2s isn't for some reason.
                        {
                            foreach (var pe in pEmitters)
                            {
                                if (pe == null) continue;
                                pe.emit = false;
                            }
                        }
                    }
                    foreach (var fx in fxEmitters) if (fx.gameObject.activeSelf) fx.Position = Position; // Update FX emitter positions.
                }
            }
        }

        public void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

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
            if (Time.time - startTime > detonationTimer)
            {
                if (!hasDetonated)
                {
                    hasDetonated = true;
                    CalculateBlastEvents();

                    LightFx = gameObject.GetComponent<Light>();
                    LightFx.range = thermalRadius;
                    LightFx.intensity = fluence;
                    if (lastValidAtmDensity < 0.05)
                    {
                        if (!string.IsNullOrWhiteSpace(flashModelPath))
                            fxEmitters.Add(FXEmitter.CreateFX(Position, scale * 50f, flashModelPath, "", 0.4f, 0.4f));
                        if (!string.IsNullOrWhiteSpace(shockModelPath))
                            fxEmitters.Add(FXEmitter.CreateFX(Position, scale * 14f, shockModelPath, "", 0.2f, 0.6f));
                    }
                    else
                    {
                        //default model scaled for 20kt; yield = 20 = scale of 1
                        //scaling calc is roughly SqRt( 400 * (6x))
                        //fireball diameter is 59 * Mathf.Pow(yield, 0.4f), apparently?
                        if (!string.IsNullOrWhiteSpace(flashModelPath))
                            fxEmitters.Add(FXEmitter.CreateFX(Position, scale, flashModelPath, "", emitTime, effectLifetime, default, true));
                        if (!string.IsNullOrWhiteSpace(shockModelPath))
                            fxEmitters.Add(FXEmitter.CreateFX(Position, scale * lastValidAtmDensity, shockModelPath, "", emitTime, effectLifetime, default, true));
                        if (!string.IsNullOrWhiteSpace(blastModelPath))
                         Debug.Log($"[BDArmory.NukeFX]: EMIT TIME: {emitTime}  FIREBALL EMT: {fireballEmitTime}, Duration: {MaxTime}");
                        fxEmitters.Add(FXEmitter.CreateFX(Position, scale, blastModelPath, blastSoundPath, emitTime, effectLifetime, default, true));

                        if (BodyUtils.GetRadarAltitudeAtPos(Position) < 200 * scale)
                        {
                            double latitudeAtPos = FlightGlobals.currentMainBody.GetLatitude(Position);
                            double longitudeAtPos = FlightGlobals.currentMainBody.GetLongitude(Position);
                            double altitude = FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos);
                            if (!string.IsNullOrWhiteSpace(plumeModelPath))
                                FXEmitter.CreateFX(FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitudeAtPos, longitudeAtPos, altitude), Mathf.Clamp(scale, 0.01f, 3f), plumeModelPath, "", effectLifetime, effectLifetime, default, true, true);
                            if (!string.IsNullOrWhiteSpace(debrisModelPath))
                                FXEmitter.CreateFX(FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitudeAtPos, longitudeAtPos, altitude), scale, debrisModelPath, "", 1.5f, effectLifetime, default, true);
                        }
                    }
                }
            }
            if (hasDetonated)
            {
                while (explosionEvents.Count > 0 && explosionEvents.Peek().TimeToImpact <= TimeIndex)
                {
                    NukeHitEvent eventToExecute = explosionEvents.Dequeue();

                    var partBlastHitEvent = eventToExecute as PartNukeHitEvent;
                    if (partBlastHitEvent != null)
                    {
                        ExecutePartBlastEvent(partBlastHitEvent);
                    }
                    else
                    {
                        ExecuteBuildingBlastEvent((BuildingNukeHitEvent)eventToExecute);
                    }
                }
            }

            if (hasDetonated && explosionEvents.Count == 0 && TimeIndex > MaxTime)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        private void ExecuteBuildingBlastEvent(BuildingNukeHitEvent eventToExecute)
        {
            DestructibleBuilding building = eventToExecute.Building;
            //Debug.Log("[BDArmory.NukeFX] Beginning building hit");
            if (building && building.IsIntact)
            {
                var distToEpicenter = Mathf.Max((Position - building.transform.position).magnitude, 1f);
                var blastImpulse = Mathf.Pow(3.01f * 1100f / distToEpicenter, 1.25f) * 6.894f * Mathf.Max(lastValidAtmDensity, 0.05f) * yieldCubeRoot;
                // Debug.Log($"[BDArmory.NukeFX]: Building hit; distToG0: {distToEpicenter}, yield: {yield}, building: {building.name}, lastValidAtmDensity: {lastValidAtmDensity}, impulse: {blastImpulse}");

                if (!double.IsNaN(blastImpulse)) //140kPa, level at which reinforced concrete structures are destroyed
                {
                    // Debug.Log("[BDArmory.NukeFX]: Building Impulse: " + blastImpulse);
                    if (blastImpulse > 140)
                    {
                        building.Demolish();
                    }
                }
            }
        }

        private void ExecutePartBlastEvent(PartNukeHitEvent eventToExecute)
        {
            if (eventToExecute.Part == null || eventToExecute.Part.Rigidbody == null || eventToExecute.Part.vessel == null || eventToExecute.Part.partInfo == null) return;

            Part part = eventToExecute.Part;
            Rigidbody rb = part.Rigidbody;
            //var realDistance = eventToExecute.Distance; //this provides a snapshot of distance at time of detonation; with multi-second lag between detonation and blastwave reaching target, target could fly outzide blastzone
            var realDistance = Math.Max((Position - part.transform.position).magnitude, 1f);
            if (realDistance > thermalRadius) return; //craft has flown out of blast zone by time blastfront has arrived at original distance
            var vesselMass = part.vessel.totalMass;
            if (vesselMass == 0) vesselMass = part.mass; // Sometimes if the root part is the only part of the vessel, then part.vessel.totalMass is 0, despite the part.mass not being 0.
            float radiativeArea = !double.IsNaN(part.radiativeArea) ? (float)part.radiativeArea : part.GetArea();
            if (!BDArmorySettings.PAINTBALL_MODE)
            {
                if (!eventToExecute.IsNegativePressure)
                {
                    if (BDArmorySettings.DEBUG_DAMAGE && double.IsNaN(part.radiativeArea))
                    {
                        Debug.Log($"[BDArmory.NukeFX]: radiative area of part {part} was NaN, using approximate area {radiativeArea} instead.");
                    }
                    double blastImpulse;
                    if (lastValidAtmDensity > 0.1)
                        blastImpulse = Mathf.Pow(3.01f * 1100f / realDistance, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot; // * (radiativeArea / 3f); pascals/m isn't going to increase if a larger surface area, it's still going go be same force
                    else
                        blastImpulse = (part.mass * 15295.74) / (4 * Math.PI * Math.Pow(realDistance, 2.0)) * (part.radiativeArea / 3.0);
                    if (blastImpulse > 0)
                    {
                        float damage = 0;
                        //float blastDamage = ((float)((yield * (45000000 * BDArmorySettings.EXP_DMG_MOD_MISSILE)) / (4f * Mathf.PI * realDistance * realDistance) * (radiativeArea / 2f)));
                        //this shouldn't scale linearly
                        float blastDamage = (float)blastImpulse; //* BDArmorySettings.EXP_DMG_MOD_MISSILE; //DMG_Mod is substantially increasing blast radius above what it should be
                        if (float.IsNaN(blastDamage))
                        {
                            Debug.LogWarning($"[BDArmory.NukeFX]: blast damage is NaN. distToG0: {realDistance}, yield: {yield}, part: {part}, radiativeArea: {radiativeArea}");
                        }
                        else
                        {
                            if (!ProjectileUtils.CalculateExplosiveArmorDamage(part, blastImpulse, realDistance, SourceVesselName, eventToExecute.Hit, ExplosionSource, thermalRadius - realDistance)) //false = armor blowthrough
                            {
                                damage = part.AddExplosiveDamage(blastDamage, 1, ExplosionSource, 1);
                            }
                            if (damage > 0) //else damage from spalling done in CalcExplArmorDamage
                            {
                                if (BDArmorySettings.BATTLEDAMAGE)
                                {
                                    BattleDamageHandler.CheckDamageFX(part, 50, 0.5f, true, false, SourceVesselName, eventToExecute.Hit);
                                }
                                // Update scoring structures
                                if (BDACompetitionMode.Instance) //moving this here - only give scores to stuff still inside blast radius when blastfront arrives
                                {
                                    bool registered = false;
                                    var damagedVesselName = part.vessel != null ? part.vessel.GetName() : null;
                                    switch (ExplosionSource)
                                    {
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
                                var aName = eventToExecute.SourceVesselName; // Attacker
                                var tName = part.vessel.GetName(); // Target
                                switch (ExplosionSource)
                                {
                                    case ExplosionSourceType.Missile:
                                        BDACompetitionMode.Instance.Scores.RegisterMissileDamage(aName, tName, damage);
                                        break;
                                    case ExplosionSourceType.BattleDamage:
                                        BDACompetitionMode.Instance.Scores.RegisterBattleDamage(aName, part.vessel, damage);
                                        break;
                                }
                            }
                        }

                        if (rb != null && rb.mass > 0)
                        {
                            if (double.IsNaN(blastImpulse))
                            {
                                Debug.LogWarning($"[BDArmory.NukeFX]: blast impulse is NaN. distToG0: {realDistance}, vessel: {part.vessel}, atmDensity: {lastValidAtmDensity}, yield^(1/3): {yieldCubeRoot}, partHit: {part}, radiativeArea: {radiativeArea}");
                            }
                            else
                            {
                                if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.NukeFX]: Applying " + blastImpulse.ToString("0.0") + " impulse to " + part + " of mass " + part.mass + " at distance " + realDistance + "m");
                                rb.AddForceAtPosition((part.transform.position - Position).normalized * ((float)blastImpulse * (radiativeArea / 3f)), part.transform.position, ForceMode.Impulse);
                            }
                        }
                        // Add Reverse Negative Event
                        explosionEvents.Enqueue(new PartNukeHitEvent()
                        {
                            Distance = thermalRadius - realDistance,
                            Part = part,
                            TimeToImpact = 2 * (thermalRadius / ExplosionVelocity) + (thermalRadius - realDistance) / ExplosionVelocity,
                            IsNegativePressure = true,
                            NegativeForce = (float)blastImpulse * 0.25f
                        });
                    }
                    else if (BDArmorySettings.DEBUG_DAMAGE)
                    {
                        Debug.Log("[BDArmory.NukeFX]: Part " + part.name + " at distance " + realDistance + "m took no damage");
                    }
                    //part.skinTemperature += fluence * 3370000000 / (4 * Math.PI * (realDistance * realDistance)) * radiativeArea / 2; // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m,
                    part.skinTemperature += (fluence * (337000000 * BDArmorySettings.EXP_DMG_MOD_MISSILE) / (4 * Math.PI * (realDistance * realDistance))); // everything gets heated via atmosphere
                    if (isEMP)
                    {
                        if (part == part.vessel.rootPart) //don't apply EMP buildup per part
                        {
                            var EMP = part.vessel.rootPart.FindModuleImplementing<ModuleDrainEC>();
                            if (EMP == null)
                            {
                                EMP = (ModuleDrainEC)part.vessel.rootPart.AddModule("ModuleDrainEC");
                            }
                            EMP.incomingDamage = ((EMPRadius / realDistance) * 100); //this way craft at edge of blast might only get disabled instead of bricked
                                                                                     //work on a better EMP damage value, in case of configs with very large thermalRadius
                            EMP.softEMP = false;                                     //IRL EMP intensity/magnitude enerated by nuke explosion is more or less constant within AoE rather than tapering off, but that's no fun
                        }
                    }
                }
                else
                {
                    if (rb != null && rb.mass > 0)
                    {
                        if (double.IsNaN(eventToExecute.NegativeForce))
                        {
                            Debug.LogWarning("[BDArmory.NukeFX]: blast impulse is NaN. distToG0: " + realDistance + ", vessel: " + part.vessel + ", atmDensity: " + lastValidAtmDensity + ", yield^(1/3): " + yieldCubeRoot + ", partHit: " + part + ", radiativeArea: " + radiativeArea);
                        }
                        else
                        {
                            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.NukeFX]: Applying " + eventToExecute.NegativeForce.ToString("0.0") + " impulse to " + part + " of mass " + part.mass + " at distance " + realDistance + "m");
                            rb.AddForceAtPosition((Position - part.transform.position).normalized * eventToExecute.NegativeForce * BDArmorySettings.EXP_IMP_MOD * 0.25f, part.transform.position, ForceMode.Impulse);
                        }
                    }
                }
            }
        }

        // We use an ObjectPool for the ExplosionFx instances as they leak KSPParticleEmitters otherwise.
        static void SetupPool(string ModelPath, string soundPath, float radius)
        {
            if (!string.IsNullOrEmpty(soundPath) && (!audioClips.ContainsKey(soundPath) || audioClips[soundPath] is null))
            {
                var audioClip = SoundUtils.GetAudioClip(soundPath);
                if (audioClip is null)
                {
                    Debug.LogError("[BDArmory.NukeFX]: " + soundPath + " was not found, using the default sound instead. Please fix your model.");
                    audioClip = SoundUtils.GetAudioClip(ModuleWeapon.defaultExplSoundPath);
                }
                audioClips.Add(soundPath, audioClip);
            }

            if (!nukePool.ContainsKey(ModelPath) || nukePool[ModelPath] == null)
            {
                GameObject templateFX;
                if (!string.IsNullOrEmpty(ModelPath))
                {
                    templateFX = GameDatabase.Instance.GetModel(ModelPath);
                    if (templateFX == null)
                    {
                        //Debug.LogError("[BDArmory.NukeFX]: " + ModelPath + " was not found, using the default explosion instead. Please fix your model.");
                        templateFX = GameDatabase.Instance.GetModel(ModuleWeapon.defaultExplModelPath);
                    }
                }
                else templateFX = GameDatabase.Instance.GetModel("BDArmory/Models/shell/model"); //near enough to invisible; model support pre-FXEmitter spawning of Nuke blast FX is only for chernobyl/mutator support for spawning a bomb model in the delay between initializing the nuke and it detonating
                var eFx = templateFX.AddComponent<NukeFX>();
                eFx.audioSource = templateFX.AddComponent<AudioSource>();
                eFx.audioSource.minDistance = 200;
                eFx.audioSource.maxDistance = radius * 3;
                eFx.audioSource.spatialBlend = 1;
                eFx.audioSource.volume = 5;
                eFx.LightFx = templateFX.AddComponent<Light>();
                eFx.LightFx.color = GUIUtils.ParseColor255("255,238,184,255");
                eFx.LightFx.intensity = radius / 3;
                eFx.LightFx.shadows = LightShadows.None;
                templateFX.SetActive(false);
                nukePool[ModelPath] = ObjectPool.CreateObjectPool(templateFX, 10, true, true, 0f, false);
            }
        }
        public static void CreateExplosion(Vector3 position, ExplosionSourceType explosionSourceType, string sourceVesselName, string sourceWeaponName = "Nuke",
            float delay = 2.5f, float blastRadius = 750, float Yield = 0.05f, float thermalShock = 0.05f, bool emp = true, string blastSound = "",
            string flashModel = "", string shockModel = "", string blastModel = "", string plumeModel = "", string debrisModel = "", string ModelPath = "", string soundPath = "",
            Part nukePart = null, Part hitPart = null, Vector3 sourceVelocity = default, float fireballT=1.5f,float emitT=0.3f,float fluenceT = 0.35f,float effT = 30f)
        {
            SetupPool(ModelPath, soundPath, blastRadius);

            Quaternion rotation;
            rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            GameObject newExplosion = nukePool[ModelPath + soundPath].GetPooledObject();
            NukeFX eFx = newExplosion.GetComponent<NukeFX>();
            newExplosion.transform.SetPositionAndRotation(position, rotation);

            eFx.Position = position;
            sourceVelocity = sourceVelocity != default ? sourceVelocity : (nukePart != null && nukePart.rb != null) ? nukePart.rb.velocity + BDKrakensbane.FrameVelocityV3f : default; // Use the explosive part's velocity if the sourceVelocity isn't specified.
            eFx.Velocity = (hitPart != null && hitPart.rb != null) ? hitPart.rb.velocity + BDKrakensbane.FrameVelocityV3f : sourceVelocity; // sourceVelocity is the real velocity w/o offloading.
            eFx.ExplosionSource = explosionSourceType;
            eFx.SourceVesselName = sourceVesselName;
            eFx.ReportingName = sourceWeaponName;
            eFx.explModelPath = ModelPath;
            eFx.explSoundPath = soundPath;
            eFx.thermalRadius = blastRadius;

            eFx.flashModelPath = flashModel;
            eFx.shockModelPath = shockModel;
            eFx.blastModelPath = blastModel;
            eFx.plumeModelPath = plumeModel;
            eFx.debrisModelPath = debrisModel;
            eFx.blastSoundPath = blastSound;
            eFx.fireballEmitTime = fireballT;
            eFx.emitTime = emitT;
            eFx.fluenceTime = fluenceT;
            eFx.effectLifetime = effT;


            eFx.yield = Yield;
            eFx.fluence = thermalShock;
            eFx.isEMP = emp;
            eFx.detonationTimer = delay;
            newExplosion.SetActive(true);
            eFx.audioSource = newExplosion.GetComponent<AudioSource>();
            eFx.SoundPath = soundPath;
            newExplosion.SetActive(true);
        }
    }

    public abstract class NukeHitEvent
    {
        public float Distance { get; set; }
        public float TimeToImpact { get; set; }
        public bool IsNegativePressure { get; set; }
    }

    internal class PartNukeHitEvent : NukeHitEvent
    {
        public Part Part { get; set; }
        public Vector3 HitPoint { get; set; }
        public RaycastHit Hit { get; set; }
        public float NegativeForce { get; set; }
        public string SourceVesselName { get; set; }
    }

    internal class BuildingNukeHitEvent : NukeHitEvent
    {
        public DestructibleBuilding Building { get; set; }
    }
}
