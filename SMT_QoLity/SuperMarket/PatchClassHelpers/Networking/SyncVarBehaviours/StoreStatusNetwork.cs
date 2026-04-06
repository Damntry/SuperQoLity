using Damntry.UtilsMirror.Components;
using Damntry.UtilsMirror.Attributes;
using Damntry.UtilsMirror.SyncVar;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class StoreStatusNetwork : SyncVarNetworkBehaviour<StoreStatusNetwork> {

        public static uint NetworkAssetId => 918219;


        [SyncVarNetwork]
		public static SyncVar<bool> IsStoreOpenOrCustomersInsideSync { get; private set; } = new(false);

	}
}
