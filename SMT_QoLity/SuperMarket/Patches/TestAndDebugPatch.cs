using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using HutongGames.PlayMaker;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AI;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.Utils.Logging;
using System.Collections;

namespace SuperQoLity.SuperMarket.Patches {


	/// <summary>
	/// Tests and stuffs, only meant to be used occasionally.
	/// Methods here are quick and dirty patches or utililty
	/// methods that are not used in any permanent functionality, 
	/// and would require some more work to do its job properly.
	/// </summary>
	public class TestAndDebugPatch : FullyAutoPatchedInstance {


		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - TestAndDebugPatch FAILED. Disabled";



		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				
			}
		}

		#region Patches

		public class FasterTimePassing{
			[HarmonyPrepare()]
			internal static bool IsFasterTimePassingActive() => false;

			[HarmonyPatch(typeof(GameData), "FixedUpdate")]
			[HarmonyPrefix]
			private static void FixedUpdate(GameData __instance) {
				if (__instance.isSupermarketOpen) {
					__instance.timeFactor = 0.025f; //lower is faster game time.
				}
			}
		}


		/*[HarmonyPatch(typeof(UpgradesManager), "ManageExtraPerks"), HarmonyPrefix]
		public static bool ManageExtraPerksPatch(UpgradesManager __instance, int perkIndex) {
			switch (perkIndex) {
				case 0:
					NPC_Manager.Instance.maxEmployees += 1;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 1:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 2:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 3:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 4:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 5:
					NPC_Manager.Instance.extraEmployeeSpeedFactor += 0.2f;
					NPC_Manager.Instance.UpdateEmployeeStats();
					break;
				case 6:
					NPC_Manager.Instance.extraCheckoutMoney += 0.1f;
					NPC_Manager.Instance.UpdateEmployeeStats();
					break;
				case 7:
					NPC_Manager.Instance.extraEmployeeSpeedFactor += 0.2f;
					NPC_Manager.Instance.UpdateEmployeeStats();
					break;
				case 8:
					__instance.boxRecycleFactor = 4;
					break;
				case 9:
					__instance.GetComponent<GameData>().extraCustomersPerk++;
					break;
				case 10:
					__instance.GetComponent<GameData>().extraCustomersPerk++;
					break;
				case 11:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 12:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 13:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 14:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 15:
					NPC_Manager.Instance.maxEmployees++;
					NPC_Manager.Instance.UpdateEmployeesNumberInBlackboard();
					break;
				case 16:
					NPC_Manager.Instance.productCheckoutWait -= 0.15f;
					break;
				case 17:
					NPC_Manager.Instance.productCheckoutWait -= 0.2f;
					break;
				case 18:
					NPC_Manager.Instance.productCheckoutWait -= 0.15f;
					break;
				case 19:
					NPC_Manager.Instance.employeeItemPlaceWait -= 0.05f;
					break;
				case 20:
					NPC_Manager.Instance.employeeItemPlaceWait -= 0.05f;
					break;
				case 21:
					NPC_Manager.Instance.extraEmployeeSpeedFactor += 0.2f;
					NPC_Manager.Instance.UpdateEmployeeStats();
					break;
				case 22:
					NPC_Manager.Instance.extraEmployeeSpeedFactor += 0.2f;
					NPC_Manager.Instance.UpdateEmployeeStats();
					break;
				case 23:
					NPC_Manager.Instance.extraEmployeeSpeedFactor += 0.2f;
					NPC_Manager.Instance.UpdateEmployeeStats();
					break;
				default:
					LOG.DEBUG(NPC_Manager.Instance.productCheckoutWait.ToString());
					return true;
			}

			GameObject obj = __instance.UIPerksParent.transform.GetChild(perkIndex).gameObject;
			obj.GetComponent<CanvasGroup>().alpha = 1f;
			obj.tag = "Untagged";
			obj.transform.Find("Highlight2").gameObject.SetActive(value: true);

			LOG.DEBUG(NPC_Manager.Instance.productCheckoutWait.ToString());

			return false;
		} */


		#endregion


		#region Utility methods

		public class FasterMovingCustomers {

			[HarmonyPrepare()]
			internal static bool IsFasterCustomerActive() => false;

		
			[HarmonyPatch(typeof(NPC_Manager), "SpawnCustomerNCP")]
			[HarmonyPostfix]
			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
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
