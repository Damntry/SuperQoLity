using System;
using BepInEx;
using BepInEx.Configuration;
using Damntry.UtilsBepInEx.ConfigurationManager;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using SuperQoLity.SuperMarket.Patches;
using SuperQoLity.SuperMarket.Patches.BetterSMT;
using UnityEngine;
using static SuperQoLity.SuperMarket.ModUtils.BetterSMT_Helper;

namespace SuperQoLity.SuperMarket.ModUtils {

	public class ModConfig {

		public static ModConfig Instance {
			get {
				return instance ??= new ModConfig();
			}
		}

		private static ModConfig instance;
		private ModConfig() { }

		private BaseUnityPlugin basePlugin;

		private ConfigManagerController configManagerControl;

		public ConfigEntry<bool> EnableEmployeeChanges { get; private set; }
		public ConfigEntry<float> EmployeeNextActionWait { get; private set; }
		public ConfigEntry<float> EmployeeIdleWait { get; private set; }
		public ConfigEntry<bool> EnableTransferProducts { get; private set; }
		public ConfigEntry<int> NumTransferProducts { get; private set; }
		public ConfigEntry<bool> TransferMoreProductsOnlyClosedStore { get; private set; }
		public ConfigEntry<bool> EnablePatchBetterSMT_General { get; private set; }
		public ConfigEntry<bool> EnablePatchBetterSMT_ExtraHighlightFunctions { get; private set; }
		public ConfigEntry<Color> PatchBetterSMT_ShelfHighlightColor { get; private set; }
		public ConfigEntry<Color> PatchBetterSMT_ShelfLabelHighlightColor { get; private set; }
		public ConfigEntry<Color> PatchBetterSMT_StorageHighlightColor { get; private set; }
		public ConfigEntry<Color> PatchBetterSMT_StorageSlotHighlightColor { get; private set; }
		public ConfigEntry<bool> EnableModNotifications { get; private set; }


		private const string RequiresRestartSymbol = "(**)";


		public void InitializeConfig(BaseUnityPlugin basePlugin) {
			this.basePlugin = basePlugin;

			configManagerControl = new ConfigManagerController(basePlugin.Config);

			configManagerControl.AddGUIHiddenNote(
				sectionName: "!!↓↓ IMPORTANT NOTE ABOUT CONFIG FILE EDITING ↓↓!!",
				key:		"",
				description: "If you can see this, you are editing the .cfg file, or using a config manager that doesnt have support for hiding settings. " +
								"If its the latter case, please let me know and I ll try to check it out.\n" +
								"In any case, the way I handle settings was intended to work with the plugin \"Bepinex.ConfigurationManager\" in mind, and I highly recommend using it " +
								"as it allows you to change values on the fly, show settings in its proper order, color preview, and some extra " +
								"features like hiding settings of disabled modules.\n" +
								"Otherwise, editing the config file works fine, but it might be more confusing since there could be settings that do nothing.\n" +
								"You can download the latest \"Bepinex.ConfigurationManager\" BepInEx5 version (BepInEx5_v18.3 as of writing this) from its GitHub Releases page.");
			//About the above comment of showing settings in its proper order in the config file itself:
			//	Prefixing numbers in sections and settings fixes this, and to this day its the only way of doing it.
			//	https://github.com/BepInEx/BepInEx.ConfigurationManager/issues/22#issuecomment-807431539
			//	Its not an option for me since changing setting order in the future would make them loose its saved value and I dont think its worth it.
			//	BepInEx would need to add support for a different way of setting configs order in the future, preferably
			//	reading the Category tag value as ConfigManager already does.

			configManagerControl.AddSectionNote(
				sectionText: $"* Settings with {RequiresRestartSymbol} require a restart to apply changes. Hides related settings when disabled. *",
				description: "This setting is purely informative and doesnt have any effect.");

			EnableEmployeeChanges = configManagerControl.AddConfig(
				sectionName: "Employee Job Module",
				key: $"Enable Employee Job Module {RequiresRestartSymbol}",
				defaultValue: true,
				description: "Enable patching of employee methods to allow related settings to show up. Disable if the module seems to cause problems.");

			EmployeeNextActionWait = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: "Employee Job Module",
				key: "Wait time after a job step is finished",
				defaultValue: 1.5f,
				description: "Adjust the amount of time an employee waits after it finishes a single step of a job, " +
								"like picking up a box or filling up a product shelf.",
				acceptableValueRange: new AcceptableValueRange<float>(0.1f, 4f),
				patchInstanceDependency: Container<NPCTargetAssignmentPatch>.Instance);

			EmployeeIdleWait = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: "Employee Job Module",
				key: "Job check frequency while idle",
				defaultValue: 2f,
				description: "Adjusts the frequency of employees checking for available jobs while idling.\n" +
								"Lowering this value too much might cause performance issues on low end rigs if you have many idling employees.",
				acceptableValueRange: new AcceptableValueRange<float>(0.25f, 10f),
				patchInstanceDependency: Container<NPCTargetAssignmentPatch>.Instance);

			configManagerControl.AddQuasiNote(
				sectionName: "Item Transfer Speed Module",
				key: "IMPORTANT NOTE:",
				textboxMessage: "HIGH LATENCY MODE NOT SUPPORTED!",
				description: "This is just a note and not a setting.\nDont look at it too much.\nExcept to understand " +
								"how important this note can be, because it is.\nAnd it is very important, so look at it now.\n" +
								"Did you look at it?\nGood good.\nStop wasting time and keep going.");

