using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using static Damntry.UtilsBepInEx.Configuration.ConfigurationManager.SettingAttributes.ConfigurationManagerAttributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.JobScheduler;

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


		public override void OnPatchFinishedVirtual(bool IsActive) {
			if (!IsActive) {
				return;
			}

			//Show or hide manual job performance settings depending on the chosen mode
			//SetManualPerfModeVisibility();
			SetCustomPerfModeVisibility();

			ModConfig.Instance.EmployeeJobFrequencyMode.SettingChanged += (object sender, EventArgs e) => {
				//SetManualPerfModeVisibility();
				SetCustomPerfModeVisibility();

				ConfigManagerController.RefreshGUI();
			};
		}


		[HarmonyPrepare]
		public static bool HarmonyPrepare() {
			return Container<EmployeeJobAIPatch>.Instance.IsPatchActive;
		}

		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.FixedUpdate))]
		[HarmonyAfterInstance(typeof(EmployeeJobAIPatch))]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> FixedUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			///Old C#:
			///		if (childCount > 0) {
			///			if (!this.mainShelfUpdateIsRunning){
			///				base.StartCoroutine(this.MainRestockUpdate());
			///			}
			///			this.EmployeeNPCControl(this.counter2);
			///			this.counter2++;
			///			if (this.counter2 >= employeeCount) {
			///				this.counter2 = 0;
			///			}
			///		}
			///New C#:
			///		if (employeeCount > 0) {
			///			ProcessEmployeeJobs(this, employeeCount);
			///		}

			codeMatcher.MatchForward(true,                              //Match for the whole "if (childCount > 0)" IL to step on its first line.
					new CodeMatch(inst => inst.IsLdloc() && inst.labels.Count > 0),
					new CodeMatch(inst => inst.LoadsConstant()),
					new CodeMatch(inst => inst.operand is Label));

			if (codeMatcher.IsInvalid) {
				throw new TranspilerDefaultMsgException($"IL line \"if (childCount > 0)\" could not be found.");
			}

			/* Old one before the restocker employee rework
			codeMatcher.MatchForward(false,                             //Match for the whole "this.EmployeeNPCControl(this.counter2);" IL to step on its first line.
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(OpCodes.Ldfld),
					new CodeMatch(OpCodes.Call));
			*/

			int startPos = codeMatcher.Pos + 1;							//Save start position inside the condition block for later

			Label endLabel = (Label)codeMatcher.Operand;                //Get the destination label where the "if (childCount > 0) {" bracket ends.

			codeMatcher.MatchForward(true,                              //Move to the label.
				new CodeMatch(inst => inst.labels.Contains(endLabel)));

			codeMatcher.RemoveInstructionsInRange(startPos, codeMatcher.Pos - 1);   //Remove all instructions inside the "if (childCount > 0) {" block.

			List<CodeInstruction> processEmployeesInstr = new();
			processEmployeesInstr.Add(new CodeInstruction(OpCodes.Ldarg_0));    //Load "this" onto the stack
																				//TODO 4 - Dont assume its the first var, and search for it.
			processEmployeesInstr.Add(new CodeInstruction(OpCodes.Ldloc_0));    //Load childCount onto the stack
																				//Call the function to consume the 2 previous arguments on the stack.
			processEmployeesInstr.Add(Transpilers.EmitDelegate(JobSchedulerManager.ProcessEmployeeJobs));

			codeMatcher.Start().Advance(startPos).Insert(processEmployeesInstr);    //Insert the method call that replaces the old code functionality.

			return codeMatcher.InstructionEnumeration();
		}

		/*
		private void SetManualPerfModeVisibility() {
			bool IsManualModeEnabled = ModConfig.Instance.EmployeeJobFrequencyMode.Value == EnumJobFrequencyMultMode.Manual;

			ModConfig.Instance.EmployeeJobFrequencyManualMultiplier.SetConfigAttribute(
					ConfigAttributes.Browsable, IsManualModeEnabled);
			ModConfig.Instance.EmployeeJobManualMaxProcessTime.SetConfigAttribute(
				ConfigAttributes.Browsable, IsManualModeEnabled);
		}
		*/

		private void SetCustomPerfModeVisibility() {
			bool IsCustomMode = ModConfig.Instance.EmployeeJobFrequencyMode.Value == EnumJobFrequencyMultMode.Auto_Custom;

			ModConfig.Instance.CustomAvgEmployeeWaitTarget.SetConfigAttribute(
				ConfigAttributes.Browsable, IsCustomMode);
			ModConfig.Instance.CustomMinimumFrequencyMult.SetConfigAttribute(
					ConfigAttributes.Browsable, IsCustomMode);
			ModConfig.Instance.CustomMaximumFrequencyMult.SetConfigAttribute(
				ConfigAttributes.Browsable, IsCustomMode);
			ModConfig.Instance.CustomMaximumFrequencyReduction.SetConfigAttribute(
					ConfigAttributes.Browsable, IsCustomMode);
			ModConfig.Instance.CustomMaximumFrequencyIncrease.SetConfigAttribute(
				ConfigAttributes.Browsable, IsCustomMode);
		}

	}


}
