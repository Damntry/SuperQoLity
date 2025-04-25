using Damntry.Utils.Logging;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Helpers;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch.Models;
using SuperQoLity.SuperMarket.PatchClassHelpers.TargetMarking;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.RestockMatch {

	public class RestockJobsManager {

		public readonly static float RestockProcessInterval = 3f;

		public readonly static float RestockProcessNotPossibleInterval = 0.5f;

		//This will be useful once I implement that multiple restockers can target the same product shelf slot.
		public readonly static int MaxStorageJobsPerShelf = 1;

		/// <summary>
		/// Fixed number of jobs to generate per restocker employee.
		/// </summary>
		public readonly static float StaticQueuedJobsPerEmployee = 1.25f;

		/// <summary>
		/// Extra number of jobs to generate per restocker employee based on speed factors.
		/// </summary>
		public readonly static float DynamicQueuedJobsPerEmployee = 0.9f;

		/// <summary>
		/// Extra fixed number of jobs to generate.
		/// Aka: "The way I calculate how many jobs to keep is scuffed so drop some more on that thang".
		/// </summary>
		public readonly static int ExtraQueuedJobsBuffer = 5;

		/// <summary>
		/// Multiplier over the calculated max quantity of available jobs, to fill each priority of non
		/// critical restock jobs. To be processed later in case the max prioritized ones werent enough.
		/// </summary>
		public readonly static float NonCriticalJobsPerPriorityMultiplier = 1.25f;


		private static RestockJob<RestockJobInfo> availableRestockJobs;

		public static void Initialize() {
			availableRestockJobs = new();
		}


		public static int JobCount => availableRestockJobs.Count;


		public enum JobFindStatus {
			FoundJob,
			JobNotValid,
			NoMoreJobs
		}

		public static bool GetAvailableRestockJob(NPC_Manager __instance, out RestockJobInfo restockJob) {
			//Performance.Start("GetAvailableRestockJob");
			restockJob = RestockJobInfo.Default;
			JobFindStatus jobFindStatus;

			do {
				if (availableRestockJobs.TryExtractPriorityJob(out RestockJobInfo possibleRestockJob, out _)) {
					//Check that both product shelf and storageSlotData slots are not in use by
					//	another employee, and that their contents are still valid.
					if(TargetMatching.RefreshAndCheckTargetProductShelf(
							__instance, possibleRestockJob, possibleRestockJob.MaxProductsPerRow)
							&& TargetMatching.RefreshAndCheckTargetStorage(__instance, possibleRestockJob)) {
						jobFindStatus = JobFindStatus.FoundJob;
						restockJob = possibleRestockJob;
					} else {
						jobFindStatus = JobFindStatus.JobNotValid;
						LOG.TEMPDEBUG_FUNC(() => $"GetAvailableRestockJob - Job was not valid anymore. " +
							$"Job info - {possibleRestockJob}.", EmployeeJobAIPatch.LogEmployeeActions);
					}
				} else {
					jobFindStatus = JobFindStatus.NoMoreJobs;
				}
			} while (jobFindStatus == JobFindStatus.JobNotValid);

			//Performance.StopAndLog("GetAvailableRestockJob");
			RestockJobInfo restockJobLog = restockJob;
			LOG.TEMPDEBUG_FUNC(() => $"GetAvailableRestockJob result: {jobFindStatus}, " +
				$"with job info - {restockJobLog}.", EmployeeJobAIPatch.LogEmployeeActions);
			return jobFindStatus == JobFindStatus.FoundJob;
		}

		public static void AddAvailableJob(RestockPriority restockPriority,
				ShelfSlotData productShelfSlotData, ShelfSlotData storageSlotData, int maxProductsPerRow) {

			RestockJobInfo restockJob = new(
				productShelfSlotData.ToProdShelfSlotInfo(),
				storageSlotData.ToStorageSlotInfo(),
				maxProductsPerRow
			);

			availableRestockJobs.TryAddJob(restockPriority, restockJob);
			LOG.TEMPDEBUG_FUNC(() => $"Added new available job with info - {restockJob}.", 
				EmployeeJobAIPatch.LogEmployeeActions);
		}

		public static void ClearJobs() {
			availableRestockJobs.ClearJobs();
		}


		/*	Not worth it in the end. Makes the whole process around 10-20% faster, but adds
			more complexity and employees get more false positives the older a job is which 
			destroys the performance gains.
		
		private record struct JobEntry(float Timestamp, RestockJobInfo Job);

		private readonly static float RestockJobForcedRemovalPercent = 0.2f;

		private readonly static int RestockJobExpirationSeconds = 8;

		public static void ClearJobs() {
			Performance.Start("0. ClearJobs");
			availableRestockJobs.ForEachPriority(
				(p, q) => RemoveOlderJobsFromQueue(q)
			);
			Performance.StopAndLog("0. ClearJobs");
		}

		private static int RemoveOlderJobsFromQueue(ConcurrentQueue<JobEntry> jobQueue) {
			int jobCountDifference = 0;
			if (!jobQueue.IsEmpty) {
				int forcedRemoveCount = (int)(MaxQueuedJobsPerEmployee * RestockJobForcedRemovalPercent);
				float currentTime = Time.time;

				while (!jobQueue.IsEmpty) {
					if (jobQueue.TryPeek(out var restockJob)) {
						if (forcedRemoveCount > 0 || restockJob.Timestamp + RestockJobExpirationSeconds < currentTime) {
							jobQueue.TryDequeue(out _);
							forcedRemoveCount--;
							jobCountDifference--;
						} else {
							//Queue is naturally sorted from newest to oldest, so once we
							//	find a job not old enough, the rest need no removal.
							break;
						}
					} else {
						break;
					}
				}
			}
			return jobCountDifference;
		}
		*/

	}
}
