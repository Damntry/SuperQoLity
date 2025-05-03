using Damntry.Utils.Logging;
using HutongGames.PlayMaker;
using UnityEngine;

namespace SuperQoLity.SuperMarket.ModUtils {

	public enum DataContainerType {
		ProductShelf = 0,
		StorageShelf = 1,
		Checkout = 2,
		SelfCheckout = 3,
		Unknown = 4,
	}

	public static class AuxUtils {

		public static bool IsKeypressed(KeyCode key, bool onlyWhileChatClosed = true) =>
			(!onlyWhileChatClosed || onlyWhileChatClosed && IsChatOpen()) && Input.GetKeyDown(key);

		public static bool IsChatOpen() => FsmVariables.GlobalVariables.GetFsmBool("InChat").Value;

		//TODO 0 - Test this
		public static DataContainerType GetContainerType(int containerID, out int parentIndex) {
			parentIndex = -1;
			if (GameData.Instance == null) {
				TimeLogger.Logger.LogTimeError($"The GameData.Instance is null.", LogCategories.Other);
				return DataContainerType.Unknown;
			}
			if (!GameData.Instance.TryGetComponent(out NetworkSpawner networkSpawner)) {
				TimeLogger.Logger.LogTimeError($"There is no NetworkSpawner Component in GameData.", LogCategories.Other);
				return DataContainerType.Unknown;
			}
			if (networkSpawner.buildables.Length < containerID) {
				TimeLogger.Logger.LogTimeError($"The containerId {containerID} is out of bounds for " +
					$"the length ({networkSpawner.buildables.Length}) of networkSpawner.buildables.", LogCategories.Other);
				return DataContainerType.Unknown;
			}

			GameObject buildable = networkSpawner.buildables[containerID];

			if (buildable.TryGetComponent(out Data_Container dataContainer)) {
				parentIndex = dataContainer.parentIndex;
				return dataContainer.GetContainerType();
			}

			TimeLogger.Logger.LogTimeError($"There is no Data_Container Component in " +
				$"the buildable for containerId {containerID}.", LogCategories.Other);

			return DataContainerType.Unknown;
		}

		//TODO 0 - Test this
		public static DataContainerType GetContainerType(this Data_Container dataContainer) {
			if (dataContainer.parentIndex >= 0 && dataContainer.parentIndex < 4) {
				return (DataContainerType)dataContainer.parentIndex;
			} else {
				TimeLogger.Logger.LogTimeWarning($"Returned unknown parentIndex type with " +
					$"value {dataContainer.parentIndex} for Data_Container.", LogCategories.Other);
				return DataContainerType.Unknown;
			}
		}

	}
}
