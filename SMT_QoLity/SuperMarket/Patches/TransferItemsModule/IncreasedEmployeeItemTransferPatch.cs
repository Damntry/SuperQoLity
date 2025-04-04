﻿
namespace SuperQoLity.SuperMarket.Patches.TransferItemsModule {

	/* Not needed now that EmployeeAddsItemToRow already accepts a variable number of items.
	
	//	IncreasedEmployeeItemTransferPatch and EmployeeNPCControlPatch depend on each other.
	//		If EmployeeNPCControlPatch patching fails, the vanilla method will fail when it calls my
	//		transpiled EmployeeAddsItemToRow. And the opposite is true too.
	//		I need to make so:
	//			- If EmployeeNPCControlPatch is disabled or fails patching, this transpile becomes
	//				inactive or unpatches itself.
	//			- If this transpile fails patching or is disabled, I need to change in EmployeeNPCControlPatch
	//				the way I call EmployeeAddsItemToRow and handle args and returns same way as if it was vanilla.

		
	internal class IncreasedEmployeeItemTransferPatch : FullyAutoPatchedInstance {

		
		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableTransferProducts.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee item transfer speed failed. Disabled";

		
		//This class was meant to exist temporarily until I transpiled NPCManager.EmployeeNPCControl
		//		so I could do both together in the same patch class.
		//		Seeing as that ship has sailed now that I ve changed so much in EmployeeNPCControl
		//		plus all future features I have planned for it, this class is here to stay.

		//Dont look at these and everything will be alright.
		public static ArgumentHelper<int> ArgBoxNumberProducts = new(typeof(IncreasedEmployeeItemTransferPatch), nameof(ArgBoxNumberProducts), -1);

		public static ArgumentHelper<int> ArgMaxProductsPerRow = new(typeof(IncreasedEmployeeItemTransferPatch), nameof(ArgMaxProductsPerRow), -1);


		//[HarmonyDebug]
		[HarmonyPatch(typeof(Data_Container), nameof(Data_Container.EmployeeAddsItemToRow))]
		[HarmonyBeforeInstance(typeof(IncreasedItemTransferPatch))]
		[HarmonyAfterInstance(typeof(EmployeeJobAIPatch))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			LocalBuilder localBnumTransfItems = generator.DeclareLocal(typeof(int));
			//Prepare the instruction to load the value of the numTransferItems local var into the stack
			CodeInstruction loadLocalVarNumTransferItemsInstr = CodeInstructionNew.LoadLocal(localBnumTransfItems.LocalIndex);

			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			///Old C#:
			///		num2 += quantity;
			///New C#:
			///		int numTransferItems = IncreasedItemTransferPatch.GetNumTransferItems(ArgBoxNumberProducts.Value, num2, ArgMaxProductsPerRow.Value);
			///		num2 += numTransferItems;
			codeMatcher.MatchForward(false,                     //Match for the whole "num2 += quantity" IL to step on its first line.
					new CodeMatch(inst => inst.IsLdloc()),
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(OpCodes.Add),
					new CodeMatch(inst => inst.IsStloc()));

			if (codeMatcher.IsInvalid) {
				throw new TranspilerDefaultMsgException("IL Line \"num2 += quantity\" couldnt be found.");
			}

			List<CodeInstruction> callGetNumTransferItemsInstrs = CallGetNumTransferItems(codeMatcher.Instruction, localBnumTransfItems.LocalIndex);

			codeMatcher
				.Insert(callGetNumTransferItemsInstrs)              //Insert the IL lines that calls the method to get the number of items we can transfer.
				.Advance(callGetNumTransferItemsInstrs.Count + 1)   //Go back to the same relative position before adding the call, plus an extra line.
				.SetInstruction(loadLocalVarNumTransferItemsInstr); //Replace the Ldc_I4_1 constant with the numTransferItems var

			if (codeMatcher.IsInvalid) {
				throw new TranspilerDefaultMsgException("Current IL line is invalid.");
			}

			///New C#:
			///		IncreasedEmployeeItemTransferPatch.ArgBoxNumberProducts.Value -= numTransferItems;
			codeMatcher
				.Advance(3)     //Move past the last line of the previous match.
				.Insert(        //Add instructions
				ArgBoxNumberProducts.LoadFieldArgHelper_IL, //Load static field of the argument
				new CodeInstruction(OpCodes.Dup),           //Duplicate previous
				ArgBoxNumberProducts.GetterValue_IL,        //Call getter to consume the duplicated field of the argument, and put its argument value in the stack
				loadLocalVarNumTransferItemsInstr,          //Load numTransferItems into the stack
				new CodeInstruction(OpCodes.Sub),           //Substract both
				ArgBoxNumberProducts.SetterValue_IL         //Call setter to consume both the remaining field of the argument, and the substraction result, to set as the argument value.
				);

			///Old C#:
			///		AchievementsManager.Instance.CmdAddAchievementPoint(1, 1);
			///New C#:
			///		AchievementsManager.Instance.CmdAddAchievementPoint(1, numTransferItems);
			MethodInfo achievementMethod = AccessTools.Method(typeof(AchievementsManager), nameof(AchievementsManager.CmdAddAchievementPoint));

			codeMatcher.MatchForward(false,					//Match to the line where "1" is passed as second argument to the achievement method.
					new CodeMatch(OpCodes.Ldc_I4_1),
					new CodeMatch(inst => inst.Calls(achievementMethod)));

			if (codeMatcher.IsInvalid) {
				throw new TranspilerDefaultMsgException("Current IL line is invalid after Looking for the achievement increase.");
			}

			codeMatcher.SetInstruction(loadLocalVarNumTransferItemsInstr); //Replace with our local var


			return codeMatcher.InstructionEnumeration();
		}


		private static List<CodeInstruction> CallGetNumTransferItems(CodeInstruction loadLocalNum2, int localVarItemTransferIndex) {
			//C#:	int numTransferItems = IncreasedItemTransferPatch.GetNumTransferItems(boxNumberProducts, num2, maxProductsPerRow);
			List<CodeInstruction> instrs = new();

			instrs.Add(ArgBoxNumberProducts.LoadFieldArgHelper_IL); //Load static field with the ArgumentHelper instance to later gets it value.
			instrs.Add(ArgBoxNumberProducts.GetterValue_IL);    //Load what would have been the 2º argument (boxNumProducts), but its now a glorified global static.
			instrs.Add(loadLocalNum2);                          //Load num2 local var
			instrs.Add(ArgMaxProductsPerRow.LoadFieldArgHelper_IL); //Load static field with the ArgumentHelper instance to later gets it value.
			instrs.Add(ArgMaxProductsPerRow.GetterValue_IL);    //Load what would have been the 3º argument (maxProductsPerRow), but its now a glorified global static.
			instrs.Add(new CodeInstruction(OpCodes.Ldc_I4, (int)CharacterType.Employee));	//Load the corresponding integer value of the enum as 4º parameter
			instrs.Add(Transpilers.EmitDelegate((int p1, int p2, int p3, CharacterType p4) =>
				IncreasedItemTransferPatch.GetNumTransferItems(p1, p2, p3, p4)));

			instrs.Add(CodeInstructionNew.StoreLocal(localVarItemTransferIndex));   //Save in the previously created local var the result of the method call

			return instrs;
		}
		
	}
	*/
}
