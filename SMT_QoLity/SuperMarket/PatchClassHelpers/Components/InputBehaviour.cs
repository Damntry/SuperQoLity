using System;
using System.Collections.Generic;
using Damntry.Utils.Logging;
using Rewired;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Components {

	public class InputBehaviour : MonoBehaviour {

		private static InputBehaviour instance;

		private Player MainPlayerControls;

		private static bool isActive;

		private static GameWorldEvent behaviourWorldEvent = GameWorldEvent.FPControllerStarted;


		public delegate void InputAction(float currentTime, Player MainPlayerControls);

		private static Dictionary<Type, (GameWorldEvent worldEventToStartAt, InputAction inputAction)> subscriptedReferences;



		public static void RegisterClickAction<T>(InputAction inputAction, GameWorldEvent worldEventToStartAt)
				where T : class {
			
			ActivateBehaviour();

			subscriptedReferences.Add(typeof(T), (worldEventToStartAt, inputAction));
		}

		public static void UnregisterClickAction<T>()
				where T : class {

			subscriptedReferences.Remove(typeof(T));

			if (subscriptedReferences.Count == 0) {
				DeactivateBehaviour();
			}
		}

		private static void ActivateBehaviour() {
			if (isActive) {
				return;
			}

			subscriptedReferences = new();

			if (instance == null || !instance.didAwake) {
				WorldState.SubscribeToWorldStateEvent(behaviourWorldEvent, AddInputBehaviourComponent);
				Action addComponentEvent = WorldState.GetSubscribersForWorldState(behaviourWorldEvent);

				if (WorldState.IsGameWorldAtOrAfter(behaviourWorldEvent)) {
					AddInputBehaviourComponent();
				}
			}

			isActive = true;
			if (instance != null) {
				instance.enabled = true;
			}
		}

		private static void AddInputBehaviourComponent() {
			if (SMTGameObjectManager.AddComponentTo<InputBehaviour>(TargetObject.LocalGamePlayer) == null) {
				TimeLogger.Logger.LogTimeError("The click behaviour component could not be added " +
					"and all its related logic wont work.", LogCategories.KeyMouse);
			}
		}

		private static void DeactivateBehaviour() {
			if (!isActive) {
				return;
			}

			subscriptedReferences = null;

			isActive = false;
			instance.enabled = false;
		}


		public void Awake() {
			instance = this;

			KeyActions.Initialize();

			PlayerNetwork pNetwork = SMTComponentInstances.PlayerNetworkInstance();
			MainPlayerControls = pNetwork.MainPlayer;
		}

		public void Update() {
			float currentTime = Time.time;

			//Call registered methods.
			foreach (var reference in subscriptedReferences) {
				if (WorldState.IsGameWorldAtOrAfter(reference.Value.worldEventToStartAt)) {
					reference.Value.inputAction(currentTime, MainPlayerControls);
				}
			}
		}

	}

	public struct KeyActions {

		public static void Initialize() {
			MainActionId = ReInput.mapping.GetActionId(ActionNames.MainAction);
			SecondaryActionId = ReInput.mapping.GetActionId(ActionNames.SecondaryAction);

			if (MainActionId < 0 && SecondaryActionId < 0) {
				throw new InvalidOperationException("None of the supported keys could " +
					$"be retrieved. {MyPluginInfo.PLUGIN_NAME} click behaviours wont work.");
			}
			if (MainActionId < 0) {
				TimeLogger.Logger.LogTimeError($"An action could not be found with name " +
					$"\"{ActionNames.MainAction}\"", LogCategories.KeyMouse);
			}
			if (SecondaryActionId < 0) {
				TimeLogger.Logger.LogTimeError($"An action could not be found with name " +
					$"\"{ActionNames.MainAction}\"", LogCategories.KeyMouse);
			}
		}

		/// <summary>Left click by default</summary>
		public static int MainActionId { get; private set; }
		/// <summary>Right click by default</summary>
		public static int SecondaryActionId { get; private set; }

		private readonly struct ActionNames {
			public readonly static string MainAction = "Main Action";
			public readonly static string SecondaryAction = "Secondary Action";
		}

	}

}
