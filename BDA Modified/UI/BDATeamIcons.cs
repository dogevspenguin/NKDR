using System.Collections.Generic;
using UnityEngine;
using BDArmory.Competition;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.VesselSpawning;
using BDArmory.Weapons.Missiles;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDATeamIcons : MonoBehaviour
    {
        public BDATeamIcons Instance;

        public Material IconMat;

        void Awake()
        {
            if (Instance)
            {
                Destroy(this);
            }
            else
                Instance = this;
        }
        GUIStyle IconUIStyle;
        GUIStyle DropshadowStyle;
        GUIStyle mIStyle;
        Color Teamcolor;
        Color Missilecolor;
        float Opacity;

        private void Start()
        {
            IconUIStyle = new GUIStyle();
            IconUIStyle.fontStyle = FontStyle.Bold;
            IconUIStyle.fontSize = 10;
            IconUIStyle.normal.textColor = XKCDColors.Red;//replace with BDATISetup defined value varable.

            DropshadowStyle = new GUIStyle();
            DropshadowStyle.fontStyle = FontStyle.Bold;
            DropshadowStyle.fontSize = 10;
            DropshadowStyle.normal.textColor = Color.black;

            mIStyle = new GUIStyle();
            mIStyle.fontStyle = FontStyle.Normal;
            mIStyle.fontSize = 10;
            mIStyle.normal.textColor = XKCDColors.Yellow;
            Missilecolor = XKCDColors.Yellow;

            IconMat = new Material(Shader.Find("KSP/Particles/Alpha Blended"));

            UpdateStyles(true);
        }

        private void DrawOnScreenIcon(Vector3 worldPos, Texture texture, Vector2 size, Color Teamcolor, bool ShowPointer)
        {
            Teamcolor.a *= BDTISetup.iconOpacity;
            if (Event.current.type.Equals(EventType.Repaint))
            {
                bool offscreen = false;
                Vector3 screenPos = GUIUtils.GetMainCamera().WorldToViewportPoint(worldPos);
                if (screenPos.z < 0)
                {
                    offscreen = true;
                    screenPos.x *= -1;
                    screenPos.y *= -1;
                }
                if (screenPos.x != Mathf.Clamp01(screenPos.x))
                {
                    offscreen = true;
                }
                if (screenPos.y != Mathf.Clamp01(screenPos.y))
                {
                    offscreen = true;
                }
                float xPos = (screenPos.x * Screen.width) - (0.5f * size.x);
                float yPos = ((1 - screenPos.y) * Screen.height) - (0.5f * size.y);
                float xtPos = 1 * (Screen.width / 2);
                float ytPos = 1 * (Screen.height / 2);

                if (!offscreen)
                {
                    IconMat.SetColor("_TintColor", Teamcolor);
                    IconMat.mainTexture = texture;
                    Rect iconRect = new Rect(xPos, yPos, size.x, size.y);
                    Graphics.DrawTexture(iconRect, texture, IconMat);
                }
                else
                {
                    if (BDTISettings.POINTERS)
                    {
                        Vector2 head;
                        Vector2 tail;

                        head.x = xPos;
                        head.y = yPos;
                        tail.x = xtPos;
                        tail.y = ytPos;
                        float angle = Vector2.Angle(Vector3.up, tail - head);
                        if (tail.x < head.x)
                        {
                            angle = -angle;
                        }
                        if (ShowPointer && BDTISettings.POINTERS)
                        {
                            DrawPointer(calculateRadialCoords(head, tail, angle, 0.75f), angle, 4, Teamcolor);
                        }
                    }
                }

            }
        }
        private void DrawThreatIndicator(Vector3 vesselPos, Vector3 targetPos, Color Teamcolor)
        {
            Teamcolor.a *= BDTISetup.iconOpacity;
            if (Event.current.type.Equals(EventType.Repaint))
            {
                Vector3 screenPos = GUIUtils.GetMainCamera().WorldToViewportPoint(vesselPos);
                Vector3 screenTPos = GUIUtils.GetMainCamera().WorldToViewportPoint(targetPos);
                if (screenTPos.z > 0)
                {
                    float xPos = (screenPos.x * Screen.width);
                    float yPos = ((1 - screenPos.y) * Screen.height);
                    float xtPos = (screenTPos.x * Screen.width);
                    float ytPos = ((1 - screenTPos.y) * Screen.height);

                    Vector2 head;
                    Vector2 tail;

                    head.x = xPos;
                    head.y = yPos;
                    tail.x = xtPos;
                    tail.y = ytPos;
                    float angle = Vector2.Angle(Vector3.up, tail - head);
                    if (tail.x < head.x)
                    {
                        angle = -angle;
                    }
                    DrawPointer(tail, (angle - 180), 2, Teamcolor);
                }
            }
        }
        public Vector2 calculateRadialCoords(Vector2 RadialCoord, Vector2 Tail, float angle, float edgeDistance)
        {
            float theta = Mathf.Abs(angle);
            if (theta > 90)
            {
                theta -= 90;
            }
            theta = theta * Mathf.Deg2Rad; //needs to be in radians for Mathf. trig
            float Cos = Mathf.Cos(theta);
            float Sin = Mathf.Sin(theta);

            if (RadialCoord.y >= Tail.y)
            {
                if (RadialCoord.x >= Tail.x) // set up Quads 3-4
                {
                    RadialCoord.x = (Cos * (edgeDistance * Tail.x)) + Tail.x;
                }
                else
                {
                    RadialCoord.x = Tail.x - ((Cos * edgeDistance) * Tail.x);
                }
                RadialCoord.y = (Sin * (edgeDistance * Tail.y)) + Tail.y;
            }
            else
            {
                if (RadialCoord.x >= Tail.x) // set up Quads 1-2 
                {
                    RadialCoord.x = (Sin * (edgeDistance * Tail.x)) + Tail.x;
                }
                else
                {
                    RadialCoord.x = Tail.x - ((Sin * edgeDistance) * Tail.x);
                }
                RadialCoord.y = Tail.y - ((Cos * edgeDistance) * Tail.y);
            }
            return RadialCoord;
        }
        public static void DrawPointer(Vector2 Pointer, float angle, float width, Color color)
        {
            Camera cam = GUIUtils.GetMainCamera();

            if (cam == null) return;

            var guiMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float length = 60;

            Rect upRect = new Rect(Pointer.x - (width / 2), Pointer.y - length, width, length);
            GUIUtility.RotateAroundPivot(-angle + 180, Pointer);
            GUIUtils.DrawRectangle(upRect, color);
            GUI.matrix = guiMatrix;
        }
        void OnGUI()
        {
            if ((HighLogic.LoadedSceneIsFlight && BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDTISettings.TEAMICONS) || HighLogic.LoadedSceneIsFlight && !BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled && BDTISettings.TEAMICONS && BDTISettings.PERSISTANT)
            {
                Texture icon;
                float size = 40;
                UpdateStyles();
                using (List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator())
                    while (v.MoveNext())
                    {
                        if (v.Current == null || v.Current.packed || !v.Current.loaded) continue;
                        if (BDTISettings.MISSILES)
                        {
                            using (var ml = VesselModuleRegistry.GetModules<MissileBase>(v.Current).GetEnumerator())
                                while (ml.MoveNext())
                                {
                                    if (ml.Current == null) continue;
                                    MissileLauncher launcher = ml.Current as MissileLauncher;
                                    //if (ml.Current.MissileState != MissileBase.MissileStates.Idle && ml.Current.MissileState != MissileBase.MissileStates.Drop)

                                    bool multilauncher = false;
                                    if (launcher != null)
                                    {
                                        if (launcher.multiLauncher && !launcher.multiLauncher.isClusterMissile) multilauncher = true;
                                    }
                                    if (ml.Current.HasFired && !multilauncher && !ml.Current.HasMissed && !ml.Current.HasExploded) //culling post-thrust missiles makes AGMs get cleared almost immediately after launch
                                    {
                                        Vector3 sPos = FlightGlobals.ActiveVessel.vesselTransform.position;
                                        Vector3 tPos = v.Current.vesselTransform.position;
                                        float Dist = (tPos - sPos).magnitude;
                                        Vector2 guiPos;
                                        string UIdist;
                                        string UoM;
                                        if (Dist >= BDTISettings.DISTANCE_THRESHOLD && Dist <= BDTISettings.MAX_DISTANCE_THRESHOLD)
                                        {
                                            if (Dist / 1000 >= 1)
                                            {
                                                UoM = "km";
                                                UIdist = (Dist / 1000).ToString("0.00");
                                            }
                                            else
                                            {
                                                UoM = "m";
                                                UIdist = Dist.ToString("0.0");
                                            }
                                            DrawOnScreenIcon(v.Current.CoM, BDTISetup.Instance.TextureIconMissile, new Vector2(20, 20), Missilecolor, true);
                                            if (GUIUtils.WorldToGUIPos(ml.Current.vessel.CoM, out guiPos))
                                            {
                                                Rect distRect = new Rect((guiPos.x - 12), (guiPos.y + 10), 100, 32);
                                                GUI.Label(distRect, UIdist + UoM, mIStyle);
                                            }
                                            if (BDTISettings.MISSILE_TEXT)
                                            {
                                                if (GUIUtils.WorldToGUIPos(ml.Current.vessel.CoM, out guiPos))
                                                {
                                                    Color iconUI = BDTISetup.Instance.ColorAssignments.ContainsKey(ml.Current.Team.Name) ? BDTISetup.Instance.ColorAssignments[ml.Current.Team.Name] : Color.gray;
                                                    iconUI.a = Opacity * BDTISetup.textOpacity;
                                                    IconUIStyle.normal.textColor = iconUI;
                                                    Rect nameRect = new Rect((guiPos.x + (24 * BDTISettings.ICONSCALE)), guiPos.y - 4, 100, 32);
                                                    Rect shadowRect = new Rect((nameRect.x + 1), nameRect.y + 1, 100, 32);
                                                    GUI.Label(shadowRect, ml.Current.vessel.vesselName, DropshadowStyle);
                                                    GUI.Label(nameRect, ml.Current.vessel.vesselName, IconUIStyle);
                                                }
                                            }
                                        }
                                    }
                                }
                        }

                        if (!v.Current.loaded || v.Current.packed || v.Current.isActiveVessel) continue;
                        if (BDTISettings.DEBRIS)
                        {
                            if (v.Current == null) continue;
                            if (v.Current.vesselType != VesselType.Debris) continue;
                            if (v.Current.LandedOrSplashed) continue;

                            Vector3 sPos = FlightGlobals.ActiveVessel.vesselTransform.position;
                            Vector3 tPos = v.Current.vesselTransform.position;
                            float Dist = (tPos - sPos).magnitude;
                            if (Dist >= BDTISettings.DISTANCE_THRESHOLD && Dist <= BDTISettings.MAX_DISTANCE_THRESHOLD)
                            {
                                GUIUtils.DrawTextureOnWorldPos(v.Current.CoM, BDTISetup.Instance.TextureIconDebris, new Vector2(20, 20), 0);
                            }
                        }
                    }
                using (var teamManagers = BDTISetup.Instance.weaponManagers.GetEnumerator())
                    while (teamManagers.MoveNext())
                    {
                        using (var wm = teamManagers.Current.Value.GetEnumerator())
                            while (wm.MoveNext())
                            {
                                if (wm.Current == null) continue;
                                if (!BDTISetup.Instance.ColorAssignments.ContainsKey(wm.Current.Team.Name)) continue; // Ignore entries that haven't been updated yet.
                                Color teamcolor = BDTISetup.Instance.ColorAssignments[wm.Current.Team.Name];
                                teamcolor.a = Opacity;
                                Teamcolor = teamcolor;
                                teamcolor.a *= BDTISetup.textOpacity;
                                IconUIStyle.normal.textColor = teamcolor;
                                size = wm.Current.vessel.vesselType == VesselType.Debris ? 20 : 40;
                                if (wm.Current.vessel.isActiveVessel)
                                {
                                    if (BDTISettings.THREATICON)
                                    {
                                        if (wm.Current.currentTarget == null) continue;
                                        Vector3 sPos = FlightGlobals.ActiveVessel.CoM;
                                        Vector3 tPos = (wm.Current.currentTarget.Vessel.CoM);
                                        float RelPos = (tPos - sPos).magnitude;
                                        if (RelPos >= BDTISettings.DISTANCE_THRESHOLD && RelPos <= BDTISettings.MAX_DISTANCE_THRESHOLD)
                                        {
                                            DrawThreatIndicator(wm.Current.vessel.CoM, wm.Current.currentTarget.Vessel.CoM, Teamcolor);
                                        }
                                    }
                                    if (BDTISettings.SHOW_SELF)
                                    {
                                        icon = GetIconForVessel(wm.Current.vessel);
                                        DrawOnScreenIcon(wm.Current.vessel.CoM, icon, new Vector2((size * BDTISettings.ICONSCALE), (size * BDTISettings.ICONSCALE)), Teamcolor, true);
                                        if (BDTISettings.VESSELNAMES)
                                        {
                                            Vector2 guiPos;
                                            if (GUIUtils.WorldToGUIPos(wm.Current.vessel.CoM, out guiPos))
                                            {
                                                Rect nameRect = new Rect((guiPos.x + (24 * BDTISettings.ICONSCALE)), guiPos.y - 4, 100, 32);
                                                Rect shadowRect = new Rect((nameRect.x + 1), nameRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, wm.Current.vessel.vesselName, DropshadowStyle);
                                                GUI.Label(nameRect, wm.Current.vessel.vesselName, IconUIStyle);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Vector3 selfPos = FlightGlobals.ActiveVessel.CoM;
                                    Vector3 targetPos = (wm.Current.vessel.CoM);
                                    Vector3 targetRelPos = (targetPos - selfPos);
                                    Vector2 guiPos;
                                    float distance;
                                    string UIdist;
                                    string UoM;
                                    string vName;
                                    string selectedWeapon = string.Empty;
                                    string AIstate = string.Empty;
                                    distance = targetRelPos.magnitude;
                                    if (distance >= BDTISettings.DISTANCE_THRESHOLD && distance <= BDTISettings.MAX_DISTANCE_THRESHOLD) //TODO - look into having vessel icons be based on vesel visibility? (So don't draw icon for undetected stealth plane, etc?)
                                    {
                                        if ((distance / 1000) >= 1)
                                        {
                                            UoM = "km";
                                            UIdist = (distance / 1000).ToString("0.00");
                                        }
                                        else
                                        {
                                            UoM = "m";
                                            UIdist = distance.ToString("0.0");
                                        }
                                        icon = GetIconForVessel(wm.Current.vessel);
                                        DrawOnScreenIcon(wm.Current.vessel.CoM, icon, new Vector2((size * BDTISettings.ICONSCALE), (size * BDTISettings.ICONSCALE)), Teamcolor, true);
                                        if (BDTISettings.THREATICON)
                                        {
                                            if (wm.Current.currentTarget != null)
                                            {
                                                if (!wm.Current.currentTarget.Vessel.isActiveVessel)
                                                {
                                                    DrawThreatIndicator(wm.Current.vessel.CoM, wm.Current.currentTarget.Vessel.CoM, Teamcolor);
                                                }
                                            }
                                        }
                                        if (GUIUtils.WorldToGUIPos(wm.Current.vessel.CoM, out guiPos))
                                        {
                                            if (BDTISettings.VESSELNAMES)
                                            {
                                                vName = wm.Current.vessel.vesselName;
                                                Rect nameRect = new Rect((guiPos.x + (24 * BDTISettings.ICONSCALE)), guiPos.y - 4, 100, 32);
                                                Rect shadowRect = new Rect((nameRect.x + 1), nameRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, vName, DropshadowStyle);
                                                GUI.Label(nameRect, vName, IconUIStyle);
                                            }
                                            if (BDTISettings.TEAMNAMES)
                                            {
                                                Rect teamRect = new Rect((guiPos.x + (16 * BDTISettings.ICONSCALE)), (guiPos.y - (19 * BDTISettings.ICONSCALE)), 100, 32);
                                                Rect shadowRect = new Rect((teamRect.x + 1), teamRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, "Team: " + $"{wm.Current.Team.Name}", DropshadowStyle);
                                                GUI.Label(teamRect, "Team: " + $"{wm.Current.Team.Name}", IconUIStyle);
                                            }

                                            if (BDTISettings.SCORE)
                                            {
                                                ScoringData scoreData = null;
                                                int Score = 0;

                                                if (BDACompetitionMode.Instance.Scores.ScoreData.ContainsKey(wm.Current.vessel.vesselName))
                                                {
                                                    scoreData = BDACompetitionMode.Instance.Scores.ScoreData[wm.Current.vessel.vesselName];
                                                    Score = scoreData.hits;
                                                }
                                                if (ContinuousSpawning.Instance.vesselsSpawningContinuously)
                                                {
                                                    if (ContinuousSpawning.Instance.continuousSpawningScores.ContainsKey(wm.Current.vessel.vesselName))
                                                    {
                                                        Score += ContinuousSpawning.Instance.continuousSpawningScores[wm.Current.vessel.vesselName].cumulativeHits;
                                                    }
                                                }

                                                Rect scoreRect = new Rect((guiPos.x + (16 * BDTISettings.ICONSCALE)), (guiPos.y + (14 * BDTISettings.ICONSCALE)), 100, 32);
                                                Rect shadowRect = new Rect((scoreRect.x + 1), scoreRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, "Score: " + Score, DropshadowStyle);
                                                GUI.Label(scoreRect, "Score: " + Score, IconUIStyle);
                                            }
                                            if (BDTISettings.HEALTHBAR)
                                            {

                                                double hpPercent = 1;
                                                hpPercent = Mathf.Clamp(wm.Current.currentHP / wm.Current.totalHP, 0, 1);
                                                if (hpPercent > 0)
                                                {
                                                    Rect barRect = new Rect((guiPos.x - (32 * BDTISettings.ICONSCALE)), (guiPos.y + (30 * BDTISettings.ICONSCALE)), (64 * BDTISettings.ICONSCALE), 12);
                                                    Rect healthRect = new Rect((guiPos.x - (30 * BDTISettings.ICONSCALE)), (guiPos.y + (32 * BDTISettings.ICONSCALE)), (60 * (float)hpPercent * BDTISettings.ICONSCALE), 8);
                                                    Color temp = XKCDColors.Grey;
                                                    temp.a = Opacity * BDTISetup.iconOpacity;
                                                    GUIUtils.DrawRectangle(barRect, temp);
                                                    temp = Color.HSVToRGB((85f * (float)hpPercent) / 255, 1f, 1f);
                                                    temp.a = Opacity * BDTISetup.iconOpacity;
                                                    GUIUtils.DrawRectangle(healthRect, temp);

                                                }
                                                Rect distRect = new Rect((guiPos.x - 12), (guiPos.y + (45 * BDTISettings.ICONSCALE)), 100, 32);
                                                Rect shadowRect = new Rect((distRect.x + 1), distRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, UIdist + UoM, DropshadowStyle);
                                                GUI.Label(distRect, UIdist + UoM, IconUIStyle);
                                            }
                                            else
                                            {
                                                Rect distRect = new Rect((guiPos.x - 12), (guiPos.y + (20 * BDTISettings.ICONSCALE)), 100, 32);
                                                Rect shadowRect = new Rect((distRect.x + 1), distRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, UIdist + UoM, DropshadowStyle);
                                                GUI.Label(distRect, UIdist + UoM, IconUIStyle);
                                            }
                                            if (BDTISettings.TELEMETRY)
                                            {
                                                selectedWeapon = "Using: " + wm.Current.selectedWeaponString;
                                                AIstate = "No AI";
                                                if (wm.Current.AI != null)
                                                {
                                                    AIstate = "Pilot " + wm.Current.AI.currentStatus;
                                                }
                                                Rect telemetryRect = new Rect((guiPos.x + (32 * BDTISettings.ICONSCALE)), guiPos.y + 32, 200, 32);
                                                Rect shadowRect = new Rect((telemetryRect.x + 1), telemetryRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect, selectedWeapon, DropshadowStyle);
                                                GUI.Label(telemetryRect, selectedWeapon, IconUIStyle);
                                                Rect telemetryRect2 = new Rect((guiPos.x + (32 * BDTISettings.ICONSCALE)), guiPos.y + 48, 200, 32);
                                                Rect shadowRect2 = new Rect((telemetryRect2.x + 1), telemetryRect2.y + 1, 100, 32);
                                                GUI.Label(telemetryRect2, AIstate, DropshadowStyle);
                                                GUI.Label(telemetryRect2, AIstate, IconUIStyle);
                                                if (wm.Current.isFlaring || wm.Current.isChaffing || wm.Current.isECMJamming)
                                                {
                                                    Rect telemetryRect3 = new Rect((guiPos.x + (32 * BDTISettings.ICONSCALE)), guiPos.y + 64, 200, 32);
                                                    Rect shadowRect3 = new Rect((telemetryRect3.x + 1), telemetryRect3.y + 1, 100, 32);
                                                    GUI.Label(shadowRect3, "Deploying Counter-Measures", DropshadowStyle);
                                                    GUI.Label(telemetryRect3, "Deploying Counter-Measures", IconUIStyle);
                                                }
                                                Rect SpeedRect = new Rect((guiPos.x - (96 * BDTISettings.ICONSCALE)), guiPos.y + 64, 100, 32);
                                                Rect shadowRect4 = new Rect((SpeedRect.x + 1), SpeedRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect4, "Speed: " + wm.Current.vessel.speed.ToString("0.0") + "m/s", DropshadowStyle);
                                                GUI.Label(SpeedRect, "Speed: " + wm.Current.vessel.speed.ToString("0.0") + "m/s", IconUIStyle);
                                                Rect RAltRect = new Rect((guiPos.x - (96 * BDTISettings.ICONSCALE)), guiPos.y + 80, 100, 32);
                                                Rect shadowRect5 = new Rect((RAltRect.x + 1), RAltRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect5, "Alt: " + wm.Current.vessel.altitude.ToString("0.0") + "m", DropshadowStyle);
                                                GUI.Label(RAltRect, "Alt: " + wm.Current.vessel.altitude.ToString("0.0") + "m", IconUIStyle);
                                                Rect ThrottleRect = new Rect((guiPos.x - (96 * BDTISettings.ICONSCALE)), guiPos.y + 96, 100, 32);
                                                Rect shadowRect6 = new Rect((ThrottleRect.x + 1), ThrottleRect.y + 1, 100, 32);
                                                GUI.Label(shadowRect6, "Throttle: " + Mathf.CeilToInt(wm.Current.vessel.ctrlState.mainThrottle * 100) + "%", DropshadowStyle);
                                                GUI.Label(ThrottleRect, "Throttle: " + Mathf.CeilToInt(wm.Current.vessel.ctrlState.mainThrottle * 100) + "%", IconUIStyle);
                                            }
                                        }
                                    }
                                }
                            }
                    }
            }
        }

        void UpdateStyles(bool forceUpdate = false)
        {
            // Update opacity for DropshadowStyle, mIStyle, Missilecolor. IconUIStyle opacity
            // is updated in OnGUI().
            if (forceUpdate || Opacity != BDTISettings.OPACITY)
            {
                Opacity = BDTISettings.OPACITY;

                Teamcolor.a = Opacity;
                Color temp;
                temp = DropshadowStyle.normal.textColor;
                temp.a = Opacity * BDTISetup.textOpacity;
                DropshadowStyle.normal.textColor = temp;
                temp = mIStyle.normal.textColor;
                temp.a = Opacity * BDTISetup.textOpacity;
                mIStyle.normal.textColor = temp;
                Missilecolor.a = Opacity;
            }
        }

        Texture2D GetIconForVessel(Vessel v)
        {
            Texture2D icon;
            if ((v.vesselType == VesselType.Ship && !v.Splashed) || v.vesselType == VesselType.Plane)
            {
                icon = BDTISetup.Instance.TextureIconPlane;
            }
            else if (v.vesselType == VesselType.Base || v.vesselType == VesselType.Lander)
            {
                icon = BDTISetup.Instance.TextureIconBase;
            }
            else if (v.vesselType == VesselType.Rover)
            {
                icon = BDTISetup.Instance.TextureIconRover;
            }
            else if (v.vesselType == VesselType.Probe)
            {
                icon = BDTISetup.Instance.TextureIconProbe;
            }
            else if (v.vesselType == VesselType.Ship && v.Splashed)
            {
                icon = BDTISetup.Instance.TextureIconShip;
                if (v.vesselType == VesselType.Ship && v.altitude < -10)
                {
                    icon = BDTISetup.Instance.TextureIconSub;
                }
            }
            else if (v.vesselType == VesselType.Debris)
            {
                icon = BDTISetup.Instance.TextureIconDebris;
                Color temp = XKCDColors.Grey;
                temp.a = Opacity;
                Teamcolor = temp;
                temp.a *= BDTISetup.textOpacity;
                IconUIStyle.normal.textColor = temp;
            }
            else
            {
                icon = BDTISetup.Instance.TextureIconGeneric;
            }
            return icon;
        }
    }
}
