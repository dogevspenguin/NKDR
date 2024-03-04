using UnityEngine;
using System;

namespace BDArmory.Extensions
{
    public static class BodyExtensions
    {
        public static bool FindFlatSpotNear(this CelestialBody body, double startLatitude, double startLongitude, out double latitude, out double longitude, double maxDeviation)
        {
            var iLat = startLatitude;
            var iLng = startLongitude;
            var dLat = 0.0;
            var dLng = 0.0;
            var threshold = 0.000001;
            var maxIterations = 1000;
            var k = 0;

            while( k++ < maxIterations )
            {
                var norm = body.GetRelSurfaceNVector(iLat + dLat, iLng + dLng);
                var normA = body.GetRelSurfaceNVector(iLat + dLat - threshold, iLng + dLng);
                var normB = body.GetRelSurfaceNVector(iLat + dLat + threshold, iLng + dLng);
                var normC = body.GetRelSurfaceNVector(iLat + dLat, iLng + dLng - threshold);
                var normD = body.GetRelSurfaceNVector(iLat + dLat, iLng + dLng + threshold);

                var angle = Vector3.Angle(norm, Vector3.up);
                var angleA = Vector3.Angle(normA, Vector3.up);
                var angleB = Vector3.Angle(normB, Vector3.up);
                var angleC = Vector3.Angle(normC, Vector3.up);
                var angleD = Vector3.Angle(normD, Vector3.up);

                if (angleA < angle)
                {
                    dLat -= threshold;
                }
                else if (angleB < angle)
                {
                    dLat += threshold;
                }
                else if (angleC < angle)
                {
                    dLng -= threshold;
                }
                else if (angleD < angle)
                {
                    dLng += threshold;
                }
                else
                {
                    break;
                }
            }

            latitude = iLat + dLat;
            longitude = iLng + dLng;

            return k < maxIterations;
        }

        public static double MinSafeAltitude(this CelestialBody body)
        {
            double maxTerrainHeight = 200;
            if (body.pqsController)
            {
                PQS pqs = body.pqsController;
                maxTerrainHeight = pqs.radiusMax - pqs.radius;
            }
            return Math.Max(maxTerrainHeight, body.atmosphereDepth);
        }
    }
}
