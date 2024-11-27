using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Configuration;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.Logging;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Components;
using SuperQoLity.SuperMarket.PatchClassHelpers.StorageSearch;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using UnityEngine;
using UnityEngine.AI;


namespace SuperQoLity.SuperMarket.Patches.EmployeeModule {
	//TODO 8 - Employees can pick up storage boxes from behind the wall. This happens in vanilla too, and I think
	//	it started after an update, so maybe I just need to move, or replace, the storage shelves.
	//	If that doesnt fix it either, I could ask around if it happens to them and fix it myself. Its just some
	//	interaction distance thingy for sure, but reducing it does have other consequences. But the thing is
	//	that they should be going for the "spotplace" or some name like that which sits in front of the storage..
	//	maybe that spot got fucked, or the interaction radius is so big that even from behind they can reach?
	//		I checked it out and even if I put it very far from the wall, they still go for the wall. Its like
	//		the navmesh cant reach, and thats the closest spot it can find to the limit.
	//		This is probably some fuckery with how I have more storage space than main area space or something.
	//		For now I ll leave this todo here with lower priority, but in general I think I can safely ignore
	//		this, unless I start hearing from other people having the same problem.

	//TODO 3 - When an employee is moving towards a destination target, instead of just waiting until it arrives, check
	//		periodically if the target is valid, so he doesnt try to do a job that a player has already made not possible.

	//TODO 2 - Should the restocker when going to fill a shelf, reserve the storage where its going to put the
	//		box back? I guess this would only need to happen if the box has enough items so there are any left
	//		when finished, but even then is it a good idea?
	//	Advantages: If there is no free storage space, it avoids the restocker going to drop the box to the left
	//		over box space, and the storage worker to unnecessarily put a box where the other one should have been.
	//	Disadvantages: If the restocker fills another shelf, or the shelf gets emptied on the way by customers
	//		or a player, and this results in the box ending up empty, the storage slot has been reserved for nothing.

	/// <summary>
	/// Changes:
	/// - Make employees acquire jobs, so its current target (a dropped box, or a storage/shelf slot) is "marked" and 
	///		other employees ignore it. Work gets distributed instead of everyone trying to do the same thing.
	///		
	///		* The vanilla functionality is that when an employee seeks for jobs of its type and finds a valid one, it sets 
	///			a destination towards the target or near it. Once reached, it checks that the target of the job is still there and valid,
	///			in which case it executes it. Then proceeds to the next step of whatever job is doing and does more or less the same once again.
	///			
	///			Changed it so:
	///				- Employees skips jobs whos target is already assigned to another employee.
	///				- Every existing call from employees that sets a destination, now instead uses our own method that clears
	///					whatever previous targets the employee had, so it frees any potentially unfinished jobs for other
	///					employees. Then, if the destination being set was for an assignable target (this is decided
	///					manually by us), then set that target as a marked job, for others to ignore, and save the job data.
	///					Once it reaches the job location, it gets the NPCs job data and checks that is still valid, in case human
	///					players changed something, and if valid, executes the job.
	///					Then proceeds to the next step of whatever job is doing and does more or less the same once again.
	///			
	/// - Configurable wait time for employees after finishing a job or idling. 
	///		* Went through all StartWaitState usages to assign the appropiate config value.
	///	- When an employee searchs for a box on the floor, get the closest one instead of one random.
	///		* New method GetClosestGroundBox that replaces the one to get a random box.
	/// </summary>
	[HarmonyPatch(typeof(NPC_Manager))]
	public class EmployeeJobAIPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - employee patch failed. Employee Module inactive";


		private static Lazy<MethodInfo> getMaxProductsPerRowMethod = new Lazy<MethodInfo>(() => 
			AccessTools.Method(typeof(NPC_Manager), "GetMaxProductsPerRow", [typeof(int), typeof(int)]));

		private const bool logEmployeeActions = true;

		private static Stopwatch swTemp = Stopwatch.StartNew();


