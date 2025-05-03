using System;
using BepInEx;
using BepInEx.Configuration;
using Damntry.Utils.ExtensionMethods;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode.DataDefinition;
using SuperQoLity.SuperMarket.Patches.HighlightModule;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using UnityEngine;
using SuperQoLity.SuperMarket.Patches.Misc;
using SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch;

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


		public ConfigEntry<bool> EnableEmployeeChanges { get; private set; }
		public ConfigEntry<float> EmployeeNextActionWait { get; private set; }
		public ConfigEntry<float> EmployeeIdleWait { get; private set; }
		public ConfigEntry<float> ClosedStoreEmployeeWalkSpeedMultiplier { get; private set; }
		public ConfigEntry<bool> ClosedStoreEmployeeItemTransferMaxed { get; private set; }
		public ConfigEntry<EnumFreeStoragePriority> FreeStoragePriority { get; private set; }

		public ConfigEntry<EnumJobFrequencyMultMode> EmployeeJobFrequencyMode { get; private set; }

		public ConfigEntry<bool> EnableTransferProducts { get; private set; }
		public ConfigEntry<EnumItemTransferMode> ItemTransferMode { get; private set; }
		public ConfigEntry<int> NumTransferProducts { get; private set; }

		public ConfigEntry<bool> EnablePatchHighlight { get; private set; }
		public ConfigEntry<Color> ShelfHighlightColor { get; private set; }
		public ConfigEntry<Color> ShelfLabelHighlightColor { get; private set; }
		public ConfigEntry<Color> StorageHighlightColor { get; private set; }
		public ConfigEntry<Color> StorageSlotHighlightColor { get; private set; }

		public ConfigEntry<bool> EnableMiscPatches { get; private set; }
		public ConfigEntry<bool> EnableCheckoutAutoClicker { get; private set; }
		public ConfigEntry<bool> EnablePriceGunFix { get; private set; }
		public ConfigEntry<bool> EnableExpandedProdOrderClickArea { get; private set; }

		public ConfigEntry<bool> EnableModNotifications { get; private set; }
		public ConfigEntry<bool> EnableWelcomeMessages { get; private set; }
		public ConfigEntry<bool> EnableErrorMessages { get; private set; }

		public ConfigEntry<bool> DisplayAutoModeFrequencyMult { get; private set; }

		public ConfigEntry<float> CustomAvgEmployeeWaitTarget { get; private set; }
		public ConfigEntry<float> CustomMinimumFrequencyMult { get; private set; }
		public ConfigEntry<float> CustomMaximumFrequencyMult { get; private set; }
		public ConfigEntry<float> CustomMaximumFrequencyReduction { get; private set; }
		public ConfigEntry<float> CustomMaximumFrequencyIncrease { get; private set; }

		/*
		public ConfigEntry<float> EmployeeJobFrequencyManualMultiplier { get; private set; }
		public ConfigEntry<float> EmployeeJobManualMaxProcessTime { get; private set; }
		*/

		public ConfigEntry<bool> EnabledDevMode { get; private set; }
		public ConfigEntry<float> TeleportSoundVolume { get; private set; }


		private const string RequiresRestartSymbol = "(**)";

		/* Depending on the modInstallSide of each setting, it would need to:
		
			HostSideOnly:			If the functionality could be executed by a client in the vanilla game, make sure now it doesnt.
			HostAndSoftClient:		If only the client has it, make sure it uses vanilla values so he doesnt see weird stuff.
			HostAndOptionalClient:	If only the client has it, it does nothing. I ll have to send a var to the client to tell him the thing
									is active, and otherwise work as default.
		*/

		public const string EmployeeJobModuleText = "Employee AI Module";
		public const string PerformanceModuleText = $"Employee AI Performance - Requires ·{EmployeeJobModuleText}· enabled";
		public const string PerfDisplayFreqMultModuleText = "x AI Workload Capacity Counter x";
		public const string PerfCustomSettingsModuleText = "xx Employee AI Custom Settings Module xx";
		public const string PerfManualSettingsModuleText = "xx Employee AI Manual Settings Module xx";
		public const string DebugModuleText = "z DEBUG z";
		public const string WorkloadCapcityDescription = "NOTE: A \"unit\" (a value of 1) of workload capacity, " +
			"is equal to a total of 50 turns to share between all employees, each second.";

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
			InitializeEmployeePerformanceModule();
			InitializeItemTransferSpeedModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeHighlightExtensionModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeMiscModule(moduleGenericEnablerKey, moduleGenericEnablerDescription);
			InitializeNotificationModule();
			InitializeDisplayAutoSettingsModule();
			InitializeEmployeePerformanceCustomSettingsModule();
			//InitializeEmployeePerformanceManualSettingsModule();
			InitializeDebugModule();

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
				"range of values to choose from. Valid values are shown in a comma separated list in \"# Acceptable values\".\n\n" +
				"   ***************************************\n\n" +
				"Host: Player that creates the game in multiplayer.\n" +
				"Client: Player that joins an already created multiplayer game"
			);
			//About the above comment of showing settings in its correct order in the config file itself:
			//	Prefixing numbers in sections and settings fixes this, and to this day its the only way of doing it.
			//	https://github.com/BepInEx/BepInEx.ConfigurationManager/issues/22#issuecomment-807431539
			//	Its not an option for me since changing setting order in the future would make them loose its saved value and I dont think its worth it.
			//	BepInEx would need to add support for a different way of setting configs order in the future, preferably
			//	reading the Category tag value as ConfigManager already does.

			configManagerControl.AddSectionNote(
				sectionText: $"· Settings with a {RequiresRestartSymbol} symbol require " +
				$"a restart to apply changes, and hides related settings when disabled.",
				description: ""
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
				description: "Restock employees will place all products in a single action while the store is closed with " +
				"no customers.",
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

			FreeStoragePriority = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: EmployeeJobModuleText,
				key: "Unassigned storage priority",
				defaultValue: EnumFreeStoragePriority.Labeled,
				description: "Allows you to prioritize one type of storage over another for employees. " +
				"Priority is always:\n1. Empty assigned storage\n2. Chosen storage\n3. Non chosen storage",
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}

		private void InitializeEmployeePerformanceModule() {
			//string manualSettingsLocationMessage = $"You can change these values near the bottom of this config, " +
			//	$"in the ·{PerfManualSettingsModuleText}· section.";

			EmployeeJobFrequencyMode = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerformanceModuleText,
				key: "Employee performance system",
				defaultValue: EnumJobFrequencyMultMode.Disabled,
				description: "TL;DR: Choose Auto_Performance if your system is slow, otherwise go with Auto_Balanced.\n\n" +
				"Employees have a fixed limit of \"actions\" per second.\n" +
				"This setting allows you to choose an automatic balancing system on how employees will perform. \n" +
				"It wont make employees faster than normal, but it avoids them reacting slower due to game limits.\n\n" +
				"- Disabled: Same as unmodded game\n" +
				"- Auto_Performance: System performance over employee reactivity\n" +
				"- Auto_Balanced: Recommended option\n" +
				"- Auto_Aggressive: Adjusts for max employee responsiveness\n" +
				"- Auto_Custom: For advanced users. Lets you set all internal values. You can change them below " +
					$"in the ·{PerfCustomSettingsModuleText}· section.",
				//"- Manual: Not recommended unless you know what you are doing. The Auto_Custom mode is better " +
				//	"in every way. Lets you choose a fixed multiplier and time limiter values. You can change these " +
				//	$"values near the bottom of this config, in the ·{PerfManualSettingsModuleText}· section.",
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			EmployeeIdleWait = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerformanceModuleText,
				key: "Job check frequency while idle",
				defaultValue: 2f,
				description: "Sets the interval (in seconds) for an idle employee to search for new jobs. " +
							"Higher values may help with performance.",
				acceptableValueRange: new AcceptableValueRange<float>(1f, 10f),
				patchInstanceDependency: Container<EmployeeJobAIPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}

		private void InitializeItemTransferSpeedModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			string itemTransferSpeedModuleText = "Item Transfer Speed Module";

			EnableTransferProducts = configManagerControl.AddConfig(
				sectionName: itemTransferSpeedModuleText,
				key: string.Format(moduleGenericEnablerKey, itemTransferSpeedModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, itemTransferSpeedModuleText)
			);

			ItemTransferMode = configManagerControl.AddConfig(
				sectionName: itemTransferSpeedModuleText,
				key: "Product shelf item transfer mode",
				defaultValue: EnumItemTransferMode.Disabled,
				description: "Sets when extra item transfer will work." +
						"IMPORTANT NOTE: Internally disabled if \"High Latency Mode\" is enabled.",
				patchInstanceDependency: Container<IncreasedItemTransferPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostAndOptionalClient
			);

			NumTransferProducts = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: itemTransferSpeedModuleText,
				key: "Product shelf item transfer quantity",
				defaultValue: 1,
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
			string highlightModuleText = "Highlight Module";

			EnablePatchHighlight = configManagerControl.AddConfig(
				sectionName: highlightModuleText,
				key: string.Format(moduleGenericEnablerKey, highlightModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, highlightModuleText)
			);

			configManagerControl.AddGUIHiddenNote(
				sectionName: highlightModuleText,
				key: "",
				description: $"Below you can set highlight colors\nThe format is an Hexadecimal " +
				$"RGB with an optional transparency channel (RRGGBB or RRGGBBAA). " +
				$"There are many \"color to hex\" websites to help you set the color."
			);

			ShelfHighlightColor = configManagerControl.AddConfig(
				sectionName: highlightModuleText,
				key: "Shelf highlight color",
				defaultValue: Color.red,
				description: "Color of product shelves when highlighted.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			ShelfLabelHighlightColor = configManagerControl.AddConfig(
				sectionName: highlightModuleText,
				key: "Shelf label highlight color",
				defaultValue: Color.yellow,
				description: "Color of shelf labels when highlighted.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			StorageHighlightColor = configManagerControl.AddConfig(
				sectionName: highlightModuleText,
				key: "Storage highlight color",
				defaultValue: Color.blue,
				description: "Color of storage shelves when highlighted.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			StorageSlotHighlightColor = configManagerControl.AddConfig(
				sectionName: highlightModuleText,
				key: "Storage slot highlight color",
				defaultValue: Color.cyan,
				description: "Color of storage slot spaces when highlighted.",
				patchInstanceDependency: Container<HighlightStorageSlotsPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);
		}

		private void InitializeMiscModule(string moduleGenericEnablerKey, string moduleGenericEnablerDescription) {
			string miscModuleText = "Misc. Module";

			EnableMiscPatches = configManagerControl.AddConfig(
				sectionName: miscModuleText,
				key: string.Format(moduleGenericEnablerKey, miscModuleText),
				defaultValue: true,
				description: string.Format(moduleGenericEnablerDescription, miscModuleText)
			);

			EnableCheckoutAutoClicker = configManagerControl.AddConfig(
				sectionName: miscModuleText,
				key: "Hold click to scan checkout products",
				defaultValue: false,
				description: "Instead of having to keep pressing Main Action (Left Click by default) to scan each product " +
				"on the cash register belt, you can hold it to continuously scan as you mouse over products.",
				patchInstanceDependency: Container<CheckoutAutoClickScanner>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);

			EnablePriceGunFix = configManagerControl.AddConfig(
				sectionName: miscModuleText,
				key: "Enable pricing gun double price fix",
				defaultValue: false,
				description: "Fixes customers sometimes complaining when prices are set to 200%." +
				"See the 0.8.2.0 changelog for a explanation on why I consider this a bug.",
				patchInstanceDependency: Container<PricingGunFixPatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			EnableExpandedProdOrderClickArea = configManagerControl.AddConfig(
				sectionName: miscModuleText,
				key: "Increased clickable area in product order",
				defaultValue: false,
				description: "When at the Manager Blackboard, you can click anywhere in the product panel " +
				"to add it to the shopping list, instead of just the \"+\" button.",
				patchInstanceDependency: Container<ExpandProductOrderClickArea>.Instance,
				modInstallSide: MultiplayerModInstallSide.Any
			);
		}
		private void InitializeNotificationModule() {
			string notificationsModuleText = "Notifications Module";

			EnableModNotifications = configManagerControl.AddConfig(
				sectionName: notificationsModuleText,
				key: $"Enable custom mod notifications {RequiresRestartSymbol}",
				defaultValue: true,
				description: "If enabled, you may receive custom notifications from this mod."
			);

			EnableWelcomeMessages = configManagerControl.AddConfig(
				sectionName: notificationsModuleText,
				key: $"Enable random welcome messages {RequiresRestartSymbol}",
				defaultValue: true,
				description: "When you load into a supermarket, there is a chance that you get a random welcoming message.",
				modInstallSide: MultiplayerModInstallSide.Any
			);

			EnableErrorMessages = configManagerControl.AddConfig(
				sectionName: notificationsModuleText,
				key: $"Enable error messages {RequiresRestartSymbol}",
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
				description: $"Shows a counter with the workload capacity used in the last cycle.\n" +
				$"This will only work while the {EmployeeJobFrequencyMode.Definition.Key} setting is " +
				$"in any of the \"Auto\" modes.",
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}
		
		private void InitializeEmployeePerformanceCustomSettingsModule() {
			string customModeText = $"∨ This setting is only used if \"{EmployeeJobFrequencyMode.Definition.Key}\" value is " +
				$"\"{EnumJobFrequencyMultMode.Auto_Custom.GetDescription()}\" ∨\n\nDefault values are the same as in the Balanced performance mode.\n";

			BalancedMode balancedMode = new();

			CustomAvgEmployeeWaitTarget = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Average wait time target",
				defaultValue: balancedMode.AvgEmployeeWaitTargetMillis,
				description: $"{customModeText}\n\n" +
				"Employees have limited \"turns\" to do its job, and end up waiting more or less depending on how much total work there is." +
				"This setting sets the wait time, in milliseconds, for the system to automatically balance workload " +
				"capacity, so the average wait time between all employees is kept as close as possible to this value.\n" +
				"Smaller values will make the system work harder to give more turns to employees and reduce wait times.",
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.AvgEmployeeWaitTarget.MinLimit, AutoModeLimits.AvgEmployeeWaitTarget.MaxLimit),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			CustomMinimumFrequencyMult = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Minimum workload capacity",
				defaultValue: balancedMode.MinFreqMult,
				description: $"{customModeText}\n\n" +
				"Sets the minimum possible workload capacity. When the job scheduler detects that the target wait time " +
				"is being sufficiently kept, it will start reducing workload capacity up to this value to save performance." +
				WorkloadCapcityDescription,
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.MinFreqMult.MinLimit, AutoModeLimits.MinFreqMult.MaxLimit),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			CustomMaximumFrequencyMult = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Maximum workload capacity",
				defaultValue: balancedMode.MaxFreqMult,
				description: $"{customModeText}\n\n" +
				"Sets the maximum possible workload capacity. When the job scheduler detects that the employee wait " +
				"time is higher than the target, it will start increasing workload capacity up to this value. " +
				"Be careful, since higher values can have a big impact on cpu usage by the employee AI.\n" +
				WorkloadCapcityDescription,
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.MaxFreqMult.MinLimit, AutoModeLimits.MaxFreqMult.MaxLimit),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
			
			CustomMaximumFrequencyReduction = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Maximum workload reduction per cycle",
				defaultValue: Math.Abs(balancedMode.DecreaseStep),
				description: $"{customModeText}\n\n" +
				"Sets how quickly the job scheduler can reduce workload capacity. Each cycle (1 second), it will reduce workload " +
				"capacity up to this quantity. The actual quantity will depend on how far below target is the employee avg wait timer.",
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.DecreaseStep.MinLimit, AutoModeLimits.DecreaseStep.MaxLimit),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			CustomMaximumFrequencyIncrease = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfCustomSettingsModuleText,
				key: "Maximum workload increase per cycle",
				defaultValue: balancedMode.IncreaseStep,
				description: $"{customModeText}\n\n" +
				"Sets how quickly the job scheduler can increase workload capacity. Each cycle (1 second), it will increase workload " +
				"capacity up to this quantity. The actual quantity will depend on how far above target is the employee avg wait timer.",
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.IncreaseStep.MinLimit, AutoModeLimits.IncreaseStep.MaxLimit),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}

		/*
		private void InitializeEmployeePerformanceManualSettingsModule() {
			string manualModeText = $"This setting is only used if \"{EmployeeJobFrequencyMode.Definition.Key}\" value is " +
				$"\"{EnumJobFrequencyMultMode.Manual.GetDescription()}\"";

			EmployeeJobFrequencyManualMultiplier = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfManualSettingsModuleText,
				key: "Employee workload capacity",
				defaultValue: 1f,
				description: $"{manualModeText}\n\n" +
				"This may have a noticeable performance hit depending on how high the value is, your system, and the size of your store. " +
				"It should be set to the lowest possible value where you stop noticing any employee slowdowns." +
				"A lower than 1 value will make them react slower than base game, but may reduce stuttering and improve performance.\n" +
				WorkloadCapcityDescription,
				acceptableValueRange: new AcceptableValueRange<float>(AutoModeLimits.MinFreqMult.MinLimit, AutoModeLimits.MaxFreqMult.MaxLimit),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);

			EmployeeJobManualMaxProcessTime = configManagerControl.AddConfigWithAcceptableValues(
				sectionName: PerfManualSettingsModuleText,
				key: "Job calculation time limiter",
				defaultValue: 0,
				description: $"{manualModeText}\n\n" +
				"The maximum amount of milliseconds allowed for employee job calculations, each internal update.\n" +
				"No matter how low this value is, at least one employee will do a job action each update, same as an unmodded game. " +
				$"This limiter is intended to safeguard against possible game stuttering caused by too high values in " +
				$"\"{EmployeeJobFrequencyManualMultiplier.Definition.Key}\".\n" +
				"A value of 0 disables this safeguard, and job calculations will take as much time as they need.",
				acceptableValueRange: new AcceptableValueRange<float>(0f, 15f),
				patchInstanceDependency: Container<EmployeePerformancePatch>.Instance,
				modInstallSide: MultiplayerModInstallSide.HostSideOnly
			);
		}
		*/

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
			bool debugConfigEnabled = false;

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

