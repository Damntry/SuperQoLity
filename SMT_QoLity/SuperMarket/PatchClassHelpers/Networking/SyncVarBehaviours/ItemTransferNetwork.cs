using Damntry.UtilsBepInEx.MirrorNetwork.Components;
using Damntry.UtilsBepInEx.MirrorNetwork.Attributes;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class ItemTransferNetwork : SyncVarNetworkBehaviour<ItemTransferNetwork> {

		public static uint NetworkAssetId => 871623;


        [SyncVarNetwork]
		public static SyncVarSetting<EnumItemTransferMode> ItemTransferModeSync { get; private set; }

		[SyncVarNetwork]
		public static SyncVarSetting<int> ItemTransferQuantitySync { get; private set; }


		static ItemTransferNetwork() {
			ItemTransferModeSync = new(EnumItemTransferMode.Disabled, ModConfig.Instance.ItemTransferMode);
			ItemTransferQuantitySync = new(EmployeeJobAIPatch.NumTransferItemsBase, ModConfig.Instance.NumTransferProducts);
		}

		/* TODO 1 Network - RPC TEST
		protected override void OnSyncVarsNetworkReady() {
			base.OnSyncVarsNetworkReady();

			LOG.TEMPWARNING("OnSyncVarsNetworkReady: Going to call doSomethingRPC");

			doSomethingRPC(4);
		}

		
		[RPC_CallOnClient(typeof(ItemTransferNetwork), nameof(GeGameObjectReference))]
		private void doSomethingRPC(int paramTest) {
			LOG.TEMPWARNING($"Original doSomethingRPC method called with param {paramTest}");
		}

		private void GeGameObjectReference(int paramTest) {
			LOG.TEMPWARNING($"Target GeGameObjectReference method called with param {paramTest}");
		}
		*/
	}

}
