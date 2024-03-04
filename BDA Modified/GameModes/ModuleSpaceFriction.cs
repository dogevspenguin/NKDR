using System.Linq;
using UnityEngine;

using BDArmory.Control;
using BDArmory.Settings;
using BDArmory.Utils;
using System.Collections.Generic;

namespace BDArmory.GameModes
{
    public class ModuleSpaceFriction : PartModule
    {
        /// <summary>
        /// Adds friction/drag to craft in null-atmo porportional to AI MaxSpeed setting to ensure craft does not exceed said speed
        /// Adds counter-gravity to prevent null-atmo ships from falling to the ground from gravity in the absence of wings and lift
        /// Provides additional friction/drag during corners to help spacecraft drift through turns instead of being stuck with straight-up joust charges
        /// TL;DR, provides the means for SciFi style space dogfights
        /// </summary>

        private double frictionCoeff = 1.0f; //how much force is applied to decellerate craft

        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Space Friction"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        //public bool FrictionEnabled = false; //global value

        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "CounterGrav"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        //public bool AntiGravEnabled = false; //global value

        [KSPField(isPersistant = true)]
        public bool AntiGravOverride = false; //per craft override to be set in the .craft file, for things like zeppelin battles where attacking planes shouldn't be under countergrav
        [KSPField(isPersistant = true)]
        public bool RepulsorOverride = false;
        public float maxVelocity = 300; //MaxSpeed setting in PilotAI

        public float frictMult; //engine thrust of craft

        float targetAlt = 25;
        //public float driftMult = 2; //additional drag multipler for cornering/decellerating so things don't take the same amount of time to decelerate as they do to accelerate

        List<ModuleWheelBase> repulsors;
        List<ModuleSpaceFriction> spaceFrictionModules;
        public static bool GameIsPaused
        {
            get { return PauseMenu.isOpen || Time.timeScale == 0; }
        }
        BDModulePilotAI AI;
        public BDModulePilotAI pilot
        {
            get
            {
                if (AI) return AI;
                AI = VesselModuleRegistry.GetBDModulePilotAI(vessel, true); // FIXME should this be IBDAIControl?
                return AI;
            }
        }
        BDModuleSurfaceAI SAI;
        public BDModuleSurfaceAI driver
        {
            get
            {
                if (SAI) return SAI;
                SAI = VesselModuleRegistry.GetBDModuleSurfaceAI(vessel, true);
                return SAI;
            }
        }

        BDModuleVTOLAI VAI;
        public BDModuleVTOLAI flier
        {
            get
            {
                if (VAI) return VAI;
                VAI = VesselModuleRegistry.GetModule<BDModuleVTOLAI>(vessel);

                return VAI;
            }
        }

        BDModuleOrbitalAI OAI;
        public BDModuleOrbitalAI orbiter
        {
            get
            {
                if (OAI) return OAI;
                OAI = VesselModuleRegistry.GetModule<BDModuleOrbitalAI>(vessel);

                return OAI;
            }
        }

        ModuleEngines Engine;
        public ModuleEngines foundEngine
        {
            get
            {
                if (Engine) return Engine;
                Engine = VesselModuleRegistry.GetModuleEngines(vessel).FirstOrDefault();
                return Engine;
            }
        }
        MissileFire MF;
        public MissileFire weaponManager
        {
            get
            {
                if (MF) return MF;
                MF = VesselModuleRegistry.GetMissileFire(vessel, true);
                return MF;
            }
        }

