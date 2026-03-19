using Cysharp.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.Vectors;
using HutongGames.PlayMaker;
using JetBrains.Annotations;
using Mirror;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Definitions;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Helpers;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.FireEffects.Model;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers {

    public enum TargetType {
        None,
        OtherCollider,
        NPC,
        Player,
        Trash
    }

    public class WeaponLogic {

        public static ShotgunDefinition ShotgunDefinition { get; } = ShotgunDefinition.DefaultShotgunDefinition;


        private PlayerNetwork pNetwork;

        private readonly SmokeManager tracerSmokeSystem;

        /// <summary>The local player object.</summary>
        private GameObject masterPlayerOBJ;


        public WeaponLogic() {
            tracerSmokeSystem = new();
        }


        public void Destroy() {
            tracerSmokeSystem.Destroy();
        }

        public void BeginLocalPlayerFire(out GameObject broomObj) {
            pNetwork ??= SMTInstances.LocalPlayerNetwork();

            Vector3 muzzlePos = GetBroomTipPoint(pNetwork, out broomObj, out Vector3 broomTip);
            Vector3 endPoint = GetCrosshairEndPoint();

            List<PelletHitData> pelletDataList = GeneratePelletsHitData(muzzlePos, endPoint);
            List<FireNetworkData> fireDataList = new ();

            foreach (PelletHitData pelletData in pelletDataList) {
                TargetType targetType = pelletData.Target;
                if (targetType == TargetType.None || targetType == TargetType.OtherCollider) {
                    continue;
                }

                if (TriggerTargetBeingShot(targetType, pelletData.Hit, out Transform targetBaseObj)) {
                    if (!targetBaseObj.TryGetComponent(out NetworkIdentity netIdentity)) {
                        TimeLogger.Logger.LogError($"Object {targetBaseObj} does not contain a " +
                            $"NetworkIdentity component.", LogCategories.Other);
                        continue;
                    }
                    fireDataList.Add(new FireNetworkData(netIdentity.netId, targetType));
                }
            }

            SpawnTracerPellets(pelletDataList, muzzlePos, broomObj, broomTip);

            uint localNetid = SMTInstances.LocalPlayerNetwork().netId;
            BroomShotgunNetwork.LocalInstance.CmdPlayerFire(endPoint, localNetid, fireDataList.ToArray());
        }

        public void BeginRemotePlayerFire(PlayerNetwork sourcePlayer, Vector3 endPoint) {
            Vector3 muzzlePos = GetBroomTipPoint(sourcePlayer, out GameObject broomObj, out Vector3 broomTip);

            List<PelletHitData> pelletDataList = GeneratePelletsHitData(muzzlePos, endPoint);

            SpawnTracerPellets(pelletDataList, muzzlePos, broomObj, broomTip);
        }

        private bool TriggerTargetBeingShot(TargetType targetType, RaycastHit hitData, out Transform targetBaseObj) {
            targetBaseObj = null;
            GameObject targetHit = hitData.collider.gameObject;

            bool isCharTarget = targetType == TargetType.NPC || targetType == TargetType.Player;
            if (isCharTarget && targetHit.tag != "Interactable" && targetHit.tag != "Player") {
                //Already under a broom or shot effect.
                return false;
            }

            targetBaseObj = GetTargetBaseObject(targetType, hitData);
            if (!targetBaseObj) {
                TimeLogger.Logger.LogError($"The target base object of type {targetType} is null.", LogCategories.Other);
                return false;
            }

            if (targetType == TargetType.NPC) {
                //This call already takes care of all thief and thief networking logic too.
                targetBaseObj.GetComponent<NPC_Info>().CmdAnimationPlay(0);
            } else if (targetType == TargetType.Player) {
                if (!masterPlayerOBJ) {
                    masterPlayerOBJ = FsmVariables.GlobalVariables.FindFsmGameObject("MasterPlayerOBJ").Value;
                }

                Vector3 val = targetHit.transform.position - masterPlayerOBJ.transform.position;
                targetBaseObj.GetComponent<PlayerNetwork>().CmdPushPlayer(val.normalized);
            } else if (targetType == TargetType.Trash) {
                //Destroy trash
                TrashSpawn trashSpawn = targetHit.GetComponent<TrashSpawn>();
                trashSpawn.CmdClearTrash();
            }

            if (isCharTarget) {
                //Make target non interactable until the effect passes
                string targetTag = targetHit.tag;
                targetHit.tag = "Untagged";
                DelayedRestoreInteractableTag(targetHit, targetTag);
            }

            return true;
        }

        private Transform GetTargetBaseObject(TargetType targetType, RaycastHit hitData) {
            Transform targetHit = hitData.collider.transform;

            if (targetType == TargetType.NPC) {
                //This call already takes care of all thief and thief networking logic too.
                return targetHit.parent;
            } else if (targetType == TargetType.Player) {
                //A player has 2 different objects that a raycast can hit.
                if (targetHit.tag == "Player") {
                    return targetHit;
                } else {
                    return targetHit.parent.parent;
                }
            } else if (targetType == TargetType.Trash) {
                return hitData.transform;
            }

            return null;
        }

        private void SpawnTracerPellets(List<PelletHitData> pelletDataList,
                Vector3 muzzlePos, GameObject broomObj, Vector3 broomTip) {

            SingleTracerData[] pelletDataArray = pelletDataList
                .Select(p => new SingleTracerData(
                    p.PelletFullRay,
                    ShotgunDefinition.Tracers.StartOffset,
                    p.EndPoint,
                    p.Distance,
                    ShotgunDefinition,
                    hitFaceRotation: pelletDataList[0].Hit.normal,
                    //Can only create impact effects on any collider surfaces we dont have customn logic for.
                    createsImpactMark: p.Target == TargetType.OtherCollider,
                    pelletDataList.Count
                ))
                .ToArray();

            ShotgunEffectsManager.SpawnTracerGroup(muzzlePos, pelletDataArray, ShotgunDefinition, broomObj,
                broomTip, tracerSmokeSystem, animateTravel: true, addMuzzleFlash: false,
                addImpactHitMark: true, addImpactSparks: true, addLight: true
            );
        }

        private Vector3 GetBroomTipPoint(PlayerNetwork pNetwork, out GameObject broomObj, out Vector3 broomTip) {
            broomObj = pNetwork.instantiatedOBJ;
            MeshFilter meshFilter = broomObj.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;
            Bounds bounds = mesh.bounds;

            //Find the farthest vertex in the "forward" direction
            broomTip = bounds.center + Vector3.forward * bounds.extents.z;
            //Offset a bit to the right
            broomTip += Vector3.right * 0.009f;
            //Convert to world space
            return broomObj.transform.TransformPoint(broomTip);
        }

        private async void DelayedRestoreInteractableTag(GameObject targetHit, string tag) {
            //Vanilla wait time after being smacked by a broom, from FSM HitTrigger-Behaviour.
            await UniTask.Delay(1550);

            if (targetHit) {
                targetHit.tag = tag;
            }
        }

        private List<PelletHitData> GeneratePelletsHitData(Vector3 muzzlePos, Vector3 endPoint) {
            List<PelletHitData> pelletDataList = [];

            Dictionary<int, TargetType> collidersProcessed = new();

            for (int i = 0; i < ShotgunDefinition.ProjectileCount; i++) {

                Ray pelletRaycast = CalculateRayForPellet(i == 0, muzzlePos, endPoint);

                TargetType targetType = TargetType.None;
                if (Physics.Raycast(pelletRaycast, out RaycastHit hitData,
                        maxDistance: ShotgunDefinition.ProjectileMaxRange, layerMask: SMTLayers.RayCastGenericLayerMask)) {

                    //Avoid processing more than 1 instance of the same collider
                    if (!collidersProcessed.TryGetValue(hitData.colliderInstanceID, out targetType)) {
                        targetType = GetTargetHitMatch(hitData.collider.gameObject);

                        targetType = SanitizeTargetCollider(targetType, hitData);

                        collidersProcessed.Add(hitData.colliderInstanceID, targetType);
                    } else if (targetType == TargetType.Player || targetType == TargetType.NPC 
                            || targetType == TargetType.Trash) {

                        //Wont trigger a repeated reaction, or generate hit vfx
                        targetType = TargetType.None;
                    } else  {
                        targetType = SanitizeTargetCollider(targetType, hitData);
                    }

                    pelletDataList.Add(new(hitData, hitData.distance, hitData.point, pelletRaycast, targetType));
                } else {
                    Vector3 calcEndPoint = pelletRaycast.GetPoint(ShotgunDefinition.ProjectileMaxRange);
                    pelletDataList.Add(new(default, ShotgunDefinition.ProjectileMaxRange, calcEndPoint, pelletRaycast, targetType));
                }
            }

            return pelletDataList;
        }

        private TargetType SanitizeTargetCollider(TargetType targetType, RaycastHit hitData) {
            if (targetType == TargetType.None && hitData.collider) {
                return TargetType.OtherCollider;
            }
            return targetType;
        }

        /// <summary>Gets the position where the crosshair center is aiming at</summary>
        private Vector3 GetCrosshairEndPoint() {
            Vector3 endPoint;
            Transform cameraTransform = Camera.main.transform;

            Ray centerRaycast = Camera.main.ViewportPointToRay(
                new Vector2(0.5f, 0.5f), Camera.MonoOrStereoscopicEye.Mono);

            if (Physics.Raycast(centerRaycast, out RaycastHit hitData,
                        maxDistance: ShotgunDefinition.ProjectileMaxRange, layerMask: SMTLayers.RayCastGenericLayerMask)) {

                endPoint = hitData.point;
            } else {
                //Calculate the theoretical max range at center crosshair.
                endPoint = cameraTransform.position + cameraTransform.forward * ShotgunDefinition.ProjectileMaxRange;
            }

            return endPoint;
        }

        private Ray CalculateRayForPellet(bool isFirstPellet, Vector3 muzzlePos, Vector3 endPoint) {
            Vector3 direction = (endPoint - muzzlePos).normalized;

            if (!isFirstPellet) {
                direction = Camera.main.GetRandomSpreadDirection(SpreadAxis.Both, direction, ShotgunDefinition.SpreadAngle);
            }

            return new Ray(muzzlePos, direction);
        }

        private TargetType GetTargetHitMatch(GameObject targetHit) {
            TargetType targetType = TargetType.None;
            
            Transform obj = targetHit.transform;
            if (obj.parent && obj.parent.GetComponent<NPC_Info>()) {
                targetType = TargetType.NPC;
            } else if (obj && 
                    (obj.GetComponent<PlayerNetwork>() ||
                        //Account for that at head level, it hits the player prefab itself and not the HitTrigger children
                        obj.parent && obj.parent.parent && obj.parent.parent.GetComponent<PlayerNetwork>())) {

                targetType = TargetType.Player;
            } else if (targetHit.TryGetComponent<TrashSpawn>(out _)) {
                targetType = TargetType.Trash;
            }

            return targetType;
        }

        private record struct PelletHitData(RaycastHit Hit, float Distance, Vector3 EndPoint, Ray PelletFullRay, TargetType Target);

    }

    public record struct FireNetworkData(uint TargetNetid, TargetType TargetType);
}
