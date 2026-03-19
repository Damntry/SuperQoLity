using BepInEx.Configuration;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Components;
using Damntry.UtilsUnity.Components.InputManagement;
using Damntry.UtilsUnity.Components.InputManagement.Model;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.ModUtils {

    /// <summary>
    /// Each context has a related Func<bool> method that must return true for the hotkey to be active.
    /// The list is numbered to remind that order matters: Contexts with higher numbers are prioritized over
    /// those with lower numbers, and generally have a more specific activation condition, but that is up to
    /// the needs of the hotkey.
    /// Example: A hotkey meant to be active while the world is loaded, might have less priority than one that 
    /// is only active while the player is inside a specific menu. But at the same time, another hotkey that opens
    /// a generic debug menu might be always active, and still have more priority than all previous ones.
    /// 
    /// The enum values are not saved or serialized in any way, so they can be changed and reordered as needed.
    /// </summary>
    public enum HotkeyActiveContext {
        AlwaysActiveLowPrio = 0,
        WorldLoaded = 1,
        WorldLoadedNotPaused = 5,
        WheelHotkeysEnabled = 6,
        BroomEquipped = 9,
        CanShoot = 10,
        WorldLoadedHighPrio = 50,
        AlwaysActiveHighPrio = 99,
        RadialWheelOpen = 100,
    }

    //TODO 1 - Add a panel on the left side. By default it ll be closed and only showing a tiny
    //  rectangle with a hotkey. That key will expand/contract the panel.
    //  Right now the idea is to show all my mod hotkeys in there, so people get curious about the tiny panel,
    //  presses the hotkey to expand it, and now they know that these things exist.
    //  There will be settings to change the panel expand key, by default F2 or something, and a setting to
    //  always hide the tiny hotkey panel expander.
    //  If the key is empty, the panel wont show and neither can be expanded. If the key is set and the setting to
    //  hide it is enabled, the tiny panel wont show until the hotkey is used, which will expand the panel.

    //If I ever make another mod in this game and I want to use InputManagerSMT, I cant have 2 different
    //  instances of it, since InputDetection can only work with a single one. Either I change InputDetection
    //  to work different, or I move this class into its own assembly and make it a requirement like the Globals.
    public sealed class InputManagerSMT : InputBepInEx {

        public static new InputManagerSMT Instance => _instance ??= new InputManagerSMT();

        private static InputManagerSMT _instance;


        public InputManagerSMT() : 
            base(typeof(InputManagerSMT), OnValidationError.Rollback, restrictAllModifiers: true) { }


        protected override void HotkeyValidationError(string hotkeyName, string message, KeyBind keyBind) {

            base.HotkeyValidationError(hotkeyName, message, keyBind);

            TimeLogger.Logger.SendMessageNotification(LogTier.Warning, message, true);
        }


        private HotkeyContext GetContextFrom(HotkeyActiveContext hotkeyActCtx) =>
            hotkeyActCtx switch {
                HotkeyActiveContext.AlwaysActiveLowPrio => GenerateContext(hotkeyActCtx, null),
                HotkeyActiveContext.WorldLoaded => GenerateContext(hotkeyActCtx, () => WorldState.IsWorldLoaded),
                HotkeyActiveContext.WorldLoadedNotPaused => GenerateContext(hotkeyActCtx, () => 
                    WorldState.IsWorldLoaded && !AuxUtils.IsMainMenuOpen()),
                HotkeyActiveContext.WheelHotkeysEnabled => GenerateContext(hotkeyActCtx, () =>
                    ModConfig.Instance.EnableRadialQuickToolsKeys.Value),
                HotkeyActiveContext.BroomEquipped => GenerateContext(hotkeyActCtx, () =>
                    WorldState.IsWorldLoaded && WeaponManager.IsBroomEquipped()),
                HotkeyActiveContext.CanShoot => GenerateContext(hotkeyActCtx, () => 
                    WeaponManager.IsPlayerInShotgunmode(BroomShotgunNetwork.LocalInstance) && WeaponManager.CanUseWeapon()),
                HotkeyActiveContext.WorldLoadedHighPrio => GenerateContext(hotkeyActCtx, null),
                HotkeyActiveContext.RadialWheelOpen => GenerateContext(hotkeyActCtx, RadialWheelManager.IsRadialShowing),
                HotkeyActiveContext.AlwaysActiveHighPrio => GenerateContext(hotkeyActCtx, () => WorldState.IsWorldLoaded),
                _ => throw new NotImplementedException(hotkeyActCtx.ToString()),
            };


        private HotkeyContext GenerateContext(HotkeyActiveContext hotkeyActCtx, Func<bool> activationCondition) =>
            new (hotkeyActCtx.ToString(), activationCondition, (int)hotkeyActCtx);

        /// <summary>
        /// Adds a new automatically managed hotkey that is bound to the 
        /// value of the BepInEx ConfigEntry passed by parameter.
        /// </summary>
        /// <param name="hotkeyConfig">BepInEx hotkey config object</param>
        /// <param name="inputState">Type of keypress action</param>
        /// <param name="action">Action to execute on keypress</param>
        /// <param name="groupName">Name of the group the hotkey belongs to. Null if its not part of a group.</param>
        public void AddHotkeyFromConfig(ConfigEntry<KeyboardShortcut> hotkeyConfig, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, Action action) {

            base.AddHotkeyFromConfig(hotkeyConfig, inputState, GetContextFrom(hotkeyActCtx), action);
        }

        public void AddHotkeyFromConfig(ConfigEntry<KeyboardShortcut> hotkeyConfig, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, int cooldownMillis, Action action) {

            base.AddHotkeyFromConfig(hotkeyConfig, inputState, GetContextFrom(hotkeyActCtx), cooldownMillis, action);
        }


        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, 
                InputState inputState, HotkeyActiveContext hotkeyActCtx, Action action) {
            return base.TryAddHotkey(hotkeyName, keyCode, modifiers, inputState,
                GetContextFrom(hotkeyActCtx), action);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, InputState inputState,
                HotkeyActiveContext hotkeyActCtx, int cooldownMillis, Action action) {
            return base.TryAddHotkey(hotkeyName, keyCode, modifiers, inputState,
                GetContextFrom(hotkeyActCtx), cooldownMillis, action);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, InputState inputState,
               HotkeyActiveContext hotkeyActCtx, Action action) {
            return base.TryAddHotkey(hotkeyName, keyCode, [], inputState,
                GetContextFrom(hotkeyActCtx), action);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, int cooldownMillis, Action action) {
            return base.TryAddHotkey(hotkeyName, keyCode, [], inputState,
                GetContextFrom(hotkeyActCtx), cooldownMillis, action);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, Action action, string groupName) {
            return base.TryAddHotkey(hotkeyName, keyCode, modifiers, inputState,
               GetContextFrom(hotkeyActCtx), action, groupName);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, int cooldownMillis, Action action, string groupName) {
            return base.TryAddHotkey(hotkeyName, keyCode, modifiers, inputState,
                GetContextFrom(hotkeyActCtx), cooldownMillis, action, groupName);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, Action action, string groupName) {
            return base.TryAddHotkey(hotkeyName, keyCode, [], inputState,
                GetContextFrom(hotkeyActCtx), action, groupName);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, InputState inputState, 
                HotkeyActiveContext hotkeyActCtx, int cooldownMillis, Action action, string groupName) {
            return base.TryAddHotkey(hotkeyName, keyCode, [], inputState, GetContextFrom(hotkeyActCtx),
                cooldownMillis, action, groupName);
        }

    }
}
