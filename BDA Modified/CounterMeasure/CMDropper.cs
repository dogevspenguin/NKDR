using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UniLinq;
using UnityEngine;

using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;
using BDArmory.Extensions;

namespace BDArmory.CounterMeasure
{
    public class CMDropper : PartModule
    {
        public static ObjectPool flarePool;
        public static ObjectPool chaffPool;
        public static ObjectPool smokePool;
        public static ObjectPool decoyPool;
        public static ObjectPool bubblePool;

        public enum CountermeasureTypes
        {
            Flare = 1 << 0,
            Chaff = 1 << 1,
            Smoke = 1 << 2,
            Decoy = 1 << 3,
            Bubbles = 1 << 4
        }

        public CountermeasureTypes cmType = CountermeasureTypes.Flare;
        [KSPField] public string countermeasureType = "flare";

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_EjectVelocity"),//Eject Velocity
        UI_FloatRange(controlEnabled = true, scene = UI_Scene.Editor, minValue = 1f, maxValue = 200f, stepIncrement = 1f)]
        public float ejectVelocity = 30;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_FiringPriority"), // Selection Priority
        UI_FloatRange(controlEnabled = true, scene = UI_Scene.Editor, minValue = 0f, maxValue = 10f, stepIncrement = 1f)]
        public float priority = 0;
        public int Priority => (int)priority;

        [KSPField] public string ejectTransformName;
        Transform ejectTransform;

        [KSPField] public string effectsTransformName = string.Empty;
        Transform effectsTransform;

        AudioSource audioSource;
        AudioClip cmSound;
        AudioClip smokePoofSound;

        string resourceName;

        VesselChaffInfo vci;

        [KSPAction("#LOC_BDArmory_FireCountermeasure")]
        public void AGDropCM(KSPActionParam param)
        {
            DropCM();
        }

        [KSPEvent(guiActive = true, guiName = "#LOC_BDArmory_FireCountermeasure", active = true)]//Fire Countermeasure
        public void EventDropCM() => DropCM();
        public bool DropCM()
        {
            switch (cmType)
            {
                case CountermeasureTypes.Flare:
                    return DropFlare();

                case CountermeasureTypes.Chaff:
                    return DropChaff();

                case CountermeasureTypes.Smoke:
                    return PopSmoke();

                case CountermeasureTypes.Decoy:
                    return LaunchDecoy();

                case CountermeasureTypes.Bubbles:
                    return DropBubbles();
            }
            return false;
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                SetupCM();

                ejectTransform = part.FindModelTransform(ejectTransformName);

                if (effectsTransformName != string.Empty)
                {
                    effectsTransform = part.FindModelTransform(effectsTransformName);
                }

                part.force_activate();

                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 1;
                audioSource.maxDistance = 1000;
                audioSource.spatialBlend = 1;

                UpdateVolume();
                BDArmorySetup.OnVolumeChange += UpdateVolume;

                GameEvents.onVesselsUndocking.Add(OnVesselsUndocking);
            }
            else
            {
                SetupCMType();
                Fields["ejectVelocity"].guiActiveEditor = cmType != CountermeasureTypes.Smoke;
            }
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }

        void OnDestroy()
        {
            BDArmorySetup.OnVolumeChange -= UpdateVolume;
            GameEvents.onVesselsUndocking.Remove(OnVesselsUndocking);
        }

        void OnVesselsUndocking(Vessel v1, Vessel v2)
        {
            if (vessel != v1 && vessel != v2) return; // Not us.
            if (countermeasureType.ToLower() == "chaff" && !vessel.gameObject.GetComponent<VesselChaffInfo>())
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CMDropper]: {vessel.vesselName} didn't have VesselChaffInfo on undocking ({v1.vesselName} — {v2.vesselName})");
                SetupCM(); // Re-setup countermeasures at least one of the vessels would have lost the VesselModule when they docked.
            }
        }

        public override void OnUpdate()
        {
            if (audioSource)
            {
                if (vessel.isActiveVessel)
                {
                    audioSource.dopplerLevel = 0;
                }
                else
                {
                    audioSource.dopplerLevel = 1;
                }
            }
        }

        void FireParticleEffects()
        {
            if (!effectsTransform) return;
            using (IEnumerator<KSPParticleEmitter> pe = effectsTransform.gameObject.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator())
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(pe.Current);
                    pe.Current.Emit();
                }
        }

        PartResource GetCMResource()
        {
            using (IEnumerator<PartResource> res = part.Resources.GetEnumerator())
                while (res.MoveNext())
                {
                    if (res.Current == null) continue;
                    if (res.Current.resourceName == resourceName) return res.Current;
                }
            return null;
        }

        void SetupCMType()
        {
            countermeasureType = countermeasureType.ToLower();
            switch (countermeasureType)
            {
                case "flare":
                    cmType = CountermeasureTypes.Flare;
                    break;

                case "chaff":
                    cmType = CountermeasureTypes.Chaff;
                    break;

                case "smoke":
                    cmType = CountermeasureTypes.Smoke;
                    break;

                case "decoy":
                    cmType = CountermeasureTypes.Decoy;
                    break;

                case "bubble":
                    cmType = CountermeasureTypes.Bubbles;
                    break;
            }
        }

        void SetupCM()
        {
            countermeasureType = countermeasureType.ToLower();
            switch (countermeasureType)
            {
                case "flare":
                    cmType = CountermeasureTypes.Flare;
                    cmSound = SoundUtils.GetAudioClip("BDArmory/Sounds/flareSound");
                    if (!flarePool)
                    {
                        SetupFlarePool();
                    }
                    resourceName = "CMFlare";
                    break;

                case "chaff":
                    cmType = CountermeasureTypes.Chaff;
                    cmSound = SoundUtils.GetAudioClip("BDArmory/Sounds/smokeEject");
                    resourceName = "CMChaff";
                    vci = vessel.gameObject.GetComponent<VesselChaffInfo>();
                    if (!vci)
                    {
                        vci = vessel.gameObject.AddComponent<VesselChaffInfo>();
                    }
                    if (!chaffPool)
                    {
                        SetupChaffPool();
                    }
                    break;

                case "smoke":
                    cmType = CountermeasureTypes.Smoke;
                    cmSound = SoundUtils.GetAudioClip("BDArmory/Sounds/smokeEject");
                    smokePoofSound = SoundUtils.GetAudioClip("BDArmory/Sounds/smokePoof");
                    resourceName = "CMSmoke";
                    if (smokePool == null)
                    {
                        SetupSmokePool();
                    }
                    break;

                case "decoy":
                    cmType = CountermeasureTypes.Decoy;
                    cmSound = SoundUtils.GetAudioClip("BDArmory/Sounds/decoySound");
                    if (!decoyPool)
                    {
                        SetupDecoyPool();
                    }
                    resourceName = "CMDecoy";
                    break;

                case "bubble":
                    cmType = CountermeasureTypes.Bubbles;
                    cmSound = SoundUtils.GetAudioClip("BDArmory/Sounds/smokeEject");
                    resourceName = "CMBubbleCurtain";
                    if (!bubblePool)
                    {
                        SetupBubblePool();
                    }
                    break;
            }
        }

        bool DropFlare()
        {
            PartResource cmResource = GetCMResource();
            if (cmResource == null || !(cmResource.amount >= 1)) return false;
            cmResource.amount--;
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(cmSound);

            GameObject cm = flarePool.GetPooledObject();
            cm.transform.position = transform.position;
            CMFlare cmf = cm.GetComponent<CMFlare>();
            cmf.velocity = part.rb.velocity
                + BDKrakensbane.FrameVelocityV3f
                + (ejectVelocity * transform.up)
                + (UnityEngine.Random.Range(-3f, 3f) * transform.forward)
                + (UnityEngine.Random.Range(-3f, 3f) * transform.right);
            cmf.SetThermal(vessel);

            cm.SetActive(true);

            FireParticleEffects();
            return true;
        }

        bool DropChaff()
        {
            PartResource cmResource = GetCMResource();
            if (cmResource == null || !(cmResource.amount >= 1)) return false;
            cmResource.amount--;
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(cmSound);

            if (!vci)
            {
                vci = vessel.gameObject.AddComponent<VesselChaffInfo>();
            }
            vci.Chaff();

            GameObject cm = chaffPool.GetPooledObject();
            CMChaff chaff = cm.GetComponent<CMChaff>();
            chaff.Emit(ejectTransform.position, ejectVelocity * ejectTransform.forward + vessel.Velocity());

            FireParticleEffects();
            return true;
        }

        bool PopSmoke()
        {
            PartResource smokeResource = GetCMResource();
            if (smokeResource.amount >= 1)
            {
                smokeResource.amount--;
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(cmSound);

                StartCoroutine(SmokeRoutine());

                FireParticleEffects();
                return true;
            }
            return false;
        }

        IEnumerator SmokeRoutine()
        {
            yield return new WaitForSecondsFixed(0.2f);
            GameObject smokeCMObject = smokePool.GetPooledObject();
            CMSmoke smoke = smokeCMObject.GetComponent<CMSmoke>();
            smoke.velocity = part.rb.velocity + (ejectVelocity * transform.up) +
                             (UnityEngine.Random.Range(-3f, 3f) * transform.forward) +
                             (UnityEngine.Random.Range(-3f, 3f) * transform.right);
            smokeCMObject.SetActive(true);
            smokeCMObject.transform.position = ejectTransform.position + (10 * ejectTransform.forward);
            float longestLife = 0;
            using (IEnumerator<KSPParticleEmitter> emitter = smokeCMObject.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(emitter.Current);
                    emitter.Current.Emit();
                    if (emitter.Current.maxEnergy > longestLife) longestLife = emitter.Current.maxEnergy;
                }

            audioSource.PlayOneShot(smokePoofSound);
            yield return new WaitForSecondsFixed(longestLife);
            smokeCMObject.SetActive(false);
        }

        bool LaunchDecoy()
        {
            PartResource cmResource = GetCMResource();
            if (cmResource == null || !(cmResource.amount >= 1)) return false;
            cmResource.amount--;
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(cmSound);

            GameObject cm = decoyPool.GetPooledObject();
            cm.transform.position = transform.position;
            CMDecoy cmd = cm.GetComponent<CMDecoy>();
            cmd.velocity = part.rb.velocity
                + BDKrakensbane.FrameVelocityV3f
                + (ejectVelocity * transform.up)
                + (UnityEngine.Random.Range(-3f, 3f) * transform.forward)
                + (UnityEngine.Random.Range(-3f, 3f) * transform.right);
            cmd.SetAcoustics(vessel);

            cm.SetActive(true);

            FireParticleEffects();
            return true;
        }

        bool DropBubbles()
        {
            PartResource smokeResource = GetCMResource();
            if (smokeResource.amount >= 1)
            {
                smokeResource.amount--;
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(cmSound);

                StartCoroutine(BubbleRoutine());

                FireParticleEffects();
                return true;
            }
            return false;
        }

        IEnumerator BubbleRoutine()
        {
            yield return new WaitForSecondsFixed(0.2f);
            GameObject bubbleCMObject = decoyPool.GetPooledObject();
            CMBubble smoke = bubbleCMObject.GetComponent<CMBubble>();
            smoke.velocity = part.rb.velocity + (ejectVelocity * transform.up) +
                             (UnityEngine.Random.Range(-3f, 3f) * transform.forward) +
                             (UnityEngine.Random.Range(-3f, 3f) * transform.right);
            bubbleCMObject.SetActive(true);
            bubbleCMObject.transform.position = ejectTransform.position + (10 * ejectTransform.forward);
            float longestLife = 0;
            using (IEnumerator<KSPParticleEmitter> emitter = bubbleCMObject.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator())
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
                    EffectBehaviour.AddParticleEmitter(emitter.Current);
                    emitter.Current.Emit();
                    if (emitter.Current.maxEnergy > longestLife) longestLife = emitter.Current.maxEnergy;
                }

            audioSource.PlayOneShot(smokePoofSound);
            yield return new WaitForSecondsFixed(longestLife);
            bubbleCMObject.SetActive(false);
        }


        void SetupFlarePool()
        {
            GameObject cm = GameDatabase.Instance.GetModel("BDArmory/Models/CMFlare/model");
            cm.SetActive(false);
            cm.AddComponent<CMFlare>();
            flarePool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        void SetupSmokePool()
        {
            GameObject cm = GameDatabase.Instance.GetModel("BDArmory/Models/CMSmoke/cmSmokeModel");
            cm.SetActive(false);
            cm.AddComponent<CMSmoke>();
            smokePool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        void SetupChaffPool()
        {
            GameObject cm = GameDatabase.Instance.GetModel("BDArmory/Models/CMChaff/model");
            cm.SetActive(false);
            cm.AddComponent<CMChaff>();
            chaffPool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        void SetupDecoyPool()
        {
            GameObject cm = GameDatabase.Instance.GetModel("BDArmory/Models/CMDecoy/model");
            cm.SetActive(false);
            cm.AddComponent<CMDecoy>();
            decoyPool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        void SetupBubblePool()
        {
            GameObject cm = GameDatabase.Instance.GetModel("BDArmory/Models/CMBubble/cmSmokeModel");
            cm.SetActive(false);
            cm.AddComponent<CMBubble>();
            bubblePool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        public static void DisableAllCMs()
        {
            if (flarePool != null && flarePool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CMDropper]: Setting {flarePool.pool.Count(flare => flare != null & flare.activeInHierarchy)} flare CMs inactive.");
                foreach (var flare in flarePool.pool)
                {
                    if (flare == null) continue;
                    flare.SetActive(false);
                }
            }
            if (smokePool != null && smokePool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CMDropper]: Setting {smokePool.pool.Count(smoke => smoke != null & smoke.activeInHierarchy)} smoke CMs inactive.");
                foreach (var smoke in smokePool.pool)
                {
                    if (smoke == null) continue;
                    smoke.SetActive(false);
                }
            }
            if (chaffPool != null && chaffPool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CMDropper]: Setting {chaffPool.pool.Count(chaff => chaff != null & chaff.activeInHierarchy)} chaff CMs inactive.");
                foreach (var chaff in chaffPool.pool)
                {
                    if (chaff == null) continue;
                    chaff.SetActive(false);
                }
            }
            if (decoyPool != null && decoyPool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CMDropper]: Setting {decoyPool.pool.Count(decoy => decoy != null & decoy.activeInHierarchy)} decoy CMs inactive.");
                foreach (var decoy in decoyPool.pool)
                {
                    if (decoy == null) continue;
                    decoy.SetActive(false);
                }
            }
            if (bubblePool != null && bubblePool.pool != null)
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CMDropper]: Setting {bubblePool.pool.Count(bubble => bubble != null & bubble.activeInHierarchy)} bubble CMs inactive.");
                foreach (var bubble in bubblePool.pool)
                {
                    if (bubble == null) continue;
                    bubble.SetActive(false);
                }
            }
        }

        // RMB info in editor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append($"Countermeasure: {countermeasureType}");
            output.Append(Environment.NewLine);

            return output.ToString();
        }
    }
}
