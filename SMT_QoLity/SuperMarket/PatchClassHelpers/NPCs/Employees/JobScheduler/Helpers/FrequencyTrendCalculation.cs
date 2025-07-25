using System;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.JobScheduler.AutoMode;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.JobScheduler.Helpers {

	public class FrequencyTrendCalculation {

		/// <summary>
		/// Current Average time value at which we already set the maximum strength
		/// multiplier for increasing or decreasing the frequency multiplier.
		/// </summary>
		private static readonly int MaxAvgTimePoint = 750;

		/// <summary>Precalculated square of MaxAvgTimePoint for performance.</summary>
		private readonly int MaxAvgTimePointSquared;

		private readonly float DampeningBufferMult;

		private float coefficientCached;

		public FrequencyTrendCalculation() {
			MaxAvgTimePointSquared = MaxAvgTimePoint * MaxAvgTimePoint;
			DampeningBufferMult = 0.15f;
			coefficientCached = -1;
		}

		public float CalculateJobFreqStepValue(float averageWaitTimeMillis, float lastAvgWaitTime,
			float lastJobFreqMult, float fixedDeltaTime, AutoModeData autoModeData) {

			float strengthMult;
			float stepValue;

			//This is where we set the baseline of average time performance.
			//	The minimum possible average time is Time.fixedDeltaTime. By default with 50 Fixed updates
			//	per second, this means that if all employees were to not skip work: 1000 / 50 = 20ms.
			//	We add a bit on top as a cushion, and anything below that, is considered as if there
			//	was no wait time at all.
			float minimumBaseline = fixedDeltaTime * 1000 + 5;

			float avgWaitTimeTarget;
			if (autoModeData.AvgEmployeeWaitTargetMillis >= 0) {
				//Take predefined target for this auto mode.
				avgWaitTimeTarget = autoModeData.AvgEmployeeWaitTargetMillis;
			} else {
				avgWaitTimeTarget = minimumBaseline;
				//Its still possible to achieve less than baseline values if the multiplier is high
				//	enough that all employees are processed in a single loop more than once.
				minimumBaseline = 0;
			}

			//How far are we from the avg wait target 
			float avgTimeTargetDiff = averageWaitTimeMillis - avgWaitTimeTarget;
			bool isAvgWaitBelowTarget = avgTimeTargetDiff <= 0;

			//Check that we are not already at the frequency limit.
			if (isAvgWaitBelowTarget && lastJobFreqMult <= autoModeData.MinFreqMult ||
					!isAvgWaitBelowTarget && lastJobFreqMult >= autoModeData.MaxFreqMult) {

				return 0;   //No margin left to push frequency any further.
			}

			if (isAvgWaitBelowTarget) {
				strengthMult = CalculateStrengthMultiplier(averageWaitTimeMillis, minimumBaseline, avgWaitTimeTarget,
						avgWaitTimeTarget, inversed: true);

				stepValue = autoModeData.DecreaseStep;
			} else {
				strengthMult = CalculateStrengthMultiplierCachedMax(averageWaitTimeMillis, avgWaitTimeTarget);

				stepValue = autoModeData.IncreaseStep;
			}

			//Apply a small level of linear dampening if the previous average wait
			//	time was on the opposite trend, since in that case, we dont want to change
			//	the frequency too much when its possible that its going to bounce back.
			float dampening = CalculateDampening(isAvgWaitBelowTarget, averageWaitTimeMillis, avgWaitTimeTarget,
				lastAvgWaitTime, minimumBaseline, MaxAvgTimePoint);

			return stepValue * strengthMult * dampening;
		}

		/// <summary>
		/// Parabolic formula to calculate the strength multiplier, from 0 (plus a small decimal 
		/// value from the baseline calculated as a coefficient) to 1.
		///	Small differences in avg time result in a very small multiplier, and as this difference
		///	increases, so does the multiplier at a faster pace as it aproaches 1.
		///	This makes it so bigger differences make the system want to overcompensate strongly.
		/// </summary>
		/// <param name="avgTimeTargetDiff">The difference between the current avg time and its preferred target.</param>
		/// <param name="avgWaitTimeTarget">
		/// The maximum avg time value at which we consider that the last freq. multiplier was enough.
		/// </param>
		/// <remarks>This method can only be used for cases where the avg wait time is ABOVE target.</remarks>
		private float CalculateStrengthMultiplierCachedMax(float avgTimeTargetDiff, float avgWaitTimeTarget) {
			return CalculateStrengthMultiplier(avgTimeTargetDiff, avgWaitTimeTarget, avgWaitTimeTarget,
				MaxAvgTimePoint, inversed: false, saveCoefficient: true, coefficientCached, MaxAvgTimePointSquared);
		}

		/// <param name="value">The value from which the multiplier will be calculated.</param>
		/// <param name="Y_valueAtStartPoint">
		/// The Y value, from which the parabole will start, and will correlate to a multiplier with value 0.
		/// This value is absolute, related to the maxAvgTimePoint, and internally ratioed to the 0 to 1 multiplier.
		/// </param>
		/// <param name="Y_ValueAt_X_Intersect">
		/// The Y value at which the parabole will intersect with the central X axis.
		/// This value is absolute, related to the maxAvgTimePoint, and internally ratioed to the 0 to 1 multiplier.
		/// </param>
		/// <param name="maxPointX">Value at which the multiplier will reach one of its extremes.</param>
		/// <param name="saveCoefficient"></param>
		/// <param name="coefficient"></param>
		/// <param name="maxAvgTimePointSquared"></param>
		/// <returns></returns>
		private float CalculateStrengthMultiplier(float value, float Y_valueAtStartPoint,
				float Y_ValueAt_X_Intersect, float maxPointX, bool inversed = false, bool saveCoefficient = false,
				float coefficient = float.MinValue, float maxAvgTimePointSquared = float.MinValue) {

			if (maxAvgTimePointSquared == float.MinValue) {
				maxAvgTimePointSquared = maxPointX * maxPointX;
			}
			if (coefficient == float.MinValue) {
				coefficient = maxAvgTimePointSquared / (1 - Y_valueAtStartPoint / maxPointX) - maxAvgTimePointSquared;
				if (saveCoefficient) {
					coefficientCached = coefficient;
				}
			}
			float inversion = inversed ? -1 : 1;

			return value * value * inversion / (maxAvgTimePointSquared + coefficient) + Y_ValueAt_X_Intersect / maxPointX;
		}

		private float CalculateDampening(bool isAvgWaitBelowTarget, float averageWaitTimeMillis,
				float avgWaitTimeTarget, float lastAvgWaitTime, float minimumBaseline, float maxAvgTimePoint) {

			bool needsDampening = false;
			float dampening = 1f;

			float oppositeExtremeValue = isAvgWaitBelowTarget ? maxAvgTimePoint : minimumBaseline;

			float dampeningLimit = avgWaitTimeTarget + (oppositeExtremeValue - avgWaitTimeTarget) * DampeningBufferMult;
			if (isAvgWaitBelowTarget) {
				needsDampening = lastAvgWaitTime > dampeningLimit;
			} else {
				needsDampening = lastAvgWaitTime < dampeningLimit;
			}

			if (needsDampening) {
				float coef = (1f - 0.75f) / (maxAvgTimePoint - minimumBaseline);
				dampening = 1 - coef * Math.Abs(lastAvgWaitTime - averageWaitTimeMillis);
			}

			return dampening;
		}

	}
}
