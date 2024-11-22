using BepInEx;
using Damntry.Utils;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching;


namespace SuperQoLity {

	//Soft dependency so we load after BetterSMT if it exists.
	[BepInDependency(BetterSMT_Helper.BetterSMTInfo.GUID_NEW, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(BetterSMT_Helper.BetterSMTInfo.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {

		//TODO 4 - TEST IN MULTIPLAYER


		public void Awake() {
			//Init logger
			BepInExTimeLogger.InitializeTimeLogger(MyPluginInfo.PLUGIN_NAME, false);
			GlobalConfig.Logger = BepInExTimeLogger.Logger;

			//Register patch containers.
			AutoPatcher.RegisterAllAutoPatchContainers();

			//Init config
			ModConfig.Instance.InitializeConfig(this);

			DebugModeHandler();

			//Init in-game notifications
			GameNotifications.Instance.InitializeGameNotifications();

			BetterSMT_Helper.LogCurrentBetterSMTStatus();

			//Start patching process of enabled auto patch classes
			bool allPatchsOK = AutoPatcher.StartAutoPatcher();

			//Compare method signatures and log results
			StartMethodSignatureCheck().LogResultMessage(TimeLoggerBase.LogTier.Debug, false, true);

			StupidMessageSendator2000_v16_MKII_CopyrightedName.SendWelcomeMessage(allPatchsOK);

			BepInExTimeLogger.Logger.LogTime(TimeLoggerBase.LogTier.Message, $"Mod {MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) loaded {(allPatchsOK ? "" : "(not quite) ")}successfully.", TimeLoggerBase.LogCategories.Loading);
		}

		private void DebugModeHandler() {
#if DEBUG
			BepInExTimeLogger.DebugEnabled = true;
			BepInExTimeLogger.Logger.LogTimeWarning("THIS BUILD IS IN DEBUG MODE.", TimeLoggerBase.LogCategories.Loading);
#else
			if (ModConfig.Instance.EnabledDebug.Value) {
				BepInExTimeLogger.DebugEnabled = true;
				BepInExTimeLogger.Logger.LogTimeWarning("Debug mode enabled.", TimeLoggerBase.LogCategories.Loading);
			}
#endif
		}

		private CheckResult StartMethodSignatureCheck() {
			MethodSignatureChecker mSigCheck = new MethodSignatureChecker(this.GetType());

			mSigCheck.PopulateMethodSignaturesFromHarmonyPatches();

			//Add manual patches and locally replaced methods.
			mSigCheck.AddMethodSignature(typeof(NPC_Manager), "GetFreeStorageContainer", [typeof(int)]);
			mSigCheck.AddMethodSignature(typeof(NPC_Manager), "CheckProductAvailability");

			return mSigCheck.StartSignatureCheck();
		}

	}
}
