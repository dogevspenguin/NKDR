using System.Collections;
using UnityEngine;

namespace BDArmory.VesselSpawning.SpawnStrategies
{
    /// <summary>
    /// A simple pass-through strategy to be able to use the current spawning functions properly.
    /// </summary>
    public class SpawnConfigStrategy : SpawnStrategy
    {
        private SpawnConfig spawnConfig;
        private bool success = false;

        public SpawnConfigStrategy(SpawnConfig spawnConfig) { this.spawnConfig = spawnConfig; }

        public IEnumerator Spawn(VesselSpawnerBase spawner)
        {
            yield return spawner.Spawn(spawnConfig);

            if (!spawner.vesselSpawnSuccess)
            {
                Debug.Log($"[BDArmory.SpawnConfigStrategy]: Vessel spawning failed: {spawner.spawnFailureReason}");
                yield break;
            }

            success = true;
        }

        public bool DidComplete()
        {
            return success;
        }
    }
}
