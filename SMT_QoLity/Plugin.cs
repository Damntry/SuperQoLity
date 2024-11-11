using System;
using BepInEx;
using Damntry.Utils;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;
using System.Collections.Generic;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;


namespace SuperQoLity {

	/*
		Oh fuck, they implemented this:
			"new prop under the devices menu tab: the Mini Transport Vehicle. It is rideable by players and allow 6 boxes 
			to be carried on it. Since it is its first implementation there could be problems that will be solved over time."

		Its kinda like a new storage? It would need highlighting. Other than that it should be fine but I ve no idea how it ll interfere with stuff.
		
		Also, check code differences from this patch to the previous.

	*/


	//Soft dependency so we load after BetterSMT if it exists.
	[BepInDependency(BetterSMT_Helper.BetterSMTInfo.GUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin {

		//TODO 4 - TEST IN MULTIPLAYER

		private void Awake() {
			//Init logger
			BepInExTimeLogger.InitializeTimeLogger(MyPluginInfo.PLUGIN_NAME);
			GlobalConfig.Logger = BepInExTimeLogger.Logger;

			//Init config
			ModConfig.Instance.InitializeConfig(this);

			//Init in-game notifications
			GameNotifications.Instance.InitializeGameNotifications();
			//TODO 5 - Make a system that gets the hashcode of all the methods I patch, and compares it to previous hashcodes so if it is
			//		different, it warns me that something changed and I should check it out. Log it so only I can see it but being very noticeable,
			//		preferably showing it in game too.

			bool allPatchsOK = AutoPatcher.StartAutoPatcher();
			if (allPatchsOK) {
				BepInExTimeLogger.Logger.LogTime(TimeLoggerBase.LogTier.Message, $"Supermarket tip of the day: {GetRandomWelcomeMessage()}", TimeLoggerBase.LogCategories.Loading);
			} else {
				BepInExTimeLogger.Logger.LogTimeFatal($"Random Error Pro Tip: {GetRandomHelpMessage()}", TimeLoggerBase.LogCategories.Loading);
			}

			BepInExTimeLogger.Logger.LogTime(TimeLoggerBase.LogTier.Message, $"Mod {MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_GUID}) loaded {(allPatchsOK ? "" : "(not quite) ")}successfully.", TimeLoggerBase.LogCategories.Loading);
		}

		//TODO 5 - Maybe I could show this as a notification when you start the game? It would need a setting, disabled by default, but also long texts might look
		//		illegible. I was thiking that in the log consumer, I could automatically split long texts into an array of fixed length strings, and I could
		//		queue them all up and show kinda like the star wars rolling. The text would have to ve very short to make each line fast to read before the next
		//		one shows up, and I should try and make cuts after periods, but thats going to be a lot more work to calculate.

		private string GetRandomWelcomeMessage() {
			List<string> superWelcomingMessages = new List<string>();
			superWelcomingMessages.Add("I dont think you are supposed to see this one.");
			superWelcomingMessages.Add("Here we are once again to make the customers pay.");
			superWelcomingMessages.Add("The ka-ching must go on.");
			superWelcomingMessages.Add("Dont forget to put your best customer service smile    :(    No not like that.");
			superWelcomingMessages.Add("\"Please remember to tie up your pets and kids outside the store.\"");
			superWelcomingMessages.Add(" :-D    :-)    :-|    :-(    T_T    💀");
			superWelcomingMessages.Add("Customers cant hurt you. But they want to.");
			superWelcomingMessages.Add("We cant process your request at this time. Queue time is: 1281 minutes.");
			superWelcomingMessages.Add("|~ #·?=@@)·´ç%... Fatal error while generating daily welcome quote. Please wait for a repair technician to fix it.");
			superWelcomingMessages.Add("Just... one more welcome message... only one more... this will be the last one... for sure...");
			superWelcomingMessages.Add("⚠🚨 AI DETECTED 🚨⚠. Please solve this Captcha to continue:      █████ [REDACTED]     █▚▌ ⬤   ▄█▀ ⬤ █▄ ▐▄█ ▀█▀ █ ⬤ █▚▌,  █▚▌ ⬤   █▬█ ⬤ ▐◣ █☰    █████ ");
			superWelcomingMessages.Add("There is something wrong in this game. I cant put my finger on it, but sometimes I feel like my own employees are watching me... Oh, hey! Hello player! Welcome to Supermarket Together!!");
			superWelcomingMessages.Add("I need every single shelf space filled with potatoes. Right now. Come on, make it happen!");
			superWelcomingMessages.Add("No welcome message is a a good welcome message.         Too late.");
			superWelcomingMessages.Add("Im going to need another Ibuprofen. \"This is not an Ibuprophen endorsement. " +
				"I am not affiliated with any companies that sell Ibuprophen. Please read the instruction manual carefully and ask your doctor if Ibuprophen is right for you\"");

			//TODO 5 - Add more welcome messages

			return superWelcomingMessages[new Random().Next(0, superWelcomingMessages.Count)];
		}

		private string GetRandomHelpMessage() {
			List<string> superUsefulHelpMessages = new List<string>();
			superUsefulHelpMessages.Add("Try burning some incense to ward off from evil spirits cursing the game.");
			superUsefulHelpMessages.Add("Maybe you should think about the life choices that brought you to this moment.");
			superUsefulHelpMessages.Add("Are you sure this isnt your fault, like everything else you touch?");
			superUsefulHelpMessages.Add("From the dev => Must be someone else s fault. Not mine though.");
			superUsefulHelpMessages.Add("Entropy comes for us all.");
			superUsefulHelpMessages.Add("Dont despair. There are better mods out there than this.");
			superUsefulHelpMessages.Add("From the dev => If I knew this was going to happen, I would have done nothing to prevent it anyway.");
			superUsefulHelpMessages.Add("Keep restarting the game. Maybe on the 100th try it ll work.");
			superUsefulHelpMessages.Add("If you stare at this message for long enough, the problem will solve itself.");
			superUsefulHelpMessages.Add("From greener_capes => \"This developer sucks, why is he not fixing this already??!\"");
			superUsefulHelpMessages.Add("This might be a good moment to do your homework, or finish that task you have been ignoring for weeks.");
			superUsefulHelpMessages.Add("From anonymous1872 => \"This is ridiculous, this Pro Tips aint helful at all!\".");
			superUsefulHelpMessages.Add("From the dev => Instead of adding useful features to the mod, I spent time doing these messages you might never see. Dont be like me.");
			superUsefulHelpMessages.Add("When boiling pasta, add a dash of salt and a bit of olive oil to the water.");
			superUsefulHelpMessages.Add("This is the golden Pro Tip that only appears once every 10 years. Share this Pro Tip to get 1000 years of terrible luck.");
			superUsefulHelpMessages.Add("Out of all the Pro Tips, this is the most useful.");
			superUsefulHelpMessages.Add("Please, contact your administrator to access this Pro Tip.");
			superUsefulHelpMessages.Add("From the dev => I could fix this... for a price. Unfortunately I am very lazy.");
			superUsefulHelpMessages.Add("Every time you blame this mod, another pro tip gets added.");

			return superUsefulHelpMessages[new Random().Next(0, superUsefulHelpMessages.Count)];
		}

	}
}
