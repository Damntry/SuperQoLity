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
					(object sender, SettingChangedEventArgs args) => delayTask.Start(1500);
			}
		}


		private static float accelerationBase = 0f;
		private static float angularSpeedBase = 0f;

		public static bool IsEmployeeSpeedIncreased {  get; private set; }

		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.UpdateEmployeeStats))]
		[HarmonyPriority(Priority.Low)]
		[HarmonyPostfix]
		private static void UpdateEmployeeStatsPostFix(NPC_Manager __instance) {
			foreach (object obj in __instance.employeeParentOBJ.transform) {
				Transform transform = (Transform)obj;
				NavMeshAgent npcNavMesh = transform.GetComponent<NavMeshAgent>();

				if (GameData.Instance.isSupermarketOpen) {
					IsEmployeeSpeedIncreased = false;
					npcNavMesh.acceleration = accelerationBase;
					npcNavMesh.angularSpeed = angularSpeedBase;
				} else {
					IsEmployeeSpeedIncreased = ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value > 1;

					//Multipliy over the value already set in UpdateEmployeeStats()
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
		private static void NetworkisSupermarketOpenPatch() {
			updateEmployeeStatsMethod.Invoke(NPC_Manager.Instance, null);
		}

		public static bool IsWarpingEnabled() {
			//TODO 4 - Make this into an static extension to get the min/max of an AcceptableValueBase, in Damntry.Globals.BepInEx.ConfigurationManager
			AcceptableValueRange<float> acceptableVal = (AcceptableValueRange<float>)ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Description.AcceptableValues;

			return EmployeeWalkSpeedPatch.IsEmployeeSpeedIncreased && ModConfig.Instance.EnabledDebug.Value
				&& ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value == acceptableVal.MaxValue;
		}

	}
}
