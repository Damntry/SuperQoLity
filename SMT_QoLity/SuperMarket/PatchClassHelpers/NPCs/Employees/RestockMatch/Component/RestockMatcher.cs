using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Tasks;
using Damntry.Utils.Tasks.AsyncDelay;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using HarmonyLib;
using Mirror;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.Search;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities.ShelfSlotInfo;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Helpers;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.Patches;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Component {

	public class RestockMatcher : NetworkBehaviour {

		private double lastTimeCheck;

		private CancellableSingleTask<AsyncDelay> restockJobGen;

		private Task restockTask;

		private static float currentRestockProcessInterval;


		public static void Enable() {
			if (Container<EmployeeJobAIPatch>.Instance.IsPatchActive) {
				WorldState.OnWorldStarted += () => {
					NPC_Manager.Instance.gameObject.AddComponent<RestockMatcher>();
					RestockJobsManager.Initialize();
				};
			}
		}


		public void Awake() {
			restockJobGen = new(logEvents: false);
			lastTimeCheck = -1;
			currentRestockProcessInterval = RestockJobsManager.RestockProcessNotPossibleInterval;
		}

		public void FixedUpdate() {
			bool shouldRun = restockTask == null || restockTask.IsTaskEnded() &&
				(lastTimeCheck == -1 || lastTimeCheck + currentRestockProcessInterval < Time.fixedUnscaledTime);
			if (shouldRun) {
				if (IsRestockPossible(NPC_Manager.Instance)) {
					GenerateAvailableRestockProducts(NPC_Manager.Instance);

					currentRestockProcessInterval = RestockJobsManager.RestockProcessInterval;
				} else {
					//Make faster checks so the player doesnt have to wait so much once conditions allow restocking.
					currentRestockProcessInterval = RestockJobsManager.RestockProcessNotPossibleInterval;
				}

				lastTimeCheck = Time.fixedUnscaledTime;
			}
		}

		private bool IsRestockPossible(NPC_Manager __instance) {
			return __instance.storageOBJ.transform.childCount > 0 &&
				__instance.shelvesOBJ.transform.childCount > 0 &&
				EmployeeJobAIPatch.HasEmployeAssignedTo(EmployeeJob.Restocker);
		}


		public void GenerateAvailableRestockProducts(NPC_Manager __instance) {
			//If we reach this point the task has ended, but if there was an exception it would be still waiting to
			// throw it. We dont want any errors of the previous run to ruin the current one,
			//	so we just await and catch any exceptions without blocking.
			//	Hopefully, whatever started failing doesnt happen every time.
			restockTask?.FireAndForgetCancels(LogCategories.JobSched);

			//Performance.Start("0. GenerateInitialCollections");
			List<ShelfData> listStorageShelf = GetInitialShelfList(__instance, ShelfType.StorageSlot);
			List<ShelfData> listProdShelf = GetInitialShelfList(__instance, ShelfType.ProdShelfSlot);
			//Performance.StopAndLog("0. GenerateInitialCollections");

			LOG.TEMPDEBUG_FUNC(() => $"{RestockJobsManager.JobCount} jobs are going to be cleared.", EmployeeJobAIPatch.LogEmployeeActions);
			RestockJobsManager.ClearJobs();
			

			int maxJobsRestockCycle = CalculateMaxQueuedJobsForCycle(__instance);

			restockTask = restockJobGen.StartAwaitableThreadedTaskAsync(
				() => RestockJobGeneration(maxJobsRestockCycle, listStorageShelf, listProdShelf),
				"Restock job generation",
				false
			);
		}

		//This is a piece of shit. I should redo how I calculate this some day in the far far future.
		private int CalculateMaxQueuedJobsForCycle(NPC_Manager __instance) {
			int restockEmployeeCount = EmployeeJobAIPatch.GetEmployeeCount(EmployeeJob.Restocker);
			float speedFactor = Time.timeScale *	//Affects the clock star perk too.
					(EmployeeWalkSpeedPatch.IsWarpingEnabled() ? 15f : EmployeeWalkSpeedPatch.WalkSpeedMultiplier);

			return (int)Math.Ceiling(RestockJobsManager.StaticQueuedJobsPerEmployee * restockEmployeeCount) +
				(int)Math.Ceiling(RestockJobsManager.DynamicQueuedJobsPerEmployee * restockEmployeeCount * speedFactor) + 
				RestockJobsManager.ExtraQueuedJobsBuffer;
		}

		private static Task RestockJobGeneration(int maxJobsRestockCycle,
				List<ShelfData> listStorageShelf, List<ShelfData> listProdShelf) {

			int jobsPerPriority = (int)Math.Ceiling(maxJobsRestockCycle * RestockJobsManager.NonCriticalJobsPerPriorityMultiplier);
			RestockJob<ProductShelfInfo> possibleRestockJobs = new();

			//Performance.Start("2. GenerateStorageSlotDictionary");
			Dictionary<int, List<ShelfSlotData>> dictStorageSlot = GenerateStorageSlotDictionary(listStorageShelf);
			//Performance.StopAndLog("2. GenerateStorageSlotDictionary");

			//Performance.Start("3. productsThresholdArray (minus GenerateStorageSlotDictionary)");

			foreach (ShelfData prodShelfData in listProdShelf) {
				foreach (ShelfSlotData prodShelfSlotData in GetProductShelfSlotList(prodShelfData)) {
					if (prodShelfSlotData.ProductId < 0) {
						continue;
					}

					int maxProductsPerRow = -1;
					RestockPriority restockPriority = prodShelfSlotData.Quantity == 0 ? RestockPriority.Critical : RestockPriority.ShelfFull;

					bool shouldBeRestocked = prodShelfSlotData.Quantity == 0;   //Cheap pre-check
					if (!shouldBeRestocked) {
						//Check if shelf product quantity is below current priority priority.
						maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCachedThreaded(
							prodShelfSlotData.DataContainer,
							prodShelfSlotData.ProductId, prodShelfSlotData.ShelfIndex);

						shouldBeRestocked = ThresholdHelper.IsShelfNotFull(prodShelfSlotData.Quantity, maxProductsPerRow,
							out restockPriority);
					}

					if (restockPriority != RestockPriority.Critical) {

						if (restockPriority != RestockPriority.ShelfFull) {
							//Save shelf to be processed later as a job. It wont get added if there are enough jobs for this restockPriority.
							//Performance.Start("PossibleJobs");
							possibleRestockJobs.TryAddJob(restockPriority, new ProductShelfInfo(prodShelfSlotData, maxProductsPerRow));
							//Performance.Stop("PossibleJobs");
						}
						continue;
					}

					//Find storage shelf from where to restock
					if (GetStorageForProdShelf(dictStorageSlot, prodShelfSlotData, 
							maxProductsPerRow, out List<ShelfSlotData> listNonEmptyStorageSlots)) {

						AddAvailableJobs(listNonEmptyStorageSlots, maxJobsRestockCycle, 
							restockPriority, prodShelfSlotData, maxProductsPerRow);
					}

					if (RestockJobsManager.JobCount >= maxJobsRestockCycle) {
						break;
					}
				}
			}

			//If there is still space left to fill the job queue, process the saved jobs that had a lower priority
			while (RestockJobsManager.JobCount < maxJobsRestockCycle && possibleRestockJobs.HasJobsLeft &&
					possibleRestockJobs.TryExtractPriorityJob(out ProductShelfInfo prodShelf, out RestockPriority priority)) {

				//Find storage shelf from where to restock
				if (GetStorageForProdShelf(dictStorageSlot, prodShelf.ShelfSlotData,
						prodShelf.MaxProductsPerRow, out List<ShelfSlotData> listNonEmptyStorageSlots)) {

					AddAvailableJobs(listNonEmptyStorageSlots, maxJobsRestockCycle,
						priority, prodShelf.ShelfSlotData, prodShelf.MaxProductsPerRow);
				}
			}

			//Performance.StopAndLog("3. productsThresholdArray (minus GenerateStorageSlotDictionary)");
			//Performance.StopAndLog("PossibleJobs");
			LOG.TEMPDEBUG_FUNC(() => $"{RestockJobsManager.JobCount} jobs available after generation.", EmployeeJobAIPatch.LogEmployeeActions);

			return Task.CompletedTask;
		}


		private static bool GetStorageForProdShelf(Dictionary<int, List<ShelfSlotData>> dictStorageSlot, 
				ShelfSlotData prodShelfSlotData, int maxProductsPerRow, out List<ShelfSlotData> listNonEmptyStorageSlots) {

			//Get storage shelfs with this productId
			if (!dictStorageSlot.TryGetValue(prodShelfSlotData.ProductId, out listNonEmptyStorageSlots)) {
				return false;
			}

			if (maxProductsPerRow == -1) {
				maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCachedThreaded(
					prodShelfSlotData.DataContainer,
					prodShelfSlotData.ProductId, prodShelfSlotData.ShelfIndex);
			}

			//Sort based on a calculation of box quantity and distance from product shelf.
			int maxProductsBox = ProductListing.Instance.productPrefabs[prodShelfSlotData.ProductId].GetComponent<Data_Product>().maxItemsPerBox;
			listNonEmptyStorageSlots.Sort(new RestockStorageComparer(prodShelfSlotData.Quantity, 
				maxProductsPerRow, prodShelfSlotData.Position, maxProductsBox));

			return true;
		}

		private static void AddAvailableJobs(List<ShelfSlotData> listNonEmptyStorageSlots, int maxJobsRestockCycle,
				RestockPriority restockPriority, ShelfSlotData prodShelfSlotData, int maxProductsPerRow) {

			if (maxProductsPerRow == -1) {
				maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCachedThreaded(
					prodShelfSlotData.DataContainer,
					prodShelfSlotData.ProductId, prodShelfSlotData.ShelfIndex);
			}

			int jobsLeft = maxJobsRestockCycle - RestockJobsManager.JobCount;
			int maxStorageCountToAdd = Math.Min(RestockJobsManager.MaxStorageJobsPerShelf, jobsLeft);
			for (int i = 0; i < maxStorageCountToAdd; i++) {
				//Used to be: productsPrioritySecondary.Add(new int[] { shelfIndex, shelfSlotIndex * 2, storageIndex, storageSlotIndex * 2, shelfProductId, storageProductId, shelfSlotIndex, storageSlotIndex, shelfQuantity, storageQuantity });
				RestockJobsManager.AddAvailableJob(restockPriority, prodShelfSlotData, listNonEmptyStorageSlots[i], maxProductsPerRow);
			}
		}

		private class RestockStorageComparer : IComparer<ShelfSlotData> {

			
			private Vector3 shelfPosition;

			private int prodShelfLeftToFillCount;

			private bool compareAgainstShelfContents;


			private AnimationCurve quantVsDistPriorityThreshold;

			private static readonly float quantityPriorityPercent = 0.85f;


			public RestockStorageComparer(int prodShelfQuantity, 
					int maxProductsPerRow, Vector3 shelfPosition, int boxMaxProductCount) {

				this.shelfPosition = shelfPosition;
				this.prodShelfLeftToFillCount = maxProductsPerRow - prodShelfQuantity;
				//No need to compare whats left on the shelf, when a box max capacity is already lower than necessary.
				compareAgainstShelfContents = prodShelfLeftToFillCount <= boxMaxProductCount;

				quantVsDistPriorityThreshold = new();
				//This curve (a line in this case) is used as a way to prioritize box quantity over shelf distance.
				//	For each Key (quantity difference) there is a corresponding value (shelf distance difference)
				//	that acts as a threshold. So for a given quantity difference, if the difference between shelf
				//	distances is lower than the threshold, quantity is used to compare, otherwise, distance will be used.

				//The "time" key is the box quantity difference. A value of 0 means no difference, and a value
				//	of <boxMaxProductCount> means one box is full and the other empty.
				//	The "value" threshold is how much closer one storage shelf must be to the product shelf, compared
				//	to the other storage shelf to the same product shelf.
				//	A value of 1 means same distance. The value 0 cant exist since it would mean an infinite
				//	distance difference, but, for example, 0.10 would mean that one storage has to be as least 10 times
				//	closer to the product shelf than the other, to be considered better at that "time" (quant. diff.) point.

				//The curve is made in such a way that differences in box product quantity are more important than distance 
				//	relative differences. This is because short trips might be quick, but a less filled box could mean
				//	progressively longer trips, while having boxes with more content increases the probability of less trips,
				//	which generally reduces total walk distance.
				//	In any case, there is logic so if 1 trip is enough to fill the shelf, the closer one is used.

				//Automatically set a number of points in the curve
				const int maxLoops = 10;
				const float loopAdd = 1f / maxLoops;

				float loopKey;
				float threshold;
				for (int i = 0; i <= maxLoops; i++) {
					loopKey = i * loopAdd;
					threshold = (1 - loopKey) * quantityPriorityPercent;
					quantVsDistPriorityThreshold.AddKey(boxMaxProductCount * loopKey, threshold);
				}
			}


			public int Compare(ShelfSlotData x, ShelfSlotData y) {
				if (compareAgainstShelfContents) {
					//Prioritize the box that can fill the product shelf. If both can, get the closest one to the product shelf.
					if (prodShelfLeftToFillCount > x.Quantity && prodShelfLeftToFillCount <= y.Quantity) {
						return 1;
					} else if (prodShelfLeftToFillCount < x.Quantity && prodShelfLeftToFillCount >= y.Quantity) {
						return 0;
					} else if (prodShelfLeftToFillCount <= x.Quantity && prodShelfLeftToFillCount <= y.Quantity) {
						return CompareShelfDistance(x, y);
					}
				}

				//None of the boxes had enough to fill the shelf. Prioritize based on quantity and distance differences.

				if (x.Quantity == y.Quantity) {
					//Fast return to avoid expensive calculations.
					return CompareShelfDistance(x, y);
				}

				//Find the precalculated threshold at which shelf distance difference
				//	has more priority than the current quantity difference.
				float boxQuantityDiff = Math.Abs(x.Quantity - y.Quantity);
				float distDiffPriorityThreshold = quantVsDistPriorityThreshold.Evaluate(boxQuantityDiff);

				//Get relative distance difference.
				float xShelfDistance = (x.Position - shelfPosition).sqrMagnitude;
				float yShelfDistance = (y.Position - shelfPosition).sqrMagnitude;
				float distRelativeDiff = xShelfDistance < yShelfDistance ? 
					xShelfDistance / yShelfDistance : yShelfDistance / xShelfDistance;

				//Compare distance against threshold
				if (distRelativeDiff < distDiffPriorityThreshold) {
					return CompareShelfDistance(x, y);
				} else {
					if (x.Quantity <= y.Quantity) {
						return 1;
					} else {
						return -1;
					}
				}
			}

			private int CompareShelfDistance(ShelfSlotData x, ShelfSlotData y) {
				return (x.Position - shelfPosition).sqrMagnitude > (y.Position - shelfPosition).sqrMagnitude ? 1 : -1;
			}

		}

		/// <summary>
		/// Gets from shelves the bare-minimum data that needs access to the Unity API.
		/// </summary>
		private static List<ShelfData> GetInitialShelfList(NPC_Manager __instance, ShelfType shelfType) {
			int prodShelfListSize = __instance.shelvesOBJ.transform.childCount * 5; //5 is a very rough estimate
			List<ShelfData> listProdShelf = new(prodShelfListSize);

			ContainerSearchLambdas.ForEachShelfLambda(__instance, shelfType,
				(shelfIndex, productInfoArray, dataContainer, position) => {
					listProdShelf.Add(new ShelfData(shelfIndex, productInfoArray, dataContainer, position));
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);
			LOG.TEMPDEBUG_FUNC(() => $"{listProdShelf.Count} initial shelfs of type " +
				$"{shelfType} obtained", EmployeeJobAIPatch.LogEmployeeActions);
			return listProdShelf;
		}

		/// <summary>
		/// Generates the complete list of product slot data from the product shelfs passed by parameter.
		/// This method does not access the Unity API and can be run outside the Unity main thread.
		/// </summary>
		private static List<ShelfSlotData> GetProductShelfSlotList(ShelfData productShelfData) {
			return ContainerSearchLambdas.GetShelfSlotsFromShelfData(
				productShelfData, ShelfType.ProdShelfSlot, ShelfSearchOptions.None)
				.ToList();
		}

		/// <summary>
		/// Generates the complete dictionary of storage slot data from the shelf passed by parameter.
		/// This method does not access the Unity API and can be run outside the Unity main thread.
		/// </summary>
		private static Dictionary<int, List<ShelfSlotData>> GenerateStorageSlotDictionary(List<ShelfData> listStorageShelfData) {
			Dictionary<int, List<ShelfSlotData>> dictStorage = new();

			foreach (ShelfData storageShelfData in listStorageShelfData) {
				IEnumerable<ShelfSlotData> shelfSlotEnumerable = ContainerSearchLambdas.GetShelfSlotsFromShelfData(
					storageShelfData, ShelfType.StorageSlot, ShelfSearchOptions.SkipEmptySlots);

				foreach (ShelfSlotData slotData in shelfSlotEnumerable) {
					if (!dictStorage.TryGetValue(slotData.ProductId, out List<ShelfSlotData> list)) {
						dictStorage.Add(slotData.ProductId, list = new());
					}
					list.Add(slotData);
				}
			}

			return dictStorage;
		}

	}
}
