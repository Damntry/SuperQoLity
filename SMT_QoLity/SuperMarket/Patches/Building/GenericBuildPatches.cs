using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using Damntry.UtilsUnity.Components.InputManagement;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.Standalone;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace SuperQoLity.SuperMarket.Patches.Building {
    public class GenericBuildPatches : FullyAutoPatchedInstance {

        public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableBuildPatches.Value;

        public override string ErrorMessageOnAutoPatchFail { get; protected set; } = 
            $"{MyPluginInfo.PLUGIN_NAME} - Generic building patches failed. Disabled.";


        public static readonly int VanillaPropLoadLimit = 5000;

        //Loading stops as soon as an index is not found, so this doesnt
        //  increase loading times unless you built above the vanilla limit.
        public static readonly int NewPropLoadLimit = 100000;


        [HarmonyPatch(typeof(NetworkSpawner), nameof(NetworkSpawner.LoadSpawnCoroutine), MethodType.Enumerator)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LoadSpawnIncreased(
                IEnumerable<CodeInstruction> instructions, ILGenerator generator) {

            if (!ModConfig.Instance.EnableIncreasedPropLoadLimits.Value) {
                return instructions;
            }

            CodeMatcher codeMatcher = new(instructions);

            //Exchange the original limiter value to a higher one so it can load more props.

            codeMatcher.MatchForward(false,
                new CodeMatch(inst => inst.LoadsConstant() &&
                    inst.operand is int operand && operand == VanillaPropLoadLimit)
            ).Repeat(
                (c) => c.SetOperandAndAdvance(NewPropLoadLimit),
                (e) => { throw new TranspilerDefaultMsgException("Vanilla buildable limiter not found. " +
                    "Its possible that the developer fixed this issue and this patch is not needed anymore. " +
                    $"Error was: {e}"); }
            );
            
            return codeMatcher.Instructions();
        }
    }
}
