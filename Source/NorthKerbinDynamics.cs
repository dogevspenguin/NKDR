using UnityEngine;
using BDArmory.Misc;
using BDArmory.FX;
using BDArmory.Modules;

namespace NuclearExplosive
{
    internal class NuclearExplosion : PartModule
    {
        //NSI is the creator of the code
        public float blastRadius;
        public float blastPower;
        public float blastTemperature;
        public bool Missile;
        public float bullets;
        public Part explosivePart;

        [KSPField(isPersistant = false)]
        public string explSpacePath = "NKDR/effects/Explosion_340_Space"; //Changed paths to NKDR no one will have NKD installed if this is installed

        [KSPField(isPersistant = false)]
        public string explAirPath = "NKDR/effects/Explosion_340_Airburst";

        [KSPField(isPersistant = false)]
        public string explGroundPath = "NKDR/effects/Explosion_340";

        [KSPField(isPersistant = false)]
        public string explWaterPath = "NKDR/effects/Explosion_340";

        [KSPField(isPersistant = false)]
        public string explSoundPath = "NKDR/sounds/explosion_MOAB";

        [KSPAction("Explode")]
        public void DetonateAG(KSPActionParam param)
        {
            Explode();
        }

        bool hasExploded = false;

        MissileLauncher weapon;
        public override void OnInitialize()
        {
            weapon = GetComponent<MissileLauncher>();
        }

        public override void OnStart(PartModule.StartState state)
        {
            part.OnJustAboutToBeDestroyed += new Callback(Explode);
        }

        public void Explode()
        {
            weapon.vessel.GetHeightFromTerrain();

            Vector3 position = transform.position;
            Quaternion rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            Vector3 direction = transform.up;

            blastRadius = weapon.blastRadius;
            blastPower = weapon.blastPower;
            blastTemperature = weapon.blastHeat;


            if (!hasExploded && weapon.vessel.heightFromTerrain >= 72000 && weapon.TimeFired >= weapon.dropTime && weapon.blastRadius >= 1000)
            {
                hasExploded = true;

                if (part != null) part.temperature = part.maxTemp + 100;
                ExplosionFx.CreateExplosion(position, blastPower, explSpacePath, explSoundPath, Missile = true, bullets = 0, explosivePart = null, direction = default(Vector3));
                Debug.Log("Space explosion confirmed");
            }
            else
            {
                if (!hasExploded && weapon.vessel.heightFromTerrain <= 50000 && weapon.vessel.heightFromTerrain >= 501 && weapon.TimeFired >= weapon.dropTime && weapon.blastRadius >= 1000)
                {
                    hasExploded = true;

                    if (part != null) part.temperature = part.maxTemp + 100;

                    GameObject source = new GameObject();
                    source.SetActive(true);
                    source.transform.position = position;
                    source.transform.rotation = rotation;
                    source.transform.up = direction;
                    ExplosionFx.CreateExplosion(position, blastPower, explAirPath, explSoundPath, Missile = true, bullets = 0, explosivePart = null, direction = default(Vector3));
                    Debug.Log("Air explosion confirmed");
                }
                else
                {
                    if (!hasExploded && weapon.vessel.heightFromTerrain <= 60000 && weapon.vessel.heightFromTerrain >= 501 && weapon.TimeFired >= weapon.dropTime && weapon.blastRadius <= 1000)
                    {
                        hasExploded = true;

                        if (part != null) part.temperature = part.maxTemp + 100;

                        GameObject csource = new GameObject();
                        csource.SetActive(true);
                        csource.transform.position = position;
                        csource.transform.rotation = rotation;
                        csource.transform.up = direction;
                        ExplosionFx.CreateExplosion(position, blastPower, explAirPath, explSoundPath, Missile = true, bullets = 0, explosivePart = null, direction = default(Vector3));
                        Debug.Log("Air explosion confirmed");
                    }
                    else
                    {
                        if (!hasExploded && weapon.TimeFired >= weapon.dropTime && weapon.vessel.heightFromTerrain <= 450 && weapon.blastRadius >= 1000)
                        {
                            hasExploded = true;

                            if (part != null) part.temperature = part.maxTemp + 100;

                            GameObject source = new GameObject();
                            source.SetActive(true);
                            source.transform.position = position;
                            source.transform.rotation = rotation;
                            source.transform.up = direction;
                            ExplosionFx.CreateExplosion(position, blastPower, explGroundPath, explSoundPath, Missile = true, bullets = 0, explosivePart = null, direction = default(Vector3));
                            Debug.Log("Ground explosion confirmed");
                        }
                        else
                        {
                            if (!hasExploded && weapon.TimeFired >= weapon.dropTime && weapon.vessel.heightFromTerrain <= 250 && weapon.blastRadius <= 1000)
                            {
                                hasExploded = true;

                                if (part != null) part.temperature = part.maxTemp + 100;

                                GameObject csource = new GameObject();
                                csource.SetActive(true);
                                csource.transform.position = position;
                                csource.transform.rotation = rotation;
                                csource.transform.up = direction;
                                ExplosionFx.CreateExplosion(position, blastPower, explGroundPath, explSoundPath, Missile = true, bullets = 0, explosivePart = null, direction = default(Vector3));
                                Debug.Log("Ground explosion confirmed");
                            }
                            else
                            {
                                if (!hasExploded && weapon.TimeFired >= weapon.dropTime && weapon.vessel.altitude <= 0)
                                {
                                    hasExploded = true;

                                    if (part != null) part.temperature = part.maxTemp + 100;

                                    GameObject csource = new GameObject();
                                    csource.SetActive(true);
                                    csource.transform.position = position;
                                    csource.transform.rotation = rotation;
                                    csource.transform.up = direction;
                                    ExplosionFx.CreateExplosion(position, blastPower, explGroundPath, explSoundPath, Missile = true, bullets = 0, explosivePart = null, direction = default(Vector3));
                                    Debug.Log("Underwater explosion confirmed");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
