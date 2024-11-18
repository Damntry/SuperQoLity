using BepInEx;
using Damntry.Utils;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using SuperQoLity.SuperMarket.ModUtils;


namespace SuperQoLity {

	/*
		Oh fuck, they implemented this:
			"new prop under the devices menu tab: the Mini Transport Vehicle. It is rideable by players and allow 6 boxes 
			to be carried on it. Since it is its first implementation there could be problems that will be solved over time."

		Its kinda like a new storage? It would need highlighting. Other than that it should be fine but I ve no idea how it ll interfere with stuff.
		
		Also, check code differences from this patch to the previous.

	*/

	//TODO 1 - When you start the game without configuration manager, it fucking fails. Wtf did I do.

	//TODO 1 - This is not really a TODO to implement it now, but to add to the external todo list.
	//	Apparently having more than 10 employees slows down the game considerably due to "spaguetti
	//	code". I bet I could fix it myself so maybe I should go for that next since it seems to be pretty important.
	//	I would use my trusty performance thingy to measure stuff.


	//Soft dependency so we load after BetterSMT if it exists.
	[BepInDependency(BetterSMT_Helper.BetterSMTInfo.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {

		//TODO 4 - TEST IN MULTIPLAYER

		private void Awake() {
			//Init logger
			BepInExTimeLogger.InitializeTimeLogger(MyPluginInfo.PLUGIN_NAME);
			GlobalConfig.Logger = BepInExTimeLogger.Logger;

			//Register patch containers.
			AutoPatcher.RegisterAllAutoPatchContainers();

			//Init config
			ModConfig.Instance.InitializeConfig(this);

			//Init in-game notifications
			GameNotifications.Instance.InitializeGameNotifications();

			BetterSMT_Helper.LogCurrentBetterSMTStatus();

			//TODO 5 - Make a system that gets the hashcode of all the methods I patch, and compares it to previous hashcodes so if it is
			//		different, it warns me that something changed and I should check it out. Log it so only I can see it but being very noticeable,
			//		preferably showing it in game too.
			//	Add too methods that I am overwriting locally, like CheckProductAvailability

			//Initialize method signature checker
			MethodSignatureChecker signatureChecker = new(this.GetType());
			signatureChecker.AddMethodSignature(typeof(GameData), "Update");

			//Start patching process of enabled auto patch classes
			bool allPatchsOK = AutoPatcher.StartAutoPatcher(signatureChecker);

			//Start comparing method signatures 
			signatureChecker.StartSignatureCheck();

			StupidMessageSendator2000_v16_MKII_CopyrightedName.SendWelcomeMessage(allPatchsOK);

			BepInExTimeLogger.Logger.LogTime(TimeLoggerBase.LogTier.Message, $"Mod {MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) loaded {(allPatchsOK ? "" : "(not quite) ")}successfully.", TimeLoggerBase.LogCategories.Loading);
		}


	}
}
