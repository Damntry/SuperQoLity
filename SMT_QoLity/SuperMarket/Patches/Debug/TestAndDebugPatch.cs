using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch;
using UnityEngine;
using UnityEngine.AI;
using static SuperQoLity.SuperMarket.Patches.EmployeeModule.EmployeeJobAIPatch;

namespace SuperQoLity.SuperMarket.Patches {

	/* ************** ONLY WORKS IN VS DEBUG MODE ************** */

	/// <summary>
	/// Tests and stuffs, only meant to be used occasionally.
	/// Methods here are quick and dirty patches or utililty
	/// methods that are not used in any permanent functionality, 
	/// and would require some more work to do its job properly.
	/// </summary>
	public class TestAndDebugPatch : FullyAutoPatchedInstance {


		public override bool IsAutoPatchEnabled => Plugin.IsSolutionInDebugMode;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - TestAndDebugPatch FAILED. Disabled";



		[Flags]
		private enum ActiveDebugPatches {
			None = 0,
			FasterTimePassing = 1,
			FasterMovingCustomers = 2,
			SpawnMoreEmployeesAndAutoAssign = 4,
			CustomerMassSpawning = 8,

			All = ~None
		}

		private static ActiveDebugPatches activeDebugPatches = ActiveDebugPatches.None;



		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				//StartPerformanceTableLogging();
				if (SpawnMoreEmployeesAndAutoAssign.IsAutoAssignAllEmployeesActive) {
					WorldState.OnGameWorldChange += SpawnMoreEmployeesAndAutoAssign.SpawnMoreEmployeesAndAutoAssignEvent;
				}
			}
		}


		#region Patches

		public class FasterTimePassing {
			[HarmonyPrepare]
			internal static bool IsFasterTimePassingActive() => activeDebugPatches.HasFlag(ActiveDebugPatches.FasterTimePassing);

			[HarmonyPatch(typeof(GameData), "FixedUpdate")]
			[HarmonyPrefix]
			private static void FixedUpdate(GameData __instance) {
				if (__instance.isSupermarketOpen && WorldState.CurrenOnlineMode == GameOnlineMode.Host) {
					__instance.timeFactor = 0.025f; //lower is faster game time.
				}
			}
		}

		public class FasterMovingCustomers {

			[HarmonyPrepare]
			internal static bool IsFasterCustomerActive() => activeDebugPatches.HasFlag(ActiveDebugPatches.FasterMovingCustomers);


			[HarmonyPatch(typeof(NPC_Manager), "SpawnCustomerNCP")]
			[HarmonyPostfix]
			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				if (WorldState.CurrenOnlineMode != GameOnlineMode.Host) {
					yield break;
				}

				UpdateLastSpawnedCustomerSpeeds();
			}

			public static void UpdateLastSpawnedCustomerSpeeds() {
				Transform customerParentT = NPC_Manager.Instance.customersnpcParentOBJ.transform;
				Transform customerT = customerParentT.GetChild(customerParentT.childCount - 1);
				NavMeshAgent nav = customerT.gameObject.GetComponent<NavMeshAgent>();
				nav.speed = 100f;
				nav.acceleration = float.MaxValue;
				nav.angularSpeed = float.MaxValue;
			}

		}

		public class SpawnMoreEmployeesAndAutoAssign {

			internal static bool IsAutoAssignAllEmployeesActive = activeDebugPatches.HasFlag(ActiveDebugPatches.SpawnMoreEmployeesAndAutoAssign);

			internal static EmployeeJob employeeAssigment = EmployeeJob.Restocker;
			internal static int TotalNumberEmployeesTarget = 30;


			public static void SpawnMoreEmployeesAndAutoAssignEvent(GameWorldEvent gameWorldEvent) {
				if (gameWorldEvent == GameWorldEvent.WorldStarted && WorldState.CurrenOnlineMode == GameOnlineMode.Host) {
					//This will only work if there is already 1 employee hired.

					int hiredEmployees = NPC_Manager.Instance.hiredEmployeesData.Length;
					Array.Resize(ref NPC_Manager.Instance.hiredEmployeesData, TotalNumberEmployeesTarget);
					Array.Resize(ref NPC_Manager.Instance.employeesArray, TotalNumberEmployeesTarget);

					//Spawn extra employees from copied data of existing ones
					int hiredIndex = 0;
					for (int i = hiredEmployees; i < TotalNumberEmployeesTarget; i++) {
						//Copy data from existing employees in a rotation.
						NPC_Manager.Instance.hiredEmployeesData[i] = NPC_Manager.Instance.hiredEmployeesData[hiredIndex];
						if (++hiredIndex >= hiredEmployees) {
							hiredIndex = 0;
						}

						NPC_Manager.Instance.SpawnEmployeeByIndex(i);
					}

					//TODO 4 - This below is executed before all employees are spawned. Need to find another way.

					//Set job assignment for all employees
					int currentEmployeCount = NPC_Manager.Instance.employeeParentOBJ.transform.childCount;
					NPC_Manager.Instance.priorityArray = Enumerable.Repeat((int)employeeAssigment, currentEmployeCount).ToArray();

					NPC_Manager.Instance.AssignEmployeesPriorities();
				}

			}

			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				Transform employeeParentT = NPC_Manager.Instance.employeeParentOBJ.transform;
				Transform employeeT = employeeParentT.GetChild(employeeParentT.childCount - 1);
				employeeT.gameObject.GetComponent<NPC_Info>().taskPriority = (int)employeeAssigment;
			}

		}

		public class CustomerMassSpawning {
			[HarmonyPrepare]
			internal static bool IsCustomerMassSpawningActive() => activeDebugPatches.HasFlag(ActiveDebugPatches.CustomerMassSpawning);

			[HarmonyPatch(typeof(GameData), "Update")]
			[HarmonyPostfix]
			private static void UpdatePatch(GameData __instance) {
				if (WorldState.IsGameWorldStarted && WorldState.CurrenOnlineMode == GameOnlineMode.Host) {
					bool spawnCustomers = false;
					int count = 0;

					if (AuxUtils.IsKeypressed(KeyCode.J)) {
						spawnCustomers = true;
						count = 25;
					}
					if (AuxUtils.IsKeypressed(KeyCode.K)) {
						spawnCustomers = true;
						count = 100;
					}
					if (AuxUtils.IsKeypressed(KeyCode.L)) {
						spawnCustomers = true;
						count = 500;
					}

					if (spawnCustomers) {
						for (int i = 0; i < count; i++) {
							IEnumerator enumCust = ReflectionHelper.CallMethod<IEnumerator>(NPC_Manager.Instance, "SpawnCustomerNCP");
							while (enumCust.MoveNext()) ;
						}
						TimeLogger.Logger.LogTimeWarning($"Total customers on map: {NPC_Manager.Instance.customersnpcParentOBJ.transform.childCount}",
							LogCategories.TempTest);
					}
				}
			}
		}

		#endregion


		#region Utility methods

		public async void StartPerformanceTableLogging() {
			await Performance.PerformanceTableLoggingMethods.StartLogPerformanceTableNewThread(10000);
		}

		/// <summary>Couple ways of handling the cat pet cooldown.</summary>
		private void CatWaitState() {
			GameObject catObj = GameObject.Find("TheCoolRoom/Cats/Cat_NoAlpha_C3/Cat_1");
			PlayMakerFSM catFsm = ActionHelpers.GetGameObjectFsm(catObj, "Behaviour");
			RandomWait fsmCatRandomWaitState = catFsm.FsmStates.
				Where(state => state.Actions?.Any() != false && state.Actions[0].GetType() == typeof(RandomWait)).
				Select(state => (RandomWait)state.Actions[0]).
				FirstOrDefault();
			if (fsmCatRandomWaitState != null) {
				//Method 1: Remove current ongoing cooldown to allow petting
				AccessTools.Field(typeof(RandomWait), "time").SetValue(fsmCatRandomWaitState, float.MinValue);

				//Method 2: Set petting cooldown (May need to call this every time after petting)
				FsmFloat flt = new FsmFloat().Value = 5;
				AccessTools.Field(typeof(RandomWait), "min").SetValue(fsmCatRandomWaitState, flt);
				AccessTools.Field(typeof(RandomWait), "max").SetValue(fsmCatRandomWaitState, flt);
			}
		}


		private static bool deleteBoxesDone;
		/// <summary>Delete every box in storage.</summary>
		public static void DeleteBoxesFromStorage(bool doOnceOnly) {
			if (doOnceOnly && deleteBoxesDone) {
				return;
			}

			ContainerSearchLambdas.ForEachStorageSlotLambda(NPC_Manager.Instance, true,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

					Data_Container dataContainer = storageObjT.GetComponent<Data_Container>();
					dataContainer.EmployeeUpdateArrayValuesStorage(slotIndex * 2, -1, -1);

					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);

			deleteBoxesDone = true;
		}

		#endregion
	}
}
