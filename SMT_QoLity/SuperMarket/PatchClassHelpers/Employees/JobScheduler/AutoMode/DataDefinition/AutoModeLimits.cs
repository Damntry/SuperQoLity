namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode.DataDefinition {

	public struct AutoModeLimits {

		private const float Frequency_MinLimit = 0.20f;
		private const float Frequency_MaxLimit = 25f;


		public static readonly AutoModeValueLimit DefaultFrequencyMult = new(
			minLimit: Frequency_MinLimit, 
			maxLimit: Frequency_MaxLimit
		);

		public static readonly AutoModeValueLimit AvgEmployeeWaitTarget = new(
			minLimit: -1,
			maxLimit: 1000f
		);

		public static readonly AutoModeValueLimit MinFreqMult = new(
			minLimit: Frequency_MinLimit,
			maxLimit: 10f
		);

		public static readonly AutoModeValueLimit MaxFreqMult = new(
			minLimit: 0.5f,
			maxLimit: Frequency_MaxLimit
		);

		public static readonly AutoModeValueLimit DecreaseStep = new(
			minLimit: 0.1f,
			maxLimit: 5f
		);

		public static readonly AutoModeValueLimit IncreaseStep = new(
			minLimit: 0.1f,
			maxLimit: 5f
		);

	}


	public enum BoundCheckResult {
		LowerBoundBreach,
		UpperBoundBreach,
		WithinBounds
	}

	public class AutoModeValueLimit {

		public float MinLimit { get; init; }
		public float MaxLimit { get; private set; }

		public AutoModeValueLimit(float minLimit, float maxLimit) {
			this.MinLimit = minLimit;
			this.MaxLimit = maxLimit;
		}

		public (BoundCheckResult boundingCheck, float defaultValue) CheckBounds(float value) {
			BoundCheckResult checkRes = BoundCheckResult.WithinBounds;
			float defaultValue = 0f;

			if (value < MinLimit) {
				checkRes = BoundCheckResult.LowerBoundBreach;
				defaultValue = MinLimit;
			} else if (value > MaxLimit) {
				checkRes = BoundCheckResult.UpperBoundBreach;
				defaultValue = MaxLimit;
			}

			return (checkRes, defaultValue);
		}

	}

}
