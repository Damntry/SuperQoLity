using Damntry.UtilsBepInEx.MirrorNetwork.Components;
using Damntry.UtilsBepInEx.MirrorNetwork.Attributes;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class StoreStatusNetwork : SyncVarNetworkBehaviour<StoreStatusNetwork> {

        public static uint NetworkAssetId => 918219;


        [SyncVarNetwork]
		public static SyncVar<bool> IsStoreOpenOrCustomersInsideSync { get; private set; } = new(false);

	}
}
