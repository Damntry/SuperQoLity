using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Timers.StopwatchImpl;
using Damntry.UtilsUnity.Timers;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Employees.TargetMarking;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.AutoMode.DataDefinition;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.Helpers;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;
using SuperQoLity.SuperMarket.Standalone.Components;
using System;
using System.ComponentModel;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler {

	public enum EnumJobFrequencyMultMode {
		[Description("Disabled")]
		Disabled,
		[Description("Auto - Prioritize system performance")]
		Auto_Performance,
		[Description("Auto - Balanced")]
		Auto_Balanced,
		[Description("Auto - Prioritize NPC responsiveness")]
		Auto_Aggressive,
		[Description("Auto - Custom dynamic multiplier")]
		Auto_Custom,
	}


	public static class JobSchedulerManager {

        /* Base rules of the Auto modes:
			- Auto_Performance: Automatically adjusts job scheduling to prioritize system performance over NPC productivity.
				NPCs will never react faster than how they do in the base game. 
				It will quickly skip as many NPC job actions as it can while trying not to restrict them.
				Choose this option if your system is struggling with the game.
			- Auto_Balanced: Recommended option. Adjusts job scheduling to skip processing NPC actions when there 
				is little job activity, and increase actions when NPCs are overworked, but trying not to bog the system down.
			- Auto_Aggressive: Adjust jobs scheduling for maximum NPC responsiveness. It may skip jobs sparingly when
				there isnt much to do, but will quickly give NPCs as many actions as they need.
				Only use this if you have a strong PC.
		*/

        //All calculations were done with a TimeScale of 1 in mind, since all timing is in real time and not Unity time.
		//	In higher TimScales they ll idle a lot more and the multiplier wont increase, which is intended or it
		//	would use too much cpu to try and keep up, in a TimeScale that is already demanding a lot from the system.

        private const int LoopMultRounding = 3;

		private static SchedulerSessionVars sessionVars;

		public static bool IsJobSchedulerActive { get; private set; }

		public static event Action<float, float> OnNewJobFrequencyMultiplier;

        private static bool IsEmployeeModuleEnabled;

        private static bool IsCustomerModuleEnabled;
		
		
        public static void InitializeJobSchedulerEvents() {
            //Dont initialize anything else here. The job scheduler
            //	only starts running if you are the host.

            IsEmployeeModuleEnabled = ModConfig.Instance.EnableEmployeeChanges.Value;
            IsCustomerModuleEnabled = ModConfig.Instance.EnableCustomerChanges.Value;

			WorldState.OnGameWorldChange += HandleWorldEvents;
        }

		private static void HandleWorldEvents(GameWorldEvent gameEvent) {
            if (gameEvent == GameWorldEvent.LoadingWorld) {
                LoadingWorldInit();
            } else if (gameEvent == GameWorldEvent.WorldLoaded) {
                if (IsJobSchedulerActive) {
                    FrequencyMult_Display.AllowDisplay();
                }
            } else if (gameEvent == GameWorldEvent.QuitOrMenu) {
                FrequencyMult_Display.Destroy();
                DestroyJobScheduler();
            }
        }

		private static void LoadingWorldInit() {
			if (WorldState.CurrentOnlineMode == GameOnlineMode.Host) {
				sessionVars = new();

                FrequencyMult_Display.Initialize();
				IsJobSchedulerActive = true;
			}
			EmployeeTargetReservation.ClearAll();
		}

		public static void DisableJobScheduler() {
            WorldState.OnGameWorldChange -= HandleWorldEvents;
			DestroyJobScheduler();
        }

		private static void DestroyJobScheduler() {
			if (!IsJobSchedulerActive) {
				return;
			}

			IsJobSchedulerActive = false;

			sessionVars.DestroyAutoModeData();
			sessionVars = null;
		}

		private static JobModeType GetJobModeTypeSetting(EnumJobFrequencyMultMode jobFreqMode) =>
			jobFreqMode switch {
				EnumJobFrequencyMultMode.Disabled => JobModeType.Other,

				EnumJobFrequencyMultMode.Auto_Performance or
				EnumJobFrequencyMultMode.Auto_Balanced or
				EnumJobFrequencyMultMode.Auto_Aggressive or
				EnumJobFrequencyMultMode.Auto_Custom => JobModeType.Automatic,

				_ => JobModeType.Invalid
			};


        public static void ProcessNPCJobs(NPC_Manager __instance, bool canEmployeesDoSomething, bool canCustomersDoSomething) {
            if (!CheckScheduler()) {
                return;
            }
			//Performance.Start("FullCycle");
            //Check if we are at the beginning of a new FixedUpdate cycle
            FixedTimes fixedTime = CachedTimeValues.GetFixedTimeCachedValues();
            if (++sessionVars.CurrentFixedUpdateCounter >= fixedTime.fixedUpdateCycleMax || sessionVars.InitialLoop) {
				if (!sessionVars.InitialLoop) {
                    //Performance.StopAndLog("FullCycle");
                }
                //Performance.Start("FullCycle");
                StartNewFixedUpdateCycle(fixedTime.fixedDeltaTime);
            }

            double maxProcessingTimeMillis = GetMaxAllowedProcessingTime(sessionVars.CurrentCycleJobFreqMode);

			if (canEmployeesDoSomething) {
                ProcessNpcJobs(__instance, NPCType.Employee, maxProcessingTimeMillis, fixedTime);
            }
            if (canCustomersDoSomething) {
                ProcessNpcJobs(__instance, NPCType.Customer, maxProcessingTimeMillis, fixedTime);
            }
            //Performance.Stop("FullCycle");
        }


        /// <summary>
        /// Handles the rate of execution of employee jobs. Each FixedUpdate, it
        /// may allow multiple employees to do jobs, or none at all, depending on settings.
        /// </summary>
        public static void ProcessNpcJobs(NPC_Manager __instance, NPCType npcType,
                double maxProcessingTimeMillis, FixedTimes fixedTime) {

            StopwatchDiag processTime = StopwatchDiag.StartNew();
            //Performance.Start("processTimeTotal");

            int loopCounter = 0;
			int workSkipCounter = 0;

			//Performance.Start("ProcessNpcInit");
            GameObject npcParentObj = SchedulerSessionVars.GetNpcParentObj(__instance, npcType);
            NpcLoopData npcLoopData = sessionVars.GetNpcLoopData(npcType);
            NpcWaitTimers npcWaitTimers = sessionVars.GetNpcWaitTimers(npcType);

			int npcCount = npcParentObj.transform.childCount;

            //TODO 0 Scheduler - Might want to make the max a value defined for each AutoModeData. fixedUpdateCycleMax is way
            //	too high and always bigger than employee number, though not for customers, but still too high.
            int workSkipLimit = Math.Min(npcParentObj.transform.childCount, fixedTime.fixedUpdateCycleMax);
            bool isLoopCounted = true;
            //Performance.StopAndLog("ProcessNpcInit");

			try {
                //Performance.Start("Perf_ShouldDoJobFullWhile");
                //Performance.Start("Perf_ShouldDoJob");
                while (ShouldDoJob(ref loopCounter, ref workSkipCounter, npcLoopData, isLoopCounted, workSkipLimit)) {
                    //Performance.StopAndLog("Perf_ShouldDoJob");
                    //Performance.Start("NpcWaitTimeAndJobControl");
					isLoopCounted = NpcWaitTimeAndJobControl(__instance, npcType, npcLoopData.CurrentNpcId,
							npcParentObj, npcWaitTimers, sessionVars.CurrentCycleJobModeType);
					//Performance.StopAndLog("NpcWaitTimeAndJobControl");
					if (++npcLoopData.CurrentNpcId >= npcCount) {
						npcLoopData.CurrentNpcId = 0;
					}

					try {
                        //Performance.Start("IsProcessWithinTime");
                        //TODO 0 Scheduler - Why the fuck is the warning inside IsProcessWithinTime registering times
						//	of 0.4s, but the performance logs I put only have like 0.01 in total?? Where the 
						//	fuck is the time going or what am I measuring wrong?
                        if (!IsProcessWithinTime(processTime, sessionVars.CurrentCycleJobFreqMode, maxProcessingTimeMillis, npcType)) {
                            //Performance.Stop("Perf_ShouldDoJobFullWhile");
                            return;
						}
					} finally {
						//Performance.Stop("IsProcessWithinTime");
                    }
				}
                //Performance.Stop("Perf_ShouldDoJob");
                //Performance.Stop("Perf_ShouldDoJobFullWhile");
            } finally {
                //processTime.Stop(); 
                //Performance.Stop("Perf_ShouldDoJob");
                //Performance.Stop("processTimeTotal");
                //Performance.StopAndLog("Perf_ShouldDoJob");
                //Performance.StopAndLog("Perf_ShouldDoJobFullWhile");
                //Performance.StopAndLog("NpcWaitTimeAndJobControl");
                //Performance.StopAndLog("IsProcessWithinTime");
                //Performance.StopAndLog("processTimeTotal");
                //TODO 0 Scheduler - In the huge save, this is registering times of up to 4 millis with no employees assigned.
                //	It is because of the skips, but considering the maximum amount of skips is 50, and single runs
                //	go for less than 0,01ms, I dont undestand how is it approaching 4 millis so much.
                //LOG.TEMPWARNING($"Total ProcessNpcJobs time: {processTime.ElapsedMillisecondsPrecise} for npc {npcType} - Max: {maxProcessingTimeMillis}");
            }
        }

        private static bool CheckScheduler() {
            if (!IsJobSchedulerActive) {
                if (sessionVars.ShowSchedulerInactiveError) {
                    TimeLogger.Logger.LogWarningShowInGame("The Job Scheduler is not currently active. " +
                        "Employees and customer will not work.", LogCategories.JobSched);

                    sessionVars.ShowSchedulerInactiveError = false;
                }

                return false;
            }

            return true;
        }

        private static void StartNewFixedUpdateCycle(float fixedDeltaTime) {
			//Update frequency mode from settings
			sessionVars.CurrentCycleJobFreqMode = ModConfig.Instance.NpcJobFrequencyMode.Value;
			sessionVars.CurrentCycleJobModeType = GetJobModeTypeSetting(sessionVars.CurrentCycleJobFreqMode);

            //Initialize npc timer calculations, or clear it in case the job frequency setting changed.
            if (sessionVars.CurrentCycleJobModeType == JobModeType.Automatic) {
                if (IsEmployeeModuleEnabled) {
                    sessionVars.InitializeNpcVarsFor(NPCType.Employee);
                }
				if (IsCustomerModuleEnabled) {
                    sessionVars.InitializeNpcVarsFor(NPCType.Customer);
                }
            } else if (sessionVars.CurrentCycleJobModeType == JobModeType.Other) {
                sessionVars.DestroyAutoModeData();
            } else {
				throw new NotSupportedException(sessionVars.CurrentCycleJobModeType.ToString());
			}

            //Get loop multipliers for this cycle.
            sessionVars.Employee.LoopData.LoopMultiplierCycle = GetLoopMultiplier(sessionVars.CurrentCycleJobFreqMode,
				sessionVars.CurrentCycleJobModeType, NPCType.Employee, sessionVars.Employee, fixedDeltaTime);
            sessionVars.Customer.LoopData.LoopMultiplierCycle = GetLoopMultiplier(sessionVars.CurrentCycleJobFreqMode,
                sessionVars.CurrentCycleJobModeType, NPCType.Customer, sessionVars.Customer, fixedDeltaTime);

            OnNewJobFrequencyMultiplier?.Invoke(sessionVars.Employee.LoopData.LoopMultiplierCycle, 
				sessionVars.Customer.LoopData.LoopMultiplierCycle);

            sessionVars.CurrentFixedUpdateCounter = 0;
			//sessionVars.LoopDecimalSurplus = 0f;
			sessionVars.InitialLoop = false;
		}

		private static float GetLoopMultiplier(EnumJobFrequencyMultMode jobFreqMode, JobModeType jobModeType,
                NPCType npcType, NpcData npcData, float fixedDeltaTime) {

			float loopMultiplier = jobModeType switch {
				JobModeType.Other =>
					1f,
				JobModeType.Automatic => 
					npcData.IsJobSchedEnabled ? 
						npcData.JobSchedProcessor.CalculateNextJobFreqMultiplier(
							jobFreqMode, npcType, npcData.WaitTimers, fixedDeltaTime) :
						0f
                ,
				JobModeType.Invalid =>
					throw new InvalidOperationException("The job frequency mode hasnt been set."),

				_ => throw new NotImplementedException($"The job frequency mode \"{jobModeType}\" is not implemented.")
			};

			return (float)Math.Round(loopMultiplier, LoopMultRounding);
		}

		private static bool NpcWaitTimeAndJobControl(NPC_Manager __instance, NPCType npcType, int npcIndex,
                GameObject npcParentObj, NpcWaitTimers npcWaitTimers, JobModeType currentCycleJobMode) {

			if (npcIndex >= npcParentObj.transform.childCount) {
				return false;
			}

			GameObject npcObj = npcParentObj.transform.GetChild(npcIndex).gameObject;
			NPC_Info npcInfo = npcObj.GetComponent<NPC_Info>();

            bool didWork = true;
			try {
                if (npcType == NPCType.Employee) {
                    didWork = EmployeeJobAIPatch.EmployeeNPCControl(__instance, npcObj, npcInfo);
                } else if (npcType == NPCType.Customer) {
                    __instance.CustomerNPCControl(npcIndex);
                }
            } catch (Exception ex) {
				TimeLogger.Logger.LogExceptionWithMessage($"Exception while processing job for " +
					$"{npcType} npc with id {npcIndex}.", ex, LogCategories.AI);
			}
            
             
			if (currentCycleJobMode == JobModeType.Automatic) {
                //Begin to time how long until this npc can act again. If it already
                //	had a timer running, stores the delay and starts timing all over again.
                npcWaitTimers.StartTimer(npcInfo.netId, didWork);
			}


			return didWork;
		}


        private static bool ShouldDoJob(ref int loopCounter, ref int workSkipCounter,
                NpcLoopData npcLoopData, bool isLoopCounted, int workSkipLimit) {

            //TODO 0 Scheduler - When all work is skipped in a single ProcessNpcJobs call, since the first isLoopCounted is
            //	always true, in reality its doing npcCount + 1 loops.
            //	Would it make more sense to increase the skip counter regardless of isLoopCounted value?
            if (!isLoopCounted) {
                if (++workSkipCounter > workSkipLimit) {
                    //Avoid infinite loop if no npc needs to do work.
                    workSkipCounter = 0;
                    return false;
                }

                return true;
            }

            bool shouldDoJob = false;

            if (loopCounter < (int)npcLoopData.LoopMultiplierCycle) {
                shouldDoJob = true;
            } else {
                //Since LoopMultiplierCycle can have decimal values, and even a value < 1 (so there is less than the 50
                //	npc job actions per second), we carry the decimals over to see if we accumulated enough.
                if (!npcLoopData.LoopMultiplierCycle.IsInteger()) {
                    npcLoopData.LoopDecimalSurplus += npcLoopData.LoopMultiplierCycle % Math.Max((int)npcLoopData.LoopMultiplierCycle, 1);

                    if (npcLoopData.LoopDecimalSurplus >= 1) {
                        npcLoopData.LoopDecimalSurplus--;
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
				_ => -1
			};
		}

		private static bool IsProcessWithinTime(StopwatchDiag processTime, EnumJobFrequencyMultMode jobFreqMode,
				double MaxNpcProcessingTimeMillis, NPCType npcType) {
			if (jobFreqMode == EnumJobFrequencyMultMode.Disabled) {
				return true;
			}
			if (MaxNpcProcessingTimeMillis <= 0) {
				return true;    //Time limiters are disabled
			}
			if (!WorldState.IsWorldLoaded) {
                // FixedUpdate is active while the game is still loading, but we dont want to send performance
                // warnings to the user while the cpu is doing work, where longer process times are normal.
                return false;
			}

            //LOG.TEMPWARNING($"IsProcessWithinTime - Time passed: {processTime.ElapsedMillisecondsPrecise}");
            if (processTime.ElapsedMillisecondsPrecise >= MaxNpcProcessingTimeMillis) {
				
				if (!sessionVars.ProcessLimitCounter.Value.TryIncreaseCounter()) {
					string warningMsgEnd;

					if (jobFreqMode == EnumJobFrequencyMultMode.Auto_Custom) {
						warningMsgEnd = $"It is recommended to adjust the settings in the " +
							$"\"{ModConfig.Instance.CustomEmployeeWaitTarget.Definition.Section}\" section.";
					} else if (sessionVars.CurrentCycleJobModeType == JobModeType.Automatic) {
						warningMsgEnd = $"It is recommended to select a different " +
							$"\"{ModConfig.Instance.NpcJobFrequencyMode.Definition.Key}\" in the settings.";
					} else {
						//Not currently possible, just a future safeguard.
						warningMsgEnd = $"It is recommended to select a " +
							$"\"{ModConfig.Instance.NpcJobFrequencyMode.Definition.Key}\" in the settings.";
					}

					string warningMsgStart = $"Processing {npcType} actions is taking too much time and its " +
						$"being automatically limited by {MyPluginInfo.PLUGIN_NAME} to improve performance. ";

					bool showInGame = ModConfig.Instance.EnabledDevMode.Value && !sessionVars.ShowInGameProcessTimeExceededError;
					TimeLogger.Logger.Log(LogTier.Warning, warningMsgStart + warningMsgEnd,
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
