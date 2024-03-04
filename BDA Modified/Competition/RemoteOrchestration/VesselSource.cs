using System;
namespace BDArmory.Competition.RemoteOrchestration
{
    public interface VesselSource
    {
        VesselModel GetVessel(int id);
        string GetLocalPath(int id);
    }
}
