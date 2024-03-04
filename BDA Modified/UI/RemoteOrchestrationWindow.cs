using System.Collections;
using UnityEngine;
using KSP.Localization;

using BDArmory.Competition.RemoteOrchestration;
using BDArmory.Settings;
using BDArmory.Utils;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RemoteOrchestrationWindow : MonoBehaviour
    {
        public static RemoteOrchestrationWindow Instance;
        private BDAScoreService service;
        private BDAScoreClient client;

        private static int _guiCheckIndex = -1;

        private readonly float _titleHeight = 30;
        private readonly float _margin = 5;

        private float _windowHeight; //auto adjusting

        private bool ready = false;
        private bool showWindow = true;

        private string status;
        private string competition;
        private string stage;
        private string heat;

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;
        }

        private void Start()
        {
            ready = false;
        }

        private void Update()
        {
            if (!ready) return;
            status = client.competition == null ? "Offline" : service.Status();
        }

        private void OnGUI()
        {
            if (!(showWindow && ready && (BDArmorySetup.GAME_UI_ENABLED || (BDArmorySettings.VESSEL_SWITCHER_PERSIST_UI && !BDArmorySetup.GAME_UI_ENABLED)) && BDArmorySettings.REMOTE_LOGGING_ENABLED))
                return;

            SetNewHeight(_windowHeight);
            BDArmorySetup.WindowRectRemoteOrchestration = new Rect(
                BDArmorySetup.WindowRectRemoteOrchestration.x,
                BDArmorySetup.WindowRectRemoteOrchestration.y,
                BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH,
                _windowHeight
            );
            BDArmorySetup.SetGUIOpacity();
            if (BDArmorySettings.UI_SCALE != 1) GUIUtility.ScaleAroundPivot(BDArmorySettings.UI_SCALE * Vector2.one, BDArmorySetup.WindowRectRemoteOrchestration.position);
            BDArmorySetup.WindowRectRemoteOrchestration = GUI.Window(
                80085,
                BDArmorySetup.WindowRectRemoteOrchestration,
                WindowRemoteOrchestration,
                StringUtils.Localize("#LOC_BDArmory_BDARemoteOrchestration_Title"),//"BDA Remote Orchestration"
                BDArmorySetup.BDGuiSkin.window
            );
            BDArmorySetup.SetGUIOpacity(false);
            GUIUtils.UpdateGUIRect(BDArmorySetup.WindowRectRemoteOrchestration, _guiCheckIndex);
        }

        private void SetNewHeight(float windowHeight)
        {
            BDArmorySetup.WindowRectRemoteOrchestration.height = windowHeight;
        }

        public void UpdateClientStatus()
        {
            client = service.client;
            competition = client.competition == null ? "-" : client.competition.name;
            stage = client.activeHeat == null ? "-" : client.activeHeat.stage.ToString();
            heat = client.activeHeat == null ? "-" : client.activeHeat.order.ToString();

        }

        public void ShowWindow()
        {
            if (!ready)
                StartCoroutine(WaitForSetup());
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            showWindow = visible;
            GUIUtils.SetGUIRectVisible(_guiCheckIndex, visible);
        }

        private IEnumerator WaitForSetup()
        {
            yield return new WaitWhile(() => BDArmorySetup.Instance == null || BDAScoreService.Instance == null || BDAScoreService.Instance.client == null);
            service = BDAScoreService.Instance;
            UpdateClientStatus();
            ready = true;
            if (_guiCheckIndex < 0) _guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
        }

        private void WindowRemoteOrchestration(int id)
        {
            GUI.DragWindow(new Rect(0, 0, BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH - _titleHeight / 2 - 2, _titleHeight));
            if (GUI.Button(new Rect(BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH - _titleHeight / 2 - 2, 2, _titleHeight / 2, _titleHeight / 2), "X", BDArmorySetup.BDGuiSkin.button))
            {
                SetVisible(false);
            }

            float offset = _titleHeight + _margin;
            float width = BDArmorySettings.REMOTE_ORCHESTRATION_WINDOW_WIDTH;
            float fifth = width / 5.0f;
            GUI.Label(new Rect(_margin, offset, 2 * fifth, _titleHeight), "Competition: ");
            GUI.Label(new Rect(_margin + 2 * fifth, offset, 3 * fifth, _titleHeight), competition);
            offset += _titleHeight;
            GUI.Label(new Rect(_margin, offset, 2 * fifth, _titleHeight), "Status: ");
            string statusLine;
            switch (service.status)
            {
                case BDAScoreService.StatusType.Waiting:
                    statusLine = status + " " + (service.TimeUntilNextHeat()).ToString("0") + "s";
                    break;
                default:
                    statusLine = status;
                    break;
            }
            GUI.Label(new Rect(_margin + 2 * fifth, offset, 3 * fifth, _titleHeight), statusLine);
            offset += _titleHeight;
            GUI.Label(new Rect(_margin, offset, 2 * fifth, _titleHeight), "Stage: ");
            GUI.Label(new Rect(_margin + 2 * fifth, offset, 3 * fifth, _titleHeight), stage);
            offset += _titleHeight;
            GUI.Label(new Rect(_margin, offset, 2 * fifth, _titleHeight), "Heat: ");
            GUI.Label(new Rect(_margin + 2 * fifth, offset, 3 * fifth, _titleHeight), heat);
            offset += _titleHeight;
            string buttonText;
            bool nextButton = false;
            switch (BDAScoreService.Instance.status)
            {
                case BDAScoreService.StatusType.Waiting:
                    buttonText = "Stop";
                    nextButton = true;
                    break;
                case BDAScoreService.StatusType.Stopped:
                case BDAScoreService.StatusType.Cancelled:
                    buttonText = "Next Heat";
                    break;
                default:
                    buttonText = "Cancel";
                    break;
            }
            if (GUI.Button(new Rect(_margin, offset, nextButton ? 2 * width / 3 - _margin : width - 2 * _margin, _titleHeight), buttonText, BDArmorySetup.BDGuiSkin.button))
            {
                switch (BDAScoreService.Instance.status)
                {
                    case BDAScoreService.StatusType.Stopped:
                    case BDAScoreService.StatusType.Cancelled:
                        service.Configure(service.vesselPath, BDArmorySettings.COMPETITION_HASH);
                        break;
                    default:
                        service.Cancel();
                        break;
                }
            }
            //if (nextButton && GUI.Button(new Rect(2 * width / 3, offset, width / 3 - _margin, _titleHeight), "Next", BDArmorySetup.BDGuiSkin.button))
            //{
            //    BDAScoreService.Instance.waitStartedAt = -1;
            //}
            offset += _titleHeight + _margin;

            _windowHeight = offset;

            GUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectRemoteOrchestration); // Prevent it from going off the screen edges.
        }
    }
}
