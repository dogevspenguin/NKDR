using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Utils;
using BDArmory.Weapons;
using System.Collections;
using BDArmory.Settings;

namespace BDArmory.FX
{
    class FXEmitter : MonoBehaviour
    {
        public static Dictionary<string, ObjectPool> FXPools = new Dictionary<string, ObjectPool>();
        public KSPParticleEmitter[] pEmitters { get; set; }
        public float StartTime { get; set; }
        public AudioClip ExSound { get; set; }
        public AudioSource audioSource { get; set; }
        public string SoundPath { get; set; }
        private float Power { get; set; }
        private float emitTime { get; set; }
        private float maxTime { get; set; }
        private bool overrideLifeTime { get; set; }
        public Vector3 Position { get { return _position; } set { _position = value; transform.position = _position; } }
        Vector3 _position;
        public Vector3 Direction { get; set; }
        public float TimeIndex => Time.time - StartTime;

        private bool disabled = true;
        public static string defaultModelPath = "BDArmory/Models/explosion/explosion";
        public static string defaultSoundPath = "BDArmory/Sounds/explode1";
        private float particlesMaxEnergy;
        private float maxEnergy;
        private void OnEnable()
        {
            StartTime = Time.time;
            disabled = false;

            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.maxSize *= Power;
                    pe.maxParticleSize *= Power;
                    pe.minSize *= Power;
                    if (maxTime > 0)
                    {
                        maxEnergy = pe.maxEnergy;
                        pe.maxEnergy = maxTime;
                        pe.minEnergy = maxTime * .66f;
                    }
                    if (pe.maxEnergy > particlesMaxEnergy)
                        particlesMaxEnergy = pe.maxEnergy;
                    pe.emit = true;
                    var emission = pe.ps.emission;
                    emission.enabled = true;
                    EffectBehaviour.AddParticleEmitter(pe);
                }
            if (!string.IsNullOrEmpty(SoundPath))
            {
                audioSource = gameObject.GetComponent<AudioSource>();
                if (ExSound == null)
                {
                    ExSound = SoundUtils.GetAudioClip(SoundPath);

                    if (ExSound == null)
                    {
                        Debug.LogError("[BDArmory.FXEmitter]: " + ExSound + " was not found, using the default sound instead. Please fix your model.");
                        ExSound = SoundUtils.GetAudioClip(ModuleWeapon.defaultExplSoundPath);
                    }
                }
                audioSource.PlayOneShot(ExSound); //get distance to active vessel and add a delay?
                //StartCoroutine(DelayBlastSFX(Vector3.Distance(Position, FlightGlobals.ActiveVessel.CoM) / 343f));
            }
        }

        void OnDisable()
        {
            foreach (var pe in pEmitters)
            {
                if (pe != null)
                {
                    pe.maxSize /= Power;
                    pe.maxParticleSize /= Power;
                    pe.minSize /= Power;
                    if (maxTime > 0)
                    {
                        pe.maxEnergy = maxEnergy;
                        pe.minEnergy = maxEnergy * .66f;
                    }
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
            }
        }

        public void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (!disabled && TimeIndex > emitTime && pEmitters != null)
            {
                if (!overrideLifeTime)
                {
                    foreach (var pe in pEmitters)
                    {
                        if (pe == null) continue;
                        pe.emit = false;
                    }
                }
                disabled = true;
            }
        }

        public void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

            if (UI.BDArmorySetup.GameIsPaused)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                return;
            }

            if (BDKrakensbane.IsActive)
            {
                Position -= BDKrakensbane.FloatingOriginOffsetNonKrakensbane;
            }

            if ((disabled || overrideLifeTime) && TimeIndex > particlesMaxEnergy)
            {
                gameObject.SetActive(false);
                return;
            }
            if (UI.BDArmorySetup.GameIsPaused)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
                return;
            }
        }
        IEnumerator DelayBlastSFX(float delay)
        {
            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }
            audioSource.PlayOneShot(ExSound);
        }

        static void CreateObjectPool(string ModelPath, string soundPath)
        {
            var key = ModelPath + soundPath;
            if (!FXPools.ContainsKey(key) || FXPools[key] == null)
            {
                var FXTemplate = GameDatabase.Instance.GetModel(ModelPath);
                if (FXTemplate == null)
                {
                    Debug.LogError("[BDArmory.FXBase]: " + ModelPath + " was not found, using the default model instead. Please fix your model.");
                    FXTemplate = GameDatabase.Instance.GetModel(defaultModelPath);
                }
                var eFx = FXTemplate.AddComponent<FXEmitter>();
                if (!string.IsNullOrEmpty(soundPath))
                {
                    eFx.audioSource = FXTemplate.AddComponent<AudioSource>();
                    eFx.audioSource.minDistance = 200;
                    eFx.audioSource.maxDistance = 5500;
                    eFx.audioSource.spatialBlend = 1;
                }
                FXTemplate.SetActive(false);
                FXPools[key] = ObjectPool.CreateObjectPool(FXTemplate, 10, true, true, 0f, false);
            }
        }

        public static FXEmitter CreateFX(Vector3 position, float scale, string ModelPath, string soundPath, float time = 0.3f, float lifeTime = -1, Vector3 direction = default(Vector3), bool scaleEmitter = false, bool fixedLifetime = false)
        {
            CreateObjectPool(ModelPath, soundPath);

            Quaternion rotation;
            if (direction == default(Vector3))
            {
                rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            }
            else
            {
                rotation = Quaternion.LookRotation(direction);
            }

            GameObject newFX = FXPools[ModelPath + soundPath].GetPooledObject();
            newFX.transform.SetPositionAndRotation(position, rotation);
            if (scaleEmitter)
            {
                newFX.transform.localScale = Vector3.one;
                newFX.transform.localScale *= scale;
            }
            //Debug.Log("[FXEmitter] start scale: " + newFX.transform.localScale);
            FXEmitter eFx = newFX.GetComponent<FXEmitter>();

            eFx.Position = position;
            eFx.Power = scale;
            eFx.emitTime = time;
            eFx.maxTime = lifeTime;
            eFx.overrideLifeTime = fixedLifetime;
            eFx.pEmitters = newFX.GetComponentsInChildren<KSPParticleEmitter>();
            if (!string.IsNullOrEmpty(soundPath))
            {
                eFx.audioSource = newFX.GetComponent<AudioSource>();
                if (scale > 3)
                {
                    eFx.audioSource.minDistance = 4f;
                    eFx.audioSource.maxDistance = 3000;
                    eFx.audioSource.priority = 9999;
                }
                eFx.SoundPath = soundPath;
            }
            newFX.SetActive(true);
            return eFx;
        }

        public static void DisableAllFX()
        {
            if (FXPools != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FXEmitter]: Setting {FXPools.Values.Where(pool => pool != null && pool.pool != null).Sum(pool => pool.pool.Count(fx => fx != null && fx.activeInHierarchy))} FXEmitter FX inactive.");
                foreach (var pool in FXPools.Values)
                {
                    if (pool == null || pool.pool == null) continue;
                    foreach (var fx in pool.pool)
                    {
                        if (fx == null) continue;
                        fx.SetActive(false);
                    }
                }
            }
        }
    }
}
