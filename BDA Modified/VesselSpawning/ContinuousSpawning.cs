using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.UI;

namespace BDArmory.VesselSpawning
{
    /// <summary>
    /// Continous spawning in an airborne ring cycling through all the vessels in the spawn folder.
    ///
    /// TODO: This should probably be subsumed into its own spawn strategy eventually.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ContinuousSpawning : VesselSpawnerBase
    {
        public static ContinuousSpawning Instance;

        public bool vesselsSpawningContinuously = false;
        int continuousSpawnedVesselCount = 0;
        int currentlySpawningCount = 0;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        void LogMessage(string message, bool toScreen = true, bool toLog = true) => LogMessageFrom("ContinuousSpawning", message, toScreen, toLog);

        public override IEnumerator Spawn(SpawnConfig spawnConfig)
        {
            var circularSpawnConfig = spawnConfig as CircularSpawnConfig;
            if (circularSpawnConfig == null) yield break;
            yield return SpawnVesselsContinuouslyAsCoroutine(circularSpawnConfig);
        }

        public void CancelSpawning()
        {
            // Continuous spawn
            if (spawnVesselsContinuouslyCoroutine != null)
            {
                StopCoroutine(spawnVesselsContinuouslyCoroutine);
                spawnVesselsContinuouslyCoroutine = null;
            }
            if (vesselsSpawningContinuously)
            {
                vesselsSpawningContinuously = false;
                if (continuousSpawningScores != null)
                    DumpContinuousSpawningScores();
                continuousSpawningScores = null;
                LogMessage("Continuous vessel spawning cancelled.");
                if (BDACompetitionMode.Instance != null) BDACompetitionMode.Instance.ResetCompetitionStuff();
            }
            currentlySpawningCount = 0;
            SpawnUtils.RevertSpawnLocationCamera(true);
        }

        public override void PreSpawnInitialisation(SpawnConfig spawnConfig)
        {
            base.PreSpawnInitialisation(spawnConfig);

            vesselsSpawningContinuously = true;
            vesselsSpawning = true;
            spawnFailureReason = SpawnFailureReason.None; // Reset the spawn failure reason.
            continuousSpawningScores = new Dictionary<string, ContinuousSpawningScores>();
            if (spawnVesselsContinuouslyCoroutine != null)
                StopCoroutine(spawnVesselsContinuouslyCoroutine);
            // Reset competition stuff.
            BDACompetitionMode.Instance.LogResults("due to continuous spawning", "auto-dump-from-spawning"); // Log results first.
            BDACompetitionMode.Instance.StopCompetition();
            BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset competition scores.
            SpawnUtilsInstance.Instance.gunGameProgress.Clear(); // Clear gun-game progress.
        }

        public void SpawnVesselsContinuously(CircularSpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            LogMessage($"[BDArmory.VesselSpawner]: Triggering continuous vessel spawning at {spawnConfig.latitude:G6}, {spawnConfig.longitude:G6} on {FlightGlobals.Bodies[spawnConfig.worldIndex].name}, with altitude {spawnConfig.altitude:0}m.", false);
            spawnVesselsContinuouslyCoroutine = StartCoroutine(SpawnVesselsContinuouslyCoroutine(spawnConfig));
        }

        /// <summary>
        /// A coroutine version of the SpawnAllVesselsContinuously function that performs the required prespawn initialisation.
        /// </summary>
        /// <param name="spawnConfig">The spawn config to use.</param>
        public IEnumerator SpawnVesselsContinuouslyAsCoroutine(CircularSpawnConfig spawnConfig)
        {
            PreSpawnInitialisation(spawnConfig);
            LogMessage("[BDArmory.VesselSpawner]: Triggering continuous vessel spawning at " + spawnConfig.latitude.ToString("G6") + ", " + spawnConfig.longitude.ToString("G6") + ", with altitude " + spawnConfig.altitude + "m.", false);
            yield return SpawnVesselsContinuouslyCoroutine(spawnConfig);
        }

        private Coroutine spawnVesselsContinuouslyCoroutine;
        // Spawns all vessels in a downward facing ring and activates them (autopilot and AG10, then stage if no engines are firing), then respawns any that die. An altitude of 1000m should be plenty.
        // Note: initial vessel separation tends towards 2*pi*spawnDistanceFactor from above for >3 vessels.
        private IEnumerator SpawnVesselsContinuouslyCoroutine(CircularSpawnConfig spawnConfig)
        {
            #region Initialisation and sanity checks
            // Tally up the craft to spawn.
            if (spawnConfig.craftFiles == null) // Prioritise the list of craftFiles if we're given them.
                spawnConfig.craftFiles = Directory.GetFiles(Path.Combine(AutoSpawnPath, spawnConfig.folder), "*.craft").ToList();
            if (spawnConfig.craftFiles.Count == 0)
            {
                LogMessage("Vessel spawning: found no craft files in " + Path.Combine(AutoSpawnPath, spawnConfig.folder));
                vesselsSpawningContinuously = false;
                spawnFailureReason = SpawnFailureReason.NoCraft;
                yield break;
            }
            spawnConfig.craftFiles.Shuffle(); // Randomise the spawn order.
            spawnConfig.altitude = Math.Max(100, spawnConfig.altitude); // Don't spawn too low.
            var spawnBody = FlightGlobals.Bodies[spawnConfig.worldIndex];
            var spawnInOrbit = spawnConfig.altitude >= spawnBody.MinSafeAltitude(); // Min safe orbital altitude
            var spawnDistance = spawnConfig.craftFiles.Count > 1 ? (spawnConfig.absDistanceOrFactor ? spawnConfig.distance : spawnConfig.distance * (1 + (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count))) : 0f; // If it's a single craft, spawn it at the spawn point.
            if (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS == 0)
                LogMessage("Spawning " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + (spawnConfig.craftFiles.Count > 8 ? "m, this may take some time..." : "m."));
            else
                LogMessage("Spawning " + Math.Min(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS, spawnConfig.craftFiles.Count) + " of " + spawnConfig.craftFiles.Count + " vessels at an altitude of " + spawnConfig.altitude.ToString("G0") + "m with rolling-spawning.");
            #endregion

            yield return AcquireSpawnPoint(spawnConfig, 2f * spawnDistance, true);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                vesselsSpawningContinuously = false;
                yield break;
            }

            #region Spawning
            ResetInternals();
            continuousSpawnedVesselCount = 0; // Reset our spawned vessel count.
            var craftURLToVesselName = new Dictionary<string, string>();
            Vector3 craftSpawnPosition;
            var spawnSlots = OptimiseSpawnSlots(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count);
            var spawnCounts = spawnConfig.craftFiles.ToDictionary(c => c, c => 0);
            var spawnQueue = new Queue<string>();
            var craftToSpawn = new Queue<string>();
            double currentUpdateTick;
            while (vesselsSpawningContinuously)
            {
                // Wait for any pending vessel removals.
                while (SpawnUtils.removingVessels)
                { yield return waitForFixedUpdate; }

                currentUpdateTick = BDACompetitionMode.Instance.nextUpdateTick;
                if (currentlySpawningCount == 0) // Do nothing while we're spawning vessels.
                {
                    // Check if sliders have changed.
                    if (spawnSlots.Count != (BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count))
                    {
                        spawnSlots = OptimiseSpawnSlots(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? Math.Min(spawnConfig.craftFiles.Count, BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS) : spawnConfig.craftFiles.Count);
                        continuousSpawnedVesselCount %= spawnSlots.Count;
                    }
                    // Add any craft that hasn't been spawned or has died to the spawn queue if it isn't already in the queue.
                    foreach (var craftURL in spawnConfig.craftFiles.Where(craftURL => (BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL == 0 || spawnCounts[craftURL] < BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL) && !spawnQueue.Contains(craftURL) && (!craftURLToVesselName.ContainsKey(craftURL) || (BDACompetitionMode.Instance.Scores.Players.Contains(craftURLToVesselName[craftURL]) && BDACompetitionMode.Instance.Scores.ScoreData[craftURLToVesselName[craftURL]].deathTime >= 0))))
                    {
                        if (BDArmorySettings.DEBUG_SPAWNING)
                        {
                            LogMessage($"Adding {craftURL}" + (craftURLToVesselName.ContainsKey(craftURL) ? $" ({craftURLToVesselName[craftURL]})" : "") + " to the spawn queue.", false);
                        }
                        spawnQueue.Enqueue(craftURL);
                        ++spawnCounts[craftURL];
                    }
                    LoadedVesselSwitcher.Instance.UpdateList();
                    var currentlyActive = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList().Count;
                    if (spawnQueue.Count + currentlySpawningCount == 0 && currentlyActive < 2)// Nothing left to spawn or being spawned and only 1 vessel surviving. Time to call it quits and let the competition end.
                    {
                        LogMessage("Spawn queue is empty and not enough vessels are active, ending competition.", false);
                        BDACompetitionMode.Instance.StopCompetition();
                        if ((BDArmorySettings.AUTO_RESUME_TOURNAMENT || BDArmorySettings.AUTO_RESUME_CONTINUOUS_SPAWN) && BDArmorySettings.AUTO_QUIT_AT_END_OF_TOURNAMENT && TournamentAutoResume.Instance != null)
                        {
                            TournamentAutoResume.AutoQuit(5);
                            var message = "Quitting KSP in 5s due to reaching the end of a tournament.";
                            BDACompetitionMode.Instance.competitionStatus.Add(message);
                            Debug.LogWarning("[BDArmory.BDATournament]: " + message);
                        }
                        break;
                    }
                    {// Perform a "bubble shuffle" (randomly swap pairs of craft moving through the queue).
                        List<string> shufflePool = new List<string>();
                        List<string> shuffleSelection = new List<string>();
                        Queue<string> bubbleShuffleQueue = new();
                        while (spawnQueue.Count > 0)
                        {
                            shufflePool.Add(spawnQueue.Dequeue()); // Take craft from the spawn queue.
                            if (shufflePool.Count > 1) // Use a pool of size 2 for shuffling.
                            {
                                shufflePool.Shuffle();
                                // Prioritise craft that have had fewer spawns/deaths.
                                int fewestSpawns = shufflePool.Min(craftUrl => spawnCounts[craftUrl]);
                                shuffleSelection = shufflePool.Where(craftUrl => spawnCounts[craftUrl] == fewestSpawns).ToList();
                                string selected = shuffleSelection.First();
                                bubbleShuffleQueue.Enqueue(selected);
                                shufflePool.Remove(selected);
                            }
                        }
                        foreach (var craft in shufflePool) bubbleShuffleQueue.Enqueue(craft); // Add any remaining craft in the shuffle pool.
                        while (bubbleShuffleQueue.Count > 0) spawnQueue.Enqueue(bubbleShuffleQueue.Dequeue()); // Re-insert the craft into the spawn queue from the bubble shuffle queue.
                    }
                    while (craftToSpawn.Count + currentlySpawningCount + currentlyActive < spawnSlots.Count && spawnQueue.Count > 0)
                        craftToSpawn.Enqueue(spawnQueue.Dequeue());
#if DEBUG
                    if (BDArmorySettings.DEBUG_SPAWNING)
                    {
                        var missing = spawnConfig.craftFiles.Where(craftURL => craftURLToVesselName.ContainsKey(craftURL) && (!spawnCounts.ContainsKey(craftURL) || spawnCounts[craftURL] < BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL) && !craftToSpawn.Contains(craftURL) && !FlightGlobals.Vessels.Where(v => !VesselModuleRegistry.ignoredVesselTypes.Contains(v.vesselType) && VesselModuleRegistry.GetModuleCount<MissileFire>(v) > 0).Select(v => v.vesselName).Contains(craftURLToVesselName[craftURL])).ToList();
                        if (missing.Count > 0)
                        {
                            LogMessage("MISSING vessels: " + string.Join(", ", craftURLToVesselName.Where(c => missing.Contains(c.Key)).Select(c => c.Value)), false);
                        }
                    }
#endif
                    if (craftToSpawn.Count > 0)
                    {
                        VesselModuleRegistry.CleanRegistries(); // Clean out any old entries.
                        yield return new WaitWhileFixed(() => LoadedVesselSwitcher.Instance.currentVesselDied); // Wait for the death camera to finish so we don't cause lag for it, then give it an extra second.
                        yield return new WaitForSecondsFixed(1);

                        // Get the spawning point in world position coordinates.
                        var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
                        var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
                        var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
                        if (BDArmorySettings.VESSEL_SPAWN_CS_FOLLOWS_CENTROID) // Allow the spawn point to drift, but bias it back to the original spawn point.
                        {
                            var vessels = LoadedVesselSwitcher.Instance.Vessels.Values.SelectMany(v => v).Where(v => v != null).ToList();
                            foreach (var vessel in vessels) spawnPoint += vessel.CoM;
                            spawnPoint /= 1 + vessels.Count;
                            radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
                            spawnPoint += (spawnConfig.altitude - BodyUtils.GetTerrainAltitudeAtPos(spawnPoint)) * radialUnitVector; // Reset the altitude to the desired spawn altitude.
                        }
                        var refDirection = Math.Abs(Vector3.Dot(Vector3.up, radialUnitVector)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
                        // Configure vessel spawn configs
                        foreach (var craftURL in craftToSpawn)
                        {
                            if (BDArmorySettings.DEBUG_SPAWNING) LogMessage($"Spawning vessel from {craftURL.Substring(AutoSpawnPath.Length - AutoSpawnFolder.Length)} for the {spawnCounts[craftURL]}{spawnCounts[craftURL] switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" }} time.", true);
                            var heading = 360f * spawnSlots[continuousSpawnedVesselCount] / spawnSlots.Count;
                            ++continuousSpawnedVesselCount;
                            continuousSpawnedVesselCount %= spawnSlots.Count;
                            var direction = (Quaternion.AngleAxis(heading, radialUnitVector) * refDirection).ProjectOnPlanePreNormalized(radialUnitVector).normalized;
                            craftSpawnPosition = spawnPoint + spawnDistance * direction;
                            StartCoroutine(SpawnCraft(new VesselSpawnConfig(craftURL, craftSpawnPosition, direction, (float)spawnConfig.altitude, -80f, true, spawnInOrbit, 0, true)));
                        }
                        craftURLToVesselName = spawnedVesselURLs.ToDictionary(kvp => kvp.Value, kvp => kvp.Key); // Update the vesselName-to-craftURL dictionary for the latest spawns.
                        craftToSpawn.Clear(); // Clear the queue since we just spawned all those vessels.
                    }
                    if (vesselsSpawning) // Wait for the initial spawn to be ready before letting CameraTools take over.
                    {
                        yield return new WaitWhileFixed(() => currentlySpawningCount > 0);
                        vesselsSpawning = false;
                    }

                    // Start the competition once we have enough craft.
                    if (currentlyActive > 1 && !(BDACompetitionMode.Instance.competitionIsActive || BDACompetitionMode.Instance.competitionStarting))
                    { BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE, BDArmorySettings.COMPETITION_START_DESPITE_FAILURES); }
                }

                // Kill off vessels that are out of ammo for too long if we're in continuous spawning mode and a competition is active.
                if (BDACompetitionMode.Instance.competitionIsActive)
                    KillOffOutOfAmmoVessels();

                if (BDACompetitionMode.Instance.competitionIsActive)
                {
                    yield return new WaitUntil(() => Planetarium.GetUniversalTime() > currentUpdateTick); // Wait for the current update tick in BDACompetitionMode so that spawning occurs after checks for dead vessels there.
                    yield return waitForFixedUpdate;
                }
                else
                {
                    yield return new WaitForSeconds(1); // 1s between checks. Nothing much happens if nothing needs spawning.
                }
            }
            #endregion
            vesselsSpawningContinuously = false;
            LogMessage("[BDArmory.VesselSpawner]: Continuous vessel spawning ended.", false);
        }

        IEnumerator SpawnCraft(VesselSpawnConfig vesselSpawnConfig)
        {
            ++currentlySpawningCount;
            // Spawn vessel
            yield return SpawnSingleVessel(vesselSpawnConfig);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                --currentlySpawningCount;
                yield break;
            }
            var vessel = GetSpawnedVesselsName(vesselSpawnConfig.craftURL);
            if (vessel == null)
            {
                --currentlySpawningCount;
                yield break;
            }

            // Perform post-spawn stuff.
            yield return PostSpawnMainSequence(vessel, true, BDArmorySettings.VESSEL_SPAWN_INITIAL_VELOCITY, false);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                --currentlySpawningCount;
                yield break;
            }

            // Spawning went fine. Time to blow stuff up!
            AddToActiveCompetition(vessel, true);

            --currentlySpawningCount;
        }

        // Stagger the spawn slots to avoid consecutive craft being launched too close together.
        private List<int> OptimiseSpawnSlots(int slotCount)
        {
            var availableSlots = Enumerable.Range(0, slotCount).ToList();
            if (slotCount < 4) return availableSlots; // Can't do anything about it for < 4 craft.
            var separation = Mathf.CeilToInt(slotCount / 3f); // Start with approximately 120° separation.
            var pos = 0;
            var optimisedSlots = new List<int>();
            while (optimisedSlots.Count < slotCount)
            {
                while (optimisedSlots.Contains(pos)) { ++pos; pos %= slotCount; }
                optimisedSlots.Add(pos);
                pos += separation;
                pos %= slotCount;
            }
            return optimisedSlots;
        }

        #region Scoring
        // For tracking scores across multiple spawns.
        public class ContinuousSpawningScores
        {
            public Vessel vessel; // The vessel.
            public int spawnCount = 0; // The number of times a craft has been spawned.
            public double outOfAmmoTime = 0; // The time the vessel ran out of ammo.
            public Dictionary<int, ScoringData> scoreData = new Dictionary<int, ScoringData>();
            public double cumulativeTagTime = 0;
            public int cumulativeHits = 0;
            public int cumulativeDamagedPartsDueToRamming = 0;
            public int cumulativeDamagedPartsDueToRockets = 0;
            public int cumulativeDamagedPartsDueToMissiles = 0;
            public int cumulativePartsLostToAsteroids = 0;
        };
        public Dictionary<string, ContinuousSpawningScores> continuousSpawningScores;
        public void UpdateCompetitionScores(Vessel vessel, bool newSpawn = false)
        {
            var vesselName = vessel.vesselName;
            if (!continuousSpawningScores.ContainsKey(vesselName)) return;
            var spawnCount = continuousSpawningScores[vesselName].spawnCount - 1;
            if (spawnCount < 0) return; // Initial spawning after scores were reset.
            var scoreData = continuousSpawningScores[vesselName].scoreData;
            if (BDACompetitionMode.Instance.Scores.Players.Contains(vesselName))
            {
                scoreData[spawnCount] = BDACompetitionMode.Instance.Scores.ScoreData[vesselName]; // Save the Score instance for the vessel.
                if (newSpawn)
                {
                    continuousSpawningScores[vesselName].cumulativeTagTime = scoreData.Sum(kvp => kvp.Value.tagTotalTime);
                    continuousSpawningScores[vesselName].cumulativeHits = scoreData.Sum(kvp => kvp.Value.hits);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRamming = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToRamming);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToRockets = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToRockets);
                    continuousSpawningScores[vesselName].cumulativeDamagedPartsDueToMissiles = scoreData.Sum(kvp => kvp.Value.totalDamagedPartsDueToMissiles);
                    continuousSpawningScores[vesselName].cumulativePartsLostToAsteroids = scoreData.Sum(kvp => kvp.Value.partsLostToAsteroids);
                    BDACompetitionMode.Instance.Scores.RemovePlayer(vesselName);
                    BDACompetitionMode.Instance.Scores.AddPlayer(vessel);
                    BDACompetitionMode.Instance.Scores.ScoreData[vesselName].lastDamageTime = scoreData[spawnCount].lastDamageTime;
                    BDACompetitionMode.Instance.Scores.ScoreData[vesselName].lastPersonWhoDamagedMe = scoreData[spawnCount].lastPersonWhoDamagedMe;
                }
            }
        }

        public void DumpContinuousSpawningScores(string tag = "")
        {
            var logStrings = new List<string>();

            if (continuousSpawningScores == null || continuousSpawningScores.Count == 0) return;
            foreach (var vesselName in continuousSpawningScores.Keys)
                UpdateCompetitionScores(continuousSpawningScores[vesselName].vessel);
            if (BDArmorySettings.DEBUG_COMPETITION) BDACompetitionMode.Instance.competitionStatus.Add("Dumping scores for competition " + BDACompetitionMode.Instance.CompetitionID.ToString() + (tag != "" ? " " + tag : ""));
            logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]: Dumping Results at " + (int)(Planetarium.GetUniversalTime() - BDACompetitionMode.Instance.competitionStartTime) + "s");
            foreach (var vesselName in continuousSpawningScores.Keys)
            {
                var vesselScore = continuousSpawningScores[vesselName];
                var scoreData = vesselScore.scoreData;
                logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]: Name:" + vesselName);
                logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  DEATHCOUNT:" + scoreData.Values.Where(v => v.deathTime >= 0).Count());
                var deathTimes = string.Join(";", scoreData.Values.Where(v => v.deathTime >= 0).Select(v => v.deathTime.ToString("0.0")));
                if (deathTimes != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  DEATHTIMES:" + deathTimes);
                #region Bullets
                var whoShotMeScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.hitCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.hitCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoShotMeScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSHOTME:" + whoShotMeScores);
                var whoDamagedMeWithBulletsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromGuns.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromGuns.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithBulletsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHBULLETS:" + whoDamagedMeWithBulletsScores);
                #endregion
                #region Rockets
                var whoStruckMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rocketStrikeCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rocketStrikeCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoStruckMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSTRUCKMEWITHROCKETS:" + whoStruckMeWithRocketsScores);
                var whoPartsHitMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rocketPartDamageCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rocketPartDamageCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoPartsHitMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOPARTSHITMEWITHROCKETS:" + whoPartsHitMeWithRocketsScores);
                var whoDamagedMeWithRocketsScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromRockets.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromRockets.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithRocketsScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHROCKETS:" + whoDamagedMeWithRocketsScores);
                #endregion
                #region Missiles
                var whoStruckMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.missileHitCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.missileHitCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoStruckMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOSTRUCKMEWITHMISSILES:" + whoStruckMeWithMissilesScores);
                var whoPartsHitMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.missilePartDamageCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.missilePartDamageCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoPartsHitMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHOPARTSHITMEWITHMISSILES:" + whoPartsHitMeWithMissilesScores);
                var whoDamagedMeWithMissilesScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.damageFromMissiles.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.damageFromMissiles.Select(kvp2 => kvp2.Value.ToString("0.0") + ":" + kvp2.Key))));
                if (whoDamagedMeWithMissilesScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHODAMAGEDMEWITHMISSILES:" + whoDamagedMeWithMissilesScores);
                #endregion
                #region Rams
                var whoRammedMeScores = string.Join(", ", scoreData.Where(kvp => kvp.Value.rammingPartLossCounts.Count > 0).Select(kvp => kvp.Key + ":" + string.Join(";", kvp.Value.rammingPartLossCounts.Select(kvp2 => kvp2.Value + ":" + kvp2.Key))));
                if (whoRammedMeScores != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  WHORAMMEDME:" + whoRammedMeScores);
                #endregion
                #region Asteroids
                var partsLostToAsteroids = string.Join(", ", scoreData.Where(kvp => kvp.Value.partsLostToAsteroids > 0).Select(kvp => $"{kvp.Key}:{kvp.Value.partsLostToAsteroids}"));
                if (!string.IsNullOrEmpty(partsLostToAsteroids)) logStrings.Add($"[BDArmory.VesselSpawner:{BDACompetitionMode.Instance.CompetitionID}]:  PARTSLOSTTOASTEROIDS: {partsLostToAsteroids}");
                #endregion
                #region Kills
                var GMKills = string.Join(", ", scoreData.Where(kvp => kvp.Value.gmKillReason != GMKillReason.None).Select(kvp => kvp.Key + ":" + kvp.Value.gmKillReason));
                if (GMKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  GMKILL:" + GMKills);
                var specialKills = new HashSet<AliveState> { AliveState.CleanKill, AliveState.HeadShot, AliveState.KillSteal }; // FIXME expand these to the separate special kill types
                var cleanKills = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Guns).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANKILL:" + cleanKills);
                var cleanFrags = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Rockets).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanFrags != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANFRAG:" + cleanFrags);
                var cleanRams = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Ramming).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanRams != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANRAM:" + cleanRams);
                var cleanMissileKills = string.Join(", ", scoreData.Where(kvp => specialKills.Contains(kvp.Value.aliveState) && kvp.Value.lastDamageWasFrom == DamageFrom.Missiles).Select(kvp => kvp.Key + ":" + kvp.Value.lastPersonWhoDamagedMe));
                if (cleanMissileKills != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  CLEANMISSILEKILL:" + cleanMissileKills);
                #endregion
                var accuracy = string.Join(", ", scoreData.Select(kvp => kvp.Key + ":" + kvp.Value.hits + "/" + kvp.Value.shotsFired + ":" + kvp.Value.rocketStrikes + "/" + kvp.Value.rocketsFired));
                if (accuracy != "") logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  ACCURACY:" + accuracy);
                if (BDArmorySettings.TAG_MODE)
                {
                    if (scoreData.Sum(kvp => kvp.Value.tagScore) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TAGSCORE:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagScore > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagScore.ToString("0.0"))));
                    if (scoreData.Sum(kvp => kvp.Value.tagTotalTime) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TIMEIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagTotalTime > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagTotalTime.ToString("0.0"))));
                    if (scoreData.Sum(kvp => kvp.Value.tagKillsWhileIt) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  KILLSWHILEIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagKillsWhileIt > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagKillsWhileIt)));
                    if (scoreData.Sum(kvp => kvp.Value.tagTimesIt) > 0) logStrings.Add("[BDArmory.VesselSpawner:" + BDACompetitionMode.Instance.CompetitionID + "]:  TIMESIT:" + string.Join(", ", scoreData.Where(kvp => kvp.Value.tagTimesIt > 0).Select(kvp => kvp.Key + ":" + kvp.Value.tagTimesIt)));
                }
            }

            // Dump the log results to a file.
            if (BDACompetitionMode.Instance.CompetitionID > 0)
            {
                var folder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "BDArmory", "Logs");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, "cts-" + BDACompetitionMode.Instance.CompetitionID.ToString() + (tag != "" ? "-" + tag : "") + ".log"), logStrings);
            }
        }
        #endregion

        public void KillOffOutOfAmmoVessels()
        {
            if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 0) return; // Never
            var now = Planetarium.GetUniversalTime();
            Vessel vessel;
            MissileFire weaponManager;
            ContinuousSpawningScores score;
            foreach (var vesselName in continuousSpawningScores.Keys)
            {
                score = continuousSpawningScores[vesselName];
                vessel = score.vessel;
                if (vessel == null) continue; // Vessel hasn't been respawned yet.
                weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                if (weaponManager == null) continue; // Weapon manager hasn't registered yet.
                if (score.outOfAmmoTime == 0 && !weaponManager.HasWeaponsAndAmmo())
                    score.outOfAmmoTime = Planetarium.GetUniversalTime();
                if (score.outOfAmmoTime > 0 && now - score.outOfAmmoTime > BDArmorySettings.OUT_OF_AMMO_KILL_TIME)
                {
                    LogMessage("Killing off " + vesselName + " as they exceeded the out-of-ammo kill time.");
                    BDACompetitionMode.Instance.Scores.RegisterDeath(vesselName, GMKillReason.OutOfAmmo); // Indicate that it was us who killed it and remove any "clean" kills.
                    SpawnUtils.RemoveVessel(vessel);
                }
            }
        }
    }
}