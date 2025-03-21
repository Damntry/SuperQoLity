using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo {

	public abstract class SlotInfoBase {

		public SlotInfoBase(int shelfIndex, int slotIndex, int productId, int quantity, Vector3 position) {
			ShelfIndex = shelfIndex;
			SlotIndex = slotIndex;
			ExtraData.ProductId = productId;
			ExtraData.Quantity = quantity;
			ExtraData.Position = position;
		}

		public SlotInfoBase(int shelfIndex, int slotIndex) {
			ShelfIndex = shelfIndex;
			SlotIndex = slotIndex;
		}

		public void SetValues(int shelfIndex, int slotIndex, int productId, int Quantity, Vector3 Position) {
			ShelfIndex = shelfIndex;
			SlotIndex = slotIndex;
			ExtraData.ProductId = productId;
			ExtraData.Quantity = Quantity;
			ExtraData.Position = Position;
		}

		public void SetValues(SlotInfoBase SlotInfoBase) {
			ShelfIndex = SlotInfoBase.ShelfIndex;
			SlotIndex = SlotInfoBase.SlotIndex;
			ExtraData.ProductId = SlotInfoBase.ExtraData.ProductId;
			ExtraData.Quantity = SlotInfoBase.ExtraData.ProductId;
			ExtraData.Position = SlotInfoBase.ExtraData.Position;
		}


		public int ShelfIndex { get; set; }

		public int SlotIndex { get; set; }

		private ExtraDataClass _extraData;

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

			public Vector3 Position { get; set; }

		}

		public override string ToString() {
			return $"Shelf {this.ShelfIndex}, Slot {this.SlotIndex} (PID: {this.ExtraData.ProductId}, Amount: {this.ExtraData.Quantity}, Position: {this.ExtraData.Position})";
		}

	}


	public class TargetContainerSlotComparer : IEqualityComparer<SlotInfoBase> {

		public bool Equals(SlotInfoBase o1, SlotInfoBase o2) {
			return o1.ShelfIndex == o2.ShelfIndex && o1.SlotIndex == o2.SlotIndex;
		}

		/// <summary>
		/// Taken from https://stackoverflow.com/a/263416/739345 by Jon Skeet
		/// </summary>
		public int GetHashCode(SlotInfoBase o) {
			if (o == null) {
				throw new ArgumentNullException("The SlotInfoBase object cant be null.");
			}

			unchecked { // Overflow is fine, just wrap
				int hash = 83;

				hash = hash * 3323 + o.ShelfIndex.GetHashCode();
				hash = hash * 3323 + o.SlotIndex.GetHashCode();
				return hash;
			}
		}
	}

}
