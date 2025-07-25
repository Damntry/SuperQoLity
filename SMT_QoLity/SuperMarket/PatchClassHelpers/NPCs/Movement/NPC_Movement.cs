using UnityEngine;
using UnityEngine.AI;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Movement {

	public class NPC_Movement : MonoBehaviour {

		private LookRotationHandler lookHandler;


		/// <summary>
		/// System to save the last destination set, only used for employee warping right now.
		/// Sometimes a destination is only calculated partially, and the current final destination is only a rough
		/// value that doesnt reflect the real final destination that will be calculated once the NPC gets closer
		/// through the NavPath. This rough location can put the npc in a non valid spot when warping, so it gets stuck.
		/// To avoid it we simply use the the original destination value from LastDestinationSet instead of the calculated one.
		/// </summary>
		public Vector3 LastDestinationSet { get; private set; }


		public void Awake() {
			lookHandler = new (transform.parent);
		}

		public void FixedUpdate() {
			if (lookHandler.IsScoutTime()) {
				lookHandler.LookAtRandomPosition(RotationSpeedMode.SecurityScout);
			}

			if (lookHandler.IsRotationPending) {
				//Rotate the NPC one step towards the target each FixedUpdate.
				lookHandler.RotateTowardsTarget(Time.fixedDeltaTime);
			}		
		}

		//TODO 0 - Engineer something so these methods can only be called from GenericNPC and derived.
		public void MoveTo(Vector3 destination, Vector3 targetObjectPosition) {
			MoveToInternal(destination, toScout: false, targetObjectPosition);
		}

		public void MoveTo(Vector3 destination) {
			MoveToInternal(destination, toScout: false);
		}

		public void MoveToScout(Vector3 destination) {
			MoveToInternal(destination, toScout: true);
		}

		private void MoveToInternal(Vector3 destination, bool toScout, Vector3? targetObjectPosition = null) {
			NavMeshAgent navMesh = gameObject.transform.parent.GetComponent<NavMeshAgent>();

			LastDestinationSet = destination;
			navMesh.destination = destination;
			lookHandler.MoveOrderCalled(targetObjectPosition, toScout);
		}

		public void SetLookTowardsPosition(Vector3 lookPosition, RotationSpeedMode rotationMode) {
			lookHandler.SetLookTowardsPosition(lookPosition, rotationMode);
		}

		public void StartLookTowardsProcess(RotationSpeedMode rotationMode) {
			lookHandler.StartLookTowardsProcess(rotationMode);
		}

	}
}
