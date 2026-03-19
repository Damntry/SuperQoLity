using Damntry.UtilsUnity.ExtensionMethods;
using Damntry.UtilsUnity.Rendering;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions.Interfaces;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects {

    public class ShotgunEffectsManager {

        public static void SpawnTracer(Vector3 start, SingleTracerData tracerData, 
                IWeaponDefinition weaponDefinition, GameObject broomObj, Vector3 broomLocalTip, 
                SmokeManager smokeManager, bool animateTravel, bool addMuzzleFlash, bool addImpactHitMark, 
                bool addImpactSparks, bool addLight) {

            SpawnTracerInternal(start, [tracerData], weaponDefinition, 
                broomObj, broomLocalTip, smokeManager, animateTravel, addMuzzleFlash, 
                addImpactHitMark, addImpactSparks, addLight);
        }

        /// <summary>
        /// Spawns a group of tracers. Each tracer will be its own particle that targets a
        /// different end point, but all share the same Material.
        /// Intended for shotguns mostly.
        /// </summary>
        public static void SpawnTracerGroup(Vector3 start, SingleTracerData[] tracerDataGroup, 
                IWeaponDefinition weaponDefinition, GameObject broomObj, Vector3 broomLocalTip, 
                SmokeManager smokeManager, bool animateTravel, bool addMuzzleFlash, bool addImpactHitMark, 
                bool addImpactSparks, bool addLight) {

            SpawnTracerInternal(start, tracerDataGroup, weaponDefinition,
                broomObj, broomLocalTip, smokeManager, animateTravel, addMuzzleFlash,
                addImpactHitMark, addImpactSparks, addLight);
        }

        private static void SpawnTracerInternal(Vector3 start, SingleTracerData[] tracerDataGroup, 
                IWeaponDefinition weaponDefinition, GameObject broomObj, Vector3 broomLocalTip, 
                SmokeManager smokeManager, bool animateTravel, bool addMuzzleFlash, bool addImpactHitMark, 
                bool addImpactSparks, bool addLight) {

            GameObject muzzleBroomObj = null;
            if (addMuzzleFlash || addLight || smokeManager != null) {
                CreateBroomMuzzleObj(weaponDefinition.Tracers.ColorStart, addMuzzleFlash, broomObj, broomLocalTip, out muzzleBroomObj);
            }

            Light light = null;
            if (addLight) {
                //Illuminate the area around the muzzle shot
                SpawnLightSourceFlash(weaponDefinition.Tracers.ColorStart, broomObj, broomLocalTip, out light);
            }

            Material tracerMat = new (ShaderUtils.URP_ParticleShaderUnlit.Value);

            List<HitMarkSpatial> hitMarkSpatialGroup = null;
            List<SparkInitialData> sparkGroup = null;
            if (addImpactHitMark) {
                hitMarkSpatialGroup = new(tracerDataGroup.Length);
            }
            if (addImpactSparks) {
                sparkGroup = new(tracerDataGroup.Length);
            }
            
            foreach (SingleTracerData tracerData in tracerDataGroup) {
                if (animateTravel) {
                    SpawnTracerEffect(start, weaponDefinition, tracerData, tracerMat, 
                        muzzleBroomObj, light, smokeManager, animateTravel);
                }

                if (tracerData.CanCreateImpactMark) {
                    hitMarkSpatialGroup?.Add(new HitMarkSpatial(tracerData.EndPoint, tracerData.HitFaceRotation));
                    sparkGroup?.Add(new SparkInitialData(tracerData.EndPoint, tracerData.TotalDistance));
                }
            }
            
            if (hitMarkSpatialGroup != null && hitMarkSpatialGroup.Count > 0) {
                VFX.SpawnImpactHitMarkGroup(hitMarkSpatialGroup.ToArray(), weaponDefinition.Tracers.HitMarks);
            }
            if (sparkGroup != null && sparkGroup.Count > 0) {
                VFX.SpawnImpactSparksGroup(sparkGroup.ToArray(), weaponDefinition.Tracers.Sparks);
            }
        }

        private static void SpawnTracerEffect(Vector3 start, IWeaponDefinition weaponDefinition, 
                SingleTracerData tracerData, Material tracerMat, GameObject muzzleFlashObj, Light light, 
                SmokeManager smokeManager, bool animateTravel) {

            GameObject tracer = new ("Tracer");
            var lineRender = tracer.AddComponent<LineRenderer>();

            tracerData.LineRender = lineRender;

            lineRender.sharedMaterial = tracerMat;

            lineRender.positionCount = 2;
            lineRender.numCapVertices = 1;
            lineRender.numCornerVertices = 1;
            lineRender.textureMode = LineTextureMode.Stretch;

            lineRender.useWorldSpace = true;

            lineRender.startColor = weaponDefinition.Tracers.ColorStart;
            lineRender.endColor = weaponDefinition.Tracers.ColorEnd;

            VFX.SpawnTracerEffect(tracerMat, weaponDefinition.ProjectileSpeed, weaponDefinition.Tracers,
                weaponDefinition.MuzzleSmoke, start, tracerData, light, smokeManager, muzzleFlashObj, animateTravel);
        }

        
        private static void CreateBroomMuzzleObj(Color color, bool addMuzzleFlash, 
                GameObject broomObj, Vector3 broomLocalTip, out GameObject muzzleBroomObj) {

            string broomMuzzleObjName = "MuzzleBroom";

            muzzleBroomObj = broomObj.transform.Find(broomMuzzleObjName).GameObject();
            if (!muzzleBroomObj) {
                if (addMuzzleFlash) {
                    muzzleBroomObj = MeshUtils.CreateQuadPrimitive(broomMuzzleObjName, width: 1f, height: 1f);

                    var r = muzzleBroomObj.GetComponent<Renderer>();
                    r.material = new Material(ShaderUtils.LegacyParticleShader.Value);
                    r.material.SetColor("_TintColor", color);
                } else {
                    muzzleBroomObj = new GameObject(broomMuzzleObjName);
                }
                muzzleBroomObj.transform.localRotation = Quaternion.LookRotation(Camera.main?.transform.forward ?? Vector3.forward);
                muzzleBroomObj.transform.localScale = Vector3.one * 0.08f;
                muzzleBroomObj.transform.parent = broomObj.transform;
                muzzleBroomObj.transform.localPosition = broomLocalTip;
            }
            
            muzzleBroomObj.SetActive(true);
        }

        private static void SpawnLightSourceFlash(Color color, GameObject broomObj, Vector3 broomLocalTip, out Light light) {
            GameObject lightObject = new ("Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 17f;
            //Intensity not too strong to avoid seizures or raves
            light.intensity = 0.65f;
            light.color = color;

            //Add light object at the broom muzzle position
            lightObject.transform.parent = broomObj.transform;
            lightObject.transform.localPosition = broomLocalTip;
        }

    }

    /* How it started out.
    public class TracerManager_V0 {

        public static void SpawnTracer(Vector3 start, Vector3 end, Color color, float duration = 0.05f) {
            GameObject tracer = new ("Tracer");
            var lr = tracer.AddComponent<LineRenderer>();

            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            lr.startWidth = 0.02f;
            lr.endWidth = 0.01f;
            lr.material = new (Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            lr.material.color = color;

            // optional: fade out and destroy
            tracer.AddComponent<TimedSelfDestroy>().Init(duration);
        }

        public class TimedSelfDestroy : MonoBehaviour {

            private float lifetime;

            public void Init(float time) {
                lifetime = time;
            }

            void Update() {
                lifetime -= Time.deltaTime;
                if (lifetime <= 0f) {
                    Destroy(gameObject);
                }
            }
        }
    }
    */
}