        void Start()
        {
            if (vessel.rootPart == this.part) //if we're an external non-root repulsor part, don't check for dupes in root.
            {
                foreach (var repMod in vessel.rootPart.FindModulesImplementing<ModuleSpaceFriction>())
                {
                    if (repMod != this)
                    {
                        // Not really sure how this is happening, but it is. It looks a bit like a race condition somewhere is allowing this module to be added twice.
                        Debug.LogWarning($"[BDArmory.GameModes.ModuleSpaceFriction]: Found a duplicate space friction module on root part of {vessel.vesselName}! Removing...");
                        Destroy(repMod);
                    }
                }
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!RepulsorOverride) //MSF added via Spawn utilities for Space Hacks
                {
                    using (var engine = VesselModuleRegistry.GetModuleEngines(vessel).GetEnumerator())
                        while (engine.MoveNext())
                        {
                            if (engine.Current == null) continue;
                            if (engine.Current.independentThrottle) continue; //only grab primary thrust engines
                            frictMult += (engine.Current.maxThrust * (engine.Current.thrustPercentage / 100)); //FIXME - Look into grabbing max thrust from velCurve, if for whatever reason a rocket engine has one of these
                            //have this called onvesselModified?
                        }
                    frictMult /= 6; //doesn't need to be 100% of thrust at max speed, Ai will already self-limit; this also has the AI throttle down, which allows for slamming the throttle full for braking/coming about, instead of being stuck with lower TwR
                    repulsors = VesselModuleRegistry.GetRepulsorModules(vessel);
                    using (var r = repulsors.GetEnumerator())
                        while (r.MoveNext())
                        {
                            if (r.Current == null) continue;
                            r.Current.part.PhysicsSignificance = 1; 
                        }
                }
                else
                {
                    spaceFrictionModules = VesselModuleRegistry.GetModules<ModuleSpaceFriction>(vessel);
                }
            }
        }

