using System.Collections.Generic;
using System.ComponentModel;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using SuperQoLity.SuperMarket.Patches;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch {

	public class ContainerSearch {

		public enum EnumFreeStoragePriority {
			[Description("1.Assigned storage > 2.Labeled > 3.Unlabeled")]
			Labeled,
			[Description("1.Assigned storage > 2.Unlabeled > 3.Labeled")]
			Unlabeled,
			[Description("1.Assigned storage > 2.Any other storage")]
			Any
		}

		public static bool CheckIfShelfWithSameProduct(NPC_Manager __instance, int productIDToCheck, NPC_Info npcInfoComponent, out ProductShelfSlotInfo productShelfSlotInfo) {
			productShelfSlotInfo = null;
			List<ProductShelfSlotInfo> productsPriority = new();

			foreach (var currentLoopProdThreshold in NPC_Manager.Instance.productsThreshholdArray) {

				ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, true,
					(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {

						if (productId == productIDToCheck) {
							Data_Container shelfDataContainer = __instance.shelvesOBJ.transform.GetChild(prodShelfIndex).GetComponent<Data_Container>();
							int maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(__instance, shelfDataContainer, productIDToCheck, prodShelfIndex);
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * currentLoopProdThreshold);
							if (quantity == 0 || quantity < shelfQuantityThreshold) {
								productsPriority.Add(new ProductShelfSlotInfo(prodShelfIndex, slotIndex, productId, 
									quantity, prodShelfObjT.position));
							}
						}

						return ContainerSearchLambdas.LoopAction.Nothing;
					}
				);

				if (productsPriority.Count > 0) {
					productShelfSlotInfo = productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
					return true;
				}
			}

			return false;
		}

		public static StorageSlotInfo GetStorageContainerWithBoxToMerge(NPC_Manager __instance, int boxIDProduct) {
			return ContainerSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageId, slotId, productId, quantity, storageObjT) => {

					if (productId == boxIDProduct && quantity > 0 &&
							quantity < ProductListing.Instance.productPrefabs[productId].GetComponent<Data_Product>().maxItemsPerBox) {
						return ContainerSearchLambdas.LoopStorageAction.SaveAndExit;
					}

					return ContainerSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

		public static StorageSlotInfo FreeUnassignedStorageContainer(NPC_Manager __instance, Transform employeeT) {
			return GetFreeStorageContainer(__instance, employeeT , - 1);
		}

		public static bool MoreThanOneBoxToMergeCheck(NPC_Manager __instance, int boxIDProduct) {
			int boxCount = 0;

			ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

					if (productId == boxIDProduct && quantity < ProductListing.Instance.productPrefabs[productId].GetComponent<Data_Product>().maxItemsPerBox) {
						boxCount++;
						if (boxCount > 1) {
							return ContainerSearchLambdas.LoopAction.Exit;
						}
					}
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);
			return boxCount > 1;
		}

		public static StorageSlotInfo GetFreeStorageContainer(NPC_Manager __instance, Transform employeeT, int boxIDProduct) {
			StorageSlotInfo foundStorage = StorageSlotInfo.Default;
			List<StorageSlotInfo> assignedPriorityStorage = null;
			List<StorageSlotInfo> highPriorityStorage = new();
			List<StorageSlotInfo> lowPriorityStorage = new();

			ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

					//Ìf boxIDProduct parameter is zero or positive, this method needs to find free storage with the
					//	same assigned item id is the max priority, but if it is unassigned or unlabeled, the priority
					//	setting is used to value one over the other.
					//Ìf boxIDProduct is negative, the assigned storage slots are skipped, and only the priority setting
					//	matters on the unassigned vs unlabeled choice.

					//TODO 2 - I can improve performance by avoiding adding less prioritized
					//	storage when we already found something more prioritized
					//	It could also be faster to save the closest distance added to each type 
					//	of priority, and skip adding any further away storages, but then I would 
					//	be doing distance calculations on potentially less prioritized storages 
					//	for nothing, if a more prioritized storage exists at the end of the loop.
					//	But at the very least I need to do it for assignedPriorityStorage, and for
					//	highPriorityStorage if productId == -1. Zero loss doing it like that.

					if (boxIDProduct >= 0 && productId == boxIDProduct && quantity < 0) {
						//Empty assigned storage slot of the same product type.
						if (assignedPriorityStorage == null) {
							assignedPriorityStorage = new();
						}
						assignedPriorityStorage.Add(new StorageSlotInfo(storageIndex, slotIndex, productId, quantity, storageObjT.position));
					} else if (productId == -1) {
						//Search for either an empty unassigned labeled storage, or an unlabeled
						//	storage, prioritizing whichever the user choose in the settings.
						StorageSlotInfo storage = new StorageSlotInfo(storageIndex, slotIndex, productId, quantity, storageObjT.position);
						if (IsStorageTypePrioritized(__instance, storageObjT)) {
							highPriorityStorage.Add(storage);
						} else {
							lowPriorityStorage.Add(storage);
						}
					}

					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);

			if (assignedPriorityStorage?.Count > 0) {
				foundStorage = GetClosestStorage(assignedPriorityStorage, employeeT);
			} else if (highPriorityStorage?.Count > 0) {
				foundStorage = GetClosestStorage(highPriorityStorage, employeeT);
			} else if (lowPriorityStorage?.Count > 0) {
				foundStorage = GetClosestStorage(lowPriorityStorage, employeeT);
			}

			return foundStorage;
		}

		private static StorageSlotInfo GetClosestStorage(List<StorageSlotInfo> listStorage, Transform employee) {
			if (!(listStorage?.Count > 0)) {
				return null;
			}

			StorageSlotInfo closestStorage = null;
			float closestDistanceSqr = float.MaxValue;

			foreach (var storageInfo in listStorage) {
				float sqrDistance = (storageInfo.ExtraData.Position - employee.position).sqrMagnitude;
				if (sqrDistance < closestDistanceSqr) {
					closestDistanceSqr = sqrDistance;
					closestStorage = storageInfo;
				}
			}

			return closestStorage;
		}

		private static bool IsStorageTypePrioritized(NPC_Manager __instance, Transform storageObjT) {
			if (ModConfig.Instance.FreeStoragePriority.Value == EnumFreeStoragePriority.Any) {
				return true;
			} else {
				bool isLabeledStorage = IsLabeledStorage(__instance, storageObjT);

				return ModConfig.Instance.FreeStoragePriority.Value == EnumFreeStoragePriority.Labeled && isLabeledStorage ||
					ModConfig.Instance.FreeStoragePriority.Value == EnumFreeStoragePriority.Unlabeled && !isLabeledStorage;
			}
		}

		private static bool IsLabeledStorage(NPC_Manager __instance, Transform storageObjT) =>
			storageObjT.Find("CanvasSigns") != null;

	}

}
