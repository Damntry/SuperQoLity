using System;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using Damntry.UtilsBepInEx.Logging;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking
{

    public static class NPC_TargetLogic {

		private enum TargetType {
			Other,
			GroundBox,
			StorageSlot,
			ProdShelfSlot
		}


		//Lists of NPCs targets. The hashsets exist to improve target search performance.
		private static Dictionary<NPC_Info, GameObject> NpcBoxTargets = new();
		private static HashSet<GameObject> groundboxesTargeted = new();

		private static Dictionary<NPC_Info, StorageSlotInfo> NpcStorageSlotTargets = new();
		private static HashSet<StorageSlotInfo> storageSlotsTargeted = new HashSet<StorageSlotInfo>(new TargetContainerSlotComparer());

		private static Dictionary<NPC_Info, ProductShelfSlotInfo> NpcShelfProductSlotTargets = new();
		private static HashSet<ProductShelfSlotInfo> shelfProductSlotsTargeted = new HashSet<ProductShelfSlotInfo>(new TargetContainerSlotComparer());


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
			
			return hasTarget && ContentsMatchOrValid(__instance.storageOBJ, storageSlotInfo, TargetType.StorageSlot);
		}

		public static bool TryCheckValidTargetedProductShelf(this NPC_Info NPC, NPC_Manager __instance, out ProductShelfSlotInfo productShelfSlotInfo) {
			bool hasTarget = NpcShelfProductSlotTargets.TryGetValue(NPC, out productShelfSlotInfo);
			
			return hasTarget && ContentsMatchOrValid(__instance.shelvesOBJ, productShelfSlotInfo, TargetType.ProdShelfSlot);
		}

		public static bool IsStorageUntargetedAndContentsMatch(NPC_Manager instance, StorageSlotInfo storageSlotInfo) {
			return !IsStorageSlotTargeted(storageSlotInfo) && ContentsMatchOrValid(instance.shelvesOBJ, storageSlotInfo, TargetType.StorageSlot);
		}

		public static bool IsProductShelfUntargetedAndContentsMatch(NPC_Manager instance, ProductShelfSlotInfo productShelfSlotInfo) {
			return !IsProductShelfSlotTargeted(productShelfSlotInfo) && ContentsMatchOrValid(instance.shelvesOBJ, productShelfSlotInfo, TargetType.ProdShelfSlot);
		}

		private static bool ContentsMatchOrValid(GameObject gameObjectShelf, SlotInfoBase slotInfoBase, TargetType targetType) {
			//Check that the saved target values still match the current content of the product shelf/storage slot.
			int[] productInfoArray = gameObjectShelf.transform.GetChild(slotInfoBase.ShelfIndex).GetComponent<Data_Container>().productInfoArray;

			int productId = productInfoArray[slotInfoBase.SlotIndex * 2];
			int shelfQuantity = productInfoArray[slotInfoBase.SlotIndex * 2 + 1];

			if (productId == slotInfoBase.ExtraData.ProductId) {
				if (targetType == TargetType.StorageSlot) {
					if (shelfQuantity >= slotInfoBase.ExtraData.Quantity) {
						return true;
					}
				} else if (targetType == TargetType.ProdShelfSlot) {
					if (shelfQuantity <= slotInfoBase.ExtraData.Quantity) {
						return true;
					}
				}
			}

			//If we reach this point, the most probable cause is that a human player took from the shelf slot while the NPC was on route.

			return false;
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
			navMesh.destination = destination;

			//Update targeted status of objects related to this NPC.
			UpdateNPCItemMarkStatus(NPC, gameObjectTarget, shelfTarget, targetType);
		}

		private static void UpdateNPCItemMarkStatus(NPC_Info NPC, GameObject gameObjectTarget, SlotInfoBase shelfTarget, TargetType targetType) {
			DeleteNPCPreviousTarget(NPC);

			UpdateTargetMarkStatus(NPC, gameObjectTarget, shelfTarget, targetType);
		}

		/// <summary>Checks if the NPC has a previous target, and deletes it.</summary>
		/// <exception cref="InvalidOperationException">Error when the NPC/target logic shouldnt be possible.</exception>
		private static void DeleteNPCPreviousTarget(NPC_Info NPC) {
			if (NpcBoxTargets.TryGetValue(NPC, out var NPCBoxPreviousTarget)) {
				DeleteTarget(NPC, NPCBoxPreviousTarget, NpcBoxTargets, groundboxesTargeted, TargetType.GroundBox);
			}if (NpcStorageSlotTargets.TryGetValue(NPC, out var NPCStoragePreviousTarget)) {
				DeleteTarget(NPC, NPCStoragePreviousTarget, NpcStorageSlotTargets, storageSlotsTargeted, TargetType.StorageSlot);
			}if (NpcShelfProductSlotTargets.TryGetValue(NPC, out var NPCProdShelfPreviousTarget)) {
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
