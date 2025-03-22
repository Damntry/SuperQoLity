using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using Damntry.UtilsBepInEx.IL;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.Misc {

	/// <summary>
	/// Fixes the Pricing Gun showing a market value different than the calculated internal one.
	/// Refer to 0.8.2.0 changelog for a full explanation.
	/// </summary>
	public class PricingGunFixPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableMiscPatches.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Pricing Gun price patch failed. Disabled.";


		[HarmonyDebug]
		[HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.CustomerNPCControl))]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> CustomerNPCControlTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			///Old C#:
			///		component.productsIDToBuy.RemoveAt(0);
			///		if (num2 > num3)
			///New C#:
			///		component.productsIDToBuy.RemoveAt(0);
			///		if (num2 > GetRandomizedMarketPrice(num))

			CodeMatcher codeMatcher = new(instructions);

			MethodInfo removeAtMethod = typeof(List<int>).GetMethod(nameof(List<int>.RemoveAt));

			//Moves to the "component.productsIDToBuy.RemoveAt(0)" line.
			CodeMatcher MoveToRemoveAt(bool useEnd) =>
				codeMatcher.MatchForward(useEnd,
					new (inst => inst.IsStloc()),
					new (inst => inst.IsLdloc()),
					new (inst => inst.opcode == OpCodes.Ldfld),
					new (inst => inst.LoadsConstant()),
					new (inst => inst.Calls(removeAtMethod))
				);


			//Match beginning of "component.productsIDToBuy.RemoveAt(0)"
			if (MoveToRemoveAt(false).IsInvalid) {
				throw new TranspilerDefaultMsgException($"IL assigning customer max buy price " +
					$"before line \"component.productsIDToBuy.RemoveAt(0);\" could not be found.");
			}

			object num3_Operand = codeMatcher.Instruction.operand;

			
			codeMatcher.MatchBack(true,
				//Find previous "ret" OpCode which defines the end of the previous switch case
				new CodeMatch(inst => inst.opcode == OpCodes.Ret)
			).MatchForward(true,
				//Find the "num" var where the current productId is assigned
				new CodeMatch(inst => inst.IsStloc())
			);

			if (codeMatcher.IsInvalid) {
				throw new TranspilerDefaultMsgException($"IL finding \"num\" productId var could not be found.");
			}

			//Save "num" local index to load into the stack later
			int num_refIndex = codeMatcher.Instruction.LocalIndex();

			//Go back to "component.productsIDToBuy.RemoveAt(0)", but to its last line.
			MoveToRemoveAt(true)
				//Find where num3 is being loaded into the stack (if (num2 > num3))
				.MatchForward(true,
					new CodeMatch(inst => inst.IsLdloc() && inst.operand == num3_Operand)
				);

			if (codeMatcher.IsInvalid) {
				throw new TranspilerDefaultMsgException($"IL loading num3 value into the stack could not be found.");
			}

			//Replace num3 for num, as argument for GetRandomizedMarketPrice.
			codeMatcher.SetInstructionAndAdvance(CodeInstructionNew.LoadLocal(num_refIndex))
				.Insert(Transpilers.EmitDelegate(GetRandomizedMarketPrice));

			return codeMatcher.InstructionEnumeration();
		}

		private static float GetRandomizedMarketPrice(int productID) {
			Data_Product dataProd = ProductListing.Instance.productPrefabs[productID].GetComponent<Data_Product>();
			float marketPrice = dataProd.basePricePerUnit * ProductListing.Instance.tierInflation[dataProd.productTier];

			//Use as base the calculated market price shown in the pricing gun, instead of 
			//	directly applying the random factor to the raw calculated number like in vanilla.
			if (ModConfig.Instance.EnablePriceGunFix.Value) {
				marketPrice = RoundPrice(marketPrice);
			}
			
			/* Tests. Keep since they may change this in the future.
			float playerPrice = ProductListing.Instance.productPlayerPricing[productID];
			float customerPriceModded = shownMarketPrice * UnityEngine.Random.Range(2f, 2f);
			float customerPriceVanilla = marketPrice * UnityEngine.Random.Range(2f, 2f);
			bool passesTestModded = !(playerPrice > customerPriceModded);
			bool passesTestVanilla = !(playerPrice > customerPriceVanilla);
			string msg = $"Passes test with 200% random? {passesTestModded} - (playerPrice {playerPrice}, " +
				$"customerPriceModded {customerPriceModded}). Passed with old method? {passesTestVanilla} ";
			if (passesTestModded == passesTestVanilla) {
				LOG.TEMPDEBUG(msg);	//Vanilla and new method have the same result
			} else if (passesTestModded && !passesTestVanilla) {
				LOG.TEMPWARNING(msg); //New method fixes the bug
			} else {
				LOG.TEMPFATAL(msg); //New method causes the bug when it didnt before
			}
			*/

			return marketPrice * UnityEngine.Random.Range(2f, 2.5f);
		}

		private static float RoundPrice(float value) => Mathf.Round(value * 100f) / 100f;
	}

}
