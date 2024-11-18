﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Damntry.UtilsBepInEx.IL;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;

namespace SuperQoLity.SuperMarket.Patches
{

    /// <summary>
    /// Uses transpiling to modify the methods that control the number of items to transfer to and from shelves.
    /// The end result is that we can modify this number of item to speed up transfers.
    /// </summary>
    [HarmonyPatch(typeof(Data_Container))]
	public class IncreasedItemTransferPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableTransferProducts.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Container patch failed. Item Transfer Module disabled";


		private enum RowActionType {
			Add,
			Remove
		}

		/// <summary>
		/// Gets the total number of products to move from the equipped box into the shelf, or viceversa.
		/// Parameter meanings change depending on the direction of transfer:
		/// From Box to Shelf: (boxItemCount, shelfItemCount, shelfMaxCapacity)
		/// From Shelf to Box: (shelfItemCount, boxItemCount, boxMaxCapacity)
		/// </summary>
		public static int GetNumTransferItems(int giverItemCount, int receiverItemCount, int receiverMaxCapacity) {
			int numMovedProducts = 1;
			
			if (ModConfig.Instance.EnableTransferProducts.Value && ModConfig.Instance.NumTransferProducts.Value != numMovedProducts && 
					(!ModConfig.Instance.TransferMoreProductsOnlyClosedStore.Value || !GameData.Instance.isSupermarketOpen)) {
				int receiverEmptyCapacity = receiverMaxCapacity - receiverItemCount;
				//Calculate quantity to transfer by taking the lower number of these 3 values:
				//	- Number of products to transfer from the config.
				//	- How much is left in the giver container (the box when placing, the shelf when removing).
				//	- How much is left to fill the receiving container (the shelf when placing, the box when removing).
				numMovedProducts = Math.Min(Math.Min(ModConfig.Instance.NumTransferProducts.Value, giverItemCount), receiverEmptyCapacity);
			}

			return numMovedProducts;
		}

