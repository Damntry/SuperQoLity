using System;
using System.Collections.Generic;
using Damntry.Utils.Logging;
using HighlightPlus;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers {
	public class ShelfHighlighting {

		public enum ShelfType {
			ProductDisplay,
			Storage
		}

		private struct ShelfData {

			public ShelfData(ShelfType shelfType) {
				if (shelfType == ShelfType.ProductDisplay) {
					highlightsName = "Labels";
					highlightsOriginalName = "";
					this.shelfType = ShelfType.ProductDisplay;
				} else {
					highlightsName = "HighlightsMarker";
					highlightsOriginalName = "Highlights";
					this.shelfType = ShelfType.Storage;
				}

			}

			public string highlightsName;
			public string highlightsOriginalName;
			public ShelfType shelfType;

		}

		private static Dictionary<int, Transform> highlightObjectCache;

		public static bool IsHighlightCacheUsed { get; set; } = true;

		public static void InitHighlightCache() {
			highlightObjectCache = new();
		}


		public static string GetGameObjectStringPath(ShelfType shelfType) {
			return shelfType switch {
				ShelfType.ProductDisplay => "Level_SupermarketProps/Shelves",
				ShelfType.Storage => "Level_SupermarketProps/StorageShelves",
				_ => throw new InvalidOperationException($"The shelf type {shelfType} is new and needs to be implemented."),
			};
		}

		public static void HighlightShelvesByProduct(int productID) {
			HighlightShelfTypeByProduct(productID, ModConfig.Instance.ShelfHighlightColor.Value, ShelfType.ProductDisplay);
			HighlightShelfTypeByProduct(productID, ModConfig.Instance.StorageHighlightColor.Value, ShelfType.Storage);
		}

		public static void ClearHighlightedShelves() {
			if (IsHighlightCacheUsed) {
				foreach (var item in highlightObjectCache) {
					if (item.Value != null) {
						HighlightShelf(item.Value, false);
					}
				}
				highlightObjectCache.Clear();
			} else {
				ClearHighlightShelvesByProduct(ShelfType.ProductDisplay);
				ClearHighlightShelvesByProduct(ShelfType.Storage);
			}
		}

		private static void ClearHighlightShelvesByProduct(ShelfType shelfType) {
			HighlightShelfTypeByProduct(-1, Color.white, shelfType);
		}

		private static void HighlightShelfTypeByProduct(int productID, Color shelfHighlightColor, ShelfType shelfType) {
			Transform highlightsMarker;

			GameObject shelvesObject = GameObject.Find(GetGameObjectStringPath(shelfType));

			for (int i = 0; i < shelvesObject.transform.childCount; i++) {
				Transform shelf = shelvesObject.transform.GetChild(i);
				int[] productInfoArray = shelf.gameObject.GetComponent<Data_Container>().productInfoArray;
				int num = productInfoArray.Length / 2;
				bool enableShelfHighlight = false;

				for (int j = 0; j < num; j++) {
					bool enableSlotHighlight = false;
					if (productID >= 0 && productInfoArray[j * 2] == productID) { 
						//Slot has same product id and should be highlighted if the setting is enabled.
						enableSlotHighlight = true;
						enableShelfHighlight = true;
					}

					if (enableSlotHighlight ||
							//If there are slot highlights pending to disable
							!enableSlotHighlight && IsHighlightCacheUsed && highlightObjectCache.Count > 0) {

						ShelfData shelfData = new ShelfData(shelfType);
						highlightsMarker = shelf.Find(shelfData.highlightsName);

						if (shelfType == ShelfType.Storage) {
							if (highlightsMarker != null) {
								HighlightShelf(highlightsMarker.GetChild(j).GetChild(0), enableSlotHighlight, ModConfig.Instance.StorageSlotHighlightColor.Value);
							} else {
								TimeLogger.Logger.LogTimeError("The highlightsMarker object for the storage could not be found. Storage slot highlighting wont work.", Damntry.Utils.Logging.LogCategories.Highlight);
							}
						} else {
							HighlightShelf(highlightsMarker.GetChild(j), enableSlotHighlight, ModConfig.Instance.ShelfLabelHighlightColor.Value);
						}
					}
				}
				//Highlight the entire storage shelf
				HighlightShelf(shelf, enableShelfHighlight, shelfHighlightColor);
			}
		}

		public static void AddHighlightMarkersToStorage(Transform storage) {
			ShelfData shelfData = new ShelfData(ShelfType.Storage);

			Transform highlightsMarker = storage.transform.Find(shelfData.highlightsName);

			if (highlightsMarker != null) {
				return;
			}

			highlightsMarker = UnityEngine.Object.Instantiate(storage.Find(shelfData.highlightsOriginalName).gameObject, storage).transform;
			highlightsMarker.name = shelfData.highlightsName;


			//Activate all markers so they wait for the enabling of the highlighting (It did weird stuff if I used the enabled as an on/off for the highlighting).
			for (int i = 0; i < highlightsMarker.childCount; i++) {
				highlightsMarker.GetChild(i).gameObject.SetActive(true);

				Transform highlight = highlightsMarker.GetChild(i).GetChild(0);
				highlight.gameObject.SetActive(true);

				HighlightShelf(highlight, false, null);
			}
		}

		public static void HighlightShelf(Transform t, bool isEnableHighlight, Color? color = null) {
			HighlightEffect highlightEffect = t.GetComponent<HighlightEffect>() ?? t.gameObject.AddComponent<HighlightEffect>();

			if (IsHighlightCacheUsed) {
				//Test this in multiplayer. Also, what happens if someone destroys
				//		the storage while you are holding its box?
				if (isEnableHighlight == highlightEffect.highlighted) {
					return;
				}

				if (isEnableHighlight) {
					if (highlightObjectCache.ContainsKey(t.GetInstanceID())) {
						if (highlightEffect.outlineColor == color) {
							//Already highlighted with the same color.
							return;
						}
					} else {
						highlightObjectCache.Add(t.GetInstanceID(), t);
					}
				}

				//Make the object to be highlighted ignore occlusion culling, so it doesnt dissapear
				MeshRenderer meshRender = t.GetComponent<MeshRenderer>();
				meshRender.allowOcclusionWhenDynamic = !isEnableHighlight;

				if (isEnableHighlight) {
					foreach (var mat in meshRender.materials) {
						mat.renderQueue = 1000;
					}
				}
			}

			//TODO 8 - Refresh should only be called when the storage changes from a box being placed in it.
			highlightEffect.Refresh();

			if (color != null) {
				highlightEffect.outlineColor = (Color)color;
			}
			highlightEffect.outlineQuality = HighlightPlus.QualityLevel.High;
			highlightEffect.outlineVisibility = Visibility.AlwaysOnTop;
			highlightEffect.outlineContourStyle = ContourStyle.AroundObjectShape;
			//highlightEffect.outlineMaskMode = MaskMode.IgnoreMask;

			// Occlusion defaults
			//highlightEffect.seeThroughOccluderThreshold = 0.3f;
			//highlightEffect.seeThroughOccluderCheckIndividualObjects = false;
			//highlightEffect.seeThroughOccluderCheckInterval = 1f;

			highlightEffect.seeThroughMaxDepth = 0f;
			highlightEffect.seeThroughDepthOffset = 0f;
			highlightEffect.seeThroughOccluderCheckIndividualObjects = false;
			highlightEffect.seeThroughOccluderCheckInterval = 1f;

			
			//highlightEffect.outlineMaskMode = MaskMode.IgnoreMask;
			//highlightEffect.seeThrough = SeeThroughMode.WhenHighlighted;
			//highlightEffect.seeThroughOccluderMask = -1;
			//highlightEffect.seeThroughBorder = 1f;
			//highlightEffect.seeThroughBorderColor = Color.green;
			//highlightEffect.seeThroughIntensity = 5f;
			//highlightEffect.targetFXGroundMaxDistance = 10000000f;
			

			highlightEffect.outlineIndependent = true;
			highlightEffect.outline = isEnableHighlight ? 1f : 0f;
			highlightEffect.glow = isEnableHighlight ? 0f : 1f;
			highlightEffect.enabled = true;

			highlightEffect.SetHighlighted(isEnableHighlight);
		}

	}
}
