using KSP.Localization;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UnityEngine;

using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Settings;
using BDArmory.Utils;

/*
* *Milestone 6: Figure out how to have TI activation toggle the F4 SHOW_LABELS (or is it Flt_Show_labels?) method to sim a keypress?
*/
namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class BDTISetup : MonoBehaviour
    {
        private ApplicationLauncherButton toolbarButton = null;
        public static Rect WindowRectGUI;

        private string windowTitle = StringUtils.Localize("#LOC_BDArmory_Icons_title");
        public static BDTISetup Instance = null;
        public static GUIStyle TILabel;
        private bool showTeamIconGUI = false;
        float toolWindowWidth = 250;
        float toolWindowHeight = 150;
        Rect IconOptionsGroup;
        Rect TeamColorsGroup;
        public string selectedTeam;
        public bool UpdateTeamColor = false;
        private float updateList = 0;
        private bool maySavethisInstance = false;

        // Opacity Settings
        internal const float textOpacity = 2f;
        internal const float iconOpacity = 1f;

        public SortedList<string, List<MissileFire>> weaponManagers = new SortedList<string, List<MissileFire>>();

        public static string textureDir = "BDArmory/Textures/";

        //legacy version check
        bool LegacyTILoaded = false;
        bool showPSA = false;
        private Texture2D dit;
        public Texture2D TextureIconDebris
        {
            get { return dit ? dit : dit = GameDatabase.Instance.GetTexture(textureDir + "Icons/debrisIcon", false); }
        }
        private Texture2D mit;
        public Texture2D TextureIconMissile
        {
            get { return mit ? mit : mit = GameDatabase.Instance.GetTexture(textureDir + "Icons/missileIcon", false); }
        }
        private Texture2D rit;
        public Texture2D TextureIconRocket
        {
            get { return rit ? rit : rit = GameDatabase.Instance.GetTexture(textureDir + "Icons/rocketIcon", false); }
        }
        private Texture2D ti7;
        public Texture2D TextureIconGeneric
        {
            get { return ti7 ? ti7 : ti7 = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Generic", false); }
        }
        private Texture2D ti1A;
        public Texture2D TextureIconShip
        {
            get { return ti1A ? ti1A : ti1A = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Ship", false); }
        }
        private Texture2D ti2A;
        public Texture2D TextureIconPlane
        {
            get { return ti2A ? ti2A : ti2A = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Plane", false); }
        }
        private Texture2D ti3A;
        public Texture2D TextureIconRover
        {
            get { return ti3A ? ti3A : ti3A = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Rover", false); }
        }
        private Texture2D ti4A;
        public Texture2D TextureIconBase
        {
            get { return ti4A ? ti4A : ti4A = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Base", false); }
        }
        private Texture2D ti5A;
        public Texture2D TextureIconProbe
        {
            get { return ti5A ? ti5A : ti5A = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Probe", false); }
        }
        private Texture2D ti6A;
        public Texture2D TextureIconSub
        {
            get { return ti6A ? ti6A : ti6A = GameDatabase.Instance.GetTexture(textureDir + "Icons/Icon_Sub", false); }
        }

        private Texture2D MTAcc;
        public Texture2D MutatorIconAcc
        {
            get { return MTAcc ? MTAcc : MTAcc = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconAccuracy", false); }
        }
        private Texture2D MTAtk;
        public Texture2D MutatorIconAtk
        {
            get { return MTAtk ? MTAtk : MTAtk = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconAttack", false); }
        }
        private Texture2D MTAtk2;
        public Texture2D MutatorIconAtk2
        {
            get { return MTAtk2 ? MTAtk2 : MTAtk2 = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconAttack2", false); }
        }
        private Texture2D MTBal;
        public Texture2D MutatorIconBullet
        {
            get { return MTBal ? MTBal : MTBal = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconBallistic", false); }
        }
        private Texture2D MTDef;
        public Texture2D MutatorIconDefense
        {
            get { return MTDef ? MTDef : MTDef = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconDefense", false); }
        }
        private Texture2D MTLsr;
        public Texture2D MutatorIconLaser
        {
            get { return MTLsr ? MTLsr : MTLsr = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconLaser", false); }
        }
        private Texture2D MTmass;
        public Texture2D MutatorIconMass
        {
            get { return MTmass ? MTmass : MTmass = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconMass", false); }
        }
        private Texture2D MTHP;
        public Texture2D MutatorIconRegen
        {
            get { return MTHP ? MTHP : MTHP = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconRegen", false); }
        }
        private Texture2D MTRkt;
        public Texture2D MutatorIconRocket
        {
            get { return MTRkt ? MTRkt : MTRkt = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconRocket", false); }
        }
        private Texture2D MTdoom;
        public Texture2D MutatorIconDoom
        {
            get { return MTdoom ? MTdoom : MTdoom = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconSkull", false); }
        }
        private Texture2D MTSpd;
        public Texture2D MutatorIconSpeed
        {
            get { return MTSpd ? MTSpd : MTSpd = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconSpeed", false); }
        }
        private Texture2D MTTgt;
        public Texture2D MutatorIconTarget
        {
            get { return MTTgt ? MTTgt : MTTgt = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconTarget", false); }
        }
        private Texture2D MTVmp;
        public Texture2D MutatorIconVampire
        {
            get { return MTVmp ? MTVmp : MTVmp = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconVampire", false); }
        }
        private Texture2D MTRnd;
        public Texture2D MutatorIconNull
        {
            get { return MTRnd ? MTRnd : MTRnd = GameDatabase.Instance.GetTexture(textureDir + "Mutators/IconUnknown", false); }
        }
        void Start()
        {
            Instance = this;
            if (HighLogic.LoadedSceneIsFlight)
                maySavethisInstance = true;
            if (ConfigNode.Load(BDTISettings.settingsConfigURL) == null)
            {
                var node = new ConfigNode();
                node.AddNode("IconSettings");
                node.Save(BDTISettings.settingsConfigURL);
            }

            AddToolbarButton();
            LoadConfig();
            UpdateList();

            using (var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator())
                while (a.MoveNext())
                {
                    string name = a.Current.FullName.Split(new char[1] { ',' })[0];
                    switch (name)
                    {
                        case "BDATeamIcons":
                            LegacyTILoaded = true;
                            break;
                    }
                }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (LegacyTILoaded)
                {
                    ScreenMessages.PostScreenMessage(StringUtils.Localize("#LOC_BDArmory_Icons_legacyinstall"), 20.0f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            TILabel = new GUIStyle();
            TILabel.font = BDArmorySetup.BDGuiSkin.window.font;
            TILabel.fontSize = BDArmorySetup.BDGuiSkin.window.fontSize;
            TILabel.fontStyle = BDArmorySetup.BDGuiSkin.window.fontStyle;
            IconOptionsGroup = new Rect(10, 55, toolWindowWidth - 20, 290);
            TeamColorsGroup = new Rect(10, IconOptionsGroup.height, toolWindowWidth - 20, 25);
            WindowRectGUI = new Rect(Screen.width - BDArmorySettings.UI_SCALE * (toolWindowWidth + 40), 150, toolWindowWidth, toolWindowHeight);
        }

        private void MissileFireOnToggleTeam(MissileFire wm, BDTeam team)
        {
            if (BDTISettings.TEAMICONS)
            {
                UpdateList();
            }
        }
        private void VesselEventUpdate(Vessel v)
        {
            if (BDTISettings.TEAMICONS)
            {
                UpdateList(true);
            }
        }
        private void Update()
        {
            if (BDTISettings.TEAMICONS)
            {
                updateList -= Time.deltaTime;
                if (updateList < 0)
                {
                    UpdateList();
                    updateList = 1f; // check team lists less often than every frame
                }
            }
        }
        public Dictionary<string, Color> ColorAssignments = new Dictionary<string, Color>();
        private void UpdateList(bool fromModifiedEvent = false)
        {
            weaponManagers.Clear();

            using (List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator())
                while (v.MoveNext())
                {
                    if (v.Current == null || !v.Current.loaded || v.Current.packed) continue;
                    if (VesselModuleRegistry.ignoredVesselTypes.Contains(v.Current.vesselType)) continue;
                    if (fromModifiedEvent) VesselModuleRegistry.OnVesselModified(v.Current, true);
                    var wms = VesselModuleRegistry.GetMissileFire(v.Current, true);
                    if (wms != null)
                    {
                        if (!ColorAssignments.ContainsKey(wms.teamString))
                        {
                            float rnd = UnityEngine.Random.Range(0f, 100f);
                            ColorAssignments.Add(wms.Team.Name, Color.HSVToRGB((rnd / 100f), 1f, 1f));
                        }
                        if (weaponManagers.TryGetValue(wms.Team.Name, out var teamManagers))
                            teamManagers.Add(wms);
                        else
                            weaponManagers.Add(wms.Team.Name, new List<MissileFire> { wms });
                    }
                }
        }
        private void ResetColors()
        {
            ColorAssignments.Clear();
            UpdateList();
            int colorcount = 0;
            var teams = ColorAssignments.Keys.ToList();
            float teamsCount = (float)teams.Count;
            foreach (var team in teams)
            {
                ColorAssignments[team] = Color.HSVToRGB(++colorcount / teamsCount, 1f, 1f);
            }
        }
        private void OnDestroy()
        {
            if (toolbarButton)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
                toolbarButton = null;
            }
            if (maySavethisInstance)
            {
                SaveConfig();
            }
        }

        IEnumerator ToolbarButtonRoutine()
        {
            if (toolbarButton || (!HighLogic.LoadedSceneIsEditor)) yield break;
            yield return new WaitUntil(() => ApplicationLauncher.Ready && BDArmorySetup.toolbarButtonAdded); // Wait until after the main BDA toolbar button.
            AddToolbarButton();
        }

        void AddToolbarButton()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (toolbarButton == null)
                {
                    Texture buttonTexture = GameDatabase.Instance.GetTexture("BDArmory/Textures/Icons/icon", false);
                    toolbarButton = ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
                }
            }
        }

        public void ShowToolbarGUI()
        {
            if (LegacyTILoaded)
            {
                ScreenMessages.PostScreenMessage(StringUtils.Localize("#LOC_BDArmory_Icons_legacyinstall"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                showTeamIconGUI = true;
                showPSA = false;
                LoadConfig();
            }
        }

        public void HideToolbarGUI()
        {
            showTeamIconGUI = false;
            SaveConfig();
        }

        public static void LoadConfig()
        {
            try
            {
                Debug.Log("[BDTeamIcons]=== Loading settings.cfg ===");

                SettingsDataField.Load();
                if (BDTISettings.MAX_DISTANCE_THRESHOLD < 1 || BDTISettings.MAX_DISTANCE_THRESHOLD > BDArmorySettings.MAX_GUARD_VISUAL_RANGE) BDTISettings.MAX_DISTANCE_THRESHOLD = BDArmorySettings.MAX_GUARD_VISUAL_RANGE;
            }
            catch (NullReferenceException)
            {
                Debug.Log("[BDTeamIcons]=== Failed to load settings config ===");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                Debug.Log("[BDTeamIcons] == Saving settings.cfg ==	");
                SettingsDataField.Save();
            }
            catch (NullReferenceException)
            {
                Debug.Log("[BDTeamIcons]: === Failed to save settings.cfg ====");
            }
        }

        GUIStyle title;

        void OnGUI()
        {
            if (LegacyTILoaded) return;

            if (showTeamIconGUI)
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    maySavethisInstance = true;
                }
                if (BDArmorySettings.UI_SCALE != 1) GUIUtility.ScaleAroundPivot(BDArmorySettings.UI_SCALE * Vector2.one, WindowRectGUI.position);
                WindowRectGUI = GUI.Window(GUIUtility.GetControlID(FocusType.Passive), WindowRectGUI, TeamIconGUI, windowTitle, BDArmorySetup.BDGuiSkin.window);
            }
            title = new GUIStyle(GUI.skin.label);
            title.fontSize = 30;
            title.alignment = TextAnchor.MiddleLeft;
            title.wordWrap = false;
            if (HighLogic.LoadedSceneIsFlight && BDTISettings.TEAMICONS)
            {
                if (GameSettings.FLT_VESSEL_LABELS && !showPSA)
                {
                    ScreenMessages.PostScreenMessage(StringUtils.Localize("#LOC_BDArmory_Icons_PSA"), 20.0f, ScreenMessageStyle.UPPER_CENTER);
                    showPSA = true;
                }
            }
        }
        public bool showTeamIconSelect = false;
        public bool showColorSelect = false;

        (float, float)[] cacheMaxDistanceThreshold;
        void TeamIconGUI(int windowID)
        {
            float line = 0;
            GUI.DragWindow(new Rect(0, 0, WindowRectGUI.width, 25));
            BDTISettings.TEAMICONS = GUI.Toggle(new Rect(5, 25, toolWindowWidth, 20), BDTISettings.TEAMICONS, StringUtils.Localize("#LOC_BDArmory_Enable_Icons"), BDArmorySetup.BDGuiSkin.toggle);
            if (BDTISettings.TEAMICONS)
            {
                if (GameSettings.FLT_VESSEL_LABELS && !showPSA)
                {
                    ScreenMessages.PostScreenMessage(StringUtils.Localize("#LOC_BDArmory_Icons_PSA"), 7.0f, ScreenMessageStyle.UPPER_CENTER);
                    showPSA = true;
                }
                GUI.BeginGroup(IconOptionsGroup, GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                BDTISettings.TEAMNAMES = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.TEAMNAMES, StringUtils.Localize("#LOC_BDArmory_Icon_teams"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.VESSELNAMES = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.VESSELNAMES, StringUtils.Localize("#LOC_BDArmory_Icon_names"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.SCORE = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.SCORE, StringUtils.Localize("#LOC_BDArmory_Icon_score"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.HEALTHBAR = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.HEALTHBAR, StringUtils.Localize("#LOC_BDArmory_Icon_healthbars"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.SHOW_SELF = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.SHOW_SELF, StringUtils.Localize("#LOC_BDArmory_Icon_show_self"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.MISSILES = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.MISSILES, StringUtils.Localize("#LOC_BDArmory_Icon_missiles"), BDArmorySetup.BDGuiSkin.toggle);
                if (BDTISettings.MISSILES) BDTISettings.MISSILE_TEXT = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.MISSILE_TEXT, StringUtils.Localize("#LOC_BDArmory_Icon_missile_text"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.DEBRIS = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.DEBRIS, StringUtils.Localize("#LOC_BDArmory_Icon_debris"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.PERSISTANT = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.PERSISTANT, StringUtils.Localize("#LOC_BDArmory_Icon_persist"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.THREATICON = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.THREATICON, StringUtils.Localize("#LOC_BDArmory_Icon_threats"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.POINTERS = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.POINTERS, StringUtils.Localize("#LOC_BDArmory_Icon_pointers"), BDArmorySetup.BDGuiSkin.toggle);
                BDTISettings.TELEMETRY = GUI.Toggle(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), BDTISettings.TELEMETRY, StringUtils.Localize("#LOC_BDArmory_Icon_telemetry"), BDArmorySetup.BDGuiSkin.toggle);
                line += 0.25f;
                GUI.Label(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), StringUtils.Localize("#LOC_BDArmory_Icon_scale") + " " + (BDTISettings.ICONSCALE * 100f).ToString("0") + "%");
                BDTISettings.ICONSCALE = GUI.HorizontalSlider(new Rect(10, line++ * 25, toolWindowWidth - 40, 20), BDTISettings.ICONSCALE, 0.25f, 2f);
                line -= 0.15f;
                GUI.Label(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), $"{StringUtils.Localize("#LOC_BDArmory_Icon_distance_threshold")} {BDTISettings.DISTANCE_THRESHOLD:0}m");
                BDTISettings.DISTANCE_THRESHOLD = BDAMath.RoundToUnit(GUI.HorizontalSlider(new Rect(10, line++ * 25, toolWindowWidth - 40, 20), BDTISettings.DISTANCE_THRESHOLD, 10f, 250f), 10f);
                GUI.Label(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), $"{StringUtils.Localize("#LOC_BDArmory_Icon_opacity")} {BDTISettings.OPACITY * 100f:0}%");
                BDTISettings.OPACITY = BDAMath.RoundToUnit(GUI.HorizontalSlider(new Rect(10, line++ * 25, toolWindowWidth - 40, 20), BDTISettings.OPACITY, 0f, 1f), 0.01f);
                GUI.Label(new Rect(15, line++ * 25, toolWindowWidth - 20, 20), $"{StringUtils.Localize("#LOC_BDArmory_Icon_max_distance_threshold")} {(BDTISettings.MAX_DISTANCE_THRESHOLD < BDArmorySettings.MAX_GUARD_VISUAL_RANGE ? $"{BDTISettings.MAX_DISTANCE_THRESHOLD / 1000f:0}km" : "Unlimited")}");
                BDTISettings.MAX_DISTANCE_THRESHOLD = GUIUtils.HorizontalSemiLogSlider(new Rect(10, line++ * 25, toolWindowWidth - 40, 20), BDTISettings.MAX_DISTANCE_THRESHOLD / 1000f, 1f, BDArmorySettings.MAX_GUARD_VISUAL_RANGE / 1000f, 1, false, ref cacheMaxDistanceThreshold) * 1000f;
                GUI.EndGroup();
                IconOptionsGroup.height = 25f * line;

                TeamColorsGroup.y = IconOptionsGroup.y + IconOptionsGroup.height;
                GUI.BeginGroup(TeamColorsGroup, GUIContent.none, BDArmorySetup.BDGuiSkin.box);
                line = 0;
                using (var teamManagers = weaponManagers.GetEnumerator())
                    while (teamManagers.MoveNext())
                    {
                        line++;
                        Rect buttonRect = new Rect(30, -20 + (line * 25), 190, 20);
                        GUIStyle vButtonStyle = showColorSelect ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
                        if (GUI.Button(buttonRect, $"{teamManagers.Current.Key}", vButtonStyle))
                        {
                            showColorSelect = !showColorSelect;
                            selectedTeam = teamManagers.Current.Key;
                        }
                        if (ColorAssignments.ContainsKey(teamManagers.Current.Key))
                        {
                            title.normal.textColor = ColorAssignments[teamManagers.Current.Key];
                        }
                        GUI.Label(new Rect(5, -20 + (line * 25), 25, 25), "*", title);
                    }
                line++;
                Rect resetRect = new Rect(30, -20 + (line * 25), 190, 20);
                if (GUI.Button(resetRect, "Reset TeamColors"))
                {
                    ResetColors();
                }
                GUI.EndGroup();
                TeamColorsGroup.height = Mathf.Lerp(TeamColorsGroup.height, (line * 25) + 5, 0.35f);
            }
            toolWindowHeight = Mathf.Lerp(toolWindowHeight, 50 + (BDTISettings.TEAMICONS ? IconOptionsGroup.height + TeamColorsGroup.height : 0) + 15, 0.35f);
            WindowRectGUI.height = toolWindowHeight;
        }
    }
}
