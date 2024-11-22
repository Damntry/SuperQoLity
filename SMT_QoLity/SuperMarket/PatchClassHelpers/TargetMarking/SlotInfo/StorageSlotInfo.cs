
namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo {

	public class StorageSlotInfo : SlotInfoBase {


		public StorageSlotInfo(int shelfIndex, int slotIndex, int productId, int quantity) 
			: base(shelfIndex, slotIndex, productId, quantity) { }

		public StorageSlotInfo(int shelfIndex, int slotIndex)
			: base(shelfIndex, slotIndex) { }


		public static StorageSlotInfo Default { get { return new StorageSlotInfo(-1, -1, -1, -1); } }


		public bool FreeStorageFound { get { return ShelfIndex >= 0 && SlotIndex >= 0; } }

	}

}
