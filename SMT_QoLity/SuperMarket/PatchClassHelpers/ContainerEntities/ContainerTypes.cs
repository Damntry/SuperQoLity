using Damntry.Utils.Logging;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities {

    //ParentContainerType, ContainerType, and ContainerTypeFlags, are
    //  all cross-value compatible with each other.
    public enum ParentContainerType {
        ProductDisplay= 1,
        Storage = 4,
        GroundBox = 16,
    }

    public enum ContainerType {
        ProdShelf = ParentContainerType.ProductDisplay,
        ProdShelfSlot = 2,
        Storage = ParentContainerType.Storage,
        StorageSlot = 8,
        GroundBox = ParentContainerType.GroundBox,
    }

    [Flags]
    public enum ContainerTypeFlags {
        None = 0,
        Storage = ContainerType.Storage,
        StorageSlot = ContainerType.StorageSlot,
        ProdShelf = ContainerType.ProdShelf,
        ProdShelfSlot = ContainerType.ProdShelfSlot,
        GroundBox = ContainerType.GroundBox,
    }

    public static class DataContainerType {

        public enum TypeIndex {
            ProductShelf = 0,
            StorageShelf = 1,
            Checkout = 2,
            SelfCheckout = 3,
            Unknown = 4,
        }

        public static TypeIndex GetContainerType(int containerID, out int parentIndex) {
            parentIndex = -1;
            if (GameData.Instance == null) {
                TimeLogger.Logger.LogError($"The GameData.Instance is null.", LogCategories.Other);
                return TypeIndex.Unknown;
            }
            if (!GameData.Instance.TryGetComponent(out NetworkSpawner networkSpawner)) {
                TimeLogger.Logger.LogError($"There is no NetworkSpawner Component in GameData.", LogCategories.Other);
                return TypeIndex.Unknown;
            }
            if (networkSpawner.buildables.Length < containerID) {
                TimeLogger.Logger.LogError($"The containerId {containerID} is out of bounds for " +
                    $"the length ({networkSpawner.buildables.Length}) of networkSpawner.buildables.", LogCategories.Other);
                return TypeIndex.Unknown;
            }

            GameObject buildable = networkSpawner.buildables[containerID];

            if (buildable.TryGetComponent(out Data_Container dataContainer)) {
                parentIndex = dataContainer.parentIndex;
                return dataContainer.GetContainerType();
            }

            TimeLogger.Logger.LogError($"There is no Data_Container Component in " +
                $"the buildable for containerId {containerID}.", LogCategories.Other);

            return TypeIndex.Unknown;
        }

        public static TypeIndex GetContainerType(this Data_Container dataContainer) {
            if (dataContainer.parentIndex >= 0 && dataContainer.parentIndex < 4) {
                return (TypeIndex)dataContainer.parentIndex;
            } else {
                TimeLogger.Logger.LogWarning($"Returned unknown parentIndex type with " +
                    $"value {dataContainer.parentIndex} for Data_Container.", LogCategories.Other);
                return TypeIndex.Unknown;
            }
        }

        public static ParentContainerType ToParentContainerType(this TypeIndex typeIndex) =>
            typeIndex switch {
                TypeIndex.ProductShelf => ParentContainerType.ProductDisplay,
                TypeIndex.StorageShelf => ParentContainerType.Storage,
                _ => throw new NotImplementedException($"No ParentContainerType for typeIndex {typeIndex}"),
            };

    }
        
}
