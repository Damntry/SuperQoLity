using System;
using System.Diagnostics;
using Damntry.Utils.Logging;
using HarmonyLib;
using UnityEngine;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;

namespace SuperQoLity.SuperMarket.Patches {

	public enum GameWorldEvent {
		Start,
		Quit
	}

	//TODO 6 - Find a proper way to get when the game finishes loading, instead of this all-nighter borne curse.
	/// <summary>
	/// Detects:
	///		- When the game finishes loading by checking when the black fade out transition ends.
	///		- When the user quits to the main menu.
	/// </summary>
	public class GameWorldEventsPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => true;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Detection of finished game loading failed. Disabled";


		public static event Action<GameWorldEvent> OnGameWorldChange;


		private class DetectGameLoadFinished {

			private enum DetectionState {
				Initial,
				Success,
				Failed
			}

			private static Stopwatch sw = Stopwatch.StartNew();

			private static GameObject transitionBCKobj;

			private static DetectionState state = DetectionState.Initial;


			[HarmonyPatch(typeof(GameCanvas), "Update")]
			[HarmonyPostfix]
			private static void GameCanvasUpdatePostFix(GameCanvas __instance) {
				if (sw.ElapsedMilliseconds < 500) { //Reduce check frequency for performance.
					return;
				}

				try {
					//Every time we exit to main menu, transitionBCKobj becomes null again.
					if (transitionBCKobj == null) {
						state = DetectionState.Initial;
						transitionBCKobj = GameObject.Find("MasterOBJ/MasterCanvas/TransitionBCK");
					}

					//The moment transitionBCKobj is not null, and then its activeSelf becomes false, is
					//	when the loading black fadeout has finished and the game has started fully.
					if (transitionBCKobj != null && !transitionBCKobj.activeSelf && state == DetectionState.Initial) {
						//TODO 3 - Test this transitionBKC thing while being a client.

						state = DetectionState.Success;

						if (OnGameWorldChange != null) {
							OnGameWorldChange(GameWorldEvent.Start);
						}
					}
				} catch (Exception ex) {
					state = DetectionState.Failed;
					TimeLogger.Logger.LogTimeExceptionWithMessage("Error while trying to detect game finished loading.", ex, TimeLogger.LogCategories.Loading);
				} finally {
					GameWorldEventsPatch instance = Container<GameWorldEventsPatch>.Instance;
					if (state == DetectionState.Failed) {
						//Something changed and it wont work anymore without a mod update. Unpatch everything and forget it exists.
						TimeLogger.Logger.LogTimeError(instance.ErrorMessageOnAutoPatchFail, TimeLogger.LogCategories.Loading);
						instance.UnpatchInstance();

						sw.Stop();
					} else {
						sw.Restart();
					}
				}

			}
		}

		private class DetectQuitToMainMenu {

			[HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.LocalHostDisconnect))]
			[HarmonyPostfix]
			public static void LocalHostDisconnectPatch(GameCanvas __instance) {
				if (OnGameWorldChange != null) {
					OnGameWorldChange(GameWorldEvent.Quit);
				}
			}

		}

	}
}
