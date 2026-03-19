using Damntry.UtilsUnity.Components.InputManagement;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.UI;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.AutoMode;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.AutoMode.DataDefinition;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler {

	public class AutoModeProcessor {

        private const string jobSchedulerHotkeyGroup = "jobSchedHotkeys";

        public static AutoModeProcessor Instance { get; private set; }


        private bool forceAutoModeRefresh;

        public AutoModeData AutoModeData { get; set; }



        private AutoModeProcessor() {
            //When any of the custom mode settings are changed, force the current autoMode to update on the next cycle.
            ModConfig.Instance.CustomEmployeeWaitTarget.SettingChanged += ForceUpdateAutoMode;
            ModConfig.Instance.CustomCustomerWaitTarget.SettingChanged += ForceUpdateAutoMode;
            ModConfig.Instance.CustomMinimumFrequencyMult.SettingChanged += ForceUpdateAutoMode;
            ModConfig.Instance.CustomMaximumFrequencyMult.SettingChanged += ForceUpdateAutoMode;
            ModConfig.Instance.CustomMaximumFrequencyReduction.SettingChanged += ForceUpdateAutoMode;
            ModConfig.Instance.CustomMaximumFrequencyIncrease.SettingChanged += ForceUpdateAutoMode;

            if (Plugin.IsSolutionInDebugMode && ModConfig.Instance.EnabledDevMode.Value) {
                UIPanelHandler.LoadUIPanel();

                if (WorldState.IsWorldLoaded) {
                    InitializeWithGameWorld(GameWorldEvent.WorldLoaded);
                }

                WorldState.OnGameWorldChange += InitializeWithGameWorld;
            }
        }

        public static void Initialize() {
            //Though this class is a Singleton, we dont want it to get initialized wherever Instance is
            //  called, but instead, from an specific point in the logic. Otherwise it would hide possible errors.
            Instance ??= new AutoModeProcessor();
        }


        private void ForceUpdateAutoMode(object sender, EventArgs e) {
            if (WorldState.IsWorldLoaded) {
                forceAutoModeRefresh = true;
            }
        }

		public static void Destroy() {
            if (Instance == null) {
                return;
            }

            if (UIPanelHandler.IsPanelLoaded) {
				UIPanelHandler.DestroyPerformancePanel();
                InputManagerSMT.Instance.RemoveHotkeyGroup(jobSchedulerHotkeyGroup);
				WorldState.OnGameWorldChange -= Instance.InitializeWithGameWorld;
			}

            ModConfig.Instance.CustomEmployeeWaitTarget.SettingChanged -= Instance.ForceUpdateAutoMode;
            ModConfig.Instance.CustomCustomerWaitTarget.SettingChanged -= Instance.ForceUpdateAutoMode;
            ModConfig.Instance.CustomMinimumFrequencyMult.SettingChanged -= Instance.ForceUpdateAutoMode;
            ModConfig.Instance.CustomMaximumFrequencyMult.SettingChanged -= Instance.ForceUpdateAutoMode;
            ModConfig.Instance.CustomMaximumFrequencyReduction.SettingChanged -= Instance.ForceUpdateAutoMode;
            ModConfig.Instance.CustomMaximumFrequencyIncrease.SettingChanged -= Instance.ForceUpdateAutoMode;
        }

        public void SetAutoModeData(EnumJobFrequencyMultMode jobFreqMode) {
            if (AutoModeData == null || AutoModeData.JobFreqMode != jobFreqMode || forceAutoModeRefresh) {
                forceAutoModeRefresh = false;
                AutoModeData = GetAutoModeData(jobFreqMode);

                UIPanelHandler.SetFreqStepValues([AutoModeData.IncreaseStep, AutoModeData.DecreaseStep]);
            }
        }

        private static AutoModeData GetAutoModeData(EnumJobFrequencyMultMode jobFreqMode) =>
            jobFreqMode switch {
                EnumJobFrequencyMultMode.Auto_Performance => new PerformanceMode(),
                EnumJobFrequencyMultMode.Auto_Balanced => new BalancedMode(),
                EnumJobFrequencyMultMode.Auto_Aggressive => new AggressiveMode(),
                EnumJobFrequencyMultMode.Auto_Custom => new CustomMode(),
                _ => null
            };

        private void InitializeWithGameWorld(GameWorldEvent gameEvent) {
            if (gameEvent == GameWorldEvent.WorldLoaded) {
                UIPanelHandler.InitializePerformancePanel([0, 0]);
                UIPanelHandler.ShowUIPanel();

                InputManagerSMT.Instance.TryAddHotkey("jobSchedDebugPanelRiseCycleUp", KeyCode.Insert, InputState.KeyHeld,
                    HotkeyActiveContext.WorldLoaded, () =>
                        SetAndUpdatePerformanceUI(increase: true, rise: true), jobSchedulerHotkeyGroup);
                InputManagerSMT.Instance.TryAddHotkey("jobSchedDebugPanelRiseCycleDown", KeyCode.Delete, InputState.KeyHeld,
                    HotkeyActiveContext.WorldLoaded, () =>
                    SetAndUpdatePerformanceUI(increase: false, rise: true), jobSchedulerHotkeyGroup);
                InputManagerSMT.Instance.TryAddHotkey("jobSchedDebugPanelDropCycleUp", KeyCode.Home, InputState.KeyHeld,
                    HotkeyActiveContext.WorldLoaded, () =>
                    SetAndUpdatePerformanceUI(increase: true, rise: false), jobSchedulerHotkeyGroup);
                InputManagerSMT.Instance.TryAddHotkey("jobSchedDebugPanelDropCycleDown", KeyCode.End, InputState.KeyHeld,
                    HotkeyActiveContext.WorldLoaded, () =>
                    SetAndUpdatePerformanceUI(increase: false, rise: false), jobSchedulerHotkeyGroup);
            }
        }

        private void SetAndUpdatePerformanceUI(bool increase, bool rise) {
			if (AutoModeData == null) {
				return;
			}

			if (rise) {
				if (increase) {
					AutoModeData.IncreaseRiseCyclePct();
				} else {
					AutoModeData.DecreaseRiseCyclePct();
				}
			} else {
				if (increase) {
					AutoModeData.IncreaseDropCyclePct();
				} else {
					AutoModeData.DecreaseDropCyclePct();
				}
			}

			UIPanelHandler.SetFreqStepValues([AutoModeData.IncreaseStep, AutoModeData.DecreaseStep]);
		}


	}

}
