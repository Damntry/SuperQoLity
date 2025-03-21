using SuperQoLity.SuperMarket.ModUtils;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode.DataDefinition {

	//After all the performance optimizations done by the dev and me, these MaxAllowedProcessingTime
	//	values are too high and they hardly could ever happen. I ll keep them for now as they are.

	public class PerformanceMode : AutoModeData {
		public static readonly float MaxAllowedProcessingTime = 3f;
		public PerformanceMode() : base(
			jobFreqMode: EnumJobFrequencyMultMode.Auto_Performance,
			defaultFrequencyMult: 1f,
			avgEmployeeWaitTargetMillis: 150f,
			minFreqMult: AutoModeLimits.MinFreqMult.MinLimit,
			maxFreqMult: 1f,
			decreaseStep: 0.375f,
			increaseStep: 0.125f
		) { }
	}
	public class BalancedMode : AutoModeData {
		public static readonly float MaxAllowedProcessingTime = 5f;
		public BalancedMode() : base(
			jobFreqMode: EnumJobFrequencyMultMode.Auto_Balanced,
			defaultFrequencyMult: 1f,
			avgEmployeeWaitTargetMillis: 100f,
			minFreqMult: 0.33f,
			maxFreqMult: 8f,
			decreaseStep: 0.90f,
			increaseStep: 0.90f
		) { }
	}
	public class AggressiveMode : AutoModeData {
		public static readonly float MaxAllowedProcessingTime = 12f;
		public AggressiveMode() : base(
			jobFreqMode: EnumJobFrequencyMultMode.Auto_Aggressive,
			defaultFrequencyMult: 1f,
			avgEmployeeWaitTargetMillis: -1f,
			minFreqMult: 0.5f,
			maxFreqMult: AutoModeLimits.MaxFreqMult.MaxLimit,
			decreaseStep: 3.5f,
			increaseStep: 5f
		) { }
	}

	public class CustomMode : AutoModeData {
		//If just processing employees takes more than this, might as well go back to play minesweeper.
		public static readonly float MaxAllowedProcessingTime = 1000 / 60f;	//60 fps if cpu time was only employee processing.
		public CustomMode() : base(
			jobFreqMode: EnumJobFrequencyMultMode.Auto_Custom,
			defaultFrequencyMult: 1f,
			avgEmployeeWaitTargetMillis: ModConfig.Instance.CustomAvgEmployeeWaitTarget.Value,
			minFreqMult: ModConfig.Instance.CustomMinimumFrequencyMult.Value,
			maxFreqMult: ModConfig.Instance.CustomMaximumFrequencyMult.Value,
			decreaseStep: ModConfig.Instance.CustomMaximumFrequencyReduction.Value,
			increaseStep: ModConfig.Instance.CustomMaximumFrequencyIncrease.Value
		) { }
	}

}
