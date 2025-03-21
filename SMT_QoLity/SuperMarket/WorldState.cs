﻿using System;
using Damntry.Utils.Events;
using Damntry.Utils.Logging;

namespace SuperQoLity.SuperMarket {

	public enum GameOnlineMode {
		None,
		Host,
		Client
	}

	public enum GameWorldEvent {
		LoadingWorld,
		CanvasLoaded,
		WorldStarted,
		QuitOrMenu
	}

	public static class WorldState {

		public static event Action<GameWorldEvent> OnGameWorldChange;

		public static event Action OnLoadingWorld;
		public static event Action OnCanvasLoaded;
		public static event Action OnWorldStarted;
		public static event Action OnQuitOrMenu;


		public static bool IsGameWorldLoadingOrStarted =>
			GameWorldState == GameWorldEvent.LoadingWorld || GameWorldState == GameWorldEvent.WorldStarted;

		public static bool IsGameWorldStarted => 
			GameWorldState == GameWorldEvent.WorldStarted;


		public static GameWorldEvent GameWorldState { get; private set; } = GameWorldEvent.QuitOrMenu;

		public static GameOnlineMode CurrenOnlineMode { get; set; } = GameOnlineMode.None;


		public static void SetGameWorldState(GameWorldEvent state) {
			TimeLogger.Logger.LogTimeDebugFunc(() => $"World state change from {GameWorldState} to {state}", LogCategories.Loading);
			GameWorldState = state;

			//Trigger the world state change event
			EventMethods.TryTriggerEvents(OnGameWorldChange, GameWorldState);

			//Trigger the specific event
			EventMethods.TryTriggerEvents(
				state switch {
					GameWorldEvent.LoadingWorld => OnLoadingWorld,
					GameWorldEvent.CanvasLoaded => OnCanvasLoaded,
					GameWorldEvent.WorldStarted => OnWorldStarted,
					GameWorldEvent.QuitOrMenu => OnQuitOrMenu,
					_ => null
				}
			);
			
		}

	}
}
