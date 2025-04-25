using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Damntry.Utils.Logging;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.EntitySearch;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.Patches;
using UnityEngine;
using System.Collections.Concurrent;
using System.Threading.Tasks;

//For the memories I guess.
public class AncientRestockLogic {

	//This is the current vanilla game restock method
	public static void MainRestockUpdate() {
		NPC_Manager instance = NPC_Manager.Instance;
		Performance.Start("MainRestockUpdate");
		List<int> auxiliarOrderingList = new List<int>();
		int performanceCounter = 0;
		int numberOfRestockingEmployees = instance.GetNumberOfRestockingEmployees();
		while (instance.lowProductCountList.Count + instance.mediumProductCountList.Count + instance.highProductCountList.Count + instance.veryHighProductCountList.Count <= numberOfRestockingEmployees) {
			numberOfRestockingEmployees = instance.GetNumberOfRestockingEmployees();
			numberOfRestockingEmployees = 10;	//Forced to generate at least 10
			for (int p = 0; p < instance.productsThreshholdArray.Length; p++) {
				for (int i = 0; i < instance.shelvesOBJ.transform.childCount; i++) {
					int[] productInfoArray = instance.shelvesOBJ.transform.GetChild(i).GetComponent<Data_Container>().productInfoArray;
					int num = productInfoArray.Length / 2;
					for (int j = 0; j < num; j++) {
						instance.auxiliarProductList.Clear();
						int num2 = productInfoArray[j * 2];
						if (num2 < 0) {
							continue;
						}
						int num3 = productInfoArray[j * 2 + 1];
						int maxProductsPerRow = instance.GetMaxProductsPerRow(i, num2);
						int num4 = Mathf.FloorToInt((float)maxProductsPerRow * instance.productsThreshholdArray[p]);
						if (num3 < num4) {
							for (int k = 0; k < instance.storageOBJ.transform.childCount; k++) {
								int[] productInfoArray2 = instance.storageOBJ.transform.GetChild(k).GetComponent<Data_Container>().productInfoArray;
								int num5 = productInfoArray2.Length / 2;
								for (int l = 0; l < num5; l++) {
									int num6 = productInfoArray2[l * 2];
									if (num6 >= 0 && num6 == num2 && productInfoArray2[l * 2 + 1] > 0) {
										string item = i + "|" + j * 2 + "|" + k + "|" + l * 2 + "|" + num2 + "|" + num6;
										instance.auxiliarProductList.Add(item);
									}
								}
							}
						}
						if (instance.auxiliarProductList.Count > 0) {
							string item2 = instance.auxiliarProductList[UnityEngine.Random.Range(0, instance.auxiliarProductList.Count)];
							auxiliarOrderingList.Add(maxProductsPerRow);
							switch (p) {
								case 0:
									instance.lowProductCountList.Add(item2);
									break;
								case 1:
									instance.mediumProductCountList.Add(item2);
									break;
								case 2:
									instance.highProductCountList.Add(item2);
									break;
								case 3:
									instance.veryHighProductCountList.Add(item2);
									break;
							}
						}
					}
					performanceCounter++;
					if (performanceCounter >= 5) {
						performanceCounter = 0;
					}
				}
				int index = 0;
				switch (p) {
					case 0:
						instance.lowProductCountList = instance.lowProductCountList.OrderBy((string d) => auxiliarOrderingList[index++]).ToList();
						break;
					case 1:
						instance.mediumProductCountList = instance.mediumProductCountList.OrderBy((string d) => auxiliarOrderingList[index++]).ToList();
						break;
					case 2:
						instance.highProductCountList = instance.highProductCountList.OrderBy((string d) => auxiliarOrderingList[index++]).ToList();
						break;
					case 3:
						instance.veryHighProductCountList = instance.veryHighProductCountList.OrderBy((string d) => auxiliarOrderingList[index++]).ToList();
						break;
				}
				auxiliarOrderingList.Clear();
				if (instance.lowProductCountList.Count + instance.mediumProductCountList.Count + instance.highProductCountList.Count + instance.veryHighProductCountList.Count > numberOfRestockingEmployees) {
					break;
				}
			}
		}
		Performance.StopAndLog("MainRestockUpdate");

		LOG.TEMPWARNING($"{instance.lowProductCountList.Count + instance.mediumProductCountList.Count + instance.highProductCountList.Count + instance.veryHighProductCountList.Count} jobs generated the vanilla way.");

		instance.lowProductCountList.Clear();
		instance.mediumProductCountList.Clear();
		instance.highProductCountList.Clear();
		instance.veryHighProductCountList.Clear();
	}


