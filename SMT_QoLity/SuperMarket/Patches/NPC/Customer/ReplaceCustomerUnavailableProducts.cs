using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.Search;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.NPC.Customer {


    /// <summary>
    /// For manufactured items we dont need to do anything: 
    ///     - In-store NPC customers already only choose from shelf-assigned manufactured products
    ///     - Online orders never ask for manufactured items
    /// </summary>
    public class ReplaceCustomerUnavailableProducts : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableCustomerChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = 
            $"{MyPluginInfo.PLUGIN_NAME} - Customers ignores unlocked and unassigned products patch failed. Disabled";


		public override void OnPatchFinishedVirtual(bool IsActive) {
			if (IsActive) {
                StoreOpenStatusPatch.OnSupermarketOpenStateChanged += (IsOpen) => {
                    if (IsOpen) {
                        allowedProductIdList = new();
                        GenerateAllowedShoppingProductList();
                    }
                };
            }
		}

        /// <summary>List of products Ids assigned to any product shelf, when opening the store.</summary>
        private static HashSet<int> allowedProductIdList;


        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.GenerateCompensatedList))]
        [HarmonyPostfix]
        private static void AdjustCustomerShoppingListPatch(List<int> __result) {
            ReplaceNotAllowedProducts(__result);
        }

        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.AddExtraProductsFromReverseVending))]
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.AddExtraProductsFromSeason))]
        [HarmonyPostfix]
        private static void AdjustCustomerShoppingListPatch(NPC_Info npcInfo) {
            ReplaceNotAllowedProducts(npcInfo.productsIDToBuy);
        }


        
        private static void GenerateAllowedShoppingProductList() {
            //Search through all product shelves to generate a list of unique product ids that the store is selling.
            ContainerSearchLambdas.ForEachProductShelfSlotLambda(NPC_Manager.Instance, false, 
                (_, _, productId, _, _) => {

                    if (productId >= 0) {
                        allowedProductIdList.Add(productId);
                    }
                    
                    return ContainerSearchLambdas.LoopAction.Nothing;
                }
            );

            if (allowedProductIdList.Count == 0) {
                TimeLogger.Logger.LogWarningShowInGame("No products assigned to shelves. Customers will " +
                    "have the default shopping list", LogCategories.AI);
            }
        }

        private static void ReplaceNotAllowedProducts(List<int> productsIDToBuy) {
            if (!ModConfig.Instance.EnableShopListOnlyAssignedProducts.Value || allowedProductIdList.Count == 0) {
                return;
            }

            //The customer shopping list was generated. Now modify it to only include assigned products.
            for (int i = 0; i < productsIDToBuy.Count; i++) {
                if (!allowedProductIdList.Contains(productsIDToBuy[i])) {
                    //Replace non assigned product with a random allowed one
                    productsIDToBuy[i] = allowedProductIdList.ElementAt(Random.Range(0, allowedProductIdList.Count));
                }
            }
        }


        [HarmonyPatch(typeof(OrderPackaging), nameof(OrderPackaging.GenerateTodayListOfOrders))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AdjustOnlineOrdersShoppingListPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            CodeMatcher codeMatcher = new(instructions);

            ///C# old:
            ///   list.Sort()
            ///   ...
            ///C# new:
            ///   list.Sort()
            ///   ReplaceNotAllowedProducts(list)
            ///   ...

            MethodInfo mInfo = AccessTools.Method(typeof(List<int>), nameof(List<int>.Sort));

            codeMatcher.MatchForward(false,
                new CodeMatch(inst => inst.Calls(mInfo))
            );

            if (codeMatcher.IsInvalid) {
                throw new TranspilerDefaultMsgException("Couldnt find the call to method List<int>.Sort");
            }
            
            codeMatcher
                .Advance(1)
                //Add onto the stack the list that was just sorted, so its passed as first parameter to ReplaceNotAllowedProducts.
                .InsertAndAdvance(codeMatcher.InstructionAt(-2))
                .Insert(Transpilers.EmitDelegate(
                    ReplaceNotAllowedProducts
                ));
            
            return codeMatcher.InstructionEnumeration();
        }

    }

}
