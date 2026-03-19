using Damntry.UtilsUnity.ExtensionMethods;
using Damntry.UtilsUnity.Resources;
using Damntry.UtilsUnity.Vectors;
using HutongGames.PlayMaker;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects;
using SuperQoLity.SuperMarket.Patches.BroomShotgun;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers {

    public class WeaponAnimationSystem {


        private readonly static Vector3 ShellOffsetPosition = new (0.25f, -0.45f, 0.425f);

        private readonly static Vector3 ShellEjectionForce = Vector3.up * 2.9f;

        private readonly static float aimAnimationTime = 0.375f;


        private GameObject shellfObj;

        private LTDescr aimMoveAnimation;

        private LTDescr aimRotateAnimation;

        private LTDescr recoilMoveAnimation;


        public void Initialize(GameObject fpController) {
            fpController.AddComponent<ShotgunEffectsBehaviour>();

            shellfObj = LoadShotgunShell();

            LeanTween.init();
        }


        private GameObject LoadShotgunShell() {
            AssetBundleElement asset = new (typeof(Plugin), $"{WeaponManager.WeaponsAssetsBundlePath}\\shotgunshell");
            if (asset.TryLoadObject("shotgunshell", out shellfObj)) {
                Renderer render = shellfObj.GetComponent<Renderer>();
                render.material.shader = ShaderUtils.SMT_Shader.Value;

                Rigidbody rigid = shellfObj.GetComponent<Rigidbody>();
                //Make collision ignore players
                rigid.excludeLayers = SMTLayers.ConvertLayersToMask(SMT_Layers.Player);
            }

            return shellfObj;
        }

        public void SpawnShotgunShell(PlayerNetwork playerNetwork) {
            if (shellfObj) {
                GameObject shellObjTemp = UnityEngine.Object.Instantiate(shellfObj);

                Rigidbody rigid = shellObjTemp.GetComponent<Rigidbody>();

                Vector3 shellOffsetPos = VectorUtils.OffsetTransformPos(playerNetwork.transform, ShellOffsetPosition);
                rigid.position = shellOffsetPos + Camera.main.transform.position;

                //Use the default prefab rotation, except to keep the camera yaw
                Vector3 euler = shellfObj.transform.eulerAngles;
                euler.y = Camera.main.transform.eulerAngles.y;
                rigid.rotation = Quaternion.Euler(euler);

                //Apply force to simulate shell ejection, plus some player movement
                Vector3 shellEjectForce = ShellEjectionForce + Camera.main.transform.right * 0.5f;
                rigid.velocity = shellEjectForce + Camera.main.velocity / 2;

                //Avoid shell motion jitter while moving the camera.
                rigid.interpolation = RigidbodyInterpolation.Interpolate;

                UnityEngine.Object.Destroy(shellObjTemp, 3f);
            }
        }

        public void StartShotgunAimAnimation(PlayerNetwork playerNetwork, Action onComplete) {
            GameObject instantiatedOBJ = playerNetwork.instantiatedOBJ;

            aimMoveAnimation = LeanTween
                .moveLocal(instantiatedOBJ, BroomShotgunPatch.BroomAimLocalPos, aimAnimationTime)
                .setEase(LeanTweenType.easeInOutQuad);
            aimRotateAnimation = LeanTween.rotateAroundLocal(instantiatedOBJ, Vector3.right, 90, aimAnimationTime)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(onComplete);
        }

        public void StartShotgunRecoilAnimation(GameObject broomObj, Action onComplete) {
            Vector3 to = Vector3.zero + Vector3.back * 1.3f + Vector3.right * 0.3f;
            recoilMoveAnimation = LeanTween.moveLocal(broomObj, to, 0.2f)
                //Something else is changing the broom location and rotation but I cant find it,
                //  so just specify the starting point. Otherwise Lean will be using the one 
                //  after the value has already been changed.
                .setForceFromCurrentLocalPosition()
                .setEase(LeanTweenType.easeOutExpo)
                .setOnComplete(
                    () => {
                        LeanTween.moveLocal(broomObj, BroomShotgunPatch.BroomAimLocalPos, 0.65f)
                            .setForceFromCurrentLocalPosition()
                            .setEase(LeanTweenType.easeInOutQuad)
                            .setOnComplete(onComplete);
                    }
                );
        }

        public void CancelAnimations() {
            if (aimMoveAnimation != null) {
                LeanTween.cancel(aimMoveAnimation.id);
            }
            if (aimRotateAnimation != null) {
                LeanTween.cancel(aimRotateAnimation.id);
            }
            if (recoilMoveAnimation != null) {
                LeanTween.cancel(recoilMoveAnimation.id);
            }
        }


    }
}
