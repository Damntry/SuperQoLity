using Damntry.UtilsBepInEx.MirrorNetwork.Components;
using Damntry.UtilsBepInEx.MirrorNetwork.Attributes;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;
using SuperQoLity.SuperMarket.Patches.TransferItemsModule;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class ItemTransferNetwork : SyncVarNetworkBehaviour<ItemTransferNetwork> {

		[SyncVarNetwork]
		public static SyncVarSetting<EnumItemTransferMode> ItemTransferModeSync { get; private set; }

		[SyncVarNetwork]
		public static SyncVarSetting<int> ItemTransferQuantitySync { get; private set; }


		static ItemTransferNetwork() {
			ItemTransferModeSync = new(EnumItemTransferMode.Disabled, ModConfig.Instance.ItemTransferMode);
			ItemTransferQuantitySync = new(EmployeeJobAIPatch.NumTransferItemsBase, ModConfig.Instance.NumTransferProducts);
		}

	}

}