			EnableTransferProducts = configManagerControl.AddConfig(
				sectionName: "Item Transfer Speed Module",
				key: $"Enable Item Transfer Speed Module {RequiresRestartSymbol}",
				defaultValue: true,
				description: "Enable patching of item transfer methods to allow related settings to show up. Disable if the module seems to cause problems.");

			NumTransferProducts = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: "Item Transfer Speed Module",
				key: "Number of items to transfer to/from product shelves each action",
				defaultValue: 1,
				description: "Adjusts the amount of time an employee waits after it finishes a single job, like " +
								"delivering a box or filling up a product shelf.",
				acceptableValueRange: new AcceptableValueRange<int>(1, 50),
				patchInstanceDependency: Container<IncreasedItemTransferPatch>.Instance);

			TransferMoreProductsOnlyClosedStore = configManagerControl.AddConfig(
				sectionName: "Item Transfer Speed Module",
				key: "Only while store is closed",
				defaultValue: true,
				description: "If enabled, extra item transfering only works while the supermarket is closed.",
				patchInstanceDependency: Container<IncreasedItemTransferPatch>.Instance);


			EnablePatchBetterSMT_General = configManagerControl.AddConfig(
				sectionName: "BetterSMT Patches",
				key: GetBetterSMTConfigMessage(true),
				defaultValue: true,
				description: "If enabled, patches related to the mod BetterSMT that dont have their own config entry, will be applied.",
				disabled: BetterSMTLoadStatus.Value == BetterSMT_Status.NotLoaded);

			//The way I implement this extra highlight function, is overriding all of BetterSMT
			//	highlighting logic, so highlighting doesnt even need BetterSMT to work.
			//	(EDIT- Actually not completely true. I still use BetterSMT HighlightShelf(), but
			//	only because I didnt want to discard his work because of a very small change)
			//	But since the base highlighting was not my idea, I disable it if BetterSMT is not loaded.
			//	If in the future BetterSMT is completely abandoned and Im still around, I can separate
			//	this setting into its own section without having BetterSMT as a requirement, and
			//	change the IsAutoPatchEnabled condition and the ShortErrorMessageOnAutoPatchFail text
			//	in HighlightStorageSlotsPatch.
			EnablePatchBetterSMT_ExtraHighlightFunctions = configManagerControl.AddConfig(
				sectionName: "BetterSMT Patches",
				key: GetBetterSMTConfigMessage(false),
				defaultValue: true,
				description: "If enabled, in addition to BetterSMT highlighting the storage shelf itself, individual storage slots that " +
				"have that product assigned will also be highlighted, empty or not.",
				disabled: BetterSMTLoadStatus.Value == BetterSMT_Status.NotLoaded);


			string colorSettingHelpMessage = $"\nRequires starting the game with the setting \"{EnablePatchBetterSMT_ExtraHighlightFunctions.Definition.Key}\" enabled.\n\n" +
				"The format has an Hexadecimal RGB notation with an added alpha channel (RRGGBBAA)\n\n" +
				"It is recommended to use the mod \"BepInEx.ConfigurationManager\" to make choosing a color more convenient.";

			PatchBetterSMT_ShelfHighlightColor = configManagerControl.AddConfig(
				sectionName: "BetterSMT Patches: Highlight Colors",
				key: "Shelf Highlight Color",
				defaultValue: Color.red,
				description: "Color of product shelves when highlighted." + colorSettingHelpMessage,
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance);

			PatchBetterSMT_ShelfLabelHighlightColor = configManagerControl.AddConfig(
				sectionName: "BetterSMT Patches: Highlight Colors",
				key: "Shelf label Highlight Color",
				defaultValue: Color.yellow,
				description: "Color of shelf labels when highlighted." + colorSettingHelpMessage,
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance);

			PatchBetterSMT_StorageHighlightColor = configManagerControl.AddConfig(
				sectionName: "BetterSMT Patches: Highlight Colors",
				key: "Storage Highlight Color",
				defaultValue: Color.blue,
				description: "Color of storage shelves when highlighted." + colorSettingHelpMessage,
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance);

			PatchBetterSMT_StorageSlotHighlightColor = configManagerControl.AddConfig(
				sectionName: "BetterSMT Patches: Highlight Colors",
				key: "Storage Slot Highlight Color",
				defaultValue: Color.cyan,
				description: "Color of storage slot spaces when highlighted." + colorSettingHelpMessage,
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance);


			EnableModNotifications = configManagerControl.AddConfig(
				sectionName: "Notifications Module",
				key: $"Enable custom mod notifications {RequiresRestartSymbol}",
				defaultValue: true,
				description: "If enabled, you may receive custom notifications from this mod. Disable if they are " +
								"causing problems or you dont want to see them.");
		}
		

		public void NotificationsSettingsChanged(object sender, EventArgs e) {
			if (EnableModNotifications.Value) {
				GameNotifications.Instance.AddNotificationSupport();
			} else {
				GameNotifications.Instance.RemoveNotificationSupport();
			}
		}


		private string GetBetterSMTConfigMessage(bool isMainModuleSetting) {
			string loadedText = isMainModuleSetting ? $"Apply {BetterSMTInfo.Name} patches {RequiresRestartSymbol}" : $"Highlight Storage Slots {RequiresRestartSymbol}";

			return BetterSMTLoadStatus.Value switch {
				BetterSMT_Status.NotLoaded => $"{BetterSMTInfo.Name} is not loaded. Setting unused.",
				BetterSMT_Status.DifferentVersion or BetterSMT_Status.LoadedOk => loadedText,
				_ => throw new NotImplementedException($"The switch case {BetterSMTLoadStatus.Value} is not implemented."),
			};
		}

	}

}

