using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch {

	public struct RestockJobInfo {

		public ProductShelfSlotInfo ProdShelf { get; set; }

		public StorageSlotInfo Storage { get; set; }

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
			this.ProdShelf = ProductShelf;
			this.Storage = Storage;
			this.MaxProductsPerRow = MaxProductsPerRow;
		}

	}
}
