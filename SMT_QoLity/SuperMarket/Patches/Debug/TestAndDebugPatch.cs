using Cysharp.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Debug;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsUnity.Components.InputManagement;
using Damntry.UtilsUnity.ExtensionMethods;
using Damntry.UtilsUnity.Resources;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Mirror;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.Search;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

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
			GameData.Instance.NetworktimeOfDay = 22.49f;
			timeFactor

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
            ExtraMaxEmployees = 1 << 5,
            CustomerNameplate = 1 << 6,
            

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
					WorldState.OnWorldLoaded += CheckoutProductFiller.StartProductSpawnLoop;
				}
				if (activeDebugUtilities == ActiveDebugUtilities.TheDuckening) {
					WorldState.OnWorldLoaded += () => {
						InputManagerSMT.Instance.TryAddHotkey("PlaceDuck", KeyCode.Mouse2, 
							InputState.KeyHeld, HotkeyActiveContext.WorldLoaded, 90, () => { TheDuckening.LoadDuck(); });
					};
					WorldState.OnQuitOrMainMenu += () => {
                        InputManagerSMT.Instance.RemoveHotkey("PlaceDuck");
					};
				}
				WorldState.OnWorldLoaded += () => {
					if (WorldState.CurrentOnlineMode == GameOnlineMode.Host) {
                        //Left Control + Left Alt + Scroll to change
                        TimeScaleDebug.Initialize<TimeScaleMethods>(SMTInstances.FirstPersonController().GameObject(),
							showInGameNotification: () => ModConfig.Instance.EnabledDevMode.Value);
                    }
                };
            }
        }

        private class TimeScaleMethods : ITimeScaleMethods {

            //Current vanilla defaults as of 17/11/25 for reference
            private float vanillaCrouchSpeed = 4f;
            private float vanillaMoveSpeed = 5f;
            private float vanillaSprintSpeed = 10f;
            private float vanillaAirSpeed = 3f;
            private float vanillaSpeedChangeRate = 10f;
            private float vanillaGravity = -15f;

            public void ReadVanillaValues() {
                FirstPersonController fpsControl = SMTInstances.FirstPersonController();

                vanillaCrouchSpeed = fpsControl.CrouchSpeed;
                vanillaMoveSpeed = fpsControl.MoveSpeed;
                vanillaSprintSpeed = fpsControl.SprintSpeed;
                vanillaAirSpeed = fpsControl.airSpeed;
                vanillaSpeedChangeRate = fpsControl.SpeedChangeRate;
				//This one is not really working but it doesnt matter that much.
                vanillaGravity = fpsControl.Gravity;
                //For the crouch animation speed I would need to transpile PlayerNetwork.CrouchLerpCoroutine. Not worth it.
            }

            public void SetAdjustedSpeed(float timeScaleDiff) {
                FirstPersonController fpsControl = SMTInstances.FirstPersonController();
                fpsControl.CrouchSpeed = vanillaCrouchSpeed * timeScaleDiff;
                fpsControl.MoveSpeed = vanillaMoveSpeed * timeScaleDiff;
                fpsControl.SprintSpeed = vanillaSprintSpeed * timeScaleDiff;
                fpsControl.airSpeed = vanillaAirSpeed * timeScaleDiff;
                fpsControl.SpeedChangeRate = vanillaSpeedChangeRate * timeScaleDiff;
                fpsControl.Gravity = vanillaGravity * timeScaleDiff;
            }
        }


        public static async Task MovePlayerTo(Vector3 position, Vector2 lookRotation) {
            /* Example call from console
			SuperQoLity.SuperMarket.Patches.Debug.TestAndDebugPatch.MovePlayerTo(
				new Vector3(-5.0987f, 0.02f, 43.0458f), 
				new Vector2(120.1996f, 0f)
			);
			*/
            FirstPersonController fpc = SMTInstances.FirstPersonController();
			CustomCameraController ccc = SMTInstances.GetCustomCameraController();

			if (!fpc || !ccc) {
				TimeLogger.Logger.LogDebug("FirstPersonController or CustomCameraController " +
					"have not awaken yet", LogCategories.Other);
			}

            fpc.isTeleporting = true;	//Disallow player movement
			
			await UniTask.DelayFrame(1, PlayerLoopTiming.FixedUpdate);

			GameObject.Find("LocalGamePlayer").GetComponent<FirstPersonTransform>().transform.position = position;
            ccc.x = lookRotation.x;
            ccc.y = lookRotation.y;

            await UniTask.DelayFrame(1, PlayerLoopTiming.FixedUpdate);

            fpc.isTeleporting = false;	//Restore movement
        }

		public static void DeleteAllFSM() {
			foreach (var item in Component.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)) {
				Component.DestroyImmediate(item);
			}
		}

        //Patches are functionality that, if enabled, will do its logic on their own
        #region Patches

        public class ShowCustomerNameplateId {

			[HarmonyPrepare]
			internal static bool IsCustomerNameplate() =>
					activeDebugPatches.HasFlag(ActiveDebugPatches.CustomerNameplate);

			/// <summary>
			/// Shows the npcid of customers as text above their model.
			/// </summary>
			[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.SpawnCustomerNPC))]
			[HarmonyPostfix]
			public static IEnumerator ShowCustomerNameplateIdPatch(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				Transform customerParent = NPC_Manager.Instance.customersnpcParentOBJ.transform;
				int customerIndex = customerParent.childCount - 1;
				GameObject gameObject = customerParent.GetChild(customerIndex).gameObject;
				Transform nameT = gameObject.transform.Find("NameCanvas");
				nameT.gameObject.SetActive(true);
				TextMeshProUGUI nameText = nameT.Find("NPCName").GetComponent<TextMeshProUGUI>();
				nameText.text = gameObject.GetInstanceID().ToString();
				nameText.isOverlay = true;
			}
		}

        public class FasterTimePassing {
			[HarmonyPrepare]
			internal static bool IsFasterTimePassingActive() => 
				activeDebugPatches.HasFlag(ActiveDebugPatches.FasterTimePassing);

			[HarmonyPatch(typeof(GameData), "FixedUpdate")]
			[HarmonyPrefix]
			private static void FixedUpdate(GameData __instance) {
				if (__instance.isSupermarketOpen && WorldState.CurrentOnlineMode == GameOnlineMode.Host) {
					__instance.timeFactor = 0.025f; //lower is faster game time.
				}
			}
		}

		public class FasterMovingCustomers {

			[HarmonyPrepare]
			internal static bool IsFasterCustomerActive() => 
				activeDebugPatches.HasFlag(ActiveDebugPatches.FasterMovingCustomers);


			[HarmonyPatch(typeof(NPC_Manager), "SpawnCustomerNCP")]
			[HarmonyPostfix]
			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				if (WorldState.CurrentOnlineMode != GameOnlineMode.Host) {
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
			internal static bool IsDifferentSizesNPCActive() => 
				activeDebugPatches.HasFlag(ActiveDebugPatches.DifferentSizesNPC);


			[HarmonyPatch(typeof(NPC_Manager), "SpawnDummyNCP")]
			[HarmonyPostfix]
			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				if (WorldState.CurrentOnlineMode != GameOnlineMode.Host) {
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
				if (WorldState.CurrentOnlineMode != GameOnlineMode.Host) {
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

			internal static bool IsAutoAssignAllEmployeesActive = 
				activeDebugPatches.HasFlag(ActiveDebugPatches.SpawnMoreEmployeesAndAutoAssign);

			internal static EmployeeJob employeeAssigment = EmployeeJob.Restocker;
			internal static int TotalNumberEmployeesTarget = 30;

			public static void SpawnMoreEmployeesAndAutoAssignEvent(GameWorldEvent gameWorldEvent) {
				if (gameWorldEvent == GameWorldEvent.WorldLoaded && WorldState.CurrentOnlineMode == GameOnlineMode.Host) {
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
			internal static bool IsCustomerMassSpawningActive() =>
				activeDebugPatches.HasFlag(ActiveDebugPatches.CustomerMassSpawning);

			[HarmonyPatch(typeof(GameData), "Update")]
			[HarmonyPostfix]
			private static void UpdatePatch(GameData __instance) {
				if (WorldState.IsWorldLoaded && WorldState.CurrentOnlineMode == GameOnlineMode.Host) {
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
							IEnumerator enumCust = NPC_Manager.Instance.SpawnCustomerNPC();
							while (enumCust.MoveNext()) ;
						}
						TimeLogger.Logger.LogWarning($"Total customers on map: {NPC_Manager.Instance.customersnpcParentOBJ.transform.childCount}",
							LogCategories.TempTest);
					}
				}
			}
		}

		public class IncreaseMaxEmployees() {

            [HarmonyPrepare]
            internal static bool IsExtraEmployeesActive() => 
				activeDebugPatches.HasFlag(ActiveDebugPatches.ExtraMaxEmployees);

            [HarmonyPatch(typeof(UpgradesManager), nameof(UpgradesManager.OnStartClient))]
            [HarmonyPrefix]
            public static void AddExtraEmployeesOnLoad(UpgradesManager __instance) {
                NPC_Manager.Instance.maxEmployees += 1;
            }

        }

		private static void RotateShelfs() {
			foreach (Transform shelf in NPC_Manager.Instance.shelvesOBJ.transform) {
				shelf.position += new Vector3(0, UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.08f, 0.08f));
				shelf.localScale = new Vector3(UnityEngine.Random.Range(0.5f, 1.4f), UnityEngine.Random.Range(0.5f, 1.4f), UnityEngine.Random.Range(0.5f, 1.4f));
				shelf.rotation = Quaternion.Euler(
					shelf.rotation.x + UnityEngine.Random.Range(0f, 8f),
					shelf.rotation.y + UnityEngine.Random.Range(0f, 17f),
					shelf.rotation.z + UnityEngine.Random.Range(0f, 4f));
			}
		}

		/*
		[HarmonyPatch(typeof(GameData), nameof(GameData.AuxiliarSupermarketOpen))]
		[HarmonyPostfix]
		private static void AuxiliarSupermarketOpenPatch(GameData __instance) {
			__instance.maxCustomersNPCs = 1;
		}
		*/

		#endregion


		//Utility is functionality that, if enabled, needs to be manually
		//	called or initialized in some way before doing its logic
		#region Utility

		public async void StartPerformanceTableLogging() {
			await Performance.PerformanceTableLoggingMethods.StartLogPerformanceTableNewThread(10000);
		}

		public void SpawnDroppedStolenProducts() {
			for (int i = 0; i < 100; i++) {
				NPC_Info employeeNpc = NPC_Manager.Instance.employeeParentOBJ.transform.GetChild(0).GetComponent<NPC_Info>();
				GameObject obj = UnityEngine.Object.Instantiate(employeeNpc.stolenProductPrefab, NPC_Manager.Instance.droppedProductsParentOBJ.transform);
				obj.transform.position = StarterAssets.FirstPersonController.Instance.transform.position + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f));
				obj.GetComponent<StolenProductSpawn>().NetworkproductID = UnityEngine.Random.Range(1, 300);
				obj.GetComponent<StolenProductSpawn>().NetworkproductCarryingPrice = 1f;
				Mirror.NetworkServer.Spawn(obj);
			}
		}

        public static void FillAllStorageSlotsWithBoxes() {
            ContainerSearchLambdas.ForEachStorageSlotLambda(NPC_Manager.Instance, false, false,
                (storageIndex, slotIndex, productId, quantity, storageObjT) => {
                    storageObjT.GetComponent<Data_Container>().CmdUpdateArrayValuesStorage(slotIndex, 1, 20);
                    return ContainerSearchLambdas.LoopAction.Nothing;
                }
            );
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

					if (WorldState.IsWorldLoaded) {
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

					await Task.Delay(2);
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
					WorldState.OnQuitOrMainMenu += () => { loopActive = false; };

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
					float num5 = ((!ProductListing.Instance.productsData[item].hasTrueCollider)
						? ProductListing.Instance.productsData[item].productPrefab.GetComponent<BoxCollider>().size.x
						: ProductListing.Instance.productsData[item].trueColliderSize.x);
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

			private static AssetBundleElement bundleElement;

			public static void LoadDuck() {
				if (!Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward,
						out RaycastHit raycastHit, 100f, 1)) {
					return;
				}

				if (bundleElement == null) {
					bundleElement = new AssetBundleElement(typeof(Plugin), $"Assets\\Debug\\rubberducks");
				}

				string basePath = "Snowconesolid Assets/Super Rubber Duck Pack/Rubber Duck PREFABS/RubberDuck_";
				string randomDuckSuffix = duckPrefabSufixes[UnityEngine.Random.Range(0, duckPrefabSufixes.Length)];
				if (bundleElement.TryLoadNewPrefabInstance(basePath + randomDuckSuffix, out GameObject superQolDuck)) {
					superQolDuck.GetComponent<MeshRenderer>().material.shader = ShaderUtils.SMT_Shader.Value;
					superQolDuck.transform.SetParent(SMTInstances.GameDataManager().transform);
					superQolDuck.transform.position = raycastHit.point;
					//Set stateActionArrayRandom model size
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
