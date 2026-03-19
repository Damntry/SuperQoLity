using Damntry.Utils.Logging;
using Mirror;
using SuperQoLity.SuperMarket.ModUtils;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers {

    internal static class TargetObjectResolver {

        private static GameObject parentTrashObj;

        private static CharacterSizeCache charSizeCache;


        public static void ResetCache() {
            charSizeCache = new CharacterSizeCache();
        }


        public static bool GetTargetAdjustedPosition(FireNetworkData fireNetData, out Vector3 position) {
            if (FindTargetObjectByNetid(fireNetData.TargetNetid, fireNetData.TargetType, out Transform remoteTargetT)) {
                Vector3 charCenter = Vector3.zero;

                bool found = fireNetData.TargetType switch {
                    TargetType.Player => charSizeCache.GetPlayerCenter(remoteTargetT, out charCenter),
                    TargetType.NPC => charSizeCache.GetGenericNpcCenter(remoteTargetT, out charCenter),
                    _ => false  //Just use the entity position for any other type
                };

                if (!found) {
                    position = remoteTargetT.position;
                    return true;
                }

                //Add a bit of random offset
                float offsetPercent = 0.6f;
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle;
                Vector3 centerOffset = new (
                    charCenter.x * offsetPercent * randomCircle.x,
                    charCenter.y * offsetPercent * randomCircle.y,
                    0f
                );

                //Use only the height. Both X and Z are the center point already in remoteTargetT position.
                Vector3 verticalCenter = Vector3.up * charCenter.y;

                Vector3 worldOffset = remoteTargetT.TransformVector(centerOffset + verticalCenter);
                position = remoteTargetT.position + worldOffset;

                return true;
            }

            position = Vector3.zero;
            return false;
        }


        public static bool FindTargetObjectByNetid(uint targetNetid, TargetType targetType, out Transform targetObj) {
            targetObj = null;

            if (targetType == TargetType.Player) {
                PlayerNetwork pNetwork = UnityEngine.Object
                    .FindObjectsByType<PlayerNetwork>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.netId == targetNetid);

                if (pNetwork) {
                    targetObj = pNetwork.transform;
                }
            } else if (targetType == TargetType.NPC) {
                targetObj = FindNpcInParentByNetid(NPC_Manager.Instance.employeeParentOBJ, targetNetid);
                if (!targetObj) {
                    targetObj = FindNpcInParentByNetid(NPC_Manager.Instance.customersnpcParentOBJ, targetNetid);
                }
                if (!targetObj) {
                    //Dummy npcs on clients dont have a parent and instead exist in the root of the scene.
                    if (WorldState.CurrentOnlineMode == GameOnlineMode.Host) {
                        targetObj = FindNpcInParentByNetid(NPC_Manager.Instance.dummynpcParentOBJ, targetNetid);
                    } else if (WorldState.CurrentOnlineMode == GameOnlineMode.Client) {
                        foreach (var rootObj in SceneManager.GetActiveScene().GetRootGameObjects()) {
                            if (rootObj.name.StartsWith("A_NPC_Agent") && rootObj.TryGetComponent(out NetworkIdentity netIdentity)
                                    && netIdentity.netId == targetNetid) {
                                targetObj = rootObj.transform;
                                break;
                            }
                        }
                    }
                }
            } else if (targetType == TargetType.Trash) {
                if (!parentTrashObj) {
                    parentTrashObj = GameObject.Find("Level_SupermarketProps/Trash/");
                }
                targetObj = FindNpcInParentByNetid(parentTrashObj, targetNetid);
            } else {
                throw new InvalidOperationException($"The target type {targetType} is not valid for this method.");
            }

            //Trash can get deleted earlier than this call, so ignore it.
            if (!targetObj && targetType != TargetType.Trash) {
                TimeLogger.Logger.LogError($"No {targetType} object could be found " +
                    $"with netid {targetNetid}", LogCategories.Other);
            }

            return (bool)targetObj;
        }

        private static Transform FindNpcInParentByNetid(GameObject parentObj, uint targetNetid) {
            if (!parentObj) {
                TimeLogger.Logger.LogError("The parentObj argument cant be null or destroyed.", LogCategories.Other);
                return null;
            }
            return parentObj.transform.Cast<Transform>()
                .FirstOrDefault(t =>
                    t.TryGetComponent(out NetworkIdentity netIdentity) && netIdentity.netId == targetNetid);
        }


        private class CharacterSizeCache {

            private bool isGenericNpcCached;

            private bool isPlayerCached;

            /// <summary>Includes employees, customers, and dummies</summary>
            private Vector3 genericNpcModelCenter;

            public Vector3 playerModelCenter;


            public bool GetGenericNpcCenter(Transform baseObj, out Vector3 center) {
                if (!isGenericNpcCached) {
                    GetCharacterCenter(baseObj, out isGenericNpcCached, out genericNpcModelCenter);
                }
                center = genericNpcModelCenter;

                return isGenericNpcCached;
            }

            public bool GetPlayerCenter(Transform baseObj, out Vector3 center) {
                if (!isPlayerCached) {
                    GetCharacterCenter(baseObj, out isPlayerCached, out playerModelCenter);
                }
                center = playerModelCenter;

                return isPlayerCached;
            }

            private void GetCharacterCenter(Transform remoteTargetT, out bool centerFound, out Vector3 center) {
                center = Vector3.zero;

                Collider collider = remoteTargetT.GetComponentInChildren<Collider>();
                if (collider) {
                    center = collider.bounds.extents;
                    centerFound = true;
                    return;
                }

                Mesh mesh = null;

                SkinnedMeshRenderer renderer = remoteTargetT.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer) {
                    mesh = renderer.sharedMesh;
                }
                if (!mesh) {
                    MeshFilter meshFilter = remoteTargetT.GetComponentInChildren<MeshFilter>();
                    if (meshFilter) {
                        mesh = meshFilter.sharedMesh;
                    }
                }
                if (!mesh) {
                    TimeLogger.Logger.LogWarning($"No Collider, SkinnedMeshRenderer or MeshFilter " +
                        $"found for transform {remoteTargetT}", LogCategories.Visuals);

                    centerFound = false;
                    return;
                }

                center = mesh.bounds.extents;
                centerFound = true;
            }

        }

    }
}
