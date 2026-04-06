using Cysharp.Threading.Tasks;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsMirror.SyncVar;
using Damntry.UtilsUnity.Components.InputManagement;
using Rito.RadialMenu_v3;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel.Model;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel {

    public enum QuickToolKeyModifiers {
        [Description("None")]
        None = KeyCode.None,
        [Description("Left Control")]
        LeftControl = KeyCode.LeftControl,
        [Description("Right Control")]
        RightControl = KeyCode.RightControl,
        [Description("Left Shift")]
        LeftShift = KeyCode.LeftShift,
        [Description("Right Shift")]
        RightShift = KeyCode.RightShift,
        [Description("Left Alt")]
        LeftAlt = KeyCode.LeftAlt,
        [Description("Right Alt")]
        RightAlt = KeyCode.RightAlt,
    }

    public static class RadialWheelManager {

        //While the radial is open, there is a pretty big stutter every 1 second.
        //	Nothing to do with the radial, happens in vanilla while the cursor is showing, even in the main menu.
        //  Reducing mouse polling fixes it. I havent found a proper fix for this but I ll leave this here for now.
        //  ** Turns out this was a DisplayFusion bug that got fixed in 12.0.

        private const string hotkeyGroupName = "radialHotkeys";
        private const string hotkeyQuickGroupName = "RadialQuickKeys";

        private static RadialRefs radialInstances;

        private static ToolActionStatus toolActionStatus;

        public static bool BlockCameraMovement { get; private set; }



        public static bool IsRadialShowing() => radialInstances.RadialMenu.IsRadialShowing();


        public static void Initialize() {
            toolActionStatus = new();

            //Wait for the "radial enabled" setting to be network ready.
            RadialWheelNetwork.RadialEnabledSync.OnFinishSyncing += () => {
                if (!RadialWheelNetwork.RadialEnabledSync.IsEnabledLocally) {
                    return;
                }

                //Depending on multiplayer side, either CanvasAwake or OnFinishSyncing triggers first.
                //TODO 2 - Actually, I think the problem I have here is that OnFinishSyncing is called way too early and its not really 
                //  synced, just at the point where it gets the local value and thats it. I though I tested it and it worked wtf.
                if (WorldState.IsGameWorldAtOrAfter(GameWorldEvent.CanvasAwake)) {
                    InitializeRadialWheel();
                } else {
                    WorldState.SubscribeToWorldStateEvent(GameWorldEvent.CanvasAwake, InitializeRadialWheel);
                }

                ModConfig.Instance.EnableRadialQuickToolsKeys.SettingChanged += UpdateQuickTools;
                ModConfig.Instance.RadialQuickToolsModifierKey.SettingChanged += UpdateQuickTools;
            };

            //Open wheel hotkey. Always enabled even if the wheel is not active, so we can tell the
            //  player why it doesnt work as a client when the host does not have it.
            InputManagerSMT.Instance.AddHotkeyFromConfig(ModConfig.Instance.RadialEquipmentWheelHotkey,
                InputState.KeyDown, HotkeyActiveContext.WorldLoadedNotPaused, RadialOpen);
            InputManagerSMT.Instance.AddHotkeyFromConfig(ModConfig.Instance.RadialEquipmentWheelHotkey,
                InputState.KeyUp, HotkeyActiveContext.WorldLoadedNotPaused, RadialCloseNormal);
        }


        private static void InitializeRadialWheel() {
            WorldState.UnsubscribeFromWorldStateEvent(GameWorldEvent.CanvasAwake, InitializeRadialWheel);

            radialInstances = RadialWheelInitialization.LoadEquipmentWheel(out Dictionary<int, WheelKeyBind> quickKeysData);

            //Hotkeys for each wheel tool
            SetWheelQuickKeys(quickKeysData);

            //If a valid option is selected, closes the radial and selects the option. Otherwise does nothing.
            InputManagerSMT.Instance.TryAddHotkey("RadialSelect", KeyCode.Mouse0,
                InputState.KeyDown, HotkeyActiveContext.RadialWheelOpen,
                RadialCloseIfSelection, hotkeyGroupName);
            InputManagerSMT.Instance.TryAddHotkey("RadialCancelEsc", KeyCode.Escape,
                InputState.KeyDown, HotkeyActiveContext.RadialWheelOpen, RadialCancelEsc, hotkeyGroupName);
            InputManagerSMT.Instance.TryAddHotkey("RadialCancelMouse", KeyCode.Mouse1,
                InputState.KeyDown, HotkeyActiveContext.RadialWheelOpen, RadialCancel, hotkeyGroupName);
                        
            radialInstances.RadialMenu.OnDestroyEvent += OnDestroy;
        }

        private static void SetWheelQuickKeys(Dictionary<int, WheelKeyBind> quickKeysData) {
            if (quickKeysData != null) {
                InputManagerSMT.Instance.RemoveHotkeyGroup(hotkeyQuickGroupName);

                foreach (var quickKey in quickKeysData) {
                    InputManagerSMT.Instance.TryAddHotkey($"RadialItemKey{quickKey.Key + 1}",
                        quickKey.Value.KeyCode, quickKey.Value.Modifiers, InputState.KeyDown,
                        HotkeyActiveContext.WheelHotkeysEnabled, () => HotkeyItemSelection(quickKey.Key),
                        hotkeyQuickGroupName);
                }
            } else {
                //Remove quick hotkeys, if any
                InputManagerSMT.Instance.RemoveHotkeyGroup(hotkeyQuickGroupName);
            }
        }

        private static void HotkeyItemSelection(int indexSelection) {
            ToolSelected(indexSelection).FireAndForget(LogCategories.UI);
        }

        public static void OnDestroy() {
            InputManagerSMT.Instance.RemoveHotkeyGroup(hotkeyGroupName);
            InputManagerSMT.Instance.RemoveHotkeyGroup(hotkeyQuickGroupName);

            ModConfig.Instance.EnableRadialQuickToolsKeys.SettingChanged -= UpdateQuickTools;
            ModConfig.Instance.RadialQuickToolsModifierKey.SettingChanged -= UpdateQuickTools;

            radialInstances = null;
        }

        private static void UpdateQuickTools(object sender, EventArgs e) {
            RadialMenu.UpdateQuickTools(
                ModConfig.Instance.EnableRadialQuickToolsKeys.Value,
                [(KeyCode)ModConfig.Instance.RadialQuickToolsModifierKey.Value],
                out Dictionary<int, WheelKeyBind> quickKeysData
            );

            if (quickKeysData != null) {
                SetWheelQuickKeys(quickKeysData);
            }
        }


        public static void RadialOpen() {
            if (!WorldState.IsWorldLoaded || !RadialWheelNetwork.RadialEnabledSync.IsEnabledLocally ||
                    //Dont show while there is some other ongoing input removing action.
                    !FirstPersonController.Instance.allowPlayerInput) {
                return;
            }

            if (WorldState.IsClient) {
                if (RadialWheelNetwork.RadialEnabledSync.Status == EnableStatus.LocallyOnly) {
                    TimeLogger.Logger.SendMessageNotification(LogTier.Message,
                        $"Equipment Wheel is disabled. Host does not have SuperQoLity or this feature enabled", skipQueue: true);
                    return;
                } else if (radialInstances == null) {
                    TimeLogger.Logger.SendMessageNotification(LogTier.Message,
                        $"Equipment Wheel is disabled. Host needs this feature enabled while you join", skipQueue: true);
                    return;
                }
            }

            //Sometimes, the radial wont show if you try to open it too fast after closing.
            //Needs to be done quick enough that it doesnt really matter if it doesnt show,
            //  but I need to make sure it worked, to change the mouse status.
            if (radialInstances.RadialMenu.Show()) {
                //Show cursor and confine it within the limits of the game window
                ChangeWheelControlStatus(isOpenWheelAction: true, skipCursorChanges: false);
            }
        }

        public static void RadialCloseNormal() {
            RadialClose(withSelection: true, skipCursorChanges: false);
        }

        public static void RadialCloseIfSelection() {
            if (!radialInstances.RadialMenu.IsPieceSelected()) {
                return;
            }

            RadialClose(withSelection: true, skipCursorChanges: false);
        }

        private static void RadialCancel() {
            RadialClose(withSelection: false, skipCursorChanges: false);
        }

        private static void RadialCancelEsc() {
            RadialClose(withSelection: false, skipCursorChanges: true);
        }


        private async static void RadialClose(bool withSelection, bool skipCursorChanges) {
            CustomCameraController camControl = SMTInstances.GetCustomCameraController();
            if (!WorldState.IsWorldLoaded || radialInstances == null || !radialInstances.RadialMenu.isActiveAndEnabled) {
                return;
            } else if (camControl && (camControl.inVehicle || camControl.isInCameraEvent)) {
                //Make sure its hidden just in case something got stuck.
                radialInstances.RadialMenu.Hide();
                return;
            }

            try {
                int selection = radialInstances.RadialMenu.Hide();
                if (withSelection && selection >= 0) {
                    await ToolSelected(selection);
                }
            } catch (Exception ex) {
                if (radialInstances.RadialObj) {
                    radialInstances.RadialObj.SetActive(false);
                }

                TimeLogger.Logger.LogExceptionWithMessage($"Radial wheel close error:", ex, LogCategories.UI);
            } finally {
                //Give full control back to the player
                ChangeWheelControlStatus(isOpenWheelAction: false, skipCursorChanges);
            }
        }


        private static void ChangeWheelControlStatus(bool isOpenWheelAction, bool skipCursorChanges) {
            if (!skipCursorChanges) {
                Cursor.lockState = isOpenWheelAction ? CursorLockMode.Confined : CursorLockMode.Locked;
                Cursor.visible = isOpenWheelAction;
            }

            BlockCameraMovement = isOpenWheelAction;
        }


        private async static Task ToolSelected(int selectedIndex) {
            if (toolActionStatus.PendingAction != PendingToolAction.None) {
                TimeLogger.Logger.SendMessageNotification(LogTier.Warning, "Tool equip process still pending.", true);

                return;
            }

            PlayerNetwork pNetwork = SMTInstances.LocalPlayerNetwork();

            int requestedToolIndex = radialInstances.IndexMapping[selectedIndex];
            int equippedIndex = pNetwork.equippedItem;

            if (requestedToolIndex != equippedIndex) {
                if (!EquipmentManager.ExistsUnusedToolOfIndex(pNetwork, 
                        requestedToolIndex, out Func<bool> toolRequestMethod)) {
                    return;
                }

                bool canRequestTool = true;
                bool itemDropped = false;

                if (equippedIndex > 0) {
                    if (EquipmentManager.CanDropItemInFront(pNetwork, out Vector3 dropPosition, 
                            out ToolWheelDefinition equippedTool)) {

                        if (EquipmentManager.DropCurrentEquippedItem(pNetwork, dropPosition, equippedTool)) {
                            itemDropped = true;
                        } else {
                            canRequestTool = false;
                        }
                    } else {
                        canRequestTool = false;
                    }
                }

                if (canRequestTool) {
                    if (itemDropped) {
                        //Delay requesting the tool until the drop action has finished
                        toolActionStatus.PendingAction = PendingToolAction.Drop;
                        toolActionStatus.ChangeEquipmentFunc = async () => {
                            await UniTask.DelayFrame(1);
                            await RequestAndChangeEquipment(pNetwork, requestedToolIndex, toolRequestMethod);
                        };
                    } else {
                        await RequestAndChangeEquipment(pNetwork, requestedToolIndex, toolRequestMethod);
                    }
                }
            }
        }

        private async static Task RequestAndChangeEquipment(PlayerNetwork pNetwork, 
                int requestedToolIndex, Func<bool> toolRequestMethod) {

            //Request (destroy) tool from the floor/organizer
            if (toolRequestMethod()) {
                //Wait for tool request to propagate
                await UniTask.DelayFrame(1);

                toolActionStatus.PendingAction = PendingToolAction.equip;
                //Spawn the tool in the player hands
                pNetwork.CmdChangeEquippedItem(requestedToolIndex);
            } else {
                toolActionStatus.PendingAction = PendingToolAction.None;
            }
        }

        public async static void OnChangedEquipment(PlayerNetwork __instance, Transform previousEquipped,
                Transform newEquipped, int previousIndex, int newIndex, bool isLocalPlayer) {

            if (toolActionStatus.PendingAction == PendingToolAction.Drop && isLocalPlayer) {
                await toolActionStatus.ChangeEquipmentFunc();
            } else if (toolActionStatus.PendingAction == PendingToolAction.equip) {
                //All tool actions finished, now it can request another tool.
                toolActionStatus.PendingAction = PendingToolAction.None;
                
            }
        }


        private enum PendingToolAction {
            None,
            Drop,
            equip
        }

        private class ToolActionStatus() {
            public PendingToolAction PendingAction { get; set; }
            public Func<Task> ChangeEquipmentFunc { get; set; }
        }

    }
}
