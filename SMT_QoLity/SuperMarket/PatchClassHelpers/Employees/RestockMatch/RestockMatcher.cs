using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Mirror;
using SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking.SlotInfo;
using SuperQoLity.SuperMarket.Patches;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch {

	public class RestockMatcher : NetworkBehaviour {

		//TODO 1 - On the biggest save, I lose an average of 6 fps (95 -> 89) with the employee module enabled.
		//	See wtf is that about.
		//	Not completely sure whats it about yet, but at least it seems that going from Disabled to Performance
		//	doesnt really affect the FPS. If anything I think I got 1 fps more.
		//	Then I tried the opposite, from disabled to balance, and now disabled is a couple fps faster.
		//	FPS is all over the place honestly, but yeah I think I need to keep checking and improving something.

		private readonly static int RestockProcessInterval = 3;

		private readonly static int RestockProcessMaxTimeMillis = 2;

		//TODO 0 Restock - Make this the max, and reduce based on how many queries are made every X seconds, and
		//	extrapolate from there. Its important that it counts all queries, since dequeuing an item doesnt
		//	mean its going to be valid.
		//Actually nevermind. Instead of being time based, convert it into a threshold.
		//	When it lowers to X, start generating, when it reaches Y, stop. The range will be small, about 10.
		//	The old jobs will never get cleared, instead there will be another periodic job that will look through
		//	the queue to discard invalid items. This discard together with npcs taking jobs, will deplete
		//	the queue and eventually trigger a regeneration.
		//Make it so the lower the number of jobs, the higher the search frequency, up to a point.

		private readonly static int MaxQueuedItems = 100;


		private static Queue<RestockJobInfo> listRestockJobs;

		private double lastTimeCheck;


		public static bool IsRestockGenerationWorking { get; private set; }


		public static void Enable() {
			if (Container<EmployeeJobAIPatch>.Instance.IsPatchActive) {
				WorldState.OnWorldStarted += () => {
					NPC_Manager.Instance.gameObject.AddComponent<RestockMatcher>();
				};
			}
		}

		public void Awake() {
			listRestockJobs = new();
			IsRestockGenerationWorking = false;
			lastTimeCheck = -1;
		}

		public void FixedUpdate() {
			if (IsRestockGenerationWorking) {
				return;
			}

			bool shouldRun = lastTimeCheck == -1 || lastTimeCheck + RestockProcessInterval < Time.fixedUnscaledTime;
			if (shouldRun) {
				GenerateAvailableRestockProducts(NPC_Manager.Instance).FireAndForget(LogCategories.JobSched);

				lastTimeCheck = Time.fixedUnscaledTime;
			}
		}

		private static bool HasRestockingEmployeAssigned(NPC_Manager __instance) {
			return __instance.employeeParentOBJ.transform
				.Cast<Transform>().Any(
					(t) => t.GetComponent<NPC_Info>().taskPriority == 2
			);
		}

		/// <summary>
		/// Generates a list of matches between product shelves, prioritizing emptier ones, and storage.
		/// </summary>
		public async Task GenerateAvailableRestockProducts(NPC_Manager __instance) {
			IsRestockGenerationWorking = true;
			int totalDelayCount = 0;
			try {
				listRestockJobs.Clear();
				if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0 ||
						!HasRestockingEmployeAssigned(__instance)) {
					return;
				}

				//TODO 1 Restock Optimization - Test this without using the lambdas to see how much time it saves.
				//	Out of curiosity more than anything else.
				List<ShelfInfo> listStorage = GenerateStorageList(__instance);
				List<ShelfInfo> listProdShelf = GenerateProductShelfList(__instance);

				//Performance.StopAndLog("LoopAllStorageAndShelves");

				//Performance.Start("2. productsThresholdArray");
				Stopwatch restockLimit = Stopwatch.StartNew();

				foreach (float productThreshold in NPC_Manager.Instance.productsThreshholdArray) {
					//Performance.Start("4. Shelves and storage loop");
					foreach (var prodShelf in listProdShelf) {
						if (prodShelf.ProductId < 0) {
							continue;
						}

						int maxProductsPerRow = -1;
						bool shouldBeRestocked = prodShelf.Quantity == 0;   //Cheap pre-check
						if (!shouldBeRestocked) {
							//Check if shelf product quantity is below current threshold.
							//Performance.Start("GetMaxProductsPerRow");
							maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
								__instance, prodShelf.ObjTransform.GetComponent<Data_Container>(),
								prodShelf.ProductId, prodShelf.ShelfIndex);
							//Performance.StopAndRecord("GetMaxProductsPerRow");

							int shelfQuantityThreshold = (int)(maxProductsPerRow * productThreshold);
							shouldBeRestocked = prodShelf.Quantity < shelfQuantityThreshold;
						}

						if (!shouldBeRestocked) {
							continue;
						}

						foreach (var storage in listStorage) {
							if (storage.ProductId >= 0 && storage.ProductId == prodShelf.ProductId && storage.Quantity > 0) {
								if (maxProductsPerRow == -1) {
									maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
										__instance, prodShelf.ObjTransform.GetComponent<Data_Container>(),
										prodShelf.ProductId, prodShelf.ShelfIndex);
								}
								//Used to be: productsPrioritySecondary.Add(new int[] { shelfIndex, shelfSlotIndex * 2, storageIndex, storageSlotIndex * 2, shelfProductId, storageProductId, shelfSlotIndex, storageSlotIndex, shelfQuantity, storageQuantity });
								listRestockJobs.Enqueue(
									new RestockJobInfo(
										prodShelf.ToProdShelfSlotInfo(), storage.ToStorageSlotInfo(), maxProductsPerRow
									)
								);

								if (listRestockJobs.Count >= MaxQueuedItems) {
									return;
								}
							}
						}
					}
					if (restockLimit.Elapsed.TotalMilliseconds > RestockProcessMaxTimeMillis) {
						totalDelayCount++;
						//Performance.Stop("2. productsThresholdArray", true);
						await Task.Delay(15);
						//Performance.Start("2. productsThresholdArray", true);
						restockLimit.Restart();
					}
				}
			} finally {
				IsRestockGenerationWorking = false;
				//Performance.StopAndLog("2. productsThresholdArray", true);
			}
		}

		private static List<ShelfInfo> GenerateProductShelfList(NPC_Manager __instance) {
			int prodShelfListSize = __instance.shelvesOBJ.transform.childCount * 5; //5 is a very rough estimate
			List<ShelfInfo> listProdShelf = new(prodShelfListSize);

			//TODO 1 Restock Optimization - I could "pre-mark" items with <25% or zero quantities, so they are prioritized or something.
			ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, false,
				(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {
					listProdShelf.Add(new ShelfInfo(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT));
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);
			return listProdShelf;
		}
		private static List<ShelfInfo> GenerateStorageList(NPC_Manager __instance) {
			int storageListSize = __instance.storageOBJ.transform.childCount * 8;
			List<ShelfInfo> listStorage = new(storageListSize);

			//TODO 1 Restock Optimization - Make this skip adding empty storage slots, storage slots with empty boxes
			//	I figure out its faster to do a bit more processing here, and save it for the
			//	heavier processing on the 2º part, since it has to be repeated for every threshold.
			ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, false,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

					listStorage.Add(new ShelfInfo(storageIndex, slotIndex, productId, quantity, storageObjT));
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);
			return listStorage;
		}

		public readonly record struct ShelfInfo
			(int ShelfIndex, int SlotIndex, int ProductId, int Quantity, Transform ObjTransform) {
		
			public StorageSlotInfo ToStorageSlotInfo() {
				return new StorageSlotInfo(ShelfIndex, SlotIndex, ProductId, Quantity, ObjTransform.position);
			}
			public ProductShelfSlotInfo ToProdShelfSlotInfo() {
				return new ProductShelfSlotInfo(ShelfIndex, SlotIndex, ProductId, Quantity, ObjTransform.position);
			}
		}


		public static bool GetAvailableRestockJob(NPC_Manager __instance, NPC_Info employee, out RestockJobInfo restockJob) {
			if (IsRestockGenerationWorking || listRestockJobs != null && listRestockJobs.Count == 0) {
				restockJob = RestockJobInfo.Default;
				return false;
			}

			//Performance.Start("GetAvailableRestockJob");
			bool existsRestockProduct;
			do {
				restockJob = listRestockJobs.Dequeue();

				//Check that both product shelf and storage slots are not in use by
				//	another employee, and that their contents are still valid.
				existsRestockProduct =
					TargetMatching.CheckAndUpdateTargetProductShelf(
						__instance, restockJob, restockJob.MaxProductsPerRow)
					&& TargetMatching.CheckAndUpdateTargetStorage(__instance, restockJob);

			} while (!existsRestockProduct && listRestockJobs.Count > 0);

			if (!existsRestockProduct) {
				restockJob = RestockJobInfo.Default;
			}

			//Performance.StopAndLog("GetAvailableRestockJob");
			return existsRestockProduct;
		}

		/*	Optimized CheckProductAvailability before changing to a background job.
		public static RestockJobInfo CheckProductAvailability(NPC_Manager __instance) {
			if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0) {
				return RestockJobInfo.Default;
			}

			//Performance.Start("LoopAllStorageAndShelves");
			int storageListSize = __instance.storageOBJ.transform.childCount * 8;
			List<(int storageIndex, int slotIndex, int productId, int quantity, Transform storageObjT)> 
				listStorage = new(storageListSize);
			int prodShelfListSize = __instance.shelvesOBJ.transform.childCount * 5;	//5 is a very rough estimate
			List<(int prodShelfIndex, int slotIndex, int productId, int quantity, Transform prodShelfObjT)> 
				listProdShelf = new(prodShelfListSize);

			ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, false,
				(storageIndex, slotIndex, productId, quantity, storageObjT) => {

					listStorage.Add((storageIndex, slotIndex, productId, quantity, storageObjT));
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);
			//I could "pre-mark" items with <25% or zero quantities, so they are prioritized or something.
			ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, false,
				(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {

					listProdShelf.Add((prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT));
					return ContainerSearchLambdas.LoopAction.Nothing;
				}
			);

			//Performance.StopAndLog("LoopAllStorageAndShelves");

			//Performance.Start("2. productsThresholdArray");
			List<RestockJobInfo> productsPriority = new();
			List<RestockJobInfo> productsPrioritySecondary = new();

			foreach (float productThreshold in NPC_Manager.Instance.productsThreshholdArray) {
				//Performance.Start("4. Shelves and storage loop");
				foreach (var prodShelf in listProdShelf) {
					//Check if this storage slot is already in use by another employee
					if (EmployeeTargetReservation.IsProductShelfSlotTargeted(prodShelf.prodShelfIndex, prodShelf.slotIndex)) {
						continue;
					}
					if (prodShelf.productId >= 0) {
						bool shouldBeRestocked = prodShelf.quantity == 0;	//Cheap pre-check
						if (!shouldBeRestocked) {
							//Check if shelf product quantity is below current threshold.
							//Performance.Start("GetMaxProductsPerRow");
							int maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
								__instance, prodShelf.prodShelfObjT.GetComponent<Data_Container>(), 
								prodShelf.productId, prodShelf.prodShelfIndex);
							//Performance.StopAndRecord("GetMaxProductsPerRow");

							int shelfQuantityThreshold = (int)(maxProductsPerRow * productThreshold);
							shouldBeRestocked = prodShelf.quantity < shelfQuantityThreshold;
						}

						productsPrioritySecondary.Clear();

						if (shouldBeRestocked) {
							foreach (var storage in listStorage) {
								if (EmployeeTargetReservation.IsStorageSlotTargeted(storage.storageIndex, storage.slotIndex)) {
									continue;
								}
								if (storage.productId >= 0 && storage.productId == prodShelf.productId && storage.quantity > 0) {
									//Used to be: productsPrioritySecondary.Add(new int[] { shelfIndex, shelfSlotIndex * 2, storageIndex, storageSlotIndex * 2, shelfProductId, storageProductId, shelfSlotIndex, storageSlotIndex, shelfQuantity, storageQuantity });
									productsPrioritySecondary.Add(
										new RestockJobInfo(prodShelf.prodShelfIndex, prodShelf.slotIndex,
										prodShelf.slotIndex * 2, prodShelf.productId, prodShelf.quantity,
										prodShelf.prodShelfObjT.position, storage.storageIndex,
										storage.slotIndex, storage.slotIndex * 2, storage.productId,
										storage.quantity, storage.storageObjT.position,
										prodShelf.prodShelfObjT.GetComponent<Data_Container>())
									);
								}
							}
						}
						if (productsPrioritySecondary.Count > 0) {
							productsPriority.Add(productsPrioritySecondary[UnityEngine.Random.Range(0, productsPrioritySecondary.Count)]);
						}
					}

					//	Though I guess it doesnt really matter?
					if (productsPriority.Count > 0) {
						break;
					}
				}
			}
			//Performance.StopAndLog("2. productsThresholdArray");
			if (productsPriority.Count > 0) {
				return productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
			}

			return RestockJobInfo.Default;
		}
		*/

		/*	Older, around 50% slower version.
		public static RestockJobInfo CheckProductAvailability(NPC_Manager __instance) {
			//Performance.Start("1. Basic var initialization");

			if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0) {
				return RestockJobInfo.Default;
			}

			List<RestockJobInfo> productsPriority = new();
			List<RestockJobInfo> productsPrioritySecondary = new();
			//Performance.StopAndRecord("1. Basic var initialization");
			Performance.Start("2. productsThresholdArray");

			foreach (float productThreshold in NPC_Manager.Instance.productsThreshholdArray) {
				//Performance.Start("4. Shelves and storage loop");
				for (int shelfIndex = 0; shelfIndex < __instance.shelvesOBJ.transform.childCount; shelfIndex++) {
					Transform shelfObjT = __instance.shelvesOBJ.transform.GetChild(shelfIndex);
					Data_Container maxProductsPerRow = shelfObjT.GetComponent<Data_Container>();
					int num = maxProductsPerRow.productInfoArray.Length / 2;
					//Performance.Start("5. Shelves slot and storage loop");
					for (int shelfSlotIndex = 0; shelfSlotIndex < num; shelfSlotIndex++) {
						//Check if this storage slot is already in use by another employee
						//Performance.Start("IsProductShelfSlotTargeted");
						if (EmployeeTargetReservation.IsProductShelfSlotTargeted(shelfIndex, shelfSlotIndex)) {
							continue;
						}
						//Performance.StopAndRecord("IsProductShelfSlotTargeted");
						int shelfProductId = maxProductsPerRow.productInfoArray[shelfSlotIndex * 2];
						if (shelfProductId >= 0) {

							int shelfQuantity = maxProductsPerRow.productInfoArray[shelfSlotIndex * 2 + 1];

							bool shouldBeRestocked = shelfQuantity == 0;
							if (!shouldBeRestocked) {
								//Check if shelf product quantity is below current threshold.
								//Performance.Start("GetMaxProductsPerRow");
								int maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
									__instance, maxProductsPerRow, shelfProductId, shelfIndex);
								//Performance.StopAndRecord("GetMaxProductsPerRow");

								int shelfQuantityThreshold = (int)(maxProductsPerRow * productThreshold);
								shouldBeRestocked = shelfQuantity < shelfQuantityThreshold;
							}
							
							productsPrioritySecondary.Clear();

							if (shouldBeRestocked) {
								//Performance.Start("7. Storage loop");
								for (int storageIndex = 0; storageIndex < __instance.storageOBJ.transform.childCount; storageIndex++) {
									Transform storageObjT = __instance.storageOBJ.transform.GetChild(storageIndex);
									int[] storageProductInfoArray = storageObjT.GetComponent<Data_Container>().productInfoArray;
									int num5 = storageProductInfoArray.Length / 2;
									//Performance.Start("8. Storage slot loop");
									for (int storageSlotIndex = 0; storageSlotIndex < num5; storageSlotIndex++) {
										//Check if this storage slot is already in use by another employee
										//Performance.Start("IsStorageSlotTargeted");
										if (EmployeeTargetReservation.IsStorageSlotTargeted(storageIndex, storageSlotIndex)) {
											continue;
										}
										//Performance.StopAndRecord("IsStorageSlotTargeted");
										int storageProductId = storageProductInfoArray[storageSlotIndex * 2];
										int storageQuantity = storageProductInfoArray[storageSlotIndex * 2 + 1];
										if (storageProductId >= 0 && storageProductId == shelfProductId && storageQuantity > 0) {
											//productsPrioritySecondary.Add(new int[] { shelfIndex, shelfSlotIndex * 2, storageIndex, storageSlotIndex * 2, shelfProductId, storageProductId, shelfSlotIndex, storageSlotIndex, shelfQuantity, storageQuantity });
											productsPrioritySecondary.Add(new RestockJobInfo(shelfIndex, shelfSlotIndex,
												shelfSlotIndex * 2, shelfProductId, shelfQuantity, shelfObjT.position, storageIndex, 
												storageSlotIndex, storageSlotIndex * 2, storageProductId, storageQuantity,
												storageObjT.position, maxProductsPerRow));
										}
									}
									//Performance.StopAndRecord("8. Storage slot loop");
								}
								//Performance.StopAndRecord("7. Storage loop");
							}
							if (productsPrioritySecondary.Count > 0) {
								productsPriority.Add(productsPrioritySecondary[UnityEngine.Random.Range(0, productsPrioritySecondary.Count)]);
							}
						}
					}
					//Performance.StopAndRecord("5. Shelves slot and storage loop");
					if (productsPriority.Count > 0) {
						break;
					}
				}
				//Performance.StopAndRecord("4. Shelves and storage loop");
			}
			Performance.StopAndLog("2. productsThresholdArray");

			if (productsPriority.Count > 0) {
				return productsPriority[UnityEngine.Random.Range(0, productsPriority.Count)];
			}

			return RestockJobInfo.Default;
			
		}
		*/

	}
}
