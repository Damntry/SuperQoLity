using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.ShelfSlotInfo;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models {

	public readonly record struct ShelfSlotData
			(int ShelfIndex, int SlotIndex, int ProductId, int Quantity, Data_Container DataContainer, Vector3 Position) {

		public ShelfSlotData(ShelfData shelfData, int slotIndex, int productId, int quantity) :
				this(shelfData.ShelfIndex, slotIndex, productId, quantity, shelfData.DataContainer, shelfData.Position) { }


		public StorageSlotInfo ToStorageSlotInfo() {
			return new StorageSlotInfo(ShelfIndex, SlotIndex, ProductId, Quantity, Position);
		}
		public ProductShelfSlotInfo ToProdShelfSlotInfo() {
			return new ProductShelfSlotInfo(ShelfIndex, SlotIndex, ProductId, Quantity, Position);
		}
	}

	public readonly record struct ShelfData
			(int ShelfIndex, int[] ProductInfoArray, Data_Container DataContainer, Vector3 Position);

	public readonly record struct ProductShelfInfo (ShelfSlotData ShelfSlotData, int MaxProductsPerRow);
}
