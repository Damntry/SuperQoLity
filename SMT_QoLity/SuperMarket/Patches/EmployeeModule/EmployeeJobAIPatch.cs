using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Tasks;
using Damntry.Utils.Tasks.AsyncDelay;
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
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Movement;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.AI;


namespace SuperQoLity.SuperMarket.Patches.EmployeeModule {

	public enum EmployeeJob {
		Unassigned = 0,
		Cashier = 1,
		Restocker = 2,
		Storage = 3,
		Security = 4,
		Technician = 5,
		OnlineOrder = 6,
		Manufacturer = 7,

		All = 99
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
	public class EmployeeJobAIPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee patch failed. Employee Module inactive";


		//TODO 1 - Create something to try and detect when an employee is stuck in some kind of loop, so
		//	I can fix the vanilla error I ve been seeing of employees going back and forth in place.

		//TODO 2 - Make this be activable when Debug is enabled and you press a certain hotkey, so
		//			I can help people with employee problems.
		//			Make it send a notification in-game to confirm its enabled/disabled.
		public const bool LogEmployeeActions = false;


		private float destroyCounter;


		private static Queue<StolenProductSpawn> stolenProdPickups;

		/// <summary>The default number of units that an employee transfers between box and shelf.</summary>
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

		//TODO 4 - Massive change. I should trash EmployeeNPCControl and create a new system to handle employee jobs.
		//	The employee employeeObj should have a new Navigation employee that handles its own movement in an Update, and
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
		//	subset of data which will speed up employee performance, specially on massive supermarkets.
		//	Also, jobs themselves will be acquired instead of targets, which will make the reservation system much simpler, with
		//	the old one being discarded.
		//	It too has the advantage of the emitter being able to set its own attributes, like priority (a product shelf 
		//	warning of how empty it is).
		//	I see 2 disadvantages:
		//	 * It will use a bit more memory in general to save all the jobs, but the amount should 
		//		be so small it wont really make a difference, and the performance gains will be big, and it
		//		should reduce potential stuttering when too many employees are seeking jobs.
		//	 * The new system will be less "safe" in general. If Im not adding or removing a job at its proper place in some
		//		rare case, it could lead to problems which would not happen if the employee just searchs in every object at its
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
		//	for the employee. But dont specify where its coming from since there can be multiple.
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
							//EmployeeLogicExample will be called again by the parent for the same employee
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

