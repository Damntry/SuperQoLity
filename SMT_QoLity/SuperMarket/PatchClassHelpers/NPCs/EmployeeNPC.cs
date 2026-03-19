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

		public static readonly string QolNPC_GameObjectName = "SuperQolNPC_Data";

		public static Dictionary<uint, EmployeeNPC> AllEmployees;

        private uint _parentNetid;
        public uint ParentNetid { 
			get {
				if (_parentNetid <= 0) {
                    _parentNetid = this.GetParentNetid();
                }
                return _parentNetid;
            }
		}

		public EmployeeNPC() {
			AllEmployees = new();
		}


        public static GameObject CreateEmployeeGameObject() {
            GameObject npcGameObject = new(QolNPC_GameObjectName);
			npcGameObject.SetActive(false);
            npcGameObject.AddComponent<EmployeeNPC>();
            npcGameObject.AddComponent<NPC_Movement>();
			return npcGameObject;
        }

		

		public static bool TryGetEmployeeFrom(GameObject employeeObj, out EmployeeNPC employeeNPC) {
			employeeNPC = null;

			if (!employeeObj) {
				TimeLogger.Logger.LogDebug("Employee object cannot be null", LogCategories.AI);
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
			} else {
				AllEmployees.RemoveDeadValueReferences();
            }
			
			GameObject employeeQolObj = employeeObj.transform.Find(QolNPC_GameObjectName).GameObject();
			if (!employeeQolObj) {
				TimeLogger.Logger.LogDebug($"Couldnt find GameObject \"{QolNPC_GameObjectName}\" " +
					$"as a children of {employeeObj}", LogCategories.AI);
				return false;
			}

			employeeNPC = employeeQolObj.GetComponent<EmployeeNPC>();

			if (!employeeNPC) {
				TimeLogger.Logger.LogError("Could not find EmployeeNPC component " +
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
            EmployeeTargetReservation.DeleteAllNPCTargets(ParentNetid);
			if (targetType != TargetType.NonReservable) {
				EmployeeTargetReservation.AddTargetReservation(ParentNetid, gameObjectTarget, shelfTarget, targetType);
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

    public static class EmployeeNPC_Extension {
        public static uint GetParentNetid(this EmployeeNPC employeeNPC) =>
			employeeNPC.transform.parent.GetComponent<NetworkBehaviour>().netId;

    }

}
