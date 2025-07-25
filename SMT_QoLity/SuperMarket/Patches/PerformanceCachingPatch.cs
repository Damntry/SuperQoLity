using System.Collections;
using System.Collections.Generic;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches {

	public class PerformanceCachingPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableEmployeeChanges.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - AI caching patch failed. Disabled";


		private static readonly Dictionary<(int containerClass, int containerId, int productId), int> maxProductsPerRowCache = new();

		private static bool cachePopulated = false;


		public class PopulateCacheOnBuilderInitialization {

			[HarmonyPatch(typeof(Builder_Main), "RetrieveInitialBehaviours")]
			[HarmonyPostfix]
			public static IEnumerator WaitEndOfIEnumerable(IEnumerator result) {
				while (result.MoveNext()) {
					yield return result.Current;
				}

				PopulateMaxProductsPerRowCache();
			}
		}

		private static void PopulateMaxProductsPerRowCache() {
			if (cachePopulated) {
				return;
			}

			//Builder_Main builderMain = GameCanvas.Instance.GetComponent<Builder_Main>();
			GameObject[] buildables = GameData.Instance.GetComponent<NetworkSpawner>().buildables;

			foreach (GameObject buildable in buildables) {
				if (buildable == null) {
					//The first buildable is null for some reason.
					continue;
				}

				
				if (buildable.TryGetComponent(out Data_Container dataContainer) && 
						dataContainer.GetContainerType() == DataContainerType.ProductShelf) {
				//Data_Container dataContainer = buildable.GetComponent<Data_Container>();
				//if (dataContainer.containerClass < 20) {
					//The Data_Container belongs to a product shelf object. Calculate its capacity for each product.
					foreach (GameObject prodPrefab in ProductListing.Instance.productPrefabs) {

						if (prodPrefab == null) {
							//Support for mod "Custom Products" that resizes the array to a
							//	fixed 9999 elements, leaving empty ones.
							continue;
						}
						int productId = prodPrefab.GetComponent<Data_Product>().productID;
						int maxProductsPerRow = GetMaxProductsPerRow(dataContainer, productId);
						maxProductsPerRowCache.Add((dataContainer.containerClass, dataContainer.containerID, productId), maxProductsPerRow);
					}
				}
			}

			cachePopulated = true;
		}

		/// <summary>
		/// Gets the max number of products that can fit in the subcontainer of the shelf type.
		/// This method is not inherently thread-safe, it just behaves differently if coming from
		/// a threaded method.
		/// </summary>
		/// <param name="dataContainer">Data of the container.</param>
		/// <param name="shelfProductId">Id of the product to put in.</param>
		/// <param name="shelfIndex">Index of the shelf. This is only used in case of failure.</param>
		public static int GetMaxProductsPerRowCachedThreaded(Data_Container dataContainer, 
				int shelfProductId, int shelfIndex) {
			return GetMaxProductsPerRowCached(null, dataContainer, shelfProductId, shelfIndex, isThreaded: true);
		}

		/// <summary>
		/// Gets the max number of products that can fit in the subcontainer of the shelf type.
		/// </summary>
		/// <param name="__instance"></param>	
		/// <param name="dataContainer">Data of the container.</param>
		/// <param name="shelfProductId">Id of the product to put in.</param>
		/// <param name="shelfIndex">Index of the shelf. This is only used in case of failure.</param>
		public static int GetMaxProductsPerRowCached(NPC_Manager __instance,
				Data_Container dataContainer, int shelfProductId, int shelfIndex) {
			return GetMaxProductsPerRowCached(null, dataContainer, shelfProductId, shelfIndex, isThreaded: false);
		}

		/// <summary>
		/// Gets the max number of products that can fit in the subcontainer of the shelf type.
		/// </summary>
		/// <param name="__instance"></param>
		/// <param name="dataContainer">Data of the container.</param>
		/// <param name="shelfProductId">Id of the product to put in.</param>
		/// <param name="shelfIndex">Index of the shelf. This is only used in case of failure.</param>
		public static int GetMaxProductsPerRowCached(NPC_Manager __instance, 
				Data_Container dataContainer, int shelfProductId, int shelfIndex, bool isThreaded) {
			bool exists = false;
			int maxProductsPerRow = 0;

			if (cachePopulated) {
				exists = maxProductsPerRowCache.TryGetValue((dataContainer.containerClass, dataContainer.containerID, shelfProductId), out maxProductsPerRow);
			} else {
				TimeLogger.Logger.LogTimeWarning("MaxProductsPerRow has been requested from the cache, " +
					"but it hasnt been populated yet.", LogCategories.Cache);
			}
			if (!exists) {
				//This case shouldnt currently happen, but keep as a safety net against future codebase changes.
				TimeLogger.Logger.LogTimeWarning($"A maxProductsPerRow cache entry doesnt exist for containerID: {dataContainer.containerID} " +
					$"and productId: {shelfProductId}. Attempting to obtain it manually.", LogCategories.Loading);
				if (Container<PerformanceCachingPatch>.Instance.IsPatchActive) {
					maxProductsPerRow = GetMaxProductsPerRow(dataContainer, shelfProductId);
				} else if (__instance != null) {
					maxProductsPerRow = __instance.GetMaxProductsPerRow(shelfIndex, shelfProductId);
				} else if (isThreaded){
					TimeLogger.Logger.LogTimeWarning($"This method is threaded and the amount cannot " +
						$"be calculated from the source Unity objects. Returning 0.", LogCategories.Loading);
				} else {
					TimeLogger.Logger.LogTimeError($"The NPC_Manager instance is null " +
						$"and the value could not be obtained manually.", LogCategories.Loading);
				}

				maxProductsPerRowCache.Add((dataContainer.containerClass, dataContainer.containerID, shelfProductId), maxProductsPerRow);
			}

			return maxProductsPerRow;
		}

		public static int GetMaxProductsPerRow(Data_Container dataContainer, int shelfProductId) {
			//return GetMaxProductsPerRowMethodReversePatch.GetMaxProductsPerRow(NPC_Manager.Instance, dataContainer, shelfProductId);
			return GetMaxProductsPerRow(NPC_Manager.Instance, -1, dataContainer, shelfProductId);
		}

		private static int GetMaxProductsPerRow(NPC_Manager __instance, int shelfIndex, Data_Container dataContainer, int ProductID) {
			/*
			float shelfLength, shelfWidth, shelfHeight;
			if (dataContainer == null) {
				//Original base game method
				shelfLength = __instance.shelvesOBJ.transform.GetChild(shelfIndex).GetComponent<Data_Container>().shelfLength;
				shelfWidth = __instance.shelvesOBJ.transform.GetChild(shelfIndex).GetComponent<Data_Container>().shelfWidth;
				shelfHeight = __instance.shelvesOBJ.transform.GetChild(shelfIndex).GetComponent<Data_Container>().shelfHeight;
			}
			*/
			if (ProductID >= ProductListing.Instance.productPrefabs.Length) {
				//Happened in one update where they added a new hidden, unusable product,
				//	and its productId didnt match a position inside the array
				return -1;
			}
			GameObject gameObject = ProductListing.Instance.productPrefabs[ProductID];
			Vector3 size = gameObject.GetComponent<BoxCollider>().size;
			bool isStackable = gameObject.GetComponent<Data_Product>().isStackable;
			int num = Mathf.FloorToInt(dataContainer.shelfLength / (size.x * 1.1f));
			num = Mathf.Clamp(num, 1, 100);
			int num2 = Mathf.FloorToInt(dataContainer.shelfWidth / (size.z * 1.1f));
			num2 = Mathf.Clamp(num2, 1, 100);
			int num3 = num * num2;
			if (isStackable) {
				int num4 = Mathf.FloorToInt(dataContainer.shelfHeight / (size.y * 1.1f));
				num4 = Mathf.Clamp(num4, 1, 100);
				num3 *= num4;
			}
			return num3;
		}


		/*	TODO 4 - For some reason this reverse transpiler is always returning the non stacked value.
					Its as if isStackable always returned false, but from the IL everything seems fine.
					Need to debug it properly and fix it. Meanwhile I ll just use a manually modified 
					local copy of the method.
		
		public class GetMaxProductsPerRowMethodReversePatch {

			[HarmonyDebug]
			[HarmonyPatch(typeof(NPC_Manager), "GetMaxProductsPerRow")]
			[HarmonyReversePatch]
			public static int GetMaxProductsPerRow(NPC_Manager instance, Data_Container dataContainer, int productID) {
				//To replicate the patched method, we need this to be a class instance method, which means having
				//	a "this" var as arg.0 in the IL signature. But since this method is static, arg.0 is not automatically generated.
				//	We emulate it simply by passing the class instance as first argument.

				///Old C#:
				///		float shelfLength = this.shelvesOBJ.transform.GetChild(containerIndex).GetComponent<Data_Container>().shelfLength;
				///		float shelfWidth = this.shelvesOBJ.transform.GetChild(containerIndex).GetComponent<Data_Container>().shelfWidth;
				///		float shelfHeight = this.shelvesOBJ.transform.GetChild(containerIndex).GetComponent<Data_Container>().shelfHeight;
				///New C#:
				///		float shelfLength = dataContainer.shelfLength;
				///		float shelfWidth = dataContainer.shelfWidth;
				///		float shelfHeight = dataContainer.shelfHeight;

				IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
					var codeMatcher = new CodeMatcher(instructions);
					//Match to the position from where we want to keep all the original code.
					int startPosition = codeMatcher.MatchForward(false,
						new CodeMatch(inst => inst.operand != null && inst.operand.ToString().Contains(nameof(ProductListing.productPrefabs)))
					).Pos - 1;

					if (codeMatcher.IsInvalid) {
						throw new TranspilerDefaultMsgException($"Couldnt find the IL line that is using the var {nameof(ProductListing.productPrefabs)}.");
					}

					//Remove instructions up to the calculated start point, to skip the first lines we dont want.
					codeMatcher.Start().RemoveInstructions(startPosition);

					//Declare the 2º and 3º local vars. We dont need to do it with
					//	the first one since it can be kept on the back of the stack.
					LocalBuilder localBshelfWidth = generator.DeclareLocal(typeof(float));
					LocalBuilder localBshelfHeight = generator.DeclareLocal(typeof(float));

					codeMatcher.Insert(
					//Load shelfLength into the stack
						new CodeInstruction(OpCodes.Ldarg_1),
						new CodeInstruction(OpCodes.Ldfld,
							AccessTools.Field(typeof(Data_Container), nameof(Data_Container.shelfLength))),
					//Store shelfWidth into the created local var
						new CodeInstruction(OpCodes.Ldarg_1),
						new CodeInstruction(OpCodes.Ldfld,
							AccessTools.Field(typeof(Data_Container), nameof(Data_Container.shelfWidth))),
						CodeInstructionNew.StoreLocal(localBshelfWidth.LocalIndex),
					//Store shelfHeight into the created local var
						new CodeInstruction(OpCodes.Ldarg_1),
						new CodeInstruction(OpCodes.Ldfld,
							AccessTools.Field(typeof(Data_Container), nameof(Data_Container.shelfHeight))),
						CodeInstructionNew.StoreLocal(localBshelfHeight.LocalIndex)
					);

					return codeMatcher.InstructionEnumeration();
				}

				//To avoid compiler errors.
				_ = Transpiler(null, null);
				return 0;
			}
		}
		*/

	}
}
