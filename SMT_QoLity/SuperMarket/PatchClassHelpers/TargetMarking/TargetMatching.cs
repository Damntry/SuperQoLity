using System;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.ShelfSlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking {

	public static class TargetMatching {

		public static bool RefreshAndCheckValidTargetedStorage(this NPC_Info NPC, NPC_Manager __instance,
				bool clearReservation, out StorageSlotInfo storageSlotInfo) {

			bool isValid = RefreshAndCheckTargetedShelf(NPC, __instance, clearReservation, -1, TargetType.StorageSlot,
				out GenericShelfSlotInfo slotInfoBase);
			storageSlotInfo = (StorageSlotInfo)slotInfoBase;
			return isValid;
		}

		
		public static bool RefreshAndCheckValidTargetedProductShelf(this NPC_Info NPC, NPC_Manager __instance,
				RestockJobInfo jobInfo) {

			return RefreshAndCheckValidTargetedProductShelf(NPC, __instance, false, jobInfo);
		}
		
		private static bool RefreshAndCheckValidTargetedProductShelf(this NPC_Info NPC, NPC_Manager __instance,
				bool clearReservation, RestockJobInfo jobInfo) {

			bool isValid = RefreshAndCheckTargetedShelf(NPC, __instance, clearReservation,
				jobInfo.MaxProductsPerRow, TargetType.ProdShelfSlot, out GenericShelfSlotInfo slotInfoBase);

			//Refresh from the new product shelf data.
			jobInfo.SetProductShelfExtraData((ProductShelfSlotInfo)slotInfoBase, jobInfo.MaxProductsPerRow);

			return isValid;
		}

		private static bool RefreshAndCheckTargetedShelf(NPC_Info NPC, NPC_Manager __instance,
				bool clearReservation, int maxProductsPerRow, TargetType targetType, out GenericShelfSlotInfo slotInfoBase) {

			bool hasTarget;
			bool contentsValid;

			if (targetType == TargetType.StorageSlot) {
				hasTarget = NPC.HasTargetedStorage(out StorageSlotInfo storageSlotInfo);
				contentsValid = RefreshAndCheckStorageContents(__instance.storageOBJ, storageSlotInfo);
				slotInfoBase = storageSlotInfo;
			} else if (targetType == TargetType.ProdShelfSlot) {
				hasTarget = NPC.HasTargetedProductShelf(out ProductShelfSlotInfo productShelfSlotInfo);
				contentsValid = RefreshAndCheckProdShelfContents(__instance.shelvesOBJ, productShelfSlotInfo, maxProductsPerRow);
				slotInfoBase = productShelfSlotInfo;
			} else {
				throw new InvalidOperationException("$Invalid target \"{targetType}\" for this method.");
			}

			if (clearReservation && hasTarget) {
				EmployeeTargetReservation.DeleteNPCTarget(NPC, targetType);
			}

			return hasTarget && contentsValid;
		}

		public static bool RefreshAndCheckTargetProductShelf(NPC_Manager __instance, RestockJobInfo jobInfo, int maxProductsPerRow) {
			ProductShelfSlotInfo prodShelfInfo = jobInfo.ProdShelf;
			bool targetAlreadyReserved = EmployeeTargetReservation.IsProductShelfSlotTargeted(prodShelfInfo);
			bool contentsMatchOrValid = RefreshAndCheckProdShelfContents(__instance.shelvesOBJ, prodShelfInfo, maxProductsPerRow);

			return !targetAlreadyReserved && contentsMatchOrValid;
		}

		public static bool RefreshAndCheckTargetStorage(NPC_Manager __instance, RestockJobInfo jobInfo) {
			StorageSlotInfo storageInfo = jobInfo.Storage;
			bool targetAlreadyReserved = EmployeeTargetReservation.IsStorageSlotTargeted(storageInfo);
			bool contentsMatchOrValid = RefreshAndCheckStorageContents(__instance.storageOBJ, storageInfo);

			return !targetAlreadyReserved && contentsMatchOrValid;
		}

		private static bool RefreshAndCheckProdShelfContents(GameObject gameObjectShelf, ProductShelfSlotInfo slotInfo, int maxProductsPerRow) {
			ProductShelfSlotMatch slotInfo2 = new(slotInfo, maxProductsPerRow);
			bool match = ContentsMatchOrValid(gameObjectShelf, slotInfo2, out int currentTargetQuantity, TargetType.ProdShelfSlot);
			//Need to take the updated value so its propagated out of the method to wherever its being called
			slotInfo.ExtraData.Quantity = currentTargetQuantity;
			return match;
		}

		private static bool RefreshAndCheckStorageContents(GameObject gameObjectStorage, StorageSlotInfo slotInfo) {
			bool match = ContentsMatchOrValid(gameObjectStorage, slotInfo, out int currentTargetQuantity, TargetType.StorageSlot);
			slotInfo.ExtraData.Quantity = currentTargetQuantity;
			return match;
		}

		private static bool ContentsMatchOrValid(GameObject gameObjectShelf, GenericShelfSlotInfo slotInfoBase, out int currentTargetQuantity, TargetType targetType) {
			//Check that the saved target values still match the current content of the product shelf/storage slot.
			int[] productInfoArray = gameObjectShelf.transform.GetChild(slotInfoBase.ShelfIndex)
				.GetComponent<Data_Container>().productInfoArray;

			int productId = productInfoArray[slotInfoBase.SlotIndex * 2];
			currentTargetQuantity = productInfoArray[slotInfoBase.SlotIndex * 2 + 1];

			if (productId == slotInfoBase.ExtraData.ProductId) {
				if (targetType == TargetType.StorageSlot) {
					return currentTargetQuantity >= slotInfoBase.ExtraData.Quantity;
				} else if (targetType == TargetType.ProdShelfSlot) {
					if (slotInfoBase is not ProductShelfSlotMatch) {
						throw new InvalidOperationException($"The target slot shelf parameter " +
							$"({nameof(slotInfoBase)}) must be an object of type {nameof(ProductShelfSlotMatch)}");
					}

					return currentTargetQuantity < ((ProductShelfSlotMatch)slotInfoBase).MaxProductsPerRow;
				}
			}

			//If we reach this point, the most probable cause is that a human
			//	player took from the shelf slot while the NPC was on route.

			return false;
		}
		

		private class ProductShelfSlotMatch : GenericShelfSlotInfo {

			public ProductShelfSlotMatch(ProductShelfSlotInfo shelfSlotInfo, int maxProductsPerRow)
				: base(shelfSlotInfo.ShelfIndex, shelfSlotInfo.SlotIndex, shelfSlotInfo.ExtraData.ProductId,
						shelfSlotInfo.ExtraData.Quantity, shelfSlotInfo.ExtraData.Position, ShelfType.ProdShelfSlot) {

				MaxProductsPerRow = maxProductsPerRow;
			}


			public int MaxProductsPerRow { get; private set; }

		}

	}
}
