﻿using System;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Settings;

namespace BDArmory.Utils
{
    public static class BlastPhysicsUtils
    {
        // This values represent percentage of the blast radius where we consider that the damage happens.

        // Methodology based on AASTP-1: MANUAL OF NATO SAFETY PRINCIPLES FOR THE STORAGE OF MILITARY AMMUNITION AND EXPLOSIVES
        // Link: http://www.rasrinitiative.org/pdfs/AASTP-1-Ed1-Chge-3-Public-Release-110810.pdf
        public static BlastInfo CalculatePartBlastEffects(Part part, float distanceToHit, double vesselMass, float explosiveMass, float range)
        {
            float clampedMinDistanceToHit = ClampRange(explosiveMass, distanceToHit);

            var minPressureDistance = distanceToHit + part.GetAverageBoundSize();

            double minPressurePerMs = 0;

            float clampedMaxDistanceToHit = ClampRange(explosiveMass, minPressureDistance);
            double maxScaledDistance = CalculateScaledDistance(explosiveMass, clampedMaxDistanceToHit);
            double maxDistPositivePhase = CalculatePositivePhaseTime(maxScaledDistance, explosiveMass);

            if (minPressureDistance <= range)
            {
                minPressurePerMs = CalculateIncidentImpulse(maxScaledDistance, explosiveMass);
            }

            double minScaledDistance = CalculateScaledDistance(explosiveMass, clampedMinDistanceToHit);
            double maxPressurePerMs = CalculateIncidentImpulse(minScaledDistance, explosiveMass);
            double minDistPositivePhase = CalculatePositivePhaseTime(minScaledDistance, explosiveMass);

            double totalDamage = (maxPressurePerMs + minPressurePerMs);// * 2 / 2 ;

            float effectivePartArea = CalculateEffectiveBlastAreaToPart(range, part);

            double maxforce = CalculateForce(maxPressurePerMs, effectivePartArea, minDistPositivePhase);
            double minforce = CalculateForce(minPressurePerMs, effectivePartArea, maxDistPositivePhase);

            float positivePhase = (float)(minDistPositivePhase + maxDistPositivePhase) / 2f;

            double force = (maxforce + minforce) / 2f;

            float acceleration = vesselMass > 0 ? (float)(force / vesselMass) : 0; // If the vesselMass is 0, don't give infinite acceleration!

            // Calculation of damage

            float finalDamage = (float)totalDamage;

            if (BDArmorySettings.DEBUG_DAMAGE)
            {
                Debug.Log(
                    "[BDArmory.BlastPhysicsUtils]: Blast Debug data: {" + part.name + " on " + part.vessel.vesselName + "}, " +
                    " clampedMinDistanceToHit: {" + clampedMinDistanceToHit + "}," +
                    " minPressureDistance: {" + minPressureDistance + "}," +
                    " minScaledDistance: {" + minScaledDistance + "}," +
                    " maxScaledDistance: {" + maxScaledDistance + "}," +
                    " minPressurePerMs: {" + minPressurePerMs + "}," +
                    " maxPressurePerMs: {" + maxPressurePerMs + "}," +
                    " minDistPositivePhase: {" + minDistPositivePhase + "}," +
                    " maxDistPositivePhase: {" + maxDistPositivePhase + "}," +
                    " totalDamage: {" + totalDamage + "}," +
                    " finalDamage: {" + finalDamage + "},");
            }

            return new BlastInfo() { TotalPressure = maxPressurePerMs, EffectivePartArea = effectivePartArea, PositivePhaseDuration = positivePhase, VelocityChange = acceleration, Damage = finalDamage };
        }

        private static float CalculateEffectiveBlastAreaToPart(float range, Part part)
        {
            float circularArea = Mathf.PI * range * range;

            return Mathf.Clamp(circularArea, 0f, part.GetArea() * 0.40f);
        }

        private static double CalculateScaledDistance(float explosiveCharge, float distanceToHit)
        {
            return (distanceToHit / Math.Pow(explosiveCharge, 1f / 3f));
        }

        private static float ClampRange(float explosiveCharge, float distanceToHit)
        {
            float cubeRootOfChargeWeight = (float)Math.Pow(explosiveCharge, 1f / 3f);

            return Mathf.Clamp(distanceToHit, 0.0674f * cubeRootOfChargeWeight, 40f * cubeRootOfChargeWeight);
        }

        private static double CalculateIncidentImpulse(double scaledDistance, float explosiveCharge)
        {
            double t = Math.Log(scaledDistance) / Math.Log(10);
            double cubeRootOfChargeWeight = Math.Pow(explosiveCharge, 0.3333333);
            double ii = 0;
            if (scaledDistance <= 0.955)
            { //NATO version
                double U = 2.06761908721 + 3.0760329666 * t;
                var U2 = U * U;
                var U3 = U2 * U;
                var U4 = U3 * U;
                ii = 2.52455620925 - 0.502992763686 * U +
                     0.171335645235 * U2 +
                     0.0450176963051 * U3 -
                     0.0118964626402 * U4;
            }
            else if (scaledDistance > 0.955)
            { //version from ???
                var U = -1.94708846747 + 2.40697745406 * t;
                var U2 = U * U;
                var U3 = U2 * U;
                var U4 = U3 * U;
                var U5 = U4 * U;
                var U6 = U5 * U;
                var U7 = U6 * U;
                ii = 1.67281645863 - 0.384519026965 * U -
                     0.0260816706301 * U2 +
                     0.00595798753822 * U3 +
                     0.014544526107 * U4 -
                     0.00663289334734 * U5 -
                     0.00284189327204 * U6 +
                     0.0013644816227 * U7;
            }

            ii = Math.Pow(10, ii);
            ii = ii * cubeRootOfChargeWeight;
            return ii;
        }

