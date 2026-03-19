using Cysharp.Threading.Tasks;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.JobScheduler;
using SuperQoLity.SuperMarket.Patches.NPC.Customer;
using SuperQoLity.SuperMarket.Patches.NPC.EmployeeModule;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.NPCs.Customers {

    public class TrappedCustomerDetection {

        public static readonly float npcVelocityThreshold = 0.75f;

        public static readonly float npcTrapProtectionTime = 1.5f;

        public static readonly float npcPriorityTrappedTime = 1f;
        public static readonly float npcNoAvoidanceTrappedTime = 2f;
        public static readonly float npcNudgeTrappedTime = 3.5f;
        public static readonly float npcExtraNudgeTrappedTime = 5.5f;

        public static readonly float maxNudgeDistance = 1.5f;
        public static readonly float maxExtraNudgeDistance = 4f;

        private bool detectionEnabled;

        private Dictionary<NPC_Info, NpcDetectionData> dictTrapData;


        public TrappedCustomerDetection() {
            dictTrapData = new();
        }

        public void EnableDetection() {
            detectionEnabled = true;
            DetectionLoop().FireAndForget(LogCategories.AI);
        }

        public void DisableDetection() {
            detectionEnabled = false;
        }

        public async Task DetectionLoop() {
            while (detectionEnabled) {
                if (dictTrapData.Count == 0 || !ModConfig.Instance.EnableCustomerStuckDetection.Value) {
                    await UniTask.Delay(2000);
                    continue;
                }

                float detectionStartTime = Time.time;

                //Performance.Start("StuckCheck");
                //Make a shallow copy of the dictionary so any new items added to the original while looping wont affect it.
                Dictionary<NPC_Info, NpcDetectionData> dictTrapDataCopy = new (dictTrapData);
                foreach (var kvp in dictTrapDataCopy) {
                    NpcDetectionData dd = kvp.Value;
                    if (dd.Active) {
                        CheckNpcNav(dd, Time.time);
                        //Performance.Stop("StuckCheck");
                        await UniTask.Delay(5);
                        //Performance.Start("StuckCheck");
                    }
                }
                //Performance.StopAndRecord("StuckCheck");

                //Since NPC_Info.npcid is not an unique value, and GetInstance/hashcode gets set to 0 when the
                //  object is dead, to remove those dead ends we need to check the entire dictionary.
                dictTrapData.RemoveDeadKeyReferences();

                //Await until its time for the next check. Delay wont be exact but more than enough for our case.
                float idleTime = detectionStartTime - Time.time + GetTrapCheckInterval();
                if (idleTime > 0) {
                    await UniTask.Delay((int)(idleTime * 1000));
                } else {
                    await UniTask.DelayFrame(1);
                }
            }

            dictTrapData.Clear();
        }

        private static float GetTrapCheckInterval() =>
            ModConfig.Instance.NpcJobFrequencyMode.Value switch {
                EnumJobFrequencyMultMode.Auto_Performance => 0.5f,
                EnumJobFrequencyMultMode.Auto_Aggressive => 0.125f,
                _ => 0.25f, //Balanced, Disabled, Custom
            };

        public void BeginNpcNavDetection(NPC_Info npcInfo) {
            ChangeNpcTrapDetection(npcInfo, true);
        }

        public void StopNpcNavDetection(NPC_Info npcInfo) {
            ChangeNpcTrapDetection(npcInfo, false);
        }

        public bool IsNavDetectionActive(NPC_Info npcInfo) =>
            npcInfo && dictTrapData.TryGetValue(npcInfo, out NpcDetectionData dd) && dd.Active;

        private void ChangeNpcTrapDetection(NPC_Info npcInfo, bool active) {
            if (!npcInfo) {
                return;
            }

            if (!dictTrapData.TryGetValue(npcInfo, out NpcDetectionData dd)) {
                dd = new(npcInfo, active);
                dictTrapData.Add(npcInfo, dd);
            }

            if (active && ModConfig.Instance.EnableCustomerStuckDetection.Value) {
                dd.BeginDetection();
            } else {
                dd.EndDetection();
            }
        }

        //TODO 0 Performance - Test in the shittiest pc how long it can take in between full loops. So basically take
        //  one NPC, and see how much time there is on average between previous TrapDetectedTimeStart and
        //  Time.Time, when its full of customers.

        private void CheckNpcNav(NpcDetectionData dd, float checkStartTime) {
            NavMeshAgent npcNavAgent = dd.NavMeshAgent;

            if (!NPC_CustomerNavFixer.TestNavAgent(npcNavAgent)) {
                dd.EndDetection();
            }

            if (dd.Active && dd.NavMeshAgent.velocity.magnitude < npcVelocityThreshold && !dd.NpcInfo.beingPushed &&
                    npcNavAgent.isOnNavMesh && npcNavAgent.remainingDistance > npcNavAgent.stoppingDistance) {

                if (dd.TrapDetectedTimeStart < 0) {
                    dd.TrapDetectedTimeStart = checkStartTime;
                }

                if (dd.ProcessProtection()) {
                    //Still under active protection time, skip check for now
                    return;
                }

                float trappedTotalTime = checkStartTime - dd.TrapDetectedTimeStart;

                if (trappedTotalTime > npcExtraNudgeTrappedTime) {
                    NudgeTowardsDestination(dd, smallNudge: false);
                    dd.SetTrapProtectionEndTime();
                } else if (trappedTotalTime > npcNudgeTrappedTime) {
                    NudgeTowardsDestination(dd, smallNudge: true);
                    dd.SetTrapProtectionEndTime();
                } else if (trappedTotalTime > npcNoAvoidanceTrappedTime) {
                    npcNavAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                    dd.SetTrapProtectionEndTime();
                } else if (trappedTotalTime > npcPriorityTrappedTime) {
                    npcNavAgent.avoidancePriority = 0;
                    dd.SetTrapProtectionEndTime();
                }
            } else {
                dd.ResetTrapDetection();
                dd.ProcessProtection();
            }
        }

        private void NudgeTowardsDestination(NpcDetectionData dd, bool smallNudge) {
            Vector3 currentPos = dd.NpcInfo.transform.position;
            Vector3 destinationPos = dd.NavMeshAgent.destination;

            Vector3 posDiff = destinationPos - currentPos;
            float distance = posDiff.magnitude;
            Vector3 direction = posDiff.normalized;

            //Calculate and clamp max distance so we dont overshoot the destination
            float nudgeDistance = Mathf.Min(smallNudge ? maxNudgeDistance : maxExtraNudgeDistance, distance);

            Vector3 nudgedPos = currentPos + direction * nudgeDistance;

            if (!NavMesh.SamplePosition(nudgedPos, out NavMeshHit navHit, 2, -1)) {
                TimeLogger.Logger.LogWarning("A valid nudge position couldnt be found for trapped npc " +
                    $"{EmployeeJobAIPatch.GetUniqueId(dd.NpcInfo)}", LogCategories.AI);
                return;
            }

            dd.NpcInfo.transform.position = navHit.position;
        }


        private class NpcDetectionData(NPC_Info npcInfo, bool active) {

            public NPC_Info NpcInfo { get; } = npcInfo;
            public bool Active { get; private set; } = active;
            public float TrapDetectedTimeStart { get; set; } = -1f;
            public float TrapProtectionEndTime { get; set; } = -1f;

            public NavMeshAgent NavMeshAgent => NpcInfo.NullableObject()?.GetComponent<NavMeshAgent>();


            public void BeginDetection() {
                TrapProtectionEndTime = -1f;
                Active = true;
            }

            public void EndDetection() {
                ResetTrapDetection();
                Active = false;
            }

            public void ResetTrapDetection() {
                TrapDetectedTimeStart = -1f;
            }

            public void SetTrapProtectionEndTime() {
                TrapProtectionEndTime = Time.time + npcTrapProtectionTime;
            }

            public bool ProcessProtection() {
                if (TrapProtectionEndTime == -1) {
                    return false;
                }

                if (TrapProtectionEndTime < Time.time) {
                    //Protection time finished
                    TrapProtectionEndTime = -1;

                    NavMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                    NavMeshAgent.avoidancePriority = 50;

                    return false;
                }

                return true;
            }

        }

        /*  Ended up just using the default hashcode, since npcid is not unique.
        private sealed class NPC_Comparer : IEqualityComparer<NPC_Info> {
            public bool Equals(NPC_Info o1, NPC_Info o2) =>
                o1.npcid == o2.npcid;
            public int GetHashCode(NPC_Info o) {
                unchecked {
                    return 83 * 4211 + o.npcid;
                }
            }
        }
        */

    }
}
