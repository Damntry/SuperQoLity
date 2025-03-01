using HutongGames.PlayMaker;
using UnityEngine;

namespace SuperQoLity.SuperMarket.ModUtils {
	public static class AuxUtils {

		public static bool IsKeypressed(KeyCode key, bool onlyWhileChatClosed = true) =>
			(!onlyWhileChatClosed || onlyWhileChatClosed && IsChatOpen()) && Input.GetKeyDown(key);

		public static bool IsChatOpen() => FsmVariables.GlobalVariables.GetFsmBool("InChat").Value;

	}
}
