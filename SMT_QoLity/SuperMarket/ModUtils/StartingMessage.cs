using System;
using Damntry.Utils.Logging;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;


namespace SuperQoLity.SuperMarket.ModUtils {

	public class StartingMessage {

		public enum MessageType { Welcome, Help }


		private static float welcomeMessageChance = 0.05f;  //5% chance

		private static Random rndMsg;

		private static bool pendingPatchError;
		

		static StartingMessage() {
			rndMsg = new Random();
		}


		public static void InitStartingMessages(bool allPatchsOK) {
			pendingPatchError = !allPatchsOK;
			
			WorldState.OnGameWorldChange += (ev) => SendWelcomeOnWorldStarted(ev);
		}

		private static void SendWelcomeOnWorldStarted(GameWorldEvent ev) {
			if (ev == GameWorldEvent.WorldStarted) {

				MessageType messageType = MessageType.Welcome;
				if (pendingPatchError) {
					pendingPatchError = false;
					messageType = MessageType.Help;
				}

				SendWelcomeMessage(messageType);
			}
		}

		private static void SendWelcomeMessage(MessageType messageType) {
			//TODO 6 - New stupid idea, just a message that says: "Dont press Alt + F4 now to exit the game"
			//	If they do it, when loading next time: "I told you not to press it"
			if (messageType == MessageType.Welcome && ModConfig.Instance.EnableWelcomeMessages.Value || 
					messageType == MessageType.Help && ModConfig.Instance.EnableErrorMessages.Value) {

				LogTier logLevel = LogTier.Fatal;
				if (messageType == MessageType.Welcome) {
					logLevel = LogTier.Message;

					if (welcomeMessageChance <= UnityEngine.Random.Range(0f, 1f)) {
						return;
					}
				}

				//Replace the \n char, to avoid having to manually use NewLineNotifSeparator directly in the string.
				string message = GetRandomMessage(messageType).Replace("\n", GameNotifications.NewLineNotifSeparator);

				TimeLogger.Logger.LogTime(logLevel, message, LogCategories.Loading, true);
			}
		}

		private static string GetRandomMessage(MessageType messageType) {
			return messageType switch {
				MessageType.Welcome => FormatMessageByType(superWelcomingMessages[rndMsg.Next(0, superWelcomingMessages.Length)], MessageType.Welcome),
				MessageType.Help => FormatMessageByType(superUsefulHelpMessages[rndMsg.Next(0, superUsefulHelpMessages.Length)], MessageType.Help),
				_ => throw new NotImplementedException($"The switch case {messageType} is not implemented."),
			};
		}

		private static string FormatMessageByType(string message, MessageType messageType) {
			return messageType switch {
				MessageType.Welcome => $"Supermarket tip of the day: \n\n{message}",
				MessageType.Help => $"An error ocurred in 1 or more SuperQolity patches.\n You can contact me (Damntry) on Discord at the \n(unofficial) Supermarket Together server. \n\n\nRandom Error Pro Tip: \n\n{message}",
				_ => throw new NotImplementedException($"The switch case {messageType} is not implemented."),
			};
		}

		private static string[] superWelcomingMessages = [
			"I dont think you are supposed to see this one.",
			"Here we are once again to make the customers pay.",
			"The ka-ching must go on.",
			"Dont forget to put your best customer service smile!    \n:(    \nNo, not like that.",
			"\"Please, remember to tie up your pets,\nand kids, outside the store. Thank you.\"",
			"^_^ $$$$$\n :-|   $$$\n :-(     $ \n T_T",
			"Customers cant hurt you.\nBut they want to.",
			"We cant process your request at this time. Queue time is:\n 1281 minutes.",
			"|~ #·?=@@)·´ç%... \n Fatal error while generating daily welcome quote. Please wait for a repair technician to fix it.",
			"Just... one more welcome message... only one more... \n this will be the last one... for sure...",
			"⚠🚨 AI DETECTED 🚨⚠. Please solve this Captcha to continue:      █████ [”REDACTED”] ██████████████████ \n █▚▌ ⬤   ▄█▀ ⬤ █▄ ▐▄█ ▀█▀ █ ⬤ █▚▌,\n  █▚▌ ⬤   █▬█ ⬤ ▐◣ █☰    █████ ",
			"There is something wrong in this game. I cant put my finger on it, but sometimes \n I feel like the employees are watchi... \n Oh, hey! Hello player! Welcome to Supermarket Together!",
			"I need every single shelf space filled with potatoes. Right now. Come on, make it happen!",
			"No welcome message, is a a good welcome message.         Too late.",
			"Im going to need another Ibuprofen. \"This is not an Ibuprophen endorsement. " +
				"I am not affiliated with any companies that sell Ibuprophen. Please read the instruction manual carefully, \n and ask your doctor if Ibuprophen is right for you\"",
			"Straighten that shrimp-shaped back.",
			"A long time ago \n in a supermarket far, far away \n\n STORE\nWARS",
			"This is not the greatest welcome message. \nThis is just a tribute",
			"Look ma, look!\n I made a mod!",
			"I would be doing this for a living, \nbut living costs money",
			"\"This supermarket would look better with some more \nbrooms near the entrances\" - Said nobody ever",
			"The secret to earning more money is simple:",
			"Sorry, Im feeling lazy, so you get this message",
			"These thiefs dont know whats coming to them *cocks broom*",
			"If you wanted to see every welcome message,\nyou would need to load into the game an average of...\nuh, let me get the calculator one sec...\nyeah ok so basically\n\na lot"
		];

		private static string[] superUsefulHelpMessages = [
			"Try burning some incense to ward off \n from evil spirits cursing the game.",
			"Maybe you should think about the life choices \n that brought you to this moment.",
			"From the dev => Must be someone else s fault. \n\n Not mine though.",
			"Entropy comes for us all.",
			"Dont despair. There are better mods out there than this.",
			"From the dev => If I knew this was going to happen, \n I would have done nothing to prevent it anyway.",
			"Keep restarting the game. Maybe on the 100th try it ll work.",
			"If you stare at this message for long enough, \n the problem will solve itself.",
			"From greener_capes => \"This developer sucks, \n why is he not fixing this already??!\"",
			"This might be a good moment to do your homework, \n or finish that task you have been ignoring for weeks.",
			"From anonymous1872 => \n \"This is ridiculous, these Pro Tips aint helpful at all!\"",
			"From the dev => Instead of adding features to the mod, \n I spent time doing these messages you might never see. \n\n Dont be like me.",
			"When boiling pasta, add a dash of salt \n and a bit of olive oil to the water.",
			"This is the golden Pro Tip that appears once every 10 years \n Share this Pro Tip to get 1000 years of terrible luck.",
			"Out of all the Pro Tips, this is the most useful.",
			"Please, contact your administrator to access this Pro Tip.",
			"From the dev => I could fix this... for a price. Unfortunately, even then I am too lazy.",
			"Every time you blame this mod, another Pro Tip gets added."
		];

	}
}
