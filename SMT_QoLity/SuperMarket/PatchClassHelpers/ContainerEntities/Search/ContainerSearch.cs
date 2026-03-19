using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.Search {

    public enum EnumFreeStoragePriority {
		[Description("1.Assigned labeled > 2.Labeled > 3.Unlabeled")]
		Labeled,
        [Description("1.Assigned labeled > 2.Unlabeled > 3.Labeled")]
		Unlabeled,
        [Description("1.Assigned labeled > 2.Labeled")]
        OnlyLabeled,
        [Description("1.Unlabeled")]
        OnlyUnlabeled,
    }

	public class ContainerSearch {

		/// <summary>
		/// Returns one of the most empty product shelf slots that are assigned to the product passed by parameter.
		/// </summary>
		public static bool CheckIfProdShelfWithSameProduct(NPC_Manager __instance, 
				int productIDToCheck, NPC_Info npcInfoComponent, 
				out (ProductShelfSlotInfo productShelfSlotInfo, int maxProductsPerRow) result) {
			result.productShelfSlotInfo = null;
			result.maxProductsPerRow = -1;

			List<(ProductShelfSlotInfo shelfSlotInfo, int maxProductsPerRow)> productsPriority = new();

			foreach (var currentLoopProdThreshold in __instance.productsThreshholdArray) {

				ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, checkNPCProdShelfTarget: true,
					(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {

						if (productId == productIDToCheck) {
							Data_Container shelfDataContainer = __instance.shelvesOBJ.transform.GetChild(prodShelfIndex).GetComponent<Data_Container>();
							int maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(__instance, shelfDataContainer, productIDToCheck, prodShelfIndex);
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * currentLoopProdThreshold);
							if (quantity == 0 || quantity < shelfQuantityThreshold) {
								productsPriority.Add(
									(new ProductShelfSlotInfo(
										prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT.position),
									maxProductsPerRow)
								);
							}
						}

						return ContainerSearchLambdas.LoopAction.Nothing;
					}
				);

				if (productsPriority.Count > 0) {
                    //Return emptiest shelf
                    result = productsPriority.OrderBy(p => p.shelfSlotInfo.ExtraData.Quantity).First();
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns the first storage shelf slot whose box has some empty space in it, and has the same product 
		/// assigned as the one passed by parameter.
		/// </summary>
		public static StorageSlotInfo GetStorageContainerWithBoxToMerge(NPC_Manager __instance, int boxIDProduct) {
			int maxItemsPerBox = ProductListing.Instance.productsData[boxIDProduct].maxItemsPerBox;

            return ContainerSearchLambdas.FindStorageSlotLambda(__instance, checkNPCStorageTarget: true,
				(storageId, slotId, productId, quantity, storageObjT) => {
					
					if (productId == boxIDProduct && quantity > 0 && quantity < maxItemsPerBox &&
							IsStorageAllowedForMerge(__instance, storageObjT)) {

						return ContainerSearchLambdas.LoopStorageAction.SaveAndExit;
					}

					return ContainerSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

		/// <summary>
		/// Gets an empty storage shelf slot that has not product assigned, prioritizing 
		/// first by the FreeStoragePriority setting, and then by the closest shelf.
		/// </summary>
		public static StorageSlotInfo FreeUnassignedStorageContainer(NPC_Manager __instance, Transform employeeT) {
			return GetFreeStorageContainer(__instance, employeeT , -1);
		}

		public static bool MoreThanOneBoxToMergeCheck(NPC_Manager __instance, int boxIDProduct) {
            int maxItemsPerBox = ProductListing.Instance.productsData[boxIDProduct].maxItemsPerBox;
            int boxCount = 0;
            
            ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, checkNPCStorageTarget: true, skipEmptyBoxes: false,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

				if (productId == boxIDProduct && quantity < maxItemsPerBox && IsStorageAllowedForMerge(__instance, storageObjT)) {
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

		/// <summary>
		/// Gets an empty storage shelf slot assigned to the product passed by parameter (unassigned if
		/// negative), prioritizing first by the FreeStoragePriority setting, and then by closest shelf.
		/// </summary>
		public static StorageSlotInfo GetFreeStorageContainer(NPC_Manager __instance, Transform employeeT, int boxIDProduct) {
			StorageSlotInfo foundStorage = StorageSlotInfo.Default;
			List<StorageSlotInfo> assignedPriorityStorage = new();
			List<StorageSlotInfo> highPriorityStorage = new();
			List<StorageSlotInfo> lowPriorityStorage = new();

			bool allowsAssignedStorage = IsAssignedStorageAllowed();
			bool allowsLowPriorityStorage = IsAllStorageTypesAllowed();


            ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, checkNPCStorageTarget: true, skipEmptyBoxes: false,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

					//Ìf boxIDProduct parameter is zero or positive, this method needs to find free storage with the
					//	same assigned item id is the max priority, but if it is unassigned or unlabeled, the priority
					//	setting is used to value one over the other.
					//Ìf boxIDProduct is negative, the assigned storage slots are skipped, and only the priority setting
					//	matters on the unassigned vs unlabeled choice.

					if (allowsAssignedStorage && boxIDProduct >= 0 && productId == boxIDProduct && quantity < 0) {
						//Empty assigned storage slot of the same product type.
						assignedPriorityStorage.Add(new (storageIndex, slotIndex, productId, quantity, storageObjT.position));
					} else if (productId == -1 && assignedPriorityStorage.Count == 0) {
						//Search for either an empty unassigned labeled storage, or an unlabeled
						//	storage, prioritizing whichever the user choose in the settings.
						StorageSlotInfo storage = new (storageIndex, slotIndex, productId, quantity, storageObjT.position);
						if (IsStorageTypePrioritized(__instance, storageObjT)) {
							highPriorityStorage.Add(storage);
						} else if (allowsLowPriorityStorage && highPriorityStorage.Count == 0) {
							lowPriorityStorage.Add(storage);
						}
					}

					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);

			if (assignedPriorityStorage?.Count > 0) {
				foundStorage = GetClosestStorageFromList(assignedPriorityStorage, employeeT);
			} else if (highPriorityStorage?.Count > 0) {
				foundStorage = GetClosestStorageFromList(highPriorityStorage, employeeT);
			} else if (lowPriorityStorage?.Count > 0) {
				foundStorage = GetClosestStorageFromList(lowPriorityStorage, employeeT);
			}

			return foundStorage;
		}

		/// <summary>
		/// Search the storage slots, and then the shelf slots, to find the first 
		/// slot that has some of the product passed by parameter available for pick-up.
		/// </summary>
		public static GenericShelfSlotInfo GetFirstOfAnyShelfWithProduct(NPC_Manager __instance, int boxIDProduct) {
			GenericShelfSlotInfo shelfSlotInfo = ContainerSearchLambdas.FindStorageSlotLambda(
				__instance, checkNPCStorageTarget: true, (storageId, slotId, productId, quantity, storageObjT) => {

					if (productId == boxIDProduct && quantity > 0) {
						return ContainerSearchLambdas.LoopStorageAction.SaveAndExit;
					}

					return ContainerSearchLambdas.LoopStorageAction.Nothing;
				}
			);
			if (shelfSlotInfo.ShelfFound) {
				return shelfSlotInfo;
			}

			ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, checkNPCProdShelfTarget: true,
				(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {

					if (productId == boxIDProduct && quantity > 0) {
						shelfSlotInfo = new ProductShelfSlotInfo(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT.position);

						return ContainerSearchLambdas.LoopAction.Exit;
					}

					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);

			return shelfSlotInfo;
		}

		private static StorageSlotInfo GetClosestStorageFromList(List<StorageSlotInfo> listStorage, Transform employee) {
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
			EnumFreeStoragePriority storagePriority = ModConfig.Instance.FreeStoragePriority.Value;

            bool isLabeledStorage = IsLabeledStorage(__instance, storageObjT);

			return isLabeledStorage && (storagePriority == EnumFreeStoragePriority.Labeled ||
                    storagePriority == EnumFreeStoragePriority.OnlyLabeled) || 
				!isLabeledStorage && (storagePriority == EnumFreeStoragePriority.Unlabeled ||
                    storagePriority == EnumFreeStoragePriority.OnlyUnlabeled);
		}

		/// <summary>
		/// While merging we only check if the type of storage is allowed, but ignore the priority setting.
		/// </summary>
		private static bool IsStorageAllowedForMerge(NPC_Manager __instance, Transform storageObjT) {
            EnumFreeStoragePriority storagePriority = ModConfig.Instance.FreeStoragePriority.Value;

            if (IsAllStorageTypesAllowed()) {
				return true;
			} else {
                bool isLabeledStorage = IsLabeledStorage(__instance, storageObjT);
				return isLabeledStorage && storagePriority == EnumFreeStoragePriority.OnlyLabeled ||
					!isLabeledStorage && storagePriority == EnumFreeStoragePriority.OnlyUnlabeled;
            }
        }
		
        public static bool IsAssignedStorageAllowed() =>
			ModConfig.Instance.FreeStoragePriority.Value != EnumFreeStoragePriority.OnlyUnlabeled;

		private static bool IsAllStorageTypesAllowed() =>
            ModConfig.Instance.FreeStoragePriority.Value != EnumFreeStoragePriority.OnlyLabeled &&
				ModConfig.Instance.FreeStoragePriority.Value != EnumFreeStoragePriority.OnlyUnlabeled;

        private static bool IsLabeledStorage(NPC_Manager __instance, Transform storageObjT) => 
			storageObjT.Find("CanvasSigns");

	}

}
