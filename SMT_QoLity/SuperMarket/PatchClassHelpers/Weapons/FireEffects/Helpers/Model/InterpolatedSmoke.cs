using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model {

    public abstract class InterpolatedSmoke : ISmokeSettings, IInterpolatedParticle {

        public abstract float InterpolationCountPerDistanceUnit { get; }
        public abstract float MaxInterpolationDistance { get; }

        public abstract float MinStartSize { get; }
        public abstract float MaxStartSize { get; }
        public abstract float MinLifetime { get; }
        public abstract float MaxLifetime { get; }
        public abstract float MinAlpha { get; }
        public abstract float MaxAlpha { get; }
        public abstract Vector3 BaseVelocity { get; }
        public abstract float Spread { get; }
    }

    public abstract class InterpolatedMuzzleSmoke : InterpolatedSmoke, IMuzzleSmokeSettings {

        public abstract float SmokeEffectTotalTime { get; }
        public abstract int MinSmokeParticles { get; }
        public abstract int MaxSmokeParticles { get; }
        public abstract float MinfloatSpeed { get; }
        public abstract float MaxFloatSpeed { get; }
    }
}
