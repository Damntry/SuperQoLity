using System;
using System.Collections.Generic;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking {

	public class StorageSlotInfo {

		public StorageSlotInfo(int storageIndex, int slotIndex, int productId, int quantity) {
			StorageIndex = storageIndex;
			SlotIndex = slotIndex;
			ExtraData.ProductId = productId;
			ExtraData.Quantity = quantity;
		}

		public StorageSlotInfo(int storageIndex, int slotIndex) {
			StorageIndex = storageIndex;
			SlotIndex = slotIndex;
		}

		public void SetValues(int storageIndex, int slotIndex, int productId, int Quantity) {
			StorageIndex = storageIndex;
			SlotIndex = slotIndex;
			ExtraData.ProductId = productId;
			ExtraData.Quantity = Quantity;
		}

		public void SetValues(StorageSlotInfo storageSlotInfo) {
			StorageIndex = storageSlotInfo.StorageIndex;
			SlotIndex = storageSlotInfo.SlotIndex;
			ExtraData.ProductId = storageSlotInfo.ExtraData.ProductId;
			ExtraData.Quantity = storageSlotInfo.ExtraData.ProductId;
		}


		public static StorageSlotInfo Default { get { return new StorageSlotInfo(-1, -1, -1, -1); } }


		public bool FreeStorageFound { get { return StorageIndex >= 0 && SlotIndex >= 0; } }

		public int StorageIndex { get; set; }

		public int SlotIndex { get; set; }

		public ExtraDataClass _extraData;

		public ExtraDataClass ExtraData {
			get {
				if (_extraData == null) {
					_extraData = new ExtraDataClass();
				}
				return _extraData;
			}
			set { _extraData = value; }
		}


		public class ExtraDataClass {
			public int ProductId { get; set; }

			public int Quantity { get; set; }

		}
	}

	/// <summary>
	/// Taken from https://stackoverflow.com/a/263416/739345 by Jon Skeet
	/// </summary>
	public class TargetContainerSlotComparer : IEqualityComparer<StorageSlotInfo> {

		public bool Equals(StorageSlotInfo o1, StorageSlotInfo o2) {
			return o1.StorageIndex == o2.StorageIndex && o1.SlotIndex == o2.SlotIndex;
		}

		public int GetHashCode(StorageSlotInfo o) {
			if (o == null) {
				throw new ArgumentNullException("The StorageSlotInfo object cant be null.");
			}

			unchecked { // Overflow is fine, just wrap
				int hash = 83;
				// Suitable nullity checks etc, of course :)
				hash = hash * 3323 + o.StorageIndex.GetHashCode();
				hash = hash * 3323 + o.SlotIndex.GetHashCode();
				return hash;
			}
		}
	}

}
