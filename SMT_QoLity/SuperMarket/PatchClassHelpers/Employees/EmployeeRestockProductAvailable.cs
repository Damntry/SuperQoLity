using System;
using System.Collections.Generic;
using Damntry.Utils.Logging;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees {

	//This is not a good idea since it breaks compatibility if the base game in the future, or a mod, sets 
	//	the value outside of my code, but fuck it, Im fed up with handling this productAvailableArray crap.
	/// <summary>
	/// Replacement of NPC_Info.productAvailableArray.
	/// Holds the values of the product available for restock assigned to each 
	///		employee, along with the target shelf and storage source.
	/// </summary>
	public static class EmployeeRestockJobInfo {

		private static Dictionary<NPC_Info, RestockJobInfo> npcRestockJobInfo = new();

		public static void SetRestockJobInfo(this NPC_Info npcInfo, RestockJobInfo jobInfo) {
			bool exist = npcRestockJobInfo.TryGetValue(npcInfo, out _);
			if (exist) {
				//Overwrite
				npcRestockJobInfo[npcInfo] = jobInfo;
			} else {
				npcRestockJobInfo.Add(npcInfo, jobInfo);
			}

			SetProductAvailableArray(npcInfo, jobInfo);
		}

		public static bool UpdateRestockJobInfo(this NPC_Info npcInfo, ProductShelfSlotInfo shelfSlotInfo, int maxProductsPerRow) {
			if (shelfSlotInfo == null) {
				TimeLogger.Logger.LogTimeFatal($"The parameter shelfSlotInfo is null for npc {npcInfo.netId}", 
					LogCategories.AI);
				return false;
			}

			bool exist = npcRestockJobInfo.TryGetValue(npcInfo, out RestockJobInfo jobInfo);
			if (!exist) {
				TimeLogger.Logger.LogTimeFatal($"An existing ProductAvailableInfo couldnt be found for npc {npcInfo.netId}",
					LogCategories.AI);
				return false;
			}

			//Cant change the ProductShelf indexes since they are used as hashcodes, so
			//	a new RestockJobInfo is created to substitute it.
			npcRestockJobInfo.Remove(npcInfo);
			RestockJobInfo jobInfoNew = new (shelfSlotInfo, jobInfo.Storage, jobInfo.MaxProductsPerRow);
			npcRestockJobInfo.Add(npcInfo, jobInfoNew);

			SetProductAvailableArray(npcInfo, jobInfoNew);

			return true;
		}

		private static void SetProductAvailableArray(this NPC_Info npcInfo, RestockJobInfo jobInfo) {

			ProductShelfSlotInfo prodShelf = jobInfo.ProdShelf;
			StorageSlotInfo storage = jobInfo.Storage;
			//Even though I ve replaced every existing use of productAvailableArray, set its
			//	values in case another mod or the base code needs them somewhere else in the future.
			npcInfo.productAvailableArray = [prodShelf.ShelfIndex, jobInfo.ShelfProdInfoIndex, storage.ShelfIndex,
				jobInfo.StorageProdInfoIndex, prodShelf.ExtraData.ProductId, storage.ExtraData.ProductId,
				prodShelf.SlotIndex, storage.SlotIndex, prodShelf.ExtraData.Quantity, storage.ExtraData.Quantity];
		}

		public static RestockJobInfo GetRestockJobInfo(this NPC_Info npcInfo) {
			bool exist = npcRestockJobInfo.TryGetValue(npcInfo, out RestockJobInfo jobInfo);
			if (!exist) {
				throw new InvalidOperationException($"The NPC with netId {npcInfo.netId} doesnt have any available product set yet.");
			}

			return jobInfo;
		}

	}
}
