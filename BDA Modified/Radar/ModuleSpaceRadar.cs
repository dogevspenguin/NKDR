using BDArmory.Extensions;

namespace BDArmory.Radar
{
    public class ModuleSpaceRadar : ModuleRadar
    {
        void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateRadar();
        }

        // This code determines if the radar is below the cutoff altitude and if so then it disables the radar...
        void UpdateRadar()
        {
            if (!radarEnabled) return;
            if (!vessel.InVacuum()) // above an atm density of 0.007 the radar will not work
            {
                DisableRadar(); // disable the radar
            }
        }
    }
}
