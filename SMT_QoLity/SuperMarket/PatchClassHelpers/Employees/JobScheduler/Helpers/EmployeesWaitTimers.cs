using System.Collections.Generic;
using Damntry.UtilsUnity.Timers;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.Helpers {

	/// <summary>
	/// Measures the average amount of time that employees are waiting to have a chance to do their actions.
	/// </summary>
	public class EmployeesWaitTimers {

		private Dictionary<uint, UnityTimeStopwatch> waitTimers;

		private object syncLock;

		private double totalWaitElapsedMillis = 0d;

		private int totalHits = 0;


		public EmployeesWaitTimers() {
			waitTimers = new();
			syncLock = new();
		}

		public void StartTimer(uint netId, bool includeResults) {
			if (waitTimers.ContainsKey(netId)) {
				UnityTimeStopwatch unitySW = waitTimers[netId];
				lock (syncLock) {
					if (unitySW.IsRunning) {
						unitySW.Stop();

						if (includeResults) {
							totalWaitElapsedMillis += unitySW.ElapsedMillisecondsPrecise;
							totalHits++;
						}
					}

					unitySW.Restart();
				}
			} else {
				waitTimers.Add(netId, UnityTimeStopwatch.StartNew());
			}
		}

		public float CalculateAvgWaitTimesAndReset() {

			double averageWaitTimeMillis = 0;

			lock (syncLock) {
				if (totalWaitElapsedMillis > 0 && totalHits > 0) {
					averageWaitTimeMillis = (float)totalWaitElapsedMillis / totalHits;
				}
				totalWaitElapsedMillis = 0d;
				totalHits = 0;
			}

			return (float)averageWaitTimeMillis;
		}

	}


}
