using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsUnity.Components;
using Damntry.UtilsUnity.Resources;
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

	/// <summary>
	/// Tests and stuffs, only meant to be used occasionally.
	/// Methods here are quick and dirty patches or utililty
	/// methods that are not used in any permanent functionality, 
	/// and would require some more work and polish.
	/// </summary>
	public class TestAndDebugPatch : FullyAutoPatchedInstance {

		/*	*********************************************************************************
			************** THESE PATCHES ONLY WORK IN VISUAL STUDIO DEBUG MODE ************** 
			******************************************************************************** */

		public override bool IsAutoPatchEnabled => Plugin.IsSolutionInDebugMode;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - TestAndDebugPatch FAILED. Disabled";

		/* Console debug tools
		 * Should make these as in-game keys with parentScale shitty ingame menu or something that
			only works on #Debug solution so I dont have to type while doing tests
			
			GameData.Instance.UserCode_CmdAlterFunds__Single(funds);
			GameData.Instance.NetworkgameFranchisePoints = XX;
			Time.timeScale = 1f;
			timeFactor

			Change timescale
			Max hireable employees
			
		*/

		private static ActiveDebugPatches activeDebugPatches = 
			ActiveDebugPatches.None;

		private static ActiveDebugUtilities activeDebugUtilities = 
			ActiveDebugUtilities.None;



		[Flags]
		private enum ActiveDebugPatches {
			None = 0,
			FasterTimePassing = 1,
			FasterMovingCustomers = 1 << 1,
			SpawnMoreEmployeesAndAutoAssign = 1 << 2,
			CustomerMassSpawning = 1 << 3,
			DifferentSizesNPC = 1 << 4,

			All = ~None
		}
		[Flags]
		private enum ActiveDebugUtilities {
			None = 0,
			PerformanceTableLogging = 1,
			GetSceneComponents = 1 << 1,
			CheckoutProductFiller = 1 << 2,
			TheDuckening = 1 << 3,

			All = ~None
		}


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
				if (activeDebugUtilities == ActiveDebugUtilities.TheDuckening) {
					WorldState.OnWorldStarted += () => {
						KeyPressDetection.AddHotkey(KeyCode.Mouse2, 90, () => { TheDuckening.LoadDuck(); });
					};
					WorldState.OnQuitOrMenu += () => {
						KeyPressDetection.RemoveHotkey(KeyCode.Mouse2);
					};
				}
				
			}
		}

		//Patches are functionality that, if enabled, will do its logic on their own
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

		public class DifferentSizesNPC {

			[HarmonyPrepare]
			internal static bool IsDifferentSizesNPCActive() => activeDebugPatches.HasFlag(ActiveDebugPatches.DifferentSizesNPC);


			[HarmonyPatch(typeof(NPC_Manager), "SpawnDummyNCP")]
			[HarmonyPostfix]
			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				if (WorldState.CurrenOnlineMode != GameOnlineMode.Host) {
					yield break;
				}

				UpdateLastSpawnedNPCSize(NPC_Manager.Instance.dummynpcParentOBJ,
					new Vector3(1.30f, 1.15f, 1.55f));
			}

			[HarmonyPatch(typeof(NPC_Manager), "GenerateCompensatedList")]
			[HarmonyPostfix]
			public static void GenerateCompensatedListCustomerPatch(int NPCID) {
				UpdateLastSpawnedNPCSize(NPC_Manager.Instance.customersnpcParentOBJ,
					new Vector3(0.45f, 0.35f, 0.55f));
			}

			//[HarmonyPatch(typeof(NPC_Manager), "SpawnEmployeeByIndex")]
			//Use this method instead as it only executes once for the last employee. We dont want an army of giant ones.
			[HarmonyPatch(typeof(NPC_Manager), "SetHiredEmployeesNumber")]
			[HarmonyPostfix]
			public static void SpawnEmployeeByIndexPostfix() {
				if (WorldState.CurrenOnlineMode != GameOnlineMode.Host) {
					return;
				}

				UpdateLastSpawnedNPCSize(NPC_Manager.Instance.employeeParentOBJ,
					new Vector3(13, 15, 13));
			}

			public async static void UpdateLastSpawnedNPCSize(GameObject obj, Vector3 size) {
				Transform parentT = obj.transform;
				Transform npcNew = parentT.GetChild(parentT.childCount - 1);

				while (npcNew.childCount < 7) {
					await Task.Delay(100);
				}
				Transform npcT = npcNew.GetChild(npcNew.childCount - 1);
				npcT.localScale = size;
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
						//Copy data from existing employees in parentScale rotation.
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


		//Utility is functionality that, if enabled, needs to be manually
		//	called or initialized in some way before doing its logic
		#region Utility

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
			/// Generates parentScale product and places it in the belt of the first checkout.
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
					float num5 = ((!ProductListing.Instance.productPrefabs[item].GetComponent<Data_Product>().hasTrueCollider) 
						? ProductListing.Instance.productPrefabs[item].GetComponent<BoxCollider>().size.x 
						: ProductListing.Instance.productPrefabs[item].GetComponent<Data_Product>().trueCollider.x);
					if (productsIDInCheckout.Count == 1) {
						num3 = num5 / 2f;
						break;
					}
					num3 += num5 / 2f + num4 / 2f + 0.01f;
					if (num3 + num5 / 2f > 0.5f) {
						num2++;
						num3 = num5 / 2f;
						if (num2 > 6) {
							num2 = 0;
						}
					}
					num4 = num5;
				}
				gameObject.transform.position = currentCheckout.transform.Find("CheckoutItemPosition").transform.TransformPoint(new Vector3(num3, 0f, (float)num2 * 0.15f));
				gameObject.transform.rotation = currentCheckout.rotation;
				currentCheckout.GetComponent<Data_Container>().internalProductListForEmployees.Add(gameObject);
				NetworkServer.Spawn(gameObject);
			}
		}

		public static class TheDuckening {

			private static readonly string[] duckPrefabSufixes = ["Black", "Blue", "Cyan", "GreenDark", "GreenNeon", 
				"LightPurple", "Orange", "Pink", "Pond", "Purple", "Red", "RedEye", "White", "Yellow", "YinYang"];

			private static Shader generalShader;

			private static AssetBundleElement bundleElement;

			public static void LoadDuck() {
				if (!Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward,
						out RaycastHit raycastHit, 100f, 1)) {
					return;
				}

				if (bundleElement == null) {
					bundleElement = new AssetBundleElement(typeof(Plugin), $"Assets\\rubberducks");
				}

				string basePath = "Snowconesolid Assets/Super Rubber Duck Pack/Rubber Duck PREFABS/RubberDuck_";
				string randomDuckSuffix = duckPrefabSufixes[UnityEngine.Random.Range(0, duckPrefabSufixes.Length)];
				if (bundleElement.TryLoadNewInstance(basePath + randomDuckSuffix, out GameObject superQolDuck)) {
					superQolDuck.GetComponent<MeshRenderer>().material.shader = GetGameShader(superQolDuck.transform);
					superQolDuck.transform.SetParent(SMTComponentInstances.GameDataManagerInstance().transform);
					superQolDuck.transform.position = raycastHit.point;
					//Set random model size
					float scale = UnityEngine.Random.Range(0.15f, 1f);
					superQolDuck.transform.localScale = new Vector3(scale, scale, scale);
					//Very basic check to attempt to reduce number of floating ducks.
					if (Physics.Raycast(superQolDuck.transform.position, Vector3.down,
							out RaycastHit raycastHit2, 5f, 1)) {
						superQolDuck.transform.position += new Vector3(0, raycastHit2.point.y + 0.1f, 0);
					}
					
					superQolDuck.transform.Rotate(0, 0, UnityEngine.Random.Range(0, 360));
				}
			}

			/// <summary>
			/// These prefabs use the built-in shader, but this game uses URP. I could convert them in the 
			///		Unity editor but instead I just copy the currently used shader with all its properties.
			/// </summary>
			private static Shader GetGameShader(Transform parentTransform) {
				if (generalShader == null) {
					if (GameData.Instance == null) {
						LOG.TEMPWARNING("GameData instance null");
						return null;
					}

					generalShader = GameObject.Find("Level_SupermarketProps/UsableProps").transform.
						GetChild(0).
						GetComponent<MeshRenderer>().
						material.shader;
				}

				return generalShader;
			}
		}

		public static class BoxStorageRemoval {

			/// <summary>Delete every box in storage.</summary>
			public static void DeleteBoxesFromStorage() {
				ContainerSearchLambdas.ForEachStorageSlotLambda(NPC_Manager.Instance, checkNPCStorageTarget: true, skipEmptyBoxes: false,
					(storageIndex, slotIndex, productId, quantity, storageObjT) => {

						Data_Container dataContainer = storageObjT.GetComponent<Data_Container>();
						dataContainer.EmployeeUpdateArrayValuesStorage(slotIndex * 2, -1, -1);

						return ContainerSearchLambdas.LoopAction.Nothing;
					}
				);
			}

		}
		

		#endregion
	}
}
