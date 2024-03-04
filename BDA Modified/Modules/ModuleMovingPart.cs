using System.Collections;
using UnityEngine;

using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Modules
{
    public class ModuleMovingPart : PartModule
    {
        Transform parentTransform;
        [KSPField] public string parentTransformName = string.Empty;

        bool setupComplete;

        Part[] children;
        Vector3[] localAnchors;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (parentTransformName == string.Empty)
                {
                    enabled = false;
                    return;
                }

                parentTransform = part.FindModelTransform(parentTransformName);

                StartCoroutine(SetupRoutine());
            }
        }

        void FixedUpdate()
        {
            if (setupComplete)
            {
                UpdateJoints();
            }
        }

        IEnumerator SetupRoutine()
        {
            yield return new WaitWhile(() => vessel is not null && (vessel.packed || !vessel.loaded));
            yield return new WaitForFixedUpdate();
            SetupJoints();
        }

        void SetupJoints()
        {
            children = part.children.ToArray();
            localAnchors = new Vector3[children.Length];

            for (int i = 0; i < children.Length; i++)
            {
                children[i].attachJoint.Joint.autoConfigureConnectedAnchor = false;
                Vector3 connectedAnchor = children[i].attachJoint.Joint.connectedAnchor;
                Vector3 worldAnchor =
                    children[i].attachJoint.Joint.connectedBody.transform.TransformPoint(connectedAnchor);
                Vector3 localAnchor = parentTransform.InverseTransformPoint(worldAnchor);
                localAnchors[i] = localAnchor;
            }

            setupComplete = true;
        }

        void UpdateJoints()
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (!children[i]) continue;

                Vector3 newWorldAnchor = parentTransform.TransformPoint(localAnchors[i]);
                Vector3 newConnectedAnchor =
                    children[i].attachJoint.Joint.connectedBody.transform.InverseTransformPoint(newWorldAnchor);
                children[i].attachJoint.Joint.connectedAnchor = newConnectedAnchor;
            }
        }

        void OnGUI()
        {
            if (setupComplete)
            {
                for (int i = 0; i < localAnchors.Length; i++)
                {
                    GUIUtils.DrawTextureOnWorldPos(parentTransform.TransformPoint(localAnchors[i]),
                        BDArmorySetup.Instance.greenDotTexture, new Vector2(6, 6), 0);
                }
            }
        }
    }
}
