using System.Reflection;
using BepInEx.Configuration;
using Damntry.Utils.Tasks;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.Logging;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;
using UnityEngine.AI;

namespace SuperQoLity.SuperMarket.Patches.EmployeeModule {


	/// <summary>
	/// Employees move faster while the store is closed. 
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

		//To avoid spamming the calls to UpdateEmployeeStats whenever the employee walk speed setting slider moves.
		private DelayedThreadedSingleTask delayTask;


		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				updateEmployeeStatsMethod = AccessTools.Method(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats));
				//No need to check null. If it were the case, UpdateEmployeeStatsPostFix patch would have failed before reaching this point.

				delayTask = new DelayedThreadedSingleTask(() => {
					if (NPC_Manager.Instance != null) {	//Its null when game hasnt started yet. Values will be set normaly while loading game world.
						updateEmployeeStatsMethod.Invoke(NPC_Manager.Instance, null);
					}
				});

				ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.ConfigFile.SettingChanged += 
					(object sender, SettingChangedEventArgs args) => delayTask.Start(2000);
			}
		}


		private static float accelerationBase = 0f;
		private static float angularSpeedBase = 0f;

		public static bool IsEmployeeSpeedIncreased {  get; private set; }


		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats))]
		[HarmonyPostfix]
		private static void UpdateEmployeeStatsPostFix(NPC_Manager __instance) {
			foreach (object obj in __instance.employeeParentOBJ.transform) {
				Transform transform = (Transform)obj;
				NavMeshAgent component2 = transform.GetComponent<NavMeshAgent>();

				if (GameData.Instance.isSupermarketOpen) {
					component2.acceleration = accelerationBase;
					component2.angularSpeed = angularSpeedBase;
					IsEmployeeSpeedIncreased = false;
				} else {
					component2.speed *= ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value;
					IsEmployeeSpeedIncreased = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value > 1;

					if (accelerationBase == 0f || angularSpeedBase == 0f) {
						accelerationBase = component2.acceleration;
						angularSpeedBase = component2.angularSpeed;
					}
					if (accelerationBase == component2.acceleration) {
						//TODO 1 - Make these 2 options below into an extra setting, in case something
						//	changes in the future and it needs extra tunning, or performance depends on it.
						component2.acceleration = accelerationBase * (1 + (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 0.75f));
						component2.angularSpeed = angularSpeedBase * (1 + (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 6000f));
					}

					/*
					if (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value > 1) {
						component2.autoBraking = true;
					}
					*/
				}
			}
			/*	For when I transpile UpdateEmployeeStats, I can just go back to use this I think. Just tweak the offsets.
			
				if (GameData.Instance.isSupermarketOpen) {
					return;
				}

				foreach (object obj in __instance.employeeParentOBJ.transform) {
					Transform transform = (Transform)obj;
					NavMeshAgent component2 = transform.GetComponent<NavMeshAgent>();

					component2.speed *= ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value;
					IsEmployeeSpeedIncreased = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value > 1;
					//TODO 1 - Make these 2 options below into an extra setting, in case something
					//	changes in the future and it needs extra tunning, or performance depends on it.
					component2.acceleration *= 1 + (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 0.15f);
					component2.angularSpeed *= 1 + (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 0.15f);

					//component2.Warp
					//if (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value > 1) {
					//	component2.autoBraking = true;
					//}
				}
			*/
		}

		/// <summary>
		/// Update speeds when the value of the property NetworkisSupermarketOpen is set.
		/// </summary>
		[HarmonyPatch(typeof(GameData), nameof(GameData.NetworkisSupermarketOpen), MethodType.Setter)]
		[HarmonyPostfix]
		private static void NetworkisSupermarketOpenPatch() {
			updateEmployeeStatsMethod.Invoke(NPC_Manager.Instance, null);
		}

	}
}
