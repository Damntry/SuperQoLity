using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model {

    public interface IImpactHandler<T> where T : IImpactData {
        List<IImpactData> ImpactCollection { get; }

        bool IsImpactFinished { get; }

        void AddNewImpactEntry(T impactData);

        void UpdateFade(float currentTime);
    }

    public class ImpactHitMarkHandler(float currentTime, Color color,
            IImpactSettings impactSettings, Material matReference)
            
            : ImpactGroupData(currentTime, color, impactSettings, matReference), IImpactHandler<IImpactData> {

        public List<IImpactData> ImpactCollection { get; } = new();


        public bool IsImpactFinished { get; private set; }


        public void AddNewImpactEntry(IImpactData impactData) {
            if (impactData is not HitMarkData) {
                throw new InvalidOperationException($"The parameter type must be {nameof(HitMarkData)}");
            }
            ImpactCollection.Add(impactData);

            OnImpactAdded?.Invoke();
        }

        public void UpdateFade(float currentTime) {
            float reverseProgress = ReverseProgress(currentTime);
            if (reverseProgress <= 0f) {
                IsImpactFinished = true;
                return;
            }
            
            matReference.SetColor("_TintColor", color * reverseProgress);
        }

    }

    public class ImpactSparksHandler(float currentTime, Color color, 
            IImpactSettings impactSettings, Material matReference)
            
            : ImpactGroupData(currentTime, color, impactSettings, matReference), IImpactHandler<IImpactData> {

        public List<IImpactData> ImpactCollection { get; } = new();


        public bool IsImpactFinished { get; private set; }


        public void AddNewImpactEntry(IImpactData impactData) {
            if (impactData is not SparkData) {
                throw new InvalidOperationException($"The parameter type must be {nameof(SparkData)}");
            }
            ImpactCollection.Add(impactData);

            OnImpactAdded?.Invoke();
        }

        public void UpdateFade(float currentTime) {
            float reverseProgress = ReverseProgress(currentTime);
            if (reverseProgress <= 0f) {
                IsImpactFinished = true;
                return;
            }

            //Slower fading than the real one
            float fadeProgress = impactSettings.MinFadeProgress + reverseProgress * impactSettings.MinFadeProgress;
            matReference.SetColor("_TintColor", color * fadeProgress);
        }


    }


    public abstract class ImpactGroupData(float currentTime, Color color,
            IImpactSettings impactSettings, Material matReference) {

        public static Action OnImpactAdded { get; set; }


        protected readonly float timeStart = currentTime;

        protected float lifeTime = impactSettings.FadeTime;

        protected Color color = color;

        protected IImpactSettings impactSettings = impactSettings;

        protected Material matReference = matReference;


        /// <summary>
        /// From 1f to 0f.
        /// </summary>
        protected float ReverseProgress(float currentTime) => 1 - (currentTime - timeStart) / lifeTime;

    }

}
