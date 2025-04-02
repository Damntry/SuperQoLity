using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Tasks;
using Damntry.UtilsUnity.Tasks.AsyncDelay;
using TMPro;
using UnityEngine;
using static Damntry.Utils.Logging.TimeLogger;


namespace SuperQoLity.SuperMarket.ModUtils {

	public class GameNotifications {

		public static GameNotifications Instance { get { return instance.Value; } }

		private static readonly Lazy<GameNotifications> instance = new Lazy<GameNotifications>(() => new GameNotifications());


		private const byte MinSingleMessageLength = 6;

		private const byte MaxSingleMessageLength = 60;

		/// <summary>
		/// Multipart message separators in decreasing order of priority.
		/// </summary>
		private readonly string[] separatorPriority = [":", ". ", " ", "."];

		/// <summary>
		/// New line separator, splits the text exclusively for the notification text, otherwise its removed.
		/// The new line "\n" works as a new line character, but when you are logging a multiline 
		/// notification and still want a one liner in the logs, this separator can be used.
		/// </summary>
		public const string NewLineNotifSeparator = "´^¨";


		public static readonly int MAX_MESSAGE_QUEUE = 5;

		private Queue<NotificationInfo> notificationQueue;

		//TODO 2 - So what happens if the multipart notif takes longer than this and another notif shows up?
		/// <summary>Wait between notifications. Multipart notifications are counted as one.</summary>
		private readonly int notificationFrequencyMilli = 4250;

		private readonly int notifConsumerFrequency = 150;

		private readonly int minDelay = 900;

		private readonly int maxDelay = 3250;

		private bool notificationSystemEnabled;

		//Use UniTaskDelay so notifications pause while the game is in the Escape menu.
		private CancellableSingleTask<UniTaskDelay> notificationTask;

		private object queueLock;

		public bool NotificationsActive { get; private set; }


		/* TODO 3 - Code to modify the FSM controlled time that notifications show on screen.
			This changes the base values from the prefab that FSM will clone to make the notification, so
			it affects every notification from then on, but I can just do this on my manually
			instantiated notification prefab so I have a per-message setting.

			PlayMakerFSM playMakerAlpha = GameCanvas.Instance.notificationPrefab.GetComponents<PlayMakerFSM>()
				.FirstOrDefault(p => p.FsmName == "AlphaDelay");
			playMakerAlpha.FsmVariables.GetFsmFloat("InitDelay").Value = 1.25f;
			playMakerAlpha.FsmVariables.GetFsmFloat("Overlapping").Value = 0.75f;

			PlayMakerFSM playMakerImportantAlpha = GameCanvas.Instance.importantNotificationPrefab.GetComponents<PlayMakerFSM>()
				.FirstOrDefault(p => p.FsmName == "AlphaDelay");
			playMakerImportantAlpha.FsmVariables.GetFsmFloat("InitDelay").Value = 1.25f;
			playMakerImportantAlpha.FsmVariables.GetFsmFloat("Overlapping").Value = 0.75f;

			//This should be same or lower than InitDelay + Overlapping
			GameNotifications.notificationFrequencyMilli = 1000;
		*/

		private GameNotifications() {
			notificationTask = new CancellableSingleTask<UniTaskDelay>();
			notificationSystemEnabled = false;
			queueLock = new object();
		}

		public void InitializeGameNotifications() {
			//Init in-game notifications
			if (ModConfig.Instance.EnableModNotifications.Value) {
				AddNotificationSupport();
			}

			WorldState.OnGameWorldChange += (GameWorldEvent ev) => {
				if (ev == GameWorldEvent.WorldStarted) {
					if (GameNotifications.Instance.notificationSystemEnabled) {
						//Even if notification support wasnt added above, we still hook onto the
						//	event since the user could enable notifications in the settings at any time.
						TryEnableNotificationsInGame();
					}
				} else if (ev == GameWorldEvent.QuitOrMenu) {
					DisableShowingNotifications();
				}
			};

			ModConfig.Instance.EnableModNotifications.SettingChanged += NotificationsSettingsChanged;
		}

		public void NotificationsSettingsChanged(object sender, EventArgs e) {
			if (ModConfig.Instance.EnableModNotifications.Value) {
				GameNotifications.Instance.AddNotificationSupport();
			} else {
				GameNotifications.Instance.RemoveNotificationSupport();
			}
		}

		public bool AddNotificationSupport() {
			if (!notificationSystemEnabled) {
				var sendNotifActionLambda = (string msg, LogTier logLevel) => SendInGameNotification(msg, logLevel);
				AddGameNotificationSupport(sendNotifActionLambda, MyPluginInfo.PLUGIN_NAME);
			}

			return notificationSystemEnabled = true;
		}

		public bool RemoveNotificationSupport() {
			if (notificationSystemEnabled) {
				RemoveGameNotificationSupport();
			}

			return notificationSystemEnabled = false;
		}

