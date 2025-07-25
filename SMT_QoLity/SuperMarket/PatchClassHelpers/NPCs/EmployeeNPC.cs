using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using Mirror;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Movement;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs {
	public class EmployeeNPC : GenericNPC {

		public static Dictionary<uint, EmployeeNPC> AllEmployees = new();

		private static readonly string QolNPC_GameObjectName = "SuperQolNPC_Data";


		public static bool TryGetEmployeeFrom(GameObject employeeObj, out EmployeeNPC employeeNPC) {
			employeeNPC = null;

			if (!employeeObj) {
				TimeLogger.Logger.LogTimeDebug("Employee object cannot be null", LogCategories.AI);
				return false;
			}

			uint netid = employeeObj.GetComponent<NetworkIdentity>().netId;
			if (AllEmployees.TryGetValue(netid, out employeeNPC)) {
				if (employeeNPC) {
					return true;
				} else {
					//Employee in cache is no longer valid. Remove from cache and continue 
					//	and try to find the new one, though it wont since new objects can 
					//	never have the same netId, but doesnt hurt to try in case a mod
					//	does some strange replacement.
					AllEmployees.Remove(netid);
				}
			}

			GameObject employeeQolObj = employeeObj.transform.Find(QolNPC_GameObjectName).GameObject();
			if (!employeeQolObj) {
				TimeLogger.Logger.LogTimeDebug($"Couldnt find GameObject \"{QolNPC_GameObjectName}\" " +
					$"as a children of {employeeObj}", LogCategories.AI);
				return false;
			}

			employeeNPC = employeeQolObj.GetComponent<EmployeeNPC>();

			if (!employeeNPC) {
				TimeLogger.Logger.LogTimeError("Could not find EmployeeNPC component " +
					$"on the \"{QolNPC_GameObjectName}\" GameObject.", LogCategories.AI);
				return false;
			}

			AllEmployees.Add(netid, employeeNPC);

			return true;
		}


		public void MoveEmployeeTo(Vector3 destination, GameObject targetObject) {
			MoveEmployee(destination, targetObject, null, TargetType.NonReservable);
		}

		public void MoveEmployeeTo(Transform transformDestination, GameObject targetObject) {
			MoveEmployee(transformDestination.position, targetObject, null, TargetType.NonReservable);
		}

		public void MoveEmployeeTo(GameObject gameObjectDestination, GameObject targetObject) {
			MoveEmployee(gameObjectDestination.transform.position, targetObject, null, TargetType.NonReservable);
		}

		public void MoveSecurityAndScout(Vector3 destination) {
			MoveEmployee(destination, null, null, TargetType.NonReservable, scout: true);
		}

		public void MoveEmployeeToRestPosition() {
			//TODO 1 - The target object will be the closest happiness furniture to the destination point.
			MoveEmployee(NPC_Manager.Instance.AttemptToGetRestPosition(), null, null, TargetType.NonReservable);
		}

		public void MoveEmployeeToShelf(Vector3 destination, ProductShelfSlotInfo prodShelfTarget) {
			MoveEmployee(destination, null, prodShelfTarget, TargetType.ProdShelfSlot);
		}

		public void MoveEmployeeToStorage(Vector3 destination, StorageSlotInfo storageTarget) {
			MoveEmployee(destination, null, storageTarget, TargetType.StorageSlot);
		}

		/// <summary>
		/// Sets a destination for NPC employees towards a box. The destination will be the position of gameObjectTarget.
		/// Substitutes navMesh.destination so we can also update marked targets accordingly.
		/// </summary>
		public void MoveEmployeeToBox(GameObject gameObjectTarget) {
			MoveEmployeeToBox(gameObjectTarget.transform.position, gameObjectTarget);
		}

		/// <summary>
		/// Sets a destination for NPC employees towards a box.
		/// Substitutes navMesh.destination so we can also update marked targets accordingly.
		/// </summary>
		public void MoveEmployeeToBox(Vector3 destination, GameObject gameObjectTarget) {
			MoveEmployee(destination, gameObjectTarget, null, TargetType.GroundBox);
		}

		protected void MoveEmployee(Vector3 destination, GameObject gameObjectTarget,
				GenericShelfSlotInfo shelfTarget, TargetType targetType, bool scout = false) {

			if (scout) {
				MoveToScout(destination);
			} else if (gameObjectTarget) {
				MoveTo(destination, gameObjectTarget.transform.position);
			} else if (shelfTarget != null && shelfTarget.ExtraData.Position != Vector3.zero) {
				MoveTo(destination, shelfTarget.ExtraData.Position);
			} else {
				//Last choice. To be used only when there really is no target object to look at.
				MoveTo(destination);
			}

			//Update targeted status of objects related to this NPC.
			EmployeeTargetReservation.DeleteAllNPCTargets(this.netId);
			if (targetType != TargetType.NonReservable) {
				EmployeeTargetReservation.AddTargetReservation(this.netId, gameObjectTarget, shelfTarget, targetType);
			}
		}

		public void StartLookProcess(RotationSpeedMode rotationMode) {
			GetComponent<NPC_Movement>().StartLookTowardsProcess(rotationMode);
		}

		/// <summary>
		/// This should only be used in very special cases when we want to do some custom NPC rotation.
		/// </summary>
		/// <param name="lookPosition"></param>
		public void SetLookTowardsPosition(Vector3 lookPosition, RotationSpeedMode rotationMode) {
			GetComponent<NPC_Movement>().SetLookTowardsPosition(lookPosition, rotationMode);
		}

	}

	public static class EmployeeReservationExtensions {

		public static void ClearNPCReservations(this EmployeeNPC employeeNPC) {
			EmployeeTargetReservation.ClearNPCReservations(employeeNPC.netId);
		}

		public static void AddExtraStorageTarget(this EmployeeNPC employeeNPC, StorageSlotInfo shelfTarget) {
			EmployeeTargetReservation.AddTargetReservation(employeeNPC.netId, null, shelfTarget, TargetType.StorageSlot);
		}

		public static void AddExtraProductShelfTarget(this EmployeeNPC employeeNPC, ProductShelfSlotInfo shelfTarget) {
			EmployeeTargetReservation.AddTargetReservation(employeeNPC.netId, null, shelfTarget, TargetType.ProdShelfSlot);
		}

		public static bool RefreshAndCheckValidTargetedStorage(this EmployeeNPC employeeNPC, NPC_Manager __instance,
				bool clearReservation, out StorageSlotInfo storageSlotInfo) {

			bool isValid = TargetMatching.RefreshAndCheckTargetedShelf(employeeNPC, __instance, 
				clearReservation, -1, TargetType.StorageSlot, out GenericShelfSlotInfo slotInfoBase);
			storageSlotInfo = (StorageSlotInfo)slotInfoBase;
			return isValid;
		}


		public static bool RefreshAndCheckValidTargetedProductShelf(this EmployeeNPC employeeNPC, NPC_Manager __instance,
				RestockJobInfo jobInfo) {

			return TargetMatching.RefreshAndCheckValidTargetedProductShelf(employeeNPC, __instance, false, jobInfo);
		}

		public static bool HasTargetedStorage(this EmployeeNPC employeeNPC) {
			return EmployeeTargetReservation.HasTargetedStorage(employeeNPC.netId, out _);
		}

		public static bool HasTargetedProductShelf(this EmployeeNPC employeeNPC) {
			return EmployeeTargetReservation.HasTargetedProductShelf(employeeNPC.netId, out _);
		}

	}
}
