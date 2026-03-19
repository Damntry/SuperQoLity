using Damntry.Utils.Logging;
using HutongGames.PlayMaker;
using Mirror;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperQoLity.SuperMarket.ModUtils {

    public static class Tags {
        public static string Interactable { get; } = "Interactable";
        public static string Movable { get; } = "Movable";
        public static string Decoration { get; } = "Decoration";
    }

    /// <summary>
    /// Here goes the stuff I dont know where to put yet.
    /// </summary>
    public static class AuxUtils {

		private static readonly uint CoolPackSteamID = 3146530;


        public static IEnumerable<GameObject> GetRemotePlayerObjects() {
            if (WorldState.CurrentGameWorldState == GameWorldEvent.QuitOrMenu) {
                yield break;
            }

            foreach (var rootObj in SceneManager.GetActiveScene().GetRootGameObjects()) {
                if (rootObj.name.StartsWith("Player_PREFAB")) {
                    yield return rootObj;
                }
            }
        }

        public static bool IsPlayerHoldingBox(out int productId) {
            PlayerNetwork pNetwork = SMTInstances.LocalPlayerNetwork();
            if (!pNetwork) {
				TimeLogger.Logger.LogError($"The {nameof(PlayerNetwork)} instance couldnt be found." +
					$"Most probably the base code has changed or this has been called on the main menu.", 
					LogCategories.Vanilla);
            }

            productId = pNetwork.extraParameter1;
            return pNetwork.equippedItem == 1;
        }

        public static bool IsKeypressed(KeyCode key, bool onlyWhileChatClosed = true) =>
			(!onlyWhileChatClosed || onlyWhileChatClosed && IsChatOpen()) && Input.GetKeyDown(key);


		public static bool IsChatOpen() => FsmVariables.GlobalVariables.GetFsmBool("InChat").Value;

        public static bool IsMainMenuOpen() => FsmVariables.GlobalVariables.FindFsmBool("InOptions").Value;

		public static bool IsDLCSubscribed() => SteamManager.Initialized && 
			SteamApps.BIsSubscribedApp((AppId_t)CoolPackSteamID);

	}
}
