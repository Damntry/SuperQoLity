using System;
using BepInEx;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsUnity.Components;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.ExternalMods;


namespace SuperQoLity {

	//TODO 1 - Find things Im loading on WorldStart and try to move them earlier if possible.
	//	There is noticeable lag when starting compared to base game, specially on big stores.

	//Soft dependency so we load after ika.smtanticheat and BetterSMT if they exist.
	[BepInDependency(ModInfoSMTAntiCheat.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(ModInfoBetterSMT.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {

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

			TimeLogger.Logger.LogTimeDebug($"{MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) initialization finished.", LogCategories.Loading);

			//Start patching process of auto patch classes
			bool allPatchsOK = AutoPatcher.StartAutoPatcher();

			//BetterSMT_Helper.Instance.LogCurrentBetterSMTStatus(allPatchsOK);
#if DEBUG
			//Compare method signatures and log results
			StartMethodSignatureCheck().LogResultMessage(LogTier.Debug, false, true);
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
				mSigCheck.AddMethod(typeof(NPC_Manager), "CheckIfShelfWithSameProduct");
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
