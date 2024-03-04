using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Competition;
using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.GameModes
{
    public class AsteroidUtils
    {
        public static UntrackedObjectClass[] UntrackedObjectClasses = (UntrackedObjectClass[])Enum.GetValues(typeof(UntrackedObjectClass)); // Get the UntrackedObjectClasses as an array of enum values.
        static System.Random RNG = new System.Random();


        /// <summary>
        /// Spawn an asteroid of the given class at the given position.
        /// </summary>
        /// <param name="position">The position to spawn the asteroid.</param>
        /// <param name="untrackedObjectClassIndex">The class of the asteroid. -1 picks one at random.</param>
        /// <returns>The asteroid vessel.</returns>
        public static Vessel SpawnAsteroid(Vector3d position, int untrackedObjectClassIndex = -1)
        {
            if (untrackedObjectClassIndex < 0)
            {
                untrackedObjectClassIndex = RNG.Next(UntrackedObjectClasses.Length);
            }
            var asteroid = DiscoverableObjectsUtil.SpawnAsteroid(DiscoverableObjectsUtil.GenerateAsteroidName(), GetOrbitForApoapsis2(position), (uint)RNG.Next(), UntrackedObjectClasses[untrackedObjectClassIndex], double.MaxValue, double.MaxValue);
            if (asteroid != null && asteroid.vesselRef != null)
            { return asteroid.vesselRef; }
            else
            { return null; }
        }

        /// <summary>
        /// Calculate an orbit that has the specified position as the apoapsis and orbital velocity that matches that of the ground below.
        /// This doesn't quite give the correct orbits for some reason, use GetOrbitForApoapsis2 instead.
        /// </summary>
        /// <param name="position">The position of the apoapsis.</param>
        /// <returns>The orbit.</returns>
        public static Orbit GetOrbitForApoapsis(Vector3d position)
        {
            // FIXME this is still giving orbits that are slightly off, e.g., an asteroid field at the KSC is coming out oval instead of round.
            // Figure out the orbit of an asteroid with apoapsis at the spawn point and the same velocity as that of the surface under the spawn point.
            double latitude, longitude, altitude;
            FlightGlobals.currentMainBody.GetLatLonAlt(position, out latitude, out longitude, out altitude);
            longitude = (longitude + FlightGlobals.currentMainBody.rotationAngle + 180d) % 360d; // Compensate coordinates for planet rotation then normalise to 0°—360°.
            var inclination = Math.Abs(latitude);
            var apoapsisAltitude = FlightGlobals.currentMainBody.Radius + altitude;
            var velocity = 2d * Math.PI * (FlightGlobals.currentMainBody.Radius + altitude) * Math.Cos(Mathf.Deg2Rad * latitude) / FlightGlobals.currentMainBody.rotationPeriod;
            var semiMajorAxis = -FlightGlobals.currentMainBody.gravParameter / (velocity * velocity / 2d - FlightGlobals.currentMainBody.gravParameter / apoapsisAltitude) / 2d;
            var eccentricity = apoapsisAltitude / semiMajorAxis - 1d;
            var upDirection = (FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitude, longitude, altitude) - FlightGlobals.currentMainBody.transform.position).normalized;
            var longitudeOfAscendingNode = (Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(Vector3.Cross(upDirection, Vector3d.Cross(Vector3d.up, upDirection)).normalized, Vector3.forward)) + longitude + (latitude > 0 ? 0d : 180d)) % 360d;
            var argumentOfPeriapsis = latitude < 0d ? 90d : 270d;
            var meanAnomalyAtEpoch = Math.PI;
            return new Orbit(inclination, eccentricity, semiMajorAxis, longitudeOfAscendingNode, argumentOfPeriapsis, meanAnomalyAtEpoch, Planetarium.GetUniversalTime(), FlightGlobals.currentMainBody);
        }

        /// <summary>
        /// Calculate an orbit that has the specified position as the apoapsis and orbital velocity that matches that of the ground below.
        /// This one gives the correct orbit to within around float precision.
        /// </summary>
        /// <param name="position">The position of the apoapsis.</param>
        /// <returns>The orbit.</returns>
        public static Orbit GetOrbitForApoapsis2(Vector3d position)
        {
            double latitude, longitude, altitude;
            FlightGlobals.currentMainBody.GetLatLonAlt(position, out latitude, out longitude, out altitude);
            longitude = (longitude + FlightGlobals.currentMainBody.rotationAngle + 180d) % 360d; // Compensate coordinates for planet rotation then normalise to 0°—360°.
            var orbitVelocity = FlightGlobals.currentMainBody.getRFrmVel(position);
            var orbitPosition = position - FlightGlobals.currentMainBody.transform.position;
            var orbit = new Orbit();
            orbit.UpdateFromStateVectors(orbitPosition.xzy, orbitVelocity.xzy, FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
            return orbit;
        }

        /// <summary>
        /// Debugging: Compare orbit of current vessel with that of the generated ones.
        /// </summary>
        public static void CheckOrbit()
        {
            if (FlightGlobals.ActiveVessel == null) { return; }
            var v = FlightGlobals.ActiveVessel;
            var orbit = FlightGlobals.ActiveVessel.orbit;
            Debug.Log($"DEBUG orbit.pos: {orbit.pos}, orbit.vel: {orbit.vel}");
            Debug.Log($"DEBUG       pos: {(v.CoM - (Vector3d)v.mainBody.transform.position).xzy},       vel: {v.mainBody.getRFrmVel(v.CoM).xzy}");
            Debug.Log($"DEBUG Δpos: {orbit.pos - ((Vector3d)v.CoM - (Vector3d)v.mainBody.transform.position).xzy}, Δvel: {orbit.vel - v.mainBody.getRFrmVel(v.CoM).xzy}");
            Debug.Log($"DEBUG Current vessel's orbit: inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit = GetOrbitForApoapsis(v.CoM);
            Debug.Log($"DEBUG Predicted orbit:        inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit = GetOrbitForApoapsis2(v.CoM);
            Debug.Log($"DEBUG Predicted orbit 2:      inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
        }

        /// <summary>
        /// Strip out various modules from the asteroid as they make excessive amounts of GC allocations. 
        /// This seems only to be possible once the asteroid is active, loaded and unpacked.
        /// </summary>
        public static void CleanOutAsteroid(Vessel asteroid)
        {
            if (asteroid == null) return;
            var mod = asteroid.GetComponent<ModuleAsteroid>();
            if (mod != null)
            {
                UnityEngine.Object.Destroy(mod);
            }
            var modInfo = asteroid.GetComponent<ModuleAsteroidInfo>();
            if (modInfo != null)
            {
                UnityEngine.Object.Destroy(modInfo);
            }
            var modResource = asteroid.GetComponent<ModuleAsteroidResource>();
            if (modResource != null)
            {
                UnityEngine.Object.Destroy(modResource);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidRain : MonoBehaviour
    {
        #region Fields
        public static AsteroidRain Instance;

        bool raining = false;
        int numberOfAsteroids;
        float altitude;
        float radius;
        float initialSpeed = -100f;
        double spawnRate;
        Vector2d geoCoords;
        Vector3d spawnPoint;
        Vector3d upDirection;
        Vector3d refDirection;
        int cleaningInProgress;
        System.Random RNG;

        Coroutine rainCoroutine;
        Coroutine cleanUpCoroutine;
        HashSet<Vessel> beingRemoved = new HashSet<Vessel>();

        // Pooling of asteroids
        List<Vessel> asteroidPool;
        int lastPoolIndex = 0;
        HashSet<string> asteroidNames = new HashSet<string>();
        #endregion

        #region Monobehaviour functions
        /// <summary>
        /// Initialisation.
        /// </summary>
        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;

            if (RNG == null)
            {
                RNG = new System.Random();
            }
            GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        void OnDestroy()
        {
            Reset(true);
            GameEvents.onGameSceneSwitchRequested.Remove(HandleSceneChange);
        }
        #endregion

        #region Rain functions
        public static bool IsRaining()
        {
            if (Instance == null) return false;
            return Instance.raining;
        }

        /// <summary>
        /// Reset the asteroid rain, deactivating all the asteroids.
        /// </summary>
        public void Reset(bool destroyAsteroids = false)
        {
            raining = false;
            StopAllCoroutines();
            beingRemoved.Clear();
            if (asteroidPool != null)
            {
                foreach (var asteroid in asteroidPool)
                {
                    if (asteroid == null || asteroid.gameObject == null) continue;
                    if (asteroid.gameObject.activeInHierarchy) { asteroid.gameObject.SetActive(false); }
                    if (destroyAsteroids) { Destroy(asteroid); }
                }
                if (destroyAsteroids) { asteroidPool.Clear(); }
            }
            UpdatePooledAsteroidNames();
            cleaningInProgress = 0;
        }

        /// <summary>
        /// Handle scene changes.
        /// </summary>
        /// <param name="fromTo">The scenes changed from and to.</param>
        public void HandleSceneChange(GameEvents.FromToAction<GameScenes, GameScenes> fromTo)
        {
            if (fromTo.from == GameScenes.FLIGHT)
            {
                Reset();
                if (fromTo.to != GameScenes.FLIGHT)
                {
                    if (asteroidPool != null)
                    {
                        foreach (var asteroid in asteroidPool)
                        { if (asteroid != null) Destroy(asteroid); }
                        asteroidPool.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Update the asteroid rain settings.
        /// </summary>
        public void UpdateSettings(bool warning = false)
        {
            altitude = BDArmorySettings.ASTEROID_RAIN_ALTITUDE * 100f; // Convert to m.
            if (!(BDArmorySettings.ASTEROID_RAIN_FOLLOWS_CENTROID && BDArmorySettings.ASTEROID_RAIN_FOLLOWS_SPREAD)) radius = BDArmorySettings.ASTEROID_RAIN_RADIUS * 1000f; // Convert to m.
            numberOfAsteroids = BDArmorySettings.ASTEROID_RAIN_NUMBER;
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            if (spawnPoint.magnitude > 9e4f)
            {
                if (warning) { BDACompetitionMode.Instance.competitionStatus.Add($"Asteroid Rain spawning point is {spawnPoint.magnitude / 1000:F1}km away, which is more than 10 times the radius away. Spawning here instead."); }
                geoCoords = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(Vector3d.zero);
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            }
            upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            spawnPoint += (altitude - BodyUtils.GetRadarAltitudeAtPos(spawnPoint, false)) * upDirection; // Adjust for terrain height.
            refDirection = Math.Abs(Vector3d.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3d.up : Vector3d.forward; // Avoid that the reference direction is colinear with the local surface normal.

            var a = -(float)FlightGlobals.getGeeForceAtPosition(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude)).magnitude / 2f;
            var b = initialSpeed;
            var c = altitude;
            var timeToFall = (-b - Math.Sqrt(b * b - 4f * a * c)) / 2f / a;
            spawnRate = numberOfAsteroids / timeToFall * Time.fixedDeltaTime;
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.Asteroids]: SpawnRate: {spawnRate} asteroids / frame");
            if (raining) SetupAsteroidPool(Mathf.RoundToInt(numberOfAsteroids * 1.1f)); // Give ourselves a 10% buffer.
        }

        /// <summary>
        /// Spawn asteroid rain.
        /// </summary>
        public void SpawnRain(Vector3d geoCoords)
        {
            Reset();
            this.geoCoords = new Vector2d(geoCoords.x, geoCoords.y);
            if (BDArmorySettings.ASTEROID_RAIN_FOLLOWS_CENTROID && BDArmorySettings.ASTEROID_RAIN_FOLLOWS_SPREAD) this.radius = 1000f; // Initial radius for spawn in case we don't have any planes yet.
            UpdateSettings(true);
            StartCoroutine(StartRain());
        }

        /// <summary>
        /// Start raining asteroids.
        /// </summary>
        IEnumerator StartRain()
        {
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid rain with {numberOfAsteroids} asteroids, altitude {altitude / 1000f}km and radius {radius / 1000f}km at coordinates ({geoCoords.x:F4}, {geoCoords.y:F4}).");

            BDACompetitionMode.Instance.competitionStatus.Add($"Spawning Asteroid Rain with ~{numberOfAsteroids} asteroids from an altitude of {altitude}m, please be patient.");
            yield return new WaitForEndOfFrame(); // Wait for the message to display.
            yield return new WaitForFixedUpdate();
            SetupAsteroidPool(Mathf.RoundToInt(numberOfAsteroids * 1.1f)); // Give ourselves a 10% buffer.

            rainCoroutine = StartCoroutine(Rain());
            cleanUpCoroutine = StartCoroutine(CleanUp(0.5f));
        }

        /// <summary>
        /// Rain asteroids.
        /// </summary>
        IEnumerator Rain()
        {
            raining = true;
            var spawnRateTracker = 0d;
            var waitForFixedUpdate = new WaitForFixedUpdate();
            var relocationTimer = Time.time;
            var relocationTimeout = 2d;
            while (raining)
            {
                if (cleaningInProgress > 0) // Don't spawn anything if asteroids are getting added to the pool.
                {
                    yield return waitForFixedUpdate;
                    continue;
                }
                while (spawnRateTracker > 1d)
                {
                    var asteroid = GetAsteroid();
                    if (asteroid != null)
                    {
                        asteroid.Landed = false;
                        asteroid.Splashed = false;
                        var direction = (Quaternion.AngleAxis((float)RNG.NextDouble() * 360f, upDirection) * refDirection).ProjectOnPlanePreNormalized(upDirection).normalized;
                        var x = (float)RNG.NextDouble();
                        var distance = BDAMath.Sqrt(1f - x) * radius;
                        StartCoroutine(RepositionWhenReady(asteroid, direction * distance));
                    }
                    --spawnRateTracker;
                }
                yield return waitForFixedUpdate;
                if (Time.time - relocationTimer > relocationTimeout)
                {
                    UpdateRainLocation();
                    relocationTimer = Time.time;
                }
                spawnRateTracker += spawnRate;
            }
        }

        void UpdateRainLocation()
        {
            if (BDArmorySettings.ASTEROID_RAIN_FOLLOWS_CENTROID)
            {
                Vector3 averagePosition = spawnPoint;
                float maxSqrDistance = 0;
                int count = 1;
                foreach (var vessel in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null && wm.vessel != null).Select(wm => wm.vessel))
                {
                    averagePosition += vessel.transform.position;
                    ++count;
                }
                averagePosition /= (float)count;
                geoCoords = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(averagePosition);
                if (BDArmorySettings.ASTEROID_RAIN_FOLLOWS_SPREAD)
                {
                    foreach (var vessel in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null && wm.vessel != null).Select(wm => wm.vessel))
                    { maxSqrDistance = Mathf.Max(maxSqrDistance, (vessel.transform.position - averagePosition).sqrMagnitude); }
                    radius = maxSqrDistance > 5e5f ? BDAMath.Sqrt(maxSqrDistance) * 1.5f : 1000f;
                }
            }
            else
            {
                if (Vector3d.Dot(upDirection, (FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude) - FlightGlobals.currentMainBody.transform.position).normalized) < 0.99) // Planet rotation has moved the spawn point and direction significantly.
                {
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log("[BDArmory.Asteroids]: Planet has rotated significantly, updating settings.");
                }
            }
            UpdateSettings();
        }

        /// <summary>
        /// Reposition the asteroid to the desired position once it's properly spawned.
        /// </summary>
        /// <param name="asteroid">The asteroid.</param>
        /// <param name="offset">The offset from the central spawn point.</param>
        /// <returns></returns>
        IEnumerator RepositionWhenReady(Vessel asteroid, Vector3 offset)
        {
            var wait = new WaitForFixedUpdate();
            asteroid.gameObject.SetActive(true);
            while (asteroid != null && (asteroid.packed || !asteroid.loaded || asteroid.rootPart.Rigidbody == null)) yield return wait;
            if (asteroid != null)
            {
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
                var position = spawnPoint + offset;
                position += (altitude - BodyUtils.GetRadarAltitudeAtPos(position, false)) * upDirection;
                asteroid.transform.position = position;
                asteroid.SetWorldVelocity(initialSpeed * upDirection);
                // Apply a gaussian random torque to the asteroid.
                asteroid.rootPart.Rigidbody.angularVelocity = Vector3.zero;
                asteroid.rootPart.Rigidbody.AddTorque(VectorUtils.GaussianVector3d(Vector3d.zero, 300 * Vector3d.one), ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Clean-up routine.
        /// Checks every interval for asteroids that are going to impact soon and schedules them for removal.
        /// </summary>
        /// <param name="interval">The interval to check. Keep low for accuracy, but not too low for performance.</param>
        IEnumerator CleanUp(float interval)
        {
            var wait = new WaitForSeconds(interval); // Don't bother checking too often.
            while (raining)
            {
                foreach (var asteroid in asteroidPool)
                {
                    if (asteroid == null || !asteroid.gameObject.activeInHierarchy || asteroid.packed || !asteroid.loaded || asteroid.rootPart.Rigidbody == null) continue;
                    var timeToImpact = (float)((asteroid.radarAltitude - asteroid.GetRadius()) / asteroid.srfSpeed); // Simple estimate.
                    if (!beingRemoved.Contains(asteroid) && (timeToImpact < 1.5f * interval || asteroid.LandedOrSplashed))
                    {
                        StartCoroutine(RemoveAfterDelay(asteroid, timeToImpact - TimeWarp.fixedDeltaTime));
                    }
                }
                yield return wait;
            }
        }

        /// <summary>
        /// Remove the asteroid after the delay, generating an explosion at the point it disappears from.
        /// </summary>
        /// <param name="asteroid">The asteroid to remove.</param>
        /// <param name="delay">The delay to wait before removing the asteroid.</param>
        IEnumerator RemoveAfterDelay(Vessel asteroid, float delay)
        {
            beingRemoved.Add(asteroid);
            yield return new WaitForSeconds(delay);
            if (asteroid != null)
            {
                // Make an explosion where the impact is going to be and remove the asteroid before it actually impacts, so that KSP doesn't destroy it (regenerating the asteroid is expensive).
                var impactPosition = asteroid.transform.position + asteroid.srf_velocity * TimeWarp.fixedDeltaTime;
                FXMonger.ExplodeWithDebris(impactPosition, Math.Pow(asteroid.GetTotalMass(), 0.3d) / 12d, null);
                asteroid.transform.position += 10000f * upDirection; // Put the asteroid where it won't immediately die on re-activating, since we apparently can't reposition it immediately upon activation.
                asteroid.SetWorldVelocity(Vector3.zero); // Also, reset its velocity.
                asteroid.Landed = false;
                asteroid.Splashed = false;
                asteroid.gameObject.SetActive(false);
                beingRemoved.Remove(asteroid);
            }
            else
            { if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.Asteroids]: Asteroid {asteroid.vesselName} is null, unable to remove."); }
        }
        #endregion

        #region Pooling
        /// <summary>
        /// Wait until the collider bounds have been generated, then remove various modules from the asteroid for performance reasons.
        /// </summary>
        /// <param name="asteroid">The asteroid to clean.</param>
        IEnumerator CleanAsteroid(Vessel asteroid)
        {
            ++cleaningInProgress;
            var wait = new WaitForFixedUpdate();
            asteroid.gameObject.SetActive(true);
            var startTime = Time.time;
            while (asteroid != null && Time.time - startTime < 10 && (asteroid.packed || !asteroid.loaded || asteroid.rootPart.GetColliderBounds().Length < 2)) yield return wait;
            if (asteroid != null)
            {
                if (Time.time - startTime >= 10) Debug.LogWarning($"[BDArmory.Asteroids]: Timed out waiting for colliders on {asteroid.vesselName} to be generated.");
                AsteroidUtils.CleanOutAsteroid(asteroid);
                asteroid.gameObject.SetActive(false);
            }
            --cleaningInProgress;
        }

        /// <summary>
        /// Set up the asteroid pool to contain at least count asteroids.
        /// </summary>
        /// <param name="count">The minimum number of asteroids in the pool.</param>
        void SetupAsteroidPool(int count)
        {
            if (asteroidPool == null) { asteroidPool = new List<Vessel>(); }
            else { asteroidPool = asteroidPool.Where(a => a != null && a.transform.position.magnitude < 9e4f).ToList(); }
            foreach (var asteroid in asteroidPool)
            {
                if (asteroid.FindPartModuleImplementing<ModuleAsteroid>() != null || asteroid.FindPartModuleImplementing<ModuleAsteroidInfo>() != null || asteroid.FindPartModuleImplementing<ModuleAsteroidResource>() != null) // We don't use the VesselModuleRegistry here as we'd need to force update it for each asteroid anyway.
                { StartCoroutine(CleanAsteroid(asteroid)); }
            }
            if (count > asteroidPool.Count) { AddAsteroidsToPool(count - asteroidPool.Count); }
        }

        /// <summary>
        /// Replace an asteroid at position i in the pool.
        /// </summary>
        /// <param name="i"></param>
        void ReplacePooledAsteroid(int i)
        {
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.Asteroids]: Replacing asteroid at position {i}.");
            var asteroid = AsteroidUtils.SpawnAsteroid(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude + 10000));
            if (asteroid != null)
            {
                StartCoroutine(CleanAsteroid(asteroid));
                asteroidPool[i] = asteroid;
            }
        }

        /// <summary>
        /// Add a number of asteroids to the pool.
        /// </summary>
        /// <param name="count"></param>
        void AddAsteroidsToPool(int count)
        {
            Debug.Log($"[BDArmory.Asteroids]: Increasing asteroid pool size to {asteroidPool.Count + count}.");
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            var refDirection = Math.Abs(Vector3d.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3d.up : Vector3d.forward; // Avoid that the reference direction is colinear with the local surface normal.
            for (int i = 0; i < count; ++i)
            {
                var direction = (Quaternion.AngleAxis(i / 60f * 360f, upDirection) * refDirection).ProjectOnPlanePreNormalized(upDirection).normalized; // 60 asteroids per layer of the spiral (approx. 100m apart).
                var position = spawnPoint + (1e4f + 1e2f * i / 60) * upDirection + 1e3f * direction; // 100m altitude difference per layer of the spiral.
                var asteroid = AsteroidUtils.SpawnAsteroid(position);
                if (asteroid != null)
                {
                    StartCoroutine(CleanAsteroid(asteroid));
                    asteroidPool.Add(asteroid);
                }
            }
            UpdatePooledAsteroidNames();
        }

        /// <summary>
        /// Get an asteroid from the pool.
        /// </summary>
        /// <returns>An asteroid vessel.</returns>
        Vessel GetAsteroid()
        {
            // Start at the last index returned and cycle round for efficiency. This makes this a typically O(1) seek operation.
            for (int i = lastPoolIndex + 1; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null)
                {
                    ReplaceNullPooledAsteroids();
                }
                if (!asteroidPool[i].gameObject.activeInHierarchy)
                {
                    lastPoolIndex = i;
                    return asteroidPool[i];
                }
            }
            for (int i = 0; i < lastPoolIndex + 1; ++i)
            {
                if (asteroidPool[i] == null)
                {
                    ReplaceNullPooledAsteroids();
                }
                if (!asteroidPool[i].gameObject.activeInHierarchy)
                {
                    lastPoolIndex = i;
                    return asteroidPool[i];
                }
            }

            var size = (int)(asteroidPool.Count * 1.1) + 1; // Grow by 10% + 1
            AddAsteroidsToPool(size - asteroidPool.Count);

            return asteroidPool[asteroidPool.Count - 1]; // Return the last entry in the pool
        }

        /// <summary>
        /// Scan for and replace null asteroids in one go to reduce delays due to the CollisionManager.
        /// </summary>
        void ReplaceNullPooledAsteroids()
        {
            BDACompetitionMode.Instance.competitionStatus.Add("Replacing lost asteroids.");
            for (int i = 0; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null)
                { ReplacePooledAsteroid(i); }
            }
            UpdatePooledAsteroidNames();
        }

        /// <summary>
        /// Update the asteroid names hashset so we can know whether it's managed or not.
        /// </summary>
        void UpdatePooledAsteroidNames()
        {
            if (asteroidPool == null) asteroidNames.Clear();
            else asteroidNames = asteroidPool.Select(a => a.vesselName).ToHashSet();
        }

        /// <summary>
        /// Is the vessel a managed asteroid.
        /// </summary>
        /// <param name="vessel"></param>
        public static bool IsManagedAsteroid(Vessel vessel)
        {
            if (Instance == null || Instance.asteroidNames == null) return false;
            return Instance.asteroidNames.Contains(vessel.vesselName);
        }

        /// <summary>
        /// Run some debugging checks on the pooled asteroids.
        /// </summary>
        public void CheckPooledAsteroids()
        {
            if (asteroidPool == null) { Debug.Log("DEBUG Asteroid pool is not set up yet."); return; }
            int activeCount = 0;
            int withModulesCount = 0;
            int withCollidersCount = 0;
            double minMass = double.MaxValue;
            double maxMass = 0d;
            double minRadius = double.MaxValue;
            double maxRadius = 0d;
            for (int i = 0; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null) { Debug.Log($"DEBUG asteroid at position {i} is null"); continue; }
                Debug.Log($"{asteroidPool[i].vesselName} has mass {asteroidPool[i].GetTotalMass()}");
                if (asteroidPool[i].gameObject != null)
                {
                    if (asteroidPool[i].gameObject.activeInHierarchy)
                        ++activeCount;
                    maxMass = Math.Max(maxMass, asteroidPool[i].GetTotalMass());
                    minMass = Math.Min(minMass, asteroidPool[i].GetTotalMass());
                    maxRadius = Math.Max(maxRadius, asteroidPool[i].GetRadius());
                    minRadius = Math.Min(minRadius, asteroidPool[i].GetRadius());
                    if (asteroidPool[i].FindPartModuleImplementing<ModuleAsteroid>() != null || asteroidPool[i].FindPartModuleImplementing<ModuleAsteroidInfo>() != null || asteroidPool[i].FindPartModuleImplementing<ModuleAsteroidResource>() != null) ++withModulesCount;
                    if (asteroidPool[i].rootPart != null && asteroidPool[i].rootPart.GetColliderBounds().Length > 1) ++withCollidersCount;
                }
            }
            Debug.Log($"DEBUG {activeCount} asteroids active of {asteroidPool.Count}, mass range: {minMass}t — {maxMass}t, radius range: {minRadius}—{maxRadius}, #withModules: {withModulesCount}, #withCollidersCount: {withCollidersCount}, cleaning in progress: {cleaningInProgress}");
        }
        #endregion
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidField : MonoBehaviour
    {
        #region Fields
        public static AsteroidField Instance;
        Vessel[] asteroids; // We use both an array of asteroids that are currently in use and a pool of asteroids for quick re-use between rounds.

        float altitude;
        float radius;
        Vector2d geoCoords;
        Vector3d spawnPoint;
        Vector3d upDirection;
        Vector3d refDirection;
        int cleaningInProgress;
        bool floating;
        Coroutine floatingCoroutine;
        public Vector3d anomalousAttraction = Vector3d.zero;
        System.Random RNG;

        // Pooling of asteroids
        List<Vessel> asteroidPool = new List<Vessel>();
        int lastPoolIndex = 0;
        HashSet<string> asteroidNames = new HashSet<string>();
        readonly Dictionary<string, float> attractionFactors = new Dictionary<string, float>();
        #endregion

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;

            if (RNG == null)
            {
                RNG = new System.Random();
            }
        }

        void OnDestroy()
        {
            Reset(true);
        }

        /// <summary>
        /// Reset the asteroid field, deactivating all the asteroids.
        /// </summary>
        public void Reset(bool destroyAsteroids = false)
        {
            floating = false;
            StopAllCoroutines();

            asteroids = null; // Clear the current array of asteroids.
            if (asteroidPool != null)
            {
                foreach (var asteroid in asteroidPool)
                {
                    if (asteroid == null || asteroid.gameObject == null) continue;
                    if (asteroid.gameObject.activeInHierarchy) { asteroid.gameObject.SetActive(false); }
                    if (destroyAsteroids) { Destroy(asteroid); }
                }
                if (destroyAsteroids) { asteroidPool.Clear(); }
            }
            UpdatePooledAsteroidNames();
            cleaningInProgress = 0;
        }

        /// <summary>
        /// Spawn an asteroid field.
        /// </summary>
        /// <param name="_numberOfAsteroids">The number of asteroids in the field.</param>
        /// <param name="_altitude">The maximum altitude AGL of the field, minimum altitude AGL is 50m.</param>
        /// <param name="_radius">The radius of the field from the spawn point.</param>
        /// <param name="_geoCoords">The spawn point (centre) of the field.</param>
        public void SpawnField(int numberOfAsteroids, float altitude, float radius, Vector3d geoCoords)
        {
            Reset();

            altitude *= 100f; // Convert to m.
            radius *= 1000f; // Convert to m.
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid field with {numberOfAsteroids} asteroids with height {altitude}m and radius {radius / 1000f}km at coordinate ({geoCoords.x:F4}, {geoCoords.y:F4}).");
            BDACompetitionMode.Instance.competitionStatus.Add($"Spawning Asteroid Field with {numberOfAsteroids} asteroids with height {altitude}m and radius {radius / 1000f}km at coordinate ({geoCoords.x:F4}, {geoCoords.y:F4}), please be patient.");

            this.altitude = altitude;
            this.radius = radius;
            this.geoCoords = new Vector2d(geoCoords.x, geoCoords.y);
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            refDirection = Math.Abs(Vector3.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            StartCoroutine(SpawnField(numberOfAsteroids));
        }

        IEnumerator SpawnField(int numberOfAsteroids)
        {
            var wait = new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame(); // Give the message a chance to show.
            yield return wait;
            SetupAsteroidPool(numberOfAsteroids);
            while (cleaningInProgress > 0) // Wait until the asteroid pool is finished being set up.
            { yield return wait; }
            asteroids = new Vessel[numberOfAsteroids];
            for (int i = 0; i < asteroids.Length; ++i)
            {
                var direction = (Quaternion.AngleAxis((float)RNG.NextDouble() * 360f, upDirection) * refDirection).ProjectOnPlanePreNormalized(upDirection).normalized;
                var x = (float)RNG.NextDouble();
                var distance = BDAMath.Sqrt(1f - x) * radius;
                var height = RNG.NextDouble() * (altitude - 50f) + 50f;
                var position = spawnPoint + direction * distance;
                position += (height - BodyUtils.GetRadarAltitudeAtPos(position)) * upDirection;
                var asteroid = GetAsteroid();
                if (asteroid != null)
                {
                    asteroid.gameObject.SetActive(true);
                    asteroid.SetPosition(position);
                    asteroid.SetWorldVelocity(Vector3d.zero);
                    StartCoroutine(SetInitialRotation(asteroid));
                    asteroids[i] = asteroid;
                }
            }
            floatingCoroutine = StartCoroutine(Float());
        }

        /// <summary>
        /// Apply forces to counteract gravity, decay overall motion and add Brownian noise.
        /// </summary>
        IEnumerator Float()
        {
            var wait = new WaitForFixedUpdate();
            floating = true;
            Vector3d offset;
            float factor = 0;
            float repulseTimer = Time.time;
            while (floating)
            {
                for (int i = 0; i < asteroids.Length; ++i)
                {
                    if (asteroids[i] == null || asteroids[i].packed || !asteroids[i].loaded || asteroids[i].rootPart.Rigidbody == null) continue;
                    var nudge = new Vector3d(RNG.NextDouble() - 0.5, RNG.NextDouble() - 0.5, RNG.NextDouble() - 0.5) * 100;
                    if (BDArmorySettings.ASTEROID_FIELD_ANOMALOUS_ATTRACTION)
                    {
                        anomalousAttraction = Vector3d.zero;
                        foreach (var weaponManager in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value))
                        {
                            if (weaponManager == null) continue;
                            offset = weaponManager.vessel.transform.position - asteroids[i].transform.position;
                            factor = (1f - (float)offset.sqrMagnitude / 1e6f); // 1-(r/1000)^2 attraction. I.e., asteroids within 1km.
                            if (factor > 0) anomalousAttraction += factor * attractionFactors[asteroids[i].vesselName] * offset.normalized;
                        }
                        anomalousAttraction *= BDArmorySettings.ASTEROID_FIELD_ANOMALOUS_ATTRACTION_STRENGTH;
                    }
                    asteroids[i].rootPart.Rigidbody.AddForce((-FlightGlobals.getGeeForceAtPosition(asteroids[i].transform.position) - asteroids[i].srf_velocity / 10f + nudge + anomalousAttraction) * TimeWarp.CurrentRate, ForceMode.Acceleration); // Float and reduce motion.
                }
                if (Time.time - repulseTimer > 1) // Once per second repulse nearby asteroids from each other to avoid them sticking. Not too often since it's O(N^2). This might be more performant using an OverlapSphere.
                {
                    for (int i = 0; i < asteroids.Length - 1; ++i)
                    {
                        if (asteroids[i] == null || asteroids[i].packed || !asteroids[i].loaded || asteroids[i].rootPart.Rigidbody == null) continue;
                        for (int j = i + 1; j < asteroids.Length; ++j)
                        {
                            if (asteroids[j] == null || asteroids[j].packed || !asteroids[j].loaded || asteroids[j].rootPart.Rigidbody == null) continue;
                            var separation = asteroids[i].transform.position - asteroids[j].transform.position;
                            var sepSqr = separation.sqrMagnitude;
                            var proximityFactor = asteroids[i].GetRadius() + asteroids[j].GetRadius(); proximityFactor *= 100 * proximityFactor;
                            if (sepSqr < proximityFactor)
                            {
                                var repulseAmount = TimeWarp.CurrentRate * BDAMath.Sqrt(proximityFactor - sepSqr) * separation.normalized;
                                asteroids[i].rootPart.Rigidbody.AddForce(repulseAmount, ForceMode.Acceleration);
                                asteroids[j].rootPart.Rigidbody.AddForce(-repulseAmount, ForceMode.Acceleration);
                            }
                        }
                    }
                    repulseTimer = Time.time;
                }
                yield return wait;
            }
        }

        /// <summary>
        /// Set the initial rotation of the asteroid.
        /// </summary>
        /// <param name="asteroid"></param>
        IEnumerator SetInitialRotation(Vessel asteroid)
        {
            var wait = new WaitForFixedUpdate();
            while (asteroid != null && asteroid.gameObject.activeInHierarchy && (asteroid.packed || !asteroid.loaded || asteroid.rootPart.Rigidbody == null)) yield return wait;
            if (asteroid != null && asteroid.gameObject.activeInHierarchy)
            {
                asteroid.rootPart.Rigidbody.angularVelocity = Vector3.zero;
                asteroid.rootPart.Rigidbody.AddTorque(VectorUtils.GaussianVector3d(Vector3d.zero, 50 * Vector3d.one), ForceMode.Acceleration); // Apply a gaussian random torque to each asteroid.
            }
        }

        #region Pooling
        /// <summary>
        /// Wait until the collider bounds have been generated, then remove various modules from the asteroid for performance reasons.
        /// </summary>
        /// <param name="asteroid">The asteroid to clean.</param>
        IEnumerator CleanAsteroid(Vessel asteroid)
        {
            ++cleaningInProgress;
            var wait = new WaitForFixedUpdate();
            asteroid.gameObject.SetActive(true);
            var startTime = Time.time;
            while (asteroid != null && Time.time - startTime < 10 && (asteroid.packed || !asteroid.loaded || asteroid.rootPart.GetColliderBounds().Length < 2)) yield return wait;
            if (asteroid != null)
            {
                if (Time.time - startTime >= 10) Debug.LogWarning($"[BDArmory.Asteroids]: Timed out waiting for colliders on {asteroid.vesselName} to be generated.");
                AsteroidUtils.CleanOutAsteroid(asteroid);
                asteroid.rootPart.crashTolerance = float.MaxValue; // Make the asteroids nigh indestructible.
                asteroid.rootPart.maxTemp = float.MaxValue;
                asteroid.gameObject.SetActive(false);
            }
            --cleaningInProgress;
        }

        /// <summary>
        /// Set up the asteroid pool to contain at least count asteroids.
        /// </summary>
        /// <param name="count">The minimum number of asteroids in the pool.</param>
        void SetupAsteroidPool(int count)
        {
            asteroidPool = asteroidPool.Where(a => a != null && a.transform.position.magnitude < 9e4f).ToList();
            foreach (var asteroid in asteroidPool)
            {
                if (asteroid.FindPartModuleImplementing<ModuleAsteroid>() != null || asteroid.FindPartModuleImplementing<ModuleAsteroidInfo>() != null || asteroid.FindPartModuleImplementing<ModuleAsteroidResource>() != null) // We don't use the VesselModuleRegistry here as we'd need to force update it for each asteroid anyway.
                { StartCoroutine(CleanAsteroid(asteroid)); }
            }
            if (count > asteroidPool.Count) { AddAsteroidsToPool(count - asteroidPool.Count); }
        }

        /// <summary>
        /// Replace an asteroid at position i in the pool.
        /// </summary>
        /// <param name="i"></param>
        void ReplacePooledAsteroid(int i)
        {
            if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.Asteroids]: Replacing asteroid at position {i}.");
            var asteroid = AsteroidUtils.SpawnAsteroid(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude + 10000));
            if (asteroid != null)
            {
                StartCoroutine(CleanAsteroid(asteroid));
                asteroidPool[i] = asteroid;
            }
        }

        /// <summary>
        /// Add a number of asteroids to the pool.
        /// </summary>
        /// <param name="count"></param>
        void AddAsteroidsToPool(int count)
        {
            Debug.Log($"[BDArmory.Asteroids]: Increasing asteroid pool size to {asteroidPool.Count + count}.");
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            var refDirection = Math.Abs(Vector3d.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3d.up : Vector3d.forward; // Avoid that the reference direction is colinear with the local surface normal.
            for (int i = 0; i < count; ++i)
            {
                var direction = (Quaternion.AngleAxis(i / 60f * 360f, upDirection) * refDirection).ProjectOnPlanePreNormalized(upDirection).normalized; // 60 asteroids per layer of the spiral (approx. 100m apart).
                var position = spawnPoint + (1e4f + 1e2f * i / 60) * upDirection + 1e3f * direction; // 100m altitude difference per layer of the spiral.
                var asteroid = AsteroidUtils.SpawnAsteroid(position);
                if (asteroid != null)
                {
                    StartCoroutine(CleanAsteroid(asteroid));
                    asteroidPool.Add(asteroid);
                }
            }
            UpdatePooledAsteroidNames();
        }

        /// <summary>
        /// Get an asteroid from the pool.
        /// </summary>
        /// <returns>An asteroid vessel.</returns>
        Vessel GetAsteroid()
        {
            // Start at the last index returned and cycle round for efficiency. This makes this a typically O(1) seek operation.
            for (int i = lastPoolIndex + 1; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null)
                {
                    ReplaceNullPooledAsteroids();
                }
                if (!asteroidPool[i].gameObject.activeInHierarchy)
                {
                    lastPoolIndex = i;
                    return asteroidPool[i];
                }
            }
            for (int i = 0; i < lastPoolIndex + 1; ++i)
            {
                if (asteroidPool[i] == null)
                {
                    ReplaceNullPooledAsteroids();
                }
                if (!asteroidPool[i].gameObject.activeInHierarchy)
                {
                    lastPoolIndex = i;
                    return asteroidPool[i];
                }
            }

            var size = (int)(asteroidPool.Count * 1.1) + 1; // Grow by 10% + 1
            AddAsteroidsToPool(size - asteroidPool.Count);

            return asteroidPool[asteroidPool.Count - 1]; // Return the last entry in the pool
        }

        /// <summary>
        /// Scan for and replace null asteroids in one go to reduce delays due to the CollisionManager.
        /// </summary>
        void ReplaceNullPooledAsteroids()
        {
            BDACompetitionMode.Instance.competitionStatus.Add("Replacing lost asteroids.");
            for (int i = 0; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null)
                { ReplacePooledAsteroid(i); }
            }
            UpdatePooledAsteroidNames();
        }

        /// <summary>
        /// Update the asteroid names hashset so we can know whether it's managed or not.
        /// </summary>
        void UpdatePooledAsteroidNames()
        {
            asteroidNames = asteroidPool.Select(a => a.vesselName).ToHashSet();
            UpdateAttractionFactors();
        }

        /// <summary>
        /// Is the vessel a managed asteroid.
        /// </summary>
        /// <param name="vessel"></param>
        public static bool IsManagedAsteroid(Vessel vessel)
        {
            if (Instance == null || Instance.asteroidNames == null) return false;
            return Instance.asteroidNames.Contains(vessel.vesselName);
        }

        /// <summary>
        /// Update the attraction factors for the asteroids.
        /// </summary>
        void UpdateAttractionFactors()
        {
            attractionFactors.Clear();
            foreach (var asteroid in asteroidPool) attractionFactors[asteroid.vesselName] = 50f * Mathf.Clamp(4f / Mathf.Log(asteroid.GetRadius() + 3f) - 1f, 0.1f, 2f);
        }

        /// <summary>
        /// Run some debugging checks on the pooled asteroids.
        /// </summary>
        public void CheckPooledAsteroids()
        {
            int activeCount = 0;
            int withModulesCount = 0;
            int withCollidersCount = 0;
            double minMass = double.MaxValue;
            double maxMass = 0d;
            double minRadius = double.MaxValue;
            double maxRadius = 0d;
            for (int i = 0; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null) { Debug.Log($"DEBUG asteroid at position {i} is null"); continue; }
                Debug.Log($"{asteroidPool[i].vesselName} has mass {asteroidPool[i].GetTotalMass()}");
                if (asteroidPool[i].gameObject != null)
                {
                    if (asteroidPool[i].gameObject.activeInHierarchy)
                        ++activeCount;
                    maxMass = Math.Max(maxMass, asteroidPool[i].GetTotalMass());
                    minMass = Math.Min(minMass, asteroidPool[i].GetTotalMass());
                    maxRadius = Math.Max(maxRadius, asteroidPool[i].GetRadius());
                    minRadius = Math.Min(minRadius, asteroidPool[i].GetRadius());
                    if (asteroidPool[i].FindPartModuleImplementing<ModuleAsteroid>() != null || asteroidPool[i].FindPartModuleImplementing<ModuleAsteroidInfo>() != null || asteroidPool[i].FindPartModuleImplementing<ModuleAsteroidResource>() != null) ++withModulesCount;
                    if (asteroidPool[i].rootPart != null && asteroidPool[i].rootPart.GetColliderBounds().Length > 1) ++withCollidersCount;
                }
            }
            Debug.Log($"DEBUG {activeCount} asteroids active of {asteroidPool.Count}, mass range: {minMass}t — {maxMass}t, radius range: {minRadius}—{maxRadius}, #withModules: {withModulesCount}, #withCollidersCount: {withCollidersCount}, cleaning in progress: {cleaningInProgress}");
        }
        #endregion
    }
}