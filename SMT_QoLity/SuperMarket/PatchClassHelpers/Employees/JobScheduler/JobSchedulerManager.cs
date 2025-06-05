using System;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Timers;
using Damntry.UtilsUnity.Timers;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.Patches.EmployeeModule;
using UnityEngine;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.Helpers;
using SuperQoLity.SuperMarket.PatchClassHelpers.Components;
using System.ComponentModel;
using SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler.AutoMode.DataDefinition;
using Damntry.Utils.Timers.StopwatchImpl;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Employees.JobScheduler {

	public enum EnumJobFrequencyMultMode {
		[Description("Disabled")]
		Disabled,
		[Description("Auto - Prioritize system performance")]
		Auto_Performance,
		[Description("Auto - Balanced")]
		Auto_Balanced,
		[Description("Auto - Prioritize employee responsiveness")]
		Auto_Aggressive,
		[Description("Auto - Custom dynamic multiplier")]
		Auto_Custom,
		//[Description("Manual - Fixed multiplier")]
		//Manual,
	}


	public static class JobSchedulerManager {

		/* Base rules of the Auto modes:
			- Auto_Performance: Automatically adjusts job scheduling to prioritize system performance over employee productivity.
				Employees will never react faster than how they do in the base game. 
				It will quickly skip as many employee job actions as it can while trying not to restrict employees.
				Choose this option if your system is struggling with the game.
			- Auto_Balanced: Recommended option. Adjusts job scheduling to skip processing employee actions when there 
				is little job activity, and increase actions when employees are overworked, but trying not to bog the system down.
			- Auto_Aggressive: Adjust jobs scheduling for maximum employee responsiveness. It may skip jobs sparingly when
				there isnt much to do, but will quickly give employees as many actions as they need.
				Only use this if you have a strong PC.
		*/
		private enum JobModeType {
			Invalid,
			Automatic,
			//Manual,
			Other
		}

		//TODO 0 - If I increase the TimeScale, they idle a lot more and the multiplier wont increase for some reason.

		private const int LoopMultRounding = 3;

		private static SchedulerSessionVars sessionVars;

		public static bool IsJobSchedulerActive { get; private set; }

		public static event Action<float> OnNewJobFrequencyMultiplier;

		private class SchedulerSessionVars {

			public JobSchedulerProcessor JobSchedProcessor { get; set; }

			/// <summary>
			/// A limit on the number of times that we allow the employee process to surpass 
			/// the established time limit, before taking extra measures.
			/// </summary>
			public Lazy<PeriodicTimeLimitedCounter<UnityTimeStopwatch>> ProcessLimitCounter { get; set; }

			public EmployeesWaitTimers EmpWaitTimers { get; set; }

			/// <summary>The index of the employee being processed this loop.</summary>
			public int CurrentEmployeeId { get; set; }

			/// <summary>
			/// If we are on the initial loop of a FixedUpdate cycle, to force-update values.
			/// This is only true at the beginning of a new sesion.
			/// </summary>
			public bool InitialLoop { get; set; }

			/// <summary>
			/// The counter for FixedUpdate calls, so we can measure when a 
			/// full cycle of 1 second (50 calls by default) is completed.
			/// </summary>
			public int CurrentFixedUpdateCounter { get; set; }

			/// <summary>
			/// When the current loop counter is higher than the value of the non integer 
			/// multiplier for this cycle, this var accumulates the decimal remaining for the next loop.
			/// </summary>
			public float LoopDecimalSurplus { get; set; }

			/// <summary>The frequency multiplier used for the current FixedUpdate cycle.</summary>
			public float LoopMultiplierCycle { get; set; }

			/// <summary>
			/// The user selected mode to set a fixed or calculated frequency multiplier.
			/// </summary>
			public EnumJobFrequencyMultMode CurrentCycleJobFreqMode { get; set; }

			/// <summary>
			/// Overall type of job frequency multiplier mode selected by the user.
			/// </summary>
			public JobModeType CurrentCycleJobModeType { get; set; }

			/// <summary>
			/// If the error message for an inactive job scheduler, 
			/// should be shown this session.
			/// </summary>
			public bool ShowSchedulerInactiveError { get; set; }

			/// <summary>
			/// If the error message for exceeding the processing time
			/// of a single loop, should be shown in game for this session.
			/// </summary>
			public bool ShowInGameProcessTimeExceededError { get; set; }


			public SchedulerSessionVars() {
				ProcessLimitCounter = new Lazy<PeriodicTimeLimitedCounter<UnityTimeStopwatch>>(() =>
					new PeriodicTimeLimitedCounter<UnityTimeStopwatch>(true, 30, 30000, true));
				CurrentEmployeeId = 0;
				CurrentFixedUpdateCounter = 0;
				LoopDecimalSurplus = 0;
				LoopMultiplierCycle = 0;
				CurrentCycleJobFreqMode = EnumJobFrequencyMultMode.Disabled;
				CurrentCycleJobModeType = JobModeType.Invalid;
				InitialLoop = true;
				ShowSchedulerInactiveError = true;
				ShowInGameProcessTimeExceededError = true;
			}

			public void Destroy() {
				JobSchedProcessor?.Destroy();
			}

		}


		public static void InitializeJobSchedulerEvents() {
			//Dont initialize anything else here. The job scheduler
			//	only starts running if you are the host.

			WorldState.OnGameWorldChange += (gameEvent) => {
				if (gameEvent == GameWorldEvent.LoadingWorld) {
					LoadingWorldInit();
				} else if (gameEvent == GameWorldEvent.WorldStarted) {
					if (IsJobSchedulerActive) {
						FrequencyMult_Display.AllowDisplay();
					}
				} else if (gameEvent == GameWorldEvent.QuitOrMenu) {
					FrequencyMult_Display.Destroy();
					DestroyJobScheduler();
				}
			};
		}

		private static void LoadingWorldInit() {
			if (WorldState.CurrenOnlineMode == GameOnlineMode.Host) {
				sessionVars = new();
				FrequencyMult_Display.Initialize();
				IsJobSchedulerActive = true;
			}
		}

		private static void DestroyJobScheduler() {
			if (!IsJobSchedulerActive) {
				return;
			}

			IsJobSchedulerActive = false;

			sessionVars.Destroy();
			sessionVars = null;
		}

		public static bool IsAutoMode(EnumJobFrequencyMultMode jobFreqMode) {
			return GetJobModeTypeSetting(jobFreqMode) == JobModeType.Automatic;
		}

		public static bool IsAutoModeCurrentCycle() {
			return sessionVars.CurrentCycleJobModeType == JobModeType.Automatic;
		}

		private static JobModeType GetJobModeTypeSetting(EnumJobFrequencyMultMode jobFreqMode) =>
			jobFreqMode switch {
				EnumJobFrequencyMultMode.Disabled => JobModeType.Other,

				EnumJobFrequencyMultMode.Auto_Performance or
				EnumJobFrequencyMultMode.Auto_Balanced or
				EnumJobFrequencyMultMode.Auto_Aggressive or
				EnumJobFrequencyMultMode.Auto_Custom => JobModeType.Automatic,

				//EnumJobFrequencyMultMode.Manual => JobModeType.Manual,

				_ => JobModeType.Invalid
			};


		/// <summary>
		/// Handles the rate of execution of employee jobs. Each FixedUpdate, it
		/// may allow multiple employees to do jobs, or none at all, depending on settings.
		/// </summary>
		public static void ProcessEmployeeJobs(NPC_Manager __instance, int employeeCount) {
			if (!IsJobSchedulerActive) {
				if (sessionVars.ShowSchedulerInactiveError) {
					TimeLogger.Logger.LogTimeWarningShowInGame("The Job Scheduler is not currently active. " +
						"No employee will be processed.", LogCategories.JobSched);

					sessionVars.ShowSchedulerInactiveError = false;
				}

				return;
			}

			StopwatchDiag processTime = StopwatchDiag.StartNew();

			//Check if we are at the beginning of a new FixedUpdate cycle
			var fixedTime = CachedTimeValues.GetFixedTimeCachedValues();
			if (++sessionVars.CurrentFixedUpdateCounter >= fixedTime.fixedUpdateCycleMax || sessionVars.InitialLoop) {
				StartNewFixedUpdateCycle(fixedTime.fixedDeltaTime);
			}

			double maxProcessingTimeMillis = GetMaxAllowedProcessingTime(sessionVars.CurrentCycleJobFreqMode);

			int loopCounter = 0;
			int workSkipCounter = 0;

			int workSkipLimit = Math.Min(NPC_Manager.Instance.employeeParentOBJ.transform.childCount,
				fixedTime.fixedUpdateCycleMax);
			bool isLoopCounted = true;

			while (ShouldDoJob(ref loopCounter, ref workSkipCounter, sessionVars.LoopMultiplierCycle, isLoopCounted, workSkipLimit)) {

				isLoopCounted = EmployeeWaitTimeAndJobControl(__instance, sessionVars.CurrentEmployeeId,
					sessionVars.EmpWaitTimers, sessionVars.CurrentCycleJobModeType);

				if (++sessionVars.CurrentEmployeeId >= employeeCount) {
					sessionVars.CurrentEmployeeId = 0;
				}

				if (!IsProcessWithinTime(processTime, sessionVars.CurrentCycleJobFreqMode, maxProcessingTimeMillis)) {
					return;
				}
			}
		}

		private static void StartNewFixedUpdateCycle(float fixedDeltaTime) {
			//Update frequency mode from settings
			sessionVars.CurrentCycleJobFreqMode = ModConfig.Instance.EmployeeJobFrequencyMode.Value;
			sessionVars.CurrentCycleJobModeType = GetJobModeTypeSetting(sessionVars.CurrentCycleJobFreqMode);

			//Initialize employee timer calculations, or clear it in case the job frequency setting changed.
			if (sessionVars.CurrentCycleJobModeType == JobModeType.Automatic) {
				sessionVars.JobSchedProcessor ??= new JobSchedulerProcessor();
				sessionVars.EmpWaitTimers ??= new EmployeesWaitTimers();
			} else if (sessionVars.CurrentCycleJobModeType != JobModeType.Automatic && sessionVars.EmpWaitTimers != null) {
				sessionVars.JobSchedProcessor?.Destroy();
				sessionVars.JobSchedProcessor = null;
				sessionVars.EmpWaitTimers = null;
			}

			//Get loop multiplier for this cycle.
			sessionVars.LoopMultiplierCycle = GetLoopMultiplier(sessionVars.CurrentCycleJobFreqMode, sessionVars.CurrentCycleJobModeType,
				sessionVars.EmpWaitTimers, fixedDeltaTime);

			if (OnNewJobFrequencyMultiplier != null) {
				OnNewJobFrequencyMultiplier(sessionVars.LoopMultiplierCycle);
			}

			sessionVars.CurrentFixedUpdateCounter = 0;
			sessionVars.LoopDecimalSurplus = 0f;
			sessionVars.InitialLoop = false;
		}

		private static float GetLoopMultiplier(EnumJobFrequencyMultMode jobFreqMode, JobModeType jobModeType,
				EmployeesWaitTimers empWaitTimers, float fixedDeltaTime) {

			float loopMultiplier = jobModeType switch {
				JobModeType.Other =>
					1f,

				//JobModeType.Manual =>
				//	ModConfig.Instance.EmployeeJobFrequencyManualMultiplier.Value,

				JobModeType.Automatic =>
					sessionVars.JobSchedProcessor.CalculateNextJobFreqMultiplier(jobFreqMode, empWaitTimers, fixedDeltaTime),

				JobModeType.Invalid =>
					throw new InvalidOperationException("The job frequency mode hasnt been set."),

				_ => throw new NotImplementedException($"The job frequency mode \"{jobModeType}\" is not implemented.")
			};

			return (float)Math.Round(loopMultiplier, LoopMultRounding);
		}

		private static bool EmployeeWaitTimeAndJobControl(NPC_Manager __instance, int employeeIndex,
				EmployeesWaitTimers empWaitTimers, JobModeType jobModeType) {
			if (employeeIndex >= __instance.employeeParentOBJ.transform.childCount) {
				return false;
			}

			GameObject employeeObj = __instance.employeeParentOBJ.transform.GetChild(employeeIndex).gameObject;
			NPC_Info employee = employeeObj.GetComponent<NPC_Info>();

			bool didWork = EmployeeJobAIPatch.EmployeeNPCControl(__instance, employeeObj, employee);

			if (jobModeType == JobModeType.Automatic) {
				//Begin to time how long until this employee can act again. If it already
				//	had a timer running, stores the delay and starts timing all over again.
				empWaitTimers.StartTimer(employee.netId, didWork);
			}


			return didWork;
		}

		private static bool ShouldDoJob(ref int loopCounter, ref int workSkipCounter, float loopMultiplierCycle, bool isLoopCounted, int workSkipLimit) {
			if (!isLoopCounted) {
				if (++workSkipCounter > workSkipLimit) {
					//Avoid infinite loop if no employee needs to do work.
					workSkipCounter = 0;
					return false;
				}

				return true;
			}

			bool shouldDoJob = false;

			if (loopCounter < (int)loopMultiplierCycle) {
				shouldDoJob = true;
			} else {
				//Since maxLoopCount can have decimal values, and even a value < 1 (so there is less than the 50
				//	employee job actions per second), we carry the decimals over to see if we accumulated enough.
				if (!loopMultiplierCycle.IsInteger()) {
					sessionVars.LoopDecimalSurplus += loopMultiplierCycle % Math.Max((int)loopMultiplierCycle, 1);

					if (sessionVars.LoopDecimalSurplus >= 1) {
						sessionVars.LoopDecimalSurplus--;
						shouldDoJob = true;
					}
				}
			}

			loopCounter++;

			return shouldDoJob;
		}

		private static double GetMaxAllowedProcessingTime(EnumJobFrequencyMultMode jobFreqMode) {
			//To maintain >= 60fps, each frame must take < 16.6~ ms. A bit less than 7ms for 144fps.
			//	I dont know how much every other process takes relative to NPC job handling on average,
			//	so these values came from quite a bit of trial and error.
			return jobFreqMode switch {
				EnumJobFrequencyMultMode.Auto_Performance => PerformanceMode.MaxAllowedProcessingTime,
				EnumJobFrequencyMultMode.Auto_Balanced => BalancedMode.MaxAllowedProcessingTime,
				EnumJobFrequencyMultMode.Auto_Aggressive => AggressiveMode.MaxAllowedProcessingTime,
				EnumJobFrequencyMultMode.Auto_Custom => CustomMode.MaxAllowedProcessingTime,
				//EnumJobFrequencyMultMode.Manual => ModConfig.Instance.EmployeeJobManualMaxProcessTime.Value,
				_ => -1
			};
		}

		private static bool IsProcessWithinTime(IStopwatch processTime, EnumJobFrequencyMultMode jobFreqMode,
				double MaxEmployeeProcessingTimeMillis) {
			if (jobFreqMode == EnumJobFrequencyMultMode.Disabled) {
				return true;
			}
			if (MaxEmployeeProcessingTimeMillis <= 0) {
				return true;    //Time limiters are disabled
			}
			if (!WorldState.IsGameWorldStarted) {
				// FixedUpdate is active while the game is still loading, but we dont want to send performance
				// warnings to the user while the cpu is doing work, where longer process times are normal.
				return false;
			}

			//TODO 1 - In the huge save, this is registering times of up to 4 millis with no employees assigned.
			//	It is because of the skips, but considering the maximum amount of skips is 50, and single runs
			//	go for less than 0,01ms, I dont undestand how is it approaching 4 millis so much.
			//LOG.TEMPWARNING($"Time passed: {processTime.ElapsedMillisecondsPrecise} - Max: {MaxEmployeeProcessingTimeMillis}");
			if (processTime.ElapsedMillisecondsPrecise >= MaxEmployeeProcessingTimeMillis) {
				
				if (!sessionVars.ProcessLimitCounter.Value.TryIncreaseCounter()) {
					string warningMsgEnd;

					//if (jobFreqMode == EnumJobFrequencyMultMode.Manual) {
					//	warningMsgEnd = $"It is recommended to decrease the value of the setting " +
					//		 "\"{ModConfig.Instance.EmployeeJobFrequencyManualMultiplier.Definition.Key}\".";
					if (jobFreqMode == EnumJobFrequencyMultMode.Auto_Custom) {
						warningMsgEnd = $"It is recommended to adjust the settings in the " +
							$"\"{ModConfig.Instance.CustomAvgEmployeeWaitTarget.Definition.Section}\" section.";
					} else if (sessionVars.CurrentCycleJobModeType == JobModeType.Automatic) {
						warningMsgEnd = $"It is recommended to select a different " +
							$"\"{ModConfig.Instance.EmployeeJobFrequencyMode.Definition.Key}\" in the settings.";
					} else {
						//Not currently possible, just a future safeguard.
						warningMsgEnd = $"It is recommended to select a " +
							$"\"{ModConfig.Instance.EmployeeJobFrequencyMode.Definition.Key}\" in the settings.";
					}

					string warningMsgStart = "Processing employee actions is taking too much time and its " +
						$"being automatically limited by {MyPluginInfo.PLUGIN_NAME} to improve performance. ";

					bool showInGame = ModConfig.Instance.EnabledDevMode.Value && !sessionVars.ShowInGameProcessTimeExceededError;
					TimeLogger.Logger.LogTime(LogTier.Warning, warningMsgStart + warningMsgEnd,
						LogCategories.PerfTest, showInGame);

					if (showInGame) {
						sessionVars.ShowInGameProcessTimeExceededError = true;
					}
				}

				return false;
			}

			return true;
		}


		/*	Unused. Creating a distribution seemed more logical, but in the end it just
			overcomplicates the code for little reason, compared to just using the multiplier
			directly and carrying over the decimal part. I dont really need precision.

		private readonly static double Integerizer = Math.Pow(10, LoopMultRounding);

		private static float GetLoopExecutionDistribution(EmployeeJobFrequencyModeEnum jobFreqMode) {
			float multiplier = GetLoopMultiplier(jobFreqMode);

			if (multiplier != 1f) {
				int n1 = (int)(multiplier * Integerizer);
				int n2 = (int)(1 * Integerizer);
				ulong maxDiv = Damntry.Utils.Maths.MathMethods.GreatestCommonDivisor((ulong)n1, (ulong)n2);
				n1 = (int)((ulong)n1 / maxDiv);
				n2 = (int)((ulong)n2 / maxDiv);

				//Now create normal distribution.
			} else {
				
			}
		}
		*/

	}
}
