﻿using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using HutongGames.PlayMaker.Actions;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.StorageSearch {
	public class StorageSearchLambdas {

		public enum LoopStorageAction {
			/// <summary>Do nothing special in this loop and keep going.</summary>
			Nothing = 0,
			/// <summary>
			/// Save storage slot data with low priority and keep going.
			/// It will be returned if the loop ends with no other value saved."/>
			/// </summary>
			SaveLowPrio = 1,
			/// <summary>
			/// Save storage slot data with high priority and keep going.
			/// It will be returned if the loop ends.
			/// </summary>
			SaveHighPrio = 2,
			/// <summary>Save storage slot data and return it immediately.</summary>
			SaveAndExit = 3
		}
		public enum LoopAction {
			Nothing = 0,    //Keep looping.
			Nothing2 = 1,   //Same as Nothing. Exists for conversion compatibility with LoopStorageAction.
			Nothing3 = 2,   //Same as Nothing. Exists for conversion compatibility with LoopStorageAction.
			Exit = 3        //Stop and return immediately.
		}


		/// <summary>Defines the parameters available in the lambda to find storage slots.</summary>
		/// <param name="storageIndex">Child index of the current storage shelf.</param>
		/// <param name="slotIndex">Child index of current storage slot.</param>
		/// <param name="productId">Product ID of the current storage slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current storage slot. Can be -1 if empty.</param>
		/// <returns>The <see cref="LoopStorageAction"/> to perform in the current loop. </returns>
		public delegate LoopStorageAction StorageSlotFunction(int storageIndex, int slotIndex, int productId, int quantity);

		/// <summary>Defines the parameters available in the lambda to iterate through storage slots.</summary>
		/// <param name="storageIndex">Child index of the current storage shelf.</param>
		/// <param name="slotIndex">Child index of current storage slot.</param>
		/// <param name="productId">Product ID of the current storage slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current storage slot. Can be -1 if empty.</param>
		/// <returns>The <see cref="LoopAction"/> to perform.</returns>
		public delegate LoopAction StorageLoopFunction(int storageIndex, int slotIndex, int productId, int quantity);

		//TODO 6 - Join ForEachStorageSlotLambda and ForEachProductShelfSlotLambda into a common private method.
		//		They are the same thing and there is no sense in duplicating their functionality.
		//		The public methods will still exist as they are now but they ll call the private with the necessary parameters.

		/// <summary>
		/// Loops through all storages and its box slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCStorageTarget">True to skip the storage slots that are currently being targeted by an employee NPC.</param>
		/// <param name="storageSlotLambda">The StorageLoopFunction search lambda. See <see cref="StorageLoopFunction"/> for more information.</param>
		/// <returns></returns>
		public static void ForEachStorageSlotLambda(NPC_Manager __instance, bool checkNPCStorageTarget, StorageLoopFunction storageSlotLambda) {
			for (int i = 0; i < __instance.storageOBJ.transform.childCount; i++) {
				int[] productInfoArray = __instance.storageOBJ.transform.GetChild(i).GetComponent<Data_Container>().productInfoArray;
				int num = productInfoArray.Length / 2;
				for (int j = 0; j < num; j++) {
					//Check if this storage slot is already in use by another NPC
					if (checkNPCStorageTarget && EmployeeTargetReservation.IsStorageSlotTargeted(i, j)) {
						continue;
					}
					int storageProductId = productInfoArray[j * 2];
					int quantity = productInfoArray[j * 2 + 1];

					if (storageSlotLambda(i, j, storageProductId, quantity) == LoopAction.Exit) {
						return;
					}
				}
			}
		}
		
		/// <summary>
		/// Loops through all storages and its box slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCStorageTarget">True to skip the storage slots that are currently being targeted by an employee NPC.</param>
		/// <param name="storageSlotLambda">The StorageSlotFunction search lambda. See <see cref="StorageSlotFunction"/> for more information.</param>
		/// <returns></returns>
		public static StorageSlotInfo FindStorageSlotLambda(NPC_Manager __instance, bool checkNPCStorageTarget, StorageSlotFunction storageSlotLambda) {
			StorageSlotInfo freeStorageSlot = StorageSlotInfo.Default;

			if (__instance.storageOBJ.transform.childCount == 0) {
				return freeStorageSlot;
			}

			ForEachStorageSlotLambda(__instance, checkNPCStorageTarget,
				(storageId, slotId, productId, quantity) => {

					LoopStorageAction loopStorageAction = storageSlotLambda(storageId, slotId, productId, quantity);

					if (loopStorageAction == LoopStorageAction.SaveHighPrio || loopStorageAction == LoopStorageAction.SaveAndExit) {
						freeStorageSlot.SetValues(storageId, slotId, productId, quantity);
					} else if (loopStorageAction == LoopStorageAction.SaveLowPrio && !freeStorageSlot.FreeStorageFound) {
						//Save only if it was empty
						freeStorageSlot.SetValues(storageId, slotId, productId, quantity);
					}

					//Their enum values are numerically equitative so it performs the correct action in ForEachStorageSlotLambda
					return (LoopAction)loopStorageAction;
				});

			return freeStorageSlot;
		}

		/// <summary>Defines the parameters available in the lambda to iterate through product shelf slots.</summary>
		/// <param name="prodShelfIndex">Child index of the current product shelf.</param>
		/// <param name="slotIndex">Child index of current product shelf slot.</param>
		/// <param name="productId">Product ID of the current product shelf slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current product shelf slot. Can be -1 if empty.</param>
		/// <returns>The <see cref="LoopAction"/> to perform.</returns>
		public delegate LoopAction ProdShelfLoopFunction(int prodShelfIndex, int slotIndex, int productId, int quantity);

		/// <summary>
		/// Loops through all product shelves and its item slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCProdShelfTarget">True to skip the product shelf slots that are currently being targeted by an employee NPC.</param>
		/// <param name="prodShelfSlotLambda">The ProdShelfLoopFunction search lambda. See <see cref="ProdShelfLoopFunction"/> for more information.</param>
		/// <returns></returns>
		public static void ForEachProductShelfSlotLambda(NPC_Manager __instance, bool checkNPCProdShelfTarget, ProdShelfLoopFunction prodShelfSlotLambda) {
			for (int i = 0; i < __instance.shelvesOBJ.transform.childCount; i++) {
				int[] productInfoArray = __instance.shelvesOBJ.transform.GetChild(i).GetComponent<Data_Container>().productInfoArray;
				int num = productInfoArray.Length / 2;
				for (int j = 0; j < num; j++) {
					//Check if this product shelf slot is already in use by another NPC
					if (checkNPCProdShelfTarget && EmployeeTargetReservation.IsProductShelfSlotTargeted(i, j)) {
						continue;
					}
					int prodShelfProductId = productInfoArray[j * 2];
					int quantity = productInfoArray[j * 2 + 1];

					if (prodShelfSlotLambda(i, j, prodShelfProductId, quantity) == LoopAction.Exit) {
						return;
					}
				}
			}
		}

	}
}
