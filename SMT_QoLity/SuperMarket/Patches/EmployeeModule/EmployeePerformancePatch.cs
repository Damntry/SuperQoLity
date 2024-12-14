﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Damntry.Utils.Logging;
using Damntry.Utils.Timers;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsUnity.Timers;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;


namespace SuperQoLity.SuperMarket.Patches.EmployeeModule {


	/// <summary>
	/// In the base game, employee logic is processed in FixedUpdate, which executes 50 times each second.
	/// In FixedUpdate, a single employee performs a single job step in the method EmployeeNPCControl, which 
	/// means that having more or faster employees, strains the amount of actions they can perform, and 
	/// employees begin to slowdown. 
	/// Even if the employee doesnt have a job assigned, it still uses the "action", so just by existing 
	/// it takes away from the rest.
	/// To solve this, I call EmployeeNPCControl more than once on every FixedUpdate, configurable by a setting
	/// so the user can set up their own performance goals. Generally this method is pretty light so the 
	/// performance loss is small.
	/// </summary>
	public class EmployeePerformancePatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee slowdown fix multiplier failed. Disabled";

		private static int currentEmployeeId;

		//To maintain >= 60fps, each frame must take < 16.6~ ms. A bit less than 7ms for 144fps.
		//I dont know how much every other process takes in relative terms, but
		//	5ms seems like a high enough ceiling to let the user go crazy enough 
		//	with the multiplier, without them completely destroying their game.
		private const double MaxEmployeeProcessingTimeMillis = 5d;

		private static Lazy<PeriodicTimeLimitedCounter<UnityTimeStopwatch>> periodicCounter = new Lazy<PeriodicTimeLimitedCounter<UnityTimeStopwatch>>(() =>
			new PeriodicTimeLimitedCounter<UnityTimeStopwatch>(true, 30, 30000, true));

		private static bool IsTimeoutWarningActive;

		/// <summary>
		/// FixedUpdate is active while the game is loading, but we dont want to send performance warnings
		/// to the user since the cpu is doing plenty of work and long process times are normal. With this
		/// we activate the warning only while the game is in the game world.
		/// </summary>
		[HarmonyPrepare]
		private static bool Prepare() {
			GameWorldEventsPatch.OnGameWorldChange += (ev) => {
				if (ev == GameWorldEvent.Start) {
					IsTimeoutWarningActive = true;
				} else if (ev == GameWorldEvent.Quit) {
					IsTimeoutWarningActive = false;
				}
			};

			return true;
		}

		//TODO 3 - Throw meaningful errors when it cant match instructions
		[HarmonyPatch(typeof(NPC_Manager), "FixedUpdate")]
		[HarmonyAfterInstance(typeof(EmployeeJobAIPatch))]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> FixedUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			///Old C#:<
			///		if (childCount > 0) {
			///			this.EmployeeNPCControl(this.counter2);
			///			this.counter2++;
			///			if (this.counter2 >= childCount) {
			///				this.counter2 = 0;
			///			}
			///		}
			///New C#:
			///		if (childCount > 0) {
			///			ProcessEmployeeJobs(this, childCount);
			///		}
			codeMatcher.MatchForward(false,                                 //Match for the whole "this.EmployeeNPCControl(this.counter2);" IL to step on its first line.
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(OpCodes.Ldfld),
					new CodeMatch(OpCodes.Call));

			int startPos = codeMatcher.Pos;                                 //Save start position for later

			Label endLabel = (Label)codeMatcher.Advance(-1).Operand;        //Get the destination label where the "if (childCount > 0) {" bracket ends.

			codeMatcher.MatchForward(true,                                  //Move to the label.
				new CodeMatch(inst => inst.labels.Contains(endLabel)));

			codeMatcher.RemoveInstructionsInRange(startPos, codeMatcher.Pos - 1);   //Remove all instructions inside the "if (childCount > 0) {" block.

			List<CodeInstruction> processEmployeesInstr = new();
			processEmployeesInstr.Add(new CodeInstruction(OpCodes.Ldarg_0));    //Load "this" onto the stack
																				//TODO 4 - Dont assume its the first var, and search for it.
			processEmployeesInstr.Add(new CodeInstruction(OpCodes.Ldloc_0));    //Load childCount onto the stack
			processEmployeesInstr.Add(Transpilers.EmitDelegate(                 //Call the function to consume the 2 previous arguments on the stack.
				(NPC_Manager __instance, int childCount) => ProcessEmployeeJobs(__instance, childCount)));

			codeMatcher.Start().Advance(startPos).Insert(processEmployeesInstr);    //Insert the method call that replaces the old code functionality.

			return codeMatcher.InstructionEnumeration();
		}


		private static void ProcessEmployeeJobs(NPC_Manager __instance, int childCount) {
			UnityTimeStopwatch processTime = UnityTimeStopwatch.StartNew();

			for (int i = 0; i < ModConfig.Instance.EmployeeJobFrequencyMultiplier.Value; i++) {
				if (currentEmployeeId >= childCount) {
					currentEmployeeId = 0;
				}
				EmployeeJobAIPatch.EmployeeNPCControlPatch(__instance, currentEmployeeId);
				currentEmployeeId++;

				//Make sure we dont overdo the time we take to process employees.
				if (ProcessTimedOut(processTime)) {
					break;
				}
			}
		}


		private static bool ProcessTimedOut(UnityTimeStopwatch processTime) {
			if (!IsTimeoutWarningActive) {
				return true;	//Stop processing more employee actions than default while loading.
			}

			if (processTime.ElapsedMillisecondsPrecise >= MaxEmployeeProcessingTimeMillis) {

				if (!periodicCounter.Value.TryIncreaseCounter()) {
					//TODO 6 - Maybe show this in-game too, but only once the first time they start a game.
					TimeLogger.Logger.LogTimeWarning("Processing employee actions is taking too much time and its " +
						$"being automatically limited by {MyPluginInfo.PLUGIN_NAME} to improve performance. " +
						$"To fix this, try decreasing the value of the setting \"{ModConfig.Instance.EmployeeJobFrequencyMultiplier.Definition.Key}\".",
						TimeLogger.LogCategories.PerfTest);
				}

				return true;
			}

			return false;
		}

	}


}
