using UnityEngine;

using BDArmory.Damage;

namespace BDArmory.Initialization
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Bootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            Dependencies.Register<DamageService, ModuleDamageService>();
        }
    }
}
