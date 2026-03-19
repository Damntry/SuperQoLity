using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using Mirror;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.ExternalMods;
using SuperQoLity.SuperMarket.PatchClassHelpers.Components;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.Search;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Component;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Movement;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;


namespace SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule {

	public enum EmployeeJob {
		Unassigned = 0,
		Cashier = 1,
		Restocker = 2,
		Storage = 3,
		Security = 4,
		Technician = 5,
		OnlineOrder = 6,
		Manufacturer = 7,

		Any = 99
	}

	public enum EnumSecurityPickUp {
		[Description(nameof(Disabled))]
		Disabled,
		[Description(nameof(Reduced))]
		Reduced,
		[Description(nameof(Normal))]
		Normal,
		[Description("Always Maxed")]
		AlwaysMaxed
	}

	public enum EnumSecurityEmployeeThiefChase {
		[Description(nameof(Disabled))]
		Disabled,
		[Description("All chase but last one")]
		AllChaseButLastOne,
		[Description("Only one per Thief")]
		OnlyOnePerThief,
	}


    /// <summary>
    /// Changes:
    /// - Make employees acquire jobs, so its current target (a dropped box, or a storage/shelf slot) is "marked" and 
    ///		other employees ignore it. Work gets distributed instead of everyone trying to do the same thing.
    ///		
    ///		* The vanilla functionality is that when an npc seeks for jobs of its type and finds a valid one, it sets 
    ///			a destination towards the target or near it. Once reached, it checks that the target of the job is still there and valid,
    ///			in which case it executes it. Then proceeds to the next step of whatever job is doing and does more or less the same once again.
    ///			
    ///			Changed it so:
    ///				- Employees skips jobs whos target is already assigned to another npc.
    ///				- Every existing call from employees that sets a destination, now instead uses our own method that clears
    ///					whatever previous targets the npc had, so it frees any potentially unfinished jobs for other
    ///					employees. Then, if the destination being set was for an assignable target (this is decided
    ///					manually by us), then set that target as a marked job, for others to ignore, and save the job data.
    ///					Once it reaches the job location, it gets the NPCs job data and checks that is still valid, in case human
    ///					players changed something, and if valid, executes the job.
    ///					Then proceeds to the next step of whatever job is doing and does more or less the same once again.
    ///			
    /// - Configurable wait time for employees after finishing a job or idling. 
    ///		* Went through all StartWaitState usages to assign the appropiate config value.
    ///	- When an npc searchs for a box on the floor, get the closest one instead of one random.
    ///		* New method GetClosestGroundBox that replaces the one to get a random box.
    /// </summary>
    public class EmployeeJobAIPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee patch failed. Employee Module inactive";


		public override void OnPatchFinishedVirtual(bool isActive) {
			if (isActive) {
                WorldState.NPC_Events.OnEmployeeSpawned += EmployeeSpawned;
            }
		}

		//TODO 2 - Make this be activable when Debug is enabled and you press a certain hotkey, so
		//			I can help people with npc problems.
		//			Make it send a notification in-game to confirm its enabled/disabled.
		public const bool LogEmployeeActions = false;

        /// <summary>False if the new security pick up system failed to start.</summary>
        private static bool newProdPickUpWorking;

        private float destroyCounter;


		private static Queue<StolenProductSpawn> stolenProdPickups;

		/// <summary>The default number of units that an npc transfers between box and shelf.</summary>
		public static int NumTransferItemsBase { get; } = 1;

        /// <summary>The level at which Security employees stop getting bonus stats for range and pick-up count.</summary>
        public static int MaxSecurityPickUpLevel { get; } = 200;

		/// <summary>Multiplier over the extra range provided by security levels.</summary>
		public static float SecurityPickUpRangeLevelMult { get; } = 1.25f;
		/// <summary>Layer used to mark stolen products on the ground.</summary>
		public static int SecurityPickUpLayer { get; } = 25;

		/// <summary>
		/// The number of Security levels (starting at zero) to get an extra pick-up.
		/// The max possible pickup count can be calculated as: 
		/// pickUpCount = MaxSecurityPickUpLevel / LevelsForExtraPickUp
		/// </summary>
		public static float LevelsForExtraPickUp { get; } = 10;

		//TODO 0 - Do manufacturing reservation.


		//TODO 2 - Instead of the massive and possibly unmaintenable change of a full IA rework, I should go halfway.
		/* 
		
		Problem: When Restocker or Storage NPCs take turns to do job actions, some of them do thousands of loops 
		through Unity objects of shelves/storage/boxes, to find if there is any work to do, which is slow and inefficient. 
		Bigger stores and more employees makes it exponentially worse.

		Solution: Create a job scheduler to handle jobs and job reservations.
		When any object that can be involved in a job, changes its state in a way that creates a job (ex: a storage slot is
		emptied and could now be filled) it will add a job to the job scheduler, with the type of job and the relevant
		object data (shelf indexes, quantity, product id, unique netId, etc). 
		Adding a job will be a very light operation that will just dump the data into a concurrent dictionary of pending jobs.

		Now that all the data is outside Unity APIs, we can use multithreading.
		The job scheduler will be a background task looking through pending jobs and trying to match 
		each other.
		Since its working with a much smaller set of data, and outside of the main thread, it will have a minimal impact
		on the game performance compared to the current method.
		Matched jobs will go into a separate concurrent dictionary of available jobs.
		The scheduler can check processing time / FPS, and throttle as needed to slow down if the potato
		pc is struggling, or speed up and do more complicated distance checks or what not.
			
		From the NPC side, when it looks for a certain kind of job, it ll do a quick dictionary lookup, pick the first one, and
		check if it is still valid (in case of human intervention and/or job scheduler lagging behind). If its good, the job is
		reserved for the NPC and it can be started. If its invalid, the job is removed and the npc repeats the lookup process.

		*/

		//TODO 4 - Massive change. I should trash EmployeeNPCControl and create a new system to handle npc jobs.
		//	The npc employeeObj should have a new Navigation npc that handles its own movement in an Update, and
		//	when on target, triggers an event.
		//	A new Singleton JobScheduler GameObject would now take charge of moving employees around and to assign jobs.
		//	It will hook into an npc Navigation to be notified when the worker is ready. Jobs functions would be async (single-threaded). 
		//	In this scheduler we can setup job frequency to finetune however we want to meet performance goals. Some
		//	types of jobs could be prioritized to get more scheduler time compared to less important ones.
		//	The current reservation system in EmployeeTargetReservation would be split and moved into the Jobs and Navigation
		//	systems accordingly.
		//	Then the job step system would be reworked and split into different functions with common logic separated from it.
		//	EmployeePerformancePatch would now simply remove the call to EmployeeNPCControl, since the scheduler will
		//	activate and handle the jobs itself.
		//	Basically this would make it so I keep my own Employee AI system, and whatever the dev releases after it, I would
		//	have to implement manually or maybe adapt and extend, but any changes would now be much easier to make.
		//
		//	Use Unity movement only for the most basic stuff, and handle deceleration, acceleration, and rotation myself.
		//	For rotation, disable updateRotation and use Quaternion.RotateTowards to set the destination, aswell as
		//	controlling how much should it decelerate when approaching the end of a path point, based on how tight the curve
		//	is so it doesnt overshoot targets, and the acceleration towards the next one depending on remaining distance.
		//	Maybe use Vector3.SmoothDamp for better looking accel/decel, but I need to check how expensive it is.
		//
		//	The job system would change so instead of employees going through every object of a certain kind to see if
		//	there is a job to do, now jobs will be emited by the object themselves as they become available.
		//	Example: A box is dropped or spawned so the box adds a new job to the scheduler with itself as a source. Then that 
		//	box is inserted into storage which generates a "pick up available" job.
		//	Adding a job will be a very simple and light operation, and employees will search jobs through this much smaller
		//	subset of data which will speed up npc performance, specially on massive supermarkets.
		//	Also, jobs themselves will be acquired instead of targets, which will make the reservation system much simpler, with
		//	the old one being discarded.
		//	It too has the advantage of the emitter being able to set its own attributes, like priority (a product shelf 
		//	warning of how empty it is).
		//	I see 2 disadvantages:
		//	 * It will use a bit more memory in general to save all the jobs, but the amount should 
		//		be so small it wont really make a difference, and the performance gains will be big, and it
		//		should reduce potential stuttering when too many employees are seeking jobs.
		//	 * The new system will be less "safe" in general. If Im not adding or removing a job at its proper place in some
		//		rare case, it could lead to problems which would not happen if the npc just searchs in every object at its
		//		real current state. Nevertheless, its still 100% worth it.


		//TODO 4 - Base game error. If an restocker is on its way to place items in a shelf, and the shelf
		//	is deleted, a never ending torrent of exceptions show up.
		//	Its because on the NPC method, every single "__instance.whateverOBJ.transform.GetChild(index)"
		//	is unchecked. Think of fixing it some day but its low priority.

		//TODO 3 Job State Machine - Try and convert EmployeeNPCControl into a state machine.
		//	Basically, each case would be its own method that redirects to a (descriptive) enum.
		//	This method would just be called at first to prepare all basic data, and depending
		//	on the enum value, it would call one or another method.
		//
		//I could do another thing, which is try and represent, somehow, the whole logic flow in the
		//	starting method in a readable way, independently of the logic. Or at least expose the possible
		//	logical options for each specific step.
		//	The only way I can think of doing this is by doing a kind of "wizard" or tree style flow, in which each step can move
		//	onto this or that next step, or go back, but it would need to go through all previous steps.

		//TODO 3 Job State Machine - Make it so from the logic flow method, you can click and go directly into a relevant method with the code.
		//		Either through some comment, or having the method directly or what, but checking the logic has to be fast and direct.
		//TODO 3 Job State Machine - Methods that are steps in the logic, must somehow expose the enum values they can return.
		//		So basically we would not make mistakes in the logic flow because the possible enums are contained 
		//		in the very method using them, and we could check that si using something from the outside should
		//		give a compile error, or at least a runtime one.
		//		This would mean that each step method would have to be inside a class. I dont know, give it a think.

		/*
		//TODO 3 Job State Machine - Make these very descriptive. The point is to read it and know whats the current situation 
		//	for the npc. But dont specify where its coming from since there can be multiple.
		public enum StorageJobCondition {
			None

		}

		public class DonerKebaber {

			HashSet<StorageJobCondition> jobStepsDone = new();


			public StorageJobCondition nextCondition;

			public bool JobStep(StorageJobCondition condition, StorageJob nextJobStep) {
				if (condition == nextCondition) {
					nextCondition = nextJobStep.JobMethod();
					if (nextCondition == StorageJobCondition.None) {
						//All jobs need to return a valid step to know where to go from here
						throw InvalidOperationException;
					}
					jobStepsDone.Add(condition);

					return true;
				} else {
					return jobStepsDone.Contains(condition);
				}
			}

			/// <summary>
			/// For returning
			/// </summary>
			/// <param name="condition"></param>
			/// <param name="conditionToReturn"></param>
			/// <returns></returns>
			public bool JobStepReturn(StorageJobCondition condition, StorageJobCondition conditionToReturn) {

				if (condition == nextCondition) {
					//TODO 3 Job State Machine - Search condition in the stack from the top
					//	Remove up to it
					//	Return true
					return true;
					//
					//Throw error if condition is not in the stack
				} else {
					return false;
				}
			}


		}

		public record StorageJob(Func<StorageJobCondition> JobMethod);
		public struct StorageJobs {
			


			public static StorageJob TakeGroundBox = new(() => Storage_TakeGroundBox());
			public static StorageJob GoPlaceBoxStorage = new(() => Storage_GoPlaceBoxStorage());

		}


		public enum EndAction {
			Exit,
			Repeat
		}

		private EndAction EmployeeLogicExample() {
			DonerKebaber currentDoner = new();

			if (currentDoner.JobStep(StorageJobCondition.RestingPlace, StorageJobs.CheckForJobs)) {
				if (currentDoner.JobStep(StorageJobCondition.PickableBoxOnGround, StorageJobs.TakeGroundBox)) {
					if (currentDoner.JobStep(StorageJobCondition.StorageFree, StorageJobs.GoPlaceBoxStorage)) {
						if (currentDoner.JobStepReturn(StorageJobCondition.BoxPlacedInStorage, StorageJobCondition.RestingPlace)) {
							//EmployeeLogicExample will be called again by the parent for the same npc
							return EndAction.Repeat;
						}
					}
					if (currentDoner.JobStep(StorageJobCondition.SomeThing, StorageJobs.SomeOtherAlternative)) {
						return EndAction.Exit;
					}
				}
			}

			//If we reach this point, something went wrong and there was a case that returned a condition
			//	that had no job to go into with its current flow.
			throw new InvalidOperationException;
		}
		*/

