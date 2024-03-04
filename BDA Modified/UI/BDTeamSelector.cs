using System.Collections;
using UnityEngine;

using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Settings;
using BDArmory.Utils;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class BDTeamSelector : MonoBehaviour
    {
        public static BDTeamSelector Instance;

        const float width = 250;
        const float margin = 5;
        const float buttonHeight = 20;
        const float buttonGap = 2;
        const float newTeanButtonWidth = 40;
        const float scrollWidth = 20;

        private static int guiCheckIndex = -1;
        private bool ready = false;
        private bool open = false;
        private Rect window;
        private float height;
        private bool scrollable;
        private Vector2 scrollPosition = Vector2.zero;

        private Vector2 windowLocation;
        private MissileFire targetWeaponManager;
        private string newTeamName = string.Empty;

        public void Open(MissileFire weaponManager, Vector2 position)
        {
            SetVisible(true);
            targetWeaponManager = weaponManager;
            newTeamName = string.Empty;
            windowLocation = position;
        }

        void SetVisible(bool visible)
        {
            open = visible;
            GUIUtils.SetGUIRectVisible(guiCheckIndex, visible);
        }

        private void TeamSelectorWindow(int id)
        {
            height = margin;
            // Team input field
            newTeamName = GUI.TextField(new Rect(margin, margin, width - buttonGap - 2 * margin - newTeanButtonWidth, buttonHeight), newTeamName, 30);

            // New team button
            Rect newTeamButtonRect = new Rect(width - margin - newTeanButtonWidth, height, newTeanButtonWidth, buttonHeight);
            if (GUI.Button(newTeamButtonRect, StringUtils.Localize("#LOC_BDArmory_Generic_New"), BDArmorySetup.BDGuiSkin.button))//"New"
            {
                if (!string.IsNullOrEmpty(newTeamName.Trim()))
                {
                    targetWeaponManager.SetTeam(BDTeam.Get(newTeamName.Trim()));
                    SetVisible(false);
                }
            }

            height += buttonHeight;

            // Scrollable list of existing teams
            scrollable = (BDArmorySetup.Instance.Teams.Count * (buttonHeight + buttonGap) * 2 > Screen.height);

            if (scrollable)
                scrollPosition = GUI.BeginScrollView(
                    new Rect(margin, height, width - margin * 2 + scrollWidth, Screen.height / 2),
                    scrollPosition,
                    new Rect(margin, height, width - margin * 2, BDArmorySetup.Instance.Teams.Count * (buttonHeight + buttonGap)),
                    false, true);

            using (var teams = BDArmorySetup.Instance.Teams.Values.GetEnumerator())
                while (teams.MoveNext())
                {
                    if (teams.Current == null || !teams.Current.Name.ToLowerInvariant().StartsWith(newTeamName.ToLowerInvariant().Trim())) continue;

                    height += buttonGap;
                    Rect buttonRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
                    GUIStyle buttonStyle = (teams.Current == targetWeaponManager.Team) ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

                    if (GUI.Button(buttonRect, teams.Current.Name + (teams.Current.Neutral ? (teams.Current.Name != "Neutral" ? "(Neutral)" : "") : ""), buttonStyle))
                    {
                        switch (Event.current.button)
                        {
                            case 1: // right click
                                if (teams.Current.Name != "Neutral" && teams.Current.Name != "A" && teams.Current.Name != "B")
                                    teams.Current.Neutral = !teams.Current.Neutral;
                                break;
                            default:
                                targetWeaponManager.SetTeam(teams.Current);
                                SetVisible(false);
                                break;
                        }
                    }
                    height += buttonHeight;
                }

            if (scrollable)
                GUI.EndScrollView();

            // Buttons
            if (Event.current.type == EventType.KeyUp)
            {
                if ((Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && !string.IsNullOrEmpty(newTeamName.Trim()))
                {
                    targetWeaponManager.SetTeam(BDTeam.Get(newTeamName.Trim()));
                    SetVisible(false);
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    SetVisible(false);
                }
            }

            height += margin;
            GUIUtils.RepositionWindow(ref window);
            GUIUtils.UseMouseEventInRect(window);
        }

        protected virtual void OnGUI()
        {
            if (ready)
            {
                if (open && BDArmorySetup.GAME_UI_ENABLED
                    && Event.current.type == EventType.MouseDown
                    && !window.Contains(Event.current.mousePosition))
                {
                    SetVisible(false);
                }

                if (open && BDArmorySetup.GAME_UI_ENABLED)
                {
                    var clientRect = new Rect(
                        Mathf.Min(windowLocation.x, Screen.width - (scrollable ? width + scrollWidth : width)),
                        Mathf.Min(windowLocation.y, Screen.height - height),
                        width,
                        scrollable ? Screen.height / 2 + buttonHeight + buttonGap + 2 * margin : height);
                    if (BDArmorySettings.UI_SCALE != 1) GUIUtility.ScaleAroundPivot(BDArmorySettings.UI_SCALE * Vector2.one, clientRect.position);
                    window = GUI.Window(10591029, clientRect, TeamSelectorWindow, "", BDArmorySetup.BDGuiSkin.window);
                    GUIUtils.UpdateGUIRect(window, guiCheckIndex);
                }
                else
                {
                    GUIUtils.UpdateGUIRect(new Rect(), guiCheckIndex);
                }
            }
        }

        private void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(WaitForBdaSettings());
        }

        private void OnDestroy()
        {
            ready = false;
        }

        private IEnumerator WaitForBdaSettings()
        {
            yield return new WaitUntil(() => BDArmorySetup.Instance is not null);

            ready = true;
            if (guiCheckIndex < 0) guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
        }
    }
}
