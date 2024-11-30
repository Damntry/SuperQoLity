using System;
using System.Collections.Generic;
using System.Linq;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch {


	public class GroundBoxSearch {

		public static GroundBoxStorageTarget GetClosestGroundBox(NPC_Manager __instance, GameObject employee) {
			//Filter list of ground boxes so we skip the ones already targeted by another NPC.
			List<GameObject> untargetedGroundBoxes = GetListUntargetedStationaryBoxes(__instance.boxesOBJ);

			//Check that there are any untargeted boxes lying around to begin with.
			if (untargetedGroundBoxes.Count == 0) {
				return GroundBoxStorageTarget.Default;
			}

			GroundBoxStorageList pickableGroundBoxes = new();

			//Get list of products for which there is an empty, but assigned, storage slot
			var storableProducts = GetProductIdListOfFreeStorage(__instance);

			if (storableProducts.Count > 0) {
				//Get list of ground boxes for which there is an empty assigned storage slot of its product.
				pickableGroundBoxes = GetStorableGroundBoxList(storableProducts, untargetedGroundBoxes);
			}

			if (!pickableGroundBoxes.HasItems()) {
				//No assigned free slot. Check if there is unassigned or unlabeled space in storage.
				StorageSlotInfo freeUnassignedStorage = ContainerSearchHelpers.FreeUnassignedStorageContainer(__instance);

				if (freeUnassignedStorage.FreeStorageFound) {
					//Generate list of existing boxes on the ground
					pickableGroundBoxes = new GroundBoxStorageList(untargetedGroundBoxes, freeUnassignedStorage);
				}
			}

			if (!pickableGroundBoxes.HasItems()) {
				//No space in storage. Check if any ground boxes are empty and can be trashed.
				pickableGroundBoxes = GetEmptyGroundBoxList(untargetedGroundBoxes);
			}

			return GetClosestGroundBox(pickableGroundBoxes, employee.transform.position);
		}

		private static List<GameObject> GetListUntargetedStationaryBoxes(GameObject allGroundBoxes) {
			List<GameObject> listUntargetedBoxes = new List<GameObject>();

			foreach (Transform box in allGroundBoxes.transform) {
				//Filter out boxes that are already reserved or moving. Sometimes boxes piled up jiggle
				//and have a decent amount of velocity applied even though they barely even flicker visually.
				if (!EmployeeTargetReservation.IsGroundBoxTargeted(box.gameObject) && box.gameObject.GetComponent<Rigidbody>().velocity.sqrMagnitude < 1.5f) {
					listUntargetedBoxes.Add(box.gameObject);
				}
			}

			return listUntargetedBoxes;
		}

		private static Dictionary<int, StorageSlotInfo> GetProductIdListOfFreeStorage(NPC_Manager __instance) {
			//Dictionary so we keep a single storage slot for each product id found
			Dictionary<int, StorageSlotInfo> storableProducts = new();

			ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					if (quantity <= 0 && productId >= 0) {
						if (!storableProducts.ContainsKey(productId)) {
							storableProducts.Add(productId, new StorageSlotInfo(storageIndex, slotIndex, productId, quantity));
						}
					}
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);

			return storableProducts;
		}

		private static GroundBoxStorageList GetStorableGroundBoxList(Dictionary<int, StorageSlotInfo> storableProducts, List<GameObject> untargetedGroundBoxes) {
			GroundBoxStorageList storableGroundBoxes = new();

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

		private static GroundBoxStorageList GetEmptyGroundBoxList(List<GameObject> untargetedGroundBoxes) {
			GroundBoxStorageList emptyGroundBoxes = new();

			foreach (GameObject gameObjectBox in untargetedGroundBoxes) {
				if (gameObjectBox.GetComponent<BoxData>().numberOfProducts == 0) {
					emptyGroundBoxes.Add(gameObjectBox);
				}
			}

			return emptyGroundBoxes;
		}

		private static GroundBoxStorageTarget GetClosestGroundBox(GroundBoxStorageList groundBoxesTargets, Vector3 sourcePos) {
			if (!groundBoxesTargets.HasItems()) {
				return GroundBoxStorageTarget.Default;
			}


			GroundBoxStorageTarget closestBoxTarget = null;
			float closestDistanceSqr = float.MaxValue;

			foreach (var groundBoxTarget in groundBoxesTargets.GetItems()) {
				float sqrDistance = (groundBoxTarget.GroundBoxObject.transform.position - sourcePos).sqrMagnitude;
				if (sqrDistance < closestDistanceSqr) {
					closestDistanceSqr = sqrDistance;
					closestBoxTarget = groundBoxTarget;
				}
			}

			return closestBoxTarget;
		}


		/// <summary>
		/// Stores the groundboxes and its storage target, if any.
		/// Also allows to set a single storage slot as the target of all added groundboxes.
		/// </summary>
		private class GroundBoxStorageList {

			private enum RelationshipMode {
				SingleStorage,
				StoragePerBox
			}

			private RelationshipMode relMode;

			/// <summary>For when there is one specific storage target for each box</summary>
			private List<GroundBoxStorageTarget> groundBoxStorage;

			/// <summary>For when there is single unassigned storage target for one or more boxes</summary>
			private List<GameObject> groundBoxList;
			private StorageSlotInfo unassignedStorageSlot;

			public GroundBoxStorageList() {
				groundBoxStorage = new();

				relMode = RelationshipMode.StoragePerBox;
			}

			public GroundBoxStorageList(List<GameObject> groundBoxList, StorageSlotInfo unassignedStorageSlot) {
				this.groundBoxList = groundBoxList;
				this.unassignedStorageSlot = unassignedStorageSlot;

				relMode = RelationshipMode.SingleStorage;
			}

			public void Add(GameObject groundBox) {
				if (relMode == RelationshipMode.SingleStorage) {
					throw new InvalidOperationException($"You must initialize {nameof(GroundBoxStorageList)} using the empty constructor to use this method.");
				}

				groundBoxStorage.Add(new GroundBoxStorageTarget(groundBox));
			}

			public void Add(GameObject groundBox, StorageSlotInfo storageSlot) {
				if (storageSlot == null) {
					throw new ArgumentNullException($"The parameter {nameof(storageSlot)} cant be null. Use \"Add(GameObject groundBox)\" instead if this is intended.");
				}
				if (relMode == RelationshipMode.SingleStorage) {
					throw new InvalidOperationException($"You must initialize {nameof(GroundBoxStorageList)} using the empty constructor to use this method.");
				}

				groundBoxStorage.Add(new GroundBoxStorageTarget(groundBox, storageSlot));
			}


			public bool HasItems() {
				return relMode switch {
					RelationshipMode.StoragePerBox => groundBoxStorage?.Count > 0,
					RelationshipMode.SingleStorage => groundBoxList?.Count > 0,
					_ => throw new InvalidOperationException($"Non implemented enum switch {relMode}")
				};
			}

			public List<GroundBoxStorageTarget> GetItems() {
				return relMode switch {
					RelationshipMode.StoragePerBox => groundBoxStorage,
					RelationshipMode.SingleStorage => groundBoxList.Select(box => new GroundBoxStorageTarget(box, unassignedStorageSlot)).ToList(),
					_ => throw new InvalidOperationException($"Non implemented enum switch {relMode}")
				};
			}

		}

	}

	public class GroundBoxStorageTarget {

		public static GroundBoxStorageTarget Default {
			get {
				return new GroundBoxStorageTarget() { FoundGroundBox = false , HasStorageTarget = false};
			}
		}

		private GroundBoxStorageTarget() { }

		public GroundBoxStorageTarget(GameObject GroundBoxObject) {
			if (GroundBoxObject == null) {
				throw new ArgumentNullException(nameof(GroundBoxObject));
			}

			this.FoundGroundBox = true;
			this.GroundBoxObject = GroundBoxObject;
		}

		public GroundBoxStorageTarget(GameObject GroundBoxObject, StorageSlotInfo StorageSlot) {
			if (GroundBoxObject == null) {
				throw new ArgumentNullException(nameof(GroundBoxObject));
			}
			if (StorageSlot == null) {
				throw new ArgumentNullException(nameof(StorageSlot));
			}

			this.FoundGroundBox = true;
			this.GroundBoxObject = GroundBoxObject;
			this.HasStorageTarget = true;
			this.StorageSlot = StorageSlot;
		}


		public bool FoundGroundBox { get; private set; }

		public GameObject GroundBoxObject { get; private set; }

		public bool HasStorageTarget { get; private set; }

		public StorageSlotInfo StorageSlot { get; private set; }

	}

}