		public void TryEnableNotificationsInGame() {
			bool notificationsOk = false;

			try {
				bool notifObjOk = TestNotificationObjects();
				if (!notifObjOk) {
					//GameNotification objects are not working. Return so the finally handles everything.
					return;
				}

				//Enable showing notifications on screen
				notificationsOk = EnableShowingNotifications();

			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeExceptionWithMessage("Error while enabling game notifications.", ex, LogCategories.Notifs);
			} finally {
				if (!notificationsOk) {
					TimeLogger.RemoveGameNotificationSupport();
				}
			}
		}

		public bool EnableShowingNotifications() {
			if (notificationSystemEnabled) {
				notificationTask.StartTaskAsync(() => NotificationConsumer(), "GameNotification Consumer", false).FireAndForget(LogCategories.Notifs);
				NotificationsActive = true;
				return true;
			} else {
				TimeLogger.Logger.LogTimeWarning("EnableShowingNotifications() was called but the notification system is not enabled. Notifications wont show.", LogCategories.Notifs);
				return false;
			}
		}

		public void DisableShowingNotifications() {
			notificationTask.StopTaskAndWaitAsync(10000).FireAndForgetCancels(LogCategories.Notifs);
			NotificationsActive = false;
		}

		public bool TestNotificationObjects() {
			//TODO 7 - Make an extra case where if only 1 of the notification objects doesnt work, we remember this to keep using only the working one.
			//Test that the notification objects exist and were initialized by the game.
			bool importantNotifExists = false;
			bool normalNotifExists = false;

			try {
				if (GameCanvas.Instance.importantNotificationPrefab != null && GameCanvas.Instance.importantNotificationParentTransform != null) {
					importantNotifExists = true;
				} else if (GameCanvas.Instance.notificationPrefab != null && GameCanvas.Instance.notificationParentTransform != null) {
					normalNotifExists = true;
				}
			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeExceptionWithMessage("Exception while accessing the game notification objects.", ex, LogCategories.Notifs);
			}

			if (!importantNotifExists && !normalNotifExists) {
				TimeLogger.Logger.LogTimeError("The prefab and transform objects required for notifications cant be used. " +
					"The mod notification system will be disabled.", LogCategories.Notifs);
				return notificationSystemEnabled = false;
			}

			return true;
		}


		private async Task NotificationConsumer() {
			while (!notificationTask.IsCancellationRequested) {
				NotificationInfo oldestNotification = null;
				int notificationDelay = notifConsumerFrequency;

				lock (queueLock) {
					if (notificationQueue?.Count > 0) {
						oldestNotification = notificationQueue.Dequeue();
					}
				}

				if (oldestNotification != null) {
					await ShowNotification(oldestNotification);
					notificationDelay = notificationFrequencyMilli;
				}

				await UniTask.Delay(notificationDelay, cancellationToken: notificationTask.CancellationToken);
			}
		}

		public void SendInGameNotification(string message, LogTier logLevel) {
			//Queue this notification. If the consumer loop is working, it ll be shown when no message is pending.
			if (notificationQueue == null) {
				notificationQueue = new Queue<NotificationInfo>();
			} else if (notificationQueue.Count >= MAX_MESSAGE_QUEUE) {
				//TODO 5 - If the current logLevel is not already the lowest one, get the oldest, lowest LogLevel in the queue,
				//	and delete if its lower than the current one.
				//	But if the is not lower level to remove, maybe I should discard the oldest one of the same LogLevel?
				//	Also I should probably indicate that there are more notifications than the max allowed? So if a notification
				//	has ever been deleted, add to the end of the queue a message saying something like "More errors not shown. Check LogOutput.log"

				//Queue is full enough, Discard. Whatever error or warning it was has been at least logged already.
				TimeLogger.Logger.LogTimeInfo($"GameNotification queue already has the max limit of {notificationQueue.Count} messages waiting. Discarding this message.", LogCategories.Notifs);
				return;
			} else if (notificationQueue.Count > 0) {
				TimeLogger.Logger.LogTimeInfo($"Queueing notification. GameNotification queue had {notificationQueue.Count} messages waiting.", LogCategories.Notifs);
			}

			notificationQueue.Enqueue(new NotificationInfo(message, logLevel));
		}


		private async Task ShowNotification(NotificationInfo notifInfo) {
			bool isMultipartMsg = IsMultipartMessage(notifInfo.message);

			if (isMultipartMsg) {
				await ShowNotificationMultipartMessage(notifInfo);
			} else {
				ShowNotificationMessage(notifInfo);
			}
		}

		private bool IsMultipartMessage(string message) {
			return message.Length > MaxSingleMessageLength || message.Contains("\n") || message.Contains(NewLineNotifSeparator);
		}

		private async Task ShowNotificationMultipartMessage(NotificationInfo notifInfo) {
			List<string> msgPartsList = SplitMessageParts(notifInfo.message);

			for (int i = 0; i < msgPartsList.Count; i++) {
				string msgPart = msgPartsList[i];
				ShowNotificationMessage(msgPartsList[i], notifInfo.logLevel);

				if (i == msgPartsList.Count - 1) {
					//Last loop. Do a smaller delay and exit, so the delay between
					//	notifications that comes after doesnt add up to too much.
					await UniTask.Delay(500);
					break;
				}

				//Calculate delay based on message length against the possible max length.
				int delay = (int)Mathf.Lerp(minDelay, maxDelay, msgPart.Length / (float)MaxSingleMessageLength);

				await UniTask.Delay(delay);
			}
		}

