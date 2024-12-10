using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using Damntry.Utils.Tasks;
using Damntry.Utils.Tasks.AsyncDelay;
using Damntry.UtilsBepInEx.Configuration;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsUnity.Tasks.AsyncDelay;
using Damntry.UtilsUnity.Timers;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;
using UnityEngine.AI;

namespace SuperQoLity.SuperMarket.Patches.EmployeeModule {


	/// <summary>
	/// Employees move faster while the store is closed and no customers are in it with pending actions. 
	/// Works with betterSMT perk speed increase.
	/// </summary>
	public class EmployeeWalkSpeedPatch : FullyAutoPatchedInstance {

		//TODO 3 - Unrelated but I found something in CustomerNPCControl. When an employee starts going after a thief,
		//	it increases the employee speed by 25%. But then there is nothing restoring that speed. So theoretically, if you
		//	let the employee try and catch multiple thiefs without changing its work assignation (which resets the speed), it
		//	will keep increasing its speed 25% on each new thief.

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee speed while closed failed. Disabled";

		private static MethodInfo updateEmployeeStatsMethod;

		private static float accelerationBase = 0f;
		private static float angularSpeedBase = 0f;

		public static bool IsEmployeeSpeedIncreased { get; private set; }

		private static CancellableSingleTask<UniTaskDelay> storeClosedTask = new CancellableSingleTask<UniTaskDelay>();

		private static bool checkCustomers;



		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				updateEmployeeStatsMethod = AccessTools.Method(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats));
				//No need to check null. If it were the case, UpdateEmployeeStatsPostFix patch would have failed before reaching this point.

				//Whenever the slider of the setting ClosedStoreEmployeeWalkSpeedMultiplier moves, we wait for a bit
				//	before applying the changes, to avoid possibly spamming calls to UpdateEmployeeStats.
				DelayedThreadedSingleTask<AsyncDelay> delayTask = new(() => {
					if (NPC_Manager.Instance != null) {	//Null when game hasnt started. We can skip since the setting will also be read while loading game world.
						updateEmployeeStatsMethod.Invoke(NPC_Manager.Instance, null);
					}
				});

				ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.ConfigFile.SettingChanged += 
					(object sender, SettingChangedEventArgs args) => delayTask.Start(1000);
			}
		}


		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats))]
		[HarmonyPriority(Priority.VeryLow)]	//So this patch runs after other postfixes from mods that might modify movement values.
		[HarmonyPostfix]
		private static void UpdateEmployeeStatsPostfix(NPC_Manager __instance) {
			foreach (object obj in __instance.employeeParentOBJ.transform) {
				Transform transform = (Transform)obj;
				NavMeshAgent npcNavMesh = transform.GetComponent<NavMeshAgent>();

				if (GameData.Instance.isSupermarketOpen || (checkCustomers && HasCustomersWithPendingActions())) {
					IsEmployeeSpeedIncreased = false;
					npcNavMesh.acceleration = accelerationBase;
					npcNavMesh.angularSpeed = angularSpeedBase;
				} else {
					IsEmployeeSpeedIncreased = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value > 1;

					//Multiply over the value already set in UpdateEmployeeStats()
					npcNavMesh.speed *= ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value;

					if (accelerationBase == 0f || angularSpeedBase == 0f) {
						//Since acceleration and turn radius are not set in UpdateEmployeeStats, we cant just
						//	multiply them, or every call to this method would increase them exponentially.
						//	We save the current base value so we can support any mods that change it in
						//	UpdateEmployeeStats or before, and then later below apply the multiplier over the base value.
						//	This doesnt support if the values are being changed dynamically though.
						accelerationBase = npcNavMesh.acceleration;
						angularSpeedBase = npcNavMesh.angularSpeed;
					}

					//Avoid increasing these if we already did so. Eventually when I rework the AI, this
					//	will become a property in the NPC object to flag if it is already applied.
					if (accelerationBase == npcNavMesh.acceleration) {
						//TODO 5 - Make these 2 options below into an extra advanced setting, in
						//	case something changes in the future and it needs extra tunning.
						npcNavMesh.acceleration = accelerationBase * (1 + (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 0.80f));
						npcNavMesh.angularSpeed = angularSpeedBase * (1 + (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 25000f));
					}
				}

				//If false, it makes them stop on its tracks when they reach their destination, instead of
				//	overshooting if their acceleration is not high enough. Though its a bit too sudden.
				npcNavMesh.autoBraking = !IsEmployeeSpeedIncreased;
			}
		}

		/// <summary>
		/// Update speeds when the value of the property NetworkisSupermarketOpen is set.
		/// </summary>
		[HarmonyPatch(typeof(GameData), nameof(GameData.NetworkisSupermarketOpen), MethodType.Setter)]
		[HarmonyPostfix]
		private static async void NetworkisSupermarketOpenPatch() {
			//Since the user could manually close the store and start a new day before customers leave or the 
			//	safety timeout is over, we make it so if there is a previous call to this method still ongoing,
			//	we cancel it since its not relevant anymore, and then let the current one proceed normally.
			if (storeClosedTask.IsTaskRunning) {
				await storeClosedTask.StopTaskAndWaitAsync();
			}

			//In UpdateEmployeeStatsPostfix we only want to do the customer check if the store closes and its being called from this setter call.
			checkCustomers = !GameData.Instance.isSupermarketOpen;

			if (!GameData.Instance.isSupermarketOpen) {
				await storeClosedTask.StartAwaitableTaskAsync(CheckCustomersPendingActions, "Wait for end of customers actions", true);
				checkCustomers = false;
			}

			if (storeClosedTask.IsCancellationRequested) {
				return;
			}

			updateEmployeeStatsMethod.Invoke(NPC_Manager.Instance, []);
		}

		private static async Task CheckCustomersPendingActions() {
			//Safety timeout in case the check in HasCustomersWithPendingActions becomes outdated and never returns true.
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

		private static bool HasCustomersWithPendingActions(CancellationToken cancelToken = default(CancellationToken)) {
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

		public static bool IsWarpingEnabled() {
			float maxWalkSpeedMultValue = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Description.AcceptableValues.GetMaxValue<float>();

			return EmployeeWalkSpeedPatch.IsEmployeeSpeedIncreased && ModConfig.Instance.EnabledDebug.Value
				&& ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value == maxWalkSpeedMultValue;
		}

	}
}
