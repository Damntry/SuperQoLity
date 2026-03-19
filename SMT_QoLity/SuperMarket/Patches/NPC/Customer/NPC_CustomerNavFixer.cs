using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Customers;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.AI;

namespace SuperQoLity.SuperMarket.Patches.NPC.Customer {

    public class NPC_CustomerNavFixer : FullyAutoPatchedInstance {

        public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableCustomerChanges.Value;

        public override string ErrorMessageOnAutoPatchFail { get; protected set; } =
            $"{MyPluginInfo.PLUGIN_NAME} - Customer navigation fix patch failed. Disabled.";


        private static TrappedCustomerDetection trapDetection;

        //Vanilla default is a bit less than 4f
        private static readonly float MaxCustomerVelocityForJob = 0.33f;


        public override void OnPatchFinishedVirtual(bool IsActive) { 
            if (IsActive) {
                trapDetection = new TrappedCustomerDetection();

                WorldState.OnWorldLoaded += trapDetection.EnableDetection;
                WorldState.OnQuitOrMainMenu += trapDetection.DisableDetection;
            }
        }


        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.SpawnCustomerNPC))]
        [HarmonyPostfix]
        public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
            while (result.MoveNext()) {
                yield return result.Current;
            }

            BeginNpcNavDetection(NPC_Manager.Instance.customersnpcParentOBJ.transform.childCount - 1);
        }

        
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.CustomerNPCControl))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CustomerDestinationSetTranspile(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            CodeMatcher codeMatcher = new(instructions);

            ///Adds a call to BeginNpcNavDetection after a customer destination is set.
            ///C# old:
            ///   component2.destination = ...
            ///C# new:
            ///   component2.destination = ...
            ///   BeginNpcNavDetection(component);

            MethodInfo mInfo = AccessTools.PropertySetter(typeof(NavMeshAgent), nameof(NavMeshAgent.destination));

            codeMatcher.MatchForward(false,
                new CodeMatch(inst => inst.Calls(mInfo))
            ).Repeat(
                (c) => c.Advance(1)
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))   //Load first argument (customerIndex) onto the stack
                    .InsertAndAdvance(Transpilers.EmitDelegate(BeginNpcNavDetection)),
                (e) => {
                    throw new TranspilerDefaultMsgException($"No calls to 'typeof(NavMeshAgent)." +
                        $"{nameof(NavMeshAgent.destination)}' found." +
                    $"Error was: {e}");
                }
            );


            return codeMatcher.Instructions();
        }

        /// <summary>
        /// Transpiles CustomerNPCControl so the check that makes customers not being able to do work while
        /// their velocity is not zero, now instead checks they are not going above a certain speed threshold.
        /// NPCs in tight corridors keep pushing each other and they can never quite have zero speed, which
        /// combined with the above makes them never arrive to their destination even though they are close enough.
        /// </summary>
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.CustomerNPCControl))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AllowedVelocityForDestinationTranspile(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            CodeMatcher codeMatcher = new(instructions);

            ///C# old:
            ///   if (velocity.sqrMagnitude != 0f)
            ///C# new:
            ///   if (velocity.sqrMagnitude > MaxCustomerVelocityForJob)

            MethodInfo magnitudeGetter = AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.sqrMagnitude));

            Label? returnLabel = null;
            codeMatcher.MatchForward(true,
                new CodeMatch(inst => inst.Calls(magnitudeGetter)),
                new CodeMatch(inst => inst.LoadsConstant()),
                new CodeMatch(inst => inst.Branches(out returnLabel))
            );

            if (codeMatcher.IsValid && returnLabel.HasValue) {
                codeMatcher
                    //Change the logical comparison
                    .Set(OpCodes.Bge, returnLabel.Value)
                    .Advance(-1)
                    .Set(OpCodes.Ldc_R4, MaxCustomerVelocityForJob);
            } else {
                //Dont throw an exception since this is just a small helper and not necessarily needed for the rest to work.
                TimeLogger.Logger.LogError($"The call to property getter {nameof(Vector3.sqrMagnitude)} " +
                    $"could not be found.", LogCategories.AI);
            }

            return codeMatcher.Instructions();
        }


        //TODO 4 - This should be a transpile of CustomerNPCControl for performance and future proofing
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.CustomerNPCControl))]
        [HarmonyPrefix]
        public static void CustomerDestinationReachedPatch(NPC_Manager __instance, int NPCIndex) {
            GameObject gameObject = __instance.customersnpcParentOBJ.transform.GetChild(NPCIndex).gameObject;

            NPC_Info npcInfo = gameObject.GetComponent<NPC_Info>();
            NavMeshAgent navAgent = gameObject.GetComponent<NavMeshAgent>();
            
            if (trapDetection.IsNavDetectionActive(npcInfo) && (!TestNavAgent(navAgent) || IsCustomerAtDestination(npcInfo, navAgent))) {
                trapDetection.StopNpcNavDetection(npcInfo);
            }
        }

        private static void BeginNpcNavDetection(int npcIndex) {
            try {
                GameObject gameObject = NPC_Manager.Instance.customersnpcParentOBJ.transform.GetChild(npcIndex).gameObject;
                NPC_Info npcInfo = gameObject.GetComponent<NPC_Info>();
                if (npcInfo && npcInfo.isCustomer) {
                    trapDetection.BeginNpcNavDetection(npcInfo);
                }
            } catch (System.Exception ex) {
                TimeLogger.Logger.LogException(ex, LogCategories.AI);
                return;
            }
        }

        //Same check as vanilla
        private static bool IsCustomerAtDestination(NPC_Info npcInfo, NavMeshAgent navAgent) =>
            npcInfo.state >= 0 && !navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance && navAgent.velocity.magnitude == 0f;
        
        public static bool TestNavAgent(NavMeshAgent navAgent) {
            //TODO 7 - Unity error:  ["GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh]
            //  Its not from the nav fixes, it happens without it, but stops happening if I remove superqolity.
            //I debugged it and now Im more confused. I dont do any call to remainingDistance after me detecting
            //  that this error would happen. Maybe this kind of error only shows in the next frame?
            //  This has minimum priority in any case since it has no real impact if you dont watch the logs.
            //Hol' on. This happened too when I quit and I didnt even open the store yet, just had a single employee
            //  guy standing guard¿?
            if (!navAgent || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh) {
                return false;
            }
            return true;
        }


        /*
        [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.CustomerNPCControl))]
        [HarmonyPrefix]
        private static bool CustomerNPCControlTest(NPC_Manager __instance, int NPCIndex) {
            GameObject gameObject = __instance.customersnpcParentOBJ.transform.GetChild(NPCIndex).gameObject;
            NPC_Info component = gameObject.GetComponent<NPC_Info>();
            int state = component.state;
            NavMeshAgent component2 = gameObject.GetComponent<NavMeshAgent>();
            if (state == -1 || component2.pathPending || !(component2.remainingDistance <= component2.stoppingDistance)) {
                return false;
            }
            if (component2.hasPath) {
                float velocityMag = component2.velocity.sqrMagnitude;
                if (velocityMag > MaxCustomerVelocityForJob) {
                    return false;
                }
            }
            if (component.productsIDToBuy.Count > 0) {
                switch (state) {
                    case 0: {
                            LOG.TEMPWARNING($"NPCID {NPCIndex} Finding shelf with product.");
                            int productID = component.productsIDToBuy[0];
                            int num9 = __instance.WhichShelfHasItem(productID);
                            if (num9 == -1) {
                                GameData.Instance.AddNotFoundList(productID);
                                component.productsIDToBuy.RemoveAt(0);
                                component.RPCNotificationAboveHead("NPCmessage0", "product" + productID);
                                component.StartWaitState(1.5f, 0);
                                component.state = -1;
                            } else {
                                component.shelfThatHasTheItem = num9;
                                Vector3 position = (__instance.shelvesOBJ.transform.GetChild(num9).Find("Standspot")).transform.position;
                                component2.destination = position;
                                BeginNpcNavDetection(NPCIndex);
                                component.state = 1;
                            }
                            break;
                        }
                    case 1: {
                            int num3 = component.productsIDToBuy[0];
                            if (__instance.IsItemInShelf(component.shelfThatHasTheItem, num3)) {
                                LOG.TEMPWARNING($"NPCID {NPCIndex} Picking up product.");
                                float num4 = ProductListing.Instance.productPlayerPricing[num3];
                                Data_Product component4 = ProductListing.Instance.productPrefabs[num3].GetComponent<Data_Product>();
                                int productTier2 = component4.productTier;
                                float num5 = component4.basePricePerUnit * ProductListing.Instance.tierInflation[productTier2] * UnityEngine.Random.Range(2f, 2.5f);
                                component.productsIDToBuy.RemoveAt(0);
                                if (num4 > num5) {
                                    component.StartWaitState(1.5f, 0);
                                    component.RPCNotificationAboveHead("NPCmessage1", "product" + num3);
                                    GameData.Instance.AddExpensiveList(num3);
                                } else {
                                    if (ProductListing.Instance.productsIDOnSale.Count > 0 && ProductListing.Instance.productsIDOnSale.Contains(num3)) {
                                        for (int m = 0; m < ProductListing.Instance.productsIDOnSale.Count; m++) {
                                            if (ProductListing.Instance.productsIDOnSale[m] == num3 && m < ProductListing.Instance.productsSaleDiscount.Count) {
                                                int num6 = ProductListing.Instance.productsSaleDiscount[m];
                                                num4 = num4 * (float)(100 - num6) / 100f;
                                                break;
                                            }
                                        }
                                    }
                                    component.productsIDCarrying.Add(num3);
                                    component.productsCarryingPrice.Add(num4);
                                    component.numberOfProductsCarried++;
                                    component.StartWaitState(1.5f, 0);
                                    ((Component)__instance.shelvesOBJ.transform.GetChild(component.shelfThatHasTheItem)).GetComponent<Data_Container>().NPCGetsItemFromRow(num3);
                                }
                                component.state = -1;
                            } else {
                                component.state = 0;
                            }
                            break;
                        }
                    case 30: {
                            bool flag2 = false;
                            for (int n = 0; n < __instance.orderPickupPointsList.Count; n++) {
                                if (__instance.orderPickupPointsList[n]) {
                                    flag2 = true;
                                    break;
                                }
                            }
                            if (!flag2) {
                                component.state = -1;
                                component.StartWaitState(1.5f, 98);
                                component.productsIDToBuy.Clear();
                                component.selfcheckoutAssigned = true;
                                break;
                            }
                            bool flag3 = false;
                            for (int num7 = 0; num7 < __instance.orderPickupPointsList.Count; num7++) {
                                if (__instance.orderPickupPointsList[num7]) {
                                    continue;
                                }
                                int[] ordersNumbers2 = __instance.orderPickupPointsList[num7].GetComponent<OrderPickupPoint>().ordersNumbers;
                                for (int num8 = 0; num8 < ordersNumbers2.Length; num8++) {
                                    if (component.customerOrderNumber == ordersNumbers2[num8]) {
                                        flag3 = true;
                                        component.state = 31;
                                        component2.destination = (__instance.orderPickupPointsList[num7].transform.Find("Standspot")).transform.position;
                                        BeginNpcNavDetection(NPCIndex);
                                        break;
                                    }
                                }
                                if (flag3) {
                                    break;
                                }
                            }
                            if (!flag3) {
                                component.state = -1;
                                component.StartWaitState(1.5f, 30);
                            }
                            break;
                        }
                    case 31: {
                            bool flag = false;
                            for (int i = 0; i < __instance.orderPickupPointsList.Count; i++) {
                                if (!__instance.orderPickupPointsList[i]) {
                                    continue;
                                }
                                int[] ordersNumbers = __instance.orderPickupPointsList[i].GetComponent<OrderPickupPoint>().ordersNumbers;
                                for (int j = 0; j < ordersNumbers.Length; j++) {
                                    if (component.customerOrderNumber != ordersNumbers[j]) {
                                        continue;
                                    }
                                    flag = true;
                                    string[] array = __instance.orderPickupPointsList[i].GetComponent<OrderPickupPoint>().ordersItemData[j].Split('_');
                                    for (int k = 0; k < array.Length; k++) {
                                        int num = int.Parse(array[k]);
                                        for (int l = 0; l < component.productsIDToBuy.Count; l++) {
                                            if (num == component.productsIDToBuy[l]) {
                                                Data_Product component3 = ProductListing.Instance.productPrefabs[num].GetComponent<Data_Product>();
                                                int productTier = component3.productTier;
                                                float num2 = component3.basePricePerUnit * ProductListing.Instance.tierInflation[productTier] * UnityEngine.Random.Range(3.25f, 3.5f);
                                                component.customerOrderFinalPrice += num2;
                                                component.productsIDToBuy.RemoveAt(l);
                                                break;
                                            }
                                        }
                                    }
                                    __instance.orderPickupPointsList[i].GetComponent<OrderPickupPoint>().NPCRetrieveBox(j);
                                    if (component.productsIDToBuy.Count > 0) {
                                        component.state = -1;
                                        component.StartWaitState(1.5f, 30);
                                        break;
                                    }
                                    component.selfcheckoutAssigned = true;
                                    component.customerOrderFinalPrice = Mathf.Round(component.customerOrderFinalPrice * 100f) / 100f;
                                    AchievementsManager.Instance.CmdAddAchievementPoint(20, (int)component.customerOrderFinalPrice);
                                    GameData.Instance.CmdAlterFunds(component.customerOrderFinalPrice);
                                    component.state = -1;
                                    component.StartWaitState(1.5f, 98);
                                    break;
                                }
                                if (flag) {
                                    break;
                                }
                            }
                            break;
                        }
                    default:
                        break;
                }
                return false;
            }
            if (component.isAThief && state < 2) {
                component2.destination = (__instance.exitPoints.GetChild(UnityEngine.Random.Range(0, __instance.exitPoints.childCount))).transform.position;
                BeginNpcNavDetection(NPCIndex);
                component2.speed *= 1.25f;
                if (__instance.offensiveNPCs) {
                    component.RPCNotificationAboveHead("NPCmessage4", "");
                }
                component.RpcShowThief();
                component.thiefFleeing = true;
                component.thiefProductsNumber = component.productsIDCarrying.Count;
                component.StartWaitState(2f, 11);
                component.state = -1;
                return false;
            }
            if (component.productsIDCarrying.Count == 0 && state < 2) {
                component2.destination = (__instance.exitPoints.GetChild(UnityEngine.Random.Range(0, __instance.exitPoints.childCount))).transform.position;
                BeginNpcNavDetection(NPCIndex);
                component.RPCNotificationAboveHead("NPCmessage2", "");
                component.StartWaitState(2f, 10);
                component.state = -1;
                return false;
            }
            if (!component.selfcheckoutAssigned && __instance.selfCheckoutOBJ.transform.childCount > 0 && !component.isAThief) {
                int availableSelfCheckout = __instance.GetAvailableSelfCheckout(component);
                if (availableSelfCheckout > -1) {
                    component.selfcheckoutIndex = availableSelfCheckout;
                    ((Component)__instance.selfCheckoutOBJ.transform.GetChild(availableSelfCheckout)).GetComponent<Data_Container>().checkoutQueue[0] = true;
                }
                component.selfcheckoutAssigned = true;
            }
            if (component.selfcheckoutIndex > -1) {
                switch (state) {
                    case 0:
                    case 1:
                        component2.destination = ((Component)((Component)__instance.selfCheckoutOBJ.transform.GetChild(component.selfcheckoutIndex)).transform.Find("Standspot")).transform.position;
                        BeginNpcNavDetection(NPCIndex);
                        component.state = 2;
                        break;
                    case 2:
                        if (!component.isCurrentlySelfcheckouting) {
                            component.isCurrentlySelfcheckouting = true;
                            component.StartCustomerSelfCheckout(((Component)__instance.selfCheckoutOBJ.transform.GetChild(component.selfcheckoutIndex)).gameObject);
                        }
                        break;
                    case 3:
                        component.paidForItsBelongings = true;
                        GameData.Instance.dailyCustomers++;
                        AchievementsManager.Instance.CmdAddAchievementPoint(3, 1);
                        component2.destination = ((Component)__instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount - 1))).transform.position;
                        BeginNpcNavDetection(NPCIndex);
                        ((Component)__instance.selfCheckoutOBJ.transform.GetChild(component.selfcheckoutIndex)).GetComponent<Data_Container>().checkoutQueue[0] = false;
                        component.state = 99;
                        break;
                    case 99:
                        UnityEngine.Object.Destroy(gameObject);
                        break;
                    default:
                        break;
                }
                return false;
            }
            switch (state) {
                case 0:
                case 1: {
                        component.selfcheckoutAssigned = true;
                        int num10 = __instance.CheckForAFreeCheckout();
                        if (num10 == -1) {
                            component.isAThief = true;
                            component.RPCNotificationAboveHead("NPCmessage3", "");
                            component.StartWaitState(2f, 1);
                            component.state = -1;
                        } else {
                            LOG.TEMPWARNING($"NPCID {NPCIndex} Going to a checkout");
                            Transform val = ((Component)__instance.checkoutOBJ.transform.GetChild(num10)).transform.Find("QueueAssign");
                            component2.destination = val.position;
                            component.state = 2;
                        }
                        break;
                    }
                case 2: {
                        int num11 = __instance.CheckForAFreeCheckout();
                        if (num11 == -1) {
                            component.state = 1;
                            break;
                        }
                        int checkoutQueueNumber = __instance.GetCheckoutQueueNumber(num11);
                        component.currentCheckoutIndex = num11;
                        component.currentQueueNumber = checkoutQueueNumber;
                        Transform child = ((Component)((Component)__instance.checkoutOBJ.transform.GetChild(num11)).transform.Find("QueuePositions")).transform.GetChild(checkoutQueueNumber);
                        component2.destination = child.position;
                        BeginNpcNavDetection(NPCIndex);
                        component.state = 3;
                        break;
                    }
                case 3:
                    if (component.currentQueueNumber == 0) {
                        if (component.productsIDCarrying.Count == component.numberOfProductsCarried) {
                            ((Component)__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex)).GetComponent<Data_Container>().NetworkproductsLeft = component.numberOfProductsCarried;
                        }
                        if (component.productsIDCarrying.Count == 0) {
                            component.state = 4;
                        } else if (!component.placingProducts) {
                            component.PlaceProducts(__instance.checkoutOBJ);
                            component.placingProducts = true;
                        }
                    } else {
                        int num12 = component.currentQueueNumber - 1;
                        Data_Container component5 = ((Component)__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex)).GetComponent<Data_Container>();
                        if (!component5.checkoutQueue[num12]) {
                            component5.checkoutQueue[component.currentQueueNumber] = false;
                            component.currentQueueNumber = num12;
                            component5.checkoutQueue[component.currentQueueNumber] = true;
                            Transform child2 = ((Component)((Component)__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex)).transform.Find("QueuePositions")).transform.GetChild(component.currentQueueNumber);
                            component2.destination = child2.position;
                            BeginNpcNavDetection(NPCIndex);
                        }
                    }
                    break;
                case 4:
                    if (((Component)__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex)).GetComponent<Data_Container>().productsLeft == 0) {
                        component.state = 5;
                    }
                    break;
                case 5:
                    if (!component.alreadyGaveMoney) {
                        component.alreadyGaveMoney = true;
                        int index = UnityEngine.Random.Range(0, 2);
                        ((Component)__instance.checkoutOBJ.transform.GetChild(component.currentCheckoutIndex)).GetComponent<Data_Container>().RpcShowPaymentMethod(index);
                    }
                    break;
                case 10:
                    component.paidForItsBelongings = true;
                    GameData.Instance.dailyCustomers++;
                    AchievementsManager.Instance.CmdAddAchievementPoint(3, 1);
                    component2.destination = ((Component)__instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount))).transform.position;
                    BeginNpcNavDetection(NPCIndex);
                    component.state = 99;
                    break;
                case 11:
                    component2.destination = ((Component)((Component)(__instance).transform.Find("ThiefRoamSpots")).transform.GetChild(UnityEngine.Random.Range(0, ((Component)(__instance).transform.Find("ThiefRoamSpots")).transform.childCount - 1))).transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                    BeginNpcNavDetection(NPCIndex);
                    component.StartWaitState(1f, 12);
                    component.state = -1;
                    break;
                case 12:
                    component2.destination = ((Component)((Component)(__instance).transform.Find("ThiefRoamSpots")).transform.GetChild(UnityEngine.Random.Range(0, ((Component)(__instance).transform.Find("ThiefRoamSpots")).transform.childCount - 1))).transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                    BeginNpcNavDetection(NPCIndex);
                    component.StartWaitState(1f, 13);
                    component.state = -1;
                    break;
                case 13:
                    component2.destination = ((Component)__instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount))).transform.position;
                    BeginNpcNavDetection(NPCIndex);
                    component.state = 99;
                    break;
                case 98:
                    component.paidForItsBelongings = true;
                    component2.destination = ((Component)__instance.destroyPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, __instance.destroyPointsOBJ.transform.childCount))).transform.position;
                    BeginNpcNavDetection(NPCIndex);
                    component.state = 99;
                    break;
                case 99:
                    Object.Destroy(gameObject);
                    break;
                default:
                    break;
            }
            return false;
        }
        */

    }
}
