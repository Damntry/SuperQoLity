using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model;
using UnityEngine;
using static SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.ShotgunTracerSettings;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions {

    public class ShotgunDefinition : IWeaponDefinition {

        public static ShotgunDefinition DefaultShotgunDefinition { get; } = new();

        public float RoundsPerSecond { get; } = 0.001f;//1.6f;	//TODO 0 !FINISH - Temp Test
        public float SpreadAngle { get; } = 1.2f;
        public int ProjectileCount { get; } = 10;
        public int ProjectileMaxRange { get; } = 200;
        public float ProjectileSpeed { get; } = 800f;

        public ITracerSettings Tracers { get; } = new ShotgunTracerSettings();

        public InterpolatedMuzzleSmoke MuzzleSmoke { get; } = new ShotgunMuzzleSmokeSettings();

    }

    public class ShotgunTracerSettings : ITracerSettings {
        public Vector3 StartOffset { get; } = new(0f, 0f, 0.5f);

        public float LengthMultiplier { get; } = 1.6f;

        public float WidthStart { get; } = 0.009f;

        public float WidthEnd { get; } = 0.2f;

        public float MaxWidthDistance { get; } = 55f;

        public Color ColorStart { get; } = new (0.95f, 0.85f, 0.65f, 1f); //Light Yellow

        public Color ColorEnd { get; } = new(0.90f, 0.75f, 0.5f, 1f); //Orangeish

        public float MuzzleFlashTime { get; } = 0.15f;

        public InterpolatedSmoke SmokeSettings { get; } = new ShotgunTracerSmokeSettings();

        public IImpactSettings Sparks { get; } = new Spark();

        public IImpactSettings HitMarks { get; } = new HitMark();


        public class ShotgunTracerSmokeSettings : InterpolatedSmoke {
            public override float InterpolationCountPerDistanceUnit { get; } = 5f;
            public override float MaxInterpolationDistance { get; } = -1f;
            public override float MinStartSize { get; } = 0.014f;
            public override float MaxStartSize { get; } = 0.02f;
            public override float MinLifetime { get; } = 1.1f;
            public override float MaxLifetime { get; } = 1.5f;
            public override float MinAlpha { get; } = 0.32f;
            public override float MaxAlpha { get; } = 0.43f;
            public override Vector3 BaseVelocity { get; } = Vector3.forward * 0.25f;
            public override float Spread { get; } = 0.03f;
        }

        public class ShotgunMuzzleSmokeSettings : InterpolatedMuzzleSmoke {
            public override float MaxInterpolationDistance { get; } = -1f;
            public override float InterpolationCountPerDistanceUnit { get; } = 50f;
            public override float SmokeEffectTotalTime { get; } = 1.55f;
            public override float MinAlpha { get; } = 0.275f;
            public override float MaxAlpha { get; } = 0.39f;
            public override Vector3 BaseVelocity { get; } = Vector3.up;
            public override float Spread { get; } = 0.043f;
            public override int MinSmokeParticles { get; } = 3;
            public override int MaxSmokeParticles { get; } = 4;
            public override float MinStartSize { get; } = 0.032f;
            public override float MaxStartSize { get; } = 0.045f;
            public override float MinLifetime { get; } = 0.525f;
            public override float MaxLifetime { get; } = 0.6f;
            public override float MinfloatSpeed { get; } = 0.4f;
            public override float MaxFloatSpeed { get; } = 0.5f;
        }

        public class Spark : IImpactSettings {
            public Color Color { get; } = new(1f, 1f, 1f, 0.75f);
            public float BaseSize { get; } = 0.025f;
            public float FadeTime { get; } = 0.25f;
            public float MinFadeProgress { get; } = 0.5f;
        }
        
        public class HitMark : IImpactSettings {
            public Color Color { get; } = new(0.95f, 0.85f, 0.65f, 1f);
            public float BaseSize { get; } = 0.014f;
            public float FadeTime { get; } = 4.5f;
            public float MinFadeProgress { get; } = 0f;

        }

    }

}