		/* TODO 1 - Fix npc pathfinding
			First of all, prepare a save properly. All franchise products locked except the starting one. 
			Only fill certain shelves so I can find which ones make the customer bug out in one or 
				another way so I have perfect test cases.
			Customers will highlight the target shelf.
			Create visible waypoints for the next path the customers will take.
				Use Debug.DrawLine for this
			Set the time of day to after the store is supposed to be open:
				GameData.Instance.NetworktimeOfDay = 9
				Customers will come but time will not progress, so they will keep coming indefinitely so I can keep testing.
			

			 *** Bugs

				pathPending = false, remainingDistance = XXX, hasPath = true, status = PathComplete

				- With the values above for the customer, and yet, it can not really properly reach the self checkout destination. It decides to go the direct path obstructed by a fence, 
					instead of taking the safe but long route around. Perhaps because it thinks it can squish close enough with the direct route. And its true, eventually it does manage to do it, 
					but it looks terrible and takes a while.
					Maybe I need to sample for spaces around it?
		
				- The issue with NPCs colliding with each other is solved in NPC_CustomerNavFixer. 

				- Not really a bug but something to take into a account. The navmesh resolution might have been baked with relatively low resolution, so a few cm to a side wont mean much to it.
					Nothing I can do to solve this, but knowing this I could take alternative approaches.
	

			 *** Possible overall solutions and useful stuff

				- This can precalculate if the NPC can really reach the place, but probably expensive:

					NavMeshPath path = new NavMeshPath();
					var agent = NPC_Manager.Instance.customersnpcParentOBJ.transform.GetChild(0)
						.gameObject.GetComponent<NavMeshAgent>();
					if (agent.CalculatePath(agent.destination, path)){
						Log("Path is wrong - " + path.status);
					}

				- To force a recalculate. This could be useful in the case where an NPC gets stuck but tries to keep moving with little success.
					agent.ResetPath();
					agent.SetDestination(targetPosition);
		*/
		
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.Awake))]
		[HarmonyPostfix]
		public static void AwakePatch(NPC_Manager __instance) {
            newProdPickUpWorking = false;

            stolenProdPickups = new();

			//TODO 3 - My idea was to create an unique npcAgentPrefab for each NPCType, but
			//	I cant instantiate a scene object using a NetIdentity or Mirror wouldnt know which
			//	one to use, since they share the same sceneId. I cant change it to be an assetId
			//	instead because then it wouldnt work for clients that dont have the mod.
			//	Try to find an alternative so not every NPC has every type of functionality.

			GameObject stolenProductPrefab = __instance.npcAgentPrefab?.GetComponent<NPC_Info>()?.stolenProductPrefab;
			if (!stolenProductPrefab) {
				TimeLogger.Logger.LogFatal("An object in the chain \"NPC_Manager.npcAgentPrefab." +
					"NPC_Info.stolenProductPrefab\" is null. It was probably renamed of changed places. " +
					"The employee patches cant be used and will be disabled.", LogCategories.AI);

				Container<EmployeeJobAIPatch>.Instance.UnpatchInstance();

				TimeLogger.Logger.SendMessageNotificationError("Something changed due to a game update " +
					"and the employee module can no longer work. It was disabled so the rest of the " +
					"mod can still work", skipQueue: false);

                return;
			}

			stolenProductPrefab.layer = SecurityPickUpLayer;
			//Begin loop to check for stolen products marked for removal.
			__instance.StartCoroutine(
				Container<EmployeeJobAIPatch>.Instance.PickupMarkedStolenProductsLoop()
			);

            newProdPickUpWorking = true;
        }

		private static void EmployeeSpawned(NPC_Info npcInfo, int index) {
            GenericNPC.AddSuperQolNpcObjects(npcInfo.gameObject, NPCType.Employee);
        }

        /// <returns>False when the npc hasnt done any meaningful work. Otherwise True.</returns>
        public static bool EmployeeNPCControl(NPC_Manager __instance, GameObject employeeObj, NPC_Info employee) {
			int state = employee.state;

			if (employee.employeeDismissed) {
				if (Vector3.Distance(employeeObj.transform.position, __instance.employeeSpawnpoint.transform.position) < 2f) {
					NetworkServer.Destroy(employeeObj);
					return true;
				}
				return false;
			}
			if (__instance.employeesOnRiot) {
				if (employee.equippedItem > 0) {
					__instance.DropBoxOnGround(employee);
					__instance.UnequipBox(employee);
				}
				if (state != 0) {
					employee.state = 0;
				}
				return true;
			}
			if (state == -1) {
				return false;
			}

			if (!EmployeeNPC.TryGetEmployeeFrom(employeeObj, out EmployeeNPC employeeSQoL)) {
				return true;
			}

			int taskPriority = employee.taskPriority;
			if (taskPriority == 4 && state == 2) {
				if (employee.currentChasedThiefOBJ) {
					if (employee.currentChasedThiefOBJ.transform.position.x < -15f || employee.currentChasedThiefOBJ.transform.position.x > 38f || employee.currentChasedThiefOBJ.GetComponent<NPC_Info>().productsIDCarrying.Count == 0) {
						employee.state = 0;
						return true;
					}
					if (Vector3.Distance(employeeObj.transform.position, employee.currentChasedThiefOBJ.transform.position) < 2f) {
						//This is basically saying "stop where you are".
						employeeSQoL.MoveEmployeeTo(employeeObj, employee.currentChasedThiefOBJ);
						employee.state = 3;
					} else {
						employee.CallPathing();
					}
				} else {
					employee.state = 0;
				}
			}

			NavMeshAgent employeeNavAgent = employeeObj.GetComponent<NavMeshAgent>();

			if (IsEmployeeAtDestination(employeeNavAgent, out float stoppingDistance)) {
				//Non limited so NPCs turn fast enough to look at the target before any following order cancels it.
				employeeSQoL.StartLookProcess(RotationSpeedMode.EmployeeTarget);

				switch (taskPriority) {
					case 0: //Unassigned

						//Shouldnt be needed, but acts as extra safety for clearing reservations
						//	if they get stuck on this NPC for some reason.
						employeeSQoL.ClearNPCReservations();

						if (state != 0) {
							if (state != 1) {
								employee.state = 0;
								return true;
							}
						} else {
							if (employee.equippedItem > 0) {
								__instance.DropBoxOnGround(employee);
								UnequipBox(employee);
								return true;
							}

							employeeSQoL.MoveEmployeeToRestPosition();
							employee.state = 1;
							return true;
						}
						break;
                    /*************************************/
                    /************   CASHIER   ************/
                    /*************************************/
                    case 1: {
							switch (state) {
								case 0:
								case 1: {
										if (employee.equippedItem > 0) {
											__instance.DropBoxOnGround(employee);
											UnequipBox(employee);
											return true;
										}

										int num = __instance.CashierGetAvailableCheckout();
										if (num != -1) {
											employee.employeeAssignedCheckoutIndex = num;
											__instance.UpdateEmployeeCheckouts();
											GameObject targetCheckout = __instance.checkoutOBJ.transform.GetChild(num).gameObject;
											employeeSQoL.MoveEmployeeTo(targetCheckout.transform.Find("EmployeePosition")
												.transform.position, targetCheckout);
											employee.state = 2;
											return true;
										}
										employee.state = 10;
										return true;
									}
								case 2:
									employee.RPCNotificationAboveHead("NPCemployee0", "");
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
									employee.state = -1;
									return true;
								case 3:
									if (__instance.CheckIfCustomerInQueue(employee.employeeAssignedCheckoutIndex)) {
										if (!__instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().checkoutQueue[0]) {
											employee.state = 4;
											return true;
										}
										if (__instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().productsLeft > 0) {
											employee.state = 5;
											return true;
										}
										employee.state = 4;
										return true;
									} else {
										if (__instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().isCheckoutClosed) {
											employee.employeeAssignedCheckoutIndex = -1;
											employee.state = 0;
											return true;
										}
										employee.state = 4;
									}
									return true;
								case 4:
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
									employee.state = -1;
									return true;
								case 5: {
										if (__instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().productsLeft == 0) {
											employee.state = 7;
											return true;
										}
										float timeToWait3 = Mathf.Clamp(__instance.productCheckoutWait - employee.cashierLevel * 0.01f, 0.05f, 1f);
										employee.StartWaitState(timeToWait3, 6);
										employee.state = -1;
										return true;
									}
								case 6: {
										List<GameObject> internalProductListForEmployees = __instance.checkoutOBJ.transform
											.GetChild(employee.employeeAssignedCheckoutIndex)
											.GetComponent<Data_Container>().internalProductListForEmployees;
										int num2 = employee.cashierLevel / 15;
										num2 = Mathf.Clamp(num2, 1, 10);
										int num3 = Mathf.Clamp(employee.cashierValue - num2 - 1, 2, 10);
										int num4 = 0;
										for (int i = 0; i < internalProductListForEmployees.Count; i++) {
											GameObject val = internalProductListForEmployees[i];
											if (val) {
												employee.cashierExperience += num3;
												val.GetComponent<ProductCheckoutSpawn>().AddProductFromNPCEmployee();
												num4++;
												if (num4 >= num2) {
													break;
												}
											}
										}
										employee.state = 5;
										return true;
									}
								case 7:
									GameObject currentNPC = __instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().currentNPC;
									if (!currentNPC) {
										employee.state = 3;
									}else if (currentNPC.GetComponent<NPC_Info>().alreadyGaveMoney) {
										__instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().AuxReceivePayment(0f, true);
										employee.state = 3;
										return true;
									}
									float timeToWait2 = Mathf.Clamp(__instance.productCheckoutWait - employee.cashierLevel * 0.01f, 0.05f, 1f);
									employee.StartWaitState(timeToWait2, 7);
									employee.state = -1;
									return true;
								case 10:
									if (Vector3.Distance(employee.transform.position, __instance.restSpotOBJ.transform.position) > 3f) {
										employeeSQoL.MoveEmployeeToRestPosition();
										return true;
									}
									employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
									employee.state = -1;
									return true;
								default:
									employee.state = 0;
									return true;
							}
						}
                    /*************************************/
                    /***********   RESTOCKER   ***********/
                    /*************************************/
                    case 2:
						switch (state) {
							case 0: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)} logic begin.", LogEmployeeActions);
									if (employee.equippedItem > 0) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Box in hand. Dropping.", LogEmployeeActions);
										__instance.DropBoxOnGround(employee);
										UnequipBox(employee);
										return true;
									}

									bool matchFound = RestockJobsManager.GetAvailableRestockJob(
										__instance, out RestockJobInfo restockJob);
									employee.SetRestockJobInfo(restockJob);

									if (matchFound) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Products available, moving to storage.", LogEmployeeActions);
										Vector3 destination = __instance.storageOBJ.transform.GetChild(restockJob.Storage.ShelfIndex).Find("Standspot").transform.position;
										employeeSQoL.MoveEmployeeToStorage(destination, restockJob.Storage);

										//Reserve too the product shelf slot that will be used for next step.
										employeeSQoL.AddExtraProductShelfTarget(restockJob.ProdShelf);
										employee.state = 2;
										return true;
									}
									if (Vector3.Distance(employee.transform.position, __instance.restSpotOBJ.transform.position) > 6.5f) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Moving to rest spot.", LogEmployeeActions);

										employeeSQoL.MoveEmployeeToRestPosition();
										return true;
									}

									employeeSQoL.ClearNPCReservations();
									employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
									employee.state = -1;
									return true;
								}
							case 1:
								return true;
							case 2: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Storage reached.", LogEmployeeActions);
									RestockJobInfo restockJob = employee.GetRestockJobInfo();

									if (employeeSQoL.RefreshAndCheckValidTargetedStorage(__instance, clearReservation: false, out StorageSlotInfo storageSlotInfo) &&
											//Check that for the next step we can still place items in the shelf.
											employeeSQoL.RefreshAndCheckValidTargetedProductShelf(__instance, restockJob)) {

										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Picking box.", LogEmployeeActions);

										Transform storageT = __instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex);
										Data_Container dataContainer = storageT.GetComponent<Data_Container>();

										if (storageT.Find("CanvasSigns")) {
											dataContainer.EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, storageSlotInfo.ExtraData.ProductId, -1);
										} else {
											dataContainer.EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, -1, -1);
										}

										employee.NetworkboxProductID = storageSlotInfo.ExtraData.ProductId;
										employee.NetworkboxNumberOfProducts = storageSlotInfo.ExtraData.Quantity;
										employee.EquipNPCItem(1);

										GameObject targetShelfObj = __instance.shelvesOBJ.transform.GetChild(restockJob.ProdShelf.ShelfIndex).gameObject;
										employeeSQoL.MoveEmployeeToShelf(targetShelfObj.transform.Find("Standspot").transform.position, restockJob.ProdShelf);
										employee.state = 3;
										return true;
									}
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Either storage or shelf reservations didnt match.", LogEmployeeActions);
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									employee.state = -1;
									return true;
								}
							case 3:	//Case 4 takes care of validation already
							case 4: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Checking if shelf is valid.", LogEmployeeActions);
									RestockJobInfo restockJob = employee.GetRestockJobInfo();

									if (employeeSQoL.RefreshAndCheckValidTargetedProductShelf(__instance,
											restockJob)) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Shelf reached fully valid.", LogEmployeeActions);

										Data_Container component4 = __instance.shelvesOBJ.transform.GetChild(restockJob.ProdShelf.ShelfIndex).GetComponent<Data_Container>();
										int maxProductsPerRow = restockJob.MaxProductsPerRow;
										int shelfQuantity = restockJob.ProdShelf.ExtraData.Quantity;

										if (employee.NetworkboxNumberOfProducts > 0 && shelfQuantity < maxProductsPerRow) {
											LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Adding products to shelf row.", LogEmployeeActions);
											//Base game calculation
											int shelfNumTransfer = maxProductsPerRow - shelfQuantity;
											shelfNumTransfer = Mathf.Clamp(shelfNumTransfer, NumTransferItemsBase, employee.restockerLevel);
											int boxNumTransfer = Mathf.Clamp(employee.boxNumberOfProducts, NumTransferItemsBase, employee.restockerLevel);
											int numItemsTransfer = Mathf.Min(shelfNumTransfer, boxNumTransfer);
											//Now recalculate based on mod settings
											numItemsTransfer = IncreasedItemTransferPatch.GetNumTransferItems(
												employee.boxNumberOfProducts, shelfQuantity, maxProductsPerRow, IncreasedItemTransferPatch.CharacterType.Employee, numItemsTransfer);
											component4.EmployeeAddsItemToRow(restockJob.ShelfProdInfoIndex, numItemsTransfer);

											employee.NetworkboxNumberOfProducts = employee.boxNumberOfProducts - numItemsTransfer;
											employee.StartWaitState(__instance.employeeItemPlaceWait, 4);
											employee.state = -1;
											employee.restockerExperience += employee.restockerValue;
											return true;
										}
									}

									//Getting to this point is ok in 2 cases:
									//	- A player fills or changes the product of the shelf, before the reserving npc reaches it.
									//	- The reserving npc has already filled the shelf himself and cant place anymore, since
									//		this switch case is called repeatedly over and over until the shelf is full.
									//	- The target shelf or some other has been deleted and the internal index vs job index dont match anymore.
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Target shelf is already full or not valid. Searching for a different one.", LogEmployeeActions);
									int productId = restockJob.ProdShelf.ExtraData.ProductId;
									if (employee.NetworkboxNumberOfProducts > 0 &&
											ContainerSearch.CheckIfProdShelfWithSameProduct(__instance, productId, employee, out var result)) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Found another different shelf to add products.", LogEmployeeActions);
										employee.UpdateRestockJobInfo(result.productShelfSlotInfo, result.maxProductsPerRow);
										employeeSQoL.MoveEmployeeToShelf(result.productShelfSlotInfo.ExtraData.Position, result.productShelfSlotInfo);
										employee.state = 3;
										return true;
									}
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Box is empty or there is no other shelf with the same product.", LogEmployeeActions);

									employee.state = 5;
									return true;
								}
							case 5: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Box in hand but cant restock anymore. Deciding what to do.", LogEmployeeActions);
									if (employee.NetworkboxNumberOfProducts <= 0) {
										MoveToBalerOrGarbageContainer(__instance, employeeObj, employee, employeeSQoL,
											cardboardTargetState: 30, trashTargetState: 6, recycleTargetState: 9);
										return true;
									} else {
										if (GetStorageContainerWithBoxToMerge(__instance, employee, employeeSQoL)) {
											return true;
										}
										StorageSlotInfo freeStorageIndexes = ContainerSearch.GetFreeStorageContainer(__instance, employeeObj.transform, employee.NetworkboxProductID);
										if (freeStorageIndexes.ShelfFound) {
											LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Moving to storage to place box.", LogEmployeeActions);
											Vector3 destination = __instance.storageOBJ.transform.GetChild(freeStorageIndexes.ShelfIndex).gameObject.transform.Find("Standspot").transform.position;
											employeeSQoL.MoveEmployeeToStorage(destination, freeStorageIndexes);
											employee.state = 7;
											return true;
										}
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Moving to left over boxes spot.", LogEmployeeActions);
										employeeSQoL.MoveEmployeeTo(__instance.leftoverBoxesSpotOBJ, null);
										employee.state = 8;
									}
									return true;
								}
							case 6:
								LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Removing box in hand", LogEmployeeActions);
								UnequipBox(employee);
								return true;
							case 7: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Arrived at storage, but checking again " +
										$"if there is some other slot to merge with.", LogEmployeeActions);
									//This is basically just a "recheck". It couldnt find a merge on the previous step, so
									//	it just went to storage to place the box. And now it does the same merge check again
									//	in case something freed up.
									if (GetStorageContainerWithBoxToMerge(__instance, employee, employeeSQoL)) {
										return true;
									}
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: No merge possible.", LogEmployeeActions);
									if (employeeSQoL.RefreshAndCheckValidTargetedStorage(__instance, clearReservation: false, out StorageSlotInfo storageSlotInfo)) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Placing box in storage.", LogEmployeeActions);
										__instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex).GetComponent<Data_Container>().
											EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, employee.NetworkboxProductID, employee.NetworkboxNumberOfProducts);
										employee.state = 6;
										return true;
									}

									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Target storage is not valid anymore.", LogEmployeeActions);
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 5);
									employee.state = -1;
									return true;
								}
							case 8: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Dropping box at left over spot.", LogEmployeeActions);
									Vector3 vector4 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 4f, UnityEngine.Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector4, employee.NetworkboxProductID, employee.NetworkboxNumberOfProducts);
									employee.state = 6;
									return true;
								}
							case 9: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Recycling box.", LogEmployeeActions);
									float num7 = 1.5f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
                                    StatisticsManager.Instance.totalBoxesRecycled++;
                                    SMTAntiCheat_Helper.Instance.CmdAlterFunds(num7);

									employee.state = 6;
									return true;
								}
							case 20: {
                                    LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Reached storage and merging contents.", LogEmployeeActions);
									EmployeeTryMergeBoxContents(__instance, employee, employeeSQoL, 5);
									return true;
								}
							case 30: {
									if (employee.closestCardboardBaler) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Adding empty box to cardboard baler.", LogEmployeeActions);
										employee.closestCardboardBaler.GetComponent<CardboardBaler>().AuxiliarAddBoxToBaler(employee.boxProductID);
										UnequipBox(employee);
									} else {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Cardboard baler not there anymore.", LogEmployeeActions);
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
										employee.state = -1;
									}
									return true;
								}
						}

						employee.state = 0;
						return true;
                    /*************************************/
                    /************   STORAGE   ************/
                    /*************************************/
                    case 3:
						switch (state) {
							case 0:
								LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)} logic begin.", LogEmployeeActions);

								if (employee.equippedItem > 0) {
									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Dropping current box.", LogEmployeeActions);
									__instance.DropBoxOnGround(employee);
									UnequipBox(employee);
									return true;
								}
								var closestGroundBox = GroundBoxSearch.GetClosestGroundBox(__instance, employeeObj.transform);
								if (closestGroundBox.FoundGroundBox) {
									bool posOk = NavMesh.SamplePosition(new Vector3(closestGroundBox.GroundBoxObject.transform.position.x,
										0f, closestGroundBox.GroundBoxObject.transform.position.z), out NavMeshHit navMeshHit, 1f, -1);
									if (posOk) {
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Going to pick up box.", LogEmployeeActions);
										employee.randomBox = closestGroundBox.GroundBoxObject;
										employeeSQoL.MoveEmployeeToBox(navMeshHit.position, closestGroundBox.GroundBoxObject);
										if (closestGroundBox.HasStorageTarget) {
											employeeSQoL.AddExtraStorageTarget(closestGroundBox.StorageSlot);
										}
										employee.state = 1;
										return true;
									}
								}

                                //TODO 1 AI Improvements - Instead of going to rest, find boxes in
                                //	storage that can be merged (fully or partially) into another.

                                employee.state = 10;
								return true;
							case 1: {
									if (employee.randomBox) {
										//Ignore height of the box when checking if we are close enough
										Vector3 vector2 = new Vector3(employee.randomBox.transform.position.x, 0f, employee.randomBox.transform.position.z);
										Vector3 vector3 = new Vector3(employee.transform.position.x, 0f, employee.transform.position.z);
										if (Vector3.Distance(vector2, vector3) < 2f) {
											LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Picking up box.", LogEmployeeActions);
											BoxData component5 = employee.randomBox.GetComponent<BoxData>();
											employee.NetworkboxProductID = component5.productID;
											employee.NetworkboxNumberOfProducts = component5.numberOfProducts;
											employee.EquipNPCItem(1);
											GameData.Instance.GetComponent<NetworkSpawner>().EmployeeDestroyBox(employee.randomBox);
											if (component5.numberOfProducts > 0) {
												employee.state = 18;
												return true;
											}
											employee.state = 6;
											return true;
										}
									}
									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Box doesnt exist or is not at pick up range anymore.", LogEmployeeActions);
									//Box got picked up by someone else, or got moved by physics
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									employee.state = -1;
									return true;
								}
							case 2: {
									StorageSlotInfo targetStorage = null;
									Vector3 destination = Vector3.zero;

									bool validStorageFound = false;
									if (employeeSQoL.HasTargetedStorage()) {
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Checking pre-reserved storage.", LogEmployeeActions);
										validStorageFound = employeeSQoL.RefreshAndCheckValidTargetedStorage(__instance, clearReservation: true, out targetStorage);
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Pre-reserved storage is {(validStorageFound ? "" : "no longer ")}valid.", LogEmployeeActions);
									}

									if (!validStorageFound) {
										//This happens if the storage is no longer valid from player input, or if we are 
										//	coming from a step where we need to find a new storage without reservation.
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Searching for storage to place held box.", LogEmployeeActions);
										targetStorage = ContainerSearch.GetFreeStorageContainer(__instance, employeeObj.transform, employee.NetworkboxProductID);
										validStorageFound = targetStorage.ShelfFound;
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Free storage {(validStorageFound ? "" : "couldnt be ")}found.", LogEmployeeActions);
									}

									if (validStorageFound) {
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Moving to storage to place box.", LogEmployeeActions);
										destination = __instance.storageOBJ.transform.GetChild(targetStorage.ShelfIndex).Find("Standspot").transform.position;
										employeeSQoL.MoveEmployeeToStorage(destination, targetStorage);
										employee.state = 3;
										return true;
									}

									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Moving to drop box at left over spot.", LogEmployeeActions);
									employeeSQoL.MoveEmployeeTo(__instance.leftoverBoxesSpotOBJ, null);
									employee.state = 4;
									return true;
								}
							case 3: {
									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Arrived at storage.", LogEmployeeActions);
									if (employeeSQoL.RefreshAndCheckValidTargetedStorage(__instance, clearReservation: false, out StorageSlotInfo storageSlotInfo)) {
										LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Placing box in storage.", LogEmployeeActions);
										__instance.storageOBJ.transform.GetChild(storageSlotInfo.ShelfIndex).GetComponent<Data_Container>().
											EmployeeUpdateArrayValuesStorage(storageSlotInfo.SlotIndex * 2, employee.NetworkboxProductID, employee.NetworkboxNumberOfProducts);
										employee.state = 5;
										employee.storageExperience += employee.storageValue;
										return true;
									}

									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Storage no longer valid.", LogEmployeeActions);
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 2);
									employee.state = -1;
									return true;
								}
							case 4: {
									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: At left over spot. Spawning box at drop.", LogEmployeeActions);
									Vector3 vector6 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 3f, UnityEngine.Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(vector6, employee.NetworkboxProductID, employee.NetworkboxNumberOfProducts);
									employee.state = 5;
									return true;
								}
							case 5:
								LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Removing box in hand.", LogEmployeeActions);
								UnequipBox(employee);
								return true;
							case 6: {
									MoveToBalerOrGarbageContainer(__instance, employeeObj, employee, employeeSQoL,
										cardboardTargetState: 33, trashTargetState: 5, recycleTargetState: 7);
									return true;
								}
							case 7: {
									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Recycling.", LogEmployeeActions);
									float num10 = 1.5f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
                                    StatisticsManager.Instance.totalBoxesRecycled++;
                                    SMTAntiCheat_Helper.Instance.CmdAlterFunds(num10);
									employee.state = 5;
									return true;
								}
							case 10:
								if (Vector3.Distance(employee.transform.position, __instance.restSpotOBJ.transform.position) > 6.5) {
									LOG.TEMPDEBUG_FUNC(() => $"Storage #{GetUniqueId(employee)}: Moving to rest spot.", LogEmployeeActions);
									employeeSQoL.MoveEmployeeToRestPosition();
									return true;
								}

								//Though it makes sense to clear reservations here anyway, it was put specifically to solve these 2 cases:
								//	- Boxes still falling out of the sky will sometimes not set a new destination for the storage worker.
								//		This makes it so they stay at the rest spot and go to the next step, which is.. going to the rest spot.
								//		Since they dont need to call a MoveEmployeeTo... method, which clears targets, its reservations get stuck.
								//	- Like above, but happens when a box is dropped next to the rest spot, ant then gets pushed away before the
								//		npc reaches it. Employee is already at the rest spot, so it doesnt call the move method that
								//		clears reservations.
								employeeSQoL.ClearNPCReservations();
								employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								employee.state = -1;
								return true;
							case 18:
								bool multipleBoxes = ContainerSearch.MoreThanOneBoxToMergeCheck(__instance, employee.boxProductID);
								if (multipleBoxes) {
									employee.state = 19;
									return true;
								}
								employee.state = 2;
								return true;
							case 19: {
									if (employee.boxNumberOfProducts <= 0) {
										employee.state = 6;
										return true;
									}
									if (GetStorageContainerWithBoxToMerge(__instance, employee, employeeSQoL)) {
										return true;
									}

									//Clear current reservation so at step 2 it has to find and create a new one.
									employeeSQoL.ClearNPCReservations();
									employee.state = 2;
									return true;
								}
							case 20: {
									EmployeeTryMergeBoxContents(__instance, employee, employeeSQoL, 19);
									return true;
								}
							case 33:
								if (employee.closestCardboardBaler) {
									employee.closestCardboardBaler.GetComponent<CardboardBaler>().AuxiliarAddBoxToBaler(employee.boxProductID);
									UnequipBox(employee);
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									employee.state = -1;
								}
								return true;
						}
						employee.state = 0;
						return true;
                    /**************************************/
                    /************   SECURITY   ************/
                    /**************************************/
                    case 4:
						switch (state) {
							case 0: {
									if (employee.equippedItem > 0) {
										__instance.DropBoxOnGround(employee);
										UnequipBox(employee);
										return true;
									}
									//Substitutes GetThiefTarget
									if (GetThiefTarget(__instance)) {
										//The logic is all inside GetThiefTarget until I rework the thief logic.
										return true;
									}
									if (__instance.IsFirstSecurityEmployee(employeeObj) ||
											__instance.customersnpcParentOBJ.transform.childCount == 0
												&& GameData.Instance.timeOfDay > 22f) {

										GameObject closestDropProduct = __instance.GetClosestDropProduct(employeeObj);
										if (closestDropProduct) {
											employee.droppedProductOBJ = closestDropProduct;
											employee.state = 4;
											employeeSQoL.MoveEmployeeTo(closestDropProduct, null);
											return true;
										}
									}

									employee.state = 1;
									return true;
								}
							case 1: {
									int patrolEmployeeIndex = __instance.RetrieveCorrectPatrolPoint(employeeObj);
									Transform transform;
									if (patrolEmployeeIndex < __instance.patrolPositionOBJ.transform.childCount) {
										transform = __instance.patrolPositionOBJ.transform.GetChild(patrolEmployeeIndex);
									} else {
										transform = __instance.patrolPositionOBJ.transform.GetChild(0);
									}
									if (Vector3.Distance(employee.transform.position, transform.position) > 3f) {
										employeeSQoL.MoveSecurityAndScout(transform.position);
										return true;
									}
									employee.StartWaitState(1f, 0);
									employee.state = -1;
									return true;
								}
							case 2:
								break;
							case 3:
								if (employee.currentChasedThiefOBJ && employee.currentChasedThiefOBJ.GetComponent<NPC_Info>().productsIDCarrying.Count > 0) {
									employee.currentChasedThiefOBJ.GetComponent<NPC_Info>().AuxiliarAnimationPlay(0);
									employee.RpcEmployeeHitThief();
									float timeToWait = Mathf.Clamp(1.45f - employee.securityLevel * 0.01f, 0.5f, 2f);
									employee.StartWaitState(timeToWait, 2);
									employee.state = -1;
									employee.securityExperience += 5 * employee.securityValue;
									return true;
								}
								employee.StartWaitState(0.5f, 0);
								employee.state = -1;
								return true;
							case 4:
								if (employee.droppedProductOBJ) {

									EnumSecurityPickUp pickUpmode = ModConfig.Instance.ImprovedSecurityPickUpMode.Value;
									if (pickUpmode != EnumSecurityPickUp.Disabled && newProdPickUpWorking) {
										if (DoSecurityAreaPickUp(__instance, pickUpmode, employeeObj, employee, stoppingDistance)) {
											employee.StartWaitState(0.5f, 0);
											employee.state = -1;
											return true;
										}
									}

									//TODO 1 - Only if the object is not in the deletion list
									//Also acts as a backup in case the OverlapSphere failed.
									employee.droppedProductOBJ.GetComponent<StolenProductSpawn>().RecoverStolenProductFromEmployee();

									employee.StartWaitState(0.5f, 0);
									employee.state = -1;
									employee.securityExperience++;
									return true;
								}
								employee.state = 0;
								return true;
							default:
								employee.state = 0;
								return true;
						}
						return true;
                    /**************************************/
                    /***********   TECHNICIAN   ***********/
                    /**************************************/
                    case 5: //(repairs + cardboard bale recycling)
						switch (state) {
							case 0: {
									if (employee.equippedItem > 0) {
										__instance.DropBoxOnGround(employee);
										UnequipBox(employee);
										return true;
									}
									GameObject furnitureToFix = __instance.GetFurnitureToFix(employeeObj);
									if (furnitureToFix) {
										employee.currentFurnitureToFix = furnitureToFix;
										employeeSQoL.MoveEmployeeTo(furnitureToFix.transform.Find("Standspot").position, furnitureToFix);
										employee.state = 1;
									} else {
										employee.state = 10;
									}
									return true;
								}
							case 10: {
									if (!TryMoveToClosestBale(__instance, employee, employeeObj, employeeSQoL)) {
										if (Vector3.Distance(employee.transform.position, __instance.restSpotOBJ.transform.position) > 6.5f) {
											LOG.TEMPDEBUG_FUNC(() => $"Technician #{GetUniqueId(employee)}: Moving to rest spot.", LogEmployeeActions);
											employeeSQoL.MoveEmployeeToRestPosition();
										} else {
											employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
											employee.state = -1;
										}
									}
									return true;
								}
							case 1:
								if (employee.currentFurnitureToFix && __instance.RetrieveFurnitureRepairState(employee.currentFurnitureToFix)) {
									float repairTime = 8f - employee.technicianLevel * 0.1f;
									repairTime = Mathf.Clamp(repairTime, 2f, 8f);
									employee.StartWaitState(repairTime, 2);
									employee.state = -1;
									return true;
								}
								return false;
							case 2:
								if (employee.currentFurnitureToFix && __instance.RetrieveFurnitureRepairState(employee.currentFurnitureToFix)) {
									if (employee.currentFurnitureToFix.GetComponent<Data_Container>()) {
										employee.currentFurnitureToFix.GetComponent<Data_Container>().CmdFixBreakingEvent();
									} else if (employee.currentFurnitureToFix.GetComponent<CardboardBaler>()) {
										employee.currentFurnitureToFix.GetComponent<CardboardBaler>().CmdFixBreakingEvent();
									}
									employee.technicianExperience += employee.technicianValue * 10;
								}
								employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								employee.state = -1;
								return true;
							case 3:
								UnequipBox(employee);
								return true;
							case 30:
								if (employee.currentCardboardBale) {
									Vector3 val2 = new(employee.currentCardboardBale.transform.position.x, 0f, employee.currentCardboardBale.transform.position.z);
									Vector3 val3 = new(employee.transform.position.x, 0f, employee.transform.position.z);
									if (Vector3.Distance(val2, val3) < 2f) {
										employee.EquipNPCItem(2);
										GameData.Instance.GetComponent<NetworkSpawner>().EmployeeDestroyBox(employee.currentCardboardBale);
										employee.state = 31;
										return true;
									}
								}
								employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
								employee.state = -1;
								return false;
							case 31:
								MoveToGarbageContainer(__instance, employeeObj, employee,
									employeeSQoL, trashTargetState: 3, recycleTargetState: 32);
								return true;
							case 32: {
									float fundsToAdd = 18f * GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(16, 1);
                                    StatisticsManager.Instance.totalBalesRecycled++;
                                    GameData.Instance.CmdAlterFunds(fundsToAdd);
									employee.technicianExperience += employee.technicianValue;
									employee.state = 3;
									return true;
								}
							default:
								employee.state = 0;
								return true;
						}
                    /***************************************/
                    /**********   ONLINE ORDERS   **********/
                    /***************************************/
                    case 6:
						switch (state) {
							case 0:
								if (employee.equippedItem > 0) {
									__instance.DropBoxOnGround(employee);
									UnequipBox(employee);
									return true;
								}
								if (GameData.Instance.GetComponent<UpgradesManager>().addonsBought[0] &&
										OrderPackaging.Instance.isOrderDepartmentActivated &&
										__instance.RetrieveAnOrderPickupPoint(checkIfFull: false)) {

									int num7 = __instance.RetrievePackagingFreeOrderIndex();
									if (num7 >= 0) {
										employee.packagingAssignedOrderIndex = num7;
										employeeSQoL.MoveEmployeeTo(__instance.AttemptToGetOrderingDepartmentPosition(), __instance.orderingDepartmentSpotOBJ);
										employee.state = 1;
										return true;
									}
								}
								employee.state = 10;
								return true;
							case 10:
								if (Vector3.Distance(employee.transform.position, __instance.restSpotOBJ.transform.position) > 6.5f) {
									employeeSQoL.MoveEmployeeToRestPosition();
									return true;
								}
								employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								employee.state = -1;
								return true;
							case 1:
								if (OrderPackaging.Instance.ordersData[employee.packagingAssignedOrderIndex] != "") {
									employee.packagingAssignedOrderData = OrderPackaging.Instance.ordersData[employee.packagingAssignedOrderIndex];
									OrderPackaging.Instance.RemoveOrderFromEmployee(employee.packagingAssignedOrderIndex);
									string[] array2 = employee.packagingAssignedOrderData.Split('|');
									if (array2[3] == "") {
										employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
										employee.state = -1;
                                        return true;
									}
									string[] array3 = array2[3].Split('_');
									for (int l = 0; l < array3.Length; l++) {
										employee.packagingAssignedOrderProducts.Add(int.Parse(array3[l]));
									}
									int num5 = employee.orderingLevel / 2;
									num5 = Mathf.Clamp(num5, 1, 25);
									for (int m = 0; m < num5; m++) {
										int item2 = ProductListing.Instance.availableProducts[UnityEngine.Random.Range(0, ProductListing.Instance.availableProducts.Count)];
										employee.packagingAssignedOrderProducts.Add(item2);
									}
									if (employee.packagingPackedOrderProducts.Count > 0) {
										employee.packagingPackedOrderProducts.Clear();
									}
									employee.EquipNPCItem(3);
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 2);
									employee.state = -1;
									return true;
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
									employee.state = -1;
									return false;
								}
							case 2:
								if (employee.packagingAssignedOrderProducts.Count > 0) {
									int iDProduct = employee.packagingAssignedOrderProducts[0];
									GenericShelfSlotInfo shelfSlotInfo = ContainerSearch.GetFirstOfAnyShelfWithProduct(__instance, iDProduct);
									//int[] storageShelfWithProduct = __instance.GetStorageShelfWithProduct(iDProduct);
									if (shelfSlotInfo.ShelfFound) {
										GameObject targetParentObj = shelfSlotInfo.ShelfType == ShelfType.ProdShelfSlot ?
											__instance.shelvesOBJ : __instance.storageOBJ;
										employee.orderProductLocationInfoArray = [(int)shelfSlotInfo.ShelfType, shelfSlotInfo.ShelfIndex, shelfSlotInfo.SlotIndex * 2];
										Transform targetShelfTransform = targetParentObj.transform.GetChild(shelfSlotInfo.SlotIndex);
										Vector3 destinationPos = targetShelfTransform.transform.Find("Standspot").position;
										//Not going to reserve shelves for these NPCs since they usually only need a single
										//	one of a product, and I dont feel they deserve to hoard a slot for this job.
										//	They wont get products from already reserved shelves either.
										employeeSQoL.MoveEmployeeTo(destinationPos, targetShelfTransform.gameObject);
										employee.state = 3;
									} else {
										employee.packagingAssignedOrderProducts.RemoveAt(0);
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 2);
										employee.state = -1;
									}
								} else if (employee.packagingPackedOrderProducts.Count > 0) {
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 4);
									employee.state = -1;
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
									employee.state = -1;
								}
								return false;
							case 3: {
									GameObject val4 = employee.orderProductLocationInfoArray[0] != 0 ? __instance.shelvesOBJ : __instance.storageOBJ;
									Data_Container employeeDC = val4.transform.GetChild(employee.orderProductLocationInfoArray[1]).GetComponent<Data_Container>();
									int[] productInfoArray = employeeDC.productInfoArray;
									int num6 = productInfoArray[employee.orderProductLocationInfoArray[2] + 1];
									if (employee.packagingAssignedOrderProducts[0] == productInfoArray[employee.orderProductLocationInfoArray[2]] && num6 > 0) {
										if (employee.orderProductLocationInfoArray[0] == 0) {
											if (num6 == 1) {
												if (employeeDC.transform.Find("CanvasSigns")) {
													employeeDC.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], employee.packagingAssignedOrderProducts[0], -1);
												} else {
													employeeDC.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], -1, -1);
												}
												GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(employeeObj.transform.position, employee.packagingAssignedOrderProducts[0], 0);
											}
											else {
												employeeDC.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], employee.packagingAssignedOrderProducts[0], num6 - 1);
											}
										}
										else {
											employeeDC.NPCGetsItemFromRow(employee.packagingAssignedOrderProducts[0]);
										}
										employee.packagingPackedOrderProducts.Add(employee.packagingAssignedOrderProducts[0]);
										employee.packagingAssignedOrderProducts.RemoveAt(0);
									}
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 2);
									employee.state = -1;
									return true;
								}
							case 4: {
									GameObject pickupPoint = __instance.RetrieveAnOrderPickupPoint(checkIfFull: true);
									if (pickupPoint) {
										employeeSQoL.MoveEmployeeTo(pickupPoint.transform.Find("Standspot").position, pickupPoint);
										employee.state = 5;
									} else {
										employee.RPCNotificationAboveHead("emplomsgnopckp", "");
										employee.StartWaitState(8f, 4);
										employee.state = -1;
									}
									return true;
								}
							case 5: {
									GameObject val2 = __instance.RetrieveAnOrderPickupPoint(checkIfFull: true);
									if (Vector3.Distance(val2.transform.position, employeeObj.transform.position) < 4f) {
										StringBuilder stringBuilder = new StringBuilder();
										for (int j = 0; j < employee.packagingPackedOrderProducts.Count; j++) {
											stringBuilder.Append(employee.packagingPackedOrderProducts[j].ToString());
											if (j != employee.packagingPackedOrderProducts.Count - 1) {
												stringBuilder.Append('_');
											}
										}
										string[] array = employee.packagingAssignedOrderData.Split('|');
										string item = array[0] + "|" + array[1] + "|" + array[2] + "|" + stringBuilder.ToString();
										__instance.NPCsOrdersList.Add(item);
										string boxData = array[0] + "|" + stringBuilder.ToString();
										string[] pickupsData = val2.GetComponent<OrderPickupPoint>().pickupsData;
										for (int k = 0; k < pickupsData.Length; k++) {
											if (pickupsData[k] == "") {
												val2.GetComponent<OrderPickupPoint>().AddOrderBox(k, boxData);
												break;
											}
										}
										AchievementsManager.Instance.CmdAddAchievementPoint(19, 1);
                                        StatisticsManager.Instance.onlineOrdersMade++;
                                        employee.orderingExperience += employee.orderingValue * 25;
										UnequipBox(employee);
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
										employee.state = -1;
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 4);
										employee.state = -1;
									}
									return true;
								}
							case 6:
							case 7:
							case 8:
							case 9:
								return true;
						}
						return true;
                    /***************************************/
                    /**********   MANUFACTURING   **********/
                    /***************************************/
                    case 7:
						switch (state) {
							case 0: {
                                    employeeSQoL.ClearNPCReservations();

                                    if (employee.equippedItem > 0) {
										__instance.DropBoxOnGround(employee);
										UnequipBox(employee);
                                        return true;
									}
									int num5 = __instance.GetPriorityIndex(employeeObj, 7) % 3;
									bool flag = false;
									bool flag2 = false;
									employee.thiefProductsNumber = -1;
									if (GameData.Instance.GetComponent<UpgradesManager>().addonsBought[1]) {
										int[] array3 = num5 switch {
											1 => [1, 2, 0],
											2 => [2, 0, 1],
											_ => [0, 1, 2],
										};
										for (int l = 0; l < array3.Length; l++) {
											switch (array3[l]) {
												case 1:
													if (__instance.GetFreeManufacturingStorageContainer(10000, "999") < 0) {
														GameObject randomGroundManufBoxForStorage = __instance.GetRandomGroundManufacturingBoxAllowedInStorage(employeeObj);
														if (randomGroundManufBoxForStorage) {
															employee.thiefProductsNumber = 1;
															employee.randomBox = randomGroundManufBoxForStorage;
															employeeSQoL.MoveEmployeeTo(randomGroundManufBoxForStorage, null);
                                                            flag = true;
															employee.state = 21;
														} else {
															employee.state = 1;
														}
													} else {
														GameObject randomGroundManufacturingBox = __instance.GetRandomGroundManufacturingBox(employeeObj);
														if ((bool)randomGroundManufacturingBox && NavMesh.SamplePosition(new Vector3(randomGroundManufacturingBox.transform.position.x, 0f, randomGroundManufacturingBox.transform.position.z), out var hit, 1f, -1)) {
															employee.thiefProductsNumber = 1;
															employee.randomBox = randomGroundManufacturingBox;
                                                            employeeSQoL.MoveEmployeeTo(hit.position, null);
															flag = true;
															employee.state = 21;
														}
													}
                                                    break;
												case 2:
													if (__instance.mainManufacturingUpdateIsBeingCalculated) {
														flag2 = true;
                                                        break;
													}
													employee.productAvailableArray = __instance.ReturnWeightedManufacturerTask(employeeObj, employee);
													if (employee.productAvailableArray[0] != -1) {
														employee.thiefProductsNumber = 2;
														Vector3 targetPos = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(employee.productAvailableArray[2]).transform.Find("Standspot").transform.position;
                                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                                        flag = true;
														employee.state = 42;
													}
													break;
                                                default: {
                                                        int manufacturingProducerWithQueue = __instance.GetManufacturingProducerWithQueue(num5);
                                                        if (manufacturingProducerWithQueue >= 0) {
                                                            employee.thiefProductsNumber = 0;
                                                            __instance.manufacturingProducersList[manufacturingProducerWithQueue].GetComponent<ManufacturingProduction>().EmployeeAssignCoroutineCall(employee);
                                                            employee.packagingAssignedOrderIndex = manufacturingProducerWithQueue;
                                                            flag = true;
                                                            employee.state = 2;
                                                        }
                                                        break;
                                                    }
                                            }
											if (flag || flag2) {
                                                break;
											}
										}
									}
									if (flag2) {
										employee.StartWaitState(0.2f, 0);
										employee.state = -1;
									} else if (!flag) {
										employee.state = 1;
									}
                                    return true;
								}
							case 1:
								if (Vector3.Distance(employee.transform.position, __instance.manufacturingRestSpotOBJ.transform.position) > 6.5f) {
									Vector3 vector = __instance.manufacturingRestSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0f, UnityEngine.Random.Range(-4f, 4f));
									if (NavMesh.SamplePosition(vector, out var hit2, 1f, -1) && hit2.distance > 0.02f) {
										vector = hit2.position;
									}
                                    employeeSQoL.MoveEmployeeTo(vector, null);
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
									employee.state = -1;
								}
                                return true;
							case 2:
								if (__instance.manufacturingProducersList[employee.packagingAssignedOrderIndex]) {
									ManufacturingProduction component5 = __instance.manufacturingProducersList[employee.packagingAssignedOrderIndex].GetComponent<ManufacturingProduction>();
									if (component5.productQueue.Count > 0) {
										string text = ManufacturingBase.Instance.RetrieveBaseRecipe(component5.productQueue[0]);
										string text2 = component5.combinableQueue[0];
										employee.manufacturedEmployeeProductsList.Clear();
										string[] array = text.Split('|');
										for (int i = 0; i < array.Length; i++) {
											string[] array2 = array[i].Split('-');
											for (int j = 0; j < array2.Length; j++) {
                                                GenericShelfSlotInfo shelfSlotInfo = ContainerSearch.GetFirstOfAnyShelfWithProduct(__instance, int.Parse(array2[j]));
                                                if (shelfSlotInfo.ShelfFound) {
													employee.manufacturedEmployeeProductsList.Add(array2[j]);
													break;
												}
											}
										}
										if (text2 != "" && text2 != "999") {
											array = text2.Split('-');
											for (int k = 0; k < array.Length; k++) {
												employee.manufacturedEmployeeProductsList.Add(array[k]);
											}
										}
										employee.EquipNPCItem(5);
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
										employee.state = -1;
                                        return true;
									}
								}
								employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								employee.state = -1;
                                return true;
							case 3:
								if (employee.manufacturedEmployeeProductsList.Count > 0) {
									string[] array4 = employee.manufacturedEmployeeProductsList[0].Split('|');
									bool flag3 = false;
									GameObject gameObject2 = null;
									int index = 0;
									for (int m = 0; m < array4.Length; m++) {
										int[] storageShelfWithProduct = __instance.GetStorageShelfWithProduct(int.Parse(array4[m]));
										if (storageShelfWithProduct[0] > -1) {
											employee.orderProductLocationInfoArray = storageShelfWithProduct;
											gameObject2 = ((storageShelfWithProduct[0] != 0) ? __instance.shelvesOBJ : __instance.storageOBJ);
											flag3 = true;
											index = storageShelfWithProduct[1];
											break;
										}
									}
									if (flag3) {
										Vector3 targetPos = gameObject2.transform.GetChild(index).transform.Find("Standspot").position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 4;
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
										employee.state = -1;
									}
								} else if (__instance.manufacturingProducersList[employee.packagingAssignedOrderIndex]) {
									Vector3 targetPos = __instance.manufacturingProducersList[employee.packagingAssignedOrderIndex].transform.Find("Standspot").position;
                                    employeeSQoL.MoveEmployeeTo(targetPos, null);
                                    employee.state = 5;
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									employee.state = -1;
								}
                                return true;
							case 4: {
									Data_Container component6 = ((employee.orderProductLocationInfoArray[0] != 0) ? __instance.shelvesOBJ : __instance.storageOBJ).transform.GetChild(employee.orderProductLocationInfoArray[1]).GetComponent<Data_Container>();
									int[] productInfoArray = component6.productInfoArray;
									int num6 = productInfoArray[employee.orderProductLocationInfoArray[2] + 1];
									int num7 = int.Parse(employee.manufacturedEmployeeProductsList[0]);
									if (num7 == productInfoArray[employee.orderProductLocationInfoArray[2]] && num6 > 0) {
										if (employee.orderProductLocationInfoArray[0] == 0) {
											if (num6 == 1) {
												if ((bool)component6.transform.Find("CanvasSigns")) {
													component6.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], num7, -1);
												} else {
													component6.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], -1, -1);
												}
												GameData.Instance.GetComponent<ManagerBlackboard>().SpawnBoxFromEmployee(employeeObj.transform.position, num7, 0);
											} else {
												component6.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], num7, num6 - 1);
											}
										} else {
											component6.NPCGetsItemFromRow(num7);
										}
										employee.manufacturedEmployeeProductsList.RemoveAt(0);
									}
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 3);
									employee.state = -1;
                                    return true;
								}
							case 5:
								if (__instance.manufacturingProducersList[employee.packagingAssignedOrderIndex]) {
									ManufacturingProduction component7 = __instance.manufacturingProducersList[employee.packagingAssignedOrderIndex].GetComponent<ManufacturingProduction>();
									if (component7.productQueue.Count > 0) {
										employee.manufacturingExperience += employee.manufacturingValue * 3;
										component7.ProduceFromEmployee();
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
										employee.state = -1;
                                        return true;
									}
								}
								employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
								employee.state = -1;
                                return true;
							case 21:
								if ((bool)employee.randomBox && Vector3.Distance(new Vector3(employee.randomBox.transform.position.x, 0f, employee.randomBox.transform.position.z), new Vector3(employee.transform.position.x, 0f, employee.transform.position.z)) < 2f) {
									ManufacturingBoxData component3 = employee.randomBox.GetComponent<ManufacturingBoxData>();
									employee.NetworkboxProductID = component3.manufacturedProductIndex;
									employee.NetworkboxNumberOfProducts = component3.numberOfProducts;
									employee.mBoxCombinableData = component3.combinablesData;
									employee.EquipNPCItem(4);
									GameData.Instance.GetComponent<NetworkSpawner>().EmployeeDestroyBox(employee.randomBox);
									if (component3.numberOfProducts > 0) {
										employee.state = 31;
									} else {
										employee.state = 26;
									}
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									employee.state = -1;
								}
                                return true;
							case 22: {
									int freeManufacturingStorageContainer4 = __instance.GetFreeManufacturingStorageContainer(employee.boxProductID, employee.mBoxCombinableData);
									if (freeManufacturingStorageContainer4 >= 0) {
										employee.currentFreeStorageIndex = freeManufacturingStorageContainer4;
										employee.currentFreeStorageOBJ = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(freeManufacturingStorageContainer4).gameObject;
										Vector3 targetPos = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(freeManufacturingStorageContainer4).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 23;
									} else {
                                        Vector3 targetPos = __instance.leftoverBoxesSpotOBJ.transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 24;
									}
                                    return true;
								}
							case 23: {
									int freeManufacturingStorageContainer = __instance.GetFreeManufacturingStorageContainer(employee.boxProductID, employee.mBoxCombinableData);
									if (freeManufacturingStorageContainer >= 0 && employee.currentFreeStorageIndex == freeManufacturingStorageContainer && (bool)employee.currentFreeStorageOBJ) {
										int freeManufacturingStorageRow = __instance.GetFreeManufacturingStorageRow(freeManufacturingStorageContainer, employee.boxProductID, employee.mBoxCombinableData);
										if (freeManufacturingStorageRow >= 0) {
                                            __instance.manufacturingStorageShelvesOBJ.transform.GetChild(freeManufacturingStorageContainer).GetComponent<ManufacturingContainer>().EmployeeUpdateArrayValuesStorage(freeManufacturingStorageRow * 2, employee.boxProductID, employee.boxNumberOfProducts, employee.mBoxCombinableData);
											employee.state = 25;
											employee.manufacturingExperience += employee.manufacturingValue;
										} else {
											employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 22);
											employee.state = -1;
										}
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 22);
										employee.state = -1;
									}
                                    return true;
								}
							case 24: {
									Vector3 spawnpoint2 = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 3f, UnityEngine.Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnManufacturingBoxFromEmployee(spawnpoint2, employee.boxProductID, employee.boxNumberOfProducts, employee.mBoxCombinableData);
									employee.state = 25;
                                    return true;
								}
							case 25:
								UnequipBox(employee);
                                return true;
							case 26: {
                                    MoveToBalerOrGarbageContainer(__instance, employeeObj, employee, employeeSQoL,
                                            cardboardTargetState: 34, trashTargetState: 25, recycleTargetState: 27);

									return true;
								}
							case 27: {
									float fundsToAdd = 1.5f * (float)GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
									StatisticsManager.Instance.totalBoxesRecycled++;
									GameData.Instance.CmdAlterFunds(fundsToAdd);
									employee.state = 25;
                                    return true;
								}
							case 31:
								if (__instance.MoreThanOneManufacturingBoxToMergeCheck(employee.boxProductID, employee.mBoxCombinableData)) {
									employee.state = 32;
								} else {
									employee.state = 22;
								}
                                return true;
							case 32: {
									if (employee.boxNumberOfProducts <= 0) {
										employee.state = 26;
                                        return true;
									}
									int storageContainerWithManufacturingBoxToMerge =__instance. GetStorageContainerWithManufacturingBoxToMerge(employee.boxProductID, employee.mBoxCombinableData);
									if (storageContainerWithManufacturingBoxToMerge >= 0) {
										employee.currentFreeStorageIndex = storageContainerWithManufacturingBoxToMerge;
										Vector3 targetPos = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(storageContainerWithManufacturingBoxToMerge).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 33;
									} else {
										employee.state = 22;
									}
                                    return true;
								}
							case 33: {
									int storageRowWithManufacturingBoxToMerge2 = __instance.GetStorageRowWithManufacturingBoxToMerge(employee.currentFreeStorageIndex, employee.boxProductID, employee.mBoxCombinableData);
									if (storageRowWithManufacturingBoxToMerge2 >= 0) {
                                        __instance.EmployeeMergeManufacturingBoxContents(employee, employee.currentFreeStorageIndex, employee.boxProductID, storageRowWithManufacturingBoxToMerge2);
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 32);
										employee.state = -1;
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 32);
										employee.state = -1;
									}
                                    return true;
								}
							case 34:
								if ((bool)employee.closestCardboardBaler) {
									employee.closestCardboardBaler.GetComponent<CardboardBaler>().AuxiliarAddBoxToBaler(employee.boxProductID);
									UnequipBox(employee);
								}
                                return true;
							case 42: {
									ManufacturingContainer component8 = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(employee.productAvailableArray[2]).GetComponent<ManufacturingContainer>();
									int[] productInfoArray2 = component8.productInfoArray;
									int num8 = productInfoArray2[employee.productAvailableArray[3]];
									string text3 = component8.combinableInfoArray[employee.productAvailableArray[3] / 2];
									if (num8 == employee.productAvailableArray[5] && text3 == employee.packagingAssignedOrderData) {
										int num9 = productInfoArray2[employee.productAvailableArray[3] + 1];
										if (num9 <= 0) {
											employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
											employee.state = -1;
                                            return true;
										}
										if (__instance.manufacturingStorageShelvesOBJ.transform.GetChild(employee.productAvailableArray[2]).transform.Find("CanvasSigns")) {
											component8.EmployeeUpdateArrayValuesStorage(employee.productAvailableArray[3], num8, -1, text3);
										} else {
											component8.EmployeeUpdateArrayValuesStorage(employee.productAvailableArray[3], -1, -1, "");
										}
										employee.NetworkboxProductID = num8;
										employee.NetworkboxNumberOfProducts = num9;
										employee.mBoxCombinableData = text3;
										employee.EquipNPCItem(4);
										Vector3 targetPos = __instance.manufacturingShelvesOBJ.transform.GetChild(employee.productAvailableArray[0]).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 43;
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeIdleWait.Value, 0);
										employee.state = -1;
									}
                                    return true;
								}
							case 43:
								if (__instance.manufacturingShelvesOBJ.transform.GetChild(employee.productAvailableArray[0]).GetComponent<ManufacturingContainer>().productInfoArray[employee.productAvailableArray[1]] == employee.productAvailableArray[4]) {
									employee.state = 44;
								} else {
									employee.state = 45;
								}
                                return true;
							case 44: {
									ManufacturingContainer component4 = __instance.manufacturingShelvesOBJ.transform.GetChild(employee.productAvailableArray[0]).GetComponent<ManufacturingContainer>();
									int num = component4.productInfoArray[employee.productAvailableArray[1] + 1];
									int maxManufacturingProductsPerRow = __instance.GetMaxManufacturingProductsPerRow(employee.productAvailableArray[0], employee.productAvailableArray[4]);
									if (employee.boxNumberOfProducts > 0 && num < maxManufacturingProductsPerRow) {
										int num2 = Mathf.Clamp(maxManufacturingProductsPerRow - num, 1, employee.restockerLevel);
										int num3 = Mathf.Clamp(employee.boxNumberOfProducts, 1, employee.restockerLevel);
										int num4 = num3;
										if (num2 < num3) {
											num4 = num2;
										}
										component4.EmployeeAddsItemToRow(employee.productAvailableArray[1], num4);
										employee.NetworkboxNumberOfProducts = employee.boxNumberOfProducts - num4;
										employee.StartWaitState(__instance.employeeItemPlaceWait, 44);
										employee.state = -1;
										employee.manufacturingExperience += employee.manufacturingValue;
									} else if (employee.boxNumberOfProducts > 0 && __instance.CheckIfManufacturingShelfWithSameProduct(employee.productAvailableArray[4], employee, employee.productAvailableArray[0], employee.packagingAssignedOrderData)) {
										Vector3 targetPos = __instance.manufacturingShelvesOBJ.transform.GetChild(employee.productAvailableArray[0]).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 43;
									} else {
										employee.state = 45;
									}
                                    return true;
								}
							case 45: {
									if (employee.boxNumberOfProducts <= 0) {
                                        MoveToBalerOrGarbageContainer(__instance, employeeObj, employee, employeeSQoL,
                                            cardboardTargetState: 60, trashTargetState: 46, recycleTargetState: 49);

                                        return true;
									}
									int storageContainerWithManufacturingBoxToMerge3 = __instance.GetStorageContainerWithManufacturingBoxToMerge(employee.boxProductID, employee.mBoxCombinableData);
									if (storageContainerWithManufacturingBoxToMerge3 >= 0) {
										employee.currentFreeStorageIndex = storageContainerWithManufacturingBoxToMerge3;
										Vector3 targetPos = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(storageContainerWithManufacturingBoxToMerge3).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 50;
                                        return true;
									}
									int freeManufacturingStorageContainer3 = __instance.GetFreeManufacturingStorageContainer(employee.boxProductID, employee.mBoxCombinableData);
									if (freeManufacturingStorageContainer3 >= 0) {
										Vector3 targetPos = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(freeManufacturingStorageContainer3).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 47;
									} else {
                                        Vector3 targetPos = __instance.leftoverBoxesSpotOBJ.transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 48;
									}
                                    return true;
								}
							case 46:
								UnequipBox(employee);
                                return true;
							case 47: {
									int storageContainerWithManufacturingBoxToMerge2 = __instance.GetStorageContainerWithManufacturingBoxToMerge(employee.boxProductID, employee.mBoxCombinableData);
									if (storageContainerWithManufacturingBoxToMerge2 >= 0) {
										employee.currentFreeStorageIndex = storageContainerWithManufacturingBoxToMerge2;
										Vector3 targetPos = __instance.manufacturingStorageShelvesOBJ.transform.GetChild(storageContainerWithManufacturingBoxToMerge2).transform.Find("Standspot").transform.position;
                                        employeeSQoL.MoveEmployeeTo(targetPos, null);
                                        employee.state = 50;
                                        return true;
									}
									int freeManufacturingStorageContainer2 = __instance.GetFreeManufacturingStorageContainer(employee.boxProductID, employee.mBoxCombinableData);
									if (freeManufacturingStorageContainer2 >= 0) {
										int freeManufacturingStorageRow2 = __instance.GetFreeManufacturingStorageRow(freeManufacturingStorageContainer2, employee.boxProductID, employee.mBoxCombinableData);
										if (freeManufacturingStorageRow2 >= 0) {
                                            __instance.manufacturingStorageShelvesOBJ.transform.GetChild(freeManufacturingStorageContainer2).GetComponent<ManufacturingContainer>().EmployeeUpdateArrayValuesStorage(freeManufacturingStorageRow2 * 2, employee.boxProductID, employee.boxNumberOfProducts, employee.mBoxCombinableData);
											employee.state = 46;
										} else {
											employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 45);
											employee.state = -1;
										}
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 45);
										employee.state = -1;
									}
                                    return true;
								}
							case 48: {
									Vector3 spawnpoint = __instance.leftoverBoxesSpotOBJ.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 4f, UnityEngine.Random.Range(-1f, 1f));
									GameData.Instance.GetComponent<ManagerBlackboard>().SpawnManufacturingBoxFromEmployee(spawnpoint, employee.boxProductID, employee.boxNumberOfProducts, employee.mBoxCombinableData);
									employee.state = 46;
                                    return true;
								}
							case 49: {
									float fundsToAdd2 = 1.5f * (float)GameData.Instance.GetComponent<UpgradesManager>().boxRecycleFactor;
									AchievementsManager.Instance.CmdAddAchievementPoint(2, 1);
									StatisticsManager.Instance.totalBoxesRecycled++;
									GameData.Instance.CmdAlterFunds(fundsToAdd2);
									employee.state = 46;
                                    return true;
								}
							case 50: {
									int storageRowWithManufacturingBoxToMerge = __instance.GetStorageRowWithManufacturingBoxToMerge(employee.currentFreeStorageIndex, employee.boxProductID, employee.mBoxCombinableData);
									if (storageRowWithManufacturingBoxToMerge >= 0) {
                                        __instance.EmployeeMergeManufacturingBoxContents(employee, employee.currentFreeStorageIndex, employee.boxProductID, storageRowWithManufacturingBoxToMerge);
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 45);
										employee.state = -1;
									} else {
										employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 45);
										employee.state = -1;
									}
									break;
								}
							case 60:
								if ((bool)employee.closestCardboardBaler) {
									employee.closestCardboardBaler.GetComponent<CardboardBaler>().AuxiliarAddBoxToBaler(employee.boxProductID);
									UnequipBox(employee);
								} else {
									employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
									employee.state = -1;
								}

								return true;
						}
                        return true;
                    default:
						UnityEngine.Debug.Log("Impossible employee current task case. Check logs.");
						return true;
				}
			} else if (EmployeeWalkSpeedPatch.IsWarpingEnabled() && !employeeNavAgent.pathPending) {

				//See NPC_Movement.LastDestinationSet for an explanation on this
				employeeNavAgent.Warp(employeeSQoL.LastDestinationSet);

				EmployeeWarpSound.PlayEmployeeWarpSound(employee);

				employee.StartWaitState(0.5f, state);
				employee.state = -1;
				return true;
			}
			return false;
		}


        private static List<EmployeeJob> taskPriorities;

		private static int currentTaskIndex;

		private static void TryNonPrimaryJob(NPC_Info employee) {
			//TODO 1 - So the question is: Do I call TryNonPrimaryJob manually after the 
			//	current job step leads to being idle? Or do I call it at the end and check here
			//	if the job is an idle one? This last one is error prone, but the first one might
			//	be too if I forget or a new idle state is added or changes number, but thats less
			//	common.
			//	Ideally, in EmployeeNPCControl, I would return an enum instead of a bool, stating
			//	what the result of the thingy is, and then in EmployeeWaitTimeAndJobControl I
			//	check if I need to call EmployeeNPCControl or what.
			//	The question is, should I retry the secondary job on the same call? or set the new job
			//	for the next one?
			//	I dont think I can overall make this in a decent way without having some performance impact.
			//TODO 1 - taskPriorities and currentTaskIndex will be fields specific for each npc.
			if (taskPriorities[currentTaskIndex + 1] != EmployeeJob.Unassigned) {
				currentTaskIndex++;

				employee.taskPriority = currentTaskIndex;
			}
		}

		private static bool DoSecurityAreaPickUp(NPC_Manager __instance, EnumSecurityPickUp pickUpmode, 
				GameObject employeeObj, NPC_Info employee, float stoppingDistance) {
			//Pick up multiple products in an area, based on the npc security level.
			int securityLevel = pickUpmode switch {
				EnumSecurityPickUp.Reduced => (int)Math.Ceiling(employee.securityLevel.ClampReturn(0, MaxSecurityPickUpLevel) / 2d),
				EnumSecurityPickUp.Normal => employee.securityLevel.ClampReturn(0, MaxSecurityPickUpLevel),
				EnumSecurityPickUp.AlwaysMaxed => MaxSecurityPickUpLevel,
				_ => throw new NotImplementedException($"Unknown security pick up mode: {pickUpmode}"),
			};

			int pickUpCount = (int)Math.Ceiling(securityLevel / LevelsForExtraPickUp);
			float pickUpRangeFromLevel = securityLevel / (float)MaxSecurityPickUpLevel * SecurityPickUpRangeLevelMult;
			//The base range is the current minimum distance to stop in front of the product, plus a small buffer.
			float pickUpRange = 0.1f + stoppingDistance + (float)Math.Round(pickUpRangeFromLevel, 2);

			Collider[] possiblePickups = new Collider[pickUpCount];
			if (Physics.OverlapSphereNonAlloc(employeeObj.transform.position,
					pickUpRange, possiblePickups, 1 << SecurityPickUpLayer) > 0) {

				float totalPickUpValue = 0f;
				foreach (Collider col in possiblePickups) {
					if (col == null) {
						//No more found.
						break;
					}

					GameObject stolenProdObj = col.gameObject;
					StolenProductSpawn stoleProdSpawn = stolenProdObj.GetComponent<StolenProductSpawn>();
					if (stoleProdSpawn && stolenProdObj.activeSelf) {

						totalPickUpValue += stoleProdSpawn.productCarryingPrice;
						

						stolenProdObj.SetActive(false);
						//Remove product from the parent object so it cant be found again by GetClosestDropProduct.
						stoleProdSpawn.transform.parent = null;
						//Remove from layer used in OverlapSphereNonAlloc so it cant be found by other security employees.
						stolenProdObj.layer = 1 << 0;

						//TODO 2 - Improve this so if I detect that the client has SuperQol mod, it calls a
						//	new RPC method that deactivates the object on the client, and then it gets
						//	deleted over time.

						//Enqueue to be deleted later over time.
						stolenProdPickups.Enqueue(stoleProdSpawn);



						employee.securityExperience++;
						pickUpCount--;
					} else {
						TimeLogger.Logger.LogDebug($"Security area pick up found a " +
							$"collider in layer {SecurityPickUpLayer} that is not a " +
							$"StolenProductSpawn: {col.gameObject.name}", LogCategories.AI);
					}
					if (pickUpCount <= 0) {
						break;
					}
				}

				if (totalPickUpValue != 0) {
					GameData.Instance.AlterFundsFromEmployee(totalPickUpValue);
				}

				return true;
			}
			return false;
		}

		private IEnumerator PickupMarkedStolenProductsLoop() {
			//Seems like framerate is a factor in products not being deleted for clients, 
			//	probably from struggling already. Thus this logic will be fps based instead of time based,
			//	to execute less Destroy calls per second in low spec systems.
			const float MaxStolenProdRemoval = 5f; //Objects to destroy per frame.


			do {
                if (stolenProdPickups.Count > 0) {
                    destroyCounter = Math.Min(MaxStolenProdRemoval, stolenProdPickups.Count);
                    while (destroyCounter >= 1) {
						if (stolenProdPickups.TryDequeue(out StolenProductSpawn stolenProdPickup)) {
							NetworkServer.UnSpawn(stolenProdPickup.gameObject);

                            //TODO 2 - I made a mistake and had the destroyCounter adding and accumulating 5
							//	stolen products every frame, no matter what.
                            //	Now it will only do so when there is something in stolenProdPickups, and it
							//	wont accumulate. Not sure if this could have been the cause of the bug where
							//	rarely a product would get picked up in the host, but the client still has it.
                            //	Old comment: Add the unspawned to another different list and make yet another 
                            //	coroutine to call the Destroy on the list, but with a much slower rate.
                            //NetworkServer.Destroy(stolenProdPickup.gameObject);
                        }
                        destroyCounter--;
                    }
				} else {
					break;
				}

				yield return null;
			} while (WorldState.CurrentGameWorldState != GameWorldEvent.QuitOrMenu);

			stolenProdPickups.Clear();
		}


		private static bool TryMoveToClosestBale(NPC_Manager __instance, NPC_Info employee, GameObject employeeObj, EmployeeNPC employeeSQoL) {
			GameObject closestBale = __instance.GetClosestBale(employeeObj);
			if (closestBale) {
				if (NavMesh.SamplePosition(new Vector3(closestBale.transform.position.x, 0f, 
						closestBale.transform.position.z), out NavMeshHit navHit, 1f, -1)) {

					employee.currentCardboardBale = closestBale;
					employeeSQoL.MoveEmployeeTo(navHit.position, closestBale);
					employee.state = 30;
					return true;
				}
			}

			return false;
		}

		private static bool GetStorageContainerWithBoxToMerge(NPC_Manager __instance, NPC_Info employee, EmployeeNPC employeeSQoL) {
			StorageSlotInfo storageToMerge = ContainerSearch.GetStorageContainerWithBoxToMerge(__instance, employee.NetworkboxProductID);
			if (storageToMerge.ShelfFound) {
				LOG.TEMPDEBUG_FUNC(() => $"{GetEmployeeTaskName(employee.taskPriority)} " +
					$"#{GetUniqueId(employee)}: Moving to storage to merge box.", LogEmployeeActions);

				//npc.currentFreeStorageIndex = storageToMerge.SlotIndex;
				Vector3 destination = __instance.storageOBJ.transform.GetChild(storageToMerge.ShelfIndex).transform.Find("Standspot").transform.position;
				employeeSQoL.MoveEmployeeToStorage(destination, storageToMerge);
				employee.state = 20;
				return true;
			}
			return false;
		}


		public static void MoveToBalerOrGarbageContainer(NPC_Manager __instance, GameObject employeeObj,
				NPC_Info employee, EmployeeNPC employeeSQoL, int cardboardTargetState, int trashTargetState, int recycleTargetState) {

			MoveToGarbageContainer(__instance, employeeObj, employee, employeeSQoL, 
				cardboardTargetState, trashTargetState, recycleTargetState, useCardboardBaler: true);
		}

		public static void MoveToGarbageContainer(NPC_Manager __instance, GameObject employeeObj,
				NPC_Info employee, EmployeeNPC employeeSQoL, int trashTargetState, int recycleTargetState) {

			MoveToGarbageContainer(__instance, employeeObj, employee, employeeSQoL,
				cardboardTargetState: -1, trashTargetState, recycleTargetState, useCardboardBaler: false);
		}

		private static void MoveToGarbageContainer(NPC_Manager __instance, GameObject employeeObj, 
				NPC_Info employee, EmployeeNPC employeeSQoL, int cardboardTargetState,
				int trashTargetState, int recycleTargetState, bool useCardboardBaler) {

			string employeeTaskName = GetEmployeeTaskName(employee.taskPriority);

			LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} #{GetUniqueId(employee)}: Box empty, trying to recycle.", LogEmployeeActions);
			GameObject closestCardboardBaler = null;
			if (useCardboardBaler) {
				closestCardboardBaler = __instance.GetClosestCardboardBaler(employeeObj);
			}
			
			if (closestCardboardBaler) {
				LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} #{GetUniqueId(employee)}: Moving to cardboard baler.", LogEmployeeActions);
				employee.closestCardboardBaler = closestCardboardBaler;
				employeeSQoL.MoveEmployeeTo(closestCardboardBaler.transform.Find("Standspot"), closestCardboardBaler);
				employee.state = cardboardTargetState;
				return;
			} else if (__instance.closestRecyclePerk) {
				LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} #{GetUniqueId(employee)}: Moving to trash container with recycling perk.", LogEmployeeActions);
				employeeSQoL.MoveEmployeeTo(__instance.trashSpotOBJ, __instance.trashSpotOBJ);
				employee.state = recycleTargetState;
				return;
			} else if (!__instance.employeeRecycleBoxes || __instance.interruptBoxRecycling) {
				LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} #{GetUniqueId(employee)}: Moving to trash container instead.", LogEmployeeActions);
				employeeSQoL.MoveEmployeeTo(__instance.trashSpotOBJ, __instance.trashSpotOBJ);
				employee.state = trashTargetState;
				return;
			}
			LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} #{GetUniqueId(employee)}: Moving to closest non trash recycle spot.", LogEmployeeActions);
			float recycleSpot1Distance = Vector3.Distance(employeeObj.transform.position, __instance.recycleSpot1OBJ.transform.position);
			float recycleSpot2Distance = Vector3.Distance(employeeObj.transform.position, __instance.recycleSpot2OBJ.transform.position);

			GameObject closestRecycleSpot = recycleSpot1Distance < recycleSpot2Distance ?
                __instance.recycleSpot1OBJ : __instance.recycleSpot2OBJ;

			employeeSQoL.MoveEmployeeTo(closestRecycleSpot, closestRecycleSpot);
			employee.state = recycleTargetState;
		}

        //TODO 2 - Security and thief seeking should be its own process independently of everything else.
        //	But for now, I can do it so when a thief is detected, it then finds the closest idle npc
        //	and assigns it, instead of doing all of this for the npc itself.
        //	Its very counterintuitive to how the entire npc method works, but...
        //	Ideally instead of checking thief locations and what not, what I need is to change the
        //	customers method so when its exposed it triggers the closest security to go.
        public static bool GetThiefTarget(NPC_Manager __instance) {
			if (__instance.customersnpcParentOBJ.transform.childCount == 0) {
				return false;
			}

			int[] securityIdleStates = [0, 1];
			GameObject targetThief = null;
			__instance.thievesList.Clear();

			foreach (Transform customer in __instance.customersnpcParentOBJ.transform) {
				NPC_Info component = customer.GetComponent<NPC_Info>();
				if (component.isAThief && component.thiefFleeing && component.productsIDCarrying.Count > 0 && 
						customer.position.z < -3f && customer.position.x > -15f && customer.position.x < 38f) {

					__instance.thievesList.Add(customer.gameObject);
					if (!component.thiefAssignedChaser) {
						component.thiefAssignedChaser = true;
						targetThief = customer.gameObject;
						break;
					}
				}
			}

			//If thieves were found but all are already being chased,
			//	check how to proceed depending on user settings.
			if (targetThief == null && __instance.thievesList.Count > 0) {

				switch (ModConfig.Instance.SecurityThiefChaseMode.Value) {
					case EnumSecurityEmployeeThiefChase.Disabled:
						targetThief = GetRandomThief(__instance);
						break;
					case EnumSecurityEmployeeThiefChase.AllChaseButLastOne:
						if (GetEmployeesAssignedTo(EmployeeJob.Security, securityIdleStates).Any()) {
							targetThief = GetRandomThief(__instance);
						}
						break;
					case EnumSecurityEmployeeThiefChase.OnlyOnePerThief:
						//All thieves already being chased so no need to do anything.
						break;
					default:
						throw new NotImplementedException(
							ModConfig.Instance.SecurityThiefChaseMode.Value.GetDescription());
				};
			}

			if (targetThief) {
				NPC_Info employee = GetClosestEmployeeToTarget(
					EmployeeJob.Security, securityIdleStates, targetThief.transform);

				if (employee) {
					employee.currentChasedThiefOBJ = targetThief;
					employee.state = 2;
					return true;
				}
			}

			return false;
		}

		private static NPC_Info GetClosestEmployeeToTarget(
				EmployeeJob employeeJob, int[] allowedStates, Transform target) {

			if (!NPC_Manager.Instance) {
				TimeLogger.Logger.LogWarning("The NPC_Manager is null. Make sure " +
					"to call this method after NPC_Manager Awake", LogCategories.AI);
			}

			NPC_Info closestEmployee = null;
			float closestDistanceSqr = float.MaxValue;

			GetEmployeesAssignedTo(EmployeeJob.Security, allowedStates)
				.ForEach((employeeT) => {
					NPC_Info employee = employeeT.GetComponent<NPC_Info>();
					float sqrDistance = (employeeT.position - target.position).sqrMagnitude;
					if (sqrDistance < closestDistanceSqr) {
						closestDistanceSqr = sqrDistance;
						closestEmployee = employee;
					}
				});

			return closestEmployee;
		}

		private static GameObject GetRandomThief(NPC_Manager __instance) => 
			__instance.thievesList[UnityEngine.Random.Range(0, __instance.thievesList.Count)];

		/// <summary>
		/// Checks if the current security npc is the only idle one.
		/// </summary>
		/// <remarks>
		/// This method doesnt check if the npc passed by parameter is idle, and just assumes 
		/// that is the case. Useful when the npc is not currently idle, but is going to.
		/// </remarks>
		private static bool IsLastIdleSecurity(NPC_Info employee) {
			return GetEmployeesAssignedTo(EmployeeJob.Security)
				.Where((t) => t != employee.transform)
				.All((t) => {
					NPC_Info employee = t.GetComponent<NPC_Info>();
					return employee.state != 0 && employee.state != 1;
				}
			);
		}

		private static void EmployeeTryMergeBoxContents(NPC_Manager __instance, NPC_Info employee, EmployeeNPC employeeSQoL, int returnState) {
			string employeeTaskName = GetEmployeeTaskName(employee.taskPriority);

			if (employeeSQoL.RefreshAndCheckValidTargetedStorage(__instance, clearReservation: false, out StorageSlotInfo storageSlotInfo)) {
				LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} " +
					$"#{GetUniqueId(employee)}: Merging storage box.", LogEmployeeActions);
				__instance.EmployeeMergeBoxContents(employee, storageSlotInfo.ShelfIndex, 
					storageSlotInfo.ExtraData.ProductId, storageSlotInfo.SlotIndex);
				employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, returnState);
				employee.state = -1;
				return;
			}

			LOG.TEMPDEBUG_FUNC(() => $"{employeeTaskName} #{GetUniqueId(employee)}: " +
				"Couldnt merge storage box.", LogEmployeeActions);
			employee.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, returnState);
			employee.state = -1;
		}
		

		public static List<Transform> GetEmployeesAssignedTo(EmployeeJob employeeJob) =>
			GetEmployeesAssignedTo(employeeJob, null);

		public static bool HasEmployeeAssignedTo(EmployeeJob employeeJob) =>
			GetEmployeesAssignedTo(employeeJob).Any();


		public static int GetEmployeeCount(EmployeeJob employeeJob) => 
			GetEmployeesAssignedTo(employeeJob).Count;

		public static List<Transform> GetEmployeesAssignedTo(EmployeeJob employeeJob, int[] allowedStates) {
			if (!NPC_Manager.Instance) {
				TimeLogger.Logger.LogWarning("The NPC_Manager is null. Make sure " +
					"to call this method after NPC_Manager Awake", LogCategories.AI);
			}

			return NPC_Manager.Instance.employeeParentOBJ.transform
				.Cast<Transform>().Where(
					(t) => {
						if (employeeJob == EmployeeJob.Any) {
							return true;
						}

						NPC_Info npcInfo = t.GetComponent<NPC_Info>();
						return npcInfo.taskPriority == (int)employeeJob &&
							(allowedStates == null || allowedStates.Length == 0 ||
								allowedStates.Contains(npcInfo.state));
					}
			).ToList();
		}


		//TODO 4 - Transpile original method to use EmployeeNextActionWait
		private static void UnequipBox(NPC_Info npcInfo) {
			npcInfo.EquipNPCItem(0);
			npcInfo.NetworkboxProductID = 0;
			npcInfo.NetworkboxNumberOfProducts = 0;
			npcInfo.StartWaitState(ModConfig.Instance.EmployeeNextActionWait.Value, 0);
			npcInfo.state = -1;
		}

		private static string GetEmployeeTaskName(int taskPriority) =>
			taskPriority switch {
				2 => "Restocker",
				3 => "Storage",
				4 => "Security",
				5 => "Technician",
				6 => "Online Order",
				7 => "Manufacturing",
				_ => "Unknown"
			};

		public static string GetUniqueId(NPC_Info NPC) {
			return $"{NPC.netId} ({NPC.NPCName})";
		}

		private static bool IsEmployeeAtDestination(NavMeshAgent employeePathing, out float stoppingDistance) {
            stoppingDistance = employeePathing.stoppingDistance;

            if (EmployeeWalkSpeedPatch.IsEmployeeSpeedIncreased) {

				if (EmployeeWalkSpeedPatch.IsWarpingEnabled() &&
						(employeePathing.pathStatus == NavMeshPathStatus.PathInvalid || employeePathing.pathStatus == NavMeshPathStatus.PathPartial)) {
					//PathInvalid may happen when warping employees are spawning, or very rarely when they warp to a box that just spawned
					//	at max height. I dont want to limit how high they can go so I ll just patch it like this for now.
					//As for PathPartial, see EmployeeTargetReservation.LastDestinationSet for an explanation.
					return false;
				}

				//Mitche was playing around with stoppingDistance, so this is in case he changes it.
				stoppingDistance = Math.Max(employeePathing.stoppingDistance, 1);

				//Reduced "arrive" requirements to avoid employees bouncing around when at high speeds.
				if (!employeePathing.pathPending && employeePathing.remainingDistance <= 5) {
					if (employeePathing.remainingDistance <= stoppingDistance &&
						(!employeePathing.hasPath || employeePathing.velocity.sqrMagnitude < EmployeeWalkSpeedPatch.WalkSpeedMultiplier * 5)) {

						employeePathing.velocity = Vector3.zero;
						return true;
					} else if (!EmployeeWalkSpeedPatch.IsWarpingEnabled()) {
						//Extra braking power to further reduce employees overshooting targets
						employeePathing.velocity = employeePathing.desiredVelocity / (3.5f / employeePathing.remainingDistance);
					}
				}

			} else {
				//Base game logic
				return !employeePathing.pathPending && employeePathing.remainingDistance <= employeePathing.stoppingDistance &&
					(!employeePathing.hasPath || employeePathing.velocity.sqrMagnitude == 0f);
			}

			return false;
		}

	}


}
