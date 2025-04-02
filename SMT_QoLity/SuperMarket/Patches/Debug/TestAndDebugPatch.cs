using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Mirror;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static SuperQoLity.SuperMarket.Patches.EmployeeModule.EmployeeJobAIPatch;

namespace SuperQoLity.SuperMarket.Patches.Debug {

	/* ************** PATCHES ONLY WORK IN VISUAL STUDIO DEBUG MODE ************** */

	/// <summary>
	/// Tests and stuffs, only meant to be used occasionally.
	/// Methods here are quick and dirty patches or utililty
	/// methods that are not used in any permanent functionality, 
	/// and would require some more work to do its job properly.
	/// </summary>
	public class TestAndDebugPatch : FullyAutoPatchedInstance {


		public override bool IsAutoPatchEnabled => Plugin.IsSolutionInDebugMode;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - TestAndDebugPatch FAILED. Disabled";

		/* Console debug tools
		 * Should make these as in-game keys with a shitty ingame menu or something that
			only works on #Debug solution so I dont have to type while doing tests
			
			GameData.Instance.UserCode_CmdAlterFunds__Single(ISyncVarBehaviour);
			GameData.Instance.NetworkgameFranchisePoints = XX;
			Time.timeScale = 1f;
			timeFactor

			Change timescale
			Max hireable employees
			
		*/


		[Flags]
		private enum ActiveDebugPatches {
			None = 0,
			FasterTimePassing = 1,
			FasterMovingCustomers = 2,
			SpawnMoreEmployeesAndAutoAssign = 4,
			CustomerMassSpawning = 8,

			All = ~None
		}
		[Flags]
		private enum ActiveDebugUtilities {
			None = 0,
			PerformanceTableLogging = 1,
			GetSceneComponents = 2,
			CheckoutProductFiller = 4,

			All = ~None
		}

		private static ActiveDebugPatches activeDebugPatches = ActiveDebugPatches.None;
		private static ActiveDebugUtilities activeDebugUtilities = ActiveDebugUtilities.CheckoutProductFiller;



		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				if (activeDebugUtilities == ActiveDebugUtilities.PerformanceTableLogging) {
					StartPerformanceTableLogging();
				}
				if (SpawnMoreEmployeesAndAutoAssign.IsAutoAssignAllEmployeesActive) {
					WorldState.OnGameWorldChange += SpawnMoreEmployeesAndAutoAssign.SpawnMoreEmployeesAndAutoAssignEvent;
				}
				if (activeDebugUtilities == ActiveDebugUtilities.GetSceneComponents) {
					SceneManager.activeSceneChanged += (_, newActiveScene) => {
						ComponentLogger.GetActiveComponentsInScene(newActiveScene); 
					};
				}
				if (activeDebugUtilities == ActiveDebugUtilities.CheckoutProductFiller) {
					WorldState.OnWorldStarted += CheckoutProductFiller.StartProductSpawnLoop;
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
							IEnumerator enumCust = NPC_Manager.Instance.SpawnCustomerNCP();
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

		public static class ComponentLogger {

			private static Dictionary<MonoBehaviour, DateTime> dict = new();

			public static async void GetActiveComponentsInScene(Scene newActiveScene) {
				if (!newActiveScene.name.ToLower().Contains("main")) {
					return;
				}

				while (true) {
					AddNewMonoBehavioursFromActiveScene();

					if (WorldState.IsGameWorldStarted) {
						System.Text.StringBuilder sb = new();

						dict
							.OrderBy(x => x.Value)
							.Do(p => {
								sb.Append(p.Key.ToString());
								sb.Append("  --  ");
								sb.AppendLine(p.Value.ToString("HH:mm:ss.fff"));
							}
						);
						LOG.TEMPWARNING(sb.ToString());
						return;
					}

					await System.Threading.Tasks.Task.Delay(2);
				}
			}

			public static void AddNewMonoBehavioursFromActiveScene() {
				DateTime currentTime = DateTime.Now;

				foreach (var gameObject in SceneManager.GetActiveScene().GetRootGameObjects()) {
					foreach (var item in gameObject.GetComponentsInChildren<MonoBehaviour>(true)) {
						if (!dict.ContainsKey(item)) {
							dict.Add(item, currentTime);
						}
					}

				}
			}

		}

		public static class CheckoutProductFiller {

			private static readonly float SpawnInterval = 0.15f;
			private static readonly int TotalProductLimit = 16;

			private static readonly List<int> productsIDInCheckout = new();
			private static float timeFill;

			private static bool loopActive = false;

			public static async void StartProductSpawnLoop() {
				if (NPC_Manager.Instance == null || NPC_Manager.Instance.checkoutOBJ)
				WorldState.OnQuitOrMenu += () => { loopActive = false; };

				loopActive = true;

				while (loopActive && NPC_Manager.Instance != null && 
						NPC_Manager.Instance.checkoutOBJ != null && 
						NPC_Manager.Instance.checkoutOBJ.transform.childCount > 0) {

					PlaceProductOnBelt();

					await UniTask.Delay(1);
				}
			}

			/// <summary>
			/// Generates a product and places it in the belt of the first checkout.
			/// Based on NPC_Info.PlaceProductsCoroutine.
			/// </summary>
			public static void PlaceProductOnBelt() {
				GameObject checkoutOBJ = NPC_Manager.Instance.checkoutOBJ;
				Transform currentCheckout = checkoutOBJ.transform.GetChild(0);

				if (timeFill > Time.time || currentCheckout.GetComponent<Data_Container>().NetworkproductsLeft > TotalProductLimit) {
					return;
				}

				timeFill = Time.time + SpawnInterval;

				currentCheckout.GetComponent<Data_Container>().NetworkproductsLeft++;

				int num = UnityEngine.Random.Range(1, 50);
				GameObject gameObject = UnityEngine.Object.Instantiate(NPC_Manager.Instance.productCheckoutPrefab);
				ProductCheckoutSpawn component = gameObject.GetComponent<ProductCheckoutSpawn>();
				component.NetworkproductID = num;
				component.NetworkcheckoutOBJ = currentCheckout.gameObject;
				//component.NetworkNPCOBJ = base.gameObject;
				component.NetworkproductCarryingPrice = 10;
				component.internalDataContainerListIndex = productsIDInCheckout.Count;
				productsIDInCheckout.Add(num);
				int num2 = 0;
				float num3 = 0f;
				float num4 = 0f;
				foreach (int item in productsIDInCheckout) {
					float x = ProductListing.Instance.productPrefabs[item].GetComponent<BoxCollider>().size.x;
					if (productsIDInCheckout.Count == 1) {
						num3 = x / 2f;
						break;
					}
					num3 += x / 2f + num4 / 2f + 0.01f;
					if (num3 + x / 2f > 0.5f) {
						num2++;
						num3 = x / 2f;
						if (num2 > 6) {
							num2 = 0;
						}
					}
					num4 = x;
				}
				gameObject.transform.position = currentCheckout.transform.Find("CheckoutItemPosition").transform.TransformPoint(new Vector3(num3, 0f, (float)num2 * 0.15f));
				gameObject.transform.rotation = currentCheckout.rotation;
				currentCheckout.GetComponent<Data_Container>().internalProductListForEmployees.Add(gameObject);
				NetworkServer.Spawn(gameObject);
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
