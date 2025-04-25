using System;
using Damntry.UtilsUnity.Components;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.UI;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode.DataDefinition;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.Helpers;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler {

	public class JobSchedulerProcessor {

		private AutoModeData autoModeData;

		private FrequencyTrendCalculation freqTrendCalc;

		/// <summary>Average wait time of employees processed in the previous cycle.</summary>
		private float lastAvgWaitTime;

		private float lastJobFreqMult;

		private bool forceAutoModeRefresh;

		public JobSchedulerProcessor() {
			lastAvgWaitTime = -1;
			freqTrendCalc = new FrequencyTrendCalculation();

			//When any of the custom mode settings are changed, force the current autoMode to update on the next cycle.
			ModConfig.Instance.CustomAvgEmployeeWaitTarget.SettingChanged += ForceUpdateAutoMode;
			ModConfig.Instance.CustomMinimumFrequencyMult.SettingChanged += ForceUpdateAutoMode;
			ModConfig.Instance.CustomMaximumFrequencyMult.SettingChanged += ForceUpdateAutoMode;
			ModConfig.Instance.CustomMaximumFrequencyReduction.SettingChanged += ForceUpdateAutoMode;
			ModConfig.Instance.CustomMaximumFrequencyIncrease.SettingChanged += ForceUpdateAutoMode;

			if (Plugin.IsSolutionInDebugMode && ModConfig.Instance.EnabledDevMode.Value) {
				InitializePerformancePanel();
			}
		}

		private void InitializePerformancePanel() {
			UIPanelHandler.LoadUIPanel();

			if (WorldState.IsGameWorldStarted) {
				InitializeWithGameWorld(GameWorldEvent.WorldStarted);
			}

			WorldState.OnGameWorldChange += InitializeWithGameWorld;
		}

		private void InitializeWithGameWorld(GameWorldEvent gameEvent) {
			if (gameEvent == GameWorldEvent.WorldStarted) {
				UIPanelHandler.InitializePerformancePanel([0, 0]);
				UIPanelHandler.ShowUIPanel();

				KeyPressDetection.AddHotkey(KeyCode.Insert, () => SetAndUpdatePerformanceUI(true, true));
				KeyPressDetection.AddHotkey(KeyCode.Delete, () => SetAndUpdatePerformanceUI(false, true));
				KeyPressDetection.AddHotkey(KeyCode.Home, () => SetAndUpdatePerformanceUI(true, false));
				KeyPressDetection.AddHotkey(KeyCode.End, () => SetAndUpdatePerformanceUI(false, false));
			}
		}

		public void Destroy() {
			//TODO 2 - This is never being called when exiting a game. Somehow the JobSchedule 
			//	object in the sessionVars is null before this Destroy is called ¿?

			if (Plugin.IsSolutionInDebugMode) {
				UIPanelHandler.DestroyPerformancePanel();
				KeyPressDetection.RemoveHotkey(KeyCode.Insert);
				KeyPressDetection.RemoveHotkey(KeyCode.Delete);
				KeyPressDetection.RemoveHotkey(KeyCode.Home);
				KeyPressDetection.RemoveHotkey(KeyCode.End);
				WorldState.OnGameWorldChange -= InitializeWithGameWorld;
			}

			ModConfig.Instance.CustomAvgEmployeeWaitTarget.SettingChanged -= ForceUpdateAutoMode;
			ModConfig.Instance.CustomMinimumFrequencyMult.SettingChanged -= ForceUpdateAutoMode;
			ModConfig.Instance.CustomMaximumFrequencyMult.SettingChanged -= ForceUpdateAutoMode;
			ModConfig.Instance.CustomMaximumFrequencyReduction.SettingChanged -= ForceUpdateAutoMode;
			ModConfig.Instance.CustomMaximumFrequencyIncrease.SettingChanged -= ForceUpdateAutoMode;
		}

		public void SetAndUpdatePerformanceUI(bool increase, bool rise) {
			if (autoModeData == null) {
				return;
			}

			if (rise) {
				if (increase) {
					autoModeData.IncreaseRiseCyclePct();
				} else {
					autoModeData.DecreaseRiseCyclePct();
				}
			} else {
				if (increase) {
					autoModeData.IncreaseDropCyclePct();
				} else {
					autoModeData.DecreaseDropCyclePct();
				}
			}

			UIPanelHandler.SetFreqStepValues([autoModeData.IncreaseStep, autoModeData.DecreaseStep]);
		}
		private void ForceUpdateAutoMode(object sender, EventArgs e) {
			if (WorldState.IsGameWorldStarted) {
				forceAutoModeRefresh = true;
			}
		}

		public float CalculateNextJobFreqMultiplier(EnumJobFrequencyMultMode jobFreqMode, EmployeesWaitTimers empWaitTimers, float fixedDeltaTime) {
			if (autoModeData == null || autoModeData.JobFreqMode != jobFreqMode || forceAutoModeRefresh) {
				forceAutoModeRefresh = false;
				autoModeData = GetAutoModeData(jobFreqMode);
				UIPanelHandler.SetFreqStepValues([autoModeData.IncreaseStep, autoModeData.DecreaseStep]);
			}

			float averageWaitTimeMillis = empWaitTimers.CalculateAvgWaitTimesAndReset();

			float newJobFreqMult = GetCalculatedJobFreqMultiplier(averageWaitTimeMillis, jobFreqMode, fixedDeltaTime);

			UIPanelHandler.AddNewHistoricValue(averageWaitTimeMillis, newJobFreqMult);

			lastAvgWaitTime = averageWaitTimeMillis;
			lastJobFreqMult = newJobFreqMult;
			return newJobFreqMult;
		}

		private float GetCalculatedJobFreqMultiplier(float averageWaitTimeMillis,
				EnumJobFrequencyMultMode jobFreqMode, float fixedDeltaTime) {
			if (lastAvgWaitTime == -1) {
				//First check after starting a game.
				return autoModeData.DefaultFrequencyMult;
			}

			//Calculate by how much we ll increase or decrease the previous frequency
			float jobFreqStepValue = freqTrendCalc.CalculateJobFreqStepValue(averageWaitTimeMillis, 
				lastAvgWaitTime, lastJobFreqMult, fixedDeltaTime, autoModeData);

			float newJobFreqMult = lastJobFreqMult + jobFreqStepValue;

			return autoModeData.Clamp(newJobFreqMult);
		}


		private AutoModeData GetAutoModeData(EnumJobFrequencyMultMode jobFreqMode) {
			return jobFreqMode switch {
				EnumJobFrequencyMultMode.Auto_Performance => new PerformanceMode(),
				EnumJobFrequencyMultMode.Auto_Balanced => new BalancedMode(),
				EnumJobFrequencyMultMode.Auto_Aggressive => new AggressiveMode(),
				EnumJobFrequencyMultMode.Auto_Custom => new CustomMode(),
				_ => null
			};
		}


		/*	Old unfinished code for the scheduler implementation using a historic of values.
		 *	Might retake this later on if the current one is not good enough.
		 
		/// <summary>
		/// The number of previously calculated values to save, each representing
		/// a complete FixedUpdate cycle. Each cycle is 1 second.
		/// </summary>
		private static readonly int CycleHistoricSize = 10;

		private static readonly double InitialRecencyScore = 2d;

		/// <summary>Historic of the last few employee average wait times.</summary>
		private FixedCapacityQueue<double> avgWaitTimeHistoric;

		/// <summary>Historic of the last few multiplier increases and decreases.</summary>
		private FixedCapacityQueue<double> multMovementsHistoric;

		public JobSchedulerProcessor() {
			avgWaitTimeHistoric = new FixedCapacityQueue<double>(CycleHistoricSize);
			multMovementsHistoric = new FixedCapacityQueue<double>(CycleHistoricSize);
		}

		private double GetAvgWaitTimeScore(EmployeeJobFrequencyModeEnum jobFreqMode) {
			double totalWaitScore = 0d;
			double recencyScore = InitialRecencyScore;


			double recencyGrowthExponent = jobFreqMode switch {
				EmployeeJobFrequencyModeEnum.Auto_Performance => 1.15d,
				EmployeeJobFrequencyModeEnum.Auto_Balanced => 1.15d,
				EmployeeJobFrequencyModeEnum.Auto_Aggressive => 1.5d,
				_ => 0d
			};

			foreach (double avgWaitTime in avgWaitTimeHistoric.ToArray()) {
				totalWaitScore += avgWaitTime * recencyScore;

				//First ones are oldest ones, so as we advance, the score
				//	of more recent time measures grow exponentially.
				recencyScore = Math.Pow(recencyScore, recencyGrowthExponent);
			}

			return totalWaitScore;
		}
		*/


	}

}
