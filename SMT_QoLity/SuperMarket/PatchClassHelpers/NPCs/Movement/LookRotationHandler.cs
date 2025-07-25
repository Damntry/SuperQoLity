using Damntry.Utils.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Movement {

	public enum RotationSpeedMode {
		SecurityScout,
		EmployeeTarget
	}

	public class LookRotationHandler {

		private static readonly float ScoutIntervalSeconds = 9.5f;

		private static readonly float ScoutIntervalVariance = 4;

		private static readonly float MinimumRandomTurnAngle = 30f;

		private static readonly float MaximumRandomTurnAngle = 170f;


		/// <summary>
		/// Correlation between a rotation speed mode and its speed settings.
		/// </summary>
		private static readonly Dictionary<RotationSpeedMode, RotationSpeedSettings> rotationSettings = 
			new() {
				{ RotationSpeedMode.SecurityScout, new(125f, 0.35f) },
				{ RotationSpeedMode.EmployeeTarget, new(300f, 0.15f) }
			};

		
		private enum MotionType {
			None,
			LookAt
		}

		//Change Scout for LookAround
		private enum ScoutMode {
			None,
			ScoutModePending,
			Scouting,
		}

		private MotionType currentMotion;
		private ScoutMode currentScoutMode;

		/// <summary>
		/// The Transform object to which we ll apply the transformations.
		/// </summary>
		private Transform baseTransform;

		/// <summary>
		/// The position of the targeted object, if any, passed manually when the move method is called.
		/// This is not the move order destination, which is usually different than the targeted object position.
		/// This should only be null after a move order in specific cases where there is no look target.
		/// </summary>
		private Vector3? previousMoveTargetObject;

		private Quaternion targetRotation;

		private RotationSpeedSettings rotateSettings;

		private float currentTurnVelocity;

		private float nextScoutTime = -1;


		public LookRotationHandler(Transform baseTransform) {
			this.baseTransform = baseTransform;
		}

		public void StartLookTowardsProcess(RotationSpeedMode rotationMode) {
			if (currentScoutMode != ScoutMode.None) {
				currentScoutMode = ScoutMode.Scouting;
			} else if (previousMoveTargetObject.HasValue) {
				SetLookTowardsPosition(previousMoveTargetObject.Value, rotationMode);
			}
		}

		public void SetLookTowardsPosition(Vector3 lookPosition, RotationSpeedMode rotationMode) {
			Vector3 yawVector = (lookPosition - baseTransform.position).normalized;
			Quaternion targetRotation = Quaternion.LookRotation(yawVector);

			SetLookRotationParams(yawVector, targetRotation, rotationMode);
		}

		public void LookAtRandomPosition(RotationSpeedMode rotationMode) {
			int direction = Random.Range(0, 2) == 0 ? 1 : -1;
			Vector3 yawVector = direction == 1 ? Vector3.up : Vector3.down;
			float totalTurnAngle = Random.Range(MinimumRandomTurnAngle, MaximumRandomTurnAngle);

			Quaternion targetRotation = Quaternion.Euler(yawVector * totalTurnAngle) * baseTransform.rotation;

			SetLookRotationParams(yawVector, targetRotation, rotationMode);
		}



		public void SetLookRotationParams(Vector3 yawVector, Quaternion targetRotation, RotationSpeedMode rotationMode) {
			if (!rotationSettings.TryGetValue(rotationMode, out rotateSettings)) {
				TimeLogger.Logger.LogTimeFatal($"Rotation speed mode {rotationMode} " +
					$"not found in rotation settings.", LogCategories.AI);
				return;
			}

			this.targetRotation = targetRotation;

			//Resets turning var
			currentTurnVelocity = 0f;

			currentMotion = MotionType.LookAt;
		}

		public void RotateTowardsTarget(float deltaTime) {
			float angle = Mathf.SmoothDampAngle(
				baseTransform.rotation.eulerAngles.y, targetRotation.eulerAngles.y, ref currentTurnVelocity,
				rotateSettings.SmoothTime, rotateSettings.MaxRotationSpeed, deltaTime
			);

			baseTransform.rotation = Quaternion.Euler(0, angle, 0);

			if (baseTransform.rotation == targetRotation) {
				//Finished rotation
				currentMotion = MotionType.None;
			}
		}

		public void MoveOrderCalled(Vector3? targetObjectPosition, bool toScout) {
			previousMoveTargetObject = targetObjectPosition;
			currentMotion = MotionType.None;
			currentScoutMode = toScout ? ScoutMode.ScoutModePending : ScoutMode.None;
		}

		public bool IsScoutTime() {
			if (currentScoutMode == ScoutMode.Scouting &&
					(nextScoutTime == -1 || nextScoutTime <= Time.fixedTime)) {

				nextScoutTime = Time.fixedTime + ScoutIntervalSeconds +
				Random.Range(-ScoutIntervalVariance, ScoutIntervalVariance);
				return true;
			}

			return false;
		}

		public bool IsRotationPending => currentMotion == MotionType.LookAt;


		private class RotationSpeedSettings {

			/// <summary>
			/// Self explanatory.	
			/// </summary>
			public float MaxRotationSpeed { get; }
			/// <summary>
			/// The time in seconds dedicated to smoothing acceleration or deceleration.
			///	The smaller the value, the faster the max speed can be reached.
			/// </summary>
			public float SmoothTime { get; }
			public RotationSpeedSettings(float maxRotationSpeed, float smoothTime) {
				MaxRotationSpeed = maxRotationSpeed;
				SmoothTime = smoothTime;
			}
		}

	}

}
