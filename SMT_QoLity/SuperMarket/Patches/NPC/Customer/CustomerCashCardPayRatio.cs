using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.NPC.Customer {

    public class CustomerCashCardPayRatio : FullyAutoPatchedInstance {

        public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableCustomerChanges.Value;

        public override string ErrorMessageOnAutoPatchFail { get; protected set; } =
            $"{MyPluginInfo.PLUGIN_NAME} - Customer card pay ratio patch failed. Disabled.";


        [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.RpcShowPaymentMethod))]
        [HarmonyPrefix]
        public static void RpcShowPaymentMethodPatch(ref int index) {
            bool isCardPay = Random.Range(0, 100) < ModConfig.Instance.CustomerCardPayRatio.Value;
            index = isCardPay ? 1 : 0;
        }

    }
}
