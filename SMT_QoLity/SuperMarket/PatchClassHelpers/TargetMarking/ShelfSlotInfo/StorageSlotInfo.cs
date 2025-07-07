using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.ShelfSlotInfo {

	public class StorageSlotInfo : GenericShelfSlotInfo {


		public StorageSlotInfo(int shelfIndex, int slotIndex, int productId, int quantity, Vector3 position) 
			: base(shelfIndex, slotIndex, productId, quantity, position, ShelfType.StorageSlot) { }

		public StorageSlotInfo(int shelfIndex, int slotIndex)
			: base(shelfIndex, slotIndex, ShelfType.StorageSlot) { }


		public static StorageSlotInfo Default { get { return new StorageSlotInfo(-1, -1, -1, -1, Vector3.zero); } }

	}

}
