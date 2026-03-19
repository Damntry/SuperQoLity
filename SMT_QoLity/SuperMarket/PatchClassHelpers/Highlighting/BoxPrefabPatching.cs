using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using Mirror;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Definitions;
using System.Linq;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting {

    public static class BoxPrefabPatching {

        private enum BoxType {
            Ground,
            GroundNetworked,
            Held
        }

        private static Vector3 localScaleGroundBox;

        private static Mesh meshCubeGroundBoxCached;


        private static bool isBoxPrefabModded;

        private static bool isNetworkBoxPrefabModded;

        private static bool isDummyBoxPrefabModded;


        public static void PrepareBoxPrefabPatching() {
            WorldState.OnLoadingWorld += () => {
                //For when joining as a client
                if (WorldState.CurrentOnlineMode == GameOnlineMode.Client) {
                    InitializeBoxPrefabs(BoxType.GroundNetworked);
                }
            };

            //OnFPControllerStarted since it depends on taking a prefab from PlayerNetwork as hosts.
            WorldState.OnFPControllerStarted += () => {
                InitializeBoxPrefabs(BoxType.Ground);

                //For clients, the dummy prefab might have already been instanced at this point, but no
                //  point fixing that just so the highlight works on boxes being held at the time of joining.
                InitializeBoxPrefabs(BoxType.Held);
            };
        }


        private static void InitializeBoxPrefabs(BoxType boxType) {
            if (boxType == BoxType.Ground && isBoxPrefabModded ||
                    boxType == BoxType.GroundNetworked && isNetworkBoxPrefabModded ||
                    boxType == BoxType.Held && isDummyBoxPrefabModded) {
                return;
            }

            Mesh cubeMesh = null;
            if (FindBoxPrefab(boxType, out GameObject boxPrefab)) {
                cubeMesh = GetMeshFromPrefab(boxType, boxPrefab);
            }

            if (!cubeMesh || !boxPrefab) {
                return;
            }

            //Add highlight to the main box/dummy prefab, so every instanced ground box will have all its highlight
            //  properties cloned, faster than setting it all up manually for each.
            HighlightInitialization.InitializeBoxHighlight(boxPrefab.transform, cubeMesh, localScaleGroundBox);

            TimeLogger.Logger.LogDebugFunc(() => $"Prefab for box '{boxType}' patched successfully.", LogCategories.Highlight);

            if (boxType == BoxType.Ground) {
                isBoxPrefabModded = true;
            } else if (boxType == BoxType.GroundNetworked) {
                isNetworkBoxPrefabModded = true;
            } else if (boxType == BoxType.Held) {
                isDummyBoxPrefabModded = true;
            }
        }

        private static bool FindBoxPrefab(BoxType boxType, out GameObject boxPrefab) {

            boxPrefab = null;

            if (boxType == BoxType.Ground) {
                boxPrefab = SMTInstances.ManagerBlackboard().NullableObject()?.boxPrefab;
            } else if (boxType == BoxType.GroundNetworked) {
                //Clients get their box prefab spawned by mirror, and by the time we can
                //  access ManagerBlackboard, it could have been already instanced.
                boxPrefab = NetworkClient.prefabs
                    .FirstOrDefault(k => k.Value.name.ToLower().Contains("1_box"))
                    .Value;
            } else if (boxType == BoxType.Held) {
                boxPrefab = SMTInstances.LocalPlayerNetwork().NullableObject()?.dummyBoxPrefab;
            }

            if (!boxPrefab) {
                TimeLogger.Logger.LogError($"The prefab for {boxType} box could not be found. " +
                    $"Highlight for these boxes wont work.", LogCategories.Highlight);
            }

            return (bool)boxPrefab;
        }

        private static Mesh GetMeshFromPrefab(BoxType boxType, GameObject boxPrefab) {
            if (boxType == BoxType.Ground || boxType == BoxType.GroundNetworked) {
                GroundBoxHighlightData boxHighlightData = ContainerHighlightData.GroundBox;

                Transform vanillaHighlightObj = boxPrefab.transform
                    .Find(boxHighlightData.VanillaName).NullableObject();

                if (!vanillaHighlightObj) {
                    TimeLogger.Logger.LogError($"The '{boxHighlightData.VanillaName}' child GameObject for " +
                        $"box {boxType} could not be found. Highlight for " +
                        $"{(boxType == BoxType.Held ? "held" : "ground")} boxes wont work.",
                        LogCategories.Highlight);
                    return null;
                }

                localScaleGroundBox = vanillaHighlightObj.localScale;

                Mesh meshCube = vanillaHighlightObj
                    .GetComponent<MeshFilter>().NullableObject()?
                    .sharedMesh;

                if (meshCube) {
                    meshCubeGroundBoxCached = meshCube;
                } else {
                    TimeLogger.Logger.LogError($"The MeshFilter for box {boxType}" +
                        $"could not be found. Highlight for held and ground boxes wont work.", LogCategories.Highlight);
                    return null;
                }

                return meshCube;
            } else {
                return meshCubeGroundBoxCached;
            }
        }

    }
}
