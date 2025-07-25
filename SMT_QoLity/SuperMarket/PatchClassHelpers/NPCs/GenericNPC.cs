using Damntry.Utils.Logging;
using Mirror;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Movement;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs {

	public enum NPCType {
		Dummy,      // NPCs wandering around the street
		Employee,   // Store workers
		Customer    // Buying customers
	}


	public abstract class GenericNPC : NetworkBehaviour {


		public Vector3 LastDestinationSet => GetComponent<NPC_Movement>().LastDestinationSet;


		protected void MoveTo(Vector3 destination, Vector3 targetObjectPosition) {
			GetComponent<NPC_Movement>().MoveTo(destination, targetObjectPosition);
		}
		
		protected void MoveTo(Vector3 destination) {
			GetComponent<NPC_Movement>().MoveTo(destination);
		}

		protected void MoveToScout(Vector3 destination) {
			GetComponent<NPC_Movement>().MoveToScout(destination);
		}

		public static void AddSuperQolNpcObjects(GameObject npcObj, NPCType npcType) {
			if (npcObj == null) {
				TimeLogger.Logger.LogTimeFatal("The parameter npcObj is null", LogCategories.AI);
				return;
			}

			GameObject npcGameObject = new("SuperQolNPC_Data");

			//TODO 0 - Before implementing this for customers, change it so instead of using an NPC_Movement
			//	component for each NPC, create a npc movement manager, much like the base game does, one for
			//	each NPCType. Its uglier but a necessity for performance.
			switch (npcType) {
				case NPCType.Dummy:
					throw new NotImplementedException("Dummy NPCs are not implemented.");
				case NPCType.Employee:
					npcGameObject.AddComponent<EmployeeNPC>();
					npcGameObject.AddComponent<NPC_Movement>();
					break;
				case NPCType.Customer:
					throw new NotImplementedException("Customer NPCs are not implemented.");
				default:
					TimeLogger.Logger.LogTimeFatal($"Unknown NPC type: {npcType}", LogCategories.AI);
					return;
			}
			
			npcGameObject.transform.SetParent(npcObj.transform);
		}

	}

}
