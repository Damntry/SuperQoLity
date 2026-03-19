using Cysharp.Threading.Tasks;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.Rendering;
using Damntry.UtilsUnity.Timers;
using Damntry.UtilsUnity.Vectors;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers {

    public class SmokeManager {

        private const float warningCooldown = 4f;


        private readonly ParticleSystem partSystem;

        private readonly InterpolationCache interpolationCache;

        private int MaxParticlesCountWarning;
        private int SoftParticleCountWarning;

        private float canWarnSoftLimitTime = -1f;
        private float canWarnHardLimitTime = -1f;

        private GameObject smokeEmitterObj;

        public bool Active { get; private set; }


        public SmokeManager() {
            int maxParticles = ModConfig.Instance.MaxShotgunSmokeParticles.Value;
            if (maxParticles > 0) {
                partSystem = CreateSmokeSystem(maxParticles);

                Active = true;

                interpolationCache = new();

                UpdateMaxParticles();
                ModConfig.Instance.MaxShotgunSmokeParticles.SettingChanged += (_, _) => UpdateMaxParticles();

                StartLoggingMaxParticleCount();
            }
        }


        private async void StartLoggingMaxParticleCount() {
#if DEBUG
            await UniTask.DelayFrame(1);

            int maxParticleCount = 0;
            UnityTimeStopwatch sw = UnityTimeStopwatch.StartNew();

            while (Active && partSystem) {
                maxParticleCount = System.Math.Max(maxParticleCount, partSystem.particleCount);

                if (sw.ElapsedSeconds >= 1) {
                    if (maxParticleCount > 0) {
                        LOG.TEMPWARNING($"Max particle count in the last period: {maxParticleCount}");
                    }

                    maxParticleCount = 0;
                    sw.Restart();
                }

                await UniTask.DelayFrame(1);
            }
#endif
        }

        public void Destroy() {
            Active = false;
            interpolationCache.Destroy();
        }

        private void UpdateMaxParticles() {
            int maxParticles = ModConfig.Instance.MaxShotgunSmokeParticles.Value;

            MainModule main = partSystem.main;
            main.maxParticles = maxParticles;   //Default is 1000.

            Active = maxParticles > 0;

            MaxParticlesCountWarning = maxParticles;
            SoftParticleCountWarning = Mathf.RoundToInt(maxParticles * 0.85f);
        }


        public void SpawnSmokeTrail(uint uniqueEffectId, Vector3 position, Vector3 direction, 
                Vector3 initialVelocity, InterpolatedSmoke smokeSettings, float smokeSpawnChance) {

            if (!Active) {
                return;
            }

            Vector3 velocity = VectorUtils.GetRandomSpreadOffset(SpreadAxis.Both, 
                direction, initialVelocity, smokeSettings.Spread);

            if (interpolationCache.CanInterpolate(uniqueEffectId, position, out Vector3 lastCenterPos)) {

                //Spawn the interpolated subframe particles
                InterpolateSmoke(lastCenterPos, position, velocity, smokeSettings, smokeSpawnChance);
            }

            if (smokeSpawnChance == 1 || Random.value <= smokeSpawnChance) {
                SpawnSmoke(position, velocity, smokeSettings);
            }
        }

        public void SpawnSmokeGroup(uint uniqueEffectId, Vector3 centerPosition, float smokeSpread, 
                Vector3 initialVelocity, int smokeCount, InterpolatedMuzzleSmoke muzzleSmokeSettings) {

            if (!Active) {
                return;
            }

            //TODO 2 - This is a frankenstein. Im thinking instead of spawning smoke individually myself,
            //  I would tell this manager what kind of behaviours I expect from a smoke particle, like "Go
            //  from here to here", "Fade in X time", "Create a trail". This way it can manage the interpolation
            //  by itself, though its more limiting.
            if (interpolationCache.CanInterpolate(uniqueEffectId, centerPosition, out Vector3 lastCenterPos)) {

                //Spawn the interpolated subframe particles
                InterpolateSmokeSpreadGroup(lastCenterPos, centerPosition, initialVelocity, 
                    muzzleSmokeSettings, smokeSpawnChance: 1f, smokeCount, smokeSpread);
            }


            for (int i = 0; i < smokeCount; i++) {
                SpawnSmokeRandomSpread(centerPosition, smokeSpread, initialVelocity, muzzleSmokeSettings);
            }
        }

        private void InterpolateSmoke(Vector3 lastCenterPos, Vector3 currentCenterPos, 
                Vector3 initialVelocity, InterpolatedSmoke smokeSettings, float smokeSpawnChance) {

            InterpolateSmokeInternal(lastCenterPos, currentCenterPos,
                initialVelocity, smokeSettings, smokeSpawnChance);
        }

        private void InterpolateSmokeSpreadGroup(Vector3 lastCenterPos, Vector3 currentCenterPos, Vector3 initialVelocity,
                InterpolatedMuzzleSmoke smokeSettings, float smokeSpawnChance, int smokeCount, float smokeSpread) {

            InterpolateSmokeInternal(lastCenterPos, currentCenterPos, 
                initialVelocity, smokeSettings, smokeSpawnChance, smokeCount, smokeSpread);
        }

        private void InterpolateSmokeInternal(Vector3 lastCenterPos, Vector3 currentCenterPos, Vector3 initialVelocity,
                InterpolatedSmoke smokeSettings, float smokeSpawnChance, int smokeCount = 1, float smokeSpread = 0f) {

            //Interpolate as many times as needed depending on the amount of distance to cover.
            float distance = Vector3.Distance(currentCenterPos, lastCenterPos);
            if (smokeSettings.MaxInterpolationDistance > 0 && distance > smokeSettings.MaxInterpolationDistance) {
                return;
            }
            int interpolationCount = Mathf.RoundToInt(distance * smokeSettings.InterpolationCountPerDistanceUnit);

            if (interpolationCount == 0) {
                return;
            }

            float divisor = interpolationCount + 1f;

            float simulatedSmokeCount = smokeCount;
            if (simulatedSmokeCount > 1) {
                //To save performance, interpolated smoke groups will have less particles than the non interpolated ones.
                simulatedSmokeCount = Mathf.Max(1f, Mathf.Floor(smokeCount / 2f));
            }
            
            for (int i = 1; i <= interpolationCount; i++) {
                if (smokeSpawnChance < 1 && Random.value > smokeSpawnChance) {
                    continue;
                }

                float progress = i / divisor;
                Vector3 interpCenterPos = Vector3.Lerp(lastCenterPos, currentCenterPos, progress);
                //Simulate group interpolation by spawning the current amount of smokes
                //  at the inbetween positions, with the same random spread.
                if (smokeSettings is InterpolatedMuzzleSmoke smokeMuzzle) {
                    for (int j = 0; j < simulatedSmokeCount; j++) {
                        SpawnSmokeRandomSpread(interpCenterPos, smokeSpread, initialVelocity, smokeMuzzle);
                    }
                } else {
                    SpawnSmoke(interpCenterPos, initialVelocity, smokeSettings);
                }
            }
        }

        private void SpawnSmokeRandomSpread(Vector3 centerPosition, float smokeSpread, Vector3 initialVelocity,
                InterpolatedMuzzleSmoke muzzleSmokeSettings) {

            Vector3 randPos = Camera.main.GetRandomWorldPosOffset(SpreadAxis.Both, centerPosition, smokeSpread);

            initialVelocity *= Random.Range(muzzleSmokeSettings.MinfloatSpeed, muzzleSmokeSettings.MaxFloatSpeed);

            SpawnSmoke(randPos, initialVelocity, muzzleSmokeSettings);
        }

        public void SpawnSmoke(Vector3 tracerPos, Vector3 initialVelocity, ISmokeSettings smokeSettings) {
            if (!Active) {
                return;
            }

            EmitParams emitParams = new() {
                position = tracerPos,
                startLifetime = Random.Range(smokeSettings.MinLifetime, smokeSettings.MaxLifetime),
                startSize = Random.Range(smokeSettings.MinStartSize, smokeSettings.MaxStartSize),
                
                startColor = new Color(1f, 1f, 1f, Random.Range(smokeSettings.MinAlpha, smokeSettings.MaxAlpha)),

                //For some reason Unity is applying velocity to it even when the parent object itself
                //  is not moving, so I always need to set it regardless.
                velocity = initialVelocity,
            };

            partSystem.Emit(emitParams, 1);

            //Skip warnings if the limit is low enough that it would trigger too easily.
            if (MaxParticlesCountWarning >= 500) {
                if (partSystem.particleCount >= MaxParticlesCountWarning && canWarnHardLimitTime < Time.time) {
                    canWarnHardLimitTime = Time.time + warningCooldown;
                    TimeLogger.Logger.LogWarning($"Smoke particle system reached hard warning limit! " +
                        $"({partSystem.particleCount}/{MaxParticlesCountWarning})", LogCategories.Visuals);
                    return;
                }
                if (partSystem.particleCount >= SoftParticleCountWarning && canWarnSoftLimitTime < Time.time) {
                    canWarnSoftLimitTime = Time.time + warningCooldown;
                    TimeLogger.Logger.LogWarning($"Smoke particle system reached soft warning limit! " +
                        $"({partSystem.particleCount}/{MaxParticlesCountWarning})", LogCategories.Visuals);
                }
            }
        }

        private ParticleSystem CreateSmokeSystem(int maxParticles = 1000) {
            smokeEmitterObj = new GameObject("SmokeTracerEmitter");

            GameObjectManager.GetGameObjectFrom(TargetObject.LocalGamePlayer, out GameObject localPlayerObj);
            if (localPlayerObj) {
                smokeEmitterObj.transform.parent = localPlayerObj.transform;
            }

            ParticleSystem ps = smokeEmitterObj.AddComponent<ParticleSystem>();
            MainModule main = ps.main;
            main.maxParticles = maxParticles;   //Unity default is 1000.
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            //Movement will be specified in the SpawnSmoke call
            main.gravityModifier = 0f;

            EmissionModule emission = ps.emission;
            emission.enabled = false;

            //Fading gradient
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new ();
            g.SetKeys(
                [
                    new (Color.white, 0f),
                    new (Color.white, 1f)
                ],
                [
                    new (1f, 0f),
                    new (0.5f, 0.75f),
                    new (0f, 1f)
                ]
            );
            col.color = new MinMaxGradient(g);

            //No shape, we ll emit manually
            ShapeModule shape = ps.shape;
            shape.enabled = false;

            var partRend = ps.GetComponent<ParticleSystemRenderer>();
            partRend.renderMode = ParticleSystemRenderMode.Billboard;

            Material mat = new (ShaderUtils.LegacyParticleShader.Value);
            Texture2D circleTex = TextureUtils.GenerateCircle(32);
            mat.mainTexture = circleTex;
            mat.color = Color.white;

            partRend.material = mat;

            return ps;
        }


        private record struct HistoricPosData(Vector3 CenterPos, float Time);


        private class InterpolationCache {

            private readonly Dictionary<uint, HistoricPosData> previousSmokeParticles;

            private readonly CancellationTokenSource cancelSource;

            public InterpolationCache() {
                previousSmokeParticles = new();
                cancelSource = new();

                CleanUpOldData().FireAndForgetCancels(LogCategories.Visuals);
            }

            public void Destroy() {
                cancelSource.Cancel();
            }

            /// <summary>Checks whether there is previous data from the smoke instance to interpolate.</summary>
            public bool CanInterpolate(uint UniqueInstanceId, Vector3 centerPosition, out Vector3 lastCenterPos) {
                lastCenterPos = default;

                HistoricPosData currentData = new(centerPosition, Time.time);
                if (previousSmokeParticles.TryGetValue(UniqueInstanceId, out HistoricPosData lastData)) {
                    previousSmokeParticles[UniqueInstanceId] = currentData;
                    lastCenterPos = lastData.CenterPos;

                    return true;
                }

                previousSmokeParticles.Add(UniqueInstanceId, currentData);

                return false;
            }

            public async Task CleanUpOldData() {
                while (!cancelSource.IsCancellationRequested) {
                    await UniTask.Delay(1000, cancellationToken: cancelSource.Token);

                    //Remove interpolation data older than 1 second.
                    float cutoffTime = Time.time - 1;

                    foreach (var item in previousSmokeParticles.ToArray()) {
                        if (item.Value.Time < cutoffTime) {
                            //Too old to be useful for interpolation, remove. Does not cause an 
                            //  enumeration exception since we are iterating a copy of the dictionary.
                            previousSmokeParticles.Remove(item.Key);
                        }
                    }
                }
            }

        }

    }

}
