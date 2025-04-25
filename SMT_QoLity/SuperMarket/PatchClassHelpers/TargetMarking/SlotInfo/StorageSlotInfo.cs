using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo {

	public class StorageSlotInfo : SlotInfoBase {


		public StorageSlotInfo(int shelfIndex, int slotIndex, int productId, int quantity, Vector3 position) 
			: base(shelfIndex, slotIndex, productId, quantity, position) { }

		public StorageSlotInfo(int shelfIndex, int slotIndex)
			: base(shelfIndex, slotIndex) { }


		public static StorageSlotInfo Default { get { return new StorageSlotInfo(-1, -1, -1, -1, Vector3.zero); } }


		public bool FreeStorageFound { get { return ShelfIndex >= 0 && SlotIndex >= 0; } }

	}

}
