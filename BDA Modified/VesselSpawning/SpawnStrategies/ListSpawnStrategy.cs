using System.Collections;
using System.Collections.Generic;

namespace BDArmory.VesselSpawning.SpawnStrategies
{
    public class ListSpawnStrategy : SpawnStrategy
    {
        private List<SpawnStrategy> strategies;
        private bool success = false;

        public ListSpawnStrategy(List<SpawnStrategy> strategies)
        {
            this.strategies = strategies;
        }

        public bool DidComplete()
        {
            return success;
        }

        public IEnumerator Spawn(VesselSpawnerBase spawner)
        {
            success = false;
            foreach (var item in strategies)
            {
                yield return item.Spawn(spawner);
            }
            success = true;
        }
    }
}