	#region Async worker to let the game do its work if taking too long. Last version before I completely reworked the restock process and moved most logic to a separate thread
	public static ConcurrentQueue<RestockJobInfo> queueRestockJobsOld;
	private readonly static int RestockProcessMaxTimeMillis = 2;
	public static bool IsRestockGenerationWorking { get; set; }

	//Currently it isnt working completely right after I had to change related stuff for the threaded process.
	/// <summary>
	/// Generates a list of matches between product shelves, prioritizing emptier ones, and storage.
	/// </summary>
	public async static Task GenerateAvailableRestockProducts(NPC_Manager __instance) {
		IsRestockGenerationWorking = true;
		int totalDelayCount = 0;
		try {
			if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0 ||
					!HasRestockingEmployeAssigned(__instance)) {
				return;
			}

			List<ShelfSlotData> listStorage = GenerateStorageList(__instance);
			List<ShelfSlotData> listProdShelf = GenerateProductShelfList(__instance);

			Performance.Start("2. productsThresholdArray");
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
						//Check if shelf product quantity is below current priority.
						//Performance.Start("GetMaxProductsPerRow");
						maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
							__instance, prodShelf.DataContainer,
							prodShelf.ProductId, prodShelf.ShelfIndex);
						//Performance.StopAndRecord("GetMaxProductsPerRow");

