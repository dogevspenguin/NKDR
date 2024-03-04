﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Ammo;
using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.CounterMeasure;
using BDArmory.Damage;
using BDArmory.Extensions;
using BDArmory.FX;
using BDArmory.Modules;
using BDArmory.Radar;
using BDArmory.Settings;
using BDArmory.Targeting;
using BDArmory.Utils;
using BDArmory.WeaponMounts;

namespace BDArmory.GameModes
{
    class BattleDamageHandler
    {
        public static void CheckDamageFX(Part part, float caliber, float penetrationFactor, bool explosivedamage, bool incendiary, string attacker, RaycastHit hitLoc, bool firsthit = true, bool cockpitPen = false)
        {      
            if (!BDArmorySettings.BATTLEDAMAGE || BDArmorySettings.PAINTBALL_MODE) return;
            if (penetrationFactor <= 0) penetrationFactor = 0.01f;
            if (BDArmorySettings.RUNWAY_PROJECT && BDArmorySettings.ZOMBIE_MODE)
            //if (BDArmorySettings.RUNWAY_PROJECT && BDArmorySettings.RUNWAY_PROJECT_ROUND == -1)
            {
                if (!BDArmorySettings.ALLOW_ZOMBIE_BD)
                {
                    if (part.vessel.rootPart != null)
                    {
                        if (part != part.vessel.rootPart) return;
                    }
                }
            }
            if (ProjectileUtils.IsIgnoredPart(part)) return; // Ignore ignored parts.

            double damageChance = Mathf.Clamp((BDArmorySettings.BD_DAMAGE_CHANCE * ((1 - part.GetDamagePercentage()) * 10) * (penetrationFactor / 2)), 0, 100); //more heavily damaged parts more likely to take battledamage

            if (BDArmorySettings.BD_TANKS)
            {
                if (part.HasFuel())
                {
                    var alreadyburning = part.GetComponentInChildren<FireFX>();
                    var rubbertank = part.FindModuleImplementing<ModuleSelfSealingTank>();
                    if (rubbertank != null)
                    {
                        if (rubbertank.SSTank && part.GetDamagePercentage() > 0.5f)
                            return;
                    }
                    //Debug.Log("[BDHandler] Hit on fueltank. SST = " + rubbertank.SSTank + "; inerting = " + rubbertank.InertTank);
                    if (penetrationFactor > 1.2)
                    {
                        if (alreadyburning != null)
                        {
                            if (rubbertank == null || !rubbertank.InertTank) BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker);
                        }
                        else
                        {
                            BulletHitFX.AttachLeak(hitLoc, part, caliber, explosivedamage, incendiary, attacker, rubbertank != null ? rubbertank.InertTank : false);
                        }
                    }
                }
            }
            if (BDArmorySettings.BD_FIRES_ENABLED)
            {
                if (part.isBattery() && part.GetDamagePercentage() < 0.95f)
                {
                    var alreadyburning = part.GetComponentInChildren<FireFX>();
                    if (alreadyburning == null)
                    {
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (explosivedamage)
                        {
                            Diceroll *= 0.33;
                        }
                        if (incendiary)
                        {
                            Diceroll *= 0.66;
                        }
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Battery Dice Roll: " + Diceroll);
                        if (Diceroll <= BDArmorySettings.BD_DAMAGE_CHANCE)
                        {
                            BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker);
                        }
                    }
                }
            }
            var Armor = part.FindModuleImplementing<HitpointTracker>();
            if (Armor != null)
            {
                if (Armor.ignitionTemp > 0) //wooden parts can potentially catch fire
                {
                    if (incendiary)
                    {
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Wood part Dice Roll: " + Diceroll);
                        if (Diceroll <= BDArmorySettings.BD_DAMAGE_CHANCE)
                        {
                            BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker, 90);
                        }
                    }
                }
            }
            //AmmoBins
            if (BDArmorySettings.BD_AMMOBINS && part.GetDamagePercentage() < 0.95f) //explosions have penetration of 0.5, should stop explosions phasing though parts from detonating ammo
            {
                var ammo = part.FindModuleImplementing<ModuleCASE>();
                if (ammo != null)
                {
                    ammo.SourceVessel = attacker; //moving this here so shots that destroy ammoboxes outright still report attacker if 'Ammo Explodes When Destroyed' is enabled
                    if (penetrationFactor > 1.2)
                    {
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (incendiary)
                        {
                            Diceroll *= 0.66;
                        }
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Ammo TAC DiceRoll: " + Diceroll + "; needs: " + damageChance);
                        if (Diceroll <= (damageChance) && part.GetDamagePercentage() < 0.95f)
                        {
                            ammo.DetonateIfPossible();
                        }
                    }
                    if (!ammo.hasDetonated) //hit didn't destroy box
                    {
                        ammo.SourceVessel = ammo.vessel.GetName();
                    }
                }
            }
            //Propulsion Damage
            if (BDArmorySettings.BD_PROPULSION)
            {
                BattleDamageTracker tracker = part.gameObject.AddOrGetComponent<BattleDamageTracker>();
                tracker.Part = part;
                if (part.isEngine() && part.GetDamagePercentage() < 0.95f) //first hit's free
                {
                    foreach (var engine in part.GetComponentsInChildren<ModuleEngines>())
                    {
                        if (engine.thrustPercentage > BDArmorySettings.BD_PROP_FLOOR) //engines take thrust damage per hit
                        {
                            //AP does bonus damage
                            engine.thrustPercentage -= ((((tracker.oldDamagePercent - part.GetDamagePercentage())) * (penetrationFactor / 2)) * BDArmorySettings.BD_PROP_DAM_RATE) * 10; //convert from damagepercent to thrustpercent
                            //use difference in old Hp and current, not just current, else it doesn't matter if its a heavy hit or chipped paint, thrust reduction is the same
                            engine.thrustPercentage = Mathf.Clamp(engine.thrustPercentage, BDArmorySettings.BD_PROP_FLOOR, 100); //even heavily damaged engines will still put out something
                            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: engine thrust: " + engine.thrustPercentage);
                            engine.PlayFlameoutFX(true);
                            tracker.oldDamagePercent = part.GetDamagePercentage();
                            /*
                            if (BDArmorySettings.BD_BALANCED_THRUST && !isSRB) //need to poke this more later, not working properly
                            {
                                using (List<Part>.Enumerator pSym = part.vessel.Parts.GetEnumerator())
                                    while (pSym.MoveNext())
                                    {
                                        if (pSym.Current == null) continue;
                                        if (pSym.Current != part)
                                        {
                                            if (pSym.Current.isSymmetryCounterPart(part))
                                            {
                                                foreach (var SymEngine in pSym.Current.GetComponentsInChildren<ModuleEngines>())
                                                {
                                                    SymEngine.thrustPercentage = engine.thrustPercentage;
                                                }
                                            }
                                        }
                                    }
                            }
                            */
                        }
                        if (part.GetDamagePercentage() < 0.75f || (part.GetDamagePercentage() < 0.82f && penetrationFactor > 2))
                        {
                            var leak = part.GetComponentInChildren<FuelLeakFX>();
                            if (leak == null && !tracker.isSRB) //engine isn't a srb
                            {
                                BulletHitFX.AttachLeak(hitLoc, part, caliber, explosivedamage, incendiary, attacker, false);
                            }
                        }
                        if (part.GetDamagePercentage() < 0.50f || (part.GetDamagePercentage() < 0.625f && penetrationFactor > 2))
                        {
                            var alreadyburning = part.GetComponentInChildren<FireFX>();
                            if (tracker.isSRB) //srbs are steel tubes full of explosives; treat differently
                            {
                                if ((explosivedamage || incendiary) && tracker.SRBFuelled)
                                {
                                    BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker);
                                }
                            }
                            else
                            {
                                if (alreadyburning == null)
                                {
                                    BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker, -1, 1);
                                }
                            }
                        }
                        if (part.GetDamagePercentage() < (BDArmorySettings.BD_PROP_FLAMEOUT / 100))
                        {
                            if (engine.EngineIgnited)
                            {
                                if (tracker.isSRB && tracker.SRBFuelled) //SRB is lit, and casing integrity fails due to damage; boom
                                {
                                    if (tracker.SRBFuelled)
                                    {
                                        var Rupture = part.GetComponent<ModuleCASE>();
                                        if (Rupture == null) Rupture = (ModuleCASE)part.AddModule("ModuleCASE");
                                        Rupture.CASELevel = 0;
                                        Rupture.DetonateIfPossible();
                                    }
                                }
                                else
                                {
                                    engine.PlayFlameoutFX(true);
                                    engine.Shutdown(); //kill a badly damaged engine and don't allow restart
                                    engine.allowRestart = false;
                                }
                            }
                        }
                    }
                }
                if (BDArmorySettings.BD_INTAKES) //intake damage
                {
                    var intake = part.FindModuleImplementing<ModuleResourceIntake>();
                    //if (part.isAirIntake(intake)) instead? or use vesselregistry
                    if (intake != null)
                    {
                        if (tracker.origIntakeArea < 0)
                        {
                            tracker.origIntakeArea = intake.area;
                        }
                        float HEBonus = 0.7f;
                        if (explosivedamage)
                        {
                            HEBonus = 1.4f;
                        }

                        if (incendiary)
                        {
                            HEBonus = 1.1f;
                        }
                        intake.intakeSpeed *= (1 - ((tracker.oldDamagePercent - part.GetDamagePercentage()) * HEBonus) * BDArmorySettings.BD_PROP_DAM_RATE); //HE does bonus damage
                        intake.intakeSpeed = Mathf.Clamp((float)intake.intakeSpeed, 0, 99999);

                        intake.area -= (tracker.origIntakeArea * (((tracker.oldDamagePercent - part.GetDamagePercentage()) * HEBonus) * BDArmorySettings.BD_PROP_DAM_RATE)); //HE does bonus damage
                        intake.area = Mathf.Clamp((float)intake.area, ((float)tracker.origIntakeArea / 4), 99999); //even shredded intake ducting will still get some air to engines
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Intake damage: Orig Area: " + tracker.origIntakeArea + "; Current Area: " + intake.area + "; Intake Speed: " + intake.intakeSpeed + "; intake damage: " + (1 - ((((tracker.oldDamagePercent - part.GetDamagePercentage())) * HEBonus) / BDArmorySettings.BD_PROP_DAM_RATE)));
                    }
                }
                if (BDArmorySettings.BD_GIMBALS) //engine gimbal damage
                {
                    var gimbal = part.FindModuleImplementing<ModuleGimbal>();
                    if (gimbal != null)
                    {
                        double HEBonus = 1;
                        if (explosivedamage)
                        {
                            HEBonus = 1.4;
                        }
                        if (incendiary)
                        {
                            HEBonus = 1.25;
                        }
                        //gimbal.gimbalRange *= (1 - (((1 - part.GetDamagePercentatge()) * HEBonus) / BDArmorySettings.BD_PROP_DAM_RATE)); //HE does bonus damage
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Gimbal DiceRoll: " + Diceroll);
                        if (Diceroll <= (BDArmorySettings.BD_DAMAGE_CHANCE * HEBonus))
                        {
                            gimbal.enabled = false;
                            gimbal.gimbalRange = 0;
                            if (incendiary)
                            {
                                BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker, 20);
                            }
                        }
                    }
                }
            }
            //Aero Damage
            if (BDArmorySettings.BD_AEROPARTS && firsthit)
            {
                float HEBonus = 1;
                if (explosivedamage)
                {
                    HEBonus = 2; //explosive rounds blow bigger holes in wings
                }                
                HEBonus *= Mathf.Clamp(penetrationFactor, 0.5f, 1.5f); 
                float liftDam = ((caliber / 20000) * HEBonus) * BDArmorySettings.BD_LIFT_LOSS_RATE;
                if (part.GetComponent<ModuleLiftingSurface>() != null)
                {
                    ModuleLiftingSurface wing;
                    wing = part.GetComponent<ModuleLiftingSurface>();
                    //2x4m wing board = 2 Lift, 0.25 Lift/m2. 20mm round = 20*20=400/20000= 0.02 Lift reduced per hit, 100 rounds to reduce lift to 0. mind you, it only takes ~15 rounds to destroy the wing...
                    if (wing.deflectionLiftCoeff > ((part.mass * 5) + liftDam)) //stock mass/lift ratio is 10; 0.2t wing has 2.0 lift; clamp lift lost at half
                    {
                        wing.deflectionLiftCoeff -= liftDam;                        
                        if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: " + part.name + "hit by " + caliber + " round, penFactor " + penetrationFactor + "; took lift damage: " + liftDam + ", current lift: " + wing.deflectionLiftCoeff);
                    }
                }
                if (BDArmorySettings.BD_CTRL_SRF && firsthit)
                {
                    if (part.GetComponent<ModuleControlSurface>() != null && part.GetDamagePercentage() > 0.125f)
                    //if ( part.isControlSurface(aileron))?
                    {
                        ModuleControlSurface aileron;
                        aileron = part.GetComponent<ModuleControlSurface>();
                        if (aileron.deflectionLiftCoeff > ((part.mass * 2.5f) + liftDam)) //stock ctrl surface mass/lift ratio is 5
                        {
                            aileron.deflectionLiftCoeff -= liftDam;
                        }
                        int Diceroll = (int)UnityEngine.Random.Range(0f, 100f);
                        if (explosivedamage)
                        {
                            HEBonus = 1.2f;
                        }
                        if (incendiary)
                        {
                            HEBonus = 1.1f;
                        }
                        if (Diceroll <= (BDArmorySettings.BD_DAMAGE_CHANCE * HEBonus))
                        {
                            if (aileron.actuatorSpeed > 3)
                            {
                                aileron.actuatorSpeed /= 2;
                                aileron.authorityLimiter /= 2;
                                aileron.ctrlSurfaceRange /= 2;
                                if (Diceroll <= ((BDArmorySettings.BD_DAMAGE_CHANCE * HEBonus) / 2))
                                {
                                    BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker, 10);
                                }
                            }
                            else
                            {
                                aileron.actuatorSpeed = 0;
                                aileron.authorityLimiter = 0;
                                aileron.ctrlSurfaceRange = 0;
                            }
                        }
                    }
                }
            }
            //Subsystems
            if (BDArmorySettings.BD_SUBSYSTEMS && firsthit)
            {
                double Diceroll = UnityEngine.Random.Range(0, 100);
                if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Subsystem DiceRoll: " + Diceroll + "; needs: " + damageChance);
                if (Diceroll <= (damageChance) && part.GetDamagePercentage() < 0.95f)
                {
                    if (part.GetComponent<ModuleReactionWheel>() != null) //should have this be separate dice rolls, else a part with more than one of these will lose them all
                    {
                        ModuleReactionWheel SAS; //could have torque reduced per hit
                        SAS = part.GetComponent<ModuleReactionWheel>();
                        part.RemoveModule(SAS);
                    }
                    if (part.GetComponent<ModuleRadar>() != null)
                    {
                        ModuleRadar radar; //would need to mod detection curve to degrade performance on hit
                        radar = part.GetComponent<ModuleRadar>();
                        part.RemoveModule(radar);
                    }
                    if (part.GetComponent<ModuleAlternator>() != null)
                    {
                        ModuleAlternator alt; //damaging alternator is probably just petty. Could reduce output per hit
                        alt = part.GetComponent<ModuleAlternator>();
                        part.RemoveModule(alt);
                    }
                    if (part.GetComponent<ModuleAnimateGeneric>() != null)
                    {
                        ModuleAnimateGeneric anim;
                        anim = part.GetComponent<ModuleAnimateGeneric>(); // could reduce anim speed, open percent per hit
                        part.RemoveModule(anim);
                    }
                    if (part.GetComponent<ModuleDecouple>() != null)
                    {
                        ModuleDecouple stage;
                        stage = part.GetComponent<ModuleDecouple>(); //decouplers decouple
                        stage.Decouple();
                    }
                    if (part.GetComponent<ModuleECMJammer>() != null)
                    {
                        ModuleECMJammer ecm;
                        ecm = part.GetComponent<ModuleECMJammer>(); //could reduce ecm strngth/rcs modifier
                        part.RemoveModule(ecm);
                    }
                    if (part.GetComponent<ModuleGenerator>() != null)
                    {
                        ModuleGenerator gen;
                        gen = part.GetComponent<ModuleGenerator>();
                        part.RemoveModule(gen);
                    }
                    if (part.GetComponent<ModuleResourceConverter>() != null)
                    {
                        ModuleResourceConverter isru;
                        isru = part.GetComponent<ModuleResourceConverter>(); //could reduce efficiency, increase heat per hit
                        part.RemoveModule(isru);
                    }
                    if (part.GetComponent<ModuleTurret>() != null)
                    {
                        ModuleTurret turret;
                        turret = part.GetComponent<ModuleTurret>(); //could reduce traverse speed, range per hit
                        part.RemoveModule(turret);
                    }
                    if (part.GetComponent<ModuleTargetingCamera>() != null)
                    {
                        ModuleTargetingCamera cam;
                        cam = part.GetComponent<ModuleTargetingCamera>(); // gimbal range??
                        part.RemoveModule(cam);
                    }
                    if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log($"[BDArmory.BattleDamageHandler]: {part.name} on {part.vessel.vesselName} took subsystem damage");
                    if (Diceroll <= (damageChance / 2))
                    {
                        if (incendiary)
                        {
                            BulletHitFX.AttachFire(hitLoc.point, part, caliber, attacker, 20);
                        }
                    }
                }
            }
            //Command parts
            if (BDArmorySettings.BD_COCKPITS && penetrationFactor > 1.2f && part.GetDamagePercentage() < 0.9f && firsthit) //lets have this be triggered by penetrative damage, not blast splash
            {
                if (part.GetComponent<ModuleCommand>() != null)
                {
                    double ControlDiceRoll = UnityEngine.Random.Range(0, 100);
                    if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Command DiceRoll: " + ControlDiceRoll);
                    if (ControlDiceRoll <= (BDArmorySettings.BD_DAMAGE_CHANCE * 2))
                    {
                        using (List<Part>.Enumerator craftPart = part.vessel.parts.GetEnumerator())
                        {
                            using (var control = VesselModuleRegistry.GetModules<BDModulePilotAI>(part.vessel).GetEnumerator()) // FIXME should this be IBDAIControl?
                                while (control.MoveNext())
                                {
                                    if (control.Current == null) continue;
                                    control.Current.evasionThreshold += 5; //pilot jitteriness increases
                                    control.Current.maxSteer *= 0.9f;
                                    if (control.Current.steerDamping > 0.625f) //damage to controls
                                    {
                                        control.Current.steerDamping -= 0.125f;
                                    }
                                    if (control.Current.dynamicSteerDampingPitchFactor > 0.625f)
                                    {
                                        control.Current.dynamicSteerDampingPitchFactor -= 0.125f;
                                    }
                                    if (control.Current.dynamicSteerDampingRollFactor > 0.625f)
                                    {
                                        control.Current.dynamicSteerDampingRollFactor -= 0.125f;
                                    }
                                    if (control.Current.dynamicSteerDampingYawFactor > 0.625f)
                                    {
                                        control.Current.dynamicSteerDampingYawFactor -= 0.125f;
                                    }
                                }
                            //GuardRange reduction to sim canopy/sensor damage?
                            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: " + part.name + "took command damage");
                        }
                    }
                }
            }
            if (BDArmorySettings.BD_PILOT_KILLS)
            {
                bool canKill = true;
                var armorglass = part.FindModuleImplementing<ModuleSelfSealingTank>();
                if (armorglass != null)
                {
                    if (armorglass.armoredCockpit && !cockpitPen) //round stopped by internal cockpit armor
                    {
                        canKill = false;
                    }
                }
                if (canKill && part.protoModuleCrew.Count > 0 && penetrationFactor > 1.5f && part.GetDamagePercentage() < 0.95f && firsthit)
                {
                    float PilotTAC = Mathf.Clamp((BDArmorySettings.BD_DAMAGE_CHANCE / part.mass), 0.01f, 100); //larger cockpits = greater volume = less chance any hit will pass through a region of volume containing a pilot
                    float killchance = UnityEngine.Random.Range(0, 100);
                    if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.BattleDamageHandler]: Pilot TAC: " + PilotTAC + "; dice roll: " + killchance);
                    if (killchance <= PilotTAC) //add penetrationfactor threshold? hp threshold?
                    {
                        ProtoCrewMember crewMember = part.protoModuleCrew.FirstOrDefault(x => x != null);
                        if (crewMember != null)
                        {
                            crewMember.UnregisterExperienceTraits(part);
                            //crewMember.outDueToG = true; //implement temp KO to simulate wounding?
                            crewMember.Die();
                            if (part.IsKerbalEVA())
                            {
                                part.Die();
                            }
                            else
                            {
                                part.RemoveCrewmember(crewMember); // sadly, I wasn't able to get the K.I.A. portrait working
                            }
                            //Vessel.CrewWasModified(part.vessel);
                            //Debug.Log("[BDArmory.BattleDamageHandler]: " + crewMember.name + " was killed by damage to cabin!");
                            if (HighLogic.CurrentGame.Parameters.Difficulty.MissingCrewsRespawn)
                            {
                                crewMember.StartRespawnPeriod();
                            }
                            //ScreenMessages.PostScreenMessage(crewMember.name + " killed by damage to " + part.vessel.name + part.partName + ".", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                            ScreenMessages.PostScreenMessage("Cockpit snipe on " + part.vessel.GetName() + "! " + crewMember.name + " killed!", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                            BDACompetitionMode.Instance.OnVesselModified(part.vessel);

                        }
                    }
                }
            }

        }
    }
}