		/* TODO 0 - Fix employee pathfinding
			First of all, prepare a save properly. Deunlock all franchise products except the starting one. 
			Only fill certain shelves so I can find which ones make the customer bug out in one or another way so I have perfect test cases.

			 *** Bugs

				pathPending = false, remainingDistance = XXX, hasPath = true, status = PathComplete

				- With the values above for the customer, and yet, it can not really properly reach the self checkout destination. It decides to go the direct path obstructed by a fence, 
					instead of taking the safe but long route around. Perhaps because it thinks it can squish close enough with the direct route. And its true, eventually it does manage to do it, 
					but it looks terrible and takes a while.
		
				- Another thing that happens is NPCs colliding with each other. For example a customer might want to pass through a small corridor next to a register, 
					but there is an employee on its register post in the middle of the corridor, and it wont move, and the NPC doesnt count it as an obstacle, so it keeps walking against it.
					There seems to be an assumption that npcs will avoid each other and a path will eventually open, but this is not true in all cases, for example, the previous one, and also
					when the other NPC is also stuck for some reason.
						A patchy fix would be to detect when a customer/employee hasnt moved for a while more than X distance, and in that case make it behave like a moving navmesh obstacle.
						Another even more patchy way would be to, instead of having it be a navmeshobstacle, just let npcs not moving enough, with a target, just let it think its at the destination
							like the unity navmesh already kind of does in some cases somehow.

				- Not really a bug but something to take into a account. The navmesh resolution might have been baked with relatively low resolution, so a few cm to a side wont mean much to it.
					Nothing I can do to solve this, but knowing this I could take alternative approaches.
	

			 *** Possible overall solutions and useful stuff

				- AI suggest to precalculate if the NPC can really reach the place:

					NavMeshPath path = new NavMeshPath();
					var agent = NPC_Manager.Instance.customersnpcParentOBJ.transform.GetChild(0)
						.gameObject.GetComponent<NavMeshAgent>();
					if (agent.CalculatePath(agent.destination, path)){
						Log("Path is wrong - " + path.status);
					}

				- To force a recalculate. This could be useful in the case where an NPC gets stuck but tries to keep moving with little success.
					agent.ResetPath();
					agent.SetDestination(targetPosition);


				- To test, without opening the store execute this:
						GameData.Instance.NetworktimeOfDay = 9
					Customers will come but time will not progress, so they will keep coming infinitely
		*/

		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.Awake))]
		[HarmonyPostfix]
		public static void AwakePatch(NPC_Manager __instance) {
			stolenProdPickups = new();
			//Modify the generic employee prefab to make mu own unique GameObject for each type of NPC
			//npcEmployeeAgentPrefab = UnityEngine.Object.Instantiate(__instance.npcAgentPrefab);
			//npcEmployeeAgentPrefab.name = __instance.npcAgentPrefab.name;
			//TODO 3 - My idea was to create an unique npcAgentPrefab for each NPCType, but
			//	I cant instantiate a scene object using a NetIdentity or Mirror wouldnt know which
			//	one to use, since they share the same sceneId. I cant change it to be an assetId
			//	instead because then it wouldnt work for clients that dont have the mod.
			//	Try to find an alternative so not every NPC has every type of functionality.
			GenericNPC.AddSuperQolNpcObjects(__instance.npcAgentPrefab, NPCType.Employee);

			GameObject stolenProductPrefab = __instance.npcAgentPrefab?.GetComponent<NPC_Info>()?.stolenProductPrefab;
			if (!stolenProductPrefab) {
				TimeLogger.Logger.LogTimeFatal("An object in the chain \"NPC_Manager.npcAgentPrefab." +
					"NPC_Info.stolenProductPrefab\" is null. It was probably renamed of changed places. " +
					"The employee patches cant be used and will be disabled.", LogCategories.AI);

				Container<EmployeeJobAIPatch>.Instance.UnpatchInstance();

				TimeLogger.Logger.SendMessageNotificationError("Something changed due to a game update " +
					"and the employee module can no longer work. It was disabled so the rest of the " +
					"mod can still work");

				return;
			}


			stolenProductPrefab.layer = SecurityPickUpLayer;
			//Begin loop to check for stolen products marked for removal.
			__instance.StartCoroutine(
				Container<EmployeeJobAIPatch>.Instance.PickupMarkedStolenProductsLoop()
			);
		}

		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.SpawnCustomerNPC), MethodType.Enumerator)]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> ChangeStolenProductLayerTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			AccessTools.Field(typeof(NPC_Manager), nameof(NPC_Manager.npcAgentPrefab));

			return codeMatcher.Instructions();
		}


		/*	No need to patch now that I call this local method from the transpiled FixedUpdate in EmployeePerformancePatch.
			The original is not called from anywhere else, being effectively abandoned.
		[HarmonyPatch(typeof(NPC_Manager), "EmployeeNPCControl")]
		[HarmonyPrefix] */
		/// <returns>False when the employee hasnt done any meaningful work. Otherwise True.</returns>
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
					case 1: {   //Cashier
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
										float timeToWait3 = Mathf.Clamp(__instance.productCheckoutWait - (float)employee.cashierLevel * 0.01f, 0.05f, 1f);
										employee.StartWaitState(timeToWait3, 6);
										employee.state = -1;
										return true;
									}
								case 6: {
										List<GameObject> internalProductListForEmployees = __instance.checkoutOBJ.transform
											.GetChild(employee.employeeAssignedCheckoutIndex)
											.GetComponent<Data_Container>().internalProductListForEmployees;
										int num2 = employee.cashierLevel / 10;
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
									}
									if (currentNPC.GetComponent<NPC_Info>().alreadyGaveMoney) {
										__instance.checkoutOBJ.transform.GetChild(employee.employeeAssignedCheckoutIndex).GetComponent<Data_Container>().AuxReceivePayment(0f, true);
										employee.state = 3;
										return true;
									}
									float timeToWait2 = Mathf.Clamp(__instance.productCheckoutWait - (float)employee.cashierLevel * 0.01f, 0.05f, 1f);
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
					case 2: //Restocker
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
							case 3: {
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Product shelf reached.", LogEmployeeActions);
									RestockJobInfo restockJob = employee.GetRestockJobInfo();

									if (__instance.shelvesOBJ.transform.GetChild(restockJob.ProdShelf.ShelfIndex).GetComponent<Data_Container>().
											productInfoArray[restockJob.ShelfProdInfoIndex] == restockJob.ProdShelf.ExtraData.ProductId) {
										LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Shelf reached has same product as box. So far so good.", LogEmployeeActions);
										employee.state = 4;
										return true;
									}
									LOG.TEMPDEBUG_FUNC(() => $"Restocker #{GetUniqueId(employee)}: Shelf reached has now different product than box.", LogEmployeeActions);
									employee.state = 5;
									return true;
								}
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
									//	- A player fills or changes the product of the shelf, before the reserving employee reaches it.
									//	- The reserving employee has already filled the shelf himself and cant place anymore, since
									//		this switch case is called repeatedly over and over until the shelf is full.
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
										employee.StartWaitState(1f, 0);
										employee.state = -1;
									}
									return true;
								}
						}

						employee.state = 0;
						return true;
					case 3: //Storage
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

								//TODO !0 AI IMPROVEMENTS - Instead of going to rest, find boxes in
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
								//		employee reaches it. Employee is already at the rest spot, so it doesnt call the move method that
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
									employee.StartWaitState(1f, 0);
									employee.state = -1;
								}
								return true;
						}
						employee.state = 0;
						return true;
					case 4: //Security
						switch (state) {
							case 0: {
									if (employee.equippedItem > 0) {
										__instance.DropBoxOnGround(employee);
										UnequipBox(employee);
										return true;
									}
									GameObject thiefTarget = __instance.GetThiefTarget();
									if (thiefTarget) {
										employee.currentChasedThiefOBJ = thiefTarget;
										employee.state = 2;
										return true;
									}
									if (__instance.IsFirstSecurityEmployee(employeeObj) ||
											(__instance.customersnpcParentOBJ.transform.childCount == 0
												&& GameData.Instance.timeOfDay > 22f)) {

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
									float timeToWait = Mathf.Clamp(1.45f - (float)employee.securityLevel * 0.01f, 0.5f, 2f);
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
									if (pickUpmode != EnumSecurityPickUp.Disabled) {
										if (DoSecurityAreaPickUp(__instance, pickUpmode, employeeObj, employee, stoppingDistance)) {
											employee.StartWaitState(0.5f, 0);
											employee.state = -1;
											return true;
										}
									}

									//TODO 0 - Only if the object is not in the deletion list
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
					case 5: //Technician (repairs + cardboard bale recycling)
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
									GameData.Instance.CmdAlterFunds(fundsToAdd);
									employee.technicianExperience += employee.technicianValue;
									employee.state = 3;
									return true;
								}
							default:
								employee.state = 0;
								return true;
						}
					case 6: //Online orders
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
										break;
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
									GameObject val4 = (employee.orderProductLocationInfoArray[0] != 0) ? __instance.shelvesOBJ : __instance.storageOBJ;
									Data_Container employee3 = val4.transform.GetChild(employee.orderProductLocationInfoArray[1]).GetComponent<Data_Container>();
									int[] productInfoArray = employee3.productInfoArray;
									int num6 = productInfoArray[employee.orderProductLocationInfoArray[2] + 1];
									if (employee.packagingAssignedOrderProducts[0] == productInfoArray[employee.orderProductLocationInfoArray[2]] && num6 > 0) {
										if (employee.orderProductLocationInfoArray[0] == 0) {
											employee3.EmployeeUpdateArrayValuesStorage(employee.orderProductLocationInfoArray[2], employee.packagingAssignedOrderProducts[0], num6 - 1);
										} else {
											employee3.NPCGetsItemFromRow(employee.packagingAssignedOrderProducts[0]);
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
												stringBuilder.Append("_");
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
					case 7: //Manufacturing
						if (state == 0 && employee.equippedItem > 0) {
							__instance.DropBoxOnGround(employee);
							UnequipBox(employee);
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


		private static bool DoSecurityAreaPickUp(NPC_Manager __instance, EnumSecurityPickUp pickUpmode, 
				GameObject employeeObj, NPC_Info employee, float stoppingDistance) {
			//Pick up multiple products in an area, based on the employee security level.
			int securityLevel = pickUpmode switch {
				EnumSecurityPickUp.Reduced => (int)Math.Ceiling(employee.securityLevel.ClampReturn(0, MaxSecurityPickUpLevel) / 2d),
				EnumSecurityPickUp.Normal => employee.securityLevel.ClampReturn(0, MaxSecurityPickUpLevel),
				EnumSecurityPickUp.AlwaysMaxed => MaxSecurityPickUpLevel,
				_ => throw new NotImplementedException($"Unknown security pick up mode: {pickUpmode}"),
			};

			int pickUpCount = (int)Math.Ceiling(securityLevel / LevelsForExtraPickUp);
			float pickUpRangeFromLevel = (securityLevel / (float)MaxSecurityPickUpLevel) * SecurityPickUpRangeLevelMult;
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

						//TODO 0 - Improve this so if I detect that the client has SuperQol mod, it calls a
						//	new RPC method that deactivates the object on the client, and then it gets
						//	deleted over time.

						//Enqueue to be deleted later over time.
						stolenProdPickups.Enqueue(stoleProdSpawn);



						employee.securityExperience++;
						pickUpCount--;
					} else {
						TimeLogger.Logger.LogTimeDebug($"Security area pick up found a " +
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
				destroyCounter += MaxStolenProdRemoval;
				while (destroyCounter >= 1) {
					destroyCounter--;
					if (stolenProdPickups.Count > 0) {
						if (stolenProdPickups.TryDequeue(out StolenProductSpawn stolenProdPickup)) {
							NetworkServer.UnSpawn(stolenProdPickup.gameObject);

							//TODO 0 - Add the unspawned to another different list and make yet another 
							//	coroutine to call the Destroy on the list, but with a much slower rate.
							//NetworkServer.Destroy(stolenProdPickup.gameObject);
						}
					} else {
						break;
					}
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

				//employee.currentFreeStorageIndex = storageToMerge.SlotIndex;
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
			float num5 = Vector3.Distance(employeeObj.transform.position, __instance.recycleSpot1OBJ.transform.position);
			float num6 = Vector3.Distance(employeeObj.transform.position, __instance.recycleSpot2OBJ.transform.position);
			if (num5 < num6) {
				employeeSQoL.MoveEmployeeTo(__instance.recycleSpot1OBJ, __instance.recycleSpot1OBJ);
				employee.state = recycleTargetState;
				return;
			}
			employeeSQoL.MoveEmployeeTo(__instance.recycleSpot2OBJ, __instance.recycleSpot2OBJ);
			employee.state = recycleTargetState;
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

		public static bool HasEmployeAssignedTo(EmployeeJob employeeJob) {
			if (!NPC_Manager.Instance) {
				TimeLogger.Logger.LogTimeWarning("The NPC_Manager is null. Make sure " +
					"to call this method after NPC_Manager Awake", LogCategories.AI);
			}

			return NPC_Manager.Instance.employeeParentOBJ.transform
				.Cast<Transform>().Any(
					(t) => t.GetComponent<NPC_Info>().taskPriority == (int)employeeJob
			);
		}

		
		public static List<Transform> GetEmployeesAssignedTo(EmployeeJob employeeJob) {
			if (!NPC_Manager.Instance) {
				TimeLogger.Logger.LogTimeWarning("The NPC_Manager is null. Make sure " +
					"to call this method after NPC_Manager Awake", LogCategories.AI);
			}

			return NPC_Manager.Instance.employeeParentOBJ.transform
				.Cast<Transform>().Where(
					(t) => employeeJob == EmployeeJob.All || 
							t.GetComponent<NPC_Info>().taskPriority == (int)employeeJob
			).ToList();
		}

		public static int GetEmployeeCount(EmployeeJob employeeJob) => 
			GetEmployeesAssignedTo(employeeJob).Count;



		/*
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.CustomerNPCControl))]
		[HarmonyPrefix]
		private static bool CustomerNPCControlPatch(NPC_Manager __instance, int NPCIndex) {
			GameObject gameObject = __instance.customersnpcParentOBJ.transform.GetChild(NPCIndex).gameObject;
			NPC_Info component = gameObject.GetComponent<NPC_Info>();
			int state = component.state;
			NavMeshAgent component2 = gameObject.GetComponent<NavMeshAgent>();
			if (state == -1) {
				return false;
			}
			if (!component2.pathPending && component2.remainingDistance <= component2.stoppingDistance && (!component2.hasPath || component2.velocity.sqrMagnitude == 0f)) {
				if (component.productsIDToBuy.Count > 0) {
					if (state != 0) {
						if (state != 1) {
							return false;
						}
						int num = component.productsIDToBuy[0];
						if (__instance.IsItemInShelf(component.shelfThatHasTheItem, num)) {
							float num2 = ProductListing.Instance.productPlayerPricing[num];
							Data_Product component3 = ProductListing.Instance.productPrefabs[num].GetComponent<Data_Product>();
							int productTier = component3.productTier;
							float num3 = component3.basePricePerUnit * ProductListing.Instance.tierInflation[productTier] * UnityEngine.Random.Range(2f, 2.5f);
							component.productsIDToBuy.RemoveAt(0);
							if (num2 > num3) {
								component.StartWaitState(1.5f, 0);
								component.RPCNotificationAboveHead("NPCmessage1", "product" + num.ToString());
								GameData.Instance.AddExpensiveList(num);
							} else {
								component.productsIDCarrying.Add(num);
								component.productsCarryingPrice.Add(num2);
								component.numberOfProductsCarried++;
								component.StartWaitState(1.5f, 0);
								__instance.shelvesOBJ.transform.GetChild(component.shelfThatHasTheItem).GetComponent<Data_Container>().NPCGetsItemFromRow(num);
							}
							component.state = -1;
							return false;
						}
						component.state = 0;
						return false;
					} else {
						int num4 = component.productsIDToBuy[0];
						int num5 = ReflectionHelper.CallMethod<int>(__instance, "WhichShelfHasItem", [num4]);
						if (num5 == -1) {
							GameData.Instance.AddNotFoundList(num4);
							component.productsIDToBuy.RemoveAt(0);
							component.RPCNotificationAboveHead("NPCmessage0", "product" + num4.ToString());
							component.StartWaitState(1.5f, 0);
							component.state = -1;
							return false;
						}
						component.shelfThatHasTheItem = num5;
						Vector3 position = __instance.shelvesOBJ.transform.GetChild(num5).Find("Standspot").transform.position;
						component2.destination = position;
						component.state = 1;
						return false;
					}
				} else {
					if (component.isAThief && state < 2) {
						component2.destination = __instance.exitPoints.GetChild(UnityEngine.Random.Range(0, __instance.exitPoints.childCount - 1)).transform.position;
						component2.speed *= 1.25f;
						component.RPCNotificationAboveHead("NPCmessage4", "");
						component.RpcShowThief();
						component.thiefFleeing = true;
						component.thiefProductsNumber = component.productsIDCarrying.Count;
						component.StartWaitState(2f, 11);
						component.state = -1;
						return false;
					}
					if (component.productsIDCarrying.Count == 0 && state < 2) {
						component2.destination = __instance.exitPoints.GetChild(UnityEngine.Random.Range(0, __instance.exitPoints.childCount - 1)).transform.position;
						component.RPCNotificationAboveHead("NPCmessage2", "");
						component.StartWaitState(2f, 10);
						component.state = -1;
						return false;
					}
					if (!component.selfcheckoutAssigned && __instance.selfCheckoutOBJ.transform.childCount > 0 && !component.isAThief) {
						int availableSelfCheckout = ReflectionHelper.CallMethod<int>(__instance, "GetAvailableSelfCheckout", [component]);
						if (availableSelfCheckout > -1) {
							component.selfcheckoutIndex = availableSelfCheckout;
							__instance.selfCheckoutOBJ.transform.GetChild(availableSelfCheckout).GetComponent<Data_Container>().checkoutQueue[0] = true;
						}
						component.selfcheckoutAssigned = true;
					}
					if (component.selfcheckoutIndex > -1) {
						switch (state) {
							case 0:
							case 1:
								component2.destination = __instance.selfCheckoutOBJ.transform.GetChild(component.selfcheckoutIndex).transform.Find("Standspot").transform.position;
								component.state = 2;
								return false;
							case 2:
								if (!component.isCurrentlySelfcheckouting) {
									component.isCurrentlySelfcheckouting = true;
									component.StartCustomerSelfCheckout(__instance.selfCheckoutOBJ.transform.GetChild(component.selfcheckoutIndex).gameObject);
									return false;
								}
								break;
							case 3:
								component.paidForItsBelongings = true;
								GameData.Instance.dailyCustomers++;
								AchievementsManager.Instance.CmdAddAchievementPoint(3, 1);
								component2.destination = __instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount - 1)).transform.position;
								__instance.selfCheckoutOBJ.transform.GetChild(component.selfcheckoutIndex).GetComponent<Data_Container>().checkoutQueue[0] = false;
								component.state = 99;
								return false;
							default:
								if (state == 99) {
									UnityEngine.Object.Destroy(gameObject);
									return false;
								}
								break;
						}
						return false;
					}
					switch (state) {
						case 0:
						case 1: {
								component.selfcheckoutAssigned = true;
								int num6 = ReflectionHelper.CallMethod<int>(__instance, "CheckForAFreeCheckout");
								if (num6 == -1) {
									component.isAThief = true;
									component.RPCNotificationAboveHead("NPCmessage3", "");
									component.StartWaitState(2f, 1);
									component.state = -1;
									return false;
								}
								Transform transform = __instance.checkoutOBJ.transform.GetChild(num6).transform.Find("QueueAssign");
								component2.destination = transform.position;
								component.state = 2;
								return false;
							}
						case 2: {
								int num7 = ReflectionHelper.CallMethod<int>(__instance, "CheckForAFreeCheckout");
								if (num7 == -1) {
									component.state = 1;
									return false;
								}
								int checkoutQueueNumber = ReflectionHelper.CallMethod<int>(__instance, "GetCheckoutQueueNumber", [num7]);
								component.currentCheckoutIndex = num7;
								component.currentQueueNumber = checkoutQueueNumber;
								Transform child = __instance.checkoutOBJ.transform.GetChild(num7).transform.Find("QueuePositions").transform.GetChild(checkoutQueueNumber);
								component2.destination = child.position;
								component.state = 3;
								return false;
							}
						case 3:
							if (component.currentQueueNumber == 0) {
								if (component.productsIDCarrying.Count == component.numberOfProductsCarried) {
									__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex).GetComponent<Data_Container>().NetworkproductsLeft = component.numberOfProductsCarried;
								}
								if (component.productsIDCarrying.Count == 0) {
									component.state = 4;
									return false;
								}
								if (!component.placingProducts) {
									component.PlaceProducts(__instance.checkoutOBJ);
									component.placingProducts = true;
									return false;
								}
								return false;
							} else {
								int num8 = component.currentQueueNumber - 1;
								Data_Container component4 = __instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex).GetComponent<Data_Container>();
								if (!component4.checkoutQueue[num8]) {
									component4.checkoutQueue[component.currentQueueNumber] = false;
									component.currentQueueNumber = num8;
									component4.checkoutQueue[component.currentQueueNumber] = true;
									Transform child2 = __instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex).transform.Find("QueuePositions").transform.GetChild(component.currentQueueNumber);
									component2.destination = child2.position;
									return false;
								}
								return false;
							}
						case 4:
							if (__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex).GetComponent<Data_Container>().productsLeft == 0) {
								component.state = 5;
								return false;
							}
							return false;
						case 5:
							if (!component.alreadyGaveMoney) {
								component.alreadyGaveMoney = true;
								int num9 = UnityEngine.Random.Range(0, 2);
								__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex).GetComponent<Data_Container>().RpcShowPaymentMethod(num9);
								return false;
							}
							return false;
						case 6:
						case 7:
						case 8:
						case 9:
							break;
						case 10:
							component.paidForItsBelongings = true;
							GameData.Instance.dailyCustomers++;
							AchievementsManager.Instance.CmdAddAchievementPoint(3, 1);
							component2.destination = __instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount - 1)).transform.position;
							component.state = 99;
							return false;
						case 11:
							component2.destination = __instance.transform.Find("ThiefRoamSpots").transform.GetChild(UnityEngine.Random.Range(0, __instance.transform.Find("ThiefRoamSpots").transform.childCount - 1)).transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
							component.StartWaitState(1f, 12);
							component.state = -1;
							return false;
						case 12:
							component2.destination = __instance.transform.Find("ThiefRoamSpots").transform.GetChild(UnityEngine.Random.Range(0, __instance.transform.Find("ThiefRoamSpots").transform.childCount - 1)).transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
							component.StartWaitState(1f, 13);
							component.state = -1;
							return false;
						case 13:
							component2.destination = __instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount - 1)).transform.position;
							component.state = 99;
							return false;
						default:
							if (state == 99) {
								UnityEngine.Object.Destroy(gameObject);
								return false;
							}
							break;
					}
				}
			}
			return false;
		}
		*/


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
						(!employeePathing.hasPath || employeePathing.velocity.sqrMagnitude < (EmployeeWalkSpeedPatch.WalkSpeedMultiplier * 5))) {

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
