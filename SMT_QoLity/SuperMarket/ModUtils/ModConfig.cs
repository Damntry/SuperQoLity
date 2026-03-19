using BepInEx;
using BepInEx.Configuration;
using Damntry.Utils.ExtensionMethods;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.Search;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.AutoMode.DataDefinition;
using SuperQoLity.SuperMarket.Patches.BroomShotgun;
using SuperQoLity.SuperMarket.Patches.Building;
using SuperQoLity.SuperMarket.Patches.EquipmentWheel;
using SuperQoLity.SuperMarket.Patches.Highlighting;
using SuperQoLity.SuperMarket.Patches.Misc;
using SuperQoLity.SuperMarket.Patches.NPC;
using SuperQoLity.SuperMarket.Patches.NPC.Customer;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.ModUtils {

	public class ModConfig {

		public static ModConfig Instance {
			get {
				return instance ??= new ModConfig();
			}
		}

		private static ModConfig instance;
		private ModConfig() { }


		private ConfigManagerController configManagerControl;

        //*** EMPLOYEES ***
        public ConfigEntry<bool> EnableEmployeeChanges { get; private set; }
		public ConfigEntry<float> ClosedStoreEmployeeWalkSpeedMultiplier { get; private set; }
        public ConfigEntry<bool> ClosedStoreEmployeeItemTransferMaxed { get; private set; }
        public ConfigEntry<float> EmployeeNextActionWait { get; private set; }
		public ConfigEntry<EnumSecurityPickUp> ImprovedSecurityPickUpMode { get; private set; }
		public ConfigEntry<EnumSecurityEmployeeThiefChase> SecurityThiefChaseMode { get; private set; }
        public ConfigEntry<EnumFreeStoragePriority> FreeStoragePriority { get; private set; }

        //*** CUSTOMERS ***
        public ConfigEntry<bool> EnableCustomerChanges { get; private set; }
		public ConfigEntry<bool> EnableCustomerStuckDetection { get; private set; }
        public ConfigEntry<bool> EnableShopListOnlyAssignedProducts { get; private set; }
        public ConfigEntry<int> CustomerCardPayRatio { get; private set; }

        //*** NPC PERFORMANCE ***
        public ConfigEntry<EnumJobFrequencyMultMode> NpcJobFrequencyMode { get; private set; }
        public ConfigEntry<float> EmployeeIdleWait { get; private set; }

        //*** ITEM TRANSFER ***
        public ConfigEntry<bool> EnableTransferProducts { get; private set; }
		public ConfigEntry<EnumItemTransferMode> ItemTransferMode { get; private set; }
		public ConfigEntry<int> NumTransferProducts { get; private set; }

        //*** HIGHLIGHT ***
        public ConfigEntry<bool> EnablePatchHighlight { get; private set; }
        public ConfigEntry<HighlightMode> HighlightVisualMode { get; private set; }
        public ConfigEntry<KeyboardShortcut> HotkeyCycleHighlightMode { get; private set; }
        public ConfigEntry<KeyboardShortcut> HotkeyToggleAimedHighlight { get; private set; }
        public ConfigEntry<Color> ShelfHighlightColorRGBA { get; private set; }
		public ConfigEntry<Color> ShelfLabelHighlightColorRGBA { get; private set; }
		public ConfigEntry<Color> StorageHighlightColorRGBA { get; private set; }
		public ConfigEntry<Color> StorageSlotHighlightColorRGBA { get; private set; }

        //*** EQUIPMENT WHEEL ***
        public ConfigEntry<bool> EnableRadialWheelPatches { get; private set; }
        public ConfigEntry<KeyboardShortcut> RadialEquipmentWheelHotkey { get; private set; }
        public ConfigEntry<bool> EnableRadialQuickToolsKeys { get; private set; }
        public ConfigEntry<QuickToolKeyModifiers> RadialQuickToolsModifierKey { get; private set; }
        public ConfigEntry<string> RadialDisplayControl { get; private set; }

        //*** BROOM SHOTGUN ***
        public ConfigEntry<bool> BroomShotgunModeEnabled { get; private set; }
		public ConfigEntry<KeyboardShortcut> BroomShotgunModeHotkey { get; private set; }
        public ConfigEntry<int> MaxShotgunSmokeParticles { get; private set; }

        //*** BUILDABLES ***
        public ConfigEntry<bool> EnableBuildPatches { get; private set; }
        public ConfigEntry<KeyboardShortcut> CloneBuildHotkey { get; private set; }
        public ConfigEntry<KeyboardShortcut> MoveBuildHotkey { get; private set; }
        public ConfigEntry<int> CloneMoveTargetDistance { get; private set; }
        public ConfigEntry<bool> EnableIncreasedPropLoadLimits { get; private set; }

        //*** MISCELLANEOUS ***
        public ConfigEntry<bool> EnableMiscPatches { get; private set; }
        public ConfigEntry<bool> EnableCheckoutAutoClicker { get; private set; }
        public ConfigEntry<bool> EnablePriceGunFix { get; private set; }
		public ConfigEntry<bool> EnableExpandedProdOrderClickArea { get; private set; }

        //*** NOTIFICATIONS ***
        public ConfigEntry<bool> EnableModNotifications { get; private set; }
		public ConfigEntry<bool> EnableWelcomeMessages { get; private set; }
		public ConfigEntry<bool> EnableErrorMessages { get; private set; }

        //*** WORKLOAD DISPLAY ***
        public ConfigEntry<bool> DisplayAutoModeFrequencyMult { get; private set; }

        //*** CUSTOMIZED PERFORMANCE ***
        public ConfigEntry<float> CustomEmployeeWaitTarget { get; private set; }
        public ConfigEntry<float> CustomCustomerWaitTarget { get; private set; }
        public ConfigEntry<float> CustomMinimumFrequencyMult { get; private set; }
		public ConfigEntry<float> CustomMaximumFrequencyMult { get; private set; }
		public ConfigEntry<float> CustomMaximumFrequencyReduction { get; private set; }
		public ConfigEntry<float> CustomMaximumFrequencyIncrease { get; private set; }

        //*** DEV STUFF ***
        public ConfigEntry<bool> EnabledDevMode { get; private set; }
		public ConfigEntry<float> TeleportSoundVolume { get; private set; }


		private const string RequiresRestartSymbol = "(**)";
        private const string RequiresReloadSymbol = "(*)";

        /* Depending on the modInstallSide of each setting, it would need to:
		
			HostSideOnly:			If the functionality could be executed by a client in the vanilla game, make sure now it doesnt.
			HostAndSoftClient:		If only the client has it, make sure it uses vanilla values so he doesnt see weird stuff.
			HostAndOptionalClient:	If only the client has it, it does nothing. I ll have to send a var to the client to tell him the thing
									is active, and otherwise work as default.
		*/
        public const string EmployeeJobText = "Employee AI";
        public const string CustomerText = "Customer";

        public const string EmployeeJobModuleText = $"{EmployeeJobText} Module";
        public const string CustomerModuleText = $"{CustomerText} Module";
        public const string PerformanceModuleText = $"NPC Performance - Requires {EmployeeJobText} or {CustomerText} module enabled";
        public const string ItemTransferSpeedModuleText = "Item Transfer Speed Module";
        public const string HighlightModuleText = "Highlight Module";
        public const string EquipWheelModuleText = "Equipment Wheel Module";
        public const string BuildModuleText = "Build Module";
        public const string MiscModuleText = "Misc. Module";
        public const string NotificationsModuleText = "Notifications Module";
        public const string PerfDisplayFreqMultModuleText = "x AI Workload Capacity Counter x";
        public const string PerfCustomSettingsModuleText = "xx NPC AI Custom Settings Module xx";
		public const string DebugModuleText = "z DEBUG z";
		public const string WorkloadCapacityDescription = "NOTE: A \"unit\" (a value of 1) of workload capacity, " +
			"is equal to a total of 50 turns to share between all employees, and another 50 for all customers, each second.";

		public void InitializeConfig(BaseUnityPlugin basePlugin) {
			configManagerControl = new ConfigManagerController(basePlugin.Config);
		}

		public void StartConfigBinding() {
			if (configManagerControl == null) {
				throw new System.InvalidOperationException("You must call InitializeConfig first.");
			}

			InitializeConfigStartNotes();

			string moduleGenericEnablerKey = $"*Enable ·{{0}}· {RequiresRestartSymbol}";
			string moduleGenericEnablerDescription = "This makes it possible for other settings in the [{0}] section to work.\n" +
				"Disable in case of errors.";

			InitializeEmployeeAIModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeNpcPerformanceModule();
			InitializeCustomerModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeItemTransferSpeedModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeHighlightExtensionModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeBroomShotgunMode(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeRadialWheelModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
            InitializeBuildModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
            InitializeMiscModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeNotificationModule();
			InitializeDisplayAutoSettingsModule();
			InitializeNpcPerformanceCustomSettingsModule();
			InitializeDebugModule();

            configManagerControl.RenameOrphanedSettings();

			Plugin.HookChangeDebugSetting();
		}


        private void InitializeConfigStartNotes() {
            configManagerControl.AddGUIHiddenNote(
				sectionName: "!↓ NOTE ABOUT CONFIG FILE EDITING ↓!",
				key: "",
				description: "I highly recommend installing \"Bepinex.ConfigurationManager\" so you can " +
				"change settings in-game.\n" +
				"You can grab the latest BepInEx5 version from its GitHub Releases page:\n" +
				"https://github.com/BepInEx/BepInEx.ConfigurationManager/releases\n\n" +
				"   *********    SETTINGS HELP    *********\n\n" +
				"Settings usually have the following format: \n\n" +
				"	## Setting description.\n" +
				"	# Setting Type: ...\n" +
				"	# Default value: ...\n" +
				"	# Acceptable value range/Acceptable values: ...  (This one might not exist for the setting)\n" +
				"	Setting short description = Value.\n\n" +
				"There are predefined values for each setting, depending on its \"Setting type\":\n" +
				" * Boolean - Its value can be either  true  or  false.\n" +
				" * Int32 - An integer numeric value. Limited by \"# Acceptable value range\"\n" +
				" * Single - A decimal value, like 13.1, or 0. Limited by \"# Acceptable value range\".\n" +
				" * EnumFreeStoragePriority/Enum... - These are special types that have a " +
				"range of values to choose from. Valid values are shown in a comma separated list in \"# Acceptable values\".\n" +
                " * KeyboardShortcut: - Activation hotkey. For a full list, see the 'Properties' section at " +
				"https://docs.unity3d.com/2023.1/Documentation/ScriptReference/KeyCode.html . Only keyboard and mouse " +
                "input is supported. Modifiers can be added with '+': Example: \"LeftControl + Y\"\n\n" +
				"   ***************************************\n\n" +
				"Host: Player that creates the game in multiplayer.\n" +
				"Client: Player that joins an already created multiplayer game"
			);
			//About the above comment of showing settings in its correct order in the config file itself:
			//	Prefixing numbers in sections and settings fixes this, and to this day its the only way of doing it.
			//	https://github.com/BepInEx/BepInEx.ConfigurationManager/issues/22#issuecomment-807431539
			//	Its not an option for me since changing setting order in the future would make them loose its saved value and I dont think its worth it.
			//	BepInEx would need to add support for a different way of setting configs order in the future, preferably
			//	reading the HotkeyContext tag value as ConfigManager already does.

			configManagerControl.AddSectionNote(
				sectionText: $"· Settings with a {RequiresRestartSymbol} symbol require " +
				$"a restart to apply changes, and hides related settings when disabled." +
				$"· Settings with a {RequiresReloadSymbol} symbol will work after reloading a save."
            );
		}

		private void InitializeEmployeeAIModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			EnableEmployeeChanges = configManagerControl.AddConfig(
				sectionName: EmployeeJobModuleText,
				key: string.Format(moduleGenericEnablerKey, EmployeeJobModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, EmployeeJobModuleText)
			);

			ClosedStoreEmployeeWalkSpeedMultiplier = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: EmployeeJobModuleText,
				key: "Employee walk speed mult. (closed store)",
				defaultValue: 1f,
				description: "Employee movement speed will be multiplied by this value while the store is closed with " +
				"no customers.",
				acceptableValueRange: new AcceptableValueRange<float>(0.25f, 10f),
				patchInstanceDependency: Container<EmployeeWalkSpeedPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			ClosedStoreEmployeeItemTransferMaxed = configManagerControl.AddConfig(
				sectionName: EmployeeJobModuleText,
				key: "Employee fast item placing (closed store)",
				defaultValue: false,
				description: "Restock employees will place all products in a shelf at once, but only while the store is " +
				"closed with no customers.",
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			EmployeeNextActionWait = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: EmployeeJobModuleText,
				key: "Wait time after job step is finished",
				defaultValue: 1.5f,
				description: "Sets the amount of time, in seconds, that an employee will idle before going into the " +
								"next job step, like picking up a box or placing it in a storage slot. \nDoes not affect " +
								"employee checkout speed or restocker item placing.",
				acceptableValueRange: new AcceptableValueRange<float>(0.1f, 4f),
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			ImprovedSecurityPickUpMode = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: EmployeeJobModuleText,
				key: "Improved security pick-up mode",
				defaultValue: EnumSecurityPickUp.Disabled,
				description: "As security employees level up, they will be able to pick up more products at " +
				$"once, and reach across a larger area. All benefits stop improving at level {EmployeeJobAIPatch.MaxSecurityPickUpLevel} \n" +
				$"- {EnumSecurityPickUp.Disabled}: Same as base game. Products are picked up one by one." +
				$"- {EnumSecurityPickUp.Reduced}: All security level benefits halved compared to \"{EnumSecurityPickUp.Normal}\". " +
				$"A level 100 security employee with the {EnumSecurityPickUp.Reduced} setting, will have the same " +
				$"pick-up skills as a level 50 with the {EnumSecurityPickUp.Normal} setting" +
				$"- {EnumSecurityPickUp.Normal}: Security will pick up an additional product every " +
				$"{EmployeeJobAIPatch.LevelsForExtraPickUp} levels, and slightly increase its range every level" + 
				$"- {EnumSecurityPickUp.AlwaysMaxed}: All security employees will have the best possible pick-up stats.",
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			SecurityThiefChaseMode = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: EmployeeJobModuleText,
				key: "Security Thief Chase",
				defaultValue: EnumSecurityEmployeeThiefChase.Disabled,
				description: "This sets the mode in which security employees will work against thieves:\n" +
				$"- {EnumSecurityEmployeeThiefChase.Disabled}: Same as base game. All security employees " +
				$"will target the first thief they can find.\n" +
				$"- {EnumSecurityEmployeeThiefChase.AllChaseButLastOne}: Same as base game, but if all thieves " +
				$"are already being chased, one security employee will keep watch for the next thief.\n" +
				$"- {EnumSecurityEmployeeThiefChase.OnlyOnePerThief}: Recommended mode. Each thief will be " +
				$"chased by a single security employee and no more. The rest keep watch for the next thief.",
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
            
            FreeStoragePriority = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: EmployeeJobModuleText,
				key: "Employee storage shelf priority",
				defaultValue: EnumFreeStoragePriority.Labeled,
				description: "Controls which types of storage shelves employees may use and in what order of priority.\n" +
                "Any shelf type not specified in the setting, becomes off-limits for employees to place boxes in, though " +
				"they will still pick up boxes from it. Restocker box merging ignores priority, but will avoid disallowed storage types.\n" +
				$"{EnumFreeStoragePriority.Labeled} ({EnumFreeStoragePriority.Labeled.GetDescription()}) - Vanilla behaviour.\n" +
                $"{EnumFreeStoragePriority.Unlabeled} ({EnumFreeStoragePriority.Unlabeled.GetDescription()}\n)" + 
                $"{EnumFreeStoragePriority.OnlyLabeled} ({EnumFreeStoragePriority.OnlyLabeled.GetDescription()})\n" +
                $"{EnumFreeStoragePriority.OnlyUnlabeled} ({EnumFreeStoragePriority.OnlyUnlabeled.GetDescription()})\n",
                patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}

		private void InitializeCustomerModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
            EnableCustomerChanges = configManagerControl.AddConfig(
                sectionName: CustomerModuleText,
                key: string.Format(moduleGenericEnablerKey, CustomerModuleText),
                defaultValue: true,
                description: string.Format(moduleGenericEnablerDescription, CustomerModuleText)
            );

            EnableCustomerStuckDetection = configManagerControl.AddConfig(
                sectionName: CustomerModuleText,
                key: "Enable stuck customer fix",
                defaultValue: false,
                description: "Check whether a customer seems to be stuck unable to move, and tries to free it " +
				"with progressively more aggressive methods.\nThis may have a small performance hit on weaker CPUs.",
                patchInstanceDependency: Container<NPC_CustomerNavFixer>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );

            EnableShopListOnlyAssignedProducts = configManagerControl.AddConfig(
                sectionName: CustomerModuleText,
                key: "Customers dont buy all unlocked products",
                defaultValue: false,
                description: "Prevents customers from trying to buy unlocked products that you did not put up for sale," +
				" so you can focus your store on selling only the products you want.\n" +
				"When you open the store, a list is generated of all product types assigned to store shelves, empty " +
				"or not. Customers will only buy products that are in that list. \n" +
				"This internal list cannot be modified, and will refresh next time you reopen the store. " +
				"Think of it as if the store is sending out the catalog of products for the day, and changing shelf " +
				"assignments afterwards wont affect what customers expect to find.\n" +
				"Customers still complain if they cant find any of the products supposed to be on stock from the list. ",
                patchInstanceDependency: Container<ReplaceCustomerUnavailableProducts>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );

            CustomerCardPayRatio = configManagerControl.AddConfigWithAcceptableValues(
                sectionName: CustomerModuleText,
                key: "Card pay percentage",
                defaultValue: 50,
                description: "Lets you adjust the percentage of customers that pay with card.\n" +
                "0: All customers pay with cash.\n100: All customers pay with credit card.",
                patchInstanceDependency: Container<CustomerCashCardPayRatio>.Instance,
                acceptableValueRange: new AcceptableValueRange<int>(0, 100),
                showRangeAsPercent: true,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );

        }

        private void InitializeNpcPerformanceModule() {
            NpcJobFrequencyMode = configManagerControl.AddConfigWithAcceptableValues(
                sectionName: PerformanceModuleText,
                key: "Npc performance system",
                defaultValue: EnumJobFrequencyMultMode.Disabled,
                description: "TL;DR: Choose Auto_Performance if your system is slow, otherwise go with Auto_Balanced.\n\n" +
                "Employees/Customers have a fixed set of \"actions\" they do each second.\n" +
                "This setting allows you to choose an automatic balancing system on how npcs will perform. \n" +
                "It wont make npcs faster than normal, but it avoids them reacting slower due to game limits.\n\n" +
                "- Disabled: Same as unmodded game\n" +
                "- Auto_Performance: System performance over npc reaction times\n" +
                "- Auto_Balanced: Recommended option. Switches between using less or more resources than vanilla game, depending on npc needs\n" +
                "- Auto_Aggressive: Adjusts for max npc responsiveness\n" +
                "- Auto_Custom: For advanced users. Lets you set all internal values. You can change them below " +
                    $"in the ·{PerfCustomSettingsModuleText}· section.",
                patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );

            EmployeeIdleWait = configManagerControl.AddConfigWithAcceptableValues(
                sectionName: PerformanceModuleText,
                key: "Job check frequency while idle",
                defaultValue: 2f,
                description: "Sets the interval (in seconds) for an idle employee to search for new jobs. " +
                            "Higher values may improve performance.",
                acceptableValueRange: new AcceptableValueRange<float>(1f, 10f),
                patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );
        }

        private void InitializeItemTransferSpeedModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			

			EnableTransferProducts = configManagerControl.AddConfig(
				sectionName: ItemTransferSpeedModuleText,
				key: string.Format(moduleGenericEnablerKey, ItemTransferSpeedModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, ItemTransferSpeedModuleText)
			);

			ItemTransferMode = configManagerControl.AddConfig(
				sectionName: ItemTransferSpeedModuleText,
				key: "Product shelf item transfer mode",
				defaultValue: EnumItemTransferMode.Disabled,
				description: "Sets when extra item transfer will work." +
						"IMPORTANT NOTE: Internally disabled if \"High Latency Mode\" is enabled.",
				patchInstanceDependency: Container<IncreasedItemTransferPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
			);

			NumTransferProducts = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: ItemTransferSpeedModuleText,
				key: "Product shelf item transfer quantity",
				defaultValue: EmployeeJobAIPatch.NumTransferItemsBase,
				description: "The number of items you place or take from a product shelf when you " +
							"click (or hold click) on it.\n" +
							$"The setting \"{ItemTransferMode.Definition.Key}\" must not be " +
							$"{EnumItemTransferMode.Disabled.GetDescription()}.",
				acceptableValueRange: new AcceptableValueRange<int>(1, 50),
				patchInstanceDependency: Container<IncreasedItemTransferPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
			);
		}

		private void InitializeHighlightExtensionModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			EnablePatchHighlight = configManagerControl.AddConfig(
				sectionName: HighlightModuleText,
				key: string.Format(moduleGenericEnablerKey, HighlightModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, HighlightModuleText)
			);

            HighlightVisualMode = configManagerControl.AddConfig(
                sectionName: HighlightModuleText,
                key: "Highlight visual mode",
                defaultValue: HighlightMode.Disabled,
                description: "The highlight visuals around the object's shape.\nWhile highlighted, " +
				$"all of them have the same small performance hit in most cases, but generally, " +
				$"{HighlightMode.OutlineOnly} is the fastest, and going lower on the list gets " +
				$"progressively slower, with {HighlightMode.SeeThrough} being the slowest:\n" +
				$" - {HighlightMode.Disabled}: No highlighting.\n" +
                $" - {HighlightMode.OutlineOnly}: A simple outline. The closest to the old highlight visuals.\n" +
                $" - {HighlightMode.OutlineGlow}: An outline in direct view, and a glow when not in view.\n" +
                $" - {HighlightMode.OutlineBlurredGlow}: An outline in direct view, and a fuzzy glow when not in view.\n" +
                $" - {HighlightMode.SeeThrough}: Colors the entire shelf when not in direct view.",
                patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.Any
            );

            HotkeyCycleHighlightMode = configManagerControl.AddConfig(
                sectionName: HighlightModuleText,
                key: "Cycle through highlight modes",
                defaultValue: KeyboardShortcut.Empty,
                description: $"Changes the current highlight visual mode with a hotkey.",
                patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.Any
            );

            HotkeyToggleAimedHighlight = configManagerControl.AddConfig(
                sectionName: HighlightModuleText,
                key: "Highlight product on crosshair",
                defaultValue: KeyboardShortcut.Empty,
                description: "Looking at a box or item from a short distance (its info will appear in the " +
				"top-right panel) and pressing this hotkey will activate its highlight. \nIf " +
				"no product is being looked at, it removes all current highlights.\nWhen holding a box and " +
				"not looking at a product, it toggles highlighting for the held item.",
                patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.Any
            );

            configManagerControl.AddGUIHiddenNote(
				sectionName: HighlightModuleText,
				key: "",
				description: $"Below you can set each highlight color.\nThe format is an Hexadecimal " +
				$"RGB with an optional transparency channel (RRGGBB or RRGGBBAA). " +
				$"There are many \"color to hex\" websites to help you set the color.\n" +
				$"The alpha channel is used to control the intensity of the color. By default its \"BF\", " +
				$"equivalent to 75% of max intensity, but higher values may be needed in brightly lit " +
				$"stores or the highlight might be hard to see."
			);

			ShelfHighlightColorRGBA = configManagerControl.AddConfig(
				sectionName: HighlightModuleText,
				key: "Shelf highlight color",
				defaultValue: new Color(1f, 0.25f, 0.1f, 0.75f),//Orange
				description: "Color of product shelves when highlighted. The alpha channel controls effect intensity.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			ShelfLabelHighlightColorRGBA = configManagerControl.AddConfig(
				sectionName: HighlightModuleText,
				key: "Shelf label highlight color",
				defaultValue: new Color(1f, 1f, 0f, 0.75f),		//Yellow
				description: "Color of shelf labels when highlighted. The alpha channel controls effect intensity.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			StorageHighlightColorRGBA = configManagerControl.AddConfig(
				sectionName: HighlightModuleText,
				key: "Storage highlight color",
				defaultValue: new Color(0f, 0f, 1f, 0.75f),		//Blue
				description: "Color of storage shelves when highlighted. The alpha channel controls effect intensity.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			StorageSlotHighlightColorRGBA = configManagerControl.AddConfig(
				sectionName: HighlightModuleText,
				key: "Storage slot highlight color",
				defaultValue: new Color(0f, 1f, 1f, 0.75f),		//Cyan,
				description: "Color of storage slot spaces when highlighted. The alpha channel controls effect intensity.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);
		}

		private void InitializeRadialWheelModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			string displayControlKeyText = "Wheel display control";
			string toolsModifierKeyText = "Quick tools modifier";
            QuickToolKeyModifiers defaultModifier = QuickToolKeyModifiers.LeftControl;

            EnableRadialWheelPatches = configManagerControl.AddConfig(
                sectionName: EquipWheelModuleText,
                key: string.Format(moduleGenericEnablerKey, EquipWheelModuleText),
                defaultValue: true,
                description: string.Format(moduleGenericEnablerDescription, EquipWheelModuleText) + "\n\n" +
				"This setting also enables other players to use the equipment wheel when you are the host."
            );
            
            RadialEquipmentWheelHotkey = configManagerControl.AddConfig(
                sectionName: EquipWheelModuleText,
                key: "Equipment wheel hotkey",
                defaultValue: KeyboardShortcut.Empty,
                description: "Hotkey for showing up the equipment wheel while held. Releasing it with a tool selected equips it. " +
				"You can also left click to select the tool, or right click to cancel and close the equipment wheel.\n" +
                "Tools are not magically spawned, but instead they are teleported to your hands, which means the tool must already " +
				"exist in an organizer or on the map for it to work.",
				patchInstanceDependency: Container<CameraBlockerPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
            );

            EnableRadialQuickToolsKeys = configManagerControl.AddConfig(
                sectionName: EquipWheelModuleText,
                key: "Enable quick tool hotkeys",
                defaultValue: false,
                description: $"Enable hotkeys to bring up a tool without using the wheel.\nHotkeys start from " +
					$"key '1' and up, with an optional modifier key ({defaultModifier.GetDescription()} " +
					$" by default) that you can set in the '{toolsModifierKeyText}' setting below." +
					$"If you want a different hotkey number for a tool, change their order in the " +
					$"'{displayControlKeyText}' setting below. \nDance moves will still play when pressing numbers, " +
					$"so you should reassign those.",
                patchInstanceDependency: Container<CameraBlockerPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
            );

            RadialQuickToolsModifierKey = configManagerControl.AddConfig(
                sectionName: EquipWheelModuleText,
                key: $"Quick tools modifier",
                defaultValue: defaultModifier,
                description: "Modifier key used in combination with a number to bring up the corresponding tool.",
                patchInstanceDependency: Container<CameraBlockerPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
            );

	        RadialDisplayControl = configManagerControl.AddConfig(
				sectionName: EquipWheelModuleText,
				key: $"{displayControlKeyText} {RequiresReloadSymbol}",
				defaultValue: ToolWheelDefinitions.GetDefaultDisplayControlString(),
				description: "Here you can list the tools you want displayed in the equipment wheel, and " +
                "their position. The first tool in the list shows up at the top of the equipment wheel, with " +
				"next ones following a clockwise direction.\n" +
				"The format is a comma separated list of tools, case insensitive." +
				"If the list is empty or its format is not valid, all tools will be shown as if default.\n",
				patchInstanceDependency: Container<CameraBlockerPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);
        }

		private void InitializeBroomShotgunMode(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			string broomShotgunModuleText = "American Module";

			BroomShotgunModeEnabled = configManagerControl.AddConfig(
				sectionName: broomShotgunModuleText,
				key: string.Format(moduleGenericEnablerKey, broomShotgunModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, broomShotgunModuleText)
			);

            BroomShotgunModeHotkey = configManagerControl.AddConfig(
                sectionName: broomShotgunModuleText,
                key: "Toggle broom aiming",
                defaultValue: new KeyboardShortcut(KeyCode.Mouse1),
                description: "Press this hotkey while holding a broom to enter aim mode, then left click to shoot. " +
				"The same key will also switch back to holding the broom normally. Have fun.",
                patchInstanceDependency: Container<BroomShotgunPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
            );

			MaxShotgunSmokeParticles = configManagerControl.AddConfigWithAcceptableValues(
                sectionName: broomShotgunModuleText,
                key: $"Max smoke particles",
                defaultValue: 2000,
                description: "Limit the amount of smoke particles from shooting. This should only " +
				"matter in potato computers, in which case lower it to get more performance." +
				"A value of 0 completely disables smoke effects.",
                acceptableValueRange: new AcceptableValueRange<int>(0, 3000),
                patchInstanceDependency: Container<BroomShotgunPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
            );


        }

		private void InitializeBuildModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			EnableBuildPatches = configManagerControl.AddConfig(
				sectionName: BuildModuleText,
				key: string.Format(moduleGenericEnablerKey, BuildModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, BuildModuleText)
			);

            CloneBuildHotkey = configManagerControl.AddConfig(
                sectionName: BuildModuleText,
                key: "Clone target buildable hotkey",
                defaultValue: KeyboardShortcut.Empty,
                description: "Shortcut to open the build menu, with the targeted building " +
				"preselected and ready to buy and place.",
                modInstallSide: MultiplayerModInstallSide.Any
            );

            MoveBuildHotkey = configManagerControl.AddConfig(
                sectionName: BuildModuleText,
                key: "Move target buildable hotkey",
                defaultValue: KeyboardShortcut.Empty,
                description: "Shortcut to put the targeted building in 'Placement mode'.",
                modInstallSide: MultiplayerModInstallSide.Any
            );
			
			CloneMoveTargetDistance = configManagerControl.AddConfigWithAcceptableValues(
                sectionName: BuildModuleText,
                key: "Clone/Move max target distance",
                defaultValue: 40,
                description: $"Limit on how far away a buildable can be for the above hotkeys to clone it or move it.",
                acceptableValueRange: new AcceptableValueRange<int>(5, 100),
                modInstallSide: MultiplayerModInstallSide.Any
            );

            EnableIncreasedPropLoadLimits = configManagerControl.AddConfig(
                sectionName: BuildModuleText,
                key: $"Increased limit of buildables {RequiresReloadSymbol}",
                defaultValue: false,
                description: $"This solves the problem of the vanilla game having a limit of {GenericBuildPatches.VanillaPropLoadLimit} " +
                $"buildables, and decoratives, so any items built after that wont show up. " +
                $"This patch is for those madmen, madwomen and madchildren that went the extra mile and got " +
                $"to hit this limit. Now you get {GenericBuildPatches.NewPropLoadLimit} as the new limit " +
                $"so try to beat that, you freaks.",
                patchInstanceDependency: Container<GenericBuildPatches>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );
        }

		private void InitializeMiscModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
            EnableMiscPatches = configManagerControl.AddConfig(
				sectionName: MiscModuleText,
				key: string.Format(moduleGenericEnablerKey, MiscModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, MiscModuleText)
			);

            EnableCheckoutAutoClicker = configManagerControl.AddConfig(
				sectionName: MiscModuleText,
				key: "Hold click to scan checkout products",
				defaultValue: false,
				description: "Instead of having to keep pressing Main Action (Left Click by default) to scan each product " +
				"on the cash register belt, you can hold it to continuously scan as you mouse over products.",
				patchInstanceDependency: Container<CheckoutAutoClickScanner>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

            EnablePriceGunFix = configManagerControl.AddConfig(
				sectionName: MiscModuleText,
				key: "Enable pricing gun double price fix",
				defaultValue: false,
				description: "Fixes customers sometimes complaining when prices are set to 200%." +
				"See the 0.8.2.0 changelog for a explanation on why I consider this a bug.",
				patchInstanceDependency: Container<PricingGunFixPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			EnableExpandedProdOrderClickArea = configManagerControl.AddConfig(
				sectionName: MiscModuleText,
				key: "Increased clickable area in product order",
				defaultValue: false,
				description: "When at the Manager Blackboard, you can click anywhere in the product panel " +
				"to add it to the shopping list, instead of just the \"+\" button.",
				patchInstanceDependency: Container<ExpandProductOrderClickArea>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);
        }
		private void InitializeNotificationModule() {			
			EnableModNotifications = configManagerControl.AddConfig(
				sectionName: NotificationsModuleText,
				key: $"Enable custom mod notifications {RequiresRestartSymbol}",
				defaultValue: true,
				description: "If enabled, you may receive custom notifications from this mod."
			);

			EnableWelcomeMessages = configManagerControl.AddConfig(
				sectionName: NotificationsModuleText,
				key: $"Enable random welcome messages {RequiresReloadSymbol}",
				defaultValue: true,
				description: "When you load into a supermarket, there is a chance that you get a random welcoming message.",
				modInstallSide: MultiplayerModInstallSide.Any
			);

			EnableErrorMessages = configManagerControl.AddConfig(
				sectionName: NotificationsModuleText,
				key: $"Enable error messages {RequiresReloadSymbol}",
				defaultValue: true,
				description: "If a mod patch fails, it will show a message to warn you.\n" +
				"Additionally, after that it will show a super helpful tip to, maybe, fix the error!",
				modInstallSide: MultiplayerModInstallSide.Any
			);
		}

		private void InitializeDisplayAutoSettingsModule() {
			DisplayAutoModeFrequencyMult = configManagerControl.AddConfig(
				sectionName: PerfDisplayFreqMultModuleText,
				key: $"Show workload capacity counter",
				defaultValue: false,
				description: $"Shows a counter with the workload capacity used in the last cycle (1 second) for " +
				$"Employees (E) and Customers (C) separately.\n" +
				$"Lower values means less cpu was used for npc actions. A value of 1 is the same as the base game value.\n" +
                $"This will only work while the {NpcJobFrequencyMode.Definition.Key} setting is " +
				$"in any of the \"Auto\" modes.",
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}
		
		private void InitializeNpcPerformanceCustomSettingsModule() {
			string customModeText = $"∨ This setting is only used if \"{NpcJobFrequencyMode.Definition.Key}\" value is " +
				$"\"{EnumJobFrequencyMultMode.Auto_Custom.GetDescription()}\" ∨\n\nDefault values are the same as in the Balanced performance mode.\n";

			BalancedMode balancedMode = new();

			CustomEmployeeWaitTarget = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Average employee wait time target",
				defaultValue: balancedMode.AvgEmployeeWaitTargetMillis,
				description: $"{customModeText}\n\n" +
				"Employees have limited \"turns\" to do its job, and end up waiting more or less depending on how much total work there is." +
				"This setting sets the wait time, in milliseconds, for the system to automatically balance workload " +
				"capacity, so the average wait time between all employees/customers is kept as close as possible to this value.\n" +
				"Smaller values will make the system work harder to give more turns to npcs and reduce wait times.",
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.AvgNpcWaitTarget.MinLimit, AutoModeLimits.AvgNpcWaitTarget.MaxLimit),
				patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

            CustomCustomerWaitTarget = configManagerControl.AddConfigWithAcceptableValues(
                sectionName: PerfCustomSettingsModuleText,
                key: "Average customer wait time target",
                defaultValue: balancedMode.AvgCustomerWaitTargetMillis,
                description: $"{customModeText}\n\n" +
                $"Same as {CustomEmployeeWaitTarget.Definition.Key}, but for customers. Since most of the time there are far more customers " +
				$"than employees, it is recommended to keep this value higher to avoid dragging the system too much at peak store times.",
                acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.AvgNpcWaitTarget.MinLimit, AutoModeLimits.AvgNpcWaitTarget.MaxLimit),
                patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
                modInstallSide: MultiplayerModInstallSide.HostSideOnly
            );

            CustomMinimumFrequencyMult = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Minimum workload capacity",
				defaultValue: balancedMode.MinFreqMult,
				description: $"{customModeText}\n\n" +
				"Sets the minimum possible workload capacity. When the job scheduler detects that the target wait time " +
				"is being sufficiently kept, it will start reducing workload capacity up to this value to save performance." +
				WorkloadCapacityDescription,
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.MinFreqMult.MinLimit, AutoModeLimits.MinFreqMult.MaxLimit),
				patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			CustomMaximumFrequencyMult = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Maximum workload capacity",
				defaultValue: balancedMode.MaxFreqMult,
				description: $"{customModeText}\n\n" +
				"Sets the maximum possible workload capacity. When the job scheduler detects that the npc wait " +
				"time is higher than the target, it will start increasing workload capacity up to this value. " +
				"Be careful, since higher values can have a big impact on cpu usage by the AI.\n" +
				WorkloadCapacityDescription,
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.MaxFreqMult.MinLimit, AutoModeLimits.MaxFreqMult.MaxLimit),
				patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
			
			CustomMaximumFrequencyReduction = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Maximum workload reduction per cycle",
				defaultValue: Math.Abs(balancedMode.DecreaseStep),
				description: $"{customModeText}\n\n" +
				"Sets how quickly the job scheduler can reduce workload capacity. Each cycle (1 second), it will reduce workload " +
				"capacity up to this quantity. The actual quantity will depend on how far below target is the npc avg wait timer.",
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.DecreaseStep.MinLimit, AutoModeLimits.DecreaseStep.MaxLimit),
				patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			CustomMaximumFrequencyIncrease = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Maximum workload increase per cycle",
				defaultValue: balancedMode.IncreaseStep,
				description: $"{customModeText}\n\n" +
				"Sets how quickly the job scheduler can increase workload capacity. Each cycle (1 second), it will increase workload " +
				"capacity up to this quantity. The actual quantity will depend on how far above target is the npc avg wait timer.",
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.IncreaseStep.MinLimit, AutoModeLimits.IncreaseStep.MaxLimit),
				patchInstanceDependency: Container<NpcJobSchedulerPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}

		private void InitializeDebugModule() {
			//Check if the setting was preinitialized and true.
			//	If thats the case, we need to set the value manually after binding.
			bool isDevEnabled = EnabledDevMode?.Value == true;
			
			EnabledDevMode = BindDebugConfig();
			if (isDevEnabled) {
				EnabledDevMode.Value = true;
			}

			TeleportSoundVolume = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: DebugModuleText,
				key: "Teleport sound volume",
				defaultValue: 0.5f,
				description: "Volume of the teleport sound effect.",
				isAdvanced: true,
				acceptableValueRange: new AcceptableValueRange<float>(0, 1),
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);
		}

		private ConfigEntry<bool> BindDebugConfig() {
			return configManagerControl.AddConfig(
				sectionName: DebugModuleText,
				key: $"Enable DEV mode",
				defaultValue: false,
				description: "Dev stuff you dont want to know about.",
				isAdvanced: true
			);
		}

		/// <summary>
		/// Checks if the dev mode is enabled in its setting.
		/// Works even before performing the complete config binding.
		/// </summary>
		/// <returns></returns>
		public bool IsDebugEnabledConfig() {
			bool debugConfigEnabled;

			if (EnabledDevMode == null) {
				//Called before its initialization was performed. Bind it, take its value, and remove 
				//	the setting, so the full config initialization can place it in its proper order.
				EnabledDevMode = BindDebugConfig();
				debugConfigEnabled = EnabledDevMode.Value;
				configManagerControl.Remove(EnabledDevMode.Definition);

				//Dont null, so we can keep the value for later, since
				//	configManagerControl.Remove also disables the setting.
				//EnabledDevMode = null;	
			} else {
				debugConfigEnabled = EnabledDevMode.Value;
			}

			return debugConfigEnabled;
		}

	}



}

