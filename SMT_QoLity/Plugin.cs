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

			//Compare method signatures and log results
			StartMethodSignatureCheck().LogResultMessage(TimeLogger.LogTier.Debug, false, true);
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

		private void ConfigDebugModeHandler() {
#if !DEBUG
			if (ModConfig.Instance.IsDebugConfig()) {
				TimeLogger.DebugEnabled = true;
				TimeLogger.Logger.LogTimeWarning($"{MyPluginInfo.PLUGIN_NAME} Debug Dev mode enabled.", 
					LogCategories.Loading);
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
