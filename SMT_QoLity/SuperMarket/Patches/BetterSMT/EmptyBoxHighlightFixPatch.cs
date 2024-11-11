using System.Reflection;
using System;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;


namespace SuperQoLity.SuperMarket.Patches.BetterSMT
{

    /// <summary>
    /// Fix for highlights not updating when holding an empty box and
    ///	retrieving from a shelf a different product than the one in the box.
    ///	
    ///	* Changes highlighting logic from the method "ChangeEquipment" to "UpdateBoxContents",
    ///		since the latter is also called in the case that didnt work.
    /// </summary>
    [HarmonyPatch]
	public class EmptyBoxHighlightFixPatch : FullyAutoPatchedInstance<EmptyBoxHighlightFixPatch> {

		/*
		 * This class is pretty much a partial clone of HighlightStorageSlotsPatch, except 
		 *	it uses the original BetterSMT methods and has none of the extra functionality.
		 * Once BetterSMT dev fixes the highlight bug in their code, I can just delete this class.
		 */

		public override bool IsAutoPatchEnabled => BetterSMT_Helper.IsBetterSMTLoadedAndPatchEnabled.Value && !ModConfig.Instance.EnablePatchBetterSMT_ExtraHighlightFunctions.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - BetterSMT box highlight fix failed. Disabled";

		public static readonly Lazy<MethodInfo> HighlightShelvesByProductMethod = new Lazy<MethodInfo>(() =>
			AccessTools.Method($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch:HighlightShelvesByProduct", [typeof(int)]));
		public static readonly Lazy<MethodInfo> ClearHighlightedShelvesMethod = new Lazy<MethodInfo>(() =>
			AccessTools.Method($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch:ClearHighlightedShelves"));


		private class DisableBetterSMTChangeEquipmentPatch {

			[HarmonyPatchStringTypes($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch", "ChangeEquipmentPatch", [typeof(PlayerNetwork), typeof(int)])]
			[HarmonyBefore(BetterSMT_Helper.BetterSMTInfo.HarmonyId)]
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