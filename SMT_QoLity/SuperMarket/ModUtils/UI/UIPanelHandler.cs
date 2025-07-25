using System;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.Resources;
using Damntry.UtilsUnity.UI;
using TMPro;
using UnityEngine;

namespace SuperQoLity.SuperMarket.ModUtils.UI {

	public class UIPanelHandler {

		public const string UIPanelName = "SuperQolInfoPanel";
		public const string UIPanelPrefabName = "superqol";

		private const float MaxBarValue = 1000;
		//private const float MinBarValue = 50;

		private static AssetBundleElement bundleElement;

		public static bool IsPanelLoaded;

		private static float barMaxHeight;

		private static GameObject superQolUIPanel;

		public static bool LoadUIPanel() {
			if (IsPanelLoaded) {
				TimeLogger.Logger.LogTimeWarning("Performance panel was already loaded.", LogCategories.PerfTest);
				return true;
			}

			if (bundleElement == null) {
				bundleElement = new AssetBundleElement(typeof(Plugin), $"Assets\\Debug\\{UIPanelPrefabName}");
			}
			
			if (bundleElement.TryLoadNewPrefabInstance(UIPanelName, out superQolUIPanel)) {
				IsPanelLoaded = true;

				barMaxHeight = GetBarHeight(GetContainerTop(), 0);  //By default all bars have maxed out values.
				superQolUIPanel.SetActive(false);

				return true;
			}

			return false;
		}

		public static void InitializePerformancePanel(float[] values) {
			if (!IsPanelLoaded) {
				throw new InvalidOperationException("Performance UI Panel is not loaded.");
			}

			//Initialize top container bar heights and text values.
			Transform containerTopT = GetContainerTop();
			for (int i = 0; i < containerTopT.childCount; i++) {
				SetBarHeight(0, containerTopT, i);
				SetAvgTimeText("-", containerTopT, i);
				SetFreqMultText("-", containerTopT, i);
			}

			//Initialize bottom container
			SetFreqStepValues(values);

			CanvasMethods.AttachPanelToCanvasWithAnchor(superQolUIPanel, GameCanvas.Instance.transform);
			superQolUIPanel.GetComponent<RectTransform>().localScale = new Vector3(1.2f, 1.2f, 1.2f);
		}

		public static void DestroyPerformancePanel() {
			if (!IsPanelLoaded) {
				return;
			}

			HideUIPanel();
			UnityEngine.Object.Destroy(superQolUIPanel);
			IsPanelLoaded = false;
		}

		public static void ShowUIPanel() {
			if (IsPanelLoaded && !superQolUIPanel.activeSelf) {
				superQolUIPanel.SetActive(true);
			}
		}

		public static void HideUIPanel() {
			if (IsPanelLoaded && superQolUIPanel.activeSelf) {
				superQolUIPanel.SetActive(false);
			}
		}

		public static void SetFreqStepValues(float[] values) {
			if (!IsPanelLoaded) {
				return;
			}

			Transform containerBottomT = GetContainerBottom();
			for (int i = 0; i < containerBottomT.childCount; i++) {
				containerBottomT.GetChild(i).GetChild(2)
					.GetComponent<TextMeshProUGUI>()
					.text = values[i].ToString("F2");
			}
		}

		private static Transform GetContainerTop() => superQolUIPanel.transform.GetChild(0).GetChild(0);
		private static Transform GetContainerBottom() => superQolUIPanel.transform.GetChild(0).GetChild(1);

		private static Transform GetSubContainerTopElement(Transform containerTop, int index, int subIndex) => 
			containerTop.GetChild(index).GetChild(subIndex);

		private static float GetBarHeight(Transform containerTop, int index) =>
			GetSubContainerTopElement(containerTop, index, 0).GetChild(0).GetComponent<RectTransform>().rect.yMax;

		private static void SetBarHeight(float height, Transform containerTop, int index) {
			RectTransform rectT = GetSubContainerTopElement(containerTop, index, 0).GetChild(0).GetComponent<RectTransform>();
			rectT.sizeDelta = new Vector2(rectT.sizeDelta.x, height);
		}

		private static string GetAvgTimeText(Transform containerTop, int index) =>
			GetSubContainerTopElement(containerTop, index, 1).GetComponent<TextMeshProUGUI>().text;

		private static void SetAvgTimeText(string text, Transform containerTop, int index) {
			GetSubContainerTopElement(containerTop, index, 1).GetComponent<TextMeshProUGUI>().text = text;
		}

		private static string GetFreqMultText(Transform containerTop, int index) =>
			GetSubContainerTopElement(containerTop, index, 2).GetComponent<TextMeshProUGUI>().text;

		private static void SetFreqMultText(string text, Transform containerTop, int index) {
			GetSubContainerTopElement(containerTop, index, 2).GetComponent<TextMeshProUGUI>().text = text;
		}

		public static void AddNewHistoricValue(float avgTime, float freqMultiplier) {
			if (!IsPanelLoaded) {
				return;
			}

			Transform containerTopT = GetContainerTop();
			for (int i = 0; i < containerTopT.childCount; i++) {
				float height;
				string textValue;
				string freqMult;
				if (i < containerTopT.childCount - 1) {
					//Move the next bar value into the current one.
					height = GetBarHeight(containerTopT, i + 1);
					textValue = GetAvgTimeText(containerTopT, i + 1);
					freqMult = GetFreqMultText(containerTopT, i + 1);
				} else {
					//Last item, aka, the current measure. Update with calculated values.
					height = CalculateBarHeight(avgTime);
					textValue = FormatAvgTime(avgTime);
					freqMult = FormatFreqMult(freqMultiplier);
				}
				
				SetBarHeight(height, containerTopT, i);
				SetAvgTimeText(textValue, containerTopT, i);
				SetFreqMultText(freqMult, containerTopT, i);
			}
		}

		private static string FormatAvgTime(float avgTime) => avgTime.ToString("F0");

		private static string FormatFreqMult(float freqMult) => freqMult.ToString("F2") + "x";


		private static float CalculateBarHeight(float value) {
			//MaxBarValue will be the max possible value against which we ll compare
			//	the argument value to get the relative bar height.
			return value * barMaxHeight / MaxBarValue;
		}

	}
}
