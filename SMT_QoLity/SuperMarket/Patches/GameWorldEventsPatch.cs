using System;
using System.Diagnostics;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches {

	//TODO 6 - Find a proper way to get when the game finishes loading, instead of this all-nighter borne curse.
	//		It has to be somewhere in the FSM.
	/// <summary>
	/// Detects:
	///		- When the game finishes loading by checking when the black fade out transition ends.
	///		- When the user quits to the main menu.
	/// </summary>
	public class GameWorldEventsPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => true;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Detection of finished game loading failed. Disabled";


		private class DetectGameLoadFinished {

			private enum DetectionState {
				Initial,
				Success,
				Failed
			}


			private static readonly Stopwatch sw = Stopwatch.StartNew();

			private static GameObject transitionBCKobj;

			private static DetectionState state = DetectionState.Initial;

						
			[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnStartServer))]
			[HarmonyPrefix]
			public static void OnStartHostPrefixPatch() {
				SetLoadingWorld();
			}

			[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnStartClient))]
			[HarmonyPrefix]
			public static void OnStartClientPrefixPatch() {
				//A host also triggers OnStartClient, and we only need to trigger once.
				if (WorldState.CurrenOnlineMode != GameOnlineMode.Host) {
					SetLoadingWorld();
				}
			}

			private static void SetLoadingWorld() {
				GameOnlineMode mode = NetworkServer.activeHost ? GameOnlineMode.Host : GameOnlineMode.Client;
				WorldState.CurrenOnlineMode = mode;
				WorldState.SetGameWorldState(GameWorldEvent.LoadingWorld);
			}

			[HarmonyPatch(typeof(GameCanvas), "Awake")]
			[HarmonyPostfix]
			private static void GameCanvasAwake(GameCanvas __instance) {
				WorldState.SetGameWorldState(GameWorldEvent.CanvasLoaded);
			}

			[HarmonyPatch(typeof(GameCanvas), "Update")]
			[HarmonyPostfix]
			private static void GameCanvasUpdatePostFix(GameCanvas __instance) {
				if (sw.ElapsedMilliseconds < 100) { //Reduce check frequency for performance.
					return;
				}

				try {
					//Every time we exit to main menu, transitionBCKobj becomes null again. So at
					//	this point where GameCanvas is updating, we know we are loading the game.
					if (transitionBCKobj == null) {
						state = DetectionState.Initial;

						transitionBCKobj = GameObject.Find("MasterOBJ/MasterCanvas/TransitionBCK");
					}

					//The moment transitionBCKobj is not null, and then its activeSelf becomes false, is
					//	when the loading black fadeout has finished and the game has started fully.
					//	Seems like transitionBCKobj is controlled in some FSM with a timer that is not
					//	hooked (or badly hooked), to when the scene finishes loading. Maybe even by design.
					if (transitionBCKobj != null && !transitionBCKobj.activeSelf && state == DetectionState.Initial) {
						state = DetectionState.Success;

						WorldState.SetGameWorldState(GameWorldEvent.WorldStarted);
					}
				} catch (Exception ex) {
					state = DetectionState.Failed;
					TimeLogger.Logger.LogTimeExceptionWithMessage("Error while trying to detect game finished loading.", ex, LogCategories.Loading);
				} finally {
					GameWorldEventsPatch instance = Container<GameWorldEventsPatch>.Instance;
					if (state == DetectionState.Failed) {
						//Something changed and it wont work anymore without a mod update. Unpatch everything and forget it exists.
						TimeLogger.Logger.LogTimeError(instance.ErrorMessageOnAutoPatchFail, LogCategories.Loading);
						instance.UnpatchInstance();

						sw.Stop();
					} else {
						sw.Restart();
					}
				}

			}
		}


		private class DetectQuitToMainMenu {

			[HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnClientDisconnect))]
			[HarmonyPostfix]
			public static void OnClientDisconnectPatch(GameCanvas __instance) {
				TimeLogger.Logger.LogTimeDebug("Quitting to main menu.", LogCategories.Other);
				WorldState.CurrenOnlineMode = GameOnlineMode.None;
				WorldState.SetGameWorldState(GameWorldEvent.QuitOrMenu);
			}

		}

	}
}
