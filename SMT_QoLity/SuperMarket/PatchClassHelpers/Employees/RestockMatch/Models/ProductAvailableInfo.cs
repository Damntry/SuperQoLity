using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.ShelfSlotInfo;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models {

	public struct RestockJobInfo {

		public ProductShelfSlotInfo ProdShelf { get; private set; }

		public StorageSlotInfo Storage { get; private set; }


		public int ShelfProdInfoIndex {
			get {
				return ProdShelf.SlotIndex * 2;
			}
		}

		public int StorageProdInfoIndex {
			get {
				return Storage.SlotIndex * 2;
			}
		}

		public int MaxProductsPerRow { get; set; }


		public static RestockJobInfo Default { get; } = new RestockJobInfo();

		public RestockJobInfo() {
			ProdShelf = ProductShelfSlotInfo.Default;
			Storage = StorageSlotInfo.Default;
			MaxProductsPerRow = -1;
		}

		public RestockJobInfo(ProductShelfSlotInfo ProductShelf, StorageSlotInfo Storage, int MaxProductsPerRow) {
			ProdShelf = ProductShelf;
			this.Storage = Storage;
			this.MaxProductsPerRow = MaxProductsPerRow;
		}

		public void SetProductShelfExtraData(ProductShelfSlotInfo productShelf, int maxProductsPerRow) {
			ProdShelf.ExtraData = productShelf.ExtraData;
			MaxProductsPerRow = maxProductsPerRow;
		}

		public override string ToString() {
			return $"Product shelf: {ProdShelf} - Storage: {Storage} - MaxProductsPerRow: {MaxProductsPerRow}";
		}

	}
}
