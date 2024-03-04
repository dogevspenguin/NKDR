using UnityEngine;

using BDArmory.UI;
using BDArmory.Settings;

namespace BDArmory.Damage
{
    public class ModuleMassAdjust : PartModule, IPartMassModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => massMod;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        public float massMod = 0f; //mass to add to part, in tons
        public float duration = 15; //duration of effect, in seconds
        private float startMass = 0;
        private bool hasSetup = false;

        private void EndEffect()
        {
            massMod = 0;
            part.RemoveModule(this);
            //Debug.Log("[BDArmory.ModuleMassAdjust]: ME field expired, " + this.part.name + "mass: " + this.part.mass);
        }

        void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (BDArmorySetup.GameIsPaused) return;

            duration -= TimeWarp.fixedDeltaTime;

            if (duration <= 0)
            {
                EndEffect();
            }
            if (!hasSetup)
            {
                SetupME();
            }
        }

        private void SetupME()
        {
            startMass = this.part.mass;
            hasSetup = true;
            if (BDArmorySettings.DEBUG_DAMAGE) Debug.Log("[BDArmory.ModuleMassAdjust]: Applying ME field to " + this.part.name + ", orig mass: " + startMass + ", massMod = " + massMod);

            if (massMod < 0) //for negative mass modifier - i.e. MassEffect sytyle antigrav/weight reduction
            {
                massMod = Mathf.Clamp(massMod, (startMass * 0.95f * -1), 0); //clamp mod mass to min of 5% of original value to prevent negative mass and whatever Kraken that summons
            }
        }
    }
}
