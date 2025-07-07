using System;
using BepInEx;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Damntry.UtilsUnity.Components;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.ExternalMods;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Component;


namespace SuperQoLity {

	/*TODO 0 - POSSIBLE HUGE PERFORMANCE OPTIMIZATION
		This is an early alternative to the idea of hiding products that are not visible using AABB.
		Shelf labels have this gameobjeci hierarchy:

		Shelf
			Labels
				LabelSign
				LabelSign 2
				...

		Each LabelSign contains a component called LODGroup in charge of the lod levels. 
			Right now its just for dissapearing after a certain distance.
		So the question is, can I do the same thing at runtime for the products? Ideally there 
		would be a 2d object at max distance instead of just dissapearing, but how would I 
		automate 2d billboard creation, I have no idea.
	*/

	//TODO 1 - Find things Im loading on WorldStart and try to move them earlier if possible.
	//	There is noticeable lag when starting compared to base game, specially on big stores.

	//TODO 2 - Make a method that when debug is enabled and a certain hotkey is pressed, it gathers
	//	info about installed mods, the current LogOutput and what not, and copies it into the clipboard
	//	to help users make error reporting easier.

	//TODO 0 - Something I noticed is that since I commit to each github project separately, there
	//	is no good way of getting the entire source code from all projects. It has to be done manually,
	//	which was more or less a given, but what happens if I dont commit all of them at the same time?
	//	What if I commit twice in a short space of time on both projects but you want the earlier version?
	//	You would need to check each manually and its prone to errors.
	//	Other than commiting to all projects as a whole, think of some other solution.

	//Soft dependency so we load after ika.smtanticheat and BetterSMT if they exist.
	[BepInDependency(ModInfoSMTAntiCheat.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(ModInfoBetterSMT.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {

		/// <summary>
		/// Beginning digits of all generated AssetIds for this mod.
		/// Must be a value between 1 and 4293, both inclusive.
		/// The value must be unique within all installed mods, so if 
		/// another mod uses the same one, it will have to be changed.
		/// It is recommended to set the value like this in a property
		/// so it can be patched by another mod once this one is not
		/// maintained anymore.
		/// </summary>
		private static int AssetIdModSignature { get; } = 3071;

		public static bool IsSolutionInDebugMode = false;


		public void Awake() {
			//Init logger
			TimeLogger.InitializeTimeLogger<BepInExTimeLogger>(
				GameNotifications.RemoveSpecialNotifNewLinesForLog, false, MyPluginInfo.PLUGIN_NAME);

			//Early debug check so we can enable debugging if solution is in DEBUG
			SolutionDebugModeHandler();

			//Basic config pre-initialization for ConfigDebugModeHandler
			ModConfig.Instance.InitializeConfig(this);

			//Late debug check to read from the config
			ConfigDebugModeHandler();

			//Register patch containers.
			AutoPatcher.RegisterAllAutoPatchContainers();

			ModConfig.Instance.StartConfigBinding();

			//Init in-game notifications
			GameNotifications.Instance.InitializeGameNotifications();

			//Start hotkey system
			KeyPressDetection.InitializeAsync(() => FirstPersonController.Instance, () => !AuxUtils.IsChatOpen())
				.FireAndForget(LogCategories.Loading);

			NetworkSpawnManager.Initialize(AssetIdModSignature);

			TimeLogger.Logger.LogTimeDebug($"{MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) initialization finished.", LogCategories.Loading);

			//Start patching process of auto patch classes
			bool allPatchsOK = AutoPatcher.StartAutoPatcher();

			//BetterSMT_Helper.Instance.LogCurrentBetterSMTStatus(allPatchsOK);

			JobSchedulerManager.InitializeJobSchedulerEvents();
			RestockMatcher.Enable();
#if DEBUG
			//Compare method signatures and log results
			StartMethodSignatureCheck().LogResultMessage(LogTier.Warning, false, true);
#endif
			StartingMessage.InitStartingMessages(allPatchsOK);

			TimeLogger.Logger.LogTimeMessage($"Mod {MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) loaded {(allPatchsOK ? "" : "(not quite) ")}successfully.", LogCategories.Loading);
		}

		private void SolutionDebugModeHandler() {
#if DEBUG
			IsSolutionInDebugMode = true;
			TimeLogger.DebugEnabled = true;
			TimeLogger.Logger.LogTimeWarning("THIS BUILD IS IN DEBUG MODE.", LogCategories.Loading);
#endif
		}

		//TODO 1 - Separate logging from dev mode in a new setting.

		/// <summary>
		/// All of this is so I can have the debug mode enabled for the config binding itself.
		/// </summary>
		public static void ConfigDebugModeHandler() {
#if !DEBUG
			bool debugEnabled = ModConfig.Instance.IsDebugEnabledConfig();
			if (debugEnabled != TimeLogger.DebugEnabled) {
				TimeLogger.DebugEnabled = debugEnabled;
				TimeLogger.Logger.LogTimeWarning($"{MyPluginInfo.PLUGIN_NAME} " +
					$"Debug Dev mode {(debugEnabled ? "enabled" : "disabled")}.", LogCategories.Loading);
			}
#endif
		}

		public static void HookChangeDebugSetting() {
#if !DEBUG
			ModConfig.Instance.EnabledDevMode.SettingChanged += (_, _) => ConfigDebugModeHandler();
#endif
		}
		
		private CheckResult StartMethodSignatureCheck() {
			try {
				MethodSignatureChecker mSigCheck = new MethodSignatureChecker(this.GetType());

				mSigCheck.PopulateMethodSignaturesFromHarmonyPatches();

				//Locally replaced methods
				mSigCheck.AddMethod(typeof(NPC_Manager), "EmployeeNPCControl", [typeof(int)]);
				mSigCheck.AddMethod(typeof(NPC_Manager), "GetFreeStorageContainer", [typeof(int)]);
				mSigCheck.AddMethod(typeof(NPC_Manager), "MainRestockUpdate");
				mSigCheck.AddMethod(typeof(NPC_Manager), "CheckIfProdShelfWithSameProduct");
				mSigCheck.AddMethod(typeof(NPC_Manager), "GetRandomGroundBox");
				mSigCheck.AddMethod(typeof(NPC_Manager), "GetRandomGroundBoxAllowedInStorage");

				//Methods called by reflection
				mSigCheck.AddMethod(typeof(NPC_Manager), "DropBoxOnGround");
				mSigCheck.AddMethod(typeof(NPC_Manager), "UnequipBox");
				mSigCheck.AddMethod(typeof(NPC_Manager), "AttemptToGetRestPosition");
				mSigCheck.AddMethod(typeof(NPC_Manager), "CashierGetAvailableCheckout");
				mSigCheck.AddMethod(typeof(NPC_Manager), "UpdateEmployeeCheckouts");
				mSigCheck.AddMethod(typeof(NPC_Manager), "CheckIfCustomerInQueue");
				mSigCheck.AddMethod(typeof(NPC_Manager), "GetThiefTarget");
				mSigCheck.AddMethod(typeof(NPC_Manager), "IsFirstSecurityEmployee");
				mSigCheck.AddMethod(typeof(NPC_Manager), "GetClosestDropProduct");
				mSigCheck.AddMethod(typeof(NPC_Manager), "RetrieveCorrectPatrolPoint");
				mSigCheck.AddMethod(typeof(NPC_Manager), "UpdateEmployeeStats");

				return mSigCheck.StartSignatureCheck();
			}catch (Exception ex) {
				TimeLogger.Logger.LogTimeException(ex, LogCategories.MethodChk);
				return CheckResult.UnknownError;
			}
		}

	}
}
