using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel {

    public static class ToolWheelDefinitions {

        private readonly static string baseAssetsPath = "assets\\RadialWheel\\Equipment\\";

        private readonly static Lazy<Dictionary<ToolIndexes, ToolWheelDefinition>> toolIndexes = new(GenerateToolIndexDictionary);


        private static Dictionary<ToolIndexes, ToolWheelDefinition> GenerateToolIndexDictionary() {
            Dictionary<ToolIndexes, ToolWheelDefinition> dict = new();

            //Find all fields and properties declared in the deriving class
            BindingFlags SearchFlags = BindingFlags.DeclaredOnly |
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var members = typeof(ToolWheelDefinitions).GetProperties(SearchFlags);

            foreach (var member in members) {
                if (member.PropertyType != typeof(ToolWheelDefinition)) {
                    continue;
                }
                ToolWheelDefinition tool = (ToolWheelDefinition)member.GetValue(null);
                dict.Add(tool.Index, tool);
            }

            return dict;
        }


        public static ToolWheelDefinition FromIndex(int index) {
            return toolIndexes.Value[(ToolIndexes)index];
        }

        public static List<ToolWheelDefinition> GetAllToolDefinitions() {
            return toolIndexes.Value.Values.ToList();
        }

        public static IEnumerable<ToolWheelDefinition> GetAllSpawnableToolDefinitions() {
            return toolIndexes.Value.Values.Where(t => t.IsRadialSpawnable);
        }

        public static string GetDefaultDisplayControlString() =>
            string.Join(",",
                GetAllSpawnableToolDefinitions().Select(t => t.DisplayName.Replace(" ", "")).ToArray()
            );


        private static void GenericSpawnMethod(NetworkSpawner netSpawner,
                PlayerNetwork pNetwork, Vector3 dropPosition, float xRotation, float zRotation) {

            netSpawner.CmdSpawnProp(pNetwork.equippedItem, dropPosition,
                new(xRotation, pNetwork.transform.eulerAngles.y, zRotation));
        }

        private static void GenericSpawnZeroPosMethod(NetworkSpawner netSpawner, PlayerNetwork pNetwork) {
            netSpawner.CmdSpawnProp(pNetwork.equippedItem, Vector3.zero, Vector3.zero);
        }

        #region Definitions

        //All these definitions below are referenced by reflection in GenerateToolIndexDictionary.
        //Adding a new definition will automatically add it to the equipment wheel.

        public static ToolWheelDefinition Nothing { get; } =
            new(index: ToolIndexes.Nothing,
                isRadialSpawnable: false,
                requiredPermission: PlayerPermissionsEnum.None,
                toolGameObjects: null,
                iconUnityPath: null,
                cmdSpawnMethod: null);
        public static ToolWheelDefinition Box { get; } =
            new(index: ToolIndexes.Box,
                isRadialSpawnable: false,
                requiredPermission: PlayerPermissionsEnum.Restocker,
                toolGameObjects: null,
                iconUnityPath: null,
                cmdSpawnMethod: (net, pNet, dropPos) => {
                    ManagerBlackboard managerBB = GameData.Instance.GetComponent<ManagerBlackboard>();
                    managerBB.CmdSpawnBoxFromPlayer(dropPos,
                        productID: pNet.extraParameter1,
                        numberOfProductsInBox: pNet.extraParameter2,
                        pNet.transform.eulerAngles.y);
                });
        public static ToolWheelDefinition PricingScanner { get; } =
            new(index: ToolIndexes.PricingScanner,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("2_PricingScanner", "Organizer_PriceScanner"),
                iconUnityPath: baseAssetsPath + "UI_OrganizerPriceScanner.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 0, 90));
        public static ToolWheelDefinition Broom { get; } =
            new(index: ToolIndexes.Broom,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Security,
                toolGameObjects: new("3_Broom", "Organizer_Brooms"),
                iconUnityPath: baseAssetsPath + "UI_OrganizerBroom.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 270, 0));
        [Obsolete($"Replaced with {nameof(PaintablesTablet)}")]
        public static ToolWheelDefinition DecoTablet { get; } =
            new(index: ToolIndexes.DecoTablet,
                isRadialSpawnable: false,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: null,
                iconUnityPath: null,
                cmdSpawnMethod: null);
        /// <summary>DLC Only</summary>
        public static ToolWheelDefinition PaintablesTablet { get; } =
            new(index: ToolIndexes.PaintablesTablet,
                isRadialSpawnable: AuxUtils.IsDLCSubscribed(),
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("5_PaintablesTablet", null),
                iconUnityPath: baseAssetsPath + "UI_PaintPalette.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 270, 0));
        public static ToolWheelDefinition OrderingDevice { get; } =
            new(index: ToolIndexes.OrderingDevice,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("6_OrderingDevice", "Organizer_OrderingTablet"),
                iconUnityPath: baseAssetsPath + "UI_OrganizerOrderTablet.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 180, 0));
        public static ToolWheelDefinition SledgeHammer { get; } =
            new(index: ToolIndexes.SledgeHammer,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("7_Sledgehammer", "Organizer_Sledgehammer"),
                iconUnityPath: baseAssetsPath + "UI_OrganizerSledge.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 0, 0));
        public static ToolWheelDefinition Ladder { get; } =
            new(index: ToolIndexes.Ladder,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("8_Ladder", null),
                iconUnityPath: baseAssetsPath + "UI_Ladder.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 15, 0));
        public static ToolWheelDefinition BlueTray { get; } =
            new(index: ToolIndexes.BlueTray,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.None,
                toolGameObjects: new("9_OrderingTray", "Organizer_Trays"),
                iconUnityPath: baseAssetsPath + "UI_Tray.png",
                cmdSpawnMethod: (net, pNet, dropPos) => {
                    net.CmdSpawnTrayFromPlayer(dropPos, pNet.trayData, pNet.transform.eulerAngles.y);
                });
        public static ToolWheelDefinition SalesDevice { get; } =
            new(index: ToolIndexes.SalesDevice,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("10_SalesDevice", "Organizer_SalesTablet"),
                iconUnityPath: baseAssetsPath + "UI_OrganizerSales.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 180, 0));
        public static ToolWheelDefinition CardboardBale { get; } =
            new(index: ToolIndexes.CardboardBale,
                isRadialSpawnable: false,
                requiredPermission: PlayerPermissionsEnum.None,
                toolGameObjects: null,
                iconUnityPath: null,
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 0, 0));
        public static ToolWheelDefinition OrderBox { get; } =
            new(index: ToolIndexes.OrderBox,
                isRadialSpawnable: false,
                requiredPermission: PlayerPermissionsEnum.None,
                toolGameObjects: null,
                iconUnityPath: null,
                cmdSpawnMethod: (net, pNet, dropPos) => {
                    net.CmdSpawnOrderBoxFromPlayer(dropPos, pNet.transform.eulerAngles.y,
                        pNet.orderNumberData, pNet.orderCustomerNameData,
                        pNet.orderItemsInBoxData);
                });
        /// <summary>From past events</summary>
        public static ToolWheelDefinition NetCatcher { get; } =
            new(index: ToolIndexes.NetCatcher,
                isRadialSpawnable: false,
                requiredPermission: PlayerPermissionsEnum.None,
                toolGameObjects: null,
                iconUnityPath: null,
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 0, 0));
        public static ToolWheelDefinition Toolbox { get; } =
            new(index: ToolIndexes.Toolbox,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("14_Toolbox", "Organizer_Toolbox"),
                iconUnityPath: baseAssetsPath + "UI_Toolbox.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 0, 0));
        public static ToolWheelDefinition Extinguisher { get; } =
            new(index: ToolIndexes.Extinguisher,
                isRadialSpawnable: true,
                requiredPermission: PlayerPermissionsEnum.Manager,
                toolGameObjects: new("15_Extinguisher", "FireExtinguisher"),
                iconUnityPath: baseAssetsPath + "UI_Extinguisher.png",
                cmdSpawnMethod: (net, pNet, dropPos) => GenericSpawnMethod(net, pNet, dropPos, 0, 0));

        #endregion
    }
}
