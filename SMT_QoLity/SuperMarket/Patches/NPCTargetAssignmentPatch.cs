using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using Damntry.Utils.Reflection;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using System.Diagnostics.Contracts;

namespace SuperQoLity.SuperMarket.Patches
{

    //TODO 2 - Implement shelf slots

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
	public class NPCTargetAssignmentPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableTransferProducts.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - NPC patch failed. Employee Module inactive";


		//TODO 3 - Check how viable it would be to change this Prefix into a transpiler.
		//	This is a big method that will 100% get changed in the original game, so try to think
		//	of alternative ways of minimizing changes to make the transpiler easier and safer.
		[HarmonyPatch("EmployeeNPCControl")]
		[HarmonyPrefix]
		private static bool EmployeeNPCControlPatch(NPC_Manager __instance, int employeeIndex) {
			GameObject gameObject = __instance.employeeParentOBJ.transform.GetChild(employeeIndex).gameObject;
			NPC_Info component = gameObject.GetComponent<NPC_Info>();
			int state = component.state;
			NavMeshAgent component2 = gameObject.GetComponent<NavMeshAgent>();
			int taskPriority = component.taskPriority;
			
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
			if (!component2.pathPending && component2.remainingDistance <= component2.stoppingDistance && (!component2.hasPath || component2.velocity.sqrMagnitude == 0f)) {
				switch (taskPriority) {
					case 0:
						if (state != 0) {
							if (state != 1) {
								component.state = 0;
								return false;
							}
						} else {
							if (component.equippedItem > 0) {
								Vector3 vector = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
								GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector, component.boxProductID, component.boxNumberOfProducts);
								component.EquipNPCItem(0);
								component.boxProductID = 0;
								component.boxNumberOfProducts = 0;
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							}
							component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
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
											GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector2, component.boxProductID, component.boxNumberOfProducts);
											component.EquipNPCItem(0);
											component.boxProductID = 0;
											component.boxNumberOfProducts = 0;
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
										component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
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
								if (component.equippedItem > 0) {
									Vector3 vector3 = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector3, component.boxProductID, component.boxNumberOfProducts);
									component.EquipNPCItem(0);
									component.boxProductID = 0;
									component.boxNumberOfProducts = 0;
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									component.state = -1;
									return false;
								}
								component.productAvailableArray = CheckProductAvailability(__instance);
								if (component.productAvailableArray[0] != -1) {
									int storageIndex = component.productAvailableArray[2];
									Vector3 destination = __instance.storageOBJ.transform.GetChild(storageIndex).Find("Standspot").transform.position;
									component.MoveEmployeeToStorage(destination, new StorageSlotInfo(storageIndex, component.productAvailableArray[7], component.productAvailableArray[5], component.productAvailableArray[10]));
									component.state = 2;
									return false;
								}
								if (Vector3.Distance(component.transform.position, __instance.restSpotOBJ.transform.position) > 3f) {
									component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
									return false;
								}
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							case 1:
								return false;
							case 2: {
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										Transform storageT = __instance.storageOBJ.transform.GetChild(storageSlotInfo.StorageIndex);
										Data_Container dataContainer = storageT.GetComponent<Data_Container>();

										if (storageT.Find("CanvasSigns")) {
											dataContainer.EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, storageSlotInfo.ExtraData.ProductId, -1);
										} else {
											dataContainer.EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, -1, -1);
										}

