using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Component;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;
using System.Collections.Generic;
using System.Reflection.Emit;
using static Damntry.UtilsBepInEx.Configuration.ConfigurationManager.SettingAttributes.ConfigurationManagerAttributes;

namespace SuperQoLity.SuperMarket.Patches.NPC {

	public class NpcJobSchedulerPatch : FullyAutoPatchedInstance {

        //TODO 0 Performance - Testing performance in the laptop, it lost 1 fps when disabling the
        //  employee module vs it being enabled, with job scheduling disabled. From 11 to 10 which 
        //  is kind of big. Seems a bit too much for a fully disabled system that just runs the 
        //  employee method on my local code? The customer module was always disabled too btw.

        public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableCustomerChanges.Value || 
			ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = 
			$"{MyPluginInfo.PLUGIN_NAME} - Npc slowdown fix multiplier failed. Disabled";

        //These are used to define if a modded process has to run, but they replicate 2 different logics at once:
        // - Each process has different vanilla conditions. Since these flags replace the code at a point where each vanilla 
        //      npc method would have been executed, I can simply read its values instead of replicating vanilla conditions.
        // - If a specific transpile is not applied because its setting is not enabled, the original vanilla
        //      code will run instead to do the npc work. Since, without the transpile, the flag is not set
        //      anywhere else, the intrinsically false value will conveniently avoid executing the modded process earlier.
        //
        //Using the modded process is not equal to using the automated part of the job scheduler. All modded processes will use
        //  the job scheduler, though it ll be a very reduced version if the performance system is Disabled.
        private static bool canEmployeesDoSomething;
        private static bool canCustomersDoSomething;