        // Calculate positive phase time in ms from AASTP-1
        private static double CalculatePositivePhaseTime(double scaledDistance, float explosiveCharge)
        {
            scaledDistance = Math.Min(Math.Max(scaledDistance, 0.178), 40); // Formula only valid for scaled distances between 0.178 and 40 m
            double t = Math.Log(scaledDistance) / Math.Log(10);
            double cubeRootOfChargeWeight = Math.Pow(explosiveCharge, 0.3333333);
            double ii = 0;

            if (scaledDistance <= 1.01)
            {
                double U = 1.92946154068 + 5.25099193925 * t;
                var U2 = U * U;
                var U3 = U2 * U;
                var U4 = U3 * U;
                var U5 = U4 * U;
                ii = -0.614227603559 + 0.130143717675 * U +
                    0.134872511954 * U2 +
                    0.0391574276906 * U3 -
                    0.00475933664702 * U4 -
                    0.00428144598008 * U5;
            }
            else if (scaledDistance <= 2.78)
            {
                double U = -2.12492525216 + 9.2996288611 * t;
                var U2 = U * U;
                var U3 = U2 * U;
                var U4 = U3 * U;
                var U5 = U4 * U;
                var U6 = U5 * U;
                var U7 = U6 * U;
                var U8 = U7 * U;
                ii = 0.315409245784 - 0.0297944268976 * U +
                    0.030632954288 * U2 +
                    0.0183405574086 * U3 -
                    0.0173964666211 * U4 -
                    0.00106321963633 * U5 +
                    0.00562060030977 * U6 +
                    0.0001618217499 * U7 -
                    0.0006860188944 * U8;
            }
            else // scaledDistance > 2.78
            {
                double U = -3.53626218091 + 3.46349745571 * t;
                var U2 = U * U;
                var U3 = U2 * U;
                var U4 = U3 * U;
                var U5 = U4 * U;
                ii = 0.686906642409 + 0.0933035304009 * U -
                    0.0005849420883 * U2 -
                    0.00226884995013 * U3 -
                    0.00295908591505 * U4 +
                    0.00148029868929 * U5;
            }

            ii = Math.Pow(10, ii);
            ii = ii * cubeRootOfChargeWeight;
            return ii;
        }

        // Calculate duration of explosion event in seconds
        public static float CalculateMaxTime(float tntMass)
        {
            float range = CalculateBlastRange(tntMass);
            range = ClampRange(tntMass, range);
            double scaledDistance = CalculateScaledDistance(tntMass, range);

            double t = Math.Log(scaledDistance) / Math.Log(10);
            double cubeRootOfChargeWeight = Math.Pow(tntMass, 0.3333333);
            double ii = 0;

            double U = -0.202425716178 + 1.37784223635 * t;
                var U2 = U * U;
                var U3 = U2 * U;
                var U4 = U3 * U;
                var U5 = U4 * U;
                var U6 = U5 * U;
                var U7 = U6 * U;
                var U8 = U7 * U;
                var U9 = U8 * U;
            ii = -0.0591634288046 + 1.35706496258 * U +
                0.052492798645 * U2 -
                0.196563954086 * U3 -
                0.0601770052288 * U4 +
                0.0696360270981 * U5 +
                0.0215297490092 * U6 -
                0.0161658930785 * U7 -
                0.00232531970294 * U8 +
                0.00147752067524 * U9;

            ii = Math.Pow(10, ii);
            ii = ii * cubeRootOfChargeWeight / 1000f;
            return (float)ii;
        }

        /// <summary>
        /// Calculate newtons from the pressure in kPa and the surface on Square meters
        /// </summary>
        /// <param name="pressure">kPa</param>
        /// <param name="surface">m2</param>
        /// <returns></returns>
        private static double CalculateForce(double pressure, float surface, double timeInMs)
        {
            return pressure * 1000f * surface * (timeInMs / 1000f);
        }

        /// <summary>
        /// Method based on Hopkinson-Cranz Scaling Law
        /// Z value of 14.8
        /// </summary>
        /// <param name="tntMass"> tnt equivales mass in kg</param>
        /// <returns>explosive range in meters </returns>
        public static float CalculateBlastRange(double tntMass)
        {
            return (float)(14.8f * Math.Pow(tntMass, 1 / 3f));
        }

        /// <summary>
        /// Method based on Hopkinson-Cranz Scaling Law
        /// Z value of 14.8
        /// </summary>
        /// <param name="range"> expected range in meters</param>
        /// <returns>explosive range in meters </returns>
        public static float CalculateExplosiveMass(float range)
        {
            var scaledRange = range / 14.8f;
            return (float)(scaledRange * scaledRange * scaledRange);
        }
    }

    public struct BlastInfo
    {
        public float VelocityChange { get; set; }
        public float EffectivePartArea { get; set; }
        public float Damage { get; set; }
        public double TotalPressure { get; set; }
        public double PositivePhaseDuration { get; set; }
    }
}
