using BepInEx;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching;


namespace SuperQoLity {

	//Soft dependency so we load after ika.smtanticheat and BetterSMT if they exist.
	[BepInDependency(ModInfoSMTAntiCheat.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(ModInfoBetterSMT.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {

		//TODO 4 - Make it so building only copies the .pdb files if we are in Debug project configuration

		//TODO 3 - When warp mode the player might not even know why, if he got the config of someone else
		//	or he is not sure what he is doing. I should probably change the DEBUG into a "cheat" section
		//	where I have all these options that I consider to be beyond what is QoL, and the debug option
		//	would be in it too.

		//TODO 1 - Go through log uses that send notifications and see what to do with ones that are too long.
		//		Maybe I should go ahead with the star wars text thingy?


		public void Awake() {
			//Init logger
			TimeLogger.InitializeTimeLogger<BepInExTimeLogger>(false, MyPluginInfo.PLUGIN_NAME);
			
			//Register patch containers.
			AutoPatcher.RegisterAllAutoPatchContainers();

			//Init config
			ModConfig.Instance.InitializeConfig(this);

			DebugModeHandler();

			//Init in-game notifications
			GameNotifications.Instance.InitializeGameNotifications();

			BetterSMT_Helper.Instance.LogCurrentBetterSMTStatus();

			//Start patching process of enabled auto patch classes
			bool allPatchsOK = AutoPatcher.StartAutoPatcher();

			//Compare method signatures and log results
			StartMethodSignatureCheck().LogResultMessage(TimeLogger.LogTier.Debug, false, true);

			StupidMessageSendator2000_v16_MKII_CopyrightedName.SendWelcomeMessage(allPatchsOK);

			TimeLogger.Logger.LogTimeMessage($"Mod {MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) loaded {(allPatchsOK ? "" : "(not quite) ")}successfully.", TimeLogger.LogCategories.Loading);
		}

		private void DebugModeHandler() {
#if DEBUG
			TimeLogger.DebugEnabled = true;
			TimeLogger.Logger.LogTimeWarning("THIS BUILD IS IN DEBUG MODE.", TimeLogger.LogCategories.Loading);
#else
			if (ModConfig.Instance.EnabledDebug.Value) {
				TimeLogger.DebugEnabled = true;
				TimeLogger.Logger.LogTimeWarning("Debug mode enabled.", TimeLogger.LogCategories.Loading);
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
