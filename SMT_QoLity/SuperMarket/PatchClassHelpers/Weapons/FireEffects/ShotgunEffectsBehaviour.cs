using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects {

    public class ShotgunEffectsBehaviour : MonoBehaviour {

        static ShotgunEffectsBehaviour() {
            TracerGroupData.OnTracerAdded += () => { Instance.enabled = true; };
            ImpactGroupData.OnImpactAdded += () => { Instance.enabled = true; };
        }

        internal static ShotgunEffectsBehaviour Instance { get; private set; }

        internal static Dictionary<Material, TracerGroupData> GroupedTracers { get; set; }

        internal static List<IImpactHandler<IImpactData>> GroupedImpacts { get; set; }


        void Awake() {
            Instance = this;

            GroupedTracers = new();
            GroupedImpacts = new();
        }


        void Update() {
            if (Time.timeScale == 0) {
                //Skip processing
                return;
            }

            List<Material> destroyTracers = null;
            List<IImpactHandler<IImpactData>> destroyImpactsGroup = null;
            float currentTime = Time.time;

            foreach (var kvp in GroupedTracers) {
                Material tracerSharedMaterial = kvp.Key;
                TracerGroupData tracerGroup = kvp.Value;

                if (!tracerGroup.HasStarted) {
                    tracerGroup.StartTracingTimers(currentTime);
                }

                float totalElapsedTime = currentTime - tracerGroup.StartLifetime;

                foreach (SingleTracerData tracerData in tracerGroup.Tracers) {

                    LineRenderer lineRender = tracerData.LineRender;

                    if (!lineRender) {
                        continue;
                    }

                    // Show and animate tracer visual
                    ProcessTracer(totalElapsedTime, tracerGroup, tracerData);

                    if (!lineRender.enabled) {
                        Destroy(lineRender.gameObject);
                        continue;
                    }
                }

                bool isSmokeFinished = ProcessMuzzleSmoke(tracerGroup, tracerGroup.MuzzleSmokeSettings, totalElapsedTime);

                if (tracerGroup.LightSrc) {
                    float lightFlashProgress = totalElapsedTime / tracerGroup.TracerSettings.MuzzleFlashTime;
                    if (lightFlashProgress < 1) {
                        tracerGroup.LightSrc.intensity = Mathf.Lerp(0, 1.2f, lightFlashProgress);
                    } else {
                        tracerGroup.LightSrc.enabled = false;
                        Destroy(tracerGroup.LightSrc.gameObject);
                    }
                }

                bool isMuzzleFlashFinished = true;
                if (tracerGroup.BroomMuzzleObj && tracerGroup.BroomMuzzleObj.activeSelf && 
                        tracerGroup.BroomMuzzleObj.TryGetComponent(out Renderer render)) {
                    float flashFadingProgress = 1 - totalElapsedTime / tracerGroup.TracerSettings.MuzzleFlashTime;
                    if (flashFadingProgress > 0) {
                        Color currentColor = render.material.GetColor("_TintColor");
                        currentColor.a = flashFadingProgress;
                        render.material.SetColor("_TintColor", currentColor);

                        isMuzzleFlashFinished = false;
                    } else {
                        tracerGroup.BroomMuzzleObj.SetActive(false);
                    }
                }

                if (tracerGroup.HasTracersFinished() && !tracerGroup.LightSrc && isSmokeFinished && isMuzzleFlashFinished) {
                    destroyTracers ??= new();
                    destroyTracers.Add(tracerSharedMaterial);
                    continue;
                }
            }

            foreach (IImpactHandler<IImpactData> impactGroup in GroupedImpacts) {
                impactGroup.UpdateFade(currentTime);
                
                if (impactGroup.IsImpactFinished) {
                    foreach (IImpactData impact in impactGroup.ImpactCollection) {
                        foreach (GameObject obj in impact.ImpactObjs) {
                            if (obj) {
                                Destroy(obj);
                            }
                        }
                    }
                    
                    destroyImpactsGroup ??= new();
                    destroyImpactsGroup.Add(impactGroup);
                }
            }

            if (destroyTracers != null) {
                foreach (Material mat in destroyTracers) {
                    GroupedTracers.Remove(mat);
                }
            }
            if (destroyImpactsGroup != null) {
                foreach (IImpactHandler<IImpactData> impactGroup in destroyImpactsGroup) {
                    GroupedImpacts.Remove(impactGroup);
                }
            }

            //Disable component when there are no more effects to show.
            if (GroupedTracers == null || GroupedTracers.Count == 0 && GroupedImpacts.Count == 0) {
                enabled = false;
            }
        }
        
        internal void ProcessTracer(float elapsedTime, TracerGroupData tracerGroup, SingleTracerData tracerData) {

            LineRenderer lineRender = tracerData.LineRender;
            if (tracerGroup.Animate && lineRender.enabled) {
                if (!tracerData.HasCalculatedEndTime) {
                    tracerData.CalculateTracerEndTime(tracerGroup.StartLifetime, tracerGroup.ProjectileSpeed);
                }
                
                //TODO 1 Particle - Seems like pellet tracers show better or worse depending on fps. I might need
                //  to add some sort of interpolation to draw more visible tracers when fps are low or who knows.

                ITracerSettings tracerSettings = tracerGroup.TracerSettings;

                float currentRelativeProgress = elapsedTime / tracerData.TotalFlightTime;
                Vector3 tracerForwardPos = tracerData.FullRayPath.GetValue(currentRelativeProgress, out float tracerWidth);

                //Calculate where the back (closest) side of the tracer would be positioned now.
                //  We apply a small dampening so it looks like the tracer is shorter at first, and 
                //  gets progressively larger as it moves away, until we reach the target delay.
                
                float currentRelativeBackProgress = 
                    LeanTween.easeInQuad(0f, 1f, currentRelativeProgress) * (1 / tracerSettings.LengthMultiplier);

                Vector3 tracerBackPos = tracerData.FullRayPath.GetValue(currentRelativeBackProgress, 
                    out float tracerBackWidth);
                
                lineRender.SetPosition(0, tracerBackPos);
                lineRender.SetPosition(1, tracerForwardPos);

                lineRender.startWidth = tracerWidth;
                lineRender.endWidth = tracerBackWidth;

                if (tracerGroup.SmokeManagerSystem != null && tracerGroup.SmokeManagerSystem.Active &&
                        tracerData.SpawnsSmokeTrail) {

                    //Spawn progressively less smoke the longer it goes. They can be barely seen at a distance anyway.
                    float smokeSpawnChance = 1 - currentRelativeProgress;

                    tracerGroup.SmokeManagerSystem.SpawnSmokeTrail(
                        tracerData.SmokeTrailId, tracerBackPos, tracerData.ProjectileRay.direction,
                        tracerSettings.SmokeSettings.BaseVelocity, tracerSettings.SmokeSettings, smokeSpawnChance);
                }
                
                if (currentRelativeBackProgress >= 1) {
                    lineRender.enabled = false;
                }
            }
        }

        private bool ProcessMuzzleSmoke(TracerGroupData tracerGroup, InterpolatedMuzzleSmoke muzzleSmokeSettings, 
                float elapsedTime) {            

            if (tracerGroup.SmokeManagerSystem != null && tracerGroup.SmokeManagerSystem.Active && 
                    tracerGroup.BroomMuzzleObj && elapsedTime < muzzleSmokeSettings.SmokeEffectTotalTime) {

                Vector3 muzzlePosition = tracerGroup.BroomMuzzleObj.transform.position;

                float smokeProgression = elapsedTime / muzzleSmokeSettings.SmokeEffectTotalTime;
                float smokeRegression = Mathf.Max(1 - smokeProgression, 0.2f);
                
                int smokeCount = Mathf.RoundToInt(Random.Range(muzzleSmokeSettings.MinSmokeParticles, muzzleSmokeSettings.MaxSmokeParticles) * smokeRegression);

                Vector3 upMovement = Vector3.up * smokeRegression;
                float smokeSpread = smokeRegression * muzzleSmokeSettings.Spread;

                tracerGroup.SmokeManagerSystem.SpawnSmokeGroup(tracerGroup.MuzzleSmokeId, muzzlePosition, smokeSpread, 
                    upMovement, smokeCount, muzzleSmokeSettings);

                return smokeProgression >= 1;
            }

            return true;
        }

    }

}
