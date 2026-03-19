using System;
using System.Collections.Generic;
using Damntry.Utils.Logging;
using Rewired;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Standalone.Components {

    //TODO 2 - After all the work done expanding the InputDetection class, this one is now sitting in a weird spot.
    //	Its main thing being that:
    //		1. It works for mouse clicks
    //			In InputDetection it also works, except that Bepinex ConfigurationManager GUI wont detect mouse0.
    //		2. Does its logic for every registered key
    //			InputDetection only processes the one with the prioritized activation.
    //		3. Makes use of the Rewire framework to get the current key associated with an action.
    //			InputDetection uses mod specific user defined keys.
    //
    //	I think this class still has its place, but it should be using prioritized activation to avoid clicks
	//	always executing. I should make this reference some of the InputDetection logic.


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

			if (subscriptedReferences == null || subscriptedReferences.Count == 0 || 
					!subscriptedReferences.ContainsKey(typeof(T))) {
				return;
			}

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

			if (!instance || !instance.didAwake) {
				WorldState.SubscribeToWorldStateEvent(behaviourWorldEvent, AddInputBehaviourComponent);
				Action addComponentEvent = WorldState.GetSubscribersForWorldState(behaviourWorldEvent);

				if (WorldState.IsGameWorldAtOrAfter(behaviourWorldEvent)) {
					AddInputBehaviourComponent();
				}
			}

			isActive = true;
			if (instance) {
				instance.enabled = true;
			}
		}

		private static void AddInputBehaviourComponent() {
			if (GameObjectManager.AddComponentTo<InputBehaviour>(TargetObject.LocalGamePlayer) == null) {
				TimeLogger.Logger.LogError("The click behaviour component could not be added " +
					"and all its related logic wont work.", LogCategories.KeyMouse);
			}
		}

		private static void DeactivateBehaviour() {
			if (!isActive) {
				return;
			}

			subscriptedReferences = null;

			isActive = false;
			if (instance) {
				instance.enabled = false;
			}
		}


		public void Awake() {
			instance = this;

			KeyActions.Initialize();

			PlayerNetwork pNetwork = SMTInstances.LocalPlayerNetwork();
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
			//TODO 1 - If the user changes the config, we would still keep old values.
			//	Try to hook into some Rewired key changed event, or worst case a SMT patch.
			MainActionId = ReInput.mapping.GetActionId(ActionNames.MainAction);
			SecondaryActionId = ReInput.mapping.GetActionId(ActionNames.SecondaryAction);

			if (MainActionId < 0 && SecondaryActionId < 0) {
				throw new InvalidOperationException("None of the supported keys could " +
					$"be retrieved. {MyPluginInfo.PLUGIN_NAME} click behaviours wont work.");
			}
			if (MainActionId < 0) {
				TimeLogger.Logger.LogError($"An action could not be found with name " +
					$"\"{ActionNames.MainAction}\"", LogCategories.KeyMouse);
			}
			if (SecondaryActionId < 0) {
				TimeLogger.Logger.LogError($"An action could not be found with name " +
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
