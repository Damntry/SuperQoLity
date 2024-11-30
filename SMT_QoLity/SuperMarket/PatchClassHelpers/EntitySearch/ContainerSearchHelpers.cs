using System.Collections.Generic;
using System.ComponentModel;
using Damntry.Utils.Reflection;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch {

	public class ContainerSearchHelpers {

		public enum FreeStoragePriorityEnum {
			[Description("1.Assigned storage > 2.Any other storage")]
			Default_Any,
			[Description("1.Assigned storage > 2.Labeled > 3.Unlabeled")]
			Labeled,
			[Description("1.Assigned storage > 2.Unlabeled > 3.Labeled")]
			Unlabeled
		}

		//Copied from the original game. I added the tarket marking, and modified some stuff to reduce the madness.
		public static int[] CheckProductAvailability(NPC_Manager __instance) {
			int[] array = [-1, -1, -1, -1, -1, -1];

			if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0) {
				return array;
			}

			List<int[]> productsPriority = new();
			List<int[]> productsPrioritySecondary = new();

			float[] productsThresholdArray = (float[])AccessTools.Field(typeof(NPC_Manager), "productsThreshholdArray").GetValue(__instance);

			for (int i = 0; i < productsThresholdArray.Length; i++) {
				productsPriority.Clear();
				for (int j = 0; j < __instance.shelvesOBJ.transform.childCount; j++) {
					int[] productInfoArray = __instance.shelvesOBJ.transform.GetChild(j).GetComponent<Data_Container>().productInfoArray;
					int num = productInfoArray.Length / 2;
					for (int k = 0; k < num; k++) {
						productsPrioritySecondary.Clear();
						//Check if this storage slot is already in use by another employee
						if (EmployeeTargetReservation.IsProductShelfSlotTargeted(j, k)) {
							continue;
						}
						int shelfProductId = productInfoArray[k * 2];
						if (shelfProductId >= 0) {
							int shelfQuantity = productInfoArray[k * 2 + 1];
							int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, EmployeeJobAIPatch.GetMaxProductsPerRowMethod.Value, [j, shelfProductId]);
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * productsThresholdArray[i]);
							if (shelfQuantity == 0 || shelfQuantity < shelfQuantityThreshold) {
								for (int l = 0; l < __instance.storageOBJ.transform.childCount; l++) {
									int[] productInfoArray2 = __instance.storageOBJ.transform.GetChild(l).GetComponent<Data_Container>().productInfoArray;
									int num5 = productInfoArray2.Length / 2;
									for (int m = 0; m < num5; m++) {
										//Check if this storage slot is already in use by another employee
										if (EmployeeTargetReservation.IsStorageSlotTargeted(l, m)) {
											continue;
										}
										int storageProductId = productInfoArray2[m * 2];
										int storageQuantity = productInfoArray2[m * 2 + 1];
										if (storageProductId >= 0 && storageProductId == shelfProductId && storageQuantity > 0) {
											productsPrioritySecondary.Add(new int[] { j, k * 2, l, m * 2, shelfProductId, storageProductId, k, m, shelfQuantity, shelfQuantityThreshold, storageQuantity });
										}
									}
								}
							}
							if (productsPrioritySecondary.Count > 0) {
								productsPriority.Add(productsPrioritySecondary[UnityEngine.Random.Range(0, productsPrioritySecondary.Count)]);
							}
						}
					}
				}
				if (productsPriority.Count > 0) {
					break;
				}
			}
			if (productsPriority.Count > 0) {
				return productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
			}
			return array;
		}

		public static bool CheckIfShelfWithSameProduct(NPC_Manager __instance, int productIDToCheck, NPC_Info npcInfoComponent, out ProductShelfSlotInfo productShelfSlotInfo) {
			productShelfSlotInfo = null;
			List<ProductShelfSlotInfo> productsPriority = new();
			float[] productsThresholdArray = (float[])AccessTools.Field(typeof(NPC_Manager), "productsThreshholdArray").GetValue(__instance);

			for (int i = 0; i < productsThresholdArray.Length; i++) {

				ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, true,
					(prodShelfIndex, slotIndex, productId, quantity) => {

						if (productId == productIDToCheck) {
							int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, EmployeeJobAIPatch.GetMaxProductsPerRowMethod.Value, new object[] { prodShelfIndex, productIDToCheck });
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * productsThresholdArray[i]);
							if (quantity == 0 || quantity < shelfQuantityThreshold) {
								productsPriority.Add(new ProductShelfSlotInfo(prodShelfIndex, slotIndex, productId, quantity));
							}
						}

						return ContainerSearchLambdas.LoopAction.Nothing;
					}
				);

				if (productsPriority.Count > 0) {
					productShelfSlotInfo = productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
					npcInfoComponent.productAvailableArray[0] = productShelfSlotInfo.ShelfIndex;
					npcInfoComponent.productAvailableArray[1] = productShelfSlotInfo.SlotIndex * 2;
					npcInfoComponent.productAvailableArray[4] = productShelfSlotInfo.ExtraData.ProductId;
					npcInfoComponent.productAvailableArray[6] = productShelfSlotInfo.SlotIndex;
					npcInfoComponent.productAvailableArray[6] = productShelfSlotInfo.ExtraData.Quantity;
					return true;
				}
			}

			return false;
		}

		public static StorageSlotInfo GetStorageContainerWithBoxToMerge(NPC_Manager __instance, int boxIDProduct) {
			return ContainerSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageId, slotId, productId, quantity) => {

					if (productId == boxIDProduct && quantity > 0 &&
							quantity < ProductListing.Instance.productPrefabs[productId].GetComponent<Data_Product>().maxItemsPerBox) {
						return ContainerSearchLambdas.LoopStorageAction.SaveAndExit;
					}

					return ContainerSearchLambdas.LoopStorageAction.Nothing;
				}
			);
		}

		public static StorageSlotInfo FreeUnassignedStorageContainer(NPC_Manager __instance) {
			return GetFreeStorageContainer(__instance, -1);
		}

		public static StorageSlotInfo GetFreeStorageContainer(NPC_Manager __instance, int boxIDProduct) {
			return ContainerSearchLambdas.FindStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					//Ìf boxIDProduct is zero or positive, it means finding free storage with the same assigned item id is the max
					//	priority, but if it is unassigned or unlabeled, the priority setting is used to value one over the other.
					//Ìf boxIDProduct is negative, the assigned storage slots are skipped, and only the priority setting matters on
					//	 the unassigned vs unlabeled choice.

					if (boxIDProduct >= 0 && productId >= 0) {
						//Search an storage slot with this assigned boxIDProduct
						if (productId == boxIDProduct && quantity < 0) {
							//Empty assigned storage slot. Return directly.
							return ContainerSearchLambdas.LoopStorageAction.SaveAndExit;
						}
					} else if (productId == -1) {
						//Search for either an empty unassigned labeled storage, or an unlabeled
						//	storage, prioritizing whichever the user choose in the settings.
						if (IsStorageTypePrioritized(__instance, storageIndex)) {
							return boxIDProduct >= 0 ? ContainerSearchLambdas.LoopStorageAction.SaveHighPrio : ContainerSearchLambdas.LoopStorageAction.SaveAndExit;
						} else {
							return ContainerSearchLambdas.LoopStorageAction.SaveLowPrio;
						}
					}

					return ContainerSearchLambdas.LoopStorageAction.Nothing;
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
