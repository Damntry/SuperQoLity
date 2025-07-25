using System;
using System.Collections.Generic;
using Damntry.Utils.Collections.Queues;
using Damntry.Utils.Collections.Queues.Interfaces;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.RestockMatch.Helpers {

	public class RestockJob<T> where T : struct {

		protected Dictionary<RestockPriority, ICommonQueue<T>> restockJobs;

		private int jobCount;

		private Func<ICommonQueue<T>> getNewQueueInstance;

		public RestockJob() {
			restockJobs = new();
			getNewQueueInstance = () => new CommonConcurrentQueue<T>();
			InitializePriorities();
		}

		public RestockJob(int fixedCapacity) {
			restockJobs = new();
			getNewQueueInstance = () => new ConcurrentFixedCapacityQueue<T>(fixedCapacity, false);
			InitializePriorities();
		}

		public void InitializePriorities() {
			RestockPriority priority;
			ICommonQueue<T> newQueueInstance;

			//Initialize each priority Queue
			for (int i = 0; i < ThresholdHelper.ThresholdCount; i++) {
				priority = ThresholdHelper.ThresholdEnumValues[i];
				newQueueInstance = getNewQueueInstance();
				if (!restockJobs.ContainsKey(priority)) {
					restockJobs.Add(priority, newQueueInstance);
				} else {
					restockJobs[priority] = newQueueInstance;
				}
				
			}
			jobCount = 0;
		}

		public bool HasJobsLeft => jobCount > 0;

		public int Count => jobCount;


		public bool TryAddJob(RestockPriority restockThreshold, T jobInfo) {
			bool isAdded = restockJobs[restockThreshold].TryEnqueue(jobInfo);
			if (isAdded) {
				jobCount++;
			}

			return isAdded;
		}

		public bool TryExtractPriorityJob(out T job, out RestockPriority restockPriority) {
			foreach (var priorityJob in restockJobs) {
				ICommonQueue<T> jobQueue = priorityJob.Value;
				if (jobQueue.TryDequeue(out job)) {
					jobCount--;
					restockPriority = priorityJob.Key;
					return true;
				}
			}
			job = default;
			restockPriority = RestockPriority.ShelfFull;	//Shouldnt be used if we return false, but just to be safe.
			return false;
		}

		
		/// <remarks>
		/// THIS METHOD IS NOT THREAD-SAFE to avoid locking in a case that is never expected to need it. 
		/// Do not use if another thread could try to access this RestockJob instance.
		/// </remarks>
		public void ClearJobs() {
			InitializePriorities();
		}
		

	}
}
