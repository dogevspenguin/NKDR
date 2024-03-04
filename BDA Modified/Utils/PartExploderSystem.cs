using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using BDArmory.Extensions;
using BDArmory.Settings;

namespace BDArmory.Utils
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class PartExploderSystem : MonoBehaviour
    {
        private static readonly HashSet<Part> ExplodingParts = new HashSet<Part>();
        private static List<Part> nowExploding = new List<Part>();

        public static void AddPartToExplode(Part p)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (p == null) return;
            ExplodingParts.Add(p);
        }

        private void OnDestroy()
        {
            ExplodingParts.Clear();
        }

        public void Update()
        {
            if (ExplodingParts.Count == 0) return;

            do
            {
                // Remove parts that are already gone.
                nowExploding.AddRange(ExplodingParts.Where(p => p is null || p.packed || (p.vessel is not null && !p.vessel.loaded)));
                ExplodingParts.ExceptWith(nowExploding);
                nowExploding.Clear();
                // Explode outer-most parts first to avoid creating new vessels needlessly.
                nowExploding.AddRange(ExplodingParts.Where(p => !ExplodingParts.Contains(p.parent)));
                foreach (var part in nowExploding)
                    part.explode();
                ExplodingParts.ExceptWith(nowExploding);
                nowExploding.Clear();
            } while (ExplodingParts.Count > 0);
        }
    }
}
