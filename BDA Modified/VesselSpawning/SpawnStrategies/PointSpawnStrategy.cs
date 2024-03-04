using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Settings;

namespace BDArmory.VesselSpawning.SpawnStrategies
{
    public class PointSpawnStrategy : SpawnStrategy
    {
        private string craftUrl;
        private double latitude, longitude, altitude;
        private float heading, pitch;
        private bool success = false;

        public PointSpawnStrategy(string craftUrl, double latitude, double longitude, double altitude, float heading, float pitch = -0.7f)
        {
            this.craftUrl = craftUrl;
            this.latitude = latitude;
            this.longitude = longitude;
            this.altitude = altitude;
            this.heading = heading;
            this.pitch = pitch;
        }

        public IEnumerator Spawn(VesselSpawnerBase spawner)
        {
            Debug.Log("[BDArmory.BDAScoreService] PointSpawnStrategy spawning.");

            // TODO: support body targeting; fixed as Kerbin for now
            var worldIndex = FlightGlobals.GetBodyIndex(FlightGlobals.GetBodyByName("Kerbin"));

            // spawn the given craftUrl at the given location/heading/pitch
            // yield return spawner.SpawnVessel(craftUrl, latitude, longitude, altitude, heading, pitch);

            // AUBRANIUM, in order to make the VesselSpawner abstract class fit with the way you've defined the SpawnStrategy interface, I found it necessary to shoe-horn the single craft spawning in like this.
            // This is far from optimal and really needs a better solution.
            // Essentially, the differences in the spawning strategies are so large, that I don't think the currently defined interface is really suitable.
            // One option would be to remove the "VesselSpawner spawner" from the "public IEnumerator Spawn(VesselSpawner spawner);" in SpawnStrategy.cs and get the appropriate vessel spawner instance directly in each SpawnStrategy.Spawn function, which would then call the specific spawning functions of the vessel spawner instead of "spawner.Spawn(spawnConfig)" as below.
            // E.g., yield return SingleVesselSpawning.Instance.SpawnVessel(craftUrl, latitude, longitude, altitude, heading, pitch);
            yield return spawner.Spawn(new SpawnConfig(worldIndex, latitude, longitude, altitude, false, false, 0, null, null, "", new List<string>{craftUrl}));

            // wait for spawner to finish
            yield return new WaitWhile(() => spawner.vesselsSpawning);

            if (!spawner.vesselSpawnSuccess)
            {
                Debug.Log("[BDArmory.BDAScoreService] PointSpawnStrategy failed.");
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
