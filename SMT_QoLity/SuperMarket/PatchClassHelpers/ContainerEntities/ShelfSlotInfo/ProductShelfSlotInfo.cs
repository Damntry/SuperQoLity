using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo {

	public class ProductShelfSlotInfo : GenericShelfSlotInfo {


		public ProductShelfSlotInfo(int shelfIndex, int slotIndex, int productId, int quantity, Vector3 position)
			: base(shelfIndex, slotIndex, productId, quantity, position, ShelfType.ProdShelfSlot) { }


		public ProductShelfSlotInfo(int shelfIndex, int slotIndex)
			: base(shelfIndex, slotIndex, ShelfType.ProdShelfSlot) { }


		public static ProductShelfSlotInfo Default { get { return new ProductShelfSlotInfo(-1, -1, -1, -1, Vector3.zero); } }

	}

}
