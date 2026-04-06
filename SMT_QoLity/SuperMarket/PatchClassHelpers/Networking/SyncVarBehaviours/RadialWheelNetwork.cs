using Damntry.UtilsMirror.Attributes;
using Damntry.UtilsMirror.Components;
using Damntry.UtilsMirror.SyncVar;
using SuperQoLity.SuperMarket.ModUtils;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class RadialWheelNetwork : SyncVarNetworkBehaviour<RadialWheelNetwork> {

        public static uint NetworkAssetId => 798271;


        [SyncVarNetwork]
		public static BoolSyncVarSetting RadialEnabledSync { get; private set; }

        static RadialWheelNetwork() {
            //As a client, the radial will only work if the host has the mod wth this setting active too.
            RadialEnabledSync = new(defaultValue: false, ModConfig.Instance.EnableRadialWheelPatches);
        }

	}

}
