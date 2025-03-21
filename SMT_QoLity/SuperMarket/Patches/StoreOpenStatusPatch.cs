using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.Utils.Tasks;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Damntry.UtilsUnity.Tasks.AsyncDelay;
using Damntry.UtilsUnity.Timers;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches {


	/// <summary>
	/// Handles sending an event when the store opens, and when it closes and the last customer starts leaving.
	/// </summary>
	public class StoreOpenStatusPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled =>
			ModConfig.Instance.EnableEmployeeChanges.Value || ModConfig.Instance.EnableTransferProducts.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Store opening state detection patch failed. Disabled";


		private static CancellableSingleTask<UniTaskDelay> storeClosedTask = new CancellableSingleTask<UniTaskDelay>();

		private static SemaphoreSlim semaphoreLock = new SemaphoreSlim(1, 1);


		public static event Action<bool> OnSupermarketOpenStateChanged;


		public override void OnPatchFinishedVirtual(bool IsActive) {
			NetworkSpawnManager.RegisterNetwork<StoreStatusNetwork>(918219);
		}

		[HarmonyPatch(typeof(GameData), nameof(GameData.NetworkisSupermarketOpen), MethodType.Setter)]
		[HarmonyPriority(Priority.High)]    //Just in case some other mod prefixes this and returns false for some reason
		[HarmonyPrefix]
		private static bool NetworkisSupermarketOpenPrefixPatch(ref bool __state) {
			//Save previous value, so in the postfix we can compare if it changed.
			__state = GameData.Instance.isSupermarketOpen;
			return true;
		}

		/// Sends an event when the supermarket opens, and when it closes after the last customer starts leaving.
		/// </summary>
		[HarmonyPatch(typeof(GameData), nameof(GameData.NetworkisSupermarketOpen), MethodType.Setter)]
		[HarmonyPostfix]
		private static async void NetworkisSupermarketOpenPostfixPatch(bool __state) {
			if (GameData.Instance.isSupermarketOpen == __state) {
				return; //The supermarket open status didnt change.
			}

			//Since the user could manually start a new day before customers leave or the safety timeout is
			//	over, we make it so if there is a previous call to this method still ongoing, we cancel
			//	 it to not wait anymore, and once its over, we let the new call proceed normally.
			if (storeClosedTask.IsTaskRunning) {
				await storeClosedTask.StopTaskAndWaitAsync();
			}

			//We only need this for when there was a existing call than we want to wait to finish,
			//	but we always do it anyway since there is no point in Releasing conditionally.
			await semaphoreLock.WaitAsync();    //Wait so event cant be sent in the wrong order.

			try {
				bool IsOpen = true;
				if (!GameData.Instance.isSupermarketOpen) {
					await storeClosedTask.StartAwaitableTaskAsync(CheckCustomersPendingActions, "Wait for end of customers actions", true);
					TimeLogger.Logger.LogTimeDebugFunc(() => "All customers are leaving or already left the supermarket.", LogCategories.Task);
					IsOpen = false;
				}

				StoreStatusNetwork.IsStoreOpenOrCustomersInsideSync.Value = IsOpen;
				if (OnSupermarketOpenStateChanged != null) {
					OnSupermarketOpenStateChanged(StoreStatusNetwork.IsStoreOpenOrCustomersInsideSync);
				}
			} finally {
				semaphoreLock.Release();
			}
		}

		private static async Task CheckCustomersPendingActions() {
			//Safety timeout in case the code in HasCustomersWithPendingActions becomes outdated and never returns true.
			UnityTimeStopwatch safetyTimeout = UnityTimeStopwatch.StartNew();
			try {
				//Delay continuing until HasCustomersWithPendingActions() returns false, so
				//	UpdateEmployeeStatsPostFix runs when it can increase their speed.
				while (HasCustomersWithPendingActions(storeClosedTask.CancellationToken) && safetyTimeout.ElapsedSeconds < 180) {
					await UniTask.Delay(500, cancellationToken: storeClosedTask.CancellationToken);
				}
			} catch (OperationCanceledException) {
				return; //Exit and let the 2º call do its logic.
			}
		}

		private static bool HasCustomersWithPendingActions(CancellationToken cancelToken = default) {
			//Check if there are any customers in a state where they still have things to do in the supermarket.
			foreach (Transform customerObj in NPC_Manager.Instance.customersnpcParentOBJ.transform) {
				cancelToken.ThrowIfCancellationRequested();

				NPC_Info customerInfo = customerObj.gameObject.GetComponent<NPC_Info>();
				if (customerInfo.state < 99) {   //99 = Leaving for the map exit
					return true;    //A customer still has stuff to do.
				}
			}

			return false;
		}

	}

}
