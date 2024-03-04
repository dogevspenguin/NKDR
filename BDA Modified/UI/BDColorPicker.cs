using KSP.Localization;
using UnityEngine;
using BDArmory.Utils;

// credit to Brian Jones (https://github.com/boj)& KSP ForumMember TaxiService
namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class TeamColorConfig : MonoBehaviour
    {
        private Texture2D displayPicker;
        public int displayTextureWidth = 360;
        public int displayTextureHeight = 360;

        public int HorizPos;
        public int VertPos;

        public Color selectedColor;
        private Texture2D selectedColorPreview;

        private float hueSlider = 0f;
        private float prevHueSlider = 0f;
        private Texture2D hueTexture;

        protected void Awake()
        {
            HorizPos = (Screen.width / 2) - (displayTextureWidth / 2);
            VertPos = (Screen.height / 2) - (displayTextureHeight / 2);

            renderColorPicker();

            hueTexture = new Texture2D(10, displayTextureHeight, TextureFormat.ARGB32, false);
            for (int x = 0; x < hueTexture.width; x++)
            {
                for (int y = 0; y < hueTexture.height; y++)
                {
                    float h = (y / (hueTexture.height * 1.0f)) * 1f;
                    hueTexture.SetPixel(x, y, new ColorHSV(h, 1f, 1f).ToColor());
                }
            }
            hueTexture.Apply();

            selectedColorPreview = new Texture2D(1, 1);
            selectedColorPreview.SetPixel(0, 0, selectedColor);
        }

        private void renderColorPicker()
        {
            Texture2D colorPicker = new Texture2D(displayTextureWidth, displayTextureHeight, TextureFormat.ARGB32, false);
            for (int x = 0; x < displayTextureWidth; x++)
            {
                for (int y = 0; y < displayTextureHeight; y++)
                {
                    float h = hueSlider;
                    float v = (y / (displayTextureHeight * 1.0f)) * 1f;
                    float s = (x / (displayTextureWidth * 1.0f)) * 1f;
                    colorPicker.SetPixel(x, y, new ColorHSV(h, s, v).ToColor());
                }
            }

            colorPicker.Apply();
            displayPicker = colorPicker;
        }

        protected void OnGUI()
        {
            if (!BDTISetup.Instance.showColorSelect) return;

            GUI.Box(new Rect(HorizPos - 3, VertPos - 3, displayTextureWidth + 60, displayTextureHeight + 60), "");

            if (hueSlider != prevHueSlider) // new Hue value
            {
                prevHueSlider = hueSlider;
                renderColorPicker();
            }

            if (GUI.RepeatButton(new Rect(HorizPos, VertPos, displayTextureWidth, displayTextureHeight), displayPicker))
            {
                int a = (int)Input.mousePosition.x;
                int b = Screen.height - (int)Input.mousePosition.y;

                selectedColor = displayPicker.GetPixel(a - HorizPos, -(b - VertPos));
            }

            hueSlider = GUI.VerticalSlider(new Rect(HorizPos + displayTextureWidth + 3, VertPos, 10, displayTextureHeight), hueSlider, 1, 0);
            GUI.Box(new Rect(HorizPos + displayTextureWidth + 20, VertPos, 20, displayTextureHeight), hueTexture);

            if (GUI.Button(new Rect(HorizPos + displayTextureWidth - 60, VertPos + displayTextureHeight + 10, 60, 25), StringUtils.Localize("#LOC_BDArmory_Icon_colorget")))
            {
                selectedColor = selectedColorPreview.GetPixel(0, 0);
                BDTISetup.Instance.showColorSelect = false;
                BDTISetup.Instance.UpdateTeamColor = true;
            }

            // box for chosen color
            GUIStyle style = new GUIStyle();
            selectedColorPreview.SetPixel(0, 0, selectedColor);
            selectedColorPreview.Apply();
            style.normal.background = selectedColorPreview;
            GUI.Box(new Rect(HorizPos + displayTextureWidth + 10, VertPos + displayTextureHeight + 10, 30, 30), new GUIContent(""), style);
        }
        float updateTimer;

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (BDTISetup.Instance.UpdateTeamColor)
            {
                updateTimer -= Time.deltaTime;
                if (updateTimer < 0)
                {
                    updateTimer = 1f;    //next update in half a sec only

                    if (BDTISetup.Instance.ColorAssignments.ContainsKey(BDTISetup.Instance.selectedTeam))
                    {
                        BDTISetup.Instance.ColorAssignments[BDTISetup.Instance.selectedTeam] = selectedColor;
                    }
                    else
                    {
                        Debug.Log("[TEAMICONS] Selected team is null.");
                    }
                    BDTISetup.Instance.UpdateTeamColor = false;
                }
            }
        }
    }
}