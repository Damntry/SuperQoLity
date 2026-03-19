using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using System;
using System.Linq;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Definitions {

    public abstract class ContainerHighlightData {

        public static ProductShelfHighlightData Products { get; } = new();
        public static StorageShelfHighlightData Storage { get; } = new();
        public static GroundBoxHighlightData GroundBox { get; } = new();

        public static ContainerHighlightData GetFromContainerParentType(ParentContainerType parentContainerType) =>
            parentContainerType switch {
                ParentContainerType.ProductDisplay => Products,
                ParentContainerType.Storage => Storage,
                ParentContainerType.GroundBox => GroundBox,
                _ => throw new NotImplementedException(parentContainerType.ToString())
            };

        public static ContainerHighlightData GetFromContainerType(ContainerType parentContainerType) =>
            parentContainerType switch {
                ContainerType.ProdShelf or ContainerType.ProdShelfSlot => Products,
                ContainerType.Storage or ContainerType.StorageSlot => Storage,
                ContainerType.GroundBox => GroundBox,
                _ => throw new NotImplementedException(parentContainerType.ToString())
            };

        public static Transform[] GetGameObjectFromParentContainerType(ParentContainerType parentContainerType) =>
            (parentContainerType switch {
                ParentContainerType.ProductDisplay => NPC_Manager.Instance?.shelvesOBJ.transform.Cast<Transform>(),
                ParentContainerType.Storage => NPC_Manager.Instance?.storageOBJ.transform.Cast<Transform>(),
                ParentContainerType.GroundBox => GetExistingParentedBoxes(),
                _ => throw new NotImplementedException($"The container type '{parentContainerType}' is not implemented."),
            }).ToArray();

        public static Transform[] GetExistingParentedBoxes() {
            ManagerBlackboard managerBBrd = SMTInstances.ManagerBlackboard();

            return NPC_Manager.Instance?.boxesOBJ.transform
                .Cast<Transform>()
                .Concat(managerBBrd.boxParent   //This one is not being used anymore but just to be safe.
                    .Cast<Transform>()
                    .Concat(managerBBrd.manufacturingBoxParent
                        .Cast<Transform>()
                    )
                ).ToArray();
        }


        public abstract string SQoLName { get; }
        public abstract string VanillaName { get; }
        public abstract ParentContainerType ParentContainerType { get; }
    }


    public class ProductShelfHighlightData : ContainerHighlightData {
        public override string SQoLName { get; } = "Labels";
        public override string VanillaName { get; } = "";
        public override ParentContainerType ParentContainerType { get; } = ParentContainerType.ProductDisplay;
    }

    public class StorageShelfHighlightData : ContainerHighlightData {
        public override string SQoLName { get; } = "SQoL_StorageBoxHighlights";
        public override string VanillaName { get; } = "Highlights";
        public override ParentContainerType ParentContainerType { get; } = ParentContainerType.Storage;
    }

    public class GroundBoxHighlightData : ContainerHighlightData {
        public override string SQoLName { get; } = $"SQoL_BoxHighlight";
        public override string VanillaName { get; } = "Highlight";
        public override ParentContainerType ParentContainerType { get; } = ParentContainerType.GroundBox;

    }

}
