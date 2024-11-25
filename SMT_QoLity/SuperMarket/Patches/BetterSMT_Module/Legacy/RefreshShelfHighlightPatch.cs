using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;

namespace SuperQoLity.SuperMarket.Patches.BetterSMT_Module.Legacy {

	/// <summary>
	/// Legacy fix that adds a call to "highlightEffect.Refresh();" in the highlighting method, 
	/// so storage slots dont stop highlighting when its box is extracted from storage.
	///	Only used for BetterSMT versions <= 1.6.2
	/// </summary>
	public class RefreshShelfHighlightPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => BetterSMT_Helper.IsEnableBetterSMTHightlightFixes.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - BetterSMT storage highlight fix failed. Disabled";

		//[HarmonyDebug]
		[HarmonyPatchStringTypes($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch", "HighlightShelf")]
		[HarmonyBefore(BetterSMT_Helper.BetterSMTInfo.HarmonyId, BetterSMT_Helper.BetterSMTInfo.HarmonyId_New)]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> HighlightShelfTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			List<CodeInstruction> instrList = instructions.ToList();

			int indexGlow = instrList.FindLastIndex(instruction => instruction.opcode == OpCodes.Stfld && instruction.operand.ToString().Contains("glow"));

			if (indexGlow < 0) {
				throw new TranspilerDefaultMsgException("Couldnt find the instruction enabling the highlight effect.");
			}

			int indexLoadHlInstance = indexGlow + 1;
			CodeInstruction highlightEffectLoadInstance = instrList[indexLoadHlInstance];

			List<CodeInstruction> callRefreshInstr = new();

			//The line of code we want to add:  highlightEffect.Refresh()
			//Load highlightEffect instance
			callRefreshInstr.Add(highlightEffectLoadInstance);
			//Load "false" argument
			callRefreshInstr.Add(new CodeInstruction(OpCodes.Ldc_I4_0));
			//Call refresh method to consume both the instance and the argument
			CodeInstruction refreshCall = new CodeInstruction(OpCodes.Callvirt,
				AccessTools.Method(typeof(HighlightPlus.HighlightEffect), nameof(HighlightPlus.HighlightEffect.Refresh)));
			callRefreshInstr.Add(refreshCall);

			instrList.InsertRange(indexLoadHlInstance, callRefreshInstr);

			return instrList;
		}

	}
}
