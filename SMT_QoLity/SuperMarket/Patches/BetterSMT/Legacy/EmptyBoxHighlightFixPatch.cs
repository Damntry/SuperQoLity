using System.Reflection;
using System;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;


namespace SuperQoLity.SuperMarket.Patches.BetterSMT.Legacy {

	/// <summary>
	/// Legacy fix for highlights not updating when holding an empty box and
	///	retrieving from a shelf a different product than the one in the box.
	///	Only used for BetterSMT versions <= 1.6.2
	///	
	///	* Changes highlighting logic from the method "ChangeEquipment" to "UpdateBoxContents",
	///		since the latter is also called in the case that didnt work.
	/// </summary>
	[HarmonyPatch]
	public class EmptyBoxHighlightFixPatch : FullyAutoPatchedInstance {

		/*
		 * This class is pretty much a partial clone of HighlightStorageSlotsPatch, except 
		 *	it uses the original BetterSMT methods and has none of the extra functionality.
		 * Once BetterSMT dev fixes the highlight bug in their code, I can just delete this class.
		 */

		public override bool IsAutoPatchEnabled => BetterSMT_Helper.IsEnableBetterSMTHightlightFixes.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - BetterSMT highlight fix failed. Disabled";

		public static readonly Lazy<MethodInfo> HighlightShelvesByProductMethod = new Lazy<MethodInfo>(() =>
			AccessTools.Method($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch:HighlightShelvesByProduct", [typeof(int)]));
		public static readonly Lazy<MethodInfo> ClearHighlightedShelvesMethod = new Lazy<MethodInfo>(() =>
			AccessTools.Method($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch:ClearHighlightedShelves"));


		private class DisableBetterSMTChangeEquipmentPatch {

			[HarmonyPatchStringTypes($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch", "ChangeEquipmentPatch", [typeof(PlayerNetwork), typeof(int)])]
			[HarmonyBefore(BetterSMT_Helper.BetterSMTInfo.HarmonyId, BetterSMT_Helper.BetterSMTInfo.HarmonyId_New)]
			[HarmonyPrefix]
			//Yo dawg, I heard you like patches, so I patched the patch so it doesnt patch.
			private static bool ChangeEquipmentBetterSMTPatch(PlayerNetwork __instance, int newEquippedItem) {
				if (newEquippedItem == 0) {
					ClearHighlightedShelvesMethod.Value.Invoke(null, null);
				}
				return false;
			}

		}

		private class UpdateBoxContentsHighlight {

			[HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.UpdateBoxContents))]
			[HarmonyPostfix]
			private static void UpdateBoxContentsPatch(PlayerNetwork __instance, int productIndex) {
				HighlightShelvesByProductMethod.Value.Invoke(null, [productIndex]);
			}

		}

	}

}