using System.Collections;
using UnityEngine;
using KSP.Localization;

using BDArmory.Settings;
using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Evolution
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class EvolutionWindow : MonoBehaviour
    {
        public static EvolutionWindow Instance;
        private BDAModuleEvolution evolution;

        private static int _guiCheckIndex = -1;
        private static readonly float _buttonSize = 20;
        private static readonly float _margin = 5;
        private static readonly float _lineHeight = _buttonSize;
        private readonly float _titleHeight = 30;
        private float _windowHeight; //auto adjusting
        private float _windowWidth;
        public bool ready = false;
        private EvolutionStatus status;

        GUIStyle leftLabel;

        Rect SLineRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth - 2 * _margin, _lineHeight);
        }

        Rect SLeftSliderRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth / 2 + _margin / 2, _lineHeight);
        }

        Rect SRightSliderRect(float line)
        {
            return new Rect(_margin + _windowWidth / 2 + _margin / 2, line * _lineHeight, _windowWidth / 2 - 7 / 2 * _margin, _lineHeight);
        }

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;
        }

        private void Start()
        {
            leftLabel = new GUIStyle();
            leftLabel.alignment = TextAnchor.UpperLeft;
            leftLabel.normal.textColor = Color.white;

            ready = false;
            StartCoroutine(WaitForSetup());
        }

        private void Update()
        {
            if (!ready) return;
            status = evolution == null ? EvolutionStatus.Idle : evolution.Status();
        }

        private void OnGUI()
        {
            if (!(ready && BDArmorySettings.EVOLUTION_ENABLED && BDArmorySetup.Instance.showEvolutionGUI))
            {
                return;
            }

            _windowWidth = BDArmorySettings.EVOLUTION_WINDOW_WIDTH;

            SetNewHeight(_windowHeight);
            BDArmorySetup.WindowRectEvolution = new Rect(
                BDArmorySetup.WindowRectEvolution.x,
                BDArmorySetup.WindowRectEvolution.y,
                _windowWidth,
                _windowHeight
            );
            BDArmorySetup.SetGUIOpacity();
            if (BDArmorySettings.UI_SCALE != 1) GUIUtility.ScaleAroundPivot(BDArmorySettings.UI_SCALE * Vector2.one, BDArmorySetup.WindowRectEvolution.position);
            BDArmorySetup.WindowRectEvolution = GUI.Window(
                8008135,
                BDArmorySetup.WindowRectEvolution,
                WindowEvolution,
                StringUtils.Localize("#LOC_BDArmory_Evolution_Title"),//"BDA Evolution"
                BDArmorySetup.BDGuiSkin.window
            );
            BDArmorySetup.SetGUIOpacity(false);
            GUIUtils.UpdateGUIRect(BDArmorySetup.WindowRectEvolution, _guiCheckIndex);
        }

        private void SetNewHeight(float windowHeight)
        {
            BDArmorySetup.WindowRectEvolution.height = windowHeight;
        }

        public void SetVisible(bool visible)
        {
            BDArmorySetup.Instance.showEvolutionGUI = visible;
            GUIUtils.SetGUIRectVisible(_guiCheckIndex, visible);
        }

        private IEnumerator WaitForSetup()
        {
            while (BDArmorySetup.Instance == null || BDAModuleEvolution.Instance == null)
            {
                yield return null;
            }
            evolution = BDAModuleEvolution.Instance;

            BDArmorySetup.Instance.hasEvolution = true;
            ready = true;
            if (_guiCheckIndex < 0) _guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
        }

        private void WindowEvolution(int id)
        {
            float line = 0.25f;
            float offset = _titleHeight + _margin;

            GUI.DragWindow(new Rect(0, 0, BDArmorySettings.EVOLUTION_WINDOW_WIDTH - _titleHeight / 2 - 2, _titleHeight));
            if (GUI.Button(SLineRect(++line), (BDArmorySettings.SHOW_EVOLUTION_OPTIONS ? StringUtils.Localize("#LOC_BDArmory_Generic_Hide") : StringUtils.Localize("#LOC_BDArmory_Generic_Show")) + " " + StringUtils.Localize("#LOC_BDArmory_Evolution_Options"), BDArmorySettings.SHOW_EVOLUTION_OPTIONS ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Show/hide evolution options
            {
                BDArmorySettings.SHOW_EVOLUTION_OPTIONS = !BDArmorySettings.SHOW_EVOLUTION_OPTIONS;
            }
            if (BDArmorySettings.SHOW_EVOLUTION_OPTIONS)
            {
                int mutationsPerHeat = BDArmorySettings.EVOLUTION_MUTATIONS_PER_HEAT;
                var mphDisplayValue = BDArmorySettings.EVOLUTION_MUTATIONS_PER_HEAT.ToString("0");
                GUI.Label(SLeftSliderRect(++line), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_MutationsPerHeat")}:  ({mphDisplayValue})", leftLabel);//Mutations Per Heat
                mutationsPerHeat = (int)GUI.HorizontalSlider(SRightSliderRect(line), mutationsPerHeat, 1, 10);
                BDArmorySettings.EVOLUTION_MUTATIONS_PER_HEAT = mutationsPerHeat;

                int adversariesPerHeat = BDArmorySettings.EVOLUTION_ANTAGONISTS_PER_HEAT;
                var aphDisplayValue = BDArmorySettings.EVOLUTION_ANTAGONISTS_PER_HEAT.ToString("0");
                GUI.Label(SLeftSliderRect(++line), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_AdversariesPerHeat")}:  ({aphDisplayValue})", leftLabel);//Adversaries Per Heat
                adversariesPerHeat = (int)GUI.HorizontalSlider(SRightSliderRect(line), adversariesPerHeat, 0, 10);
                BDArmorySettings.EVOLUTION_ANTAGONISTS_PER_HEAT = adversariesPerHeat;

                int heatsPerGroup = BDArmorySettings.EVOLUTION_HEATS_PER_GROUP;
                var hpgDisplayValue = BDArmorySettings.EVOLUTION_HEATS_PER_GROUP.ToString("0");
                GUI.Label(SLeftSliderRect(++line), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_HeatsPerGroup")}:  ({hpgDisplayValue})", leftLabel);//Heats Per Group
                heatsPerGroup = (int)GUI.HorizontalSlider(SRightSliderRect(line), heatsPerGroup, 1, 50);
                BDArmorySettings.EVOLUTION_HEATS_PER_GROUP = heatsPerGroup;

                offset += 3 * _lineHeight;
            }

            float fifth = _windowWidth / 5.0f;
            offset += 0.25f * _lineHeight;
            GUI.Label(new Rect(_margin, offset, 2 * fifth, _lineHeight), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_ID")}: ");
            GUI.Label(new Rect(_margin + 2 * fifth, offset, 3 * fifth, _lineHeight), evolution.EvolutionId);
            offset += _lineHeight;
            GUI.Label(new Rect(_margin, offset, 2 * fifth, _lineHeight), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_Status")}: ");
            string statusLine;
            switch (evolution.Status())
            {
                default:
                    statusLine = status.ToString();
                    break;
            }
            GUI.Label(new Rect(_margin + 2 * fifth, offset, 3 * fifth, _lineHeight), statusLine);
            offset += _lineHeight;
            GUI.Label(new Rect(_margin, offset, fifth, _lineHeight), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_Group")}: ");
            GUI.Label(new Rect(_margin + fifth, offset, fifth, _lineHeight), evolution.GroupId.ToString());
            GUI.Label(new Rect(_margin + 2 * fifth, offset, fifth, _lineHeight), $"{StringUtils.Localize("#LOC_BDArmory_Evolution_Heat")}: ");
            GUI.Label(new Rect(_margin + 3 * fifth, offset, fifth, _lineHeight), evolution.Heat.ToString());
            offset += _lineHeight;
            string buttonText;
            bool nextButton = false;
            switch (status)
            {
                case EvolutionStatus.Idle:
                    buttonText = "Start";
                    nextButton = true;
                    break;
                default:
                    buttonText = "Cancel";
                    break;
            }
            if (GUI.Button(new Rect(_margin, offset, nextButton ? 2 * _windowWidth / 3 - _margin : _windowWidth - 2 * _margin, _lineHeight), buttonText, BDArmorySetup.BDGuiSkin.button))
            {
                switch (status)
                {
                    case EvolutionStatus.Idle:
                        evolution.StartEvolution();
                        break;
                    default:
                        evolution.StopEvolution();
                        break;
                }
            }
            offset += _lineHeight + _margin;

            _windowHeight = offset;

            GUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectEvolution); // Prevent it from going off the screen edges.
        }
    }
}
