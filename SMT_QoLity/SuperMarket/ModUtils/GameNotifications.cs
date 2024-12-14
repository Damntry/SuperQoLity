using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Tasks;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.Tasks.AsyncDelay;
using TMPro;
using UnityEngine;
using SuperQoLity.SuperMarket.Patches;
using static Damntry.Utils.Logging.TimeLogger;
using Cysharp.Threading.Tasks;


namespace SuperQoLity.SuperMarket.ModUtils {

	public class GameNotifications {

		public static GameNotifications Instance { get { return instance.Value; } }

		private static readonly Lazy<GameNotifications> instance = new Lazy<GameNotifications>(() => new GameNotifications());

		public const int MAX_MESSAGE_QUEUE = 5;

		private Queue<NotificationInfo> notificationQueue;

		//TODO 6 - Instead of a fixed value, I could make this a range and the system chooses based on character/word count.
		private int notificationFrequencyMilli = 4250;


		public bool NotificationSystemEnabled { get; private set; }

		//Use UniTaskDelay so notifications pause while the game is in the Escape menu.
		private CancellableSingleTask<UniTaskDelay> notificationTask;

		private object queueLock;


		private GameNotifications() {
			notificationTask = new CancellableSingleTask<UniTaskDelay>();
			NotificationSystemEnabled = false;
			queueLock = new object();
		}

		public void InitializeGameNotifications() {
			//Init in-game notifications
			if (ModConfig.Instance.EnableModNotifications.Value) {
				AddNotificationSupport();
			}

			GameWorldEventsPatch.OnGameWorldChange += (GameWorldEvent ev) => {
				if (ev == GameWorldEvent.Start) {
					if (GameNotifications.Instance.NotificationSystemEnabled) {
						//Even if notification support wasnt added above, we still hook onto the
						//	event since the user could enable notifications in the settings at any time.
						TryEnableNotificationsInGame();
					}
				} else if (ev == GameWorldEvent.Quit) {
					DisableShowingNotifications();
				}
			};

			ModConfig.Instance.EnableModNotifications.SettingChanged += ModConfig.Instance.NotificationsSettingsChanged;
		}

		public bool AddNotificationSupport() {
			if (!NotificationSystemEnabled) {
				var sendNotifActionLambda = (string msg, LogTier logLevel) => SendInGameNotification(msg, logLevel);
				AddGameNotificationSupport(sendNotifActionLambda, MyPluginInfo.PLUGIN_NAME);
			}

			return NotificationSystemEnabled = true;
		}

		public bool RemoveNotificationSupport() {
			if (NotificationSystemEnabled) {
				RemoveGameNotificationSupport();
			}

			return NotificationSystemEnabled = false;
		}

		public void TryEnableNotificationsInGame() {
			bool notificationsOk = false;

			try {
				bool notifObjOk = TestNotificationObjects();
				if (!notifObjOk) {
					//Notification objects are not working. Return so the finally handles everything.
					return;
				}

				//Enable showing notifications on screen
				notificationsOk = EnableShowingNotifications();

			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeExceptionWithMessage("Error while enabling game notifications.", ex, TimeLogger.LogCategories.Notifs);
			} finally {
				if (!notificationsOk) {
					TimeLogger.RemoveGameNotificationSupport();
				}
			}
		}

		public bool EnableShowingNotifications() {
			if (NotificationSystemEnabled) {
				notificationTask.StartTaskAsync(() => notificationConsumer(), "Notification Consumer", false).FireAndForget(LogCategories.Notifs);
				return true;
			} else {
				TimeLogger.Logger.LogTimeWarning("EnableShowingNotifications() was called but the notification system is not enabled. Notifications wont show.", LogCategories.Notifs);
				return false;
			}

		}

		public void DisableShowingNotifications() {
			notificationTask.StopTaskAndWaitAsync(10000).FireAndForgetCancels(LogCategories.Notifs);
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
				return NotificationSystemEnabled = false;
			}

			return true;
		}


		private async Task notificationConsumer() {
			while (!notificationTask.IsCancellationRequested) {
				NotificationInfo oldestNotification = null;

				lock (queueLock) {
					if (notificationQueue?.Count > 0) {
						oldestNotification = notificationQueue.Dequeue();
					}
				}

				if (oldestNotification != null) {
					ShowNotification(oldestNotification);
				}

				await UniTask.Delay(notificationFrequencyMilli, cancellationToken: notificationTask.CancellationToken);
			}
		}

		

		public void SendInGameNotification(string message, LogTier logLevel) {
			//Queue this notification. If the consumer loop is working, it ll be shown whenno message is pending.
			if (notificationQueue == null) {
				notificationQueue = new Queue<NotificationInfo>();
			} else if (notificationQueue.Count >= MAX_MESSAGE_QUEUE) {
				//TODO 5 - If the current logLevel is not already the lowest one, get the oldest, lowest LogLevel in the queue,
				//	and delete if its lower than the current one.
				//	But if the is not lower level to remove, maybe I should discard the oldest one of the same LogLevel?
				//	Also I should probably indicate that there are more notifications than the max allowed? So if a notification
				//	has ever been deleted, add to the end of the queue a message saying something like "More errors not shown. Check LogOutput.log"

				//Queue is full enough, Discard. Whatever error or warning it was has been at least logged already.
				TimeLogger.Logger.LogTimeInfo($"Notification queue already has the max limit of {notificationQueue.Count} messages waiting. Discarding this message.", LogCategories.Notifs);
				return;
			} else if (notificationQueue.Count > 0) {
				TimeLogger.Logger.LogTimeInfo($"Queueing notification. Notification queue had {notificationQueue.Count} messages waiting.", LogCategories.Notifs);
			}

			notificationQueue.Enqueue(new NotificationInfo(message, logLevel));
		}


		private void ShowNotification(NotificationInfo notifInfo) {
			GameObject notifObj = CreateNotificationObjetFromLogLevel(notifInfo.logLevel);
			notifObj.GetComponent<TextMeshProUGUI>().text = notifInfo.message;
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


		private record class NotificationInfo {
			public NotificationInfo(string message, LogTier logLevel) {
				this.message = message;
				this.logLevel = logLevel;
			}
			public string message { get; set; }
			public LogTier logLevel { get; set; }
		}

	}
}
