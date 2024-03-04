using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BDArmory.Damage;
using KSP.Localization;
using BDArmory.Utils;
using BDArmory.Settings;

namespace BDArmory.Armor
{
    public class BDAdjustableArmor : PartModule
    {
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorAdjustParts"),//Move Child Parts
            UI_Toggle(disabledText = "#LOC_BDArmory_false", enabledText = "#LOC_BDArmory_true")]//false--true
        public bool moveChildParts = true;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorWidth"),//Armor Width
            UI_FloatSemiLogRange(minValue = 0.1f, maxValue = 16, scene = UI_Scene.Editor)]
        public float Width = 1;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_ArmorWidthR"),//Right Side Width
            UI_FloatSemiLogRange(minValue = 0.1f, maxValue = 8, scene = UI_Scene.Editor)]
        public float scaleneWidth = 1;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ArmorLength"),//Armor Length
            UI_FloatSemiLogRange(minValue = 0.1f, maxValue = 16, scene = UI_Scene.Editor)]
        public float Length = 1;

        [KSPField]
        public float maxScale = 16;

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_ArmorTriIso", active = true)]//Toggle Tri Type
        public void ToggleTriTypeOption() => ToggleTriTypeOptionHandler();
        void ToggleTriTypeOptionHandler(bool applySym = true, Toggle state = Toggle.Toggle)
        {
            switch (state)
            {
                case Toggle.Toggle:
                    scaleneTri = !scaleneTri;
                    break;
                case Toggle.NoChange:
                    break;
                case Toggle.Off:
                    scaleneTri = false;
                    break;
                case Toggle.On:
                    scaleneTri = true;
                    break;
            }

            Fields["scaleneWidth"].guiActiveEditor = scaleneTri;
            UI_FloatSemiLogRange AWidth = (UI_FloatSemiLogRange)Fields["Width"].uiControlEditor;
            AWidth.UpdateLimits(clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, scaleneTri ? clamped ? maxScale / 2 : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y / 2 : clamped ? maxScale : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y);

            if (scaleneTri)
            {
                Fields["Width"].guiName = StringUtils.Localize("#LOC_BDArmory_ArmorWidthL");
                Events["ToggleTriTypeOption"].guiName = StringUtils.Localize("#LOC_BDArmory_ArmorTriSca");
            }
            else
            {
                Fields["Width"].guiName = StringUtils.Localize("#LOC_BDArmory_ArmorWidth");
                Events["ToggleTriTypeOption"].guiName = StringUtils.Localize("#LOC_BDArmory_ArmorTriIso");
            }
            GUIUtils.RefreshAssociatedWindows(part);
            if (applySym)
            {
                using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                    while (sym.MoveNext())
                    {
                        if (sym.Current == null) continue;
                        sym.Current.FindModuleImplementing<BDAdjustableArmor>().ToggleTriTypeOptionHandler(false, state);
                    }
            }
        }

        [KSPEvent(active = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_UnclampTuning_disabledText")]//Toggle scale limit
        public void ToggleScaleClamp() => ToggleScaleClampHandler();
        public void ToggleScaleClampHandler(bool applySym = true, Toggle state = Toggle.Toggle)
        {
            switch (state)
            {
                case Toggle.Toggle:
                    clamped = !clamped;
                    break;
                case Toggle.NoChange:
                    break;
                case Toggle.Off:
                    clamped = false;
                    break;
                case Toggle.On:
                    clamped = true;
                    break;
            }

            UI_FloatSemiLogRange AWidth = (UI_FloatSemiLogRange)Fields["Width"].uiControlEditor;
            AWidth.UpdateLimits(clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, scaleneTri ? clamped ? maxScale / 2 : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y / 2 : clamped ? maxScale : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y);
            UI_FloatSemiLogRange ALength = (UI_FloatSemiLogRange)Fields["Length"].uiControlEditor;
            ALength.UpdateLimits(clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, clamped ? maxScale : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y);
            UI_FloatSemiLogRange SWidth = (UI_FloatSemiLogRange)Fields["scaleneWidth"].uiControlEditor;
            SWidth.UpdateLimits(clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, clamped ? maxScale / 2 : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y / 2);

            if (!clamped)
            {
                Events["ToggleScaleClamp"].guiName = StringUtils.Localize("#LOC_BDArmory_UnclampTuning_enabledText");
            }
            else
            {
                Events["ToggleScaleClamp"].guiName = StringUtils.Localize("#LOC_BDArmory_UnclampTuning_disabledText");
            }
            GUIUtils.RefreshAssociatedWindows(part);
            if (applySym)
            {
                using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                    while (sym.MoveNext())
                    {
                        if (sym.Current == null) continue;
                        sym.Current.FindModuleImplementing<BDAdjustableArmor>().ToggleScaleClampHandler(false, state);
                    }
            }
        }

        [KSPField(isPersistant = true)]
        bool clamped = true;

        //public bool isCurvedPanel = false;
        private float armorthickness = 1;
        private float Oldthickness = 1;

        [KSPField]
        public bool isTriangularPanel = false;

        [KSPField(isPersistant = true)]
        bool scaleneTri = false;

        [KSPField]
        public string TriangleType = "none";

        [KSPField]
        public string ArmorTransformName = "ArmorTransform"; //transform of armor panel mesh/box collider
        Transform[] armorTransforms;

        [KSPField]
        public string ScaleneTransformName = "ScaleneTransform"; //transform of armor panel mesh/box collider
        Transform[] scaleneTransforms;

        [KSPField]
        public string ThicknessTransformName = "ThicknessTransform"; //name of armature to control thickness of curved panels
        Transform ThicknessTransform;

        [KSPField] public string stackNodePosition;

        Dictionary<string, Vector3> originalStackNodePosition;

        HitpointTracker armor;

        public override void OnStart(StartState state)
        {
            armorTransforms = part.FindModelTransforms(ArmorTransformName);
            ThicknessTransform = part.FindModelTransform(ThicknessTransformName);
            if (isTriangularPanel && TriangleType != "Right")
            {
                Events["ToggleTriTypeOption"].guiActiveEditor = true;
                scaleneTransforms = part.FindModelTransforms(ScaleneTransformName);
                UI_FloatSemiLogRange SWidth = (UI_FloatSemiLogRange)Fields["scaleneWidth"].uiControlEditor;
                SWidth.onFieldChanged = AdjustSWidth;
                // SWidth.UpdateLimits(0.1f, maxScale / 2);
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                ParseStackNodePosition();
                StartCoroutine(DelayedUpdateStackNode());
                GameEvents.onEditorShipModified.Add(OnEditorShipModifiedEvent);
                ToggleTriTypeOptionHandler(state: Toggle.NoChange); // Initialise the UI for the Triangle Type toggle
                ToggleScaleClampHandler(state: Toggle.NoChange); // Initialise the UI for the Clamped toggle
            }
            UpdateThickness(true);
            UI_FloatSemiLogRange AWidth = (UI_FloatSemiLogRange)Fields["Width"].uiControlEditor;
            AWidth.onFieldChanged = AdjustWidth;
            // AWidth.UpdateLimits(0.1f, maxScale);
            UI_FloatSemiLogRange ALength = (UI_FloatSemiLogRange)Fields["Length"].uiControlEditor;
            ALength.onFieldChanged = AdjustLength;
            // ALength.UpdateLimits(0.1f, maxScale);

            armor = GetComponent<HitpointTracker>();
            UpdateScale(Width, Length, scaleneWidth, false);
            GUIUtils.RefreshAssociatedWindows(part);
        }
        void ParseStackNodePosition()
        {
            originalStackNodePosition = new Dictionary<string, Vector3>();
            string[] nodes = stackNodePosition.Split(new char[] { ';' });
            for (int i = 0; i < nodes.Length; i++)
            {
                string[] split = nodes[i].Split(new char[] { ',' });
                string id = split[0];
                Vector3 position = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
                originalStackNodePosition.Add(id, position);
            }
        }

        IEnumerator DelayedUpdateStackNode()
        {
            yield return null;
            UpdateStackNode(false);
        }

        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorShipModifiedEvent);
        }

        public void AdjustWidth(BaseField field, object obj)
        {
            Width = Mathf.Clamp(Width, clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, scaleneTri ? clamped ? maxScale / 2 : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y / 2 : clamped ? maxScale : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y);
            for (int i = 0; i < armorTransforms.Length; i++)
            {
                armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
            }
            if (isTriangularPanel && TriangleType != "Right" && !scaleneTri)
            {
                for (int i = 0; i < scaleneTransforms.Length; i++)
                {
                    scaleneTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
                }
            }
            using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                while (sym.MoveNext())
                {
                    if (sym.Current == null) continue;
                    sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Width, Length, scaleneWidth); //needs to be changed to use updatewitth() - FIXME later, future SI
                }
            updateArmorStats();
            UpdateStackNode(true);
        }
        public void AdjustSWidth(BaseField field, object obj)
        {
            scaleneWidth = Mathf.Clamp(scaleneWidth, clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, clamped ? maxScale / 2 : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y / 2);
            for (int i = 0; i < scaleneTransforms.Length; i++)
            {
                scaleneTransforms[i].localScale = new Vector3(scaleneWidth * 2, Length, armorthickness);
            }
            using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                while (sym.MoveNext())
                {
                    if (sym.Current == null) continue;
                    sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Width, Length, scaleneWidth); //needs to be changed to use updatewitth() - FIXME later, future SI
                }
            updateArmorStats();
            UpdateStackNode(true);
        }
        public void AdjustLength(BaseField field, object obj)
        {
            Length = Mathf.Clamp(Length, clamped ? 0.1f : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.x, clamped ? maxScale : BDArmorySettings.PROC_ARMOR_ALT_LIMITS.y);
            for (int i = 0; i < armorTransforms.Length; i++)
            {
                armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
            }
            if (isTriangularPanel && TriangleType != "Right")
            {
                for (int i = 0; i < scaleneTransforms.Length; i++)
                {
                    scaleneTransforms[i].localScale = new Vector3(scaleneWidth, Length, armorthickness);
                }
            }
            using (List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator())
                while (sym.MoveNext())
                {
                    if (sym.Current == null) continue;
                    sym.Current.FindModuleImplementing<BDAdjustableArmor>().UpdateScale(Width, Length, scaleneWidth);
                }
            updateArmorStats();
            UpdateStackNode(true);
        }

        public void UpdateScale(float width, float length, float scalenewidth = 1, bool updateNodes = true)
        {
            Width = width;
            scaleneWidth = scalenewidth;
            Length = length;

            for (int i = 0; i < armorTransforms.Length; i++)
            {
                armorTransforms[i].localScale = new Vector3(Width, Length, Mathf.Clamp((armor.Armor / 10), 0.1f, 1500));
            }
            if (isTriangularPanel && TriangleType != "Right")
            {
                for (int i = 0; i < scaleneTransforms.Length; i++)
                {
                    scaleneTransforms[i].localScale = new Vector3(scaleneTri ? scaleneWidth : Width, Length, Mathf.Clamp((armor.Armor / 10), 0.1f, 1500));
                }
            }
            updateArmorStats();
            if (updateNodes) UpdateStackNode(true);
        }
        IEnumerator updateDrag()
        {
            yield return null;
            DragCube DragCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
            part.DragCubes.ClearCubes();
            part.DragCubes.Cubes.Add(DragCube);
            part.DragCubes.ResetCubeWeights();
            part.DragCubes.ForceUpdate(true, true, false);
            part.DragCubes.SetDragWeights();
            if (HighLogic.LoadedSceneIsEditor) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }
        public void UpdateStackNode(bool translateChidren)
        {
            using (List<AttachNode>.Enumerator stackNode = part.attachNodes.GetEnumerator())
                while (stackNode.MoveNext())
                {
                    if (stackNode.Current?.nodeType != AttachNode.NodeType.Stack ||
                        !originalStackNodePosition.ContainsKey(stackNode.Current.id)) continue;

                    if (stackNode.Current.id == "top" || stackNode.Current.id == "bottom")
                    {
                        Vector3 prevPos = stackNode.Current.position;
                        Vector3 prevAngle = stackNode.Current.orientation;
                        int offsetScale = 2;
                        if (isTriangularPanel && TriangleType != "Right" && !scaleneTri)
                        {
                            offsetScale = 4;
                        }
                        if (stackNode.Current.id == "top")
                        {
                            stackNode.Current.size = Mathf.CeilToInt(Width / 2);
                            stackNode.Current.breakingForce = Width * 100;
                            stackNode.Current.breakingTorque = Width * 100;
                            stackNode.Current.position.x = originalStackNodePosition[stackNode.Current.id].x + (((Width - 1) / (scaleneTri ? 2 : 1)) / offsetScale); //if eqi tri this needs to be /4
                            if (isTriangularPanel) stackNode.Current.orientation = new Vector3(1, 0, -((Width / 2) / Length));
                            if (translateChidren) MoveParts(stackNode.Current, stackNode.Current.position - prevPos, stackNode.Current.orientation - prevAngle);
                        }
                        else
                        {
                            stackNode.Current.size = Mathf.CeilToInt(scaleneTri ? scaleneWidth / 2 : Width / 2);
                            stackNode.Current.breakingForce = scaleneTri ? scaleneWidth : Width * 100;
                            stackNode.Current.breakingTorque = scaleneTri ? scaleneWidth : Width * 100;
                            stackNode.Current.position.x = originalStackNodePosition[stackNode.Current.id].x - ((scaleneTri ? ((scaleneWidth - 1) / 2) : Width - 1) / offsetScale);// and a right tri hypotenuse node shouldn't move at all
                            if (isTriangularPanel && TriangleType != "Right")
                            {
                                stackNode.Current.orientation = new Vector3(-1, 0, -(((scaleneTri ? scaleneWidth : Width) / 2) / Length));
                            }
                            if (translateChidren) MoveParts(stackNode.Current, stackNode.Current.position - prevPos, stackNode.Current.orientation - prevAngle); //look into making triangle side nodes rotate attachnode based on new angle? AttachNode.Orientation?                            
                        }
                    }
                    else if (stackNode.Current.id == "left" || stackNode.Current.id == "right")
                    {
                        stackNode.Current.size = Mathf.CeilToInt(Length / 2);
                        stackNode.Current.breakingForce = Length * 100;
                        stackNode.Current.breakingTorque = Length * 100;
                        Vector3 prevPos = stackNode.Current.position;
                        if (stackNode.Current.id == "right")
                        {
                            stackNode.Current.position.z = originalStackNodePosition[stackNode.Current.id].z + ((Length - 1) / 2);
                            if (translateChidren) MoveParts(stackNode.Current, stackNode.Current.position - prevPos, Vector3.zero);
                        }
                        else
                        {
                            stackNode.Current.position.z = originalStackNodePosition[stackNode.Current.id].z - ((Length - 1) / 2);
                            if (translateChidren) MoveParts(stackNode.Current, stackNode.Current.position - prevPos, Vector3.zero);
                        }
                    }
                    else if (stackNode.Current.id == "side")
                    {
                        stackNode.Current.size = Mathf.CeilToInt(((Width / 2) + (Length / 2)) / 2);
                        stackNode.Current.orientation = new Vector3(1, 0, -(Width / Length));
                    }
                }
        }
        public void MoveParts(AttachNode node, Vector3 delta, Vector3 angleDelta)
        {
            if (!moveChildParts) return;
            if (node.attachedPart is Part pushTarget)
            {
                if (pushTarget == null) return;
                Vector3 worldDelta = part.transform.TransformVector(delta);
                pushTarget.transform.position += worldDelta;
                //Vector3 worldAngle = part.transform.TransformVector(angleDelta);
                //pushTarget.transform.rotation += worldAngle;
            }
        }
        public void updateArmorStats()
        {
            armor.armorVolume = ((scaleneTri ? scaleneWidth + Width : Width) * Length);
            if (isTriangularPanel)
            {
                armor.armorVolume /= 2;
            }
            armor.ArmorSetup(null, null);
            StartCoroutine(updateDrag());
        }
        void UpdateThickness(bool onLoad = false)
        {
            if (armor != null && armorTransforms != null)
            {
                armorthickness = Mathf.Clamp((armor.Armor / 10), 0.1f, 1500);

                if (armorthickness != Oldthickness)
                {
                    for (int i = 0; i < armorTransforms.Length; i++)
                    {
                        armorTransforms[i].localScale = new Vector3(Width, Length, armorthickness);
                    }
                    if (isTriangularPanel && TriangleType != "Right")
                    {
                        for (int i = 0; i < scaleneTransforms.Length; i++)
                        {
                            scaleneTransforms[i].localScale = new Vector3(scaleneTri ? scaleneWidth : Width, Length, armorthickness);
                        }
                    }
                }
            }
            else
            {
                //if (armor == null) Debug.Log("[BDAAdjustableArmor] No HitpointTracker found! aborting UpdateThickness()!");
                //if (armorTransforms == null) Debug.Log("[BDAAdjustableArmor] No ArmorTransform found! aborting UpdateThickness()!");
                return;
            }
            if (onLoad) return; //don't adjust part placement on load
            /*
            if (armorthickness != Oldthickness)
            {
                float ratio = (armorthickness - Oldthickness) / 100;

                Vector3 prevPos = new Vector3(0f, Oldthickness / 100, 0f);
                Vector3 delta = new Vector3(0f, armorthickness / 100, 0f);
                Vector3 worldDelta = part.transform.TransformVector(delta);
                List<Part>.Enumerator p = part.children.GetEnumerator();
                while (p.MoveNext())
                {
                    if (p.Current == null) continue;
                    if (p.Current.FindAttachNodeByPart(part) is AttachNode node && node.nodeType == AttachNode.NodeType.Surface)
                    {

                        p.Current.transform.position += worldDelta;
                    }
                }
                Oldthickness = armorthickness;
            }
            */
        }
        private void OnEditorShipModifiedEvent(ShipConstruct data)
        {
            UpdateThickness();
        }
    }
}