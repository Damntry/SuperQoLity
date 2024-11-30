using System.ComponentModel;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.StorageSearch {

	public class StorageSearchHelpers {

		public enum FreeStoragePriorityEnum {
			[Description("1.Assigned storage > 2.Any other storage")]
			Default_Any,
			[Description("1.Assigned storage > 2.Labeled > 3.Unlabeled")]
			Labeled,
			[Description("1.Assigned storage > 2.Unlabeled > 3.Labeled")]
			Unlabeled
		}


		public static StorageSlotInfo GetStorageContainerWithBoxToMerge(NPC_Manager __instance, int boxIDProduct) {
			return StorageSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageId, slotId, productId, quantity) => {

					if (productId == boxIDProduct && quantity > 0 &&
							quantity < ProductListing.Instance.productPrefabs[productId].GetComponent<Data_Product>().maxItemsPerBox) {
						return StorageSearchLambdas.LoopStorageAction.SaveAndExit;
					}

					return StorageSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

		public static StorageSlotInfo FreeUnassignedStorageContainer(NPC_Manager __instance) {
			return GetFreeStorageContainer(__instance, -1);
		}

		public static StorageSlotInfo GetFreeStorageContainer(NPC_Manager __instance, int boxIDProduct) {
			return StorageSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					//Ìf boxIDProduct is zero or positive, it means finding free storage with the same assigned item id is the max
					//	priority, but if it is unassigned or unlabeled, the priority setting is used to value one over the other.
					//Ìf boxIDProduct is negative, the assigned storage slots are skipped, and only the priority setting matters on
					//	 the unassigned vs unlabeled choice.

					if (boxIDProduct >= 0 && productId >= 0) {
						//Search an storage slot with this assigned boxIDProduct
						if (productId == boxIDProduct && quantity < 0) {
							//Empty assigned storage slot. Return directly.
							return StorageSearchLambdas.LoopStorageAction.SaveAndExit;
						}
					} else if (productId == -1) {
						//Search for either an empty unassigned labeled storage, or an unlabeled
						//	storage, prioritizing whichever the user choose in the settings.
						if (IsStorageTypePrioritized(__instance, storageIndex)) {
							return boxIDProduct >= 0 ? StorageSearchLambdas.LoopStorageAction.SaveHighPrio : StorageSearchLambdas.LoopStorageAction.SaveAndExit;
						} else {
							return StorageSearchLambdas.LoopStorageAction.SaveLowPrio;
						}
					}

					return StorageSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

		private static bool IsStorageTypePrioritized(NPC_Manager __instance, int storageIndex) {
			if (ModConfig.Instance.FreeStoragePriority.Value == FreeStoragePriorityEnum.Default_Any) {
				return true;
			} else {
				bool isLabeledStorage = IsLabeledStorage(__instance, storageIndex);

				return ModConfig.Instance.FreeStoragePriority.Value == FreeStoragePriorityEnum.Labeled && isLabeledStorage ||
					ModConfig.Instance.FreeStoragePriority.Value == FreeStoragePriorityEnum.Unlabeled && !isLabeledStorage;
			}
		}

		private static bool IsLabeledStorage(NPC_Manager __instance, int storageIndex) =>
			__instance.storageOBJ.transform.GetChild(storageIndex).Find("CanvasSigns") != null;

	}
}
