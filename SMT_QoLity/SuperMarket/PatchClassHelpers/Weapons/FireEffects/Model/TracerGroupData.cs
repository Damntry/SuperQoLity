using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model {

    public class TracerGroupData(ITracerSettings tracerSettings, float projectileSpeed,
            InterpolatedMuzzleSmoke muzzleSmokeSettings, Vector3 startPos, Light lightSrc,
            SmokeManager smokeManager, GameObject muzzleFlashObj, bool animate) {

        public static uint GlobalSmokeIdCounter { get; set; } = 1;
        /// <summary>
        /// Unique identifier for the interpolation of smoke from the broom muzzle.
        /// Only really matters if the fire rate is faster than the muzzle smoke lifetime.
        /// </summary>
        public uint MuzzleSmokeId { get; private set; } = GlobalSmokeIdCounter++;

        public ITracerSettings TracerSettings { get; private set; } = tracerSettings;


        public InterpolatedMuzzleSmoke MuzzleSmokeSettings { get; private set; } = muzzleSmokeSettings;

        public bool HasStarted { get; private set; }

        public float StartLifetime { get; private set; }
        public float ProjectileSpeed { get; } = projectileSpeed;
        public Light LightSrc { get; } = lightSrc;
        public SmokeManager SmokeManagerSystem { get; } = smokeManager;
        public GameObject BroomMuzzleObj { get; } = muzzleFlashObj;
        public bool Animate { get; } = animate;
        public Vector3 StartPos { get; } = startPos;
        public List<SingleTracerData> Tracers { get; } = [];


        public void StartTracingTimers(float currentTime) {
            if (HasStarted) {
                return;
            }
            HasStarted = true;
            StartLifetime = currentTime;
        }

        public static Action OnTracerAdded { get; set; }

        public void AddNewTracerEntry(SingleTracerData tracerData) {
            Tracers.Add(tracerData);

            OnTracerAdded?.Invoke();
        }

        public bool HasTracersFinished() => Tracers.All(t => !t.LineRender || !t.LineRender.enabled);

    }
}
