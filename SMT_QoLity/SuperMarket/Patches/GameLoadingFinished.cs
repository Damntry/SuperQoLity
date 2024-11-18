using System;
using System.Diagnostics;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;

namespace SuperQoLity.SuperMarket.Patches
{

    //TODO 6 - Find a proper way to get when the game finishes loading, instead of this all-nighter borne curse.

    /// <summary>
    /// Detects when the game finishes loading by checking when the black fade out transition ends.
    /// Currently used to activate pending message notifications that dont work earlier.
    /// </summary>
    public class GameLoadingFinished : HybridPatchedInstance {

		public override bool IsAutoPatchEnabled => GameNotifications.Instance.NotificationSystemEnabled;

		public override bool IsRollbackOnAutoPatchFail => false;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Custom mod notifications patch failed. Disabled";


		public override bool IsPatchActive { get; protected set; } = false;

		public override event Action<bool> OnPatchFinished;


		private class LoadScenes {

			[HarmonyPatch(typeof(GameCanvas), "Awake")]
			[HarmonyPostfix]
			private static void AwakePostFix(GameCanvas __instance) {
				bool notifObjOk = GameNotifications.Instance.TestNotificationObjects();

				if (notifObjOk) {
					Instance.PatchClassByType(typeof(GameLoadingCheck));
				} else {
					//Notification objects are not working. Unpatch so we stop trying.
					Instance.UnpatchInstance();
				}
			}

		}


		/// <summary>
		/// This class is patched manually
		/// </summary>
		[HarmonyPatch]
		[AutoPatchIgnoreClass]
		private class GameLoadingCheck {

			private readonly static Stopwatch updateCooldown = new Stopwatch();

			private enum NotificationState {
				Initial,
				Success,
				Failed
			}

			[HarmonyPatch(typeof(GameData), "Update")]
			[HarmonyPostfix]
			private static void Update_Postfix(GameData __instance) {
				if (updateCooldown.IsRunning && updateCooldown.ElapsedMilliseconds < 500) {
					return;
				}
				
				NotificationState state = NotificationState.Initial;
				try {
					//The moment transitionBCKobj is not null, and then its activeSelf becomes false, is when the
					//	loading black fadeout has finished and it should allow me to show messages.
					GameObject transitionBCKobj = GameObject.Find("MasterOBJ/MasterCanvas/TransitionBCK");

					if (transitionBCKobj != null && !transitionBCKobj.activeSelf) {
						//TODO 5 - Test this transitionBKC thing while being a client.

						//Enable showing notifications on screen
						bool success = GameNotifications.Instance.EnableShowingNotifications();

						if (success) {
							state = NotificationState.Success;
						} else {
							state = NotificationState.Failed;
						}

						//Unpatch so we stop triggering on updates until next GameCanvas.Awake()
						Instance.UnpatchInstanceMethod(typeof(GameData), "Update");

						updateCooldown.Stop();
					} else {
						updateCooldown.Restart();
					}

				} catch (Exception ex) {
					state = NotificationState.Failed;
					BepInExTimeLogger.Logger.LogTimeExceptionWithMessage("Error while in the Update to enable game notifications.", ex, TimeLoggerBase.LogCategories.Notifs);
				} finally {
					if (state == NotificationState.Failed) {
						TimeLoggerBase.RemoveGameNotificationSupport();

						//Something changed and it wont work anymore without a mod update. Unpatch everything and forget it exists.
						//	This should be shown in game but since the notification system failed... well.
						BepInExTimeLogger.Logger.LogTimeError(Instance.ErrorMessageOnAutoPatchFail, TimeLoggerBase.LogCategories.Notifs);
						Instance.UnpatchInstance();
					}

					Instance.IsPatchActive = state == NotificationState.Success;

					if (Instance.OnPatchFinished != null && state == NotificationState.Success || state == NotificationState.Failed) {
						Instance.OnPatchFinished(Instance.IsPatchActive);
					}
				}

			}

		}

	}
}
