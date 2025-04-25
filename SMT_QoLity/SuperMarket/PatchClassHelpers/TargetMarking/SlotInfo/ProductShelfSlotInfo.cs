using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo {

	public class ProductShelfSlotInfo : SlotInfoBase {


		public ProductShelfSlotInfo(int shelfIndex, int slotIndex, int productId, int quantity, Vector3 position)
			: base(shelfIndex, slotIndex, productId, quantity, position) { }


		public ProductShelfSlotInfo(int shelfIndex, int slotIndex)
			: base(shelfIndex, slotIndex) { }


		public static ProductShelfSlotInfo Default { get { return new ProductShelfSlotInfo(-1, -1, -1, -1, Vector3.zero); } }


		public bool FreeProductShelfFound { get { return ShelfIndex >= 0 && SlotIndex >= 0; } }

	}

}
