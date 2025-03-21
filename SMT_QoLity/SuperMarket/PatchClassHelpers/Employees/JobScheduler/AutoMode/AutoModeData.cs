using System;
using Damntry.Utils.Logging;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode.DataDefinition;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode {

	public abstract class AutoModeData {

		public EnumJobFrequencyMultMode JobFreqMode { get; init; }

		/// <summary>The default frequency multiplier. Only used in case of 
		/// errors and for the initial job scheduler state.</summary>
		public float DefaultFrequencyMult { get; protected set; }

		/// <summary>
		/// Each second, the job scheduler measures how much an employee had to wait to be 
		/// able to start its own job, and then calculates an average between all employees. 
		/// The value of AvgEmployeeWaitTargetMillis is the target for the job scheduler to
		/// try and balance the frequency multiplier, so it keeps the average wait as close 
		/// as possible to this value.
		/// A negative value will set the target as the minimum possible wait, plus a small buffer.
		/// </summary>
		public float AvgEmployeeWaitTargetMillis { get; init; }

		/// <summary>Minimum possible frequency multiplier.</summary>
		public float MinFreqMult { get; protected set; }
		/// <summary>Maximum possible frequency multiplier.</summary>
		public float MaxFreqMult { get; protected set; }
		/// <summary>
		/// Maximum absolute value that can be substracted from a
		/// decreasing frequency multiplier. Calculated each cycle.
		/// </summary>
		public float DecreaseStep { get; protected set; }
		/// <summary>
		/// Maximum absolute value that can be added to an increasing 
		/// frequency multiplier. Calculated each cycle.
		/// </summary>
		public float IncreaseStep { get; protected set; }


		#region	"Tests"
		private const float StepChange = 0.01f;

		public void IncreaseRiseCyclePct() {
			IncreaseStep += StepChange;
		}
		public void DecreaseRiseCyclePct() {
			IncreaseStep -= StepChange;
		}
		public void IncreaseDropCyclePct() {
			DecreaseStep -= StepChange;
		}
		public void DecreaseDropCyclePct() {
			DecreaseStep += StepChange;
		}
		#endregion


		/// <summary>
		/// Creates a new dynamic AutoMode for the job scheduler.
		/// </summary>
		/// <param name="jobFreqMode"></param>
		/// <param name="defaultFrequencyMult">
		/// The default frequency multiplier. Only used in case of errors and for the initial job scheduler state.
		/// </param>
		/// <param name="avgEmployeeWaitTargetMillis">
		/// Each second, the job scheduler measures how much an employee had to wait to be 
		/// able to start its own job, and then calculates an average between all employees. 
		/// The value of AvgEmployeeWaitTargetMillis is the target for the job scheduler to
		/// try and balance the frequency multiplier, so it keeps the average wait as close 
		/// as possible to this value.
		/// A -1 value will set the target as the minimum possible wait, plus a small buffer.
		/// </param>
		/// <param name="minFreqMult">Minimum possible frequency multiplier.</param>
		/// <param name="maxFreqMult">Maximum possible frequency multiplier.</param>
		/// <param name="decreaseStep">
		/// Maximum absolute value that can be substracted from a 
		/// decreasing frequency multiplier. Calculated each cycle
		/// </param>
		/// <param name="increaseStep">
		/// Maximum absolute value that can be added to an increasing 
		/// frequency multiplier. Calculated each cycle.
		/// </param>
		protected AutoModeData(EnumJobFrequencyMultMode jobFreqMode, float defaultFrequencyMult, float avgEmployeeWaitTargetMillis,
				float minFreqMult, float maxFreqMult, float decreaseStep, float increaseStep) {

			this.JobFreqMode = jobFreqMode;

			this.DefaultFrequencyMult = defaultFrequencyMult;
			this.AvgEmployeeWaitTargetMillis = avgEmployeeWaitTargetMillis;

			this.MinFreqMult = minFreqMult;
			this.MaxFreqMult = maxFreqMult;

			this.DecreaseStep = decreaseStep;
			this.IncreaseStep = increaseStep;

			int notifCounter = VerifyLocalValues();
			if (notifCounter > 0) {
				NotifyErrors(notifCounter);
			}
		}

		private int VerifyLocalValues() {
			int notificationCounter = 0;

			if (DecreaseStep < 0) {
				//Check bounds using its positive value
				DecreaseStep = Math.Abs(DecreaseStep);
			}

			//Custom checks
			if (DefaultFrequencyMult < MinFreqMult) {
				DefaultFrequencyMult = MinFreqMult;
			} else if (DefaultFrequencyMult > MaxFreqMult) {
				DefaultFrequencyMult = MaxFreqMult;
			}

			if (MinFreqMult > MaxFreqMult) {
				notificationCounter++;
				MaxFreqMult = MinFreqMult;
			}

			//Check upper and lower bounds of each value
			(float value, AutoModeValueLimit limits)[] AllValueLimits = [
				(DefaultFrequencyMult,          AutoModeLimits.DefaultFrequencyMult),
				(AvgEmployeeWaitTargetMillis,   AutoModeLimits.AvgEmployeeWaitTarget),
				(MinFreqMult,                   AutoModeLimits.MinFreqMult),
				(MaxFreqMult,                   AutoModeLimits.MaxFreqMult),
				(DecreaseStep,                  AutoModeLimits.DecreaseStep),
				(IncreaseStep,                  AutoModeLimits.IncreaseStep)
			];

			for (int i = 0; i < AllValueLimits.Length; i++) {
				var (value, limits) = AllValueLimits[i];

				var result = limits.CheckBounds(value);
				if (result.boundingCheck != BoundCheckResult.WithinBounds) {
					value = result.defaultValue;
					notificationCounter++;
				}
			}


			DecreaseStep = DecreaseStep * -1;

			return notificationCounter;
		}

		private void NotifyErrors(int notificationCounter) {
			bool IsCustomMode = ModConfig.Instance.EmployeeJobFrequencyMode.Value == EnumJobFrequencyMultMode.Auto_Custom;
			if (IsCustomMode) {
				TimeLogger.Logger.LogTimeWarningShowInGame($"{notificationCounter} value/s of Custom auto mode were " +
				$"outside allowed limits", LogCategories.JobSched);
			} else {
				//I fucked up.
				throw new InvalidOperationException($"{notificationCounter} value/s of the " +
					$"{ModConfig.Instance.EmployeeJobFrequencyMode.Value} mode were out of " +
					$"bounds. Check the AutoModes class.");
			}
		}
	

		public float Clamp(float jobFreqMultiplier) {
			if (float.IsNaN(jobFreqMultiplier)) {
				TimeLogger.Logger.LogTimeWarning("Calculated Job Frency Multiplier is NaN. " +
					$"Its been automatically adjusted to {DefaultFrequencyMult} before clampling, " +
					"but it should not have happened.", LogCategories.JobSched);
				jobFreqMultiplier = DefaultFrequencyMult;
			}

			return Mathf.Clamp(jobFreqMultiplier, MinFreqMult, MaxFreqMult);
		}

	}
}