		/* TODO 1 - BREAKING ERROR
		
		While I was testing getting things in and out of a box with increased transfer products. I got this error:

		[Error  : Unity Log] Disconnecting connection: connection(0) because handling a message of type Mirror.RpcMessage caused an Exception. This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: UnityEngine.UnityException: Transform child out of bounds
		  at (wrapper managed-to-native) UnityEngine.Transform.GetChild_Injected(intptr,int)
		  at UnityEngine.Transform.GetChild (System.Int32 index) [0x00006] in <04caeab4847d44b79ab96b12952cb81a>:0
		  at Data_Container.ItemSpawner () [0x00254] in <1ec1c12e4f004bc8aa470c37717b5c7b>:0
		  at Data_Container.UserCode_RpcUpdateObjectOnClients__Int32__Int32__Int32 (System.Int32 index, System.Int32 PID, System.Int32 PNUMBER) [0x0001c] in <1ec1c12e4f004bc8aa470c37717b5c7b>:0
		  at Data_Container.InvokeUserCode_RpcUpdateObjectOnClients__Int32__Int32__Int32 (Mirror.NetworkBehaviour obj, Mirror.NetworkReader reader, Mirror.NetworkConnectionToClient senderConnection) [0x0002e] in <1ec1c12e4f004bc8aa470c37717b5c7b>:0
		  at Mirror.RemoteCalls.RemoteProcedureCalls.Invoke (System.UInt16 functionHash, Mirror.RemoteCalls.RemoteCallType remoteCallType, Mirror.NetworkReader reader, Mirror.NetworkBehaviour component, Mirror.NetworkConnectionToClient senderConnection) [0x00019] in <06b4f157fbc44d6ebda84ac3ed72fcf2>:0
		  at Mirror.NetworkIdentity.HandleRemoteCall (System.Byte componentIndex, System.UInt16 functionHash, Mirror.RemoteCalls.RemoteCallType remoteCallType, Mirror.NetworkReader reader, Mirror.NetworkConnectionToClient senderConnection) [0x00065] in <06b4f157fbc44d6ebda84ac3ed72fcf2>:0
		  at Mirror.NetworkClient.OnRPCMessage (Mirror.RpcMessage message) [0x00020] in <06b4f157fbc44d6ebda84ac3ed72fcf2>:0
		  at (wrapper delegate-invoke) System.Action`1[Mirror.RpcMessage].invoke_void_T(Mirror.RpcMessage)
		  at Mirror.NetworkClient+<>c__DisplayClass61_0`1[T].<RegisterHandler>g__HandlerWrapped|0 (Mirror.NetworkConnection _, T value) [0x00000] in <06b4f157fbc44d6ebda84ac3ed72fcf2>:0
		  at Mirror.NetworkMessages+<>c__DisplayClass9_0`2[T,C].<WrapHandler>g__Wrapped|0 (C conn, T msg, System.Int32 _) [0x00000] in <06b4f157fbc44d6ebda84ac3ed72fcf2>:0
		  at Mirror.NetworkMessages+<>c__DisplayClass8_0`2[T,C].<WrapHandler>b__0 (Mirror.NetworkConnection conn, Mirror.NetworkReader reader, System.Int32 channelId) [0x000ac] in <06b4f157fbc44d6ebda84ac3ed72fcf2>:0

			I had a pretty large config number, something like 25? And I was quickly getting things in and out 
				of a box just fine while changing between items, in a few shelves. Then I ended up in front of the fridge with
				sodas doing the same and I quickly look down to the bottom shelf slot of sodas, (with the box empty maybe? 
				No idea because I was also testing trying to surpass limits) and tried to take something from it
				and this happened.
			So first of all, try getting sodas from that specific shelf, in case its bugged somehow. 
				Otherwise this is going to be a fun one.
			Maybe its just a desync from poor code? and the problem is simply working with too much quantities.
			Less probable, but it could also be that Im introducing lag to the method and even if I were moving one
				item at a time, it could desync? If I manage to make it happen with 1 item, switch back to vanilla
				and try to reproduce. Maybe its just a shitty game bug, though I might be making it worse?

		*/

		

		//[HarmonyDebug]
		[HarmonyPatch(nameof(Data_Container.AddItemToRow))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> AddItemToRowTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			return TranspilerChangeProductTransferCount(instructions, generator, RowActionType.Add);
		}

		//[HarmonyDebug]
		[HarmonyPatch(nameof(Data_Container.RemoveItemFromRow))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> RemoveItemFromRowTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			return TranspilerChangeProductTransferCount(instructions, generator, RowActionType.Remove);
		}

		private static IEnumerable<CodeInstruction> TranspilerChangeProductTransferCount(IEnumerable<CodeInstruction> instructions, ILGenerator generator, RowActionType rowActionType) {
			List<CodeInstruction> instrList = instructions.ToList();

			int indexStart = -1;
			int indexEnd = -1;

			FieldInfo extraParameter2 = AccessTools.Field(typeof(PlayerNetwork), "extraParameter2");
			int indexInsideElseBlock = instrList.FindLastIndex(instruction => instruction.LoadsField(extraParameter2));
			if (indexInsideElseBlock < 0) {
				throw new TranspilerDefaultMsgException($"Couldnt find the instruction loading the value of the field {nameof(PlayerNetwork.extraParameter2)}");
			}

			//Find the first br.s above this index (the "goto" like instruction, that in our C# code corresponds to the else), and get to what labels it moves to.
			//	The line after the br.s is the start of the else block, and the line before the label it moves to, is the end of the else block.
			int indexElse = instrList.FindLastIndex(indexInsideElseBlock, indexInsideElseBlock, instruction => instruction.opcode == OpCodes.Br || instruction.opcode == OpCodes.Br_S);
			if (indexElse < 0) {
				throw new TranspilerDefaultMsgException($"Couldnt find the Br/Br_S instruction when looking up to IL line {indexInsideElseBlock}");
			}

			//Find the end of the else block that we previously found
			Label endElseLabel = (Label)instrList[indexElse].operand;
			int lastIndex = instrList.Count - 1;
			int indexEndElse = instrList.FindLastIndex(lastIndex, lastIndex - indexInsideElseBlock, instruction => instruction.labels.FirstOrDefault() == endElseLabel);
			if (indexEndElse < 0) {
				throw new TranspilerDefaultMsgException($"Couldnt find IL Label {endElseLabel} when searching from IL line {indexInsideElseBlock}");
			}

			indexStart = indexElse + 1;
			indexEnd = indexEndElse - 1;

			//if (rowActionType == RowActionType.Remove) { BepInExTimeLogger.Loggerger.LogTimeWarning($"Instruction list before:\n\n{instrList.GetFormattedIL()}\n", TimeLoggerBase.LogCategories.TempTest); }

			ReplaceConstantsWithCallResult(instrList, generator, indexStart, indexEnd, rowActionType);

			//if (rowActionType == RowActionType.Remove) { BepInExTimeLogger.Loggerger.LogTimeWarning($"Instruction list after:\n\n{instrList.GetFormattedIL()}\n", TimeLoggerBase.LogCategories.TempTest); }

			return instrList;
		}

