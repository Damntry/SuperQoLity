using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using SuperQoLity.SuperMarket.Patches;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch {

	public class ContainerSearch {

		private static Lazy<float[]> productsThresholdArray = new Lazy<float[]>(GetProductsThresholdArray);

		public enum EnumFreeStoragePriority {
			[Description("1.Assigned storage > 2.Labeled > 3.Unlabeled")]
			Labeled,
			[Description("1.Assigned storage > 2.Unlabeled > 3.Labeled")]
			Unlabeled,
			[Description("1.Assigned storage > 2.Any other storage")]
			Any
		}

		//Method from the original game. I added the tarket marking, and modified some stuff to reduce the madness.
		/* Index cheat sheet for if I need to compare against base game code ("06" and onwards, I added them myself):
		
		CODE HAS CHANGED SINCE. Our loop indexes dont match anymore so I didnt add them to the cheat sheet.
		I dont want them to match since after my optimizations I can afford to loop productsThresholdArray.

		00: shelfIndex
		01: shelfSlotIndex, 
		02: shelfProdInfoIndex
		03: shelfProductId
		04: shelfQuantity
		05: shelfQuantityThreshold
		06: storageIndex, 
		07: storageSlotIndex
		08: storageProdInfoIndex
		09: storageProductId
		10: storageQuantity

		*/

		public static ProductAvailableInfo CheckProductAvailability(NPC_Manager __instance) {
			//Performance.Start("1. Basic var initialization");

			if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0) {
				return ProductAvailableInfo.Default;
			}

			List<ProductAvailableInfo> productsPriority = new();
			List<ProductAvailableInfo> productsPrioritySecondary = new();
			//Performance.StopAndRecord("1. Basic var initialization");
			//Performance.Start("2. productsThresholdArray");
			//Performance.StopAndRecord("2. productsThresholdArray");

			foreach (float productThreshold in productsThresholdArray.Value) {
				//Performance.Start("4. Shelves and storage loop");
				for (int shelfIndex = 0; shelfIndex < __instance.shelvesOBJ.transform.childCount; shelfIndex++) {
					Transform shelfObjT = __instance.shelvesOBJ.transform.GetChild(shelfIndex);
					Data_Container shelfDataContainer = shelfObjT.GetComponent<Data_Container>();
					int num = shelfDataContainer.productInfoArray.Length / 2;
					//Performance.Start("5. Shelves slot and storage loop");
					for (int shelfSlotIndex = 0; shelfSlotIndex < num; shelfSlotIndex++) {
						//Check if this storage slot is already in use by another employee
						//Performance.Start("IsProductShelfSlotTargeted");
						if (EmployeeTargetReservation.IsProductShelfSlotTargeted(shelfIndex, shelfSlotIndex)) {
							continue;
						}
						//Performance.StopAndRecord("IsProductShelfSlotTargeted");
						int shelfProductId = shelfDataContainer.productInfoArray[shelfSlotIndex * 2];
						if (shelfProductId >= 0) {

							int shelfQuantity = shelfDataContainer.productInfoArray[shelfSlotIndex * 2 + 1];

							bool shouldBeRestocked = shelfQuantity == 0;
							if (!shouldBeRestocked) {
								//Check if shelf product quantity is below current threshold.
								//Performance.Start("GetMaxProductsPerRow");
								int maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
									__instance, shelfDataContainer, shelfProductId, shelfIndex);
								//Performance.StopAndRecord("GetMaxProductsPerRow");

								int shelfQuantityThreshold = (int)(maxProductsPerRow * productThreshold);
								shouldBeRestocked = shelfQuantity < shelfQuantityThreshold;
							}
							
							productsPrioritySecondary.Clear();

							if (shouldBeRestocked) {
								//Performance.Start("7. Storage loop");
								for (int storageIndex = 0; storageIndex < __instance.storageOBJ.transform.childCount; storageIndex++) {
									Transform storageObjT = __instance.storageOBJ.transform.GetChild(storageIndex);
									int[] storageProductInfoArray = storageObjT.GetComponent<Data_Container>().productInfoArray;
									int num5 = storageProductInfoArray.Length / 2;
									//Performance.Start("8. Storage slot loop");
									for (int storageSlotIndex = 0; storageSlotIndex < num5; storageSlotIndex++) {
										//Check if this storage slot is already in use by another employee
										//Performance.Start("IsStorageSlotTargeted");
										if (EmployeeTargetReservation.IsStorageSlotTargeted(storageIndex, storageSlotIndex)) {
											continue;
										}
										//Performance.StopAndRecord("IsStorageSlotTargeted");
										int storageProductId = storageProductInfoArray[storageSlotIndex * 2];
										int storageQuantity = storageProductInfoArray[storageSlotIndex * 2 + 1];
										if (storageProductId >= 0 && storageProductId == shelfProductId && storageQuantity > 0) {
											//productsPrioritySecondary.Add(new int[] { shelfIndex, shelfSlotIndex * 2, storageIndex, storageSlotIndex * 2, shelfProductId, storageProductId, shelfSlotIndex, storageSlotIndex, shelfQuantity, storageQuantity });
											productsPrioritySecondary.Add(new ProductAvailableInfo(shelfIndex, shelfSlotIndex,
												shelfSlotIndex * 2, shelfProductId, shelfQuantity, shelfObjT.position, storageIndex, 
												storageSlotIndex, storageSlotIndex * 2, storageProductId, storageQuantity,
												storageObjT.position, shelfDataContainer));
										}
									}
									//Performance.StopAndRecord("8. Storage slot loop");
								}
								//Performance.StopAndRecord("7. Storage loop");
							}
							if (productsPrioritySecondary.Count > 0) {
								productsPriority.Add(productsPrioritySecondary[UnityEngine.Random.Range(0, productsPrioritySecondary.Count)]);
							}
						}
					}
					//Performance.StopAndRecord("5. Shelves slot and storage loop");
					if (productsPriority.Count > 0) {
						break;
					}
				}
				//Performance.StopAndRecord("4. Shelves and storage loop");
			}

			if (productsPriority.Count > 0) {
				return productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
			}

			return ProductAvailableInfo.Default;
		}

		private static float[] GetProductsThresholdArray() {
			float[] productsThresholds;
			FieldInfo field = AccessTools.Field(typeof(NPC_Manager), "productsThreshholdArray");
			if (field == null) {
				//In case they fix the typo in the future.
				field = AccessTools.Field(typeof(NPC_Manager), "productsThresholdArray");
			}

			if (field != null) {
				productsThresholds = (float[])field.GetValue(NPC_Manager.Instance);
			} else {
				//Use last known defaults
				productsThresholds = [0.25f, 0.5f, 0.75f, 1f];
			}

			return productsThresholds;
		}
		
		public static bool CheckIfShelfWithSameProduct(NPC_Manager __instance, int productIDToCheck, NPC_Info npcInfoComponent, out ProductShelfSlotInfo productShelfSlotInfo) {
			productShelfSlotInfo = null;
			List<ProductShelfSlotInfo> productsPriority = new();

			foreach (var currentLoopProdThreshold in productsThresholdArray.Value) {

				ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, true,
					(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {

						if (productId == productIDToCheck) {
							//int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, EmployeeJobAIPatch.GetMaxProductsPerRowMethod.Value, new object[] { prodShelfIndex, productIDToCheck });
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
					npcInfoComponent.SetRestockProductAvailablePartial(productShelfSlotInfo);
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

	public class ProductAvailableInfo {
		public int ShelfIndex { get; private set; }
		public int ShelfSlotIndex { get; private set; }
		public int ShelfProdInfoIndex { get; private set; }
		public int ShelfProductId { get; private set; }
		public int ShelfQuantity { get; private set; }
		public Vector3 ShelfPosition { get; private set; }
		public int StorageIndex { get; private set; }
		public int StorageSlotIndex { get; private set; }
		public int StorageProdInfoIndex { get; private set; }
		public int StorageProductId { get; private set; }
		public int StorageQuantity { get; private set; }
		public Vector3 StoragePosition { get; private set; }
		public Data_Container ShelfDataContainer { get; private set; }


		public static ProductAvailableInfo Default { get; } = new ProductAvailableInfo();

		private ProductAvailableInfo() {
			SetValues(-1, -1, -1, -1, -1, Vector3.zero, - 1, -1, -1, -1, -1, Vector3.zero, null);
		}

		public ProductAvailableInfo(int ShelfIndex, int ShelfSlotIndex, int ShelfProdInfoIndex, int ShelfProductId,
				int ShelfQuantity, Vector3 ShelfPosition, int StorageIndex, int StorageSlotIndex, int StorageProdInfoIndex,
				int StorageProductId, int StorageQuantity, Vector3 StoragePosition, Data_Container ShelfDataContainer) {

			SetValues(ShelfIndex, ShelfSlotIndex, ShelfProdInfoIndex, ShelfProductId,
				ShelfQuantity, ShelfPosition, StorageIndex, StorageSlotIndex,
				StorageProdInfoIndex, StorageProductId, StorageQuantity, StoragePosition, ShelfDataContainer);
		}

		public void SetValues(int ShelfIndex, int ShelfSlotIndex, int ShelfProdInfoIndex, int ShelfProductId,
				int ShelfQuantity, Vector3 ShelfPosition, int StorageIndex, int StorageSlotIndex, int StorageProdInfoIndex,
				int StorageProductId, int StorageQuantity, Vector3 StoragePosition, Data_Container ShelfDataContainer) {
			this.ShelfIndex = ShelfIndex;
			this.ShelfSlotIndex = ShelfSlotIndex;
			this.ShelfProdInfoIndex = ShelfProdInfoIndex;
			this.ShelfProductId = ShelfProductId;
			this.ShelfQuantity = ShelfQuantity;
			this.ShelfPosition = ShelfPosition;
			this.StorageIndex = StorageIndex;
			this.StorageSlotIndex = StorageSlotIndex;
			this.StorageProdInfoIndex = StorageProdInfoIndex;
			this.StorageProductId = StorageProductId;
			this.StorageQuantity = StorageQuantity;
			this.StoragePosition = StoragePosition;
			this.ShelfDataContainer = ShelfDataContainer;
		}

		public void SetValuesPartial(int? shelfIndex = null, int? shelfSlotIndex = null, int? shelfProdInfoIndex = null, 
				int? shelfProductId = null, int? shelfQuantity = null, Vector3? shelfPosition = null, int ? storageIndex = null, 
				int? storageSlotIndex = null, int? storageProdInfoIndex = null, int? storageProductId = null, 
				int? storageQuantity = null, Vector3? storagePosition = null, Data_Container shelfDataContainer = null) {

			this.ShelfIndex = shelfIndex != null ? (int)shelfIndex : this.ShelfIndex;
			this.ShelfSlotIndex = shelfSlotIndex != null ? (int)shelfSlotIndex : this.ShelfSlotIndex;
			this.ShelfProdInfoIndex = shelfProdInfoIndex != null ? (int)shelfProdInfoIndex : this.ShelfProdInfoIndex;
			this.ShelfProductId = shelfProductId != null ? (int)shelfProductId : this.ShelfProductId;
			this.ShelfQuantity = shelfQuantity != null ? (int)shelfQuantity : this.ShelfQuantity;
			this.ShelfPosition = shelfPosition != null ? (Vector3)shelfPosition : this.ShelfPosition;
			this.StorageIndex = storageIndex != null ? (int)storageIndex : this.StorageIndex;
			this.StorageSlotIndex = storageSlotIndex != null ? (int)storageSlotIndex : this.StorageSlotIndex;
			this.StorageProdInfoIndex = storageProdInfoIndex != null ? (int)storageProdInfoIndex : this.StorageProdInfoIndex;
			this.StorageProductId = storageProductId != null ? (int)storageProductId : this.StorageProductId;
			this.StorageQuantity = storageQuantity != null ? (int)storageQuantity : this.StorageQuantity;
			this.StoragePosition = storagePosition != null ? (Vector3)storagePosition : this.StoragePosition;
			this.ShelfDataContainer = shelfDataContainer != null ? shelfDataContainer : this.ShelfDataContainer;

		}

	}

}