		private List<string> SplitMessageParts(string message) {
			List<string> msgPartsNewLineFull = new();
			List<string> msgPartsComplete = new();

			//Split new lines by the special notification character
			List<string> msgPartsNewLineNotif = message.Split([NewLineNotifSeparator], StringSplitOptions.None).ToList();

			//Further split each item, this time by the default new line character
			msgPartsNewLineNotif.ForEach((msgPart) => msgPartsNewLineFull.AddRange(msgPart.Split('\n')));

			//Split each individual string if it exceeds the length limit.
			msgPartsNewLineNotif.ForEach((msgPart) => msgPartsComplete.AddRange(GetExceededCharsMultipartMessage(msgPart.Trim())));

			return msgPartsComplete;
		}

		private List<string> GetExceededCharsMultipartMessage(string message) {
			if (message.Length <= MaxSingleMessageLength) {
				return [message];
			}

			List<string> msgPartsList = new();
			int currentIndexPos = 0;
			int lengthLeft = message.Length;

			//End the search before reaching the point where a message part would be too short.
			int indexSearchCount = MaxSingleMessageLength - MinSingleMessageLength;

			while (lengthLeft > MaxSingleMessageLength) {
				//Since we search backwards for a cut off point, get the last splitPointIndex from which we ll start searching.
				int lastIndexSearchBegin = currentIndexPos + MaxSingleMessageLength;

				int splitPointIndex = FindSeparatorSplitPoint(message, lastIndexSearchBegin, indexSearchCount);

				int partLength;
				if (splitPointIndex != -1) {
					//Split string at the found separator position.
					partLength = splitPointIndex - currentIndexPos;
				} else {
					//Couldnt find char in a valid position, just split at max allowed length.
					partLength = MaxSingleMessageLength;
				}

				msgPartsList.Add(message.Substring(currentIndexPos, partLength).Trim());

				currentIndexPos += partLength;
				lengthLeft = message.Length - currentIndexPos - 1;
			}

			//Add whatever text is left
			msgPartsList.Add(message.Substring(currentIndexPos, lengthLeft + 1).Trim());

			return msgPartsList;
		}

		private int FindSeparatorSplitPoint(string message, int lastIndexSearchBegin, int indexSearchCount) {
			int splitPointIndex = -1;

			foreach (string separator in separatorPriority) {
				//Find a split point by searching backwards for the last instance 
				//	of this separator character, within the allowed range.
				splitPointIndex = message.LastIndexOf(separator, lastIndexSearchBegin, indexSearchCount);
				if (splitPointIndex != -1) {
					//Split right after the found separator.
					splitPointIndex += separator.Length;
					break;
				}
			}

			return splitPointIndex;
		}

		private void ShowNotificationMessage(NotificationInfo notifInfo) {
			ShowNotificationMessage(notifInfo.message, notifInfo.logLevel);
		}

		private void ShowNotificationMessage(string message, LogTier logLevel) {
			GameObject notifObj = CreateNotificationObjetFromLogLevel(logLevel);
			notifObj.GetComponent<TextMeshProUGUI>().text = message;
			notifObj.SetActive(true);
		}

		/// <summary>Returns the appropiate GameNotificationType for a given LogTier</summary>
		private GameObject CreateNotificationObjetFromLogLevel(LogTier logLevel) {
			GameObject notificationGameObj;

			if (logLevel == LogTier.Warning || logLevel == LogTier.Error || logLevel == LogTier.Fatal || logLevel == LogTier.All) {
				notificationGameObj = UnityEngine.Object.Instantiate(GameCanvas.Instance.importantNotificationPrefab, GameCanvas.Instance.importantNotificationParentTransform);
			} else {
				notificationGameObj = UnityEngine.Object.Instantiate(GameCanvas.Instance.notificationPrefab, GameCanvas.Instance.notificationParentTransform);
			}

			return notificationGameObj;
		}

		/// <summary>
		/// Implementation of the delegate <see cref="TimeLogger.PreprocessMessageFunc"/>.
		/// Removes the special character used in notifications to denote a new line, so it doesnt show in the log file.
		/// This method is sent to the TimeLogger as a preprocess that will be used only for file logging, so the 
		/// notification will still receive original message with special characters.
		/// This way there is no need to manually send different texts for logs and notifications in two different calls.
		/// </summary>
		public static string RemoveSpecialNotifNewLinesForLog(string text, LogTier logLevel,
			LogCategories category, bool showInGameNotification, PreprocessType preprocType) {
			//Skip the expensive Contains check if notifications are not meant to be shown for
			//	this log, which means that the text "shouldnt" have the new line separators.
			if (preprocType == PreprocessType.FileLogging && showInGameNotification &&
					text.Contains(GameNotifications.NewLineNotifSeparator)) {
				text = text.Replace(GameNotifications.NewLineNotifSeparator, "");
			}

			return text;
		}


		private record NotificationInfo(string message, LogTier logLevel);

	}
}
