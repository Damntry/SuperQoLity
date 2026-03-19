using Damntry.Utils.Logging;
using Damntry.UtilsUnity.Rendering;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers.Model;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers {

    public class VFX {

        private static GameObject hitmarkPrimitive;


        public static void SpawnTracerEffect(Material mat, float projectileSpeed, ITracerSettings tracerSettings,
                InterpolatedMuzzleSmoke muzzleSmokeSettings, Vector3 start,
                SingleTracerData tracerData, Light lightRef, SmokeManager smokeManager,
                GameObject muzzleFlashObj, bool animateTravel) {

            if (!ShotgunEffectsBehaviour.Instance) {
                TimeLogger.Logger.LogError($"Tracers cant be spawned at this moment, the " +
                    $"{nameof(ShotgunEffectsBehaviour)} has not awaken yet.", LogCategories.Visuals);
                return;
            }

            if (!ShotgunEffectsBehaviour.GroupedTracers.TryGetValue(mat, out TracerGroupData tracerGroupData)) {
                tracerGroupData = new(
                    tracerSettings, projectileSpeed, muzzleSmokeSettings, start, 
                    lightRef, smokeManager, muzzleFlashObj, animateTravel
                );

                ShotgunEffectsBehaviour.GroupedTracers.Add(mat, tracerGroupData);
            }

            tracerGroupData.AddNewTracerEntry(tracerData);

            float currentTime = Time.time;

            tracerGroupData.StartTracingTimers(currentTime);

            float elapsedTime = currentTime - tracerGroupData.StartLifetime;

            ShotgunEffectsBehaviour.Instance.ProcessTracer(elapsedTime, tracerGroupData, tracerData);
        }

        public static void SpawnImpactHitMark(HitMarkSpatial position, IImpactSettings impactSettings) {
            SpawnImpactHitMarkGroup([position], impactSettings);
        }

        public static void SpawnImpactHitMarkGroup(HitMarkSpatial[] hitMarkSpatials, IImpactSettings impactSettings) {
            if (hitMarkSpatials == null || hitMarkSpatials.Length == 0) {
                TimeLogger.Logger.LogError($"Param impactPositions cant be null or empty.", LogCategories.Visuals);
                return;
            }
            if (!ShotgunEffectsBehaviour.Instance) {
                TimeLogger.Logger.LogError($"Impact hit marks cant be spawned at this moment, the " +
                    $"{nameof(ShotgunEffectsBehaviour)} has not awaken yet.", LogCategories.Visuals);
                return;
            }
            
            Color color = impactSettings.Color;
            Material sharedMaterial = CreateLegacyParticleMaterial(color);

            ImpactHitMarkHandler impactHandler = new(Time.time, color, impactSettings, sharedMaterial);

            foreach (HitMarkSpatial hit in hitMarkSpatials) {
                GameObject impactHitMarkObj = CreateImpactHitMarkObject(hit, impactSettings, sharedMaterial);
                if (impactHitMarkObj) {
                    impactHandler.AddNewImpactEntry(new HitMarkData(impactHitMarkObj));
                }
            }

            ShotgunEffectsBehaviour.GroupedImpacts.Add(impactHandler);
        }


        private static GameObject CreateImpactHitMarkObject(HitMarkSpatial hit, 
                IImpactSettings impactSettings, Material sharedMaterial = null) {

            if (!hitmarkPrimitive) {
                //A cube instead of quad so its still a bit visible when looking at it at an angle,
                //  otherwise it seems like its not visible on relatively short distances.
                hitmarkPrimitive = MeshUtils.CreateCubePrimitive("ImpactHitMark", width: 1f, height: 1f, depth: 0.5f);
                if (!hitmarkPrimitive) {
                    TimeLogger.Logger.LogError("The hitmark primitive could not be created. " +
                        "Hitmarks wont show up.", LogCategories.Visuals);
                    return null;
                }
                hitmarkPrimitive.SetActive(false);
            }

            GameObject hitMark = Object.Instantiate(hitmarkPrimitive);

            //Move it away a bit to avoid z-fighting
            Vector3 separation = hit.HitFaceRotation * (Vector3.forward * 0.006f);
            hitMark.transform.position = hit.Position + separation;
            hitMark.transform.rotation = hit.HitFaceRotation;
            hitMark.transform.localScale *= impactSettings.BaseSize;

            var renderer = hitMark.GetComponent<Renderer>();

            if (sharedMaterial) {
                renderer.sharedMaterial = sharedMaterial;
            } else {
                renderer.sharedMaterial = CreateLegacyParticleMaterial(impactSettings.Color);
            }

            hitMark.SetActive(true);

            return hitMark;
        }

        public static void SpawnImpactSparksGroup(SparkInitialData[] sparkDataGroup, 
                IImpactSettings impactSettings, int sparkCount = 4) {

            if (sparkDataGroup == null || sparkDataGroup.Length == 0) {
                TimeLogger.Logger.LogError($"Param sparkDataGroup cant be null or empty.", LogCategories.Visuals);
                return;
            }
            if (!ShotgunEffectsBehaviour.Instance) {
                TimeLogger.Logger.LogError($"Impact sparks cant be spawned at this moment, the " +
                    $"{nameof(ShotgunEffectsBehaviour)} has not awaken yet.", LogCategories.Visuals);
                return;
            }

            Color color = impactSettings.Color;
            Material sharedMaterial = CreateLegacyParticleMaterial(color);

            ImpactSparksHandler impactHandler = new (Time.time, color, impactSettings, sharedMaterial);

            foreach (SparkInitialData sparkData in sparkDataGroup) {
                GameObject[] impactSparkObjs = SpawnImpactSparks(sparkData,  
                    impactSettings, sparkCount, sharedMaterial);
                impactHandler.AddNewImpactEntry(new SparkData(sparkData, impactSparkObjs));
            }

            ShotgunEffectsBehaviour.GroupedImpacts.Add(impactHandler);
        }

        private static GameObject[] SpawnImpactSparks(SparkInitialData sparkData, 
                IImpactSettings impactSettings, int sparkCount = 4, Material sharedMaterial = null) {

            Material mat = sharedMaterial ?? new (ShaderUtils.LegacyParticleShader.Value);
            mat.SetColor("_TintColor", impactSettings.Color);

            GameObject[] sparks = new GameObject[sparkCount];
            for (int i = 0; i < sparkCount; i++) {
                Vector3 dir = Random.onUnitSphere * 0.05f;
                GameObject spark = MeshUtils.CreateQuadPrimitive("ImpactSpark", width: 1f, height: 1f);
                sparks[i] = spark;
                spark.transform.position = sparkData.Position;
                spark.transform.rotation = Quaternion.LookRotation(dir);
                //Bigger sparks the farther away it is, otherwise they are either too big, or can be barely seen.
                float distanceMult = Mathf.Max(sparkData.Distance / 10f, 1f);
                spark.transform.localScale = Vector3.one * impactSettings.BaseSize * distanceMult;

                var r = spark.GetComponent<Renderer>();
                r.sharedMaterial = mat;

                var rb = spark.AddComponent<Rigidbody>();
                rb.useGravity = false;
                float speed = Random.Range(13f, 18f);
                rb.velocity = dir * speed;
            }

            return sparks;
        }

        private static Material CreateLegacyParticleMaterial(Color color) {
            Material material = new (ShaderUtils.LegacyParticleShader.Value);
            material.SetColor("_TintColor", color);
            return material;
        }

    }

}
