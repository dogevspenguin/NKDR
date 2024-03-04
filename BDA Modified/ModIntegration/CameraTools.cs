using BDArmory.Extensions;
using BDArmory.Utils;
using BDArmory.VesselSpawning;
using UnityEngine;

namespace BDArmory.ModIntegration
{
  class CameraTools
  {
    // Instead of directly affecting CameraTools, this provides the fields and properties that CameraTools can look for and interact with.
    public static bool InhibitCameraTools => VesselSpawnerStatus.vesselsSpawning; // Flag for CameraTools (currently just checks for vessels being spawned).
    public static float RestoreDistanceLimit { get; private set; } = 50f; // Limit to how far away to set the camera when restoring it due to BDA automatically enabling the camera.
    public static Vessel MissileTargetVessel // Get the current target of a missile that is the active vessel.
    {
      get
      {
        var vessel = FlightGlobals.ActiveVessel;
        if (vessel == null || !vessel.IsMissile()) return null;
        var mb = VesselModuleRegistry.GetMissileBase(vessel);
        if (mb == null) return null;
        var ti = mb.targetVessel;
        if (ti == null) return null;
        return ti.Vessel;
      }
    }
    public static Vector3 MissileTargetPosition // Get the current target position of a missile that is the active vessel (if the target isn't a vessel).
    {
      get
      {
        var vessel = FlightGlobals.ActiveVessel;
        if (vessel == null || !vessel.IsMissile()) return default;
        var mb = VesselModuleRegistry.GetMissileBase(vessel);
        if (mb == null) return default;
        return mb.TargetPosition;
      }
    }
  }
}