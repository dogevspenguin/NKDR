using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Competition.OrchestrationStrategies;
using BDArmory.Settings;
using BDArmory.VesselSpawning.SpawnStrategies;
using BDArmory.VesselSpawning;

namespace BDArmory.Competition
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class TournamentCoordinator : MonoBehaviour
    {
        public static TournamentCoordinator Instance;
        private SpawnStrategy spawnStrategy;
        private OrchestrationStrategy orchestrator;
        private VesselSpawnerBase vesselSpawner;
        private Coroutine executing = null;
        private Coroutine executingForEach = null;
        public bool IsRunning { get; private set; }

        void Awake()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
        }

        public void Configure(SpawnStrategy spawner, OrchestrationStrategy orchestrator, VesselSpawnerBase vesselSpawner)
        {
            this.spawnStrategy = spawner;
            this.orchestrator = orchestrator;
            this.vesselSpawner = vesselSpawner;
        }

        public void Run()
        {
            Stop();
            executing = StartCoroutine(Execute());
        }

        public void Stop()
        {
            if (executing != null)
            {
                StopCoroutine(executing);
                executing = null;
                orchestrator.CleanUp();
            }
        }

        public IEnumerator Execute()
        {
            IsRunning = true;

            // clear all vessels
            yield return SpawnUtils.RemoveAllVessels();

            // first, spawn vessels
            yield return spawnStrategy.Spawn(vesselSpawner);

            if (!spawnStrategy.DidComplete())
            {
                Debug.Log($"[BDArmory.TournamentCoordinator]: TournamentCoordinator spawn failed: {vesselSpawner.spawnFailureReason}");
                yield break;
            }

            // now, hand off to orchestrator
            yield return orchestrator.Execute(null, null);

            IsRunning = false;
        }

        public void RunForEach<T>(List<T> strategies, OrchestrationStrategy orchestrator, VesselSpawnerBase spawner) where T : SpawnStrategy
        {
            StopForEach();
            executingForEach = StartCoroutine(ExecuteForEach(strategies, orchestrator, spawner));
        }

        public void StopForEach()
        {
            if (executingForEach != null)
            {
                StopCoroutine(executingForEach);
                executingForEach = null;
                orchestrator.CleanUp();
            }
        }

        IEnumerator ExecuteForEach<T>(List<T> strategies, OrchestrationStrategy orchestrator, VesselSpawnerBase spawner) where T : SpawnStrategy
        {
            int i = 0;
            foreach (var strategy in strategies)
            {
                Configure(strategy, orchestrator, spawner);
                Run();
                yield return new WaitWhile(() => IsRunning);
                if (++i < strategies.Count())
                {
                    double startTime = Planetarium.GetUniversalTime();
                    while ((Planetarium.GetUniversalTime() - startTime) < BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS)
                    {
                        BDACompetitionMode.Instance.competitionStatus.Add("Waiting " + (BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS - (Planetarium.GetUniversalTime() - startTime)).ToString("0") + "s, then running the next round.");
                        yield return new WaitForSeconds(1);
                    }
                }
            }
        }
    }
}