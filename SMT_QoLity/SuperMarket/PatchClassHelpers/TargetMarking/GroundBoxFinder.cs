using System;
using System.Collections.Generic;
using System.Linq;
using SuperQoLity.SuperMarket.PatchClassHelpers.StorageSearch;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking {

	public class GroundBoxFinder {

		public static GameObject GetClosestGroundBox(NPC_Manager __instance, GameObject employee, out StorageSlotInfo storageSlot) {
			storageSlot = null;

			//Filter list of ground boxes so we skip the ones already targeted by another NPC.
			List<GameObject> untargetedGroundBoxes = EmployeeTargetReservation.GetListUntargetedBoxes(__instance.boxesOBJ);

			//Check that there are any untargeted boxes lying around to begin with.
			if (untargetedGroundBoxes.Count == 0) {
				return null;
			}

			GroundBoxStorageTargets pickableGroundBoxes = null;
			
			//Check if there is space in storage for this box.
			StorageSlotInfo freeUnassignedStorage = StorageSearchHelpers.IsFreeStorageContainer(__instance);

			if (freeUnassignedStorage.FreeStorageFound) {
				//Generate list of existing boxes on the ground
				pickableGroundBoxes = new GroundBoxStorageTargets(untargetedGroundBoxes, freeUnassignedStorage);
			} else {
				//The quick check of unassigned storage slots got nothing, now we need to do the more expensive logic.

				//Get list of products for which there is an empty, but assigned, storage slot
				var storableProducts = GetProductIdListOfFreeStorage(__instance);

				if (storableProducts.Count > 0) {
					//Get list of ground boxes for which there is an empty assigned storage slot of its product.
					pickableGroundBoxes = GetStorableGroundBoxList(storableProducts, untargetedGroundBoxes);
				}
			}

			if (!pickableGroundBoxes.HasItems()) {
				//No space in storage. Check if any ground boxes are empty and can be trashed.
				pickableGroundBoxes = GetEmptyGroundBoxList(untargetedGroundBoxes);
			}

			return GetClosestGroundBox(pickableGroundBoxes, employee.transform.position, out storageSlot);
		}

		private static Dictionary<int, StorageSlotInfo> GetProductIdListOfFreeStorage(NPC_Manager __instance) {
			//Dictionary so we keep a single storage slot for each product id found
			Dictionary<int, StorageSlotInfo> storableProducts = new();

			StorageSearchLambdas.ForEachStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					if (quantity <= 0 && productId >= 0) {
						if (!storableProducts.ContainsKey(productId)) {
							storableProducts.Add(productId, new StorageSlotInfo(storageIndex, slotIndex, productId, quantity));
						}
					}
					return StorageSearchLambdas.LoopAction.Nothing;
				}
			);

			return storableProducts;
		}

		private static GroundBoxStorageTargets GetStorableGroundBoxList(Dictionary<int, StorageSlotInfo> storableProducts, List<GameObject> untargetedGroundBoxes) {
			GroundBoxStorageTargets storableGroundBoxes = new();

			foreach (GameObject gameObjectBox in untargetedGroundBoxes) {
				int boxProductID = gameObjectBox.GetComponent<BoxData>().productID;

				foreach (var storableProduct in storableProducts) {
					if (boxProductID == storableProduct.Key) {
						storableGroundBoxes.Add(gameObjectBox, storableProduct.Value);
						break;
					}
				}
			}

			return storableGroundBoxes;
		}

		private static GroundBoxStorageTargets GetEmptyGroundBoxList(List<GameObject> untargetedGroundBoxes) {
			GroundBoxStorageTargets emptyGroundBoxes = new();

			foreach (GameObject gameObjectBox in untargetedGroundBoxes) {
				if (gameObjectBox.GetComponent<BoxData>().numberOfProducts == 0) {
					emptyGroundBoxes.Add(gameObjectBox);
				}
			}

			return emptyGroundBoxes;
		}

		private static GameObject GetClosestGroundBox(GroundBoxStorageTargets groundBoxesTargets, Vector3 sourcePos, out StorageSlotInfo storageSlot) {
			storageSlot = null;

			if (!groundBoxesTargets.HasItems()) {
				return null;
			}

			GameObject closestBox = null;
			float closestDistanceSqr = float.MaxValue;

			foreach (var groundBoxTarget in groundBoxesTargets.GetItems()) {
				float sqrDistance = (groundBoxTarget.groundBox.transform.position - sourcePos).sqrMagnitude;
				if (sqrDistance < closestDistanceSqr) {
					closestDistanceSqr = sqrDistance;
					closestBox = groundBoxTarget.groundBox;
					storageSlot = groundBoxTarget.storageSlot;
				}
			}

			return closestBox;
		}


		/// <summary>
		/// Stores the relation between a groundbox and its storage target.
		/// </summary>
		private class GroundBoxStorageTargets {

			private enum RelationshipMode {
				SingleStorage,
				StoragePerBox
			}

			private RelationshipMode relMode;

			/// <summary>For when there is one specific storage target for each box</summary>
			private List<(GameObject, StorageSlotInfo)> groundBoxStorage;

			/// <summary>For when there is single unassigned storage target for one or more boxes</summary>
			private List<GameObject> groundBoxList;
			private StorageSlotInfo unassignedStorageSlot;

			public GroundBoxStorageTargets() {
				groundBoxStorage = new();

				relMode = RelationshipMode.StoragePerBox;
			}

			public GroundBoxStorageTargets(List<GameObject> groundBoxList, StorageSlotInfo unassignedStorageSlot) {
				this.groundBoxList = groundBoxList;
				this.unassignedStorageSlot = unassignedStorageSlot;

				relMode = RelationshipMode.SingleStorage;
			}

			public void Add(GameObject groundBoxList, StorageSlotInfo storageSlot = null) {
				if (relMode == RelationshipMode.SingleStorage) {
					throw new InvalidOperationException($"You must initialize {nameof(GroundBoxStorageTargets)} using the empty constructor to use this method.");
				}

				groundBoxStorage.Add((groundBoxList, storageSlot));
			}

			public bool HasItems() {
				return relMode switch {
					RelationshipMode.StoragePerBox => groundBoxStorage?.Count > 0,
					RelationshipMode.SingleStorage => groundBoxList?.Count > 0,
					_ => throw new InvalidOperationException($"Non implemented enum switch {relMode}")
				};
			}

			public List<(GameObject groundBox, StorageSlotInfo storageSlot)> GetItems() {
				return relMode switch {
					RelationshipMode.StoragePerBox => groundBoxStorage,
					RelationshipMode.SingleStorage => groundBoxList.Select(box => (box, unassignedStorageSlot)).ToList(),
					_ => throw new InvalidOperationException($"Non implemented enum switch {relMode}")
				};
			}

		}

	}
}
