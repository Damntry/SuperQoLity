using System;
using System.Collections.Generic;
using System.Reflection;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;

namespace SuperQoLity.SuperMarket.Patches
{

    internal class IncreasedEmployeeItemTransferPatch : FullyAutoPatchedInstance<IncreasedEmployeeItemTransferPatch> {

		//TODO 3 - This class only exists temporarily until I transpile the NPCManager.EmployeeNPCControl method.
		//		Once that is done, I need to move this back to TransferMoreItemsPatch where it belongs, and
		//		create another separate transpiler method in the same class, that changes the call
		//		arguments of EmployeeAddsItemToRow to pass what we need.
		//		This way the functionality will be fully self contained again.


		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableTransferProducts.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Employee item transfer speed failed. Disabled";



		private static readonly Lazy<MethodInfo> RpcUpdateObjectOnClientsMethod = new Lazy<MethodInfo>(() =>
			AccessTools.Method($"{nameof(Data_Container)}:RpcUpdateObjectOnClients", [typeof(int), typeof(int), typeof(int)]));

		
		//TODO 3 - Need to transpile this, not just for future proofing, but also because currently this is being called from NPCTargetAssignmentPatch
		//		which is related to the employee module. So if the employee module is disabled, now this never gets called and it doesnt work, which kind of makes
		//		sense since its employee related but its not the way I want to group settings.-
		//	WELP, funny thing, I need arguments that I can only get from the caller, which is the big employee control method which I am currently
		//		completely replacing. I could still do this below and from my NPC method call with reflection the original, now transpiled one, but
		//		then I would have to be custom controlling the patching state because if this transpile fails, I need to call either a backup of this
		//		local replacement function below, or the vanilla function which has different arguments.
		//	I would need to move this function into its own patch functionk
		
		public static bool EmployeeAddsItemToRow(Data_Container __instance, int rowIndex, ref int boxNumberProducts, int maxProductsPerRow) {
			int num2 = __instance.productInfoArray[rowIndex + 1];




			int numTransferItems = IncreasedItemTransferPatch.GetNumTransferItems(boxNumberProducts, num2, maxProductsPerRow);
			boxNumberProducts -= numTransferItems;
			num2 += numTransferItems;
			


			AchievementsManager.Instance.CmdAddAchievementPoint(1, num2);




			return false;
		}

		[HarmonyDebug]
		[HarmonyPatch(typeof(Data_Container), nameof(Data_Container.EmployeeAddsItemToRow))]
		[HarmonyBeforeInstance(IncreasedItemTransferPatch.Instance)]    
								//TODO 3 - So this thing only works passing an Harmony id, which is relatively problematic since I dont want to expose it.
								//			Otherwise you could take the harmonyId, and create a new harmony object that replicates the one from a different
								//			patch instance, which is jorribol.
								//			Create an harmonyId {get;}, but make it internal, and then create an alternative HarmonyBefore attribute that
								//			will access this harmonyId since its going to be in the same assembly. Not 100% how its meant to work, but its fine. Izi.
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			return new CodeMatcher(instructions)
						 .InstructionEnumeration();
		}

		/*
		//COMMENTED CODE IS THE ORIGINAL CODE I CAN KEEP AS IS, UNCOMMENTED ARE THE CHANGES I NEED TO MAKE
		public static bool EmployeeAddsItemToRow(int rowIndex, ref int boxNumberProducts, int maxProductsPerRow) {
			//int num = __instance.productInfoArray[rowIndex];
			//int num2 = __instance.productInfoArray[rowIndex + 1];

			int numTransferItems = IncreasedItemTransferPatch.GetNumTransferItems(boxNumberProducts, num2, maxProductsPerRow);
			if (numTransferItems == 0) {
				return false;
			}
			num2 += numTransferItems;
			boxNumberProducts -= numTransferItems;

			//__instance.productInfoArray[rowIndex + 1] = num2;
			AchievementsManager.Instance.CmdAddAchievementPoint(1, num2);
			//RpcUpdateObjectOnClientsMethod.Value.Invoke(__instance, [rowIndex, num, num2]);
			return true;
		}
		*/

	}
}
