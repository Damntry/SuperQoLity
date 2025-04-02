using System;
using System.Threading.Tasks;
using Damntry.UtilsUnity.UI.Extensions;
using HarmonyLib;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler;
using TMPro;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Components {

	/// <summary>Displays the current job frequency multiplier on the top right.</summary>
	public class FrequencyMult_Display : MonoBehaviour {

		private static FrequencyMult_Display instance;

		private static GameObject masterCanvas;

		private GameObject freqMultDisplay;

		private TextMeshProUGUI textDisplay;

		private float loopMultiplierCycle;

		private bool allowDisplay;
		
		private void Awake() {
			freqMultDisplay = SMTGameObjectManager.CreateSuperQoLGameObject("JobWorkload_Display", TargetObject.UI_MasterCanvas, false);

			SetupUI();

			instance = this;
		}
		
		private void Start() {
			DisplayLogicFromSetting();

			ModConfig.Instance.DisplayAutoModeFrequencyMult.SettingChanged += 
				(object sender, EventArgs e) => DisplayLogicFromSetting();
			JobSchedulerManager.OnNewJobFrequencyMultiplier += UpdateFreqMultiplierDisplay;
		}

		public static void Initialize() {
			SMTGameObjectManager.AddComponentTo<FrequencyMult_Display>(TargetObject.UI_MasterCanvas);
		}

		private void SetupUI() {
			freqMultDisplay.layer = 5;

			RectTransform rectT = freqMultDisplay.AddComponent<RectTransform>();
			rectT.SetAnchorAndPivot(AnchorPresets.TopRight, PivotPresets.TopRight);
			rectT.anchoredPosition = new Vector2(-10, -60);
			rectT.sizeDelta = new Vector2(100, 50);

			textDisplay = freqMultDisplay.AddComponent<TextMeshProUGUI>();
			textDisplay.color = new Color(0.9725f, 0.3176f, 0.8948f, 1f);
			textDisplay.fontSize = 21;
			textDisplay.fontStyle = FontStyles.Normal;
			textDisplay.horizontalAlignment = HorizontalAlignmentOptions.Right;
			textDisplay.verticalAlignment = VerticalAlignmentOptions.Middle;
		}

		public static void AllowDisplay() {
			instance.allowDisplay = true;

			instance.ShowDisplay();
		}

		public static void Destroy() {
			if (instance == null) {
				return;
			}

			instance.allowDisplay = false;
			ModConfig.Instance.DisplayAutoModeFrequencyMult.SettingChanged -=
				(object sender, EventArgs e) => instance.DisplayLogicFromSetting();
			JobSchedulerManager.OnNewJobFrequencyMultiplier -= instance.UpdateFreqMultiplierDisplay;

			UnityEngine.Object.Destroy(instance.freqMultDisplay);

			masterCanvas = null;
			instance = null;
		}

		private void DisplayLogicFromSetting() {
			if (ModConfig.Instance.DisplayAutoModeFrequencyMult.Value) {
				ShowDisplay();
			} else {
				HideDisplay();
			}
		}

		private void ShowDisplay() {
			if (allowDisplay && ModConfig.Instance.DisplayAutoModeFrequencyMult.Value) {
				UpdateDisplay(forceUpdate: true);

				freqMultDisplay.SetActive(true);
			}
		}

		private void HideDisplay() {
			freqMultDisplay.SetActive(false);
		}

		private void UpdateFreqMultiplierDisplay(float loopMultiplierCycle) {
			this.loopMultiplierCycle = loopMultiplierCycle;

			UpdateDisplay(forceUpdate: false);
		}

		private void UpdateDisplay(bool forceUpdate) {
			if (freqMultDisplay.activeSelf || forceUpdate) {
				textDisplay.text = $"{Math.Round(this.loopMultiplierCycle, 2)} x";
			}
		}
	}
}
