using System;
using Damntry.Utils.Events;
using Damntry.Utils.Logging;
using SuperQoLity.SuperMarket.Patches;

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
		WorldStarted = 4,
	}

	public static class WorldState {

		public static event Action<GameWorldEvent> OnGameWorldChange;

		public static event Action OnQuitOrMenu;
		public static event Action OnLoadingWorld;
		public static event Action OnCanvasAwake;
		public static event Action OnFPControllerStarted;
		public static event Action OnWorldStarted;

		public static class BuildingsEvents {
			public static Action<Data_Container> OnProductShelfLoadedOrUpdated;
			public static Action<Data_Container> OnStorageLoadedOrUpdated;
			public static Action<NetworkSpawner, int> OnShelfBuilt;
		}
		


		public static GameWorldEvent CurrentGameWorldState { get; private set; } = GameWorldEvent.QuitOrMenu;

		public static GameOnlineMode CurrenOnlineMode { get; set; } = GameOnlineMode.None;


		public static bool IsGameWorldStarted =>
			CurrentGameWorldState == GameWorldEvent.WorldStarted;

		/// <summary>
		/// Returns true if the world state is at, or after, the state passed through parameter.
		/// <see cref="GameWorldEvent"/> for the order of events
		/// </summary>
		public static bool IsGameWorldAtOrAfter(GameWorldEvent worldState) =>
			(int)CurrentGameWorldState >= (int)worldState;


		public static void SetGameWorldState(GameWorldEvent state) {
			TimeLogger.Logger.LogTimeDebugFunc(() => $"World state change from {CurrentGameWorldState} to {state}", LogCategories.Loading);
			CurrentGameWorldState = state;

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
					OnQuitOrMenu += actionOnEvent;
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
				case GameWorldEvent.WorldStarted:
					OnWorldStarted += actionOnEvent;
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
				GameWorldEvent.QuitOrMenu => OnQuitOrMenu,
				GameWorldEvent.LoadingWorld => OnLoadingWorld,
				GameWorldEvent.CanvasAwake => OnCanvasAwake,
				GameWorldEvent.FPControllerStarted => OnFPControllerStarted,
				GameWorldEvent.WorldStarted => OnWorldStarted,
				_ => null
			};

	}
}
