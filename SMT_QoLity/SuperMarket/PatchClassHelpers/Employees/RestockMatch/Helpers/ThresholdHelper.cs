using System;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch
{
	/// <summary>
	/// Each numeric value corresponds to an index in the restockThresholds array, except "ShelfFull".
	/// </summary>
	public enum RestockPriority {
		Critical = 0,
		High = 1,
		Medium = 2,
		Low = 3,
		ShelfFull = 4
	}

	public class ThresholdHelper {

		//By default:  0.25f, 0.50f, 0.75f, 1f  -- NPC_Manager.Instance.productsThreshholdArray
		private static readonly float[] restockThresholds = [0.10f, 0.33f, 0.66f, 1f];

		public static readonly int ThresholdCount = restockThresholds.Length;

		public static readonly RestockPriority[] ThresholdEnumValues =
			(RestockPriority[])Enum.GetValues(typeof(RestockPriority));


		public static bool IsShelfNotFull(int prodQuantity, int maxProductsPerRow, out RestockPriority restockPriority) {
			if (prodQuantity < maxProductsPerRow) {
				for (int i = 0; i < ThresholdCount; i++) {
					if (prodQuantity < (int)(maxProductsPerRow * restockThresholds[i])) {
						restockPriority = (RestockPriority)i;
						return true;
					}
				}
			}

			restockPriority = RestockPriority.ShelfFull;
			return false;
		}

	}
}