										component.boxProductID = storageSlotInfo.ExtraData.ProductId;
										component.boxNumberOfProducts = storageSlotInfo.ExtraData.Quantity;
										component.EquipNPCItem(1);
										GameObject targetShelf = __instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).gameObject;
										component.MoveEmployeeToShelf(targetShelf.transform.Find("Standspot").transform.position);
										component.state = 3;
										return false;
									}
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 5);
									component.state = -1;
									return false;
								}
							case 3:
								if (__instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).GetComponent<Data_Container>().productInfoArray[component.productAvailableArray[1]] == component.productAvailableArray[4]) {
									component.state = 4;
									return false;
								}
								component.state = 5;
								return false;
							case 4: {
									Data_Container component4 = __instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).GetComponent<Data_Container>();
									int num4 = component4.productInfoArray[component.productAvailableArray[1] + 1];
									int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, "GetMaxProductsPerRow", new object[] { component.productAvailableArray[0], component.productAvailableArray[4] });
									bool success = IncreasedEmployeeItemTransferPatch.EmployeeAddsItemToRow(component4, component.productAvailableArray[1], ref component.boxNumberOfProducts, maxProductsPerRow);
									if (success) {
										//TODO 3 - When I Transpile this, remember that I need to remove the line with:  "component.boxNumberOfProducts--;"
										//		since its now dynamically decreased in the EmployeeAddsItemToRow method.
										component.StartWaitState(__instance.employeeItemPlaceWait, 4);
										component.state = -1;
										return false;
									}
									if (component.boxNumberOfProducts > 0 && ReflectionHelper.CallMethod<bool>(__instance, "CheckIfShelfWithSameProduct", new object[] { component.productAvailableArray[4], component, component.productAvailableArray[0] })) {
										GameObject targetShelf = __instance.shelvesOBJ.transform.GetChild(component.productAvailableArray[0]).gameObject;
										component.MoveEmployeeToShelf(targetShelf.transform.Find("Standspot").transform.position);
										component.state = 3;
										return false;
									}
									component.state = 5;
									return false;
								}
							case 5: {
									if (component.boxNumberOfProducts <= 0) {
										if (!__instance.employeeRecycleBoxes || __instance.interruptBoxRecycling) {
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
										StorageSlotInfo storageToMerge = GetStorageContainerWithBoxToMerge(__instance, component.boxProductID);
										if (storageToMerge.FreeStorageFound) {
											//component.currentFreeStorageIndex = storageSlotInfo.SlotIndex;
											Vector3 destination = __instance.storageOBJ.transform.GetChild(storageToMerge.StorageIndex).transform.Find("Standspot").transform.position;
											component.MoveEmployeeToStorage(destination, storageToMerge);
											component.state = 20;
											return false;
										}
										StorageSlotInfo freeStorageIndexes = GetFreeStorageContainer(__instance, component.boxProductID);
										if (freeStorageIndexes.FreeStorageFound) {
											Vector3 destination = __instance.storageOBJ.transform.GetChild(freeStorageIndexes.StorageIndex).gameObject.transform.Find("Standspot").transform.position;
											component.MoveEmployeeToStorage(destination, freeStorageIndexes);
											component.state = 7;
											return false;
										}
										component.MoveEmployeeTo(__instance.leftoverBoxesSpotOBJ);
										component.state = 8;
									}
									return false;
								}
							case 6:
								component.EquipNPCItem(0);
								component.boxProductID = 0;
								component.boxNumberOfProducts = 0;
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							case 7: {
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										__instance.storageOBJ.transform.GetChild(storageSlotInfo.StorageIndex).GetComponent<Data_Container>().EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, component.boxProductID, component.boxNumberOfProducts);
										component.state = 6;
										return false;
									}
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 5);
									component.state = -1;
									return false;
								}
							case 8: {
									Vector3 vector4 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(Random.Range(-1f, 1f), 4f, Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector4, component.boxProductID, component.boxNumberOfProducts);
									component.state = 6;
									return false;
								}
							case 9: {
									float num7 = 1.5f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
									GameData.Instance.CmdAlterFunds(num7);
									component.state = 6;
									return false;
								}
							case 20: {
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										ReflectionHelper.CallMethod(__instance, "EmployeeMergeBoxContents",
											new object[] { component, storageSlotInfo.StorageIndex, storageSlotInfo.ExtraData.ProductId, storageSlotInfo.SlotIndex });
									}
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
								if (component.equippedItem > 0) {
									Vector3 vector5 = component.transform.position + component.transform.forward * 0.5f + new Vector3(0f, 1f, 0f);
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector5, component.boxProductID, component.boxNumberOfProducts);
									component.EquipNPCItem(0);
									component.boxProductID = 0;
									component.boxNumberOfProducts = 0;
									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									component.state = -1;
									return false;
								}
								GameObject closestGroundBox = GetClosestGroundBox(__instance, gameObject);
								if (closestGroundBox) {
									component.randomBox = closestGroundBox;
									component.MoveEmployeeToBox(closestGroundBox);
									component.state = 1;
									return false;
								}
								component.state = 10;
								return false;
							case 1: {
									if (!component.randomBox || Vector3.Distance(component.randomBox.transform.position, component.transform.position) >= 2f) {
										//Box got picked up by someone else, or got moved
										component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
										component.state = -1;
										return false;
									}
									BoxData component5 = component.randomBox.GetComponent<BoxData>();
									component.boxProductID = component5.productID;
									component.boxNumberOfProducts = component5.numberOfProducts;
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
									StorageSlotInfo freeStorageIndexes = GetFreeStorageContainer(__instance, component.boxProductID);
									if (freeStorageIndexes.FreeStorageFound) {
										component.MoveEmployeeToStorage(__instance.storageOBJ.transform.GetChild(freeStorageIndexes.StorageIndex).Find("Standspot").transform.position, freeStorageIndexes);
										component.state = 3;
										return false;
									}
									component.MoveEmployeeTo(__instance.leftoverBoxesSpotOBJ);
									component.state = 4;
									return false;
								}
							case 3: {
									if (component.TryCheckValidTargetedStorage(__instance, out StorageSlotInfo storageSlotInfo)) {
										__instance.storageOBJ.transform.GetChild(storageSlotInfo.StorageIndex).GetComponent<Data_Container>().
											EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, component.boxProductID, component.boxNumberOfProducts);
										component.state = 5;
										return false;
									}

									component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 2);
									component.state = -1;
									return false;
								}
							case 4: {
									Vector3 vector6 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(Random.Range(-1f, 1f), 3f, Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector6, component.boxProductID, component.boxNumberOfProducts);
									component.state = 5;
									return false;
								}
							case 5:
								component.EquipNPCItem(0);
								component.boxProductID = 0;
								component.boxNumberOfProducts = 0;
								component.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								component.state = -1;
								return false;
							case 6: {
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
									float num10 = 1.5f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
									GameData.Instance.CmdAlterFunds(num10);
									component.state = 5;
									return false;
								}
							case 10:
								if (Vector3.Distance(component.transform.position, __instance.restSpotOBJ.transform.position) > 3f) {
									component.MoveEmployeeTo(__instance.restSpotOBJ.transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
									return false;
								}
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
										GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector7, component.boxProductID, component.boxNumberOfProducts);
										component.EquipNPCItem(0);
										component.boxProductID = 0;
										component.boxNumberOfProducts = 0;
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
						Debug.Log("Impossible employee current task case. Check logs.");
						break;
				}
			}

			return false; //Skip running the original method since we did all the logic here already.
		}



		private static StorageSlotInfo GetStorageContainerWithBoxToMerge(NPC_Manager __instance, int boxIDProduct) {
			return FindStorageSlotLambda(__instance, true,
				(storageId, slotId, productId, quantity) => {

					if (productId == boxIDProduct && quantity > 0 && 
							quantity < ProductListing.Instance.productPrefabs[productId].GetComponent<Data_Product>().maxItemsPerBox) {
						return LoopStorageAction.SaveAndExit;
					}

					return LoopStorageAction.Nothing;
				}
			);
		}

		private static bool IsFreeStorageContainer(NPC_Manager __instance) {
			return FindStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {
					if (productId == -1) {
						//Free storage slot, either unassigned or unlabeled.
						return LoopStorageAction.SaveAndExit;
					}

					return LoopStorageAction.Nothing;
				}
			).FreeStorageFound;
		}

		private static StorageSlotInfo GetFreeStorageContainer(NPC_Manager __instance, int boxIDProduct) {
			return FindStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					if (boxIDProduct >= 0 && productId == boxIDProduct && quantity < 0) {
						//Free assigned storage slot. Return it.
						return LoopStorageAction.SaveAndExit;
					} else if (productId == -1) {
						//Save for later in case there is no assigned storage for this product.
						return LoopStorageAction.Save;
					}

					return LoopStorageAction.Nothing;
				}
			);
		}

		public enum LoopStorageAction {
			Nothing = 0,	//Do nothing special in this loop and keep going.
			Save = 1,       //Save storage slot data and keep going.
			SaveAndExit = 2 //Save storage slot data and return immediately.
		}
		public enum LoopAction {
			Nothing = 0,    //Keep looping.
			Nothing2 = 1,   //Same as Nothing. Exists for conversion compatibility with LoopStorageAction.
			Exit = 2		//Stop and return immediately.
		}


		/// <summary>Defines the parameters available in the lambda to find storage slots.</summary>
		/// <param name="storageIndex">Child index of the current storage shelf.</param>
		/// <param name="slotIndex">Child index of current storage slot.</param>
		/// <param name="productId">Product ID of the current storage slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current storage slot. Can be -1 if empty.</param>
		/// <returns>The LoopStorageAction to perform in the current loop. <see cref="LoopStorageAction"/></returns>
		public delegate LoopStorageAction StorageSlotFunction(int storageIndex, int slotIndex, int productId, int quantity);

		/// <summary>Defines the parameters available in the lambda to iterate through storage slots.</summary>
		/// <param name="storageIndex">Child index of the current storage shelf.</param>
		/// <param name="slotIndex">Child index of current storage slot.</param>
		/// <param name="productId">Product ID of the current storage slot. Can be -1 if unassigned or empty. 0 is a valid product.</param>
		/// <param name="quantity">Quantity of the product in the current storage slot. Can be -1 if empty.</param>
		/// <returns>The LoopAction to perform. <see cref="LoopAction"/></returns>
		public delegate LoopAction StorageLoopFunction(int storageIndex, int slotIndex, int productId, int quantity);

		/// <summary>
		/// Loops through all storages and its box slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCStorageTarget">True to skip the storage slots that are currently being targeted by an employee NPC.</param>
		/// <param name="storageSlotLambda">The StorageLoopFunction search lambda. See <see cref="StorageLoopFunction"/> for more information.</param>
		/// <returns></returns>
		private static void ForEachStorageSlotLambda(NPC_Manager __instance, bool checkNPCStorageTarget, StorageLoopFunction storageSlotLambda) {
			for (int i = 0; i < __instance.storageOBJ.transform.childCount; i++) {
				int[] productInfoArray = __instance.storageOBJ.transform.GetChild(i).GetComponent<Data_Container>().productInfoArray;
				int num = productInfoArray.Length / 2;
				for (int j = 0; j < num; j++) {
					//Check if this storage slot is already in use by another NPC
					if (checkNPCStorageTarget && NPC_TargetLogic.IsStorageSlotTargeted(i, j)) {
						continue;
					}
					int storageProductId = productInfoArray[j * 2];
					int quantity = productInfoArray[j * 2 + 1];

					if (storageSlotLambda(i, j, storageProductId, quantity) == LoopAction.Exit) {
						return;
					}
				}
			}
		}

		/// <summary>
		/// Loops through all storages and its box slots, and executes on each the lambda passed through parameter.
		/// </summary>
		/// <param name="__instance">NPC_Manager instance</param>
		/// <param name="checkNPCStorageTarget">True to skip the storage slots that are currently being targeted by an employee NPC.</param>
		/// <param name="storageSlotLambda">The StorageSlotFunction search lambda. See <see cref="StorageSlotFunction"/> for more information.</param>
		/// <returns></returns>
		private static StorageSlotInfo FindStorageSlotLambda(NPC_Manager __instance, bool checkNPCStorageTarget, StorageSlotFunction storageSlotLambda) {
			StorageSlotInfo freeStorageSlot = StorageSlotInfo.Default;

			if (__instance.storageOBJ.transform.childCount == 0) {
				return freeStorageSlot;
			}

			ForEachStorageSlotLambda(__instance, true,
				(storageId, slotId, productId, quantity) => {

					LoopStorageAction loopAction = storageSlotLambda(storageId, slotId, productId, quantity);

					if (loopAction == LoopStorageAction.Save || loopAction == LoopStorageAction.SaveAndExit) {
						freeStorageSlot.SetValues(storageId, slotId, productId, quantity);
					}

					//Their enum values are numerically equitative so it performs the correct action in ForEachStorageSlotLambda
					return (LoopAction)loopAction;
				});

			return freeStorageSlot;
		}


		//Copied from the original game. I only added the tarket marking, and slightly modified some superficial stuff to reduce the madness.
		//TODO 4 - Convert to transpile
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
						int shelfProductId = productInfoArray[k * 2];
						if (shelfProductId >= 0) {
							int shelfQuantity = productInfoArray[k * 2 + 1];
							int maxProductsPerRow = ReflectionHelper.CallMethod<int>(__instance, "GetMaxProductsPerRow", new object[] { j, shelfProductId });
							int shelfQuantityThreshold = Mathf.FloorToInt(maxProductsPerRow * productsThresholdArray[i]);
							if (shelfQuantity == 0 || shelfQuantity < shelfQuantityThreshold) {
								for (int l = 0; l < __instance.storageOBJ.transform.childCount; l++) {
									int[] productInfoArray2 = __instance.storageOBJ.transform.GetChild(l).GetComponent<Data_Container>().productInfoArray;
									int num5 = productInfoArray2.Length / 2;
									for (int m = 0; m < num5; m++) {
										//Check if this storage slot is already in use by another NPC
										if (NPC_TargetLogic.IsStorageSlotTargeted(l, m)) {
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
								productsPriority.Add(productsPrioritySecondary[Random.Range(0, productsPrioritySecondary.Count)]);
							}
						}
					}
				}
				if (productsPriority.Count > 0) {
					break;
				}
			}
			if (productsPriority.Count > 0) {
				return productsPriority[Random.Range(0, productsPriority.Count)];
			}
			return array;
		}

		private static GameObject GetClosestGroundBox(NPC_Manager __instance, GameObject employee) {

			//Filter list of ground boxes so we skip the ones already targeted by another NPC.
			List<GameObject> untargetedGroundBoxes = NPC_TargetLogic.GetListUntargetedBoxes(__instance.boxesOBJ);

			//Check that there are any untargeted boxes lying around to begin with.
			if (untargetedGroundBoxes.Count == 0) {
				return null;
			}

			List<GameObject> pickableGroundBoxes = null;

			//Check if there is space in storage for this box.
			//Performance.Start("IsThereUnusedFreeStorage", true);
			bool freeUnassignedStorageAvailable = IsFreeStorageContainer(__instance);
			//Performance.StopLogAndReset("IsThereUnusedFreeStorage");

			if (freeUnassignedStorageAvailable) {
				//Generate list of existing boxes on the ground
				pickableGroundBoxes = untargetedGroundBoxes;
			} else {
				//The quick check of unassigned storage slots got nothing, now we need to do the more expensive logic.

				//Get list of products for which there is an empty, but assigned, storage slot
				//Performance.Start("GetProductIdListOfFreeStorage", true);
				List<int> storableProductIds = GetProductIdListOfFreeStorage(__instance);
				//Performance.StopLogAndReset("GetProductIdListOfFreeStorage");

				if (storableProductIds.Count > 0) {
					//Get list of ground boxes for which there is an empty assigned storage slot of its product.
					//Performance.Start("GetStorableGroundBoxList", true);
					pickableGroundBoxes = GetStorableGroundBoxList(storableProductIds, untargetedGroundBoxes);
					//Performance.StopLogAndReset("GetStorableGroundBoxList");
				}
			}

			if (pickableGroundBoxes.Count == 0) {
				//No space in storage. Check if any ground boxes are empty and can be trashed.
				//Performance.Start("GetEmptyGroundBoxList", true);
				pickableGroundBoxes = GetEmptyGroundBoxList(untargetedGroundBoxes);
				//Performance.StopLogAndReset("GetEmptyGroundBoxList");
				if (pickableGroundBoxes.Count == 0) {
					return null;
				}
			}

			//Performance.Start("GetClosestGroundBox", true);
			GameObject closestGroundBox = GetClosestGroundBox(pickableGroundBoxes, employee.transform.position);
			//Performance.StopLogAndReset("GetClosestGroundBox");
			return closestGroundBox;
		}

		//TODO 1 - TEST THIS WORKS, after changing it to use FindStorageSlotLambda
		private static List<int> GetProductIdListOfFreeStorage(NPC_Manager __instance) {
			List<int> storableProductIds = new();

			ForEachStorageSlotLambda(__instance, true,
				(storageIndex, slotIndex, productId, quantity) => {

					if (quantity <= 0 && productId >= 0) {
						storableProductIds.Add(productId);
					}
					return LoopAction.Nothing;
				}
			);

			return storableProductIds;
		}

		private static List<GameObject> GetStorableGroundBoxList(List<int> storableProductIds, List<GameObject> untargetedGroundBoxes) {
			List<GameObject> storableGroundBoxes = new();

			foreach (GameObject gameObjectBox in untargetedGroundBoxes) {
				int boxProductID = gameObjectBox.GetComponent<BoxData>().productID;

				foreach (int storeProductId in storableProductIds) {
					if (boxProductID == storeProductId) {
						storableGroundBoxes.Add(gameObjectBox);
						break;
					}
				}
			}

			return storableGroundBoxes;
		}

		private static List<GameObject> GetEmptyGroundBoxList(List<GameObject> untargetedGroundBoxes) {
			List<GameObject> emptyGroundBoxes = new();

			foreach (GameObject gameObjectBox in untargetedGroundBoxes) {
				if (gameObjectBox.GetComponent<BoxData>().numberOfProducts == 0) {
					emptyGroundBoxes.Add(gameObjectBox);
				}
			}

			return emptyGroundBoxes;
		}

		private static GameObject GetClosestGroundBox(List<GameObject> groundBoxes, Vector3 sourcePos) {
			if (groundBoxes == null || groundBoxes.Count == 0) {
				return null;
			}

			GameObject closestBox = null;
			float closestDistanceSqr = float.MaxValue;

			foreach (GameObject groundBox in groundBoxes) {
				float sqrDistance = (groundBox.transform.position - sourcePos).sqrMagnitude;
				if (sqrDistance < closestDistanceSqr) {
					closestDistanceSqr = sqrDistance;
					closestBox = groundBox;
				}
			}

			return closestBox;
		}

	}


}
