using System;

namespace SuperQoLity.SuperMarket.ModUtils {

    [Flags]
    public enum SMT_Layers {
        None = -1,
        Default = 0,
        TransparentFX = 1,
        IgnoreRaycast = 2,
        OverlapLayer = 3,
        Water = 4,
        UI = 5,
        Player = 6,
        Interactable = 7,
    }

    public static class SMTLayers {


        public static readonly int RayCastGenericLayerMask =
            ConvertLayersToMask(SMT_Layers.Default, SMT_Layers.Water, SMT_Layers.Player);

        public static int ConvertLayersToMask(params SMT_Layers[] layers) {
            int layerMask = 0;
            foreach (var layer in layers) {
                layerMask |= 1 << (int)layer;
            }

            return layerMask;
        }

    }
}
