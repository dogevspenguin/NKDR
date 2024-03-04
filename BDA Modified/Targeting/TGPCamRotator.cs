using UnityEngine;

namespace BDArmory.Targeting
{
    public class TGPCamRotator : MonoBehaviour
    {
        void OnPreRender()
        {
            if (TargetingCamera.Instance)
            {
                TargetingCamera.Instance.UpdateCamRotation(transform);
            }
        }
    }
}
