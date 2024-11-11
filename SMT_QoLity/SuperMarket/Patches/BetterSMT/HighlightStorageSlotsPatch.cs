using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers;
using UnityEngine;
using UnityEngine.Rendering;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;

namespace SuperQoLity.SuperMarket.Patches.BetterSMT
{

    /// <summary>
    /// Adds highlighting of storage slots to the BetterSMT mod.
    /// Additionally, it uses a custom implementation of highlighting based on 
    /// the one from BetterSMT, that allows customizing all highlight colors.
    /// </summary>
    public class HighlightStorageSlotsPatch : FullyAutoPatchedInstance<HighlightStorageSlotsPatch> {

		public override bool IsAutoPatchEnabled => BetterSMT_Helper.IsBetterSMTLoadedAndPatchEnabled.Value && ModConfig.Instance.EnablePatchBetterSMT_ExtraHighlightFunctions.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Extra highlight functions failed. Disabled";


		private class DisableBetterSMTChangeEquipmentPatch {

			[HarmonyPatchStringTypes($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch", "ChangeEquipmentPatch", [typeof(PlayerNetwork), typeof(int)])]
			[HarmonyBefore(BetterSMT_Helper.BetterSMTInfo.HarmonyId)]
			[HarmonyPrefix]
			//Yo dawg, I heard you like patches, so I patched the patch so it doesnt patch.
			private static bool ChangeEquipmentBetterSMTPatch(PlayerNetwork __instance, int newEquippedItem) {
				if (newEquippedItem == 0) {
					HighlightingMethods.ClearHighlightedShelves();
				}
				return false;
			}

		}

		private class UpdateBoxContentsHighlight {

			[HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.UpdateBoxContents))]
			[HarmonyPostfix]
			private static void UpdateBoxContentsPatch(PlayerNetwork __instance, int productIndex) {
				HighlightingMethods.HighlightShelvesByProduct(productIndex);
			}

		}

		private class AddHighlightsMarkerToStorageSlots {

			[HarmonyPatch(typeof(Data_Container), "BoxSpawner")]
			[HarmonyPostfix]
			/// <summary>Storage object loaded</summary>
			private static void BoxSpawnerPatch(Data_Container __instance) {
				HighlightingMethods.AddHighlightMarkersToStorage(__instance.transform);
			}

			[HarmonyPatch(typeof(NetworkSpawner), "UserCode_CmdSpawn__Int32__Vector3__Vector3")]
			[HarmonyPostfix]
			/// <summary>New buildable constructed</summary>
			private static void NewBuildableConstructed(NetworkSpawner __instance, int prefabID) {
				GameObject buildable = __instance.buildables[prefabID];

				if (buildable.name.Contains("StorageShelf")) {    //Hopefully this makes it so new storage shelfs in the future are still supported.
					int index = buildable.GetComponent<Data_Container>().parentIndex;
					Transform buildableParent = __instance.levelPropsOBJ.transform.GetChild(index);
					GameObject lastStorageObject = buildableParent.GetChild(buildableParent.childCount - 1).gameObject;

					HighlightingMethods.AddHighlightMarkersToStorage(lastStorageObject.transform);
				}
			}

		}

	}
}
