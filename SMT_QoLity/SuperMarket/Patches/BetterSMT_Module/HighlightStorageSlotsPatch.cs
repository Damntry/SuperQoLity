using System;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.ExternalMods;
using SuperQoLity.SuperMarket.PatchClassHelpers;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.HighlightModule {

	/// <summary>
	/// Adds highlighting of shelves and storage slots.
	/// </summary>
	public class HighlightStorageSlotsPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnablePatchHighlight.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Highlight patching failed. Disabled";


		public override void OnPatchFinishedVirtual(bool IsActive) {
			if (IsActive) {
				WorldState.OnGameWorldChange += (ev) => {
					if (ev == GameWorldEvent.WorldStarted) {
						ShelfHighlighting.InitHighlightCache();
					}
				};

				/*
				//Highlight cache test
				KeyPressDetection.AddHotkey(KeyCode.O, 700, () => {
					ShelfHighlighting.IsHighlightCacheUsed = !ShelfHighlighting.IsHighlightCacheUsed;
					LOG.DEBUGWARNING($"{(ShelfHighlighting.IsHighlightCacheUsed ? "Highlighting Cache ENABLED" : "Highlighting Cache DISABLED")}");
				});
				*/
			}
		}

		/// <summary>
		/// If BetterSMT is not loaded, patches all pickup/drop box methods to set the highlighting.
		/// </summary>
		private class EnableHighlighting {

			[HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.ChangeEquipment))]
			[HarmonyPostfix]
			private static void ChangeEquipmentPatch(PlayerNetwork __instance, int newEquippedItem) {
				//ChangeEquipment is called locally for every player, so we need to check if its for the local player.
				if (__instance.isLocalPlayer && newEquippedItem == 0) { 
					ShelfHighlighting.ClearHighlightedShelves();
				}
				
			}

			[HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.UpdateBoxContents))]
			[HarmonyPostfix]
			private static void UpdateBoxContentsPatch(PlayerNetwork __instance, int productIndex) {
				ShelfHighlighting.HighlightShelvesByProduct(productIndex);
			}

		}

		private class AddHighlightsMarkerToStorageSlots {

			[HarmonyPatch(typeof(Data_Container), nameof(Data_Container.BoxSpawner))]
			[HarmonyPostfix]
			/// <summary>Storage object loaded</summary>
			private static void BoxSpawnerPatch(Data_Container __instance) {
				ShelfHighlighting.AddHighlightMarkersToStorage(__instance.transform);
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

					ShelfHighlighting.AddHighlightMarkersToStorage(lastStorageObject.transform);
				}
			}

		}

		/// <summary>
		/// If BetterSMT is loaded, patch his methods so we use our highlighting instead.
		/// </summary>
		private class BetterSMTRemoveHighlighting {

			[HarmonyPrepare]
			private static bool HarmonyPrepare() => BetterSMT_Helper.Instance.IsModLoadedAndEnabled;

			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "ChangeEquipmentPatch")]
			[HarmonyBefore(ModInfoBetterSMT.HarmonyId)]
			[HarmonyPrefix]
			//Yo dawg, I heard you like patches, so I patched the patch so it doesnt patch.
			private static bool ChangeEquipmentBetterSMTPatch(PlayerNetwork __instance, int newEquippedItem) {
				//In reality, both patches still exist. What happens is that my prefix patch replaces his
				//	patch code, and when the original ChangeEquipment method is called, his patch
				//	code is invoked, which is just my code now.
				return false;
			}

			/// <summary>
			/// For post-Viviko BetterSMT versions (> 1.6.2), patches the BetterSMT method 
			/// UpdateBoxContentsPatch itself, so it uses my updated highlight method instead.
			/// </summary>

			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "UpdateBoxContentsPatch")]
			[HarmonyBefore(ModInfoBetterSMT.HarmonyId, ModInfoBetterSMT.HarmonyId)]
			[HarmonyPrefix]
			private static bool UpdateBoxContentsPatch(PlayerNetwork __instance, int productIndex) {
				//Overwrite BetterSMT patch so the game only uses my code instead.
				return false;
			}

		}

		private class BetterSMTRemoveMarkerHighlighting {

			[HarmonyPrepare]
			private static bool HarmonyPrepare() => 
				BetterSMT_Helper.Instance.IsModLoadedAndEnabled && IsBetterSMTVersionWithMarkers();

			private static bool IsBetterSMTVersionWithMarkers() =>
				AssemblyUtils.GetMethodFromLoadedAssembly(
					$"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "BoxSpawnerPatch", true
				) != null;


			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "BoxSpawnerPatch")]
			[HarmonyPrefix]
			private static bool BoxSpawnerPatch(Data_Container __instance) {
				return false;
			}

			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "NewBuildableConstructed")]
			[HarmonyPrefix]
			private static bool NewBuildableConstructed(NetworkSpawner __instance, int prefabID) {
				return false;
			}

		}


		/* Not needed since integrating highlighting from BetterSMT
		
		[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "HighlightShelvesByProduct")]
		[HarmonyBefore(ModInfoBetterSMT.HarmonyId)]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> ModifyShelfColorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			//Changes color passed by parameter so it uses my config settings instead of a fixed color.

			///Old C#:
			///		PlayerNetworkPatch.HighlightShelfTypeByProduct(productID, 
			///			Color.yellow, 
			///			PlayerNetworkPatch.ShelfType.ProductDisplay);
			///		PlayerNetworkPatch.HighlightShelfTypeByProduct(productID, 
			///			Color.red, 
			///			PlayerNetworkPatch.ShelfType.Storage);
			///New C#:
			///		PlayerNetworkPatch.HighlightShelfTypeByProduct(productID, 
			///			ModConfig.Instance.BetterSMT_ShelfHighlightColor.Value, 
			///			PlayerNetworkPatch.ShelfType.ProductDisplay);
			///		PlayerNetworkPatch.HighlightShelfTypeByProduct(productID, 
			///			ModConfig.Instance.BetterSMT_StorageHighlightColor.Value, 
			///			PlayerNetworkPatch.ShelfType.Storage);
			///		}

			//First color
			codeMatcher.MatchForward(true,
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(inst => inst.opcode == OpCodes.Call));

			if (!codeMatcher.IsValid) {
				throw new TranspilerDefaultMsgException($"IL line to get the first color could not be found.");
			}

			codeMatcher.SetInstruction(Transpilers.EmitDelegate(() => 
				ModConfig.Instance.BetterSMT_ShelfHighlightColor.Value));

			//Second color
			codeMatcher.MatchForward(true,
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(inst => inst.opcode == OpCodes.Call));

			if (!codeMatcher.IsValid) {
				throw new TranspilerDefaultMsgException($"IL line to get the second color could not be found.");
			}

			codeMatcher.SetInstruction(Transpilers.EmitDelegate(() =>
				ModConfig.Instance.BetterSMT_StorageHighlightColor.Value));

			return codeMatcher.InstructionEnumeration();
		}

		
		[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "HighlightShelfTypeByProduct")]
		[HarmonyBefore(ModInfoBetterSMT.HarmonyId)]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> ModifySlotColorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			//Changes color passed by parameter so it uses my config settings instead of a fixed color.

			///Old C#:
			///		HighlightShelf(specificHighlight, true, Color.yellow);
			///New C#:
			///		Color slotColor = shelfType == ShelfType.Storage ? 
			///			ModConfig.Instance.BetterSMT_StorageSlotHighlightColor.Value : 
			///			ModConfig.Instance.BetterSMT_ShelfLabelHighlightColor.Value;
			///		HighlightShelf(specificHighlight, true, slotColor);

			//First color
			codeMatcher.MatchForward(true,
					new CodeMatch(inst => inst.IsLdloc()),
					new CodeMatch(inst => inst.opcode == OpCodes.Ldc_I4_1),
					new CodeMatch(inst => inst.opcode == OpCodes.Call));

			if (!codeMatcher.IsValid) {
				throw new TranspilerDefaultMsgException($"IL line to get the slot color could not be found.");
			}

			//Replace existing instruction to get the fixed color, with the argument with the current shelf type.
			codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldarg_2))
				.Advance(1)
				//Call the function that returns the color depending on the shelf type we just added on the stack.
				.Insert(Transpilers.EmitDelegate(GetShelfSlotColor));

			return codeMatcher.InstructionEnumeration();
		}

		private static Color GetShelfSlotColor(int shelfType) =>
			shelfType == 0 ? 
				ModConfig.Instance.BetterSMT_ShelfLabelHighlightColor.Value :
				ModConfig.Instance.BetterSMT_StorageSlotHighlightColor.Value;
		*/

	}
}
