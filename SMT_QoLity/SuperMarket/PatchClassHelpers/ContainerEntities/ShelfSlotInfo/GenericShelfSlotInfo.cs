using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo {

	//HACK Dont change the order in this enum, as it is the same fixed order 
	//	used in ContainerSearch.GetFirstOfAnyShelfWithProduct, and then 
	//	converted to int elsewhere for the the base game.
	//	The correct fix would be to save the values for the NPC, including the
	//	ShelfType enum value, and then use that send the appropiate int.
	public enum ShelfType {
		StorageSlot,
		ProdShelfSlot
	}

	public abstract class GenericShelfSlotInfo {

		public GenericShelfSlotInfo(int shelfIndex, int slotIndex, 
				int productId, int quantity, Vector3 position, ShelfType shelfType) {
			ShelfIndex = shelfIndex;
			SlotIndex = slotIndex;
			ShelfType = shelfType;
			ExtraData.ProductId = productId;
			ExtraData.Quantity = quantity;
			ExtraData.Position = position;
		}

		public GenericShelfSlotInfo(int shelfIndex, int slotIndex, ShelfType shelfType) {
			ShelfIndex = shelfIndex;
			SlotIndex = slotIndex;
			ShelfType = shelfType;
		}


		
		public void SetExtraDataValues(int shelfIndex, int slotIndex, int productId, int Quantity, Vector3 Position) {
			ExtraData.ProductId = productId;
			ExtraData.Quantity = Quantity;
			ExtraData.Position = Position;
		}

		public void SetExtraDataValues(GenericShelfSlotInfo SlotInfoBase) {
			ExtraData.ProductId = SlotInfoBase.ExtraData.ProductId;
			ExtraData.Quantity = SlotInfoBase.ExtraData.Quantity;
			ExtraData.Position = SlotInfoBase.ExtraData.Position;
		}

		/// <summary>
		/// The child index in either NPC_Manager.shelvesOBJ or NPC_Manager.storageOBJ 
		///		of the object that this GenericShelfSlotInfo references.
		///	It must be immutable as its value is used in hashcode calculation.
		/// </summary>
		public int ShelfIndex { get; init; }

		/// <summary>
		/// The index in the productInfoArray that references the specific space
		/// of a product in a shelf object.
		///	It must be immutable as its value is used in hashcode calculation.
		/// </summary>
		public int SlotIndex { get; init; }

		public ShelfType ShelfType { get; init; }


		public bool ShelfFound { get { return ShelfIndex >= 0 && SlotIndex >= 0; } }


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
			return $"Shelf {ShelfIndex}, Slot {SlotIndex} (ProdID: {ExtraData.ProductId}, Amount: {ExtraData.Quantity}, Position: {ExtraData.Position})";
		}

	}


	public class TargetContainerSlotComparer : IEqualityComparer<GenericShelfSlotInfo> {

		public bool Equals(GenericShelfSlotInfo o1, GenericShelfSlotInfo o2) {
			return o1.ShelfIndex == o2.ShelfIndex && o1.SlotIndex == o2.SlotIndex;
		}

		/// <summary>
		/// Taken from https://stackoverflow.com/a/263416/739345 by Jon Skeet
		/// </summary>
		public int GetHashCode(GenericShelfSlotInfo o) {
			if (o == null) {
				throw new ArgumentNullException("The GenericShelfSlotInfo object cant be null.");
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
