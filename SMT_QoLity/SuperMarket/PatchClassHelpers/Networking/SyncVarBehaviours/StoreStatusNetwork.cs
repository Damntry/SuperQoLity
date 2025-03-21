using Damntry.UtilsBepInEx.MirrorNetwork.Components;
using Damntry.UtilsBepInEx.MirrorNetwork.Attributes;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class StoreStatusNetwork : SyncVarNetworkBehaviour<StoreStatusNetwork> {

		[SyncVarNetwork]
		public static SyncVar<bool> IsStoreOpenOrCustomersInsideSync { get; private set; } = new(false);

	}
}