		private static void ReplaceConstantsWithCallResult(List<CodeInstruction> instrList, ILGenerator generator, int indexStart, int indexEnd, RowActionType rowActionType) {
			//Get the IL code to call the method that calculates the number of items to place at a time.
			List<CodeInstruction> callGetNumTransferItemsInstr = CreateILCall_GetNumTransferItems(instrList, indexStart, indexEnd, rowActionType);

			//Create local var to hold the number of items to transfer
			LocalBuilder localBnumItemsTransf = generator.DeclareLocal(typeof(int));
			//localBnumItemsTransf.SetLocalSymInfo("numItemsTransfer");
			//Prepare the instruction to load the value of local var into the stack
			CodeInstruction loadLocalVarNumItemsTransfer = CodeInstructionNew.LoadLocal(localBnumItemsTransf.LocalIndex);

			//Since we are going to write at the start of a new branch with labels, move those labels to what is now going to be the first line of the branch.
			instrList[indexStart].MoveLabelsTo(callGetNumTransferItemsInstr[0]);

			//The AddItemToRow method increases achievement points based on the number of items transferred.
			if (rowActionType == RowActionType.Add) {
				if (instrList[indexStart + 3].operand == null || !instrList[indexStart + 3].operand.ToString().Contains("AchievementPoint")) {
					throw new TranspilerDefaultMsgException($"The call to a method containing the text \"AchievementPoint\" wasnt found at the expected line {indexStart + 3}");
				}
				if (instrList[indexStart + 2].opcode != OpCodes.Ldc_I4_1) {
					throw new TranspilerDefaultMsgException($"The opcode Ldc_I4_1 for achievements wasnt found at the expected line {indexStart + 2}.");
				}

				//Replace the number of achievement points awarded. From the original 1, to the number of products to transfer.
				instrList[indexStart + 2] = loadLocalVarNumItemsTransfer;

				//Move the achievement related instructions to the end of the else.
				instrList.InsertRange(indexEnd, instrList.GetRange(indexStart, 4));
				instrList.RemoveRange(indexStart, 4);
			}

			//First line where we will exchange the value of 1 with the value returned by the method GetNumTransferItems
			int indexReplace1 = instrList.FindIndex(indexStart, indexEnd - indexStart, instruction => instruction.opcode == OpCodes.Ldc_I4_1);
			if (indexReplace1 == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find first line with Ldc_I4_1 to replace with our new local variable numItemsTransfer");
			}

			int indexReplace2 = instrList.FindIndex(indexReplace1 + 1, indexEnd - indexReplace1 + 1, instruction => instruction.opcode == OpCodes.Ldc_I4_1);
			if (indexReplace2 == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find second line with Ldc_I4_1 to replace with our new local variable numItemsTransfer");
			}

			//Save in the new local var the result of the method GetNumTransferItems(...)
			instrList.InsertRange(indexStart, callGetNumTransferItemsInstr);
			instrList.Insert(indexStart + callGetNumTransferItemsInstr.Count, CodeInstructionNew.StoreLocal(localBnumItemsTransf.LocalIndex));

			indexReplace1 += callGetNumTransferItemsInstr.Count + 1;
			indexReplace2 += callGetNumTransferItemsInstr.Count + 1;

			//Replace the constant placing a value of 1 in the stack, with the value of our local var
			instrList[indexReplace1] = loadLocalVarNumItemsTransfer;
			instrList[indexReplace2] = loadLocalVarNumItemsTransfer;
		}

