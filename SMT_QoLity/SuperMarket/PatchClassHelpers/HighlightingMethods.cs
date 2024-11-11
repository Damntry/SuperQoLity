using System;
using System.Reflection;
using Damntry.UtilsBepInEx.Logging;
using HarmonyLib;
using HighlightPlus;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers {
	public class HighlightingMethods {

		public static readonly Lazy<MethodInfo> HighlightShelfMethod = new Lazy<MethodInfo>(() =>
			AccessTools.Method($"{BetterSMT_Helper.BetterSMTInfo.PatchesNamespace}.PlayerNetworkPatch:HighlightShelf"));

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

		public static string GetGameObjectStringPath(ShelfType shelfType) {
			return shelfType switch {
				ShelfType.ProductDisplay => "Level_SupermarketProps/Shelves",
				ShelfType.Storage => "Level_SupermarketProps/StorageShelves",
				_ => throw new InvalidOperationException($"The shelf type {shelfType} is new and needs to be implemented."),
			};
		}

		public static void HighlightShelvesByProduct(int productID) {
			HighlightShelfTypeByProduct(productID, ModConfig.Instance.PatchBetterSMT_ShelfHighlightColor.Value, ShelfType.ProductDisplay);
			HighlightShelfTypeByProduct(productID, ModConfig.Instance.PatchBetterSMT_StorageHighlightColor.Value, ShelfType.Storage);
		}

		public static void ClearHighlightedShelves() {
			ClearHighlightShelvesByProduct(ShelfType.ProductDisplay);
			ClearHighlightShelvesByProduct(ShelfType.Storage);
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
					if (productID >= 0) {
						enableSlotHighlight = productInfoArray[j * 2] == productID;
						if (enableSlotHighlight) {
							enableShelfHighlight = true;
						}
					}

					ShelfData shelfData = new ShelfData(shelfType);
					if (shelfType == ShelfType.Storage) {
						highlightsMarker = shelf.Find(shelfData.highlightsName);

						if (highlightsMarker != null) {
							HighlightShelf(highlightsMarker.GetChild(j).GetChild(0), enableSlotHighlight, ModConfig.Instance.PatchBetterSMT_StorageSlotHighlightColor.Value);
						} else {
							BepInExTimeLogger.Logger.LogTimeError("The highlightsMarker object for the storage could not be found. Storage slot highlighting wont work.", Damntry.Utils.Logging.TimeLoggerBase.LogCategories.Highlight);
						}
					} else {
						highlightsMarker = shelf.Find(shelfData.highlightsName);
						HighlightShelf(highlightsMarker.GetChild(j), enableSlotHighlight, ModConfig.Instance.PatchBetterSMT_ShelfLabelHighlightColor.Value);
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

		private static void ClearHighlightedShelves(string shelfObjString, ShelfType shelfType) {
			GameObject gameObject = GameObject.Find(shelfObjString);
			for (int i = 0; i < gameObject.transform.childCount; i++) {
				Transform child = gameObject.transform.GetChild(i);

				Transform highlightObject = null;
				ShelfData shelfData = new ShelfData(shelfType);

				if (shelfType == ShelfType.ProductDisplay) {
					highlightObject = child.Find(shelfData.highlightsName);
				} else if (shelfType == ShelfType.Storage) {
					highlightObject = child.Find(shelfData.highlightsName);
				}

				int num = child.gameObject.GetComponent<Data_Container>().productInfoArray.Length / 2;
				for (int j = 0; j < num; j++) {
					HighlightShelf(highlightObject.GetChild(j), false, null);
				}
				HighlightShelf(child, false, null);
			}
		}

		public static void HighlightShelf(Transform t, bool value, Color? color = null) {
			HighlightShelfMethod.Value.Invoke(null, [t, value, color]);
		}

		/*
		public static void HighlightShelf_(Transform t, bool value, Color? color = null) {
			HighlightEffect highlightEffect = t.GetComponent<HighlightEffect>();
			if (highlightEffect == null) {
				highlightEffect = t.gameObject.AddComponent<HighlightEffect>();
				BepInExTimeLogger.Logger.LogTimeWarning($"Transform {t.GetPath()} doesnt have an highlight effect", Damntry.Utils.Logging.TimeLoggerBase.LogCategories.TempTest);
			}

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
			//highlightEffect.seeThrough = SeeThroughMode.;
			//highlightEffect.seeThroughOccluderMask = 0;
			//highlightEffect.seeThroughOccluderThreshold = 0f;
			highlightEffect.seeThroughOccluderCheckIndividualObjects = false;
			highlightEffect.seeThroughOccluderCheckInterval = 1f;

			highlightEffect.outlineIndependent = true;
			highlightEffect.outline = value ? 1f : 0f;
			highlightEffect.glow = value ? 0f : 1f;
			//TODO 8 - Refresh should only be called when the storage changes from a box being placed in it.
			highlightEffect.Refresh();
			highlightEffect.enabled = true;
			highlightEffect.SetHighlighted(value);
		}
		*/

	}
}