        public override void OnPatchFinishedVirtual(bool IsActive) {
            if (!IsActive) {
                return;
            }

            try {
                //Show or hide manual job performance settings depending on the chosen mode
                SetCustomPerfModeVisibility();

                ModConfig.Instance.NpcJobFrequencyMode.SettingChanged += (sender, e) => {
                    SetCustomPerfModeVisibility();

                    ConfigManagerController.RefreshGUI();
                };

                JobSchedulerManager.InitializeJobSchedulerEvents();

                if (ModConfig.Instance.EnableEmployeeChanges.Value) {
                    RestockMatcher.Enable();
                }
            } catch {
                JobSchedulerManager.DisableJobScheduler();
                RestockMatcher.Disable();

                Container<NpcJobSchedulerPatch>.Instance.UnpatchInstance();

                throw;
            }
        }


        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.FixedUpdate))]
        [HarmonyPrefix]
        public static void FixedUpdateResetFlags(NPC_Manager __instance) {
            canEmployeesDoSomething = false;
            canCustomersDoSomething = false;
        }

        [HarmonyAfterInstance(typeof(EmployeeJobAIPatch))]  //To make sure that the OnPatchFinishedVirtual is executed after
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.FixedUpdate))]
		[HarmonyPostfix]
		public static void FixedUpdateProcessNPCs(NPC_Manager __instance) {
            //Since we removed the manufacturing logic in the transpile, add it back here.
            if (canEmployeesDoSomething && !__instance.mainManufacturingShelfUpdateIsRunning &&
                    GameData.Instance.GetComponent<UpgradesManager>().addonsBought[1]) {

                __instance.StartCoroutine(__instance.MainManufacturingRestockUpdate());
            }

            if (!canEmployeesDoSomething && !canCustomersDoSomething) {
                return;
            }

            //Pass as arguments if each method would have been called in the vanilla method, to keep vanilla logic intact.
            JobSchedulerManager.ProcessNPCJobs(__instance, canEmployeesDoSomething, canCustomersDoSomething);
		}

        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.FixedUpdate))]
        [HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> FixedUpdateNpcTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            CodeMatcher matcher = new(instructions, generator);

            if (ModConfig.Instance.EnableEmployeeChanges.Value) {
                matcher = TranspileEmployeeCall(matcher);
            }
            if (ModConfig.Instance.EnableCustomerChanges.Value) {
                matcher = TranspileCustomerCall(matcher);
            }

            return matcher.InstructionEnumeration();
        }


        public static CodeMatcher TranspileEmployeeCall(CodeMatcher codeMatcher) {
            ///Old C#:
            ///		if (childCount > 0) {
            ///			if (!mainShelfUpdateIsRunning){
            ///				base.StartCoroutine(MainRestockUpdate());
            ///			}
            ///			if (!mainManufacturingShelfUpdateIsRunning && GameData.Instance.GetComponent<UpgradesManager>().addonsBought[1]){
            ///             StartCoroutine(MainManufacturingRestockUpdate());
            ///         }
            ///			EmployeeNPCControl(this.counter2);
            ///			counter2++;
            ///			if (counter2 >= childCount) {
            ///				counter2 = 0;
            ///			}
            ///		}
            ///New C#:
            ///		if (childCount > 0) {
            ///			canEmployeesDoSomething = true;
            ///		}

            codeMatcher.MatchForward(true,                              //Match for the whole "if (childCount > 0)" IL to step on its first line.
                    new CodeMatch(inst => inst.IsLdloc() && inst.labels.Count > 0),
                    new CodeMatch(inst => inst.LoadsConstant()),
                    new CodeMatch(inst => inst.operand is Label));

            if (codeMatcher.IsInvalid) {
                throw new TranspilerDefaultMsgException($"IL line \"if (childCount > 0)\" could not be found.");
            }

            int startPos = codeMatcher.Pos + 1;                         //Save start position inside the condition block for later

            Label endLabel = (Label)codeMatcher.Operand;                //Get the destination label where the "if (childCount > 0) {" bracket ends.

            codeMatcher.MatchForward(true,                              //Move to the label.
                new CodeMatch(inst => inst.labels.Contains(endLabel)));

            codeMatcher.RemoveInstructionsInRange(startPos, codeMatcher.Pos - 1);   //Remove all instructions inside the "if (childCount > 0) {" block.

            //Set to true the value of canEmployeesDoSomething
            codeMatcher.Start().Advance(startPos)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
                .InsertAndAdvance(CodeInstruction.StoreField(typeof(NpcJobSchedulerPatch), nameof(canEmployeesDoSomething)));

            return codeMatcher;
        }

        private static CodeMatcher TranspileCustomerCall(CodeMatcher codeMatcher) {
            ///Old C#:
            ///	if (childCount2 != 0) {
            ///		if (counter >= childCount2 - 1) {
            ///			counter = 0;
            ///     } else {
			///			counter++;
            ///    }
            ///    CustomerNPCControl(counter);
            /// }
            ///New C#:
            ///		if (childCount > 0) {
            ///			canCustomersDoSomething = true;
            ///		}

            codeMatcher.MatchForward(true,                              //Match for the whole "if (childCount2 != 0)" IL to step on its first line.
                    new CodeMatch(inst => inst.IsLdloc()),
                    new CodeMatch(inst => inst.opcode == OpCodes.Brtrue_S || inst.opcode == OpCodes.Brtrue));

            if (codeMatcher.IsInvalid) {
                throw new TranspilerDefaultMsgException($"IL line \"if (childCount2 != 0)\" could not be found.");
            }

            int conditionalLabelPos = codeMatcher.Pos;

            Label startCondLabel = (Label)codeMatcher.Operand;          //Get the destination label that the childCount2 != 0 condition takes you when true.
            
            codeMatcher.MatchForward(true,                              //Move to the label.
                new CodeMatch(inst => inst.labels.Contains(startCondLabel)));
            int insideCondPos = codeMatcher.Pos;                        //Save start position inside the condition block for later
            codeMatcher.MatchForward(true,                              //Find the next return.
                new CodeMatch(inst => inst.opcode == OpCodes.Ret));

            //Remove all instructions inside the "if (childCount2 != 0), until the found return, inclusive
            codeMatcher.RemoveInstructionsInRange(insideCondPos - 1, codeMatcher.Pos - 1);

            codeMatcher.Start().Advance(conditionalLabelPos + 1)
                //Set to true the value of canCustomersDoSomething
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
                .InsertAndAdvance(CodeInstruction.StoreField(typeof(NpcJobSchedulerPatch), nameof(canCustomersDoSomething)))
                //At the end of the condition branch. Create target label
                .CreateLabel(out Label startConditionLabel)
                .Start().Advance(conditionalLabelPos)
                //Had problems trying to keep the existing brtrue opcode, so I inversed it
                //  instead so it goes to the exit point if the condition is not met.
                .Set(OpCodes.Brfalse, startConditionLabel);

            return codeMatcher;
        }

        private void SetCustomPerfModeVisibility() {
            bool IsCustomMode = ModConfig.Instance.NpcJobFrequencyMode.Value == EnumJobFrequencyMultMode.Auto_Custom;

            ModConfig.Instance.CustomEmployeeWaitTarget.SetConfigAttribute(
                ConfigAttributes.Browsable, IsCustomMode);
            ModConfig.Instance.CustomCustomerWaitTarget.SetConfigAttribute(
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
