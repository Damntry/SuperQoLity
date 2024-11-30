using System;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using System.Linq;
using Damntry.UtilsBepInEx.Logging;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking {

	public static class EmployeeTargetReservation {

		private enum TargetType {
			Other,
			GroundBox,
			StorageSlot,
			ProdShelfSlot
		}


		//Lists of NPCs targets. The hashsets exist to improve target search performance.
		private static readonly Dictionary<NPC_Info, GameObject> NpcBoxTargets = new();
		private static readonly HashSet<GameObject> groundboxesTargeted = new();

		private static readonly Dictionary<NPC_Info, StorageSlotInfo> NpcStorageSlotTargets = new();
		private static readonly HashSet<StorageSlotInfo> storageSlotsTargeted = new (new TargetContainerSlotComparer());

		private static readonly Dictionary<NPC_Info, ProductShelfSlotInfo> NpcShelfProductSlotTargets = new();
		private static readonly HashSet<ProductShelfSlotInfo> shelfProductSlotsTargeted = new (new TargetContainerSlotComparer());

		/// <summary>
		/// Shit system to save the last destination set, only used for warping.
		/// Sometimes a destination is only calculated partially, and the current final destination is only a rough
		/// value that doesnt reflect the real final destination that will be calculated once the NPC gets closer
		/// through the NavPath. This rough location can put the npc in a non valid spot when warping, so it gets stuck.
		/// To avoid it we simply use the the original destination value from LastDestinationSet instead of the calculated one.
		/// </summary>
		public static Dictionary<NPC_Info, Vector3> LastDestinationSet { get; private set; } = new();


		//TODO 2 - Eventually this info will become the new NPC watch panel. But instead of returning
		//		a string, I ll return a new info class so the caller formats it however it wants.
		//Actually useful stats:
		//	Employee total time idle while store open
		//	% of time working per TaskPriority while store open
		//	Number of boxes on the ground
		//	Free unassigned storage slots
		//	Free assigned storage slots
		//	Average total prod. shelf refillment %
		//	Lowest prod. shelf refillment count and its product
		//	Product assigned to any product shelf, with the lowest total number of items in storage (including ground boxes, and carried by employees)
		public static string GetReservationStatusLog() {
			return $"\n\t\t{"Ground boxes:", -15} {groundboxesTargeted.Count, 2} - {GetNpcReservations(NpcBoxTargets)}\n" +
				$"\t\t{"Storage slots:", -15} {storageSlotsTargeted.Count, 2} - {GetNpcReservations(NpcStorageSlotTargets)}\n" +
				$"\t\t{"Shelf slots:", -15} {shelfProductSlotsTargeted.Count, 2} - {GetNpcReservations(NpcShelfProductSlotTargets)}";
		}

		private static string GetNpcReservations<T>(Dictionary<NPC_Info, T> targets) {
			//TODO 0 - Group by taskpriority of the employee and show it
			//Grouping is not working right, but it needs a remake to add new features so not worth fixing.
			return string.Join("\n", 
				targets.Keys
					.GroupBy(npc => npc.taskPriority)
					.OrderBy(npcTaskGroup => npcTaskGroup.Key)
					.Select(npcTaskGroup => {
						string npcTaskStr = "";
						foreach (NPC_Info npc in npcTaskGroup) {
							npcTaskStr = string.Join(", ", $"#{npc.netId, 4}");
						}
						return npcTaskStr;
					})
			);
		}

		public static void ClearNPCReservations(this NPC_Info NPC) {
			DeleteNPCTargets(NPC);
		}

		public static bool TryCheckValidTargetedStorage(this NPC_Info NPC, NPC_Manager __instance, out StorageSlotInfo storageSlotInfo) {
			bool hasTarget = NpcStorageSlotTargets.TryGetValue(NPC, out storageSlotInfo);

			return hasTarget && ContentsMatchOrValid(__instance.storageOBJ, storageSlotInfo, TargetType.StorageSlot);
		}

		public static bool TryCheckValidTargetedProductShelf(this NPC_Info NPC, NPC_Manager __instance, int maxProductsPerRow, out ProductShelfSlotInfo productShelfSlotInfo) {
			bool hasTarget = NpcShelfProductSlotTargets.TryGetValue(NPC, out productShelfSlotInfo);
			
			return hasTarget && ContentsMatchOrValid(__instance.shelvesOBJ, new ProductShelfSlotMatch(productShelfSlotInfo, maxProductsPerRow), TargetType.ProdShelfSlot);
		}
		

		private static bool ContentsMatchOrValid(GameObject gameObjectShelf, SlotInfoBase slotInfoBase, TargetType targetType) {
			//Check that the saved target values still match the current content of the product shelf/storage slot.
			int[] productInfoArray = gameObjectShelf.transform.GetChild(slotInfoBase.ShelfIndex).GetComponent<Data_Container>().productInfoArray;

			int productId = productInfoArray[slotInfoBase.SlotIndex * 2];
			int shelfQuantity = productInfoArray[slotInfoBase.SlotIndex * 2 + 1];

			if (productId == slotInfoBase.ExtraData.ProductId) {
				if (targetType == TargetType.StorageSlot) {
					return shelfQuantity >= slotInfoBase.ExtraData.Quantity;
				} else if (targetType == TargetType.ProdShelfSlot) {
					if (slotInfoBase is not ProductShelfSlotMatch) {
						throw new InvalidOperationException($"The target slot shelf parameter ({nameof(slotInfoBase)}) must be an object of type {nameof(ProductShelfSlotMatch)}");
					}

					return shelfQuantity < ((ProductShelfSlotMatch)slotInfoBase).MaxProductsPerRow;
				}
			}

			//If we reach this point, the most probable cause is that a human player took from the shelf slot while the NPC was on route.

			return false;
		}


		private class ProductShelfSlotMatch : SlotInfoBase {


			public ProductShelfSlotMatch(ProductShelfSlotInfo shelfSlotInfo, int maxProductsPerRow)
				: base(shelfSlotInfo.ShelfIndex, shelfSlotInfo.SlotIndex, shelfSlotInfo.ExtraData.ProductId, shelfSlotInfo.ExtraData.Quantity) {

				this.MaxProductsPerRow = maxProductsPerRow;
			}


			public int MaxProductsPerRow { get; private set; }

		}

		
		public static bool IsGroundBoxTargeted(GameObject boxObj) {
			return groundboxesTargeted.Contains(boxObj);
		}

		public static bool IsStorageSlotTargeted(int StorageIndex, int SlotIndex) {
			return IsStorageSlotTargeted(new StorageSlotInfo(StorageIndex, SlotIndex));
		}

		public static bool IsProductShelfSlotTargeted(int ProdShelfIndex, int SlotIndex) {
			return IsProductShelfSlotTargeted(new ProductShelfSlotInfo(ProdShelfIndex, SlotIndex));
		}

		public static bool IsStorageSlotTargeted(StorageSlotInfo storageSlotInfo) {
			return storageSlotsTargeted.Contains(storageSlotInfo);
		}

		public static bool IsProductShelfSlotTargeted(ProductShelfSlotInfo productShelfSlotInfo) {
			return shelfProductSlotsTargeted.Contains(productShelfSlotInfo);
		}


		public static void MoveEmployeeTo(this NPC_Info NPC, Vector3 destination) {
			NPC.MoveEmployee(destination, null, null, TargetType.Other);
		}

		public static void MoveEmployeeTo(this NPC_Info NPC, GameObject gameObjectTarget) {
			NPC.MoveEmployee(gameObjectTarget.transform.position, null, null, TargetType.Other);
		}

		public static void MoveEmployeeToShelf(this NPC_Info NPC, Vector3 destination, ProductShelfSlotInfo prodShelfTarget) {
			NPC.MoveEmployee(destination, null, prodShelfTarget, TargetType.ProdShelfSlot);
		}

		public static void MoveEmployeeToStorage(this NPC_Info NPC, Vector3 destination, StorageSlotInfo storageTarget) {
			NPC.MoveEmployee(destination, null, storageTarget, TargetType.StorageSlot);
		}

		/// <summary>
		/// Sets a destination for NPC employees towards a box. The destination will be the position of gameObjectTarget.
		/// Substitutes navMesh.destination so we can also update marked targets accordingly.
		/// </summary>
		public static void MoveEmployeeToBox(this NPC_Info NPC, GameObject gameObjectTarget) {
			NPC.MoveEmployeeToBox(gameObjectTarget.transform.position, gameObjectTarget);
		}

		/// <summary>
		/// Sets a destination for NPC employees towards a box.
		/// Substitutes navMesh.destination so we can also update marked targets accordingly.
		/// </summary>
		public static void MoveEmployeeToBox(this NPC_Info NPC, Vector3 destination, GameObject gameObjectTarget) {
			NPC.MoveEmployee(destination, gameObjectTarget, null, TargetType.GroundBox);
		}

		private static void MoveEmployee(this NPC_Info NPC, Vector3 destination, GameObject gameObjectTarget,
				SlotInfoBase shelfTarget, TargetType targetType) {
			NavMeshAgent navMesh = NPC.gameObject.GetComponent<NavMeshAgent>();
			
			if (LastDestinationSet.ContainsKey(NPC)) {
				LastDestinationSet[NPC] = destination;
			} else {
				LastDestinationSet.Add(NPC, destination);
			}

			navMesh.destination = destination;

			//Update targeted status of objects related to this NPC.
			UpdateNPCItemMarkStatus(NPC, gameObjectTarget, shelfTarget, targetType);
		}

		public static void AddExtraStorageTarget(this NPC_Info NPC, StorageSlotInfo shelfTarget) {
			UpdateTargetMarkStatus(NPC, null, shelfTarget, TargetType.StorageSlot);
		}

		public static void AddExtraProductShelfTarget(this NPC_Info NPC, ProductShelfSlotInfo shelfTarget) {
			UpdateTargetMarkStatus(NPC, null, shelfTarget, TargetType.ProdShelfSlot);
		}

		private static void UpdateNPCItemMarkStatus(NPC_Info NPC, GameObject gameObjectTarget, SlotInfoBase shelfTarget, TargetType targetType) {
			DeleteNPCTargets(NPC);

			UpdateTargetMarkStatus(NPC, gameObjectTarget, shelfTarget, targetType);
		}

		/// <summary>Checks if the NPC has previous targets, and deletes them.</summary>
		/// <exception cref="InvalidOperationException">Error when the NPC/target logic shouldnt be possible.</exception>
		private static void DeleteNPCTargets(NPC_Info NPC) {
			if (NpcBoxTargets.TryGetValue(NPC, out var NPCBoxPreviousTarget)) {
				DeleteTarget(NPC, NPCBoxPreviousTarget, NpcBoxTargets, groundboxesTargeted, TargetType.GroundBox);
			}
			if (NpcStorageSlotTargets.TryGetValue(NPC, out var NPCStoragePreviousTarget)) {
				DeleteTarget(NPC, NPCStoragePreviousTarget, NpcStorageSlotTargets, storageSlotsTargeted, TargetType.StorageSlot);
			}
			if (NpcShelfProductSlotTargets.TryGetValue(NPC, out var NPCProdShelfPreviousTarget)) {
				DeleteTarget(NPC, NPCProdShelfPreviousTarget, NpcShelfProductSlotTargets, shelfProductSlotsTargeted, TargetType.ProdShelfSlot);
			}
		}

		private static void DeleteTarget<T>(NPC_Info NPC, T targetItem, Dictionary<NPC_Info, T> NPCTargets, HashSet<T> targetedItems, TargetType targetType) {
			if (!NPCTargets.ContainsKey(NPC)) {
				throw new InvalidOperationException($"NPC {NPC.NPCID} was found with a {targetType} as target, but the NPC is no longer on the NPC target list.");
			}
			if (!targetedItems.Contains(targetItem)) {
				throw new InvalidOperationException($"The {targetType} was expected to be in the list of targeted items, but its no longer there.");
			}

			targetedItems.Remove(targetItem);
			NPCTargets.Remove(NPC);
		}

		private static void UpdateTargetMarkStatus(NPC_Info NPC, GameObject gameObjectTarget, SlotInfoBase shelfTarget, TargetType targetType) {
			switch (targetType) {
				case TargetType.GroundBox:
					AddTarget(NPC, gameObjectTarget, NpcBoxTargets, groundboxesTargeted, targetType);
					break;
				case TargetType.StorageSlot:
					AddTarget(NPC, (StorageSlotInfo)shelfTarget, NpcStorageSlotTargets, storageSlotsTargeted, targetType);
					break;
				case TargetType.ProdShelfSlot:
					AddTarget(NPC, (ProductShelfSlotInfo)shelfTarget, NpcShelfProductSlotTargets, shelfProductSlotsTargeted, targetType);
					break;
			}
		}

		private static void AddTarget<T>(NPC_Info NPC, T targetItem, Dictionary<NPC_Info, T> NPCTargets, HashSet<T> targetedItems, TargetType targetType) {
			if (targetItem == null) {
				throw new ArgumentNullException(nameof(targetItem));
			}
			if (NPCTargets.ContainsKey(NPC)) {
				throw new InvalidOperationException($"NPC {NPC.NPCID} still has a {targetType} as target, but it should not have any by now.");
			}
			if (targetedItems.Contains(targetItem)) {
				throw new InvalidOperationException($"A {targetType} was going to be marked as targeted, but it was found already marked.");
			}

			NPCTargets.Add(NPC, targetItem);
			targetedItems.Add(targetItem);
		}

	}
}
