using Damntry.Utils.Events;
using Damntry.Utils.Logging;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket {

	public enum GameOnlineMode {
		None,
		Host,
		Client
	}

	public enum GameWorldEvent {
		QuitOrMenu = 0,
		LoadingWorld = 1,
		CanvasAwake = 2,
		FPControllerStarted = 3,
		WorldLoaded = 4,
	}


    public enum CharacterSourceType {
        LocalPlayer,
        RemotePlayer,
        Employee
    }

    public static class WorldState {

		public static event Action<GameWorldEvent> OnGameWorldChange;

		public static event Action OnQuitOrMainMenu;
		public static event Action OnLoadingWorld;
		public static event Action OnCanvasAwake;
		public static event Action OnFPControllerStarted;
		public static event Action OnWorldLoaded;

        public static Action<bool, bool> OnGamePauseChanged;

        public static class BuildingEvents {
            public static Action<Transform, ParentContainerType> OnShelfBuiltOrLoaded;
            public static Action<Data_Container> OnProductShelfLoadedBuiltOrUpdated;
			public static Action<Data_Container> OnStorageLoadedBuiltOrUpdated;
        }

        public static class ContainerEvents {
            public static Action<Transform> OnBoxSpawned;
            /// <summary>
			/// Only triggers with local player handling of boxes. For employees and remote players 
			/// use OnBoxEquippedNonLocalPlayer.
			/// Parameters: Box Transform, ProductId
			/// </summary>
            public static Action<Transform, int> OnBoxEquippedOrUpdatedLocalPlayer;
            /// <summary>
			/// Triggers when a box is picked up by an employee or remote player, but cant detect
			/// the case where a box is emptied and then filled with a different product.
			/// Parameters: Character Transform, Box Transform, ProductId, CharacterSourceType
			/// </summary>
            public static Action<Transform, Transform, int, CharacterSourceType> OnBoxEquippedRemotePlayerOrEmployee;
            public static Action<Transform, CharacterSourceType> OnBoxDroppedByPlayer;
            /// <summary>Box put into storage by employee or player.</summary>
            public static Action<Data_Container> OnBoxIntoStorage;
            /// <summary>New product assigned to product shelf.</summary>
            public static Action<Data_Container> OnProdShelfAssigned;
            /// <summary>Product unassigned from product shelf.</summary>
            public static Action<Data_Container> OnProdShelfUnassigned;
        }

        public static class PlayerEvents {
            /// <summary>PlayerNetwork instance, dropped object transform, equipped object transform, 
			/// old tool index, new tool index, local/remote player.</summary>
            public static Action<PlayerNetwork, Transform, Transform, int, int, bool> OnChangeEquipment;
        }

        public static class NPC_Events {
            /// <summary>int argument is the index in employeesArray and hiredEmployeesData, from NPC_Manager.</summary>
            public static Action<NPC_Info, int> OnEmployeeSpawned;
		}


		public static GameWorldEvent CurrentGameWorldState { get; private set; } = GameWorldEvent.QuitOrMenu;

		public static GameOnlineMode CurrentOnlineMode { get; set; } = GameOnlineMode.None;


		public static bool IsWorldLoaded =>
			CurrentGameWorldState == GameWorldEvent.WorldLoaded;

		public static bool IsHost => CurrentOnlineMode == GameOnlineMode.Host;

        public static bool IsClient => CurrentOnlineMode == GameOnlineMode.Client;

        /// <summary>
        /// Returns true if the world state is at, or after, the state passed through parameter.
        /// <see cref="GameWorldEvent"/> for the order of events
        /// </summary>
        public static bool IsGameWorldAtOrAfter(GameWorldEvent worldState) =>
			(int)CurrentGameWorldState >= (int)worldState;


		public static void SetGameWorldState(GameWorldEvent state) {
            //As a client, WorldLoaded happens before OnFPControllerStarted, and we dont want it to be overwritten.
            if (state > CurrentGameWorldState || state == GameWorldEvent.QuitOrMenu) {
                TimeLogger.Logger.LogDebugFunc(() => $"World state change from " +
					$"{CurrentGameWorldState} to {state}", LogCategories.Loading);
                CurrentGameWorldState = state;
			} else {
				LogTier logTier = CurrentOnlineMode == GameOnlineMode.Client ? LogTier.Debug : LogTier.Error;

                TimeLogger.Logger.LogFunc(logTier, () => $"World state wanted to change from " +
					$"{CurrentGameWorldState} to {state}, but it is not allowed.", 
					LogCategories.Loading, false, null);
            }

			//Trigger the world state change event
			EventMethods.TryTriggerEvents(OnGameWorldChange, state);

			//Trigger the specific event
			EventMethods.TryTriggerEvents(
				GetSubscribersForWorldState(state)
			);
		}

		public static void SubscribeToWorldStateEvent(GameWorldEvent state, Action actionOnEvent) {
			switch (state) {
				case GameWorldEvent.QuitOrMenu:
					OnQuitOrMainMenu += actionOnEvent;
					break;
				case GameWorldEvent.LoadingWorld:
					OnLoadingWorld += actionOnEvent;
					break;
				case GameWorldEvent.CanvasAwake:
					OnCanvasAwake += actionOnEvent;
					break;
				case GameWorldEvent.FPControllerStarted:
					OnFPControllerStarted += actionOnEvent;
					break;
				case GameWorldEvent.WorldLoaded:
					OnWorldLoaded += actionOnEvent;
					break;
			}
		}

        public static void UnsubscribeFromWorldStateEvent(GameWorldEvent state, Action actionOnEvent) {
            switch (state) {
                case GameWorldEvent.QuitOrMenu:
                    OnQuitOrMainMenu -= actionOnEvent;
                    break;
                case GameWorldEvent.LoadingWorld:
                    OnLoadingWorld -= actionOnEvent;
                    break;
                case GameWorldEvent.CanvasAwake:
                    OnCanvasAwake -= actionOnEvent;
                    break;
                case GameWorldEvent.FPControllerStarted:
                    OnFPControllerStarted -= actionOnEvent;
                    break;
                case GameWorldEvent.WorldLoaded:
                    OnWorldLoaded -= actionOnEvent;
                    break;
            }
        }


        /// <summary>
        /// Gets an Action with all existing subscribers for the related state event.
        /// </summary>
        /// <param name="state">The state from which its related action event will be returned.</param>
        /// <remarks>The return Action is only meant to be used to access current event subscriptions.
        /// It does not represent the original event, so it cant be subscribed to.
        /// For subscriptions use <see cref="SubscribeToWorldStateEvent"/> instead</remarks>
        public static Action GetSubscribersForWorldState(GameWorldEvent state) =>
			state switch {
				GameWorldEvent.QuitOrMenu => OnQuitOrMainMenu,
				GameWorldEvent.LoadingWorld => OnLoadingWorld,
				GameWorldEvent.CanvasAwake => OnCanvasAwake,
				GameWorldEvent.FPControllerStarted => OnFPControllerStarted,
				GameWorldEvent.WorldLoaded => OnWorldLoaded,
				_ => null
			};

	}
}
