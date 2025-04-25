using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;
using UnityEngine.UI;

namespace SuperQoLity.SuperMarket.Patches.Misc {

	/// <summary>
	/// Expands the clickable area of the blackboard product order button, so you can click anywhere in it.
	/// </summary>
	public class ExpandProductOrderClickArea : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnableMiscPatches.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } =
			$"{MyPluginInfo.PLUGIN_NAME} - Increased clickable area in product order patch failed. Disabled.";


		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				ManageState();

				ModConfig.Instance.EnableExpandedProdOrderClickArea.SettingChanged += (_, _) => ManageState();
			}
		}

		
		private void ManageState() {
			if (ModConfig.Instance.EnableExpandedProdOrderClickArea.Value) {
				WorldState.OnFPControllerStarted += ExpandOrderClickableArea;

				if (WorldState.IsGameWorldAtOrAfter(GameWorldEvent.FPControllerStarted) && 
						IsOrderUiPrefabLoaded(out ManagerBlackboard managerBlackboard)) {
					ExpandOrderClickableArea(managerBlackboard);
				}
			} else {
				WorldState.OnFPControllerStarted -= ExpandOrderClickableArea;

				if (WorldState.IsGameWorldAtOrAfter(GameWorldEvent.FPControllerStarted) && 
						IsOrderUiPrefabLoaded(out ManagerBlackboard managerBlackboard)) {
					RestoreOrderClickableArea(managerBlackboard);
				}
			}
		}

		private void ExpandOrderClickableArea() {
			if (!IsOrderUiPrefabLoaded(out ManagerBlackboard managerBlackboard)) {
				TimeLogger.Logger.LogTimeError($"{nameof(ManagerBlackboard)}." +
					$"{nameof(ManagerBlackboard.UIShopItemPrefab)} should be instanced " +
					$"by now but its not.", LogCategories.UI);
				return;
			}

			ExpandOrderClickableArea(managerBlackboard);
		}

		private void ExpandOrderClickableArea(ManagerBlackboard managerBlackboard) {
			SetButtonClickableArea(managerBlackboard, new Vector4(-175, -5, -3, -50));

			//Make the icons showing where the product shelf goes on non
			//	interactable, otherwise the button wont work on top of them.
			GameObject prodShelfIcon = managerBlackboard.UIShopItemPrefab.transform.Find("ContainerTypeBCK").gameObject;
			prodShelfIcon.GetComponent<Image>().raycastTarget = false;
			prodShelfIcon.transform.Find("ContainerImage").GetComponent<Image>().raycastTarget = false;
		}

		private void RestoreOrderClickableArea(ManagerBlackboard managerBlackboard) {
			SetButtonClickableArea(managerBlackboard, Vector4.zero);
		}

		private void SetButtonClickableArea(ManagerBlackboard managerBlackboard, Vector4 padding) {
			managerBlackboard.UIShopItemPrefab.transform.Find("AddButton")
				.GetComponent<Image>()
				//Any other way is better than doing this. And yet here we are.
				.raycastPadding = padding;
		}

		private bool IsOrderUiPrefabLoaded(out ManagerBlackboard managerBlackboard) {
			managerBlackboard = null;

			return GameData.Instance != null 
				&& GameData.Instance.TryGetComponent<ManagerBlackboard>(out managerBlackboard) 
				&& managerBlackboard.UIShopItemPrefab != null;
		}

	}

}
