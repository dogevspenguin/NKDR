using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Control;
using BDArmory.Radar;
using BDArmory.Settings;
using BDArmory.Targeting;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Radar
{
    public class RadarWarningReceiver : PartModule
    {
        public delegate void RadarPing(Vessel v, Vector3 source, RWRThreatTypes type, float persistTime);

        public static event RadarPing OnRadarPing;

        public delegate void MissileLaunchWarning(Vector3 source, Vector3 direction, bool radar);

        public static event MissileLaunchWarning OnMissileLaunch;

        public enum RWRThreatTypes
        {
            None = -1,
            SAM = 0,
            Fighter = 1,
            AWACS = 2,
            MissileLaunch = 3,
            MissileLock = 4,
            Detection = 5,
            Sonar = 6,
            Torpedo = 7,
            TorpedoLock = 8,
            Jamming = 9
        }

        string[] iconLabels = new string[] { "S", "F", "A", "M", "M", "D", "So", "T", "T", "J" };

        public MissileFire weaponManager;

        // This field may not need to be persistent.  It was combining display with active RWR status.
        [KSPField(isPersistant = true)] public bool rwrEnabled;
        //for if the RWR should detect everything, or only be able to detect radar sources
        [KSPField(isPersistant = true)] public bool omniDetection = true;

        // This field was added to separate RWR active status from the display of the RWR.  the RWR should be running all the time...
        public bool displayRWR = false;
        internal static bool resizingWindow = false;

        public Rect RWRresizeRect = new Rect(
            BDArmorySetup.WindowRectRwr.width - (16 * BDArmorySettings.RWR_WINDOW_SCALE),
            BDArmorySetup.WindowRectRwr.height - (16 * BDArmorySettings.RWR_WINDOW_SCALE),
            (16 * BDArmorySettings.RWR_WINDOW_SCALE),
            (16 * BDArmorySettings.RWR_WINDOW_SCALE));

        public static Texture2D rwrDiamondTexture =
            GameDatabase.Instance.GetTexture(BDArmorySetup.textureDir + "rwrDiamond", false);

        public static Texture2D rwrMissileTexture =
            GameDatabase.Instance.GetTexture(BDArmorySetup.textureDir + "rwrMissileIcon", false);

        public static AudioClip radarPingSound;
        public static AudioClip missileLockSound;
        public static AudioClip missileLaunchSound;
        public static AudioClip sonarPing;
        public static AudioClip torpedoPing;
        private float torpedoPingPitch;
        private float audioSourceRepeatDelay;
        private const float audioSourceRepeatDelayTime = 0.5f;

        //float lastTimePinged = 0;
        const float minPingInterval = 0.12f;
        const float pingPersistTime = 1;

        const int dataCount = 12;

        internal float rwrDisplayRange = BDArmorySettings.MAX_ACTIVE_RADAR_RANGE;
        internal static float RwrSize = 256;
        internal static float BorderSize = 10;
        internal static float HeaderSize = 15;

        public TargetSignatureData[] pingsData;
        public Vector3[] pingWorldPositions;
        List<TargetSignatureData> launchWarnings;

        Transform rt;

        Transform referenceTransform
        {
            get
            {
                if (!rt)
                {
                    rt = new GameObject().transform;
                    rt.parent = part.transform;
                    rt.localPosition = Vector3.zero;
                }
                return rt;
            }
        }

        internal static Rect RwrDisplayRect = new Rect(0, 0, RwrSize * BDArmorySettings.RWR_WINDOW_SCALE, RwrSize * BDArmorySettings.RWR_WINDOW_SCALE);

        GUIStyle rwrIconLabelStyle;

        AudioSource audioSource;
        public static bool WindowRectRWRInitialized;

        public override void OnAwake()
        {
            radarPingSound = SoundUtils.GetAudioClip("BDArmory/Sounds/rwrPing");
            missileLockSound = SoundUtils.GetAudioClip("BDArmory/Sounds/rwrMissileLock");
            missileLaunchSound = SoundUtils.GetAudioClip("BDArmory/Sounds/mLaunchWarning");
            sonarPing = SoundUtils.GetAudioClip("BDArmory/Sounds/rwr_sonarping");
            torpedoPing = SoundUtils.GetAudioClip("BDArmory/Sounds/rwr_torpedoping");
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                pingsData = new TargetSignatureData[dataCount];
                pingWorldPositions = new Vector3[dataCount];
                TargetSignatureData.ResetTSDArray(ref pingsData);
                launchWarnings = new List<TargetSignatureData>();

                rwrIconLabelStyle = new GUIStyle();
                rwrIconLabelStyle.alignment = TextAnchor.MiddleCenter;
                rwrIconLabelStyle.normal.textColor = Color.green;
                rwrIconLabelStyle.fontSize = 12;
                rwrIconLabelStyle.border = new RectOffset(0, 0, 0, 0);
                rwrIconLabelStyle.clipping = TextClipping.Overflow;
                rwrIconLabelStyle.wordWrap = false;
                rwrIconLabelStyle.fontStyle = FontStyle.Bold;

                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 500;
                audioSource.maxDistance = 1000;
                audioSource.spatialBlend = 1;
                audioSource.dopplerLevel = 0;
                audioSource.loop = false;

                UpdateVolume();
                BDArmorySetup.OnVolumeChange += UpdateVolume;

                if (!WindowRectRWRInitialized)
                {
                    BDArmorySetup.WindowRectRwr = new Rect(BDArmorySetup.WindowRectRwr.x, BDArmorySetup.WindowRectRwr.y, RwrDisplayRect.height + BorderSize, RwrDisplayRect.height + BorderSize + HeaderSize);
                    // BDArmorySetup.WindowRectRwr = new Rect(40, Screen.height - RwrDisplayRect.height, RwrDisplayRect.height + BorderSize, RwrDisplayRect.height + BorderSize + HeaderSize);
                    WindowRectRWRInitialized = true;
                }

                using (var mf = VesselModuleRegistry.GetModules<MissileFire>(vessel).GetEnumerator())
                    while (mf.MoveNext())
                    {
                        if (mf.Current == null) continue;
                        mf.Current.rwr = this; // Set the rwr on all weapon managers to this.
                        if (!weaponManager)
                        {
                            weaponManager = mf.Current; // Set the first found weapon manager as the one in control.
                        }
                    }
                //if (rwrEnabled) EnableRWR();
                EnableRWR();
            }
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_UI_VOLUME;
            }
        }

        public void EnableRWR()
        {
            OnRadarPing += ReceivePing;
            OnMissileLaunch += ReceiveLaunchWarning;
            rwrEnabled = true;
        }

        public void DisableRWR()
        {
            OnRadarPing -= ReceivePing;
            OnMissileLaunch -= ReceiveLaunchWarning;
            rwrEnabled = false;
        }

        void OnDestroy()
        {
            OnRadarPing -= ReceivePing;
            OnMissileLaunch -= ReceiveLaunchWarning;
            BDArmorySetup.OnVolumeChange -= UpdateVolume;
        }

        IEnumerator PingLifeRoutine(int index, float lifeTime)
        {
            yield return new WaitForSecondsFixed(Mathf.Clamp(lifeTime - 0.04f, minPingInterval, lifeTime));
            pingsData[index] = TargetSignatureData.noTarget;
        }

        IEnumerator LaunchWarningRoutine(TargetSignatureData data)
        {
            launchWarnings.Add(data);
            yield return new WaitForSecondsFixed(2);
            launchWarnings.Remove(data);
        }

        void ReceiveLaunchWarning(Vector3 source, Vector3 direction, bool radar)
        {
            if (referenceTransform == null) return;
            if (part == null || !part.isActiveAndEnabled) return;
            if (weaponManager == null) return;
            if (!omniDetection && !radar) return;

            float sqrDist = (part.transform.position - source).sqrMagnitude;
            //if ((weaponManager && weaponManager.guardMode) && (sqrDist > (weaponManager.guardRange * weaponManager.guardRange))) return; //doesn't this clamp the RWR to visual view range, not radar/RWR range?
            if (sqrDist < BDArmorySettings.MAX_ENGAGEMENT_RANGE * BDArmorySettings.MAX_ENGAGEMENT_RANGE && sqrDist > 10000f && Vector3.Angle(direction, part.transform.position - source) < 15f)
            {
                StartCoroutine(
                    LaunchWarningRoutine(new TargetSignatureData(Vector3.zero,
                        RadarUtils.WorldToRadar(source, referenceTransform, RwrDisplayRect, rwrDisplayRange), Vector3.zero,
                        true, (float)RWRThreatTypes.MissileLaunch)));
                PlayWarningSound(RWRThreatTypes.MissileLaunch);

                if (weaponManager && weaponManager.guardMode)
                {
                    //weaponManager.FireAllCountermeasures(Random.Range(1, 2)); // Was 2-4, but we don't want to take too long doing this initial dump before other routines kick in
                    weaponManager.incomingThreatPosition = source;
                    weaponManager.missileIsIncoming = true;
                }
            }
        }

        void ReceivePing(Vessel v, Vector3 source, RWRThreatTypes type, float persistTime)
        {
            if (v == null || v.packed || !v.loaded || !v.isActiveAndEnabled) return;
            if (referenceTransform == null) return;
            if (weaponManager == null) return;

            if (rwrEnabled && vessel && v == vessel)
            {
                //if we are airborne or on land, no Sonar or SLW type weapons on the RWR!
                if ((type == RWRThreatTypes.Torpedo || type == RWRThreatTypes.TorpedoLock || type == RWRThreatTypes.Sonar) && (vessel.situation != Vessel.Situations.SPLASHED))
                {
                    // rwr stays silent...
                    return;
                }

                if (type == RWRThreatTypes.MissileLaunch || type == RWRThreatTypes.Torpedo)
                {
                    StartCoroutine(
                        LaunchWarningRoutine(new TargetSignatureData(Vector3.zero,
                            RadarUtils.WorldToRadar(source, referenceTransform, RwrDisplayRect, rwrDisplayRange),
                            Vector3.zero, true, (float)type)));
                    PlayWarningSound(type, (source - vessel.transform.position).sqrMagnitude);
                    return;
                }
                else if (type == RWRThreatTypes.MissileLock)
                {
                    if (weaponManager && weaponManager.guardMode)
                    {
                        weaponManager.FireChaff();
                        weaponManager.missileIsIncoming = true;
                        // TODO: if torpedo inbound, also fire accoustic decoys (not yet implemented...)
                    }
                }

                int openIndex = -1;
                for (int i = 0; i < dataCount; i++)
                {
                    if (pingsData[i].exists &&
                        ((Vector2)pingsData[i].position -
                         RadarUtils.WorldToRadar(source, referenceTransform, RwrDisplayRect, rwrDisplayRange)).sqrMagnitude < (BDArmorySettings.LOGARITHMIC_RADAR_DISPLAY ? 100f : 900f))    //prevent ping spam
                    {
                        break;
                    }

                    if (!pingsData[i].exists && openIndex == -1)
                    {
                        openIndex = i;
                    }
                }

                if (openIndex >= 0)
                {
                    referenceTransform.rotation = Quaternion.LookRotation(vessel.ReferenceTransform.up,
                        VectorUtils.GetUpDirection(transform.position));

                    pingsData[openIndex] = new TargetSignatureData(Vector3.zero,
                        RadarUtils.WorldToRadar(source, referenceTransform, RwrDisplayRect, rwrDisplayRange), Vector3.zero,
                        true, (float)type);    // HACK! Evil misuse of signalstrength for the threat type!
                    pingWorldPositions[openIndex] = source; //FIXME source is improperly defined
                    if (weaponManager.hasAntiRadiationOrdinance)
                    {
                        BDATargetManager.ReportVessel(AIUtils.VesselClosestTo(source), weaponManager); // Report RWR ping as target for anti-rads
                    } //MissileFire RWR-vessel checks are all (RWR ping position - guardtarget.CoM).Magnitude < 20*20?, could we simplify the more complex vessel aquistion function used here?
                    StartCoroutine(PingLifeRoutine(openIndex, persistTime));

                    PlayWarningSound(type, (source - vessel.transform.position).sqrMagnitude);
                }
            }
        }

        void PlayWarningSound(RWRThreatTypes type, float sqrDistance = 0f)
        {
            if (vessel.isActiveVessel && audioSourceRepeatDelay <= 0f)
            {
                switch (type)
                {
                    case RWRThreatTypes.MissileLaunch:
                        if (audioSource.isPlaying)
                            break;
                        audioSource.clip = missileLaunchSound;
                        audioSource.Play();
                        break;

                    case RWRThreatTypes.Sonar:
                        if (audioSource.isPlaying)
                            break;
                        audioSource.clip = sonarPing;
                        audioSource.Play();
                        break;

                    case RWRThreatTypes.Torpedo:
                    case RWRThreatTypes.TorpedoLock:
                        if (audioSource.isPlaying)
                            break;
                        torpedoPingPitch = Mathf.Lerp(1.5f, 1.0f, sqrDistance / (2000 * 2000)); //within 2km increase ping pitch
                        audioSource.Stop();
                        audioSource.clip = torpedoPing;
                        audioSource.pitch = torpedoPingPitch;
                        audioSource.Play();
                        audioSourceRepeatDelay = audioSourceRepeatDelayTime;    //set a min repeat delay to prevent too much audi pinging
                        break;

                    case RWRThreatTypes.MissileLock:
                        if (audioSource.isPlaying)
                            break;
                        audioSource.clip = (missileLockSound);
                        audioSource.Play();
                        audioSourceRepeatDelay = audioSourceRepeatDelayTime;    //set a min repeat delay to prevent too much audi pinging
                        break;
                    case RWRThreatTypes.None:
                        break;
                    default:
                        if (!audioSource.isPlaying)
                        {
                            audioSource.clip = (radarPingSound);
                            audioSource.Play();
                            audioSourceRepeatDelay = audioSourceRepeatDelayTime;    //set a min repeat delay to prevent too much audi pinging
                        }
                        break;
                }
            }
        }

        void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || !BDArmorySetup.GAME_UI_ENABLED ||
                !vessel.isActiveVessel || !displayRWR) return;
            if (audioSourceRepeatDelay > 0)
                audioSourceRepeatDelay -= Time.fixedDeltaTime;

            if (resizingWindow && Event.current.type == EventType.MouseUp) { resizingWindow = false; }

            if (BDArmorySettings.UI_SCALE != 1) GUIUtility.ScaleAroundPivot(BDArmorySettings.UI_SCALE * Vector2.one, BDArmorySetup.WindowRectRwr.position);
            BDArmorySetup.WindowRectRwr = GUI.Window(94353, BDArmorySetup.WindowRectRwr, WindowRwr, "Radar Warning Receiver", GUI.skin.window);
            GUIUtils.UseMouseEventInRect(RwrDisplayRect);
        }

        internal void WindowRwr(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, BDArmorySetup.WindowRectRwr.width - 18, 30));
            if (GUI.Button(new Rect(BDArmorySetup.WindowRectRwr.width - 18, 2, 16, 16), "X", GUI.skin.button))
            {
                displayRWR = false;
                BDArmorySetup.SaveConfig();
            }
            GUI.BeginGroup(new Rect(BorderSize / 2, HeaderSize + (BorderSize / 2), RwrDisplayRect.width, RwrDisplayRect.height));
            //GUI.DragWindow(RwrDisplayRect);

            GUI.DrawTexture(RwrDisplayRect, VesselRadarData.omniBgTexture, ScaleMode.StretchToFill, false);
            float pingSize = 32 * BDArmorySettings.RWR_WINDOW_SCALE;

            for (int i = 0; i < dataCount; i++)
            {
                Vector2 pingPosition = (Vector2)pingsData[i].position;
                //pingPosition = Vector2.MoveTowards(displayRect.center, pingPosition, displayRect.center.x - (pingSize/2));
                Rect pingRect = new Rect(pingPosition.x - (pingSize / 2), pingPosition.y - (pingSize / 2), pingSize,
                    pingSize);

                if (!pingsData[i].exists) continue;
                if (pingsData[i].signalStrength == (float)RWRThreatTypes.MissileLock) //Hack! Evil misuse of field signalstrength...
                {
                    GUI.DrawTexture(pingRect, rwrMissileTexture, ScaleMode.StretchToFill, true);
                }
                else
                {
                    GUI.DrawTexture(pingRect, rwrDiamondTexture, ScaleMode.StretchToFill, true);
                    GUI.Label(pingRect, iconLabels[Mathf.RoundToInt(pingsData[i].signalStrength)], rwrIconLabelStyle); //Hack! Evil misuse of field signalstrength...
                }
            }

            List<TargetSignatureData>.Enumerator lw = launchWarnings.GetEnumerator();
            while (lw.MoveNext())
            {
                Vector2 pingPosition = (Vector2)lw.Current.position;
                //pingPosition = Vector2.MoveTowards(displayRect.center, pingPosition, displayRect.center.x - (pingSize/2));

                Rect pingRect = new Rect(pingPosition.x - (pingSize / 2), pingPosition.y - (pingSize / 2), pingSize,
                    pingSize);
                GUI.DrawTexture(pingRect, rwrMissileTexture, ScaleMode.StretchToFill, true);
            }
            lw.Dispose();
            GUI.EndGroup();

            // Resizing code block.
            RWRresizeRect =
                new Rect(BDArmorySetup.WindowRectRwr.width - 18, BDArmorySetup.WindowRectRwr.height - 18, 16, 16);
            GUI.DrawTexture(RWRresizeRect, GUIUtils.resizeTexture, ScaleMode.StretchToFill, true);
            if (Event.current.type == EventType.MouseDown && RWRresizeRect.Contains(Event.current.mousePosition))
            {
                resizingWindow = true;
            }

            if (Event.current.type == EventType.Repaint && resizingWindow)
            {
                if (Mouse.delta.x != 0 || Mouse.delta.y != 0)
                {
                    float diff = (Mathf.Abs(Mouse.delta.x) > Mathf.Abs(Mouse.delta.y) ? Mouse.delta.x : Mouse.delta.y) / BDArmorySettings.UI_SCALE;
                    BDArmorySettings.RWR_WINDOW_SCALE = Mathf.Clamp(BDArmorySettings.RWR_WINDOW_SCALE + diff / RwrSize, BDArmorySettings.RWR_WINDOW_SCALE_MIN, BDArmorySettings.RWR_WINDOW_SCALE_MAX);
                    BDArmorySetup.ResizeRwrWindow(BDArmorySettings.RWR_WINDOW_SCALE);
                }
            }
            // End Resizing code.

            GUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectRwr);
        }

        public static void PingRWR(Vessel v, Vector3 source, RWRThreatTypes type, float persistTime)
        {
            if (OnRadarPing != null)
            {
                OnRadarPing(v, source, type, persistTime);
            }
        }

        public static void PingRWR(Ray ray, float fov, RWRThreatTypes type, float persistTime)
        {
            using (var vessel = FlightGlobals.Vessels.GetEnumerator())
                while (vessel.MoveNext())
                {
                    if (vessel.Current == null || !vessel.Current.loaded) continue;
                    if (VesselModuleRegistry.ignoredVesselTypes.Contains(vessel.Current.vesselType)) continue;
                    Vector3 dirToVessel = vessel.Current.transform.position - ray.origin;
                    if (Vector3.Angle(ray.direction, dirToVessel) < fov / 2)
                    {
                        PingRWR(vessel.Current, ray.origin, type, persistTime);
                    }
                }
        }

        public static void WarnMissileLaunch(Vector3 source, Vector3 direction, bool radarMissile)
        {
            OnMissileLaunch?.Invoke(source, direction, radarMissile);
        }
    }
}
