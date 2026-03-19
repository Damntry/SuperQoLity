using SuperQoLity.SuperMarket.ModUtils.UI;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.AutoMode;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.Helpers;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler {

	public class JobSchedulerProcessor {


		private FrequencyTrendCalculation freqTrendCalc;

		/// <summary>Average wait time of employees processed in the previous cycle.</summary>
		private float lastAvgWaitTime;

		private float lastJobFreqMult;


		public JobSchedulerProcessor() {
			lastAvgWaitTime = -1;
			freqTrendCalc = new FrequencyTrendCalculation();

			AutoModeProcessor.Initialize();
        }


		public float CalculateNextJobFreqMultiplier(EnumJobFrequencyMultMode jobFreqMode, 
				NPCType npcType, NpcWaitTimers npcWaitTimers, float fixedDeltaTime) {

            AutoModeProcessor.Instance.SetAutoModeData(jobFreqMode);

            float averageWaitTimeMillis = npcWaitTimers.CalculateAvgWaitTimesAndReset();

			float newJobFreqMult = GetCalculatedJobFreqMultiplier(averageWaitTimeMillis, jobFreqMode, fixedDeltaTime, npcType);

			UIPanelHandler.AddNewHistoricValue(averageWaitTimeMillis, newJobFreqMult);

			lastAvgWaitTime = averageWaitTimeMillis;
			lastJobFreqMult = newJobFreqMult;
			return newJobFreqMult;
		}

		private float GetCalculatedJobFreqMultiplier(float averageWaitTimeMillis,
				EnumJobFrequencyMultMode jobFreqMode, float fixedDeltaTime, NPCType npcType) {

			AutoModeData autoModeData = AutoModeProcessor.Instance.AutoModeData;


            if (lastAvgWaitTime == -1) {
				//First check after starting a game.
				return autoModeData.DefaultFrequencyMult;
			}

			//Calculate by how much we ll increase or decrease the previous frequency
			float jobFreqStepValue = freqTrendCalc.CalculateJobFreqStepValue(averageWaitTimeMillis, 
				lastAvgWaitTime, lastJobFreqMult, fixedDeltaTime, autoModeData, npcType);

			float newJobFreqMult = lastJobFreqMult + jobFreqStepValue;

			return autoModeData.Clamp(newJobFreqMult);
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
