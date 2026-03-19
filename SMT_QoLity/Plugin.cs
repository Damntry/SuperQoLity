using BepInEx;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Damntry.UtilsUnity.Components.InputManagement;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.ExternalMods;
using SuperQoLity.SuperMarket.ModUtils.Messaging;
using SuperQoLity.SuperMarket.Standalone;
using SuperQoLity.SuperMarket.Standalone.MainMenuLogo;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityHotReloadNS;

namespace SuperQoLity {

    //Funny thing, I got the MeshCombiner error that some people where getting too. It happened
    //  once after trying to open the in game configuration menu while the game was loading.
    //  Nothing I can fix, this is just a reminder to tell people to disable any overlays (steam, discord)
    //  and try if that works.

    //TODO 1 - Find things Im loading on WorldLoaded and try to move them earlier if possible.
    //	There is noticeable lag when starting compared to base game, specially on big stores.

    //TODO 2 - Make a method that when debug is enabled and a user-defined hotkey from a Debug setting
    //	is pressed, it gathers info about installed mods, the current LogOutput and what not, and copies
    //	it into the clipboard for users to paste into github or wherever they make the bug report.

    //TODO 2 - Something I noticed is that since I commit to each github project separately, there
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

		public static bool IsSolutionInDebugMode { get; private set; } = false;


        public void Awake() {
            AssemblyResolver.Init();

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
            InputManagerSMT.Instance.InitializeAsync(() => FirstPersonController.Instance, () => !AuxUtils.IsChatOpen())
				.FireAndForget(LogCategories.Loading);

            NetworkSpawnManager.Initialize(AssetIdModSignature);

			TimeLogger.Logger.LogDebug($"{MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) initialization finished.", LogCategories.Loading);

            //Start patching process of auto patch classes
            bool allPatchsOK = AutoPatcher.StartAutoPatcher();

			//BetterSMT_Helper.Instance.LogCurrentBetterSMTStatus(allPatchsOK);

			CopyBuildableOnCursor.AddHotkeys();
			
			MainMenuLogos.StartProcess();

#if DEBUG
            //Compare method signatures and log results
            StartMethodSignatureCheck().LogResultMessage(LogTier.Warning, false, true);
            SetupHotReloading();
            MirrorDebugHeartbeat.Init();
#endif
            StartingMessage.InitStartingMessages(allPatchsOK);

            TimeLogger.Logger.LogMessage($"Mod {MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) loaded {(allPatchsOK ? "" : "(not quite) ")}successfully.", LogCategories.Loading);
        }


        private void SetupHotReloading() {
			const string sourceDllFilePath = $@"C:\Users\Damntry\Visual Studio Projects\Visual Studio 2019 Projects\repos\" +
				$@"!Supermarket Together\SupermarketTogether_SuperQoLity\SMT_QoLity\bin\Debug\net481\{MyPluginInfo.PLUGIN_NAME}.dll";

			InputManagerSMT.Instance.TryAddHotkey("HotReload", KeyCode.F5, [KeyCode.LeftControl], 
				InputState.KeyDown, HotkeyActiveContext.AlwaysActiveHighPrio,
                async () => {
					TimeLogger.Logger.LogMessageShowInGame($"Beginning {MyPluginInfo.PLUGIN_NAME} Hot Reloading", LogCategories.Other);
					await Task.Delay(100);
					UnityHotReload.LoadNewAssemblyVersion(this.GetType().Assembly, sourceDllFilePath);
					TimeLogger.Logger.LogMessageShowInGame($"Hot Reloading finished", LogCategories.Other);
                });
        }


        private void SolutionDebugModeHandler() {
#if DEBUG
			IsSolutionInDebugMode = true;
			TimeLogger.DebugEnabled = true;
			TimeLogger.Logger.LogWarning("THIS BUILD IS IN DEBUG MODE.", LogCategories.Loading);
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
				TimeLogger.Logger.LogWarning($"{MyPluginInfo.PLUGIN_NAME} " +
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
				TimeLogger.Logger.LogException(ex, LogCategories.MethodChk);
				return CheckResult.UnknownError;
			}
		}

	}
}