						Performance.Start("ThresholdCalculation");
						int shelfQuantityThreshold = (int)(maxProductsPerRow * productThreshold);
						Performance.Stop("ThresholdCalculation");
						shouldBeRestocked = prodShelf.Quantity < shelfQuantityThreshold;
					}

					if (!shouldBeRestocked) {
						continue;
					}

					foreach (var storage in listStorage) {
						if (storage.ProductId >= 0 && storage.ProductId == prodShelf.ProductId && storage.Quantity > 0) {

							if (maxProductsPerRow == -1) {
								maxProductsPerRow = PerformanceCachingPatch.GetMaxProductsPerRowCached(
									__instance, prodShelf.DataContainer,
									prodShelf.ProductId, prodShelf.ShelfIndex);
							}
							//Used to be: productsPrioritySecondary.Add(new int[] { shelfIndex, shelfSlotIndex * 2, storageIndex, storageSlotIndex * 2, shelfProductId, storageProductId, shelfSlotIndex, storageSlotIndex, shelfQuantity, storageQuantity });
							queueRestockJobsOld.Enqueue(
								new RestockJobInfo(
									prodShelf.ToProdShelfSlotInfo(), storage.ToStorageSlotInfo(), maxProductsPerRow
								)
							);

							if (queueRestockJobsOld.Count >= 50) {
								return;
							}
						}
					}
				}
				if (restockLimit.Elapsed.TotalMilliseconds > RestockProcessMaxTimeMillis) {
					totalDelayCount++;
					Performance.Stop("2. productsThresholdArray", true);
					await Task.Delay(15);
					Performance.Start("2. productsThresholdArray", true);
					restockLimit.Restart();
				}
			}
		} finally {
			IsRestockGenerationWorking = false;
			Performance.StopAndLog("2. productsThresholdArray", true);
			Performance.StopAndLog("ThresholdCalculation", true);
			LOG.TEMPWARNING($"{queueRestockJobsOld.Count} jobs generated.");
		}
	}
	public static bool GetAvailableRestockJob(NPC_Manager __instance, NPC_Info employee, out RestockJobInfo restockJob) {
		if (IsRestockGenerationWorking || queueRestockJobsOld != null && queueRestockJobsOld.Count == 0) {
			restockJob = RestockJobInfo.Default;
			return false;
		}

		//Performance.Start("GetAvailableRestockJob");
		bool existsRestockProduct;
		do {
			queueRestockJobsOld.TryDequeue(out restockJob);

			//Check that both product shelf and storage slots are not in use by
			//	another employee, and that their contents are still valid.
			existsRestockProduct =
				TargetMatching.RefreshAndCheckTargetProductShelf(
					__instance, restockJob, restockJob.MaxProductsPerRow)
				&& TargetMatching.RefreshAndCheckTargetStorage(__instance, restockJob);

		} while (!existsRestockProduct && queueRestockJobsOld.Count > 0);

		if (!existsRestockProduct) {
			restockJob = RestockJobInfo.Default;
		}

		//Performance.StopAndLog("GetAvailableRestockJob");
		return existsRestockProduct;
	}
	private static List<ShelfSlotData> GenerateStorageList(NPC_Manager __instance) {
		int storageListSize = __instance.storageOBJ.transform.childCount * 8;
		List<ShelfSlotData> listStorage = new(storageListSize);

		ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, checkNPCStorageTarget: false, skipEmptyBoxes: true,
			(storageIndex, slotIndex, productId, quantity, storageObjT) => {

				listStorage.Add(new ShelfSlotData(storageIndex, slotIndex, productId, quantity, 
					storageObjT.GetComponent<Data_Container>(), storageObjT.position));
				return ContainerSearchLambdas.LoopAction.Nothing;
			}
		);
		return listStorage;
	}

	private static List<ShelfSlotData> GenerateProductShelfList(NPC_Manager __instance) {
		int prodShelfListSize = __instance.shelvesOBJ.transform.childCount * 5; //5 is a very rough estimate
		List<ShelfSlotData> listProdShelf = new(prodShelfListSize);

		ContainerSearchLambdas.ForEachProductShelfSlotLambda(__instance, false,
			(prodShelfIndex, slotIndex, productId, quantity, prodShelfObjT) => {
				listProdShelf.Add(new ShelfSlotData(prodShelfIndex, slotIndex, productId, quantity, 
					prodShelfObjT.GetComponent<Data_Container>(), prodShelfObjT.position));
				return ContainerSearchLambdas.LoopAction.Nothing;
			}
		);
		return listProdShelf;
	}

	private static bool HasRestockingEmployeAssigned(NPC_Manager __instance) {
		return __instance.employeeParentOBJ.transform
			.Cast<Transform>().Any(
				(t) => t.GetComponent<NPC_Info>().taskPriority == 2
		);
	}
	#endregion


	/*	Optimized CheckProductAvailability getting rid of productInfoArray madness
	 *	and other stuff before changing to above background job.
	
	public static RestockJobInfo CheckProductAvailability(NPC_Manager __instance) {
		if (__instance.storageOBJ.transform.childCount == 0 || __instance.shelvesOBJ.transform.childCount == 0) {
			return RestockJobInfo.Default;
		}

		//Performance.Start("LoopAllStorageAndShelves");
		int storageListSize = __instance.storageOBJ.transform.childCount * 8;
		List<(int storageIndex, int slotIndex, int productId, int quantity, Transform storageObjT)> 
			dictStorage = new(storageListSize);
		int prodShelfListSize = __instance.shelvesOBJ.transform.childCount * 5;	//5 is a very rough estimate
		List<(int prodShelfIndex, int slotIndex, int productId, int quantity, Transform prodShelfObjT)> 
			listProdShelf = new(prodShelfListSize);

		ContainerSearchLambdas.ForEachStorageSlotLambda(__instance, false,
			(storageIndex, slotIndex, productId, quantity, storageObjT) => {

				dictStorage.Add((storageIndex, slotIndex, productId, quantity, storageObjT));
				return ContainerSearchLambdas.LoopAction.Nothing;
			}
		);
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
						//Check if shelf product quantity is below current priority.
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
						foreach (var storage in dictStorage) {
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
							//Check if shelf product quantity is below current priority.
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
