using System;
using System.Collections.Generic;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.TargetMarking {

	public enum TargetType {
		NonReservable,
		GroundBox,
		StorageSlot,
		ProdShelfSlot
	}

	public static class EmployeeTargetReservation {

		//TODO 1 - Integrate this as a component of GenericNPC, instead of its own thing.

		//Lists of NPCs targets. The hashsets exist to improve target search performance.
		private static readonly Dictionary<uint, GameObject> NpcBoxTargets = new();
		private static readonly HashSet<GameObject> groundboxesTargeted = new();

		private static readonly Dictionary<uint, StorageSlotInfo> NpcStorageSlotTargets = new();
		private static readonly HashSet<StorageSlotInfo> storageSlotsTargeted = new (new TargetContainerSlotComparer());

		private static readonly Dictionary<uint, ProductShelfSlotInfo> NpcShelfProductSlotTargets = new();
		private static readonly HashSet<ProductShelfSlotInfo> shelfProductSlotsTargeted = new (new TargetContainerSlotComparer());


		/*	TODO 5 - Eventually this info will become the new NPC watch panel. But instead of returning
		//	a string, I ll return a new info class so the caller formats it however it wants.
		//		
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

		private static string GetNpcReservations<T>(Dictionary<uint, T> targets) {
			//TODO 5 - Group by taskpriority of the employee and show it.
			//	The grouping below is not working right but it needs a remake to add new features, so not worth fixing.
			return string.Join("\n", 
				targets.Keys
					.GroupBy(npc => npc.taskPriority)
					.OrderBy(npcTaskGroup => npcTaskGroup.Key)
					.Select(npcTaskGroup => {
						string npcTaskStr = "";
						foreach (uint netId in npcTaskGroup) {
							npcTaskStr = string.Join(", ", $"#{netId, 4}");
						}
						return npcTaskStr;
					})
			);
		}
		*/

		public static void ClearAll() {
			NpcBoxTargets.Clear();
			groundboxesTargeted.Clear();
			NpcStorageSlotTargets.Clear();
			storageSlotsTargeted.Clear();
			NpcShelfProductSlotTargets.Clear();
			shelfProductSlotsTargeted.Clear();
		}

		public static void ClearNPCReservations(uint netidNPC) {
			DeleteAllNPCTargets(netidNPC);
		}

		public static bool IsGroundBoxTargeted(GameObject boxObj) {
			return groundboxesTargeted.Contains(boxObj);
		}

		public static bool IsShelfSlotTargeted(ShelfType shelfType, int ShelfIndex, int SlotIndex) =>
			shelfType switch {
				ShelfType.StorageSlot => IsStorageSlotTargeted(ShelfIndex, SlotIndex),
				ShelfType.ProdShelfSlot => IsProductShelfSlotTargeted(ShelfIndex, SlotIndex),
				_ => throw new NotImplementedException(shelfType.ToString())
			};

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

		public static bool HasTargetedStorage(this uint netidNPC, out StorageSlotInfo storageSlotInfo) {
			return NpcStorageSlotTargets.TryGetValue(netidNPC, out storageSlotInfo);
		}

		public static bool HasTargetedProductShelf(this uint netidNPC, out ProductShelfSlotInfo productShelfSlotInfo) {
			return NpcShelfProductSlotTargets.TryGetValue(netidNPC, out productShelfSlotInfo);
		}

		/// <summary>Checks if the NPC has previous targets, and deletes them.</summary>
		/// <exception cref="InvalidOperationException">Error when the NPC/target logic shouldnt be possible.</exception>
		public static void DeleteAllNPCTargets(uint netidNPC) {
			DeleteNPCTarget(netidNPC, TargetType.GroundBox);
			DeleteNPCTarget(netidNPC, TargetType.StorageSlot);
			DeleteNPCTarget(netidNPC, TargetType.ProdShelfSlot);
		}

		public static void DeleteNPCTarget(uint netidNPC, TargetType targetType) {
			if (targetType == TargetType.GroundBox && NpcBoxTargets.TryGetValue(netidNPC, out var NPCBoxPreviousTarget)) {
				DeleteTarget(netidNPC, NPCBoxPreviousTarget, NpcBoxTargets, groundboxesTargeted, targetType);
			}
			if (targetType == TargetType.StorageSlot && NpcStorageSlotTargets.TryGetValue(netidNPC, out var NPCStoragePreviousTarget)) {
				DeleteTarget(netidNPC, NPCStoragePreviousTarget, NpcStorageSlotTargets, storageSlotsTargeted, targetType);
			}
			if (targetType == TargetType.ProdShelfSlot && NpcShelfProductSlotTargets.TryGetValue(netidNPC, out var NPCProdShelfPreviousTarget)) {
				DeleteTarget(netidNPC, NPCProdShelfPreviousTarget, NpcShelfProductSlotTargets, shelfProductSlotsTargeted, targetType);
			}
		}

		private static void DeleteTarget<T>(uint netidNPC, T targetItem, Dictionary<uint, T> NPCTargets, HashSet<T> targetedItems, TargetType targetType) {
			if (!NPCTargets.ContainsKey(netidNPC)) {
				throw new InvalidOperationException($"NPC {netidNPC} was found with a {targetType} as target, but the NPC is no longer on the NPC target list.");
			}
			if (!targetedItems.Contains(targetItem)) {
				throw new InvalidOperationException($"The {targetType} was expected to be in the list of targeted items, but its no longer there.");
			}

			targetedItems.Remove(targetItem);
			NPCTargets.Remove(netidNPC);
		}

		public static void AddTargetReservation(uint netidNPC, GameObject gameObjectTarget, GenericShelfSlotInfo shelfTarget, TargetType targetType) {
			switch (targetType) {
				case TargetType.GroundBox:
					AddTarget(netidNPC, gameObjectTarget, NpcBoxTargets, groundboxesTargeted, targetType);
					break;
				case TargetType.StorageSlot:
					AddTarget(netidNPC, (StorageSlotInfo)shelfTarget, NpcStorageSlotTargets, storageSlotsTargeted, targetType);
					break;
				case TargetType.ProdShelfSlot:
					AddTarget(netidNPC, (ProductShelfSlotInfo)shelfTarget, NpcShelfProductSlotTargets, shelfProductSlotsTargeted, targetType);
					break;
			}
		}

		private static void AddTarget<T>(uint netidNPC, T targetItem, Dictionary<uint, T> NPCTargets, HashSet<T> targetedItems, TargetType targetType) {
			if (targetItem == null) {
				throw new ArgumentNullException(nameof(targetItem));
			}
			if (NPCTargets.ContainsKey(netidNPC)) {
				throw new InvalidOperationException($"NPC {netidNPC} still has a {targetType} as target, but it should not have any by now.");
			}
			if (targetedItems.Contains(targetItem)) {
				throw new InvalidOperationException($"A {targetType} was going to be marked as targeted, but it was found already marked.");
			}

			NPCTargets.Add(netidNPC, targetItem);
			targetedItems.Add(targetItem);
		}

	}
}
