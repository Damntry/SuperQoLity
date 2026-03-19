using Damntry.Utils.Timers;
using Damntry.UtilsUnity.Timers;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler.Helpers;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler {


    public enum JobModeType {
        Invalid,
        Automatic,
        Other
    }

    public class SchedulerSessionVars {

        /// <summary>
        /// A limit on the number of times that we allow the npc process to surpass 
        /// the established time limit, before taking extra measures.
        /// </summary>
        public Lazy<PeriodicTimeLimitedCounter<UnityTimeStopwatch>> ProcessLimitCounter { get; set; }

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

        public NpcData Employee { get; private set; }

        public NpcData Customer { get; private set; }


        public SchedulerSessionVars() {
            ProcessLimitCounter = new Lazy<PeriodicTimeLimitedCounter<UnityTimeStopwatch>>(() =>
                new PeriodicTimeLimitedCounter<UnityTimeStopwatch>(true, 30, 30000, true));

            Employee = new();
            Customer = new();

            CurrentFixedUpdateCounter = 0;
            CurrentCycleJobFreqMode = EnumJobFrequencyMultMode.Disabled;
            CurrentCycleJobModeType = JobModeType.Invalid;
            InitialLoop = true;
            ShowSchedulerInactiveError = true;
            ShowInGameProcessTimeExceededError = true;
        }

        public void InitializeNpcVarsFor(NPCType npcType) {
            if (npcType == NPCType.Employee) {
                Employee.InitializeAutoModeData();
            } else if (npcType == NPCType.Customer) {
                Customer.InitializeAutoModeData();
            }
        }


        public void DestroyAutoModeData() {
            AutoModeProcessor.Destroy();

            Employee?.DestroyAutoModeData();
            Customer?.DestroyAutoModeData();
        }

        public static GameObject GetNpcParentObj(NPC_Manager __instance, NPCType npcType) =>
            npcType switch {
                NPCType.Employee => __instance.employeeParentOBJ,
                NPCType.Customer => __instance.customersnpcParentOBJ,
                _ => throw new NotImplementedException(npcType.ToString())
            };

        public NpcLoopData GetNpcLoopData(NPCType npcType) =>
            npcType switch {
                NPCType.Employee => Employee.LoopData,
                NPCType.Customer => Customer.LoopData,
                _ => throw new NotImplementedException(npcType.ToString())
            };

        public NpcWaitTimers GetNpcWaitTimers(NPCType npcType) =>
            npcType switch {
                NPCType.Employee => Employee.WaitTimers,
                NPCType.Customer => Customer.WaitTimers,
                _ => throw new NotImplementedException(npcType.ToString())
            };

    }

    public class NpcData {
        public NpcData() {
            LoopData = new();
        }

        public void DestroyAutoModeData() {
            JobSchedProcessor = null;
            WaitTimers = null;
        }

        public void InitializeAutoModeData() {
            JobSchedProcessor ??= new();
            IsJobSchedEnabled = true;
            WaitTimers ??= new();
        }

        public JobSchedulerProcessor JobSchedProcessor { get; set; }

        public NpcWaitTimers WaitTimers { get; set; }

        public NpcLoopData LoopData { get; set; }


        public bool IsJobSchedEnabled { get; private set;}
    }

    public class NpcLoopData {
        /// <summary>The index of the npc being processed this loop.</summary>
        public int CurrentNpcId { get; set; }

        /// <summary>
        /// When the current loop counter is higher than the value of the non integer 
        /// multiplier for this cycle, this var accumulates the decimal remaining for the next loop.
        /// </summary>
        public float LoopDecimalSurplus { get; set; }

        /// <summary>The frequency multiplier used for npc in the current FixedUpdate cycle.</summary>
        public float LoopMultiplierCycle { get; set; }

    }

}
