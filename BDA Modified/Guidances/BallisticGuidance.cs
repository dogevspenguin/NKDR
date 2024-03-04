using System;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.Weapons.Missiles;

namespace BDArmory.Guidances
{
    class BallisticGuidance : IGuidance
    {
        private Vector3 _startPoint;
        private double _originalDistance = float.MinValue;


        public Vector3 GetDirection(MissileBase missile, Vector3 targetPosition, Vector3 targetVelocity)
        {
            //set up
            if (_originalDistance == float.MinValue)
            {
                _startPoint = missile.vessel.CoM;
                _originalDistance = Vector3.Distance(targetPosition, missile.vessel.CoM);
            }

            var surfaceDistanceVector = Vector3
                .Project((missile.vessel.CoM - _startPoint), (targetPosition - _startPoint).normalized);

            var pendingDistance = _originalDistance - surfaceDistanceVector.magnitude;

            if (missile.TimeIndex < 1)
            {
                return missile.vessel.CoM + missile.vessel.Velocity() * 10;
            }

            Vector3 agmTarget;

            if (missile.vessel.verticalSpeed > 0 && pendingDistance > _originalDistance * 0.5)
            {
                var freeFallTime = CalculateFreeFallTime(missile);

                var futureDistanceVector = Vector3
                    .Project((missile.vessel.GetFuturePosition() - _startPoint), (targetPosition - _startPoint).normalized);

                var futureHorizontalSpeed = CalculateFutureHorizontalSpeed(missile);

                var horizontalTime = (_originalDistance - futureDistanceVector.magnitude) / futureHorizontalSpeed;

                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES)
                {
                    missile.debugString.AppendLine($"Ascending");
                    missile.debugString.AppendLine($"freeFallTime: {freeFallTime}");
                    missile.debugString.AppendLine($"horizontalTime: {horizontalTime}");
                }

                if (freeFallTime >= horizontalTime)
                {
                    if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES) missile.debugString.AppendLine($"Free fall achieved:");
                    missile.Throttle = Mathf.Clamp(missile.Throttle - 0.001f, 0.01f, 1f);
                }
                else
                {
                    if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES) missile.debugString.AppendLine($"Free fall not achieved:");
                    missile.Throttle = Mathf.Clamp(missile.Throttle + 0.001f, 0.01f, 1f);
                }

                Vector3 dToTarget = targetPosition - missile.vessel.CoM;
                Vector3 direction = Quaternion.AngleAxis(Mathf.Clamp(missile.maxOffBoresight * 0.9f, 0, missile.BallisticAngle), Vector3.Cross(dToTarget, missile.vessel.up)) * dToTarget;
                agmTarget = missile.vessel.CoM + direction;


                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES) missile.debugString.AppendLine($"Throttle: {missile.Throttle}");
            }
            else
            {
                if (BDArmorySettings.DEBUG_TELEMETRY || BDArmorySettings.DEBUG_MISSILES) missile.debugString.AppendLine($"Descending");
                agmTarget = MissileGuidance.GetAirToGroundTarget(targetPosition, targetVelocity, missile.vessel, 1.85f);

                missile.Throttle = Mathf.Clamp((float)(missile.vessel.atmDensity * 10f), 0.01f, 1f);
            }

            if (missile is BDModularGuidance)
            {
                if (missile.vessel.InVacuum())
                {
                    missile.vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Prograde);
                    agmTarget = missile.vessel.CoM + missile.vessel.Velocity() * 100;
                }
                else
                {
                    missile.vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                }
            }
            return agmTarget;
        }

        private double CalculateFreeFallTime(MissileBase missile, int predictionTime = 10)
        {
            double vi = CalculateFutureVerticalSpeed(missile, predictionTime) * -1;
            double a = 9.80665f * missile.BallisticOverShootFactor;
            double d = missile.vessel.GetFutureAltitude(predictionTime);

            double time1 = (-vi + Math.Sqrt(vi * vi - 4 * (0.5f * a) * (-d))) / a;
            double time2 = (-vi - Math.Sqrt(vi * vi - 4 * (0.5f * a) * (-d))) / a;

            return Math.Max(time1, time2);
        }

        private double CalculateFutureHorizontalSpeed(MissileBase missile, int predictionTime = 10)
        {
            return missile.vessel.horizontalSrfSpeed + (missile.HorizontalAcceleration / Time.fixedDeltaTime) * predictionTime;
        }

        private double CalculateFutureVerticalSpeed(MissileBase missile, int predictionTime = 10)
        {
            return missile.vessel.verticalSpeed + (missile.VerticalAcceleration / Time.fixedDeltaTime) * predictionTime;
        }
    }
}
