using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using Damntry.UtilsBepInEx.IL;
using Damntry.UtilsMirror.Helpers;
using Damntry.UtilsUnity.Components.InputManagement;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.BroomShotgun {

    public class BroomShotgunPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => BroomShotgunNetwork.ShotgunModuleEnabledSync.IsEnabledLocally;


        public static readonly Vector3 BroomAimLocalPos = new (0.2f, 0.25f, -1f);

        public static readonly Quaternion BroomAimLocalRotation = Quaternion.Euler(Vector3.zero);

        private static WeaponManager shotgunManager;

        private static bool pNetworkPrefabModded;


        public override string ErrorMessageOnAutoPatchFail { get; protected set; } = 
			$"{MyPluginInfo.PLUGIN_NAME} - Broom Shotgun patching failed. Disabled";


		public override void OnPatchFinishedVirtual(bool IsActive) {
			if (IsActive) {
                NetworkSpawnManager.RegisterNetwork<BroomShotgunNetwork>(
                    BroomShotgunNetwork.NetworkAssetId, isSelfManagedSpawning: false);

                InputManagerSMT.Instance.AddHotkeyFromConfig(ModConfig.Instance.BroomShotgunModeHotkey, InputState.KeyDown,
                    HotkeyActiveContext.WorldLoadedNotPaused, () => shotgunManager?.ToggleShotgunMode());
                InputManagerSMT.Instance.TryAddHotkey("broomShotgunShoot",
                    KeyCode.Mouse0, InputState.KeyDown, HotkeyActiveContext.CanShoot, 150, 
					() => shotgunManager?.LocalPlayerFire()
                );

				WorldState.OnFPControllerStarted += () => {
					shotgunManager = new();
					shotgunManager.InitializeShotgunData();
				};

				WorldState.PlayerEvents.OnChangeEquipment += ChangedEquipment;

				WorldState.OnQuitOrMainMenu += () => {
					if (shotgunManager != null) {
                        shotgunManager.LeaveShotgunAimMode();
                        shotgunManager.Destroy();
                        shotgunManager = null;
                    }
				};

                NetworkSpawnManager.OnBeforeObjectSpawn += OnBeforeSpawn;

                WeaponAudioSystem.LoadSoundFiles().FireAndForget(LogCategories.Audio);
            }
        }


        private void ChangedEquipment(PlayerNetwork __instance, Transform previousEquipped, 
                Transform newEquipped, int previousIndex, int newIndex, bool isLocalPlayer) {

			//ChangeEquipment is called for every player in the session, so we need to check if its for us.
			if (shotgunManager != null && isLocalPlayer && newIndex != (int)ToolIndexes.Broom) {
                shotgunManager.LeaveShotgunAimMode();
			}
		}
        
        private static void OnBeforeSpawn() {
            if (pNetworkPrefabModded) {
                //Only needs to be modified the first time the scene is loaded. Changes are kept after that even after the scene unloads.
                return;
            }

            //Now that the scene is loaded, find the original PlayerNetwork prefab.
            PlayerNetwork pNetwork = Resources.FindObjectsOfTypeAll<PlayerNetwork>().FirstOrDefault();
            if (!pNetwork) {
                TimeLogger.Logger.LogError("The base PlayerNetwork object couldnt be found. " +
                    "This will break the broom shotgun module.", LogCategories.Other);
                return;
            }

            pNetwork.gameObject.AddComponent<BroomShotgunNetwork>();
            WeaponAudioSystem.AddAudioSourceComponents(pNetwork.gameObject);

            pNetworkPrefabModded = true;
        }

        
        [HarmonyPatch(typeof(PlayerSyncCharacter), nameof(PlayerSyncCharacter.LateUpdate))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PlayerSyncCharacterTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            CodeMatcher matcher = new(instructions, generator);

            matcher.MatchForward(useEnd: false,
                new CodeMatch(OpCodes.Switch),
                new CodeMatch(OpCodes.Ret));

            if (matcher.IsInvalid) {
                throw new TranspilerDefaultMsgException("Couldnt find the Switch + Ret IL.");
            }

            matcher.Advance(-3) //Go at the start of the var loading used for the switch
                .CreateLabel(out Label vanillaConditions)
                //Put on the stack checking if the broom is equipped
                .InsertAndAdvance(CodeInstructionNew.LoadArgument(0))
                .InsertAndAdvance(CodeInstruction.LoadField(typeof(PlayerSyncCharacter), 
                    nameof(PlayerSyncCharacter.pNetwork)))   //Put pNetwork into the stack to pass as parameter
                .InsertAndAdvance(Transpilers.EmitDelegate(WeaponManager.IsBroomEquipped))
                //If its not, skip our custom instructions and go to vanilla code
                .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, vanillaConditions))
                //Otherwise, we call our own custom logic and exit to skip vanilla logic
                .InsertAndAdvance(CodeInstructionNew.LoadArgument(0))   //Puts "this" instance as parameter
                .InsertAndAdvance(Transpilers.EmitDelegate(CustomAnimationLogic))
                .Insert(new CodeInstruction(OpCodes.Ret));

            return matcher.InstructionEnumeration();
        }

        private static void CustomAnimationLogic(PlayerSyncCharacter __instance) {
            if (!BroomShotgunNetwork.ShotgunModuleEnabledSync.Value) {
                return;
            }

            PlayerNetwork pNetwork = __instance.pNetwork;

            if (shotgunManager == null || !pNetwork || pNetwork.equippedItem != (int)ToolIndexes.Broom) {
                return;
            }

            bool aimingPose;
            bool isLocalPlayerFiring = false;

            if (pNetwork.isLocalPlayer) {
                ShotgunStatus shotgunStatus = WeaponManager.LocalShotgunStatus;

                aimingPose = shotgunStatus == ShotgunStatus.EquipReady || shotgunStatus == ShotgunStatus.IdleCooldown;
                isLocalPlayerFiring = shotgunStatus == ShotgunStatus.FireAnimation;
            } else {
                //Logic is simpler since we dont play animations for remote players.
                BroomShotgunNetwork shotgunNetwork = pNetwork.GetComponent<BroomShotgunNetwork>();
                aimingPose = WeaponManager.IsPlayerInShotgunmode(shotgunNetwork);
            }

            //Something other than PlayerSyncCharacter.LateUpdate is modifying the rotation and 
            //  position of the held object. Maybe some rigging stuff, no idea, just overwrite where needed.
            if (aimingPose) {
                //Final broom position
                pNetwork.instantiatedOBJ.transform.localPosition = BroomAimLocalPos;
                //Order:  Z (Roll), X (Yaw/Left-Right), Y (Pitch/Up-Down)
                pNetwork.instantiatedOBJ.transform.localRotation = BroomAimLocalRotation;
            } else if (isLocalPlayerFiring) {
                //Overwrite rotation set by vanilla game with ours.
                //  Position is handled elsewhere with a LeanTween animation
                pNetwork.instantiatedOBJ.transform.localRotation = BroomAimLocalRotation;
            }

            //Reposition hand
            if (!__instance.rightHandDestinationOBJ && pNetwork.instantiatedOBJ && pNetwork.instantiatedOBJ.transform.Find("RightHandIK")) {
                __instance.rightHandDestinationOBJ = pNetwork.instantiatedOBJ.transform.Find("RightHandIK");
                __instance.rightHandConstraint.weight = 1f;
            }

            __instance.rightHandOBJ.position = __instance.rightHandDestinationOBJ.position;
            __instance.rightHandOBJ.rotation = __instance.rightHandDestinationOBJ.rotation;
            __instance.rightHandOBJ.transform.localRotation = Quaternion.Euler(90f, 45f, 70f);
        }


        //TODO 1 - Reduce player animation time when shot by shotgun, but not when whacked by the broom.
        //  This is for PVP so the player has time to recover from being shot while the other player is still reloading.
        //  This transpiler below (untested) should work but more needs to be done:
        //      - Recovery time should be reduced to 0.75f ONLY when its from being shot, and not every push source.
        //      - When shot, the animation should be speed up too in PlayerNetwork.UserCode_RpcPushPlayer__Vector3,
        //          otherwise it would look janky when the player recovers mid-animation.
        //      - Add a new condition in UserCode_RpcPushPlayer__Vector3 so not only it checks
        //          isBeingPushed, but also a new var I would create that would stay true for 1.5
        //          seconds to keep the original invulnerability time.
        /*
        [HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.PushCoroutine), MethodType.Enumerator)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PushCoroutineTranspiler(
                IEnumerable<CodeInstruction> instructions, ILGenerator generator) {


            ///Old C#
            ///     yield return new WaitForSeconds(1.5f);
            ///
            ///New C#w
            ///     yield return new WaitForSeconds(0.75f);

            CodeMatcher matcher = new(instructions, generator);

            ConstructorInfo WaitForSecondsC = AccessTools.Constructor(typeof(WaitForSeconds), [], searchForStatic: false);
            if (WaitForSecondsC == null) {
                TimeLogger.Logger.LogError($"The constructor for the class {nameof(WaitForSeconds)} couldnt be found.",
                    LogCategories.Other);
                return instructions;
            }

            matcher.MatchForward(useEnd: false,
                new CodeMatch(OpCodes.Newobj, WaitForSecondsC));

            if (matcher.IsInvalid) {
                TimeLogger.Logger.LogError($"The IL line using the {nameof(WaitForSeconds)} logic couldnt be found.",
                    LogCategories.Other);
                return instructions;
            }

            matcher
                .Advance(-1)
                .Operand = 0.75f;

            return matcher.InstructionEnumeration();
        }
        */

    }

}
