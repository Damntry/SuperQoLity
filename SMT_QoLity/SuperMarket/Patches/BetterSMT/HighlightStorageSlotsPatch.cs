using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers;
using UnityEngine;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;

namespace SuperQoLity.SuperMarket.Patches.BetterSMT
{

    /// <summary>
    /// Adds highlighting of storage slots to the BetterSMT mod.
    /// Additionally, it uses a custom implementation of highlighting based on 
    /// the one from BetterSMT, that allows customizing all highlight colors.
    /// </summary>
    public class HighlightStorageSlotsPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => BetterSMT_Helper.IsBetterSMTLoadedAndPatchEnabled.Value && ModConfig.Instance.EnablePatchBetterSMT_ExtraHighlightFunctions.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Extra highlight functions failed. Disabled";



		private class ReplaceBetterSMTChangeEquipmentPatch {

			[HarmonyPatchStringTypes($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch", "ChangeEquipmentPatch", [typeof(PlayerNetwork), typeof(int)])]
			[HarmonyBefore(BetterSMT_Helper.BetterSMTInfo.HarmonyId, BetterSMT_Helper.BetterSMTInfo.HarmonyId_New)]
			[HarmonyPrefix]
			//Yo dawg, I heard you like patches, so I patched the patch so it doesnt patch.
			private static bool ChangeEquipmentBetterSMTPatch(PlayerNetwork __instance, int newEquippedItem) {
				//In reality, both patches work. What happens is that my prefix patch replaces his
				//	patch code, and when the original ChangeEquipment method is called, his patch
				//	code is invoked, which is just my code now.
				if (newEquippedItem == 0) {
					HighlightingMethods.ClearHighlightedShelves();
				}
				return false;
			}

		}

		/// <summary>
		/// For post-Viviko BetterSMT versions (> 1.6.2), patches the BetterSMT method 
		/// UpdateBoxContentsPatch itself, so it uses my updated highlight method instead.
		/// </summary>
		private class ReplaceBetterSMTUpdateBoxContentsPatch {

			/// <summary>Harmony will only execute this class patch if Prepare returns true.</summary>
			[HarmonyPrepare]
			private static bool Prepare() =>
				BetterSMT_Helper.BetterSMTInfo.LoadedVersion > BetterSMT_Helper.BetterSMTInfo.LastVivikoVersion;


			[HarmonyPatchStringTypes($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch", "UpdateBoxContentsPatch")]
			[HarmonyBefore(BetterSMT_Helper.BetterSMTInfo.HarmonyId, BetterSMT_Helper.BetterSMTInfo.HarmonyId_New)]
			[HarmonyPrefix]
			private static bool UpdateBoxContentsPatch(PlayerNetwork __instance, int productIndex) {
				//Overwrite BetterSMT patch so it uses my code instead.
				HighlightingMethods.HighlightShelvesByProduct(productIndex);

				return false;
			}

		}

		/// <summary>
		/// For older Viviko BetterSMT versions (<= 1.6.2), patches the vanilla UpdateBoxContents
		/// method so it uses my updated highlighting method instead.
		/// </summary>
		private class UpdateBoxContentsHighlight {

			/// <summary>Harmony will only execute this class patch if Prepare returns true.</summary>
			[HarmonyPrepare]
			private static bool Prepare() =>
				BetterSMT_Helper.BetterSMTInfo.LoadedVersion <= BetterSMT_Helper.BetterSMTInfo.LastVivikoVersion;

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
