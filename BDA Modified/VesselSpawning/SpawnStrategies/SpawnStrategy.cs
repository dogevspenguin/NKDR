using System.Collections;

namespace BDArmory.VesselSpawning.SpawnStrategies
{
    public interface SpawnStrategy
    {
        /// <summary>
        /// Part 1 of Remote Orchestration
        ///
        /// Spawns craft according to a provided strategy and prepares them for flight.
        /// </summary>
        /// <param name="spawner"></param>
        /// <returns></returns>
        public IEnumerator Spawn(VesselSpawnerBase spawner);

        public bool DidComplete();
    }
}
