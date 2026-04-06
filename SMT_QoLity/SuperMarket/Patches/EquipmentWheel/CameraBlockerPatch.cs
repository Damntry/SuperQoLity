using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsMirror.Helpers;
using HarmonyLib;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.EquipmentWheel {

	/// <summary>
	/// Patches the camera to block and unlock on petition, while still allowing keyboard movement.
	/// </summary>
	public class CameraBlockerPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => RadialWheelNetwork.RadialEnabledSync.IsEnabledLocally;

        public override string ErrorMessageOnAutoPatchFail { get; protected set; } =
            $"{MyPluginInfo.PLUGIN_NAME} - Camera blocker failed. Disabled.";


        public override void OnPatchFinishedVirtual(bool IsPatchActive) {
            if (IsPatchActive) {
                NetworkSpawnManager.RegisterNetwork<RadialWheelNetwork>(RadialWheelNetwork.NetworkAssetId);
                RadialWheelManager.Initialize();

                WorldState.PlayerEvents.OnChangeEquipment += RadialWheelManager.OnChangedEquipment;
            }
        }


        private static float xPlayerRotation;
        private static float yCameraRotation;


        [HarmonyPatch(typeof(CustomCameraController), nameof(CustomCameraController.LateUpdate))]
		[HarmonyPrefix]
		static void LateUpdatePatchPrefix(CustomCameraController __instance) {
			if (ShouldBlockCameraMovement(__instance)) {
                xPlayerRotation = __instance.x;
                yCameraRotation = __instance.y;
            }
		}

        [HarmonyPatch(typeof(CustomCameraController), nameof(CustomCameraController.LateUpdate))]
        [HarmonyPostfix]
        static void LateUpdatePatchPostfix(CustomCameraController __instance) {
            if (ShouldBlockCameraMovement(__instance)) {
                //Restore character and camera rotation to previous values.
                __instance.masterPlayerOBJ.transform.rotation = Quaternion.Euler(0f, xPlayerRotation, 0f);
                __instance.cinemachineOBJ.transform.localRotation = Quaternion.Euler(yCameraRotation, 0f, 0f);
                __instance.x = xPlayerRotation;
                __instance.y = yCameraRotation;
            }
        }

        private static bool ShouldBlockCameraMovement(CustomCameraController __instance) =>
            RadialWheelManager.BlockCameraMovement &&
                __instance.masterPlayerOBJ && __instance.cinemachineOBJ &&
                !__instance.inVehicle && !__instance.isInCameraEvent && !__instance.IsInOptions;

    }
}
