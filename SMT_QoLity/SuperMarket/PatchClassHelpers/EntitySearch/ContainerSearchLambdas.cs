using System;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models;
using System.Collections.Generic;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch {


	[Flags]
	public enum ShelfSearchOptions {
		None = 0,
		/// <summary>
		/// Skip shelf slots that are currently being targeted by an employee NPC.
		/// </summary>
		CheckNPCShelfTarget = 1,
		/// <summary>
		/// Skip shelf slots that have zero quantity, regardless of having a product assigned or not.
		/// </summary>
		SkipEmptySlots = 1 << 1,
	}

	public enum ShelfType {
		StorageSlot,
		ProdShelfSlot
	}

	public class ContainerSearchLambdas {

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
			/// <summary>Keeps looping.</summary>
			Nothing = 0,
			/// <summary>Same as Nothing. Exists for conversion compatibility with LoopStorageAction.</summary>
			Nothing2 = 1,
			/// <summary>Same as Nothing. Exists for conversion compatibility with LoopStorageAction.</summary>
			Nothing3 = 2,
			/// <summary>Stop and return immediately.</summary>
			Exit = 3
		}


		/// <summary>Defines the parameters available in the lambda to find storage slots.</summary>
		/// <param name="storageIndex">Child index of the current storage shelf.</param>
		/// <param name="slotIndex">Child index of current storage slot.</param>
		/// <param name="productId">Product ID of the current storage slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current storage slot. Can be -1 if empty.</param>
		/// <param name="storageObjT">The storage object transform.</param>
		/// <returns>The <see cref="LoopStorageAction"/> to perform in the current loop. </returns>
		public delegate LoopStorageAction StorageSlotFunction(int storageIndex, int slotIndex, int productId, int quantity, Transform storageObjT);

		/// <summary>Defines the parameters available in the lambda to iterate through storage slots.</summary>
		/// <param name="storageIndex">Child index of the current storage shelf.</param>
		/// <param name="slotIndex">Child index of current storage slot.</param>
		/// <param name="productId">Product ID of the current storage slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current storage slot. Can be -1 if empty.</param>
		/// <param name="storageObjT">The storage object transform.</param>
		/// <returns>The <see cref="LoopAction"/> to perform.</returns>
		public delegate LoopAction StorageLoopFunction(int storageIndex, int slotIndex, int productId, int quantity, Transform storageObjT);

		/// <summary>
		/// Loops through all storages and its box slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCStorageTarget">True to skip the storage slots that are currently being targeted by an employee NPC.</param>
		/// <param name="skipEmptyBoxes">Small optimization to skip slots with either no box, or an empty box, so they are not passed to the caller.</param>
		/// <param name="storageSlotLambda">The StorageLoopFunction search lambda. See <see cref="StorageLoopFunction"/> for more information.</param>
		public static void ForEachStorageSlotLambda(NPC_Manager __instance, bool checkNPCStorageTarget, bool skipEmptyBoxes, StorageLoopFunction storageSlotLambda) {
			for (int i = 0; i < __instance.storageOBJ.transform.childCount; i++) {
				Transform storageObjT = __instance.storageOBJ.transform.GetChild(i);
				int[] productInfoArray = storageObjT.GetComponent<Data_Container>().productInfoArray;
				int num = productInfoArray.Length / 2;
				for (int j = 0; j < num; j++) {
					//For performance, first do quick skip condition and both check and assign quantity value for
					//	later, in case storage ends up being valid. Then do the slower check of targeted storage last.
					int quantity = -1;
					if (skipEmptyBoxes && (quantity = productInfoArray[j * 2 + 1]) <= 0 ||
							checkNPCStorageTarget && EmployeeTargetReservation.IsStorageSlotTargeted(i, j)) {
						continue;
					}

					int storageProductId = productInfoArray[j * 2];
					if (quantity == -1) {
						quantity = productInfoArray[j * 2 + 1];
					}

					if (storageSlotLambda(i, j, storageProductId, quantity, storageObjT) == LoopAction.Exit) {
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
		public static StorageSlotInfo FindStorageSlotLambda(NPC_Manager __instance, bool checkNPCStorageTarget, StorageSlotFunction storageSlotLambda) {
			StorageSlotInfo freeStorageSlot = StorageSlotInfo.Default;

			if (__instance.storageOBJ.transform.childCount == 0) {
				return freeStorageSlot;
			}

			ForEachStorageSlotLambda(__instance, checkNPCStorageTarget, skipEmptyBoxes: false,
				(storageId, slotId, productId, quantity, storageObjT) => {

					LoopStorageAction loopStorageAction = storageSlotLambda(storageId, slotId, productId, quantity, storageObjT);

					if (loopStorageAction == LoopStorageAction.SaveHighPrio || loopStorageAction == LoopStorageAction.SaveAndExit) {
						freeStorageSlot = new(storageId, slotId, productId, quantity, storageObjT.position);
					} else if (loopStorageAction == LoopStorageAction.SaveLowPrio && !freeStorageSlot.FreeStorageFound) {
						//Save only if it was empty
						freeStorageSlot = new(storageId, slotId, productId, quantity, storageObjT.position);
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
		/// <param name="prodShelfObjT">The product shelf object transform.</param>
		/// <returns>The <see cref="LoopAction"/> to perform.</returns>
		public delegate LoopAction ProdShelfLoopFunction(int prodShelfIndex, int slotIndex, int productId, int quantity, Transform prodShelfObjT);

		/// <summary>
		/// Loops through all product shelves and its item slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCProdShelfTarget">True to skip the product shelf slots that are currently being targeted by an employee NPC.</param>
		/// <param name="prodShelfSlotLambda">The ProdShelfLoopFunction search lambda. See <see cref="ProdShelfLoopFunction"/> for more information.</param>
		/// <returns></returns>
		public static void ForEachProductShelfSlotLambda(NPC_Manager __instance, bool checkNPCProdShelfTarget, ProdShelfLoopFunction prodShelfSlotLambda) {
			for (int i = 0; i < __instance.shelvesOBJ.transform.childCount; i++) {
				Transform prodShelfObjT = __instance.shelvesOBJ.transform.GetChild(i);
				int[] productInfoArray = prodShelfObjT.GetComponent<Data_Container>().productInfoArray;
				int num = productInfoArray.Length / 2;
				for (int j = 0; j < num; j++) {
					//Check if this product shelf slot is already in use by another NPC
					if (checkNPCProdShelfTarget && EmployeeTargetReservation.IsProductShelfSlotTargeted(i, j)) {
						continue;
					}
					int prodShelfProductId = productInfoArray[j * 2];
					int quantity = productInfoArray[j * 2 + 1];

					if (prodShelfSlotLambda(i, j, prodShelfProductId, quantity, prodShelfObjT) == LoopAction.Exit) {
						return;
					}
				}
			}
		}




		/* TODO 4 - Look into replacing ForEachStorageSlotLambda and ForEachProductShelfSlotLambda 
		 * with this generic implementation.
		
		public static void ForEachProductShelfSlotLambdaNew(NPC_Manager __instance, ShelfSearchOptions searchOptions, ShelfSlotLoopFunction shelfSlotLambda) {
			ForEachShelfSlotLambda(__instance, ShelfType.ProdShelfSlot, searchOptions, shelfSlotLambda);
		}

		public static void ForEachStorageSlotLambdaNew(NPC_Manager __instance, ShelfSearchOptions searchOptions, ShelfSlotLoopFunction shelfSlotLambda) {
			ForEachShelfSlotLambda(__instance, ShelfType.StorageSlot, searchOptions, shelfSlotLambda);
		}
				

		public delegate LoopAction ShelfSlotLoopFunction(int shelfIndex, int slotIndex, int productId, int quantity, Data_Container dataContainer, Vector3 position);

		
		public static void ForEachShelfSlotLambda(NPC_Manager __instance, ShelfType shelfType, 
				ShelfSearchOptions searchOptions, ShelfSlotLoopFunction shelfSlotLambda) {

			Transform shelfTransform;
			Data_Container dataContainer;
			int[] productInfoArray;

			Transform shelfObjT = shelfType == ShelfType.ProdShelfSlot ? 
				__instance.shelvesOBJ.transform : __instance.storageOBJ.transform;
			
			int shelfCount = shelfObjT.childCount;
			for (int i = 0; i < shelfCount; i++) {

				shelfTransform = shelfObjT.GetChild(i);
				dataContainer = shelfTransform.GetComponent<Data_Container>();
				productInfoArray = dataContainer.productInfoArray;

				int num = productInfoArray.Length / 2;

				for (int j = 0; j < num; j++) {
					int quantity = productInfoArray[j * 2 + 1];
					if (((searchOptions & ShelfSearchOptions.SkipEmptySlots) != 0) && quantity <= 0) {
						continue;
					}
					//Check if this product shelf slot is already in use by another NPC
					if (((searchOptions & ShelfSearchOptions.CheckNPCShelfTarget) != 0) 
							&& EmployeeTargetReservation.IsProductShelfSlotTargeted(i, j)) {
						continue;
					}
					int prodShelfProductId = productInfoArray[j * 2];

					if (shelfSlotLambda(i, j, prodShelfProductId, quantity, dataContainer, shelfTransform.position) == LoopAction.Exit) {
						return;
					}
				}
			}
		}
		*/

		public delegate LoopAction ShelfLoopFunction(int shelfIndex, int[] productInfoArray, Data_Container dataContainer, Vector3 position);
		public delegate LoopAction SlotLoopFunction(int slotIndex, int productId, int quantity);

		/// <summary>
		/// Loops through all product shelves and its item slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="productShelfLambda">The ProdShelfLoopFunction search lambda. See <see cref="ShelfLoopFunction"/> for more information.</param>
		/// <returns></returns>
		public static void ForEachShelfLambda(NPC_Manager __instance, 
				ShelfType shelfType, ShelfLoopFunction productShelfLambda) {

			Transform shelfTransform;
			Data_Container dataContainer;
			int[] productInfoArray;

			Transform shelfObjT = shelfType == ShelfType.ProdShelfSlot ?
				__instance.shelvesOBJ.transform : __instance.storageOBJ.transform;

			int shelfCount = shelfObjT.childCount;
			for (int i = 0; i < shelfCount; i++) {

				shelfTransform = shelfObjT.GetChild(i);
				dataContainer = shelfTransform.GetComponent<Data_Container>();
				productInfoArray = dataContainer.productInfoArray;

				if (productShelfLambda(i, productInfoArray, dataContainer, shelfTransform.position) == LoopAction.Exit) {
					return;
				}
			}
		}

		public static IEnumerable<ShelfSlotData> GetShelfSlotsFromShelfData(ShelfData shelfData, 
				ShelfType shelfType, ShelfSearchOptions searchOptions) {

			int[] productInfoArray = shelfData.ProductInfoArray;

			int num = productInfoArray.Length / 2;

			for (int j = 0; j < num; j++) {
				int quantity = productInfoArray[j * 2 + 1];
				if (((searchOptions & ShelfSearchOptions.SkipEmptySlots) != 0) && quantity <= 0) {
					continue;
				}
				//Check if this product shelf slot is already in use by another NPC
				if (((searchOptions & ShelfSearchOptions.CheckNPCShelfTarget) != 0)
						&& EmployeeTargetReservation.IsShelfSlotTargeted(shelfType, shelfData.ShelfIndex, j)) {
					continue;
				}
				int shelfProductId = productInfoArray[j * 2];

				yield return new(shelfData, j, shelfProductId, quantity);
			}
		}

	}
}
