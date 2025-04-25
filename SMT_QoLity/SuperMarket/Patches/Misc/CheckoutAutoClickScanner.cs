using System.Collections;
using System.Linq;
using Damntry.Utils.Collections.Queues;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using Rewired;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperQoLity.SuperMarket.Patches.Misc {

	
	public class CheckoutAutoClickScanner : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableMiscPatches.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = 
			$"{MyPluginInfo.PLUGIN_NAME} - Hold click to scan checkout products patch failed. Disabled.";


		private LayerMask interactableMask = -1;

		private FixedCapacityUniqueQueue<ProductCheckoutSpawn> checkoutItemQueue;

		private static readonly int MaxItemsQueue = 2;

		private float nextCheckoutProductPickTime;

		private float nextCheckoutProductQueueTime;

		private float DequeueTimeLimit;

		private static readonly float CheckoutProductPickInterval = 0.175f;

		private static readonly float CheckoutProductQueueInterval = 0.025f;

		private static readonly float NoCashierPermissionDelay = 0.5f;

		//In practice, with a 0.175 pick interval and a 0.20 dequeue time, its like the
		//	queue only allowed 1 item. But the plan is to eventually have a slower click
		//	time, that you can speed up through progression through perks or usage or something.
		private static readonly float AllowedDequeueTimeSinceLastRaycast = 0.20f;

		//Once I add different kinds of delays, this value will be assigned with the lowest one.
		private static readonly float NoHitDelay = CheckoutProductQueueInterval;

		private bool wasButtonDown;


		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				checkoutItemQueue = new(MaxItemsQueue, true);

				ModConfig.Instance.EnableCheckoutAutoClicker.SettingChanged += (_, _) => ManageState();

				ManageState();
			}
		}


		/// <summary>
		/// Makes the value of ProductCheckoutSpawn.isFinished false by default.
		/// See comments in <see cref="CheckoutProductAimed"/> on why I do this.
		/// </summary>
		[HarmonyPatch(typeof(ProductCheckoutSpawn), "CreateProductObject")]
		[HarmonyPostfix]
		public static IEnumerator CreateProductObject(IEnumerator result, ProductCheckoutSpawn __instance) {
			while (result.MoveNext()) {
				yield return result.Current;
			}

			__instance.isFinished = false;
		}


		private void ManageState() {
			if (ModConfig.Instance.EnableCheckoutAutoClicker.Value) {
				WorldState.OnFPControllerStarted += InitState;

				InputBehaviour.RegisterClickAction<CheckoutAutoClickScanner>(
					inputAction: ProcessCheckoutProductPickup,
					//We depend of FirstPersonController -> PlayerNetwork component.
					worldEventToStartAt: GameWorldEvent.FPControllerStarted
				);
			} else {
				WorldState.OnFPControllerStarted -= InitState;

				InputBehaviour.UnregisterClickAction<CheckoutAutoClickScanner>();
			}

			if (WorldState.IsGameWorldAtOrAfter(GameWorldEvent.FPControllerStarted)) {
				InitState();
			}
		}

		private void InitState() {
			SetVanillaCheckoutScanState(!ModConfig.Instance.EnableCheckoutAutoClicker.Value);

			if (interactableMask < 0) {
				interactableMask = SMTComponentInstances.PlayerNetworkInstance().interactableMask;
			}
		}

		/// <summary>
		/// Enables or disables the vanilla game implementation to scan checkout
		/// products, of having to click products one by one.
		/// </summary>
		private static void SetVanillaCheckoutScanState(bool enable) {
			//Change the main prefab from which products are instantiated.
			ChangeVanillaClickEnabled(NPC_Manager.Instance.productCheckoutPrefab, enable);

			//If there are products already spawned, must update each one "enabled" state.
			ChangeClickAllSpawnProductsInScene(enable);
		}

		private static void ChangeClickAllSpawnProductsInScene(bool enable) {
			if (!WorldState.IsGameWorldAtOrAfter(GameWorldEvent.LoadingWorld)){
				return;
			}

			var spawnObjs = SceneManager.GetActiveScene()
				.GetRootGameObjects()
				.Where(g => g.TryGetComponent<ProductCheckoutSpawn>(out _));

			foreach (GameObject spawnObj in spawnObjs) {
				ChangeVanillaClickEnabled(spawnObj, enable);
			}
		}

		private static void ChangeVanillaClickEnabled(GameObject spawnObject, bool enable) {
			spawnObject
				.GetComponent<ProductCheckoutSpawn>()
				.GetComponents<PlayMakerFSM>()
				.FirstOrDefault(fsm => fsm.FsmName == "Behaviour")
				.enabled = enable;
		}

		public void ProcessCheckoutProductPickup(float currentTime, Player mainPlayerControl) {
			int scanProdAction = KeyActions.MainActionId;

			if (wasButtonDown && mainPlayerControl.GetButtonUp(scanProdAction)) {
				wasButtonDown = false;
				checkoutItemQueue.Clear();

				//Clear times to allow players to spam click if they still want.
				nextCheckoutProductQueueTime = 0;
				nextCheckoutProductPickTime = 0;
			}

			if (nextCheckoutProductQueueTime > currentTime) {
				//Too early to click or queue
				return;
			}

			if (mainPlayerControl.GetButton(scanProdAction)) {
				wasButtonDown = true;
				//Default delay for performance. It may be set to a different value below.
				nextCheckoutProductQueueTime = currentTime + NoHitDelay;

				if (!FirstPersonController.Instance.GetComponent<PlayerPermissions>().RequestCP()) {
					//Player does not have cashier permission.
					nextCheckoutProductQueueTime = currentTime + NoCashierPermissionDelay;
					return;
				}

				if (Physics.Raycast(Camera.main.transform.position,
						Camera.main.transform.forward, out RaycastHit raycastHit, 4f, interactableMask) &&
						raycastHit.transform.TryGetComponent(out ProductCheckoutSpawn productBelt)) {

					DequeueTimeLimit = currentTime + AllowedDequeueTimeSinceLastRaycast;

					CheckoutProductAimed(currentTime, productBelt);
				} else if (nextCheckoutProductPickTime < currentTime && checkoutItemQueue.Count > 0) {
					//Dont want to have items being scanned seemingly magically after last real raycast.
					//	Instead of artifically limiting the queue to solve this, check if its been 
					//	long enough from last successful product raycast.
					if (DequeueTimeLimit >= currentTime) {
						PerformCheckoutProductClick(checkoutItemQueue.Dequeue(), currentTime);
					} else {
						checkoutItemQueue.Clear();
					}
				}
			}

			//Debug.TestAndDebugPatch.CheckoutProductFiller.PlaceProductOnBelt();
		}

		private void CheckoutProductAimed(float currentTime, ProductCheckoutSpawn productBelt) {
			if (nextCheckoutProductPickTime < currentTime) {
				//In time to perform a click;
				PerformCheckoutProductClick(productBelt, currentTime);
				//Since we prioritize current raycasts vs queued ones, products sitting in the queue that
				//	where moused over earlier, could now start getting scanned if no new raycast 
				//	is found, which looks out of order and unnatural. To avoid this we clear the queue.
				checkoutItemQueue.Clear();
			} else {
				//Queue the object to be clicked. This is an extra smoothing functionality so
				//	we can keep a steady click interval, while still letting mouse hovered
				//	products enter a short queue to be latter clicked if no product is currently raycasted.
				//	This lets you fluidly mouse over products to pick them up, instead of having
				//	to jerk the mouse over every item and wait for the click cooldown to end.
				if (productBelt.isFinished) {
					//The check interval is short enough that it can queue an object that was already clicked and 
					//	scheduled to be destroyed, which eventually makes it try to process a GameObject with netid 0.
					//	To avoid this I ll just use ProductCheckoutSpawn.isFinished to flag this product as "processed",
					//	since isFinished is now unused after I disabled the FSM that accessed it.
					//	I ll probably regret this decision in the future.
					return;
				}

				if (checkoutItemQueue.TryEnqueue(productBelt, out _)) {
					nextCheckoutProductQueueTime = currentTime + CheckoutProductQueueInterval;
				}
			}
		}


		private void PerformCheckoutProductClick(ProductCheckoutSpawn productBelt, float currentTime) {
			productBelt.isFinished = true;

			if (productBelt.netId == 0) {
				//Related to my use of productBelt.isFinished, in case I missed some case.
				//	I ll still show errors so it doesnt get forgotten.
				TimeLogger.Logger.LogTimeWarning($"Attempted to scan product " +
					$"with netId 0. Skipping.", LogCategories.Other);
				return;
			}

			productBelt.CmdAddProductValueToCheckout();

			nextCheckoutProductPickTime = currentTime + CheckoutProductPickInterval;
		}

	}

}