		//TODO 4 - Pretty big change. I should trash EmployeeNPCControl and create a new system to handle employee jobs.
		//	The employee gameObject should have a new Navigation component that handles its own movement in an Update, and
		//	when on target, triggers an event.
		//	A new Singleton JobScheduler GameObject would now take charge of moving employees around and to assign jobs.
		//	It will hook into an employee Navigation to be notified when the worker is ready. Jobs functions would be async (single-threaded). 
		//	In this scheduler we can setup job frequency to finetune however we want to meet performance goals. Some
		//	types of jobs could be prioritized to get more scheduler time compared to less important ones.
		//	The current reservation system in EmployeeTargetReservation would be split and moved into the Jobs and Navigation
		//	systems accordingly.
		//	Then the job step system would be reworked and split into different functions with common logic separated from it.
		//	EmployeePerformancePatch would now simply remove the call to EmployeeNPCControl, since the scheduler will
		//	activate and handle the jobs itself.
		//	Basically this would make it so I keep my own Employee AI system, and whatever the dev releases after it, I would
		//	have to implement manually or maybe adapt and extend, but any changes would now be much easier to make.

		[HarmonyPatch("EmployeeNPCControl")]
		[HarmonyPrefix]
		public static bool EmployeeNPCControlPatch(NPC_Manager __instance, int employeeIndex) {
			GameObject gameObject = __instance.employeeParentOBJ.transform.GetChild(employeeIndex).gameObject;
			NPC_Info component = gameObject.GetComponent<NPC_Info>();
			int state = component.state;
			NavMeshAgent component2 = gameObject.GetComponent<NavMeshAgent>();

			int taskPriority = component.taskPriority;

			if (swTemp.ElapsedMilliseconds > 5000) {
				LOG.DEBUG(EmployeeTargetReservation.GetReservationStatusLog());
				swTemp.Restart();
			}

			if (state == -1) {
				return false;
			}

			if (taskPriority == 4 && state == 2) {
				if (component.currentChasedThiefOBJ) {
					if (component.currentChasedThiefOBJ.transform.position.x < -15f || component.currentChasedThiefOBJ.transform.position.x > 38f || component.currentChasedThiefOBJ.GetComponent<NPC_Info>().productsIDCarrying.Count == 0) {
						component.state = 0;
						return false;
					}
					if (Vector3.Distance(gameObject.transform.position, component.currentChasedThiefOBJ.transform.position) < 2f) {
						component.MoveEmployeeTo(gameObject);
						component.state = 3;
					} else {
						component.CallPathing();
					}
				} else {
					component.state = 0;
				}
			}

			if (IsEmployeeAtDestination(component2)) {
				switch (taskPriority) {
					case 0:
						component.ClearNPCReservations();   //Hack so there is a safe way of clearing reservations if they get stuck for some reason.

						if (state != 0) {
							if (state != 1) {
								component.state = 0;
								return false;
							}
						} else {
							if (component.equippedItem > 0) {
								Vector3 vector = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
								GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
								component.EquipNPCItem(0);
								component.NetworkboxProductID = 0;
								component.NetworkboxNumberOfProducts = 0;
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							}

							component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)));
							component.state = 1;
							return false;
						}
						break;
					case 1: {
							switch (state) {
								case 0:
								case 1: {
										if (component.equippedItem > 0) {
											Vector3 vector2 = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
											GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector2, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
											component.EquipNPCItem(0);
											component.NetworkboxProductID = 0;
											component.NetworkboxNumberOfProducts = 0;
											component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
											component.state = -1;
											return false;
										}

										int num = ReflectionHelper.CallMethod<int>(__instance, "CashierGetAvailableCheckout", new object[] { employeeIndex });
										if (num != -1) {
											component.employeeAssignedCheckoutIndex = num;
											ReflectionHelper.CallMethod(__instance, "UpdateEmployeeCheckouts", new object[] { });
											GameObject targetCheckout = __instance.checkoutOBJ.transform.GetChild(num).gameObject;
											component.MoveEmployeeTo(targetCheckout.transform.Find("EmployeePosition").transform.position);
											component.state = 2;
											return false;
										}
										component.state = 10;
										return false;
									}
								case 2:
									component.RPCNotificationAboveHead("NPCemployee0", "");
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
									component.state = -1;
									return false;
								case 3:
									if (ReflectionHelper.CallMethod<bool>(__instance, "CheckIfCustomerInQueue", new object[] { component.employeeAssignedCheckoutIndex })) {
										if (!__instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().checkoutQueue[0]) {
											component.state = 4;
											return false;
										}
										if (__instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().productsLeft > 0) {
											component.state = 5;
											return false;
										}
										component.state = 4;
										return false;
									} else {
										if (__instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().isCheckoutClosed) {
											component.employeeAssignedCheckoutIndex = -1;
											component.state = 0;
											return false;
										}
										component.state = 4;
									}
									return false;
								case 4:
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
									component.state = -1;
									return false;
								case 5:
									if (__instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().productsLeft == 0) {
										component.state = 7;
										return false;
									}
									component.StartWaitState(__instance.productCheckoutWait, 6);
									component.state = -1;
									return false;
								case 6: {
										using (List<GameObject>.Enumerator enumerator = __instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().internalProductListForEmployees.GetEnumerator()) {
											while (enumerator.MoveNext()) {
												GameObject gameObject2 = enumerator.Current;
												if (gameObject2 != null) {
													gameObject2.GetComponent<ProductCheckoutSpawn>().AddProductFromNPCEmployee();
													break;
												}
												component.state = 5;
											}
										}
										return false;
									}
								case 7:
									break;
								case 8:
								case 9:
									goto IL_0602;
								case 10:
									if (Vector3.Distance(component.transform.position, __instance.restSpotOBJ.transform.position) > 3f) {
										component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)));
										return false;
									}
									component.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
									component.state = -1;
									return false;
								default:
									goto IL_0602;
							}
							GameObject currentNPC = __instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().currentNPC;
							if (!currentNPC) {
								component.state = 3;
							}
							if (currentNPC.GetComponent<NPC_Info>().alreadyGaveMoney) {
								__instance.checkoutOBJ.transform.GetChild(component.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().AuxReceivePayment(0f, true);
								component.state = 3;
								return false;
							}
							component.StartWaitState(__instance.productCheckoutWait, 7);
							component.state = -1;
							return false;
						IL_0602:
							component.state = 0;
							return false;
						}
					case 2:
						switch (state) {
							case 0:
								LOG.DEBUG($"Restocker #{GetUniqueId(component)} logic begin.", logEmployeeActions);
								if (component.equippedItem > 0) {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Box in hand. Dropping.", logEmployeeActions);
									Vector3 vector3 = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector3, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
									component.EquipNPCItem(0);
									component.NetworkboxProductID = 0;
									component.NetworkboxNumberOfProducts = 0;
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									component.state = -1;
									return false;
								}

								component.productAvailableArray = CheckProductAvailability(__instance);
								if (component.productAvailableArray[0] != -1) {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Products available, moving to storage.", logEmployeeActions);
									int storageIndex = component.productAvailableArray[2];
									Vector3 destination = __instance.storageOBJ.transform.GetChild(storageIndex).Find("Standspot").transform.position;
									component.MoveEmployeeToStorage(destination, new StorageSlotInfo(storageIndex, component.productAvailableArray[7], component.productAvailableArray[5], component.productAvailableArray[10]));

									component.AddExtraProductShelfTarget(   //Reserve too the product shelf slot that will be used for next step.
											new ProductShelfSlotInfo(component.productAvailableArray[0], component.productAvailableArray[6],
												component.productAvailableArray[4], component.productAvailableArray[8])
										);
									component.state = 2;
									return false;
								}
								if (Vector3.Distance(component.transform.position, __instance.restSpotOBJ.transform.position) > 3f) {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Moving to rest spot.", logEmployeeActions);
									component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)));
									return false;
								}

								component.ClearNPCReservations();
								component.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								component.state = -1;
								return false;
							case 1:
								return false;
							case 2: {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Storage reached.", logEmployeeActions);
									GameObject targetShelfObj = __instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).gameObject;
									int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, getMaxProductsPerRowMethod.Value,
										[component.productAvailableArray[0], component.productAvailableArray[4]]);

									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo) &&
											//Check that for the next step we can still place items in the shelf.
											component.TryCheckValidTargetedProductShelf(__instance, maxProductsPerRow, out ProductShelfSlotInfo shelfTargetInfo)) {

										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Picking box.", logEmployeeActions);

										Transform storageT = __instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex);
										Data_Container dataContainer = storageT.GetComponent<Data_Container>();

										if (storageT.Find("CanvasSigns")) {
											dataContainer.EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, storageSlotInfo.ExtraData.ProductId, -1);
										} else {
											dataContainer.EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, -1, -1);
										}

										component.NetworkboxProductID = storageSlotInfo.ExtraData.ProductId;
										component.NetworkboxNumberOfProducts = storageSlotInfo.ExtraData.Quantity;
										component.EquipNPCItem(1);

										component.MoveEmployeeToShelf(targetShelfObj.transform.Find("Standspot").transform.position, shelfTargetInfo);
										component.state = 3;
										return false;
									}
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Either storage or shelf reservations didnt match.", logEmployeeActions);
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									component.state = -1;
									return false;
								}
							case 3:
								LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Product shelf reached.", logEmployeeActions);
								if (__instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).GetComponent<Data_Container>().productInfoArray[component.productAvailableArray[1]] == component.productAvailableArray[4]) {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Shelf reached has same product as box. So far so good.", logEmployeeActions);
									component.state = 4;
									return false;
								}
								LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Shelf reached has now different product than box.", logEmployeeActions);
								component.state = 5;
								return false;
							case 4: {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Checking if shelf is valid.", logEmployeeActions);
									int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, getMaxProductsPerRowMethod.Value,
										[component.productAvailableArray[0], component.productAvailableArray[4]]);

									if (component.TryCheckValidTargetedProductShelf(__instance, maxProductsPerRow, out ProductShelfSlotInfo prodShelfSlotInfo)) {
										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Shelf reached fully valid.", logEmployeeActions);
										Data_Container component4 = __instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).GetComponent<Data_Container>();
										int num4 = component4.productInfoArray[component.productAvailableArray[1] + 1];

										if (component.NetworkboxNumberOfProducts > 0 && num4 < maxProductsPerRow) {
											LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Adding products to shelf row.", logEmployeeActions);
											IncreasedEmployeeItemTransferPatch.ArgBoxNumberProducts.Value = component.NetworkboxNumberOfProducts;
											IncreasedEmployeeItemTransferPatch.ArgMaxProductsPerRow.Value = maxProductsPerRow;

											ReflectionHelper.CallMethod(component4, nameof(Data_Container.EmployeeAddsItemToRow), new object[] { component.productAvailableArray[1] });

											//Update boxNumberOfProducts now that ArgBoxNumberProducts has been changed in EmployeeAddsItemToRow
											component.NetworkboxNumberOfProducts = IncreasedEmployeeItemTransferPatch.ArgBoxNumberProducts.Value;
											component.StartWaitState(__instance.employeeItemPlaceWait, 4);
											component.state = -1;
											return false;
										}
									}

									GameObject targetShelfObj = __instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).gameObject;
									var targetShelfInfo = new ProductShelfSlotInfo(component.productAvailableArray[0], component.productAvailableArray[6],
										component.productAvailableArray[4], component.productAvailableArray[8]);

									//Getting to this point is ok in 2 cases:
									//	- A player fills or changes the product of the shelf, before the reserving employee reaches it.
									//	- The reserving employee has already filled the shelf himself and cant place anymore, since
									//		this switch case is called repeatedly over and over until the shelf is full.
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Target shelf is already full or not valid. Searching for a different one.", logEmployeeActions);
									if (component.NetworkboxNumberOfProducts > 0 && CheckIfShelfWithSameProduct(__instance, targetShelfInfo.ExtraData.ProductId, component, out ProductShelfSlotInfo productShelfSlotInfo)) {
										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Found another different shelf to add products.", logEmployeeActions);
										component.MoveEmployeeToShelf(targetShelfObj.transform.Find("Standspot").transform.position, productShelfSlotInfo);
										component.state = 3;
										return false;
									}
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Box is empty or there is no other shelf with the same product.", logEmployeeActions);

									component.state = 5;
									return false;
								}
							case 5: {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Box in hand but cant restock anymore. Deciding what to do.", logEmployeeActions);
									if (component.NetworkboxNumberOfProducts <= 0) {
										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Box empty, trying to recycle.", logEmployeeActions);
										if (!__instance.employeeRecycleBoxes || __instance.interruptBoxRecycling) {
											LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Move to trash instead.", logEmployeeActions);
											component.MoveEmployeeTo(__instance.trashSpotOBJ);
											component.state = 6;
											return false;
										}
										float num5 = Vector3.Distance(gameObject.transform.position, __instance.recycleSpot1OBJ.transform.position);
										float num6 = Vector3.Distance(gameObject.transform.position, __instance.recycleSpot2OBJ.transform.position);
										if (num5 < num6) {
											component.MoveEmployeeTo(__instance.recycleSpot1OBJ);
											component.state = 9;
											return false;
										}
										component.MoveEmployeeTo(__instance.recycleSpot2OBJ);
										component.state = 9;
									} else {
										StorageSlotInfo storageToMerge = StorageSearchHelpers.GetStorageContainerWithBoxToMerge(__instance, component.NetworkboxProductID);
										if (storageToMerge.FreeStorageFound) {
											LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Moving to storage to merge box.", logEmployeeActions);
											//component.currentFreeStorageIndex = storageSlotInfo.SlotIndex;
											Vector3 destination = __instance.storageOBJ.transform.GetChild(storageToMerge.ShelfIndex).transform.Find("Standspot").transform.position;
											component.MoveEmployeeToStorage(destination, storageToMerge);
											component.state = 20;
											return false;
										}
										StorageSlotInfo freeStorageIndexes = StorageSearchHelpers.GetFreeStorageContainer(__instance, component.NetworkboxProductID);
										if (freeStorageIndexes.FreeStorageFound) {
											LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Moving to storage to place box.", logEmployeeActions);
											Vector3 destination = __instance.storageOBJ.transform.GetChild(freeStorageIndexes.ShelfIndex).gameObject.transform.Find("Standspot").transform.position;
											component.MoveEmployeeToStorage(destination, freeStorageIndexes);
											component.state = 7;
											return false;
										}
										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Moving to left over boxes spot.", logEmployeeActions);
										component.MoveEmployeeTo(__instance.leftoverBoxesSpotOBJ);
										component.state = 8;
									}
									return false;
								}
							case 6:
								LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Removing box in hand", logEmployeeActions);
								component.EquipNPCItem(0);
								component.NetworkboxProductID = 0;
								component.NetworkboxNumberOfProducts = 0;
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							case 7: {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Checking to place box in storage", logEmployeeActions);
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Targeted storage is valid. Box added to shelf.", logEmployeeActions);
										__instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex).GetComponent<Data_Container>().EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
										component.state = 6;
										return false;
									}
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Targeted storage is not valid.", logEmployeeActions);
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 5);
									component.state = -1;
									return false;
								}
							case 8: {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Dropping box at left over spot.", logEmployeeActions);
									Vector3 vector4 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 4f, UnityEngine.Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector4, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
									component.state = 6;
									return false;
								}
							case 9: {
									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Box recycled.", logEmployeeActions);
									float num7 = 1.5f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
									GameData.Instance.CmdAlterFunds(num7);
									component.state = 6;
									return false;
								}
							case 20: {
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Merging storage box.", logEmployeeActions);
										ReflectionHelper.CallMethod(__instance, "EmployeeMergeBoxContents",
											new object[] { component, storageSlotInfo.ShelfIndex, storageSlotInfo.ExtraData.ProductId, storageSlotInfo.SlotIndex });
										component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 5);
										component.state = -1;
										return false;
									}

									LOG.DEBUG($"Restocker #{GetUniqueId(component)}: Couldnt merge storage box.", logEmployeeActions);
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									component.state = -1;
									return false;
								}
						}

						component.state = 0;
						return false;
					case 3:
						switch (state) {
							case 0:
								LOG.DEBUG($"Storage #{GetUniqueId(component)} logic begin.", logEmployeeActions);

								if (component.equippedItem > 0) {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Dropping current box.", logEmployeeActions);
									Vector3 vector5 = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector5, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
									component.EquipNPCItem(0);
									component.NetworkboxProductID = 0;
									component.NetworkboxNumberOfProducts = 0;
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									component.state = -1;
									return false;
								}
								var closestGroundBox = GroundBoxFinder.GetClosestGroundBox(__instance, gameObject);
								if (closestGroundBox.FoundGroundBox) {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Going to pick up box.", logEmployeeActions);
									component.randomBox = closestGroundBox.GroundBoxObject;
									component.MoveEmployeeToBox(closestGroundBox.GroundBoxObject);
									if (closestGroundBox.HasStorageTarget) {
										component.AddExtraStorageTarget(closestGroundBox.StorageSlot);
									}
									component.state = 1;
									return false;
								}
								component.state = 10;
								return false;
							case 1: {
									if (!component.randomBox || Vector3.Distance(component.randomBox.transform.position, component.transform.position) >= 2f) {
										LOG.DEBUG($"Storage #{GetUniqueId(component)}: Box is not there anymore.", logEmployeeActions);
										//Box got picked up by someone else, or got moved
										component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
										component.state = -1;
										return false;
									}
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Picking up box.", logEmployeeActions);
									BoxData component5 = component.randomBox.GetComponent<BoxData>();
									component.NetworkboxProductID = component5.productID;
									component.NetworkboxNumberOfProducts = component5.numberOfProducts;
									component.EquipNPCItem(1);
									GameData.Instance.GetComponent<NetworkSpawner>().EmployeeDestroyBox(component.randomBox);
									if (component5.numberOfProducts > 0) {
										component.state = 2;
										return false;
									}
									component.state = 6;
									return false;
								}
							case 2: {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Checking pre-reserved storage.", logEmployeeActions);
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										LOG.DEBUG($"Storage #{GetUniqueId(component)}: Moving to pre-reserved storage.", logEmployeeActions);
										component.MoveEmployeeToStorage(__instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex).Find("Standspot").transform.position, storageSlotInfo);
										component.state = 3;
										return false;
									}
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Pre-reserved storage is no longer valid. Moving to drop box at left over spot.", logEmployeeActions);
									component.MoveEmployeeTo(__instance.leftoverBoxesSpotOBJ);
									component.state = 4;
									return false;
								}
							case 3: {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Arrived at storage.", logEmployeeActions);
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										LOG.DEBUG($"Storage #{GetUniqueId(component)}: Placing box in storage.", logEmployeeActions);
										__instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex).GetComponent<Data_Container>().
											EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
										component.state = 5;
										return false;
									}

									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Storage no longer valid.", logEmployeeActions);
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 2);
									component.state = -1;
									return false;
								}
							case 4: {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: At left over spot. Spawning box at drop.", logEmployeeActions);
									Vector3 vector6 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 3f, UnityEngine.Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector6, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
									component.state = 5;
									return false;
								}
							case 5:
								LOG.DEBUG($"Storage #{GetUniqueId(component)}: Removing box in hand.", logEmployeeActions);
								component.EquipNPCItem(0);
								component.NetworkboxProductID = 0;
								component.NetworkboxNumberOfProducts = 0;
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							case 6: {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Trying to recycle.", logEmployeeActions);
									if (!__instance.employeeRecycleBoxes) {
										component.MoveEmployeeTo(__instance.trashSpotOBJ);
										component.state = 5;
										return false;
									}
									float num8 = Vector3.Distance(gameObject.transform.position, __instance.recycleSpot1OBJ.transform.position);
									float num9 = Vector3.Distance(gameObject.transform.position, __instance.recycleSpot2OBJ.transform.position);
									if (num8 < num9) {
										component.MoveEmployeeTo(__instance.recycleSpot1OBJ);
										component.state = 7;
										return false;
									}
									component.MoveEmployeeTo(__instance.recycleSpot2OBJ);
									component.state = 7;
									return false;
								}
							case 7: {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Recycling.", logEmployeeActions);
									float num10 = 1.5f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
									GameData.Instance.CmdAlterFunds(num10);
									component.state = 5;
									return false;
								}
							case 10:
								if (Vector3.Distance(component.transform.position, __instance.restSpotOBJ.transform.position) > 3f) {
									LOG.DEBUG($"Storage #{GetUniqueId(component)}: Moving to rest spot.", logEmployeeActions);
									component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)));
									return false;
								}

								//Though it makes sense to clear reservations here anyway, it was put specifically to solve these 2 cases:
								//	- Boxes still falling out of the sky will sometimes not set a new destination for the storage worker. This makes it
								//		so they stay at the rest spot and go to the next step, which is.. going to the rest spot. Since they dont need to
								//		call a MoveEmployeeTo... method, which clears targets, its reservations get stuck.
								//	- Like above, but happens when a box is dropped next to the rest spot, but then it gets pushed away before the
								//		employee reaches it. Employee is already at the rest spot, so no need to move that clears reservations.
								component.ClearNPCReservations();
								component.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								component.state = -1;
								return false;
						}
						component.state = 0;
						return false;
					case 4:
						switch (state) {
							case 0: {
									if (component.equippedItem > 0) {
										Vector3 vector7 = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
										GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector7, component.NetworkboxProductID, component.NetworkboxNumberOfProducts);
										component.EquipNPCItem(0);
										component.NetworkboxProductID = 0;
										component.NetworkboxNumberOfProducts = 0;
										component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
										component.state = -1;
										return false;
									}
									GameObject thiefTarget = ReflectionHelper.CallMethod<GameObject>(__instance, "GetThiefTarget", new object[] { });
									if (thiefTarget != null) {
										component.currentChasedThiefOBJ = thiefTarget;
										component.state = 2;
										return false;
									}
									if (!ReflectionHelper.CallMethod<bool>(__instance, "IsFirstSecurityEmployee", new object[] { employeeIndex })) {
										component.state = 1;
										return false;
									}
									GameObject closestDropProduct = ReflectionHelper.CallMethod<GameObject>(__instance, "GetClosestDropProduct", new object[] { gameObject });
									if (closestDropProduct != null) {
										component.droppedProductOBJ = closestDropProduct;
										component.state = 4;
										component.MoveEmployeeTo(closestDropProduct);
										return false;
									}
									component.state = 1;
									return false;
								}
							case 1: {
									Transform transform;
									if (employeeIndex < __instance.patrolPositionOBJ.transform.childCount) {
										transform = __instance.patrolPositionOBJ.transform.GetChild(employeeIndex);
									} else {
										transform = __instance.patrolPositionOBJ.transform.GetChild(0);
									}
									if (Vector3.Distance(component.transform.position, transform.position) > 3f) {
										component.MoveEmployeeTo(transform.position);
										return false;
									}
									component.StartWaitState(1f, 0);
									component.state = -1;
									return false;
								}
							case 2:
								break;
							case 3:
								if (component.currentChasedThiefOBJ && component.currentChasedThiefOBJ.GetComponent<NPC_Info>().productsIDCarrying.Count > 0) {
									component.currentChasedThiefOBJ.GetComponent<NPC_Info>().AuxiliarAnimationPlay(0);
									component.RpcEmployeeHitThief();
									component.StartWaitState(1.45f, 2);
									component.state = -1;
									return false;
								}
								component.StartWaitState(0.5f, 0);
								component.state = -1;
								return false;
							case 4:
								if (component.droppedProductOBJ != null) {
									component.droppedProductOBJ.GetComponent<StolenProductSpawn>().RecoverStolenProductFromEmployee();
									component.StartWaitState(0.5f, 0);
									component.state = -1;
									return false;
								}
								component.state = 0;
								return false;
							default:
								component.state = 0;
								return false;
						}
						break;
					default:
						UnityEngine.Debug.Log("Impossible employee current task case. Check logs.");
						break;
				}
			} else if (EmployeeWalkSpeedPatch.IsWarpingEnabled() && !component2.pathPending) {

				//See EmployeeTargetReservation.LastDestinationSet for an explanation on this
				component2.Warp(EmployeeTargetReservation.LastDestinationSet[component]);

				EmployeeWarpSound.PlayEmployeeWarpSound(component);

				component.StartWaitState(0.5f, state);
				component.state = -1;
			}

			return false; //Skip running the original method since we did all the logic here already.
		}

		public static string GetUniqueId(NPC_Info NPC) {
			return NPC.netId.ToString();	//In the end this was enough as an unique npc identifier.
		}

		private static bool IsEmployeeAtDestination(NavMeshAgent employeePathing) {
			//TODO 1 - Temporary log
			if (employeePathing.pathStatus == NavMeshPathStatus.PathInvalid) {
				LOG.DEBUG($"Employee {employeePathing.gameObject.GetComponent<NPC_Info>().netId} " + $" has invalid path status with warping disabled");
			}

			if (EmployeeWalkSpeedPatch.IsEmployeeSpeedIncreased) {

				if (EmployeeWalkSpeedPatch.IsWarpingEnabled() && 
						(employeePathing.pathStatus == NavMeshPathStatus.PathInvalid || employeePathing.pathStatus == NavMeshPathStatus.PathPartial)) {
					//PathInvalid may happen when warping employees are spawning, or very rarely when they warp to a box that just spawned
					//	at max height. I dont want to limit how high they can go so I ll just patch it like this for now.
					//As for PathPartial, see EmployeeTargetReservation.LastDestinationSet for an explanation.
					return false;
				}

				//Reduced "arrive" requirements to avoid employees bouncing around when at high speeds.
				float stoppingDistance = Math.Max(employeePathing.stoppingDistance, 1);
				if (!employeePathing.pathPending && employeePathing.remainingDistance <= stoppingDistance &&
						(!employeePathing.hasPath || employeePathing.velocity.sqrMagnitude < (ModConfig.Instance.ClosedStoreEmployeeWalkSpeedMultiplier.Value * 2))) {

					employeePathing.velocity = Vector3.zero;
					return true;
				}
			} else {
				//Base game logic
				return !employeePathing.pathPending && employeePathing.remainingDistance <= employeePathing.stoppingDistance &&
					(!employeePathing.hasPath || employeePathing.velocity.sqrMagnitude == 0f);
			}

			return false;
		}


		//TODO 0 - Move these 2 methods to StorageSearchHelper, and rename its folder and class so its named after generic shelves instead of storage.

		//Copied from the original game. I only added the tarket marking, and slightly modified some superficial stuff to reduce the madness.
		//TODO 6 - Convert CheckProductAvailability to transpile
		private static int[] CheckProductAvailability(NPC_Manager __instance) {
			int[] array = [-1, -1, -1, -1, -1, -1];

			if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0) {
				return array;
			}

			List<int[]> productsPriority = new();
			List<int[]> productsPrioritySecondary = new();

			float[] productsThresholdArray = (float[])AccessTools.Field(typeof(NPC_Manager), "productsThreshholdArray").GetValue(__instance);

			for (int i = 0; i < productsThresholdArray.Length; i++) {
				productsPriority.Clear();
				for (int j = 0; j < __instance.shelvesOBJ.transform.childCount; j++) {
					int[] productInfoArray = __instance.shelvesOBJ.transform.GetChild(j).GetComponent<Data_Container>().productInfoArray;
					int num = productInfoArray.Length / 2;
					for (int k = 0; k < num; k++) {
						productsPrioritySecondary.Clear();
						//Check if this storage slot is already in use by another employee
						if (EmployeeTargetReservation.IsProductShelfSlotTargeted(j, k)) {
							continue;
						}
						int shelfProductId = productInfoArray[k * 2];
						if (shelfProductId >= 0) {
							int shelfQuantity = productInfoArray[k * 2 + 1];
							int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, getMaxProductsPerRowMethod.Value, [j, shelfProductId]);
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * productsThresholdArray[i]);
							if (shelfQuantity == 0 || shelfQuantity < shelfQuantityThreshold) {
								for (int l = 0; l < __instance.storageOBJ.transform.childCount; l++) {
									int[] productInfoArray2 = __instance.storageOBJ.transform.GetChild(l).GetComponent<Data_Container>().productInfoArray;
									int num5 = productInfoArray2.Length / 2;
									for (int m = 0; m < num5; m++) {
										//Check if this storage slot is already in use by another employee
										if (EmployeeTargetReservation.IsStorageSlotTargeted(l, m)) {
											continue;
										}
										int storageProductId = productInfoArray2[m * 2];
										int storageQuantity = productInfoArray2[m * 2 + 1];
										if (storageProductId >= 0 && storageProductId == shelfProductId && storageQuantity > 0) {
											productsPrioritySecondary.Add(new int[] { j, k * 2, l, m * 2, shelfProductId, storageProductId, k, m, shelfQuantity, shelfQuantityThreshold, storageQuantity });
										}
									}
								}
							}
							if (productsPrioritySecondary.Count > 0) {
								productsPriority.Add(productsPrioritySecondary[UnityEngine.Random.Range(0, productsPrioritySecondary.Count)]);
							}
						}
					}
				}
				if (productsPriority.Count > 0) {
					break;
				}
			}
			if (productsPriority.Count > 0) {
				return productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
			}
			return array;
		}

		public static bool CheckIfShelfWithSameProduct(NPC_Manager __instance, int productIDToCheck, NPC_Info npcInfoComponent, out ProductShelfSlotInfo productShelfSlotInfo) {
			productShelfSlotInfo = null;
			List<ProductShelfSlotInfo> productsPriority = new();
			float[] productsThresholdArray = (float[])AccessTools.Field(typeof(NPC_Manager), "productsThreshholdArray").GetValue(__instance);

			for (int i = 0; i < productsThresholdArray.Length; i++) {

				StorageSearchLambdas.ForEachProductShelfSlotLambda(__instance, true,
					(prodShelfIndex, slotIndex, productId, quantity) => {

						if (productId == productIDToCheck) {
							int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, getMaxProductsPerRowMethod.Value, new object[] { prodShelfIndex, productIDToCheck });
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * productsThresholdArray[i]);
							if (quantity == 0 || quantity < shelfQuantityThreshold) {
								productsPriority.Add(new ProductShelfSlotInfo(prodShelfIndex, slotIndex, productId, quantity));
							}
						}

						return StorageSearchLambdas.LoopAction.Nothing;
					}
				);

				if (productsPriority.Count > 0) {
					productShelfSlotInfo = productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
					npcInfoComponent.productAvailableArray[0] = productShelfSlotInfo.ShelfIndex;
					npcInfoComponent.productAvailableArray[1] = productShelfSlotInfo.SlotIndex * 2;
					npcInfoComponent.productAvailableArray[4] = productShelfSlotInfo.ExtraData.ProductId;
					npcInfoComponent.productAvailableArray[6] = productShelfSlotInfo.SlotIndex;
					npcInfoComponent.productAvailableArray[6] = productShelfSlotInfo.ExtraData.Quantity;
					return true;
				}
			}

			return false;
		}

	}


}
