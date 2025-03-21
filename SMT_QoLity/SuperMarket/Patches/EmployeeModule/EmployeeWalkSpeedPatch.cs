using System;
using Damntry.Utils.Logging;
using Damntry.Utils.Tasks;
using Damntry.Utils.Tasks.AsyncDelay;
using Damntry.UtilsBepInEx.Configuration;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using UnityEngine;
using UnityEngine.AI;

namespace SuperQoLity.SuperMarket.Patches.EmployeeModule {


	/// <summary>
	/// Employees move faster while the store is closed and no customers are in it with pending actions. 
	/// Works with betterSMT perk speed increase.
	/// </summary>
	public class EmployeeWalkSpeedPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee speed while closed failed. Disabled";

		private static bool warpNotifyDone;


		public static float WalkSpeedMultiplier { get; set; } = -1;


		public static bool IsEmployeeSpeedIncreased { get; private set; }


		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				//Set the walk speed multiplier from the settings if starting a game as host.
				WorldState.OnGameWorldChange += (ev) => {
					if (ev == GameWorldEvent.LoadingWorld) {
						float walkSpeedMult = 1f;	//Default

						if (WorldState.CurrenOnlineMode == GameOnlineMode.Host) {
							walkSpeedMult = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value;
						}
						ChangeEmployeesSpeed(walkSpeedMult);
					}
				}; 

				//Whenever the slider of the setting ClosedStoreEmployeeWalkSpeedMultiplier moves, we wait
				//	a bit before applying the changes, to avoid spamming calls to UpdateEmployeeStats.
				DelayedSingleTask<AsyncDelay> delayTask = new(() => {
					if (WorldState.CurrenOnlineMode != GameOnlineMode.Client) {
						ChangeEmployeesSpeed(ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value);
					}
				});

				ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.SettingChanged += 
					(object sender, EventArgs args) => delayTask.Start(1000);

				ModConfig.Instance.EnabledDevMode.SettingChanged +=
					(object sender, EventArgs args) => ShowWarpSpeedNotification();
				

				StoreOpenStatusPatch.OnSupermarketOpenStateChanged += SupermarketOpenStateChanged;
			}
		}

		private void ChangeEmployeesSpeed(float walkSpeedMultiplier) {
			EmployeeWalkSpeedPatch.WalkSpeedMultiplier = walkSpeedMultiplier;
			ShowWarpSpeedNotification();
			if (NPC_Manager.Instance != null) {
				NPC_Manager.Instance.UpdateEmployeeStats();
			}
		}

		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats))]
		[HarmonyPriority(Priority.VeryLow)]	//So this patch runs after other postfixes from mods that might modify movement values.
		[HarmonyPostfix]
		private static void UpdateEmployeeStatsPostfix(NPC_Manager __instance) {
			if (WorldState.CurrenOnlineMode != GameOnlineMode.Client) {
				foreach (Transform employeeT in __instance.employeeParentOBJ.transform) {
					NavMeshAgent npcNavMesh = employeeT.GetComponent<NavMeshAgent>();

					if (StoreStatusNetwork.IsStoreOpenOrCustomersInsideSync) {
						IsEmployeeSpeedIncreased = false;
					} else {
						float walkSpeedMultiplier = EmployeeWalkSpeedPatch.WalkSpeedMultiplier;
						IsEmployeeSpeedIncreased = walkSpeedMultiplier > 1;

						//Multiply over the value already set in UpdateEmployeeStats()
						npcNavMesh.speed *= walkSpeedMultiplier;

						npcNavMesh.acceleration *= (1 + (walkSpeedMultiplier * 0.70f));
						npcNavMesh.angularSpeed *= (1 + (walkSpeedMultiplier * 25000000f));
					}

					//If false, it makes them stop on its tracks when they reach their destination, instead of
					//	overshooting if their acceleration is not high enough. Though its a bit too sudden.
					npcNavMesh.autoBraking = !IsEmployeeSpeedIncreased;
				}
			}
		}

		/// <summary>
		/// Set employee speeds when spawned.
		/// </summary>
		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.SpawnEmployeeByIndex))]
		[HarmonyPriority(Priority.VeryLow)] //So this patch runs after other postfixes from mods that might modify movement values.
		[HarmonyPostfix]
		private static void SpawnEmployeeByIndexPostfix(NPC_Manager __instance) {
			//UpdateEmployeeStats does so for all employees, not just the spawned one. Its inneficient but safest.
			NPC_Manager.Instance.UpdateEmployeeStats();
		}

		/// <summary>
		/// Update speeds when the supermarket opens or closes.
		/// </summary>
		private static void SupermarketOpenStateChanged(bool isOpen) {
			if (WorldState.CurrenOnlineMode == GameOnlineMode.Host) {
				//isOpen is unused. It is checked in the postfix of updateEmployeeStats 
				NPC_Manager.Instance.UpdateEmployeeStats();
			}
		}

		public static bool IsWarpingEnabled() {
			float maxWalkSpeedMultValue = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Description.AcceptableValues.GetMaxValue<float>();

			return EmployeeWalkSpeedPatch.IsEmployeeSpeedIncreased && ModConfig.Instance.EnabledDevMode.Value
				&& WalkSpeedMultiplier == maxWalkSpeedMultValue;
		}

		private void ShowWarpSpeedNotification() {
			if (!warpNotifyDone && IsWarpingEnabled() && GameNotifications.Instance.NotificationsActive) {
				warpNotifyDone = true;
				TimeLogger.Logger.LogTimeMessageShowInGame("SPEEEEEEDS", LogCategories.Config);
			}
		}

	}
}