        public void FixedUpdate()
        {
            if ((!BDArmorySettings.SPACE_HACKS && (!AntiGravOverride && !RepulsorOverride)) || !HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || this.vessel.packed || GameIsPaused) return;

            if (this.part.vessel.situation == Vessel.Situations.FLYING || this.part.vessel.situation == Vessel.Situations.SUB_ORBITAL)
            {
                if (BDArmorySettings.SF_FRICTION)
                {
                    if (this.part.vessel.speed > 10)
                    {
                        if (AI != null)
                        {
                            maxVelocity = AI.maxSpeed;
                        }
                        else if (SAI != null)
                        {
                            maxVelocity = SAI.MaxSpeed;
                        }
                        else if (VAI != null)
                            maxVelocity = VAI.MaxSpeed;

                        var speedFraction = (float)part.vessel.speed / maxVelocity;
                        if (speedFraction > 1) speedFraction = Mathf.Max(2, speedFraction);
                        frictionCoeff = speedFraction * speedFraction * speedFraction * frictMult; //at maxSpeed, have friction be 100% of vessel's engines thrust

                        frictionCoeff *= (1 + (Vector3.Angle(this.part.vessel.srf_vel_direction, this.part.vessel.GetTransform().up) / 180) * BDArmorySettings.SF_DRAGMULT * 4); //greater AoA off prograde, greater drag
                        frictionCoeff /= vessel.Parts.Count;
                        //part.vessel.rootPart.rb.AddForceAtPosition((-part.vessel.srf_vel_direction * frictionCoeff), part.vessel.CoM, ForceMode.Acceleration);
                        using (var p = part.vessel.Parts.GetEnumerator())
                            while (p.MoveNext())
                            {
                                if (p.Current == null || p.Current.PhysicsSignificance == 1) continue;
                                p.Current.Rigidbody.AddForceAtPosition((-part.vessel.srf_vel_direction * frictionCoeff), part.vessel.CoM, ForceMode.Acceleration);
                            }
                    }
                }
                if (BDArmorySettings.SF_GRAVITY || AntiGravOverride) //have this disabled if no engines left?
                {
                    if (weaponManager != null && foundEngine != null) //have engineless craft fall
                    {
                        using (var p = part.vessel.Parts.GetEnumerator())
                            while (p.MoveNext())
                            {
                                if (p.Current == null || p.Current.PhysicsSignificance == 1) continue; //attempting to apply rigidbody force to non-significant parts will NRE
                                p.Current.Rigidbody.AddForce(-FlightGlobals.getGeeForceAtPosition(p.Current.transform.position), ForceMode.Acceleration);
                            }
                    }
                    else //out of control/engineless craft get hurtled into the ground
                    {
                        using (var p = part.vessel.Parts.GetEnumerator())
                            while (p.MoveNext())
                            {
                                if (p.Current == null || p.Current.PhysicsSignificance == 1) continue;
                                p.Current.Rigidbody.AddForce(FlightGlobals.getGeeForceAtPosition(p.Current.transform.position), ForceMode.Acceleration);
                            }
                    }
                }
            }
            if (this.part.vessel.situation != Vessel.Situations.ORBITING || this.part.vessel.situation != Vessel.Situations.DOCKED || this.part.vessel.situation != Vessel.Situations.ESCAPING || this.part.vessel.situation != Vessel.Situations.PRELAUNCH)
            {
                if (BDArmorySettings.SF_REPULSOR || RepulsorOverride)
                {
                    if ((pilot != null || driver != null || flier != null || RepulsorOverride) && foundEngine != null)
                    {
                        targetAlt = 10;
                        if (AI != null)
                        {
                            targetAlt = AI.defaultAltitude; // Use default alt instead of min alt to keep the vessel away from 'gain alt' behaviour.
                        }
                        else if (SAI != null)
                        {
                            targetAlt = SAI.MaxSlopeAngle * 2;
                        }
                        else if (VAI != null)
                            targetAlt = VAI.defaultAltitude;

                        Vector3d grav = FlightGlobals.getGeeForceAtPosition(vessel.CoM);
                        var vesselMass = part.vessel.GetTotalMass();
                        if (RepulsorOverride) //Asking this first, so SPACEHACKS repulsor mode will ignore it
                        {
                            float pointAltitude = BodyUtils.GetRadarAltitudeAtPos(part.transform.position);
                            if (pointAltitude <= 0 || pointAltitude > 2f * targetAlt) return;
                            var factor = Mathf.Clamp(Mathf.Exp(BDArmorySettings.SF_REPULSOR_STRENGTH * (targetAlt - pointAltitude) / targetAlt - (float)vessel.verticalSpeed / targetAlt), 0f, 5f * BDArmorySettings.SF_REPULSOR_STRENGTH); // Decaying exponential balanced at the target altitude with velocity damping.
                            float repulsorForce = vesselMass * factor / spaceFrictionModules.Count; // Spread the force between the repulsors.
                            if (float.IsNaN(factor) || float.IsInfinity(factor)) // This should only happen if targetAlt is 0, which should never happen.
                                Debug.LogWarning($"[BDArmory.Spacehacks]: Repulsor Force is NaN or Infinity. TargetAlt: {targetAlt}, point Alt: {pointAltitude}, VesselMass: {vesselMass}");
                            else
                                part.Rigidbody.AddForce(-grav * repulsorForce, ForceMode.Force);
                        }
                        else
                        {
                            using (var repulsor = repulsors.GetEnumerator())
                                while (repulsor.MoveNext())
                                {
                                    if (repulsor.Current == null) continue;
                                    float pointAltitude = BodyUtils.GetRadarAltitudeAtPos(repulsor.Current.transform.position);
                                    if (pointAltitude <= 0 || pointAltitude > 2f * targetAlt) continue;
                                    var factor = Mathf.Clamp(Mathf.Exp(BDArmorySettings.SF_REPULSOR_STRENGTH * (targetAlt - pointAltitude) / targetAlt - (float)vessel.verticalSpeed / targetAlt), 0f, 5f * BDArmorySettings.SF_REPULSOR_STRENGTH); // Decaying exponential balanced at the target altitude with velocity damping.
                                    float repulsorForce = vesselMass * factor / repulsors.Count; // Spread the force between the repulsors.
                                    if (float.IsNaN(factor) || float.IsInfinity(factor)) // This should only happen if targetAlt is 0, which should never happen.
                                        Debug.LogWarning($"[BDArmory.Spacehacks]: Repulsor Force is NaN or Infinity. TargetAlt: {targetAlt}, point Alt: {pointAltitude}, VesselMass: {vesselMass}");
                                    else
                                        repulsor.Current.part.Rigidbody.AddForce(-grav * repulsorForce, ForceMode.Force);
                                }
                        }
                    }
                }
            }
        }

        public static void AddSpaceFrictionToAllValidVessels()
        {
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (VesselModuleRegistry.GetMissileFire(vessel, true) != null && vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>() == null)
                {
                    vessel.rootPart.AddModule("ModuleSpaceFriction");
                }
            }
        }
    }
}
