using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces {

    public interface IWeaponDefinition {
        float RoundsPerSecond { get; }
        float SpreadAngle { get; }
        int ProjectileCount { get; }
        int ProjectileMaxRange { get; }
        float ProjectileSpeed { get; }

        ITracerSettings Tracers { get; }
        InterpolatedMuzzleSmoke MuzzleSmoke { get; }
    }

    public interface ITracerSettings {
        /// <summary>
        /// Offset applied to the start point of a tracer, facing the direction the tracer is moving to.
        /// </summary>
        Vector3 StartOffset { get; }
        /// <summary>
        /// Multiplier over the default length of the tracer. Tracer uses a QuadIn easing function to calculate
        /// the back position, relative to the current forward position. With this you can amplify or reduce 
        /// the effect of this easing.
        /// </summary>
        float LengthMultiplier { get; }
        /// <summary>
        /// Width at the start (muzzle) point of the tracer. Keep in mind that the tracer is constantly moving away
        /// from the origin point, so the start/closest point of the tracer will always use this value no matter 
        /// the current distance from the muzzle.
        /// </summary>
        float WidthStart { get; }
        /// <summary>
        /// Width at the ProjectileMaxRange point of the tracer. Keep in mind that the tracer is constantly moving away
        /// from the origin point, so the farthest point of the tracer will always use this value no matter 
        /// the current distance from the muzzle.
        /// </summary>
        float WidthEnd { get; }
        /// <summary>
        /// Distance to reach the width value of WidthEnd.
        /// If the value is negative, it will behave as if it had the same value as ProjectileMaxRange.
        /// </summary>
        float MaxWidthDistance { get; }
        Color ColorStart { get; }
        Color ColorEnd { get; }
        /// <summary>
        /// Time until the flash of light coming from the muzzle completely fades out.
        /// Used for both the flash coming from the muzzle, and lighting up the world around it.
        /// </summary>
        float MuzzleFlashTime { get; }

        InterpolatedSmoke SmokeSettings { get; }

        IImpactSettings Sparks { get; }
        IImpactSettings HitMarks { get; }
    }

    public interface IInterpolatedParticle {
        /// <summary>
        /// Number of interpolated smoke particles that will be inserted for each Unity unit of distance.
        /// </summary>
        float InterpolationCountPerDistanceUnit { get; }
        /// <summary>
        /// Any distances above this value wont be interpolated. 
        /// A value of zero or lower means it will always interpolate.
        /// </summary>
        float MaxInterpolationDistance { get; }
    }
    
    public interface ISmokeSettings {
        float MinStartSize { get; }
        float MaxStartSize { get; }
        float MinLifetime { get; }
        float MaxLifetime { get; }
        float MinAlpha { get; }
        float MaxAlpha { get; }
        /// <summary>
        /// Velocity that the particle will have through its lifetime. The axis correlate with the projectile
        /// trajectory, so Vector3.forward would make smoke follow the same direction as the projectile
        /// </summary>
        Vector3 BaseVelocity { get; }
        float Spread { get; }
    }

    public interface IMuzzleSmokeSettings : ISmokeSettings {
        float SmokeEffectTotalTime { get; }
        int MinSmokeParticles { get; }
        int MaxSmokeParticles { get; }
        float MinfloatSpeed { get; }
        float MaxFloatSpeed { get; }
    }

    public interface IImpactSettings {
        Color Color { get; }
        float BaseSize { get; }
        float FadeTime { get; }
        float MinFadeProgress { get; }
    }

}
