#if DEBUG

// This will only be live in debug builds
using UnityEngine;

using BDArmory.Settings;
using BDArmory.Utils;
using System.Text;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KrakensbaneDebug : MonoBehaviour
    {
        float lastShift = 0;
        Vector3 lastOffsetAmount = default;
        StringBuilder debugString = new();

        void FixedUpdate()
        {
            if (BDKrakensbane.IsActive)
            {
                lastShift = Time.time;
                lastOffsetAmount = BDKrakensbane.FloatingOriginOffset;
                // Debug.Log($"DEBUG {Time.time} Krakensbane shifted by {BDKrakensbane.FloatingOriginOffset:G3} ({(Vector3)FloatingOrigin.Offset:G3}) | N-Kb {BDKrakensbane.FloatingOriginOffsetNonKrakensbane:G3} ({(Vector3)FloatingOrigin.OffsetNonKrakensbane:G3}) | V3f: {BDKrakensbane.FrameVelocityV3f:G3} ({Krakensbane.GetFrameVelocityV3f():G3})");
            }
        }

        void OnGUI()
        {
            if (BDArmorySettings.DEBUG_TELEMETRY)
            {
                debugString.Clear();
                var frameVelocity = BDKrakensbane.FrameVelocityV3f;
                //var rFrameVelocity = FlightGlobals.currentMainBody.getRFrmVel(Vector3d.zero);
                //var rFrameRotation = rFrameVelocity - FlightGlobals.currentMainBody.getRFrmVel(VectorUtils.GetUpDirection(Vector3.zero));
                debugString.AppendLine($"Frame velocity: {frameVelocity.magnitude} ({frameVelocity})");
                debugString.AppendLine($"FO offset: {(Vector3)BDKrakensbane.FloatingOriginOffset:G3}");
                debugString.AppendLine($"N-Kb offset: {(Vector3)BDKrakensbane.FloatingOriginOffsetNonKrakensbane:G3}");
                debugString.AppendLine($"Last offset {Time.time - lastShift}s ago");
                debugString.AppendLine($"Last offset amount {lastOffsetAmount:G3}");
                debugString.AppendLine($"Local vessel speed: {FlightGlobals.ActiveVessel.rb_velocity.magnitude}, ({FlightGlobals.ActiveVessel.rb_velocity})");
                // debugString.AppendLine($"Reference frame speed: {rFrameVelocity}");
                // debugString.AppendLine($"Reference frame rotation speed: {rFrameRotation}");
                // debugString.AppendLine($"Reference frame angular speed: {rFrameRotation.magnitude / Mathf.PI * 180}");
                // debugString.AppendLine($"Ref frame is {(FlightGlobals.RefFrameIsRotating ? "" : "not ")}rotating");
                GUI.Label(new Rect(10, 150, 400, 400), debugString.ToString());
            }
        }
    }
}

#endif