		private static List<CodeInstruction> CreateILCall_GetNumTransferItems(List<CodeInstruction> instrList, int indexStart, int indexEnd, RowActionType rowActionType) {
			List<CodeInstruction> GetNumTransferItemsInstr = new List<CodeInstruction>();

			List<CodeInstruction> getBoxCountILInstr = GetBoxCountILInstructions(instrList, indexStart, indexEnd);

			//Put method parameters on the stack
			if (rowActionType == RowActionType.Add) {
				GetNumTransferItemsInstr.AddRange(GetILParametersAddingItems(instrList, getBoxCountILInstr));
			} else if (rowActionType == RowActionType.Remove) {
				GetNumTransferItemsInstr.AddRange(GetILParametersRemovingItems(instrList, getBoxCountILInstr));
			} else {
				throw new TranspilerDefaultMsgException($"Initial RowActionType: {rowActionType}");
			}

			//Call GetNumTransferItems to put its return value on the stack
			GetNumTransferItemsInstr.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(IncreasedItemTransferPatch), nameof(GetNumTransferItems))));

			return GetNumTransferItemsInstr;
		}

		private static List<CodeInstruction> GetILParametersAddingItems(List<CodeInstruction> instrList, List<CodeInstruction> getBoxCountILInstr) {
			List<CodeInstruction> paramsILinstr = new List<CodeInstruction>();
			//Find anchor that exists right after the fields comparison we are looking for (num7 >= num3)).
			int indexMessageError = instrList.FindIndex(instruction => instruction.opcode == OpCodes.Ldstr && instruction.operand.ToString() == "message1");

			if (indexMessageError == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find the line loading the string \"message1\".");
			}

			//Find previous index that loads a local var. Corresponds to var num3 with the shelf max capacity.
			int indexNum3 = instrList.FindLastIndex(indexMessageError, instruction => instruction.IsLdloc());
			if (indexNum3 == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find the line loading the local var \"num3\".");
			}
			int indexNum7 = indexNum3 - 1;  //Var num7 is on the previous index. Holds the current shelf item count.
			int num3_refIndex = instrList[indexNum3].LocalIndex();

			int num7_refIndex = instrList[indexNum7].LocalIndex();

			//Put parameters in the stack: (boxItemCount = component.extraParameter2, shelfItemCount = num7, shelfMaxCapacity = num3)
			paramsILinstr.AddRange(getBoxCountILInstr);     //Duplicate the IL lines that put the value of "component.extraParameter2" on the stack as 1º parameter
			paramsILinstr.Add(CodeInstructionNew.LoadLocal(num7_refIndex)); ; //Put value of num7 local var onto the stack, as 2º parameter.
			paramsILinstr.Add(CodeInstructionNew.LoadLocal(num3_refIndex)); ; //Put value of num3 local var onto the stack, as 3º parameter.

			return paramsILinstr;
		}

		private static List<CodeInstruction> GetILParametersRemovingItems(List<CodeInstruction> instrList, List<CodeInstruction> getBoxCountILInstr) {
			List<CodeInstruction> paramsILinstr = new List<CodeInstruction>();
			int maxItemsPerBoxLocalVarIndex = -1;
			int productInfoArrayInstance = 0;
			int num3IndexBeginSearch = int.MinValue;

			FieldInfo productInfoArrayField = AccessTools.Field(typeof(Data_Container), nameof(Data_Container.productInfoArray));
			FieldInfo maxItemsPerBoxField = AccessTools.Field(typeof(Data_Product), nameof(Data_Product.maxItemsPerBox));

			if (productInfoArrayField == null) {
				throw new TranspilerDefaultMsgException($"The field Data_Container.productInfoArray could not be found.");
			}
			if (maxItemsPerBoxField == null) {
				throw new TranspilerDefaultMsgException($"The field Data_Product.maxItemsPerBox could not be found.");
			}

			int maxItemsPerBox_refIndex = -1;
			int num3_refIndex = -1;

			for (int i = 0; i < instrList.Count; i++) {
				CodeInstruction instruction = instrList[i];

				//Search for the 1º instance of loading the field productInfoArray
				if (productInfoArrayInstance < 2 && instruction.LoadsField(productInfoArrayField)) {                                                                                                                                                                                          //Search for the 2º instance of loading the field productInfoArray

					productInfoArrayInstance++;
					if (productInfoArrayInstance == 2) {
						//Once found, we ll signal to begin searching for the opcode that stores the
						//	value, on the IL stack, with (hopefully) the number of items in the shelf.
						num3IndexBeginSearch = i + 1;
					}
				} else if (num3_refIndex == -1 && i >= num3IndexBeginSearch && i < num3IndexBeginSearch + 10 && instruction.IsStloc()) {  //Limit search to 10 lines
					num3_refIndex = instruction.LocalIndex();
				} else if (maxItemsPerBoxLocalVarIndex < 0 && instruction.LoadsField(maxItemsPerBoxField)) {
					maxItemsPerBoxLocalVarIndex = i + 1;
				} else if (i == maxItemsPerBoxLocalVarIndex && instruction.IsStloc()) {
					maxItemsPerBox_refIndex = instruction.LocalIndex();
				}
			}

			if (productInfoArrayInstance < 2) {
				throw new TranspilerDefaultMsgException($"Couldnt reach the appropiate amount of productInfoArray usages (productInfoArrayInstance = {productInfoArrayInstance})");
			} else if (num3_refIndex == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find the num3 local var reference (num3IndexBeginSearch = {num3IndexBeginSearch})");
			} else if (maxItemsPerBox_refIndex == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find the maxItemsPerBox local var reference (maxItemsPerBoxLocalVarIndex = {maxItemsPerBoxLocalVarIndex})");
			}
			//From Shelf to Box: (shelfItemCount, boxItemCount, boxMaxCapacity)
			paramsILinstr.Add(CodeInstructionNew.LoadLocal(num3_refIndex));             //Put value of num3 local var onto the stack, as 1º parameter.
			paramsILinstr.AddRange(getBoxCountILInstr);     ////Duplicate the IL lines that put the value of "component.extraParameter2" on the stack as 2º parameter
			paramsILinstr.Add(CodeInstructionNew.LoadLocal(maxItemsPerBox_refIndex));   //Put value of maxItemsPerBox local var onto the stack, as 3º parameter.

			return paramsILinstr;
		}

		private static List<CodeInstruction> GetBoxCountILInstructions(List<CodeInstruction> instrList, int indexStart, int indexEnd) {
			//Get the line with the Dup opcode, since around it there are the lines to load component.extraParameter2.
			int indexDup = instrList.FindIndex(indexStart, indexEnd - indexStart, instruction => instruction.opcode == OpCodes.Dup);
			if (indexDup == -1) {
				throw new TranspilerDefaultMsgException($"Couldnt find the Dup instruction between: (indexStart = {indexStart}, indexEnd = {indexEnd})");
			}

			//Copy the IL lines that put the value of "component.extraParameter2" on the stack
			return new List<CodeInstruction>(new CodeInstruction[] { instrList[indexDup - 1], instrList[indexDup + 1] });
		}

	}

}
