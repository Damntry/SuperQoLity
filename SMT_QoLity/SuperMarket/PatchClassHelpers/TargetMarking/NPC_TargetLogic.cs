using System;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking {

	public static class NPC_TargetLogic {

		private enum TargetType {
			Other,
			GroundBox,
			StorageSlot,
			ShelfSlot
		}


		//Ground boxes currently targeted by NPCs. To avoid multiple NPCs trying to do the same job.
		private static Dictionary<NPC_Info, GameObject> NpcBoxTargets = new();
		private static HashSet<GameObject> groundboxesTargeted = new();

		private static Dictionary<NPC_Info, StorageSlotInfo> NpcStorageSlotTargets = new();
		private static HashSet<StorageSlotInfo> storageSlotsTargeted = new();


		public static List<GameObject> GetListUntargetedBoxes(GameObject allGroundBoxes) {
			List<GameObject> listUntargetedBoxes = new List<GameObject>();

			foreach (Transform box in allGroundBoxes.transform) {
				if (!groundboxesTargeted.Contains(box.gameObject)) {
					listUntargetedBoxes.Add(box.gameObject);
				}
			}

			return listUntargetedBoxes;
		}

		public static bool TryCheckValidTargetedStorage(this NPC_Info NPC, NPC_Manager __instance, out StorageSlotInfo storageSlotInfo) {
			bool hasTarget = NpcStorageSlotTargets.TryGetValue(NPC, out storageSlotInfo);

			if (hasTarget) {
				//Check that the target values still match the current content of the storage slot.
				int[] productInfoArray = __instance.storageOBJ.transform.GetChild(storageSlotInfo.StorageIndex).GetComponent<Data_Container>().productInfoArray;
				int productId = productInfoArray[storageSlotInfo.SlotIndex * 2];
				int quantity = productInfoArray[storageSlotInfo.SlotIndex * 2 + 1];

				if (productId == storageSlotInfo.ExtraData.ProductId && quantity >= storageSlotInfo.ExtraData.Quantity) {
					return true;
				}
			}

			//If we reach this point, the most probable cause is that a human player took from the storage slot while the NPC was on route.

			return false;
		}

		public static bool IsStorageSlotTargeted(int StorageIndex, int SlotIndex) {
			return storageSlotsTargeted.Contains(new StorageSlotInfo(StorageIndex, SlotIndex));
		}


		public static void MoveEmployeeTo(this NPC_Info NPC, Vector3 destination) {
			NPC.MoveEmployee(destination, null, null, TargetType.Other);
		}

		public static void MoveEmployeeTo(this NPC_Info NPC, GameObject gameObjectTarget) {
			NPC.MoveEmployee(gameObjectTarget.transform.position, null, null, TargetType.Other);
		}

		public static void MoveEmployeeToShelf(this NPC_Info NPC, Vector3 destination) {
			NPC.MoveEmployee(destination, null, null, TargetType.ShelfSlot);
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
				StorageSlotInfo storageTarget, TargetType targetType) {
			NavMeshAgent navMesh = NPC.gameObject.GetComponent<NavMeshAgent>();
			navMesh.destination = destination;

			//Update targeted status of objects related to this NPC.
			UpdateNPCItemMarkStatus(NPC, gameObjectTarget, storageTarget, targetType);
		}

		private static void UpdateNPCItemMarkStatus(NPC_Info NPC, GameObject gameObjectTarget, StorageSlotInfo storageTarget, TargetType targetType) {
			DeleteNPCPreviousTarget(NPC);

			UpdateTargetMarkStatus(NPC, gameObjectTarget, storageTarget, targetType);
		}

		/// <summary>Checks if the NPC has a previous target, and deletes it.</summary>
		/// <exception cref="InvalidOperationException">Error when the NPC/target logic shouldnt be possible.</exception>
		private static void DeleteNPCPreviousTarget(NPC_Info NPC) {
			if (NpcBoxTargets.TryGetValue(NPC, out var NPCBoxPreviousTarget)) {
				DeleteTarget(NPC, NPCBoxPreviousTarget, NpcBoxTargets, groundboxesTargeted, TargetType.GroundBox);
			} else if (NpcStorageSlotTargets.TryGetValue(NPC, out var NPCStoragePreviousTarget)) {
				DeleteTarget(NPC, NPCStoragePreviousTarget, NpcStorageSlotTargets, storageSlotsTargeted, TargetType.StorageSlot);
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

		private static void UpdateTargetMarkStatus(NPC_Info NPC, GameObject gameObjectTarget, StorageSlotInfo storageTarget, TargetType targetType) {
			switch (targetType) {
				case TargetType.GroundBox:
					AddTarget(NPC, gameObjectTarget, NpcBoxTargets, groundboxesTargeted, TargetType.GroundBox);
					break;
				case TargetType.StorageSlot:
					AddTarget(NPC, storageTarget, NpcStorageSlotTargets, storageSlotsTargeted, TargetType.StorageSlot);
					break;
			}
		}

		private static void AddTarget<T>(NPC_Info NPC, T targetItem, Dictionary<NPC_Info, T> NPCTargets, HashSet<T> targetedItems, TargetType targetType) {
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
