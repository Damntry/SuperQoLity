using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.StorageSearch {

	public class StorageSearchHelpers {

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
			return StorageSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {
					if (productId == -1) {
						//Free storage slot, either unassigned or unlabeled.
						return StorageSearchLambdas.LoopStorageAction.SaveAndExit;
					}

					return StorageSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

		public static StorageSlotInfo GetFreeStorageContainer(NPC_Manager __instance, int boxIDProduct) {
			return StorageSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					if (boxIDProduct >= 0 && productId == boxIDProduct && quantity < 0) {
						//Free assigned storage slot. Return it.
						return StorageSearchLambdas.LoopStorageAction.SaveAndExit;
					} else if (productId == -1) {
						//Save for later in case there is no assigned storage for this product.
						return StorageSearchLambdas.LoopStorageAction.Save;
					}

					return StorageSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

	}
}
