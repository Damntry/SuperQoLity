using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using HighlightPlus;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Caching;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Definitions;
using System.Collections.Generic;
using System.Linq;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using UnityEngine;
using UnityEngine.SceneManagement;
using QualityLevel = HighlightPlus.QualityLevel;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting {

    public class HighlightInitialization {

        public static readonly float InitializedHighlightEffectValue = 55224.3144f;


        private static List<Transform> nonParentedBoxes;


        public static bool IsHighlightTransformInitialized(Transform t) =>
            //Use this component as a flag for initialization
            t.GetComponent<HighlightInitialized>() == true;

        public static bool IsHighlightEffectInitialized(HighlightEffect he) =>
            //Use this component as a flag for initialization
            he.targetFXGroundMaxDistance == InitializedHighlightEffectValue;

        public static bool IsPreinitializedContainer(ContainerType containerType) =>
            containerType switch {
                ContainerType.Storage or ContainerType.StorageSlot or ContainerType.GroundBox =>
                    true,
                _ => false
            };


        public static void ReinitializeAllContainerHighlights() {
            //Product shelves and its slots
            foreach (Transform shelf in NPC_Manager.Instance.shelvesOBJ.transform) {
                ReinitializeSingleTransformHighlightProperties(shelf, ContainerType.ProdShelf);

                Transform highlightsMarker = shelf.Find(ContainerHighlightData.Products.SQoLName);
                if (highlightsMarker) {
                    foreach (Transform highlightSlot in highlightsMarker) {
                        ReinitializeSingleTransformHighlightProperties(highlightSlot, ContainerType.ProdShelfSlot);
                    }
                }
            }

            //TODO 4 - When you press to cycle highlight modes without having highlighted anything before, you get a bunch of:
            //  "The highlightsMarker object for the shelf could not be found. Storage slot highlighting wont work"
            //This only happens when the game hasnt finished loading stuff after game load. It probably cant even happen
            //  if the user doesnt have the Quickload mod installed.

            //Storage shelves and its slots
            foreach (Transform storageShelf in NPC_Manager.Instance.storageOBJ.transform) {
                ReinitializeSingleTransformHighlightProperties(storageShelf, ContainerType.Storage);

                Transform highlightsMarker = storageShelf.Find(ContainerHighlightData.Storage.SQoLName);
                if (highlightsMarker) {
                    HighlightTargetCollection objCollection = new(ContainerType.StorageSlot);

                    foreach (Transform highlightSlot in highlightsMarker) {
                        objCollection.TryAddHighlightTarget(highlightSlot.GetChild(0), false);
                    }

                    HighlightEffect highlightEffect = highlightsMarker.GetComponent<HighlightEffect>();
                    ReinitializeHighlightProperties(highlightEffect, objCollection);
                } else {
                    TimeLogger.Logger.LogError("The highlightsMarker object for the storage " +
                        "could not be found. Storage slot highlighting wont work.", LogCategories.Highlight);
                }
            }

            //Ground boxes
            foreach (Transform box in ContainerHighlightData.GetExistingParentedBoxes()) {  //NPC_Manager.Instance.boxesOBJ.transform) {
                Transform highlightsObj = box.Find(ContainerHighlightData.GroundBox.SQoLName);
                if (highlightsObj) {
                    ReinitializeSingleTransformHighlightProperties(highlightsObj, ContainerType.GroundBox);
                } else {
                    TimeLogger.Logger.LogError("The highlightsMarker object for the ground box " +
                        "could not be found. Ground box highlighting wont work.", LogCategories.Highlight);
                }
            }

            if (WorldState.CurrentOnlineMode == GameOnlineMode.Client) {
                //Handle ground boxes that existed before the client joined the store.
                foreach (Transform box in GetNonParentedBoxes()) {
                    Transform highlightsObj = box.Find(ContainerHighlightData.GroundBox.SQoLName);
                    if (highlightsObj) {
                        ReinitializeSingleTransformHighlightProperties(highlightsObj, ContainerType.GroundBox);
                    }
                }
            }

            //Held boxes
            foreach (CharacterData boxData in GetAllHeldBoxesData()) {
                if (GetBoxDataFromCharacter(boxData.CharTransform, boxData.CharSource, out Transform boxHighlightT, out _)) {
                    ReinitializeSingleTransformHighlightProperties(boxData.CharTransform, ContainerType.GroundBox);
                }
            }
        }

        
        public static void InitializeHighlightProperties(HighlightEffect highlightEffect, HighlightTargetCollection highlightTargets) {
            PropertiesInitializer.SetInitialHighlightProperties(highlightEffect, highlightTargets, forceInitialization: false);
        }

        public static void InitializeSingleTransformHighlightProperties(Transform t, ContainerType containerType) {
            InitializeSingleTransformHighlightProperties(t, containerType, forceInitialization: false);
        }

        private static void ReinitializeHighlightProperties(HighlightEffect highlightEffect, HighlightTargetCollection highlightTargets) {
            PropertiesInitializer.SetInitialHighlightProperties(highlightEffect, highlightTargets, forceInitialization: true);
        }

        private static void ReinitializeSingleTransformHighlightProperties(Transform t, ContainerType containerType) {
            InitializeSingleTransformHighlightProperties(t, containerType, forceInitialization: true);
        }

        private static void InitializeSingleTransformHighlightProperties(Transform t, ContainerType containerType, bool forceInitialization) {
            if (t.TryGetComponent(out HighlightEffect highlightEffect)) {
                PropertiesInitializer.SetInitialSingleTransformHighlightProperties(t, highlightEffect, containerType, forceInitialization);
            } else {
                CreateInitializedSingleTransformHighlight(t, containerType);
            }
        }

        public static HighlightEffect CreateInitializedSingleTransformHighlight(Transform t, ContainerType containerType) {
            if (t.TryGetComponent(out HighlightEffect highlightEffect)) {
                TimeLogger.Logger.LogWarning($"Transform {t} already has an HighlightEffect component", LogCategories.Highlight);
                return highlightEffect;
            }
            if (HighlightLogic.IsSeparatedHighlightAndRender(containerType)) {
                TimeLogger.Logger.LogWarning($"Something went wrong. This transform was set to be initialized, " +
                    $"but this type of highlight should have been already initialized.", LogCategories.Highlight);
                return null;
            }

            highlightEffect = t.gameObject.AddComponent<HighlightEffect>();

            PropertiesInitializer.SetInitialSingleTransformHighlightProperties(t, highlightEffect, containerType, forceInitialization: false);

            return highlightEffect;
        }

        public static void InitializeSingleTransformHighlightIfNeeded(Transform t, HighlightEffect highlightEffect, ContainerType containerType) {
            if (!IsHighlightTransformInitialized(t)) {
                PropertiesInitializer.SetInitialSingleTransformHighlightProperties(t, highlightEffect, containerType, forceInitialization: false);
            }
        }

        public static void PopulateNonParentedBoxes() {
            nonParentedBoxes = [];

            //Ground boxes
            Transform[] worldBoxes = SceneManager.GetActiveScene()
                .GetRootGameObjects()
                .Where(g => g.name == "1_Box(Clone)")
                .Select(g => g.transform)
                .ToArray();

            foreach (Transform box in worldBoxes) {
                nonParentedBoxes.Add(box);
            }

            TimeLogger.Logger.LogDebugFunc(() => $"{nonParentedBoxes.Count} non parented boxes populated.", 
                LogCategories.Highlight);
        }

        public static List<Transform> GetNonParentedBoxes() {
            if (nonParentedBoxes == null) {
                return [];
            }

            //Remove ones that do not exist anymore
            nonParentedBoxes = nonParentedBoxes.Where(g => g).ToList();
            return nonParentedBoxes;
        }

        public static bool GetBoxDataFromCharacter(Transform charTransform,
                CharacterSourceType charSource, out Transform boxHighlightT, out int boxProductId) {

            boxHighlightT = null;
            boxProductId = -1;

            GroundBoxHighlightData boxHighlightData = ContainerHighlightData.GroundBox;

            if (charSource == CharacterSourceType.Employee) {
                if (charTransform.TryGetComponent(out NPC_Info npcInfo) && npcInfo.equippedItem == (int)ToolIndexes.Box) {
                    //Need to get the object where the renderer is so it shares the same behaviour as the GroundBox logic.
                    boxHighlightT = npcInfo.instantiatedOBJ.NullableObject()?.transform.Find(boxHighlightData.SQoLName);
                    boxProductId = npcInfo.boxProductID;
                }
            } else if (charSource == CharacterSourceType.LocalPlayer) {
                //Currently unused. I would need to uncomment the local player logic in GetAllHeldBoxesData.
                PlayerNetwork pNetwork = charTransform.GetComponent<PlayerNetwork>();

                if (pNetwork && pNetwork.equippedItem == (int)ToolIndexes.Box) {
                    boxHighlightT = pNetwork.instantiatedOBJ.NullableObject()?.transform.Find(boxHighlightData.SQoLName);
                    boxProductId = pNetwork.extraParameter1;
                }
            } else if (charSource == CharacterSourceType.RemotePlayer) {
                PlayerNetwork pNetwork = charTransform.GetComponent<PlayerNetwork>();

                if (pNetwork && pNetwork.equippedItem == (int)ToolIndexes.Box) {
                    boxHighlightT = pNetwork.instantiatedOBJ.NullableObject()?.transform.Find(boxHighlightData.SQoLName);
                    PlayerSyncCharacter pSyncChar = charTransform.GetComponent<PlayerSyncCharacter>();
                    boxProductId = pSyncChar.syncedProductID;
                }
            }

            return boxHighlightT == true;
        }

        public static List<CharacterData> GetAllHeldBoxesData() {
            List<CharacterData> boxList = new();

            foreach (Transform employeeT in NPC_Manager.Instance.employeeParentOBJ.transform) {
                GetBoxDataFromCharacter(employeeT, CharacterSourceType.Employee, out _, out int heldBoxProductId);
                boxList.Add(new(employeeT, CharacterSourceType.Employee, heldBoxProductId));
            }

            //If I ever want to highlight the player held box
            //boxList.Add(new(SMTInstances.FirstPersonController().transform, CharacterSourceType.LocalPlayer));

            foreach (GameObject playerObj in AuxUtils.GetRemotePlayerObjects()) {
                GetBoxDataFromCharacter(playerObj.transform, CharacterSourceType.RemotePlayer, out _, out int heldBoxProductId);
                boxList.Add(new(playerObj.transform, CharacterSourceType.RemotePlayer, heldBoxProductId));
            }

            return boxList;
        }

        public static void InitializeBoxHighlight(Transform box, Mesh cubeMesh, Vector3 highLightLocalScale) {
            GroundBoxHighlightData boxHighlightData = ContainerHighlightData.GroundBox;

            if (!box || box.Find(boxHighlightData.SQoLName)) {
                return;
            }
            
            GameObject highlightObj = new (ContainerHighlightData.GroundBox.SQoLName);

            highlightObj.AddComponent<MeshRenderer>();
            highlightObj.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            highlightObj.transform.parent = box;

            highlightObj.transform.localPosition = Vector3.zero;
            //TODO 0 Highlight - The mesh used in the meshFilter for the storage boxes is a completely different one.
            //  In fact, the sharedmesh used in a box highlight, is also a different instance? Fix that so they use
            //  the same sharedmesh.
            highlightObj.transform.localScale = highLightLocalScale; //new(0.202f, 0.208f, 0.302f);
            highlightObj.transform.rotation = Quaternion.identity;  //Make sure to keep parent rotation

            highlightObj.SetActive(true);

            CreateInitializedSingleTransformHighlight(highlightObj.transform, ContainerType.GroundBox);
            
        }

        private class PropertiesInitializer {

            public static void SetInitialSingleTransformHighlightProperties(Transform t, HighlightEffect highlightEffect,
                            ContainerType containerType, bool forceInitialization) {

                if (SetInitialHighlightProperties(highlightEffect, containerType, forceInitialization)) {
                    InitializeTransformRenderer(t);
                    HighlightLogic.SetToBaseHighlightState(t, highlightEffect, containerType);
                }
            }

            public static void SetInitialHighlightProperties(HighlightEffect highlightEffect,
                    HighlightTargetCollection highlightTargets, bool forceInitialization) {

                if (SetInitialHighlightProperties(highlightEffect, highlightTargets.ContainerType, forceInitialization)) {
                    foreach (HighlightTarget hObject in highlightTargets.GetActiveObjectSet()) {
                        InitializeTransformRenderer(hObject.Transform);
                        HighlightLogic.SetToBaseHighlightState(hObject.Transform, highlightEffect, highlightTargets.ContainerType);
                    }
                }
            }


            private static bool SetInitialHighlightProperties(HighlightEffect highlightEffect,
                        ContainerType containerType, bool forceInitialization) {

                if (!highlightEffect) {
                    TimeLogger.Logger.LogError($"The highlightEffect parameter cannot be null.", LogCategories.Highlight);
                    return false;
                }

                if (!forceInitialization && IsHighlightEffectInitialized(highlightEffect)) {
                    TimeLogger.Logger.LogError($"The highlightEffect '{highlightEffect.transform}' of type " +
                        $"'{containerType}' tried to be initialized again.", LogCategories.Highlight);
                    return false;
                }

                // ** Common properties **

                highlightEffect.outlineQuality = QualityLevel.High;
                highlightEffect.useSmoothOutline = true;
                //Default mode. Dont see much difference with StencilAndCutout. IgnoreMask makes the whole thing light up.
                highlightEffect.outlineMaskMode = MaskMode.StencilAndCutout;
                //If true, the outline is unique and wont merge with other overlaped highlights behind it
                highlightEffect.outlineIndependent = true;
                //No idea what was this for. I think it didnt matter for what I want
                highlightEffect.outlineContourStyle = ContourStyle.AroundObjectShape;
                //Outline needs this true or looks too thin for some reason.
                highlightEffect.constantWidth = true;

                //Minimize glow width. Even this low is already wide enough, specially at some distance.
                highlightEffect.glowWidth = 0.0001f;
                //Some shelves (product shelves for example) have a custom filter for some reason. This makes it
                //  so the in-shelf products renderers are not included, and count as occluders, which lights them up
                //  and looks wrong. We dont want that so we make sure we dont filter out any.
                //  The performance penalty is practically none, since the only active renderers are the ones for combined products.
                highlightEffect.effectNameFilter = null;

                //Inside borders glow too. Makes easier to see if its in view or occluded by something.
                highlightEffect.glowMaskMode = MaskMode.StencilAndCutout;

                //Faster highlightEffect.Refresh(), pretty much neligible performance improvement,
                //  but still better. Since I dont need the smooth shadowing for this, its a win.
                highlightEffect.normalsOption = NormalsOption.PreserveOriginal;

                //Highlighting that uses object activation (storage slots and ground boxes) have
                //  no fade, since the object gets immediately disabled and the highlight dissapears.
                highlightEffect.fadeInDuration = 0.2f;
                highlightEffect.fadeOutDuration = 0.15f;

                // ** Mode & Container based properties **

                HighlightMode highlightMode = ContainerHighlightManager.GetCurrentHighlightMode();

                highlightEffect.outlineVisibility = HighlightValueDefinitions.GetOutlineVisibility(highlightMode, containerType);
                highlightEffect.glowVisibility = HighlightValueDefinitions.GetGlowVisibility(highlightMode, containerType);
                highlightEffect.glowBlurMethod = HighlightValueDefinitions.GetGlowBlurMethod(highlightMode, containerType);
                highlightEffect.glowQuality = HighlightValueDefinitions.GetGlowQuality(highlightMode, containerType);
                highlightEffect.glowDownsampling = HighlightValueDefinitions.GetGlowDownsampling(highlightMode, containerType);
                highlightEffect.seeThrough = HighlightValueDefinitions.GetSeeThrough(highlightMode, containerType);


                //Highlight properties unique to each HighlightMode. Changing any of these wont cause any 
                //  effect on the other modes, as the "switch" that enables the specific funcionality is off.
                if (highlightMode == HighlightMode.SeeThrough) {
                    highlightEffect.seeThrough = SeeThroughMode.WhenHighlighted;
                    highlightEffect.seeThroughNoise = 0f;
                }/* else if (highlightMode == HighlightMode.PerformanceGlow) {
                    highlightEffect.glowDitheringStyle = GlowDitheringStyle.Noise;
                    highlightEffect.glowDithering = 0; //The lower the number, the closer it looks like a solid outline.
                }*/

                if (HighlightLogic.IsSeparatedHighlightAndRender(containerType) || containerType == ContainerType.ProdShelf) {
                    //For product shelves, we use Children to include all products renderers too, so they
                    //  dont count as occluders for its own shelf, which would trigger a see through.
                    //For separated highlights and renders, there is a single HighlightEffect in a GameObject
                    //  I created for it, and a renderer for each box slot in children GameObjects.
                    //  So we simply let the HighlightEffect search through all children renderers by itself.
                    highlightEffect.effectGroup = TargetOptions.Children;
                } else {
                    //Labels and storage shelves only need to take care of its one renderer.
                    highlightEffect.effectGroup = TargetOptions.OnlyThisObject;
                }

                if (containerType == ContainerType.ProdShelf) {
                    //When the MeshCombiner updates the whole product mesh, the renderers included at this
                    //  point in the highlightEffect will be obsolete. This will cause visual issues, so we
                    //  need to refresh the highlight so it updates its internal renderer list.
                    foreach (Transform t in highlightEffect.transform.Find("ProductContainer")) {
                        if (t.TryGetComponent(out MeshCombiner meshCombiner) && meshCombiner) {
                            meshCombiner.OnRenderersUpdated += (r) => RefreshHighlightRenderers(highlightEffect, r);
                        }
                    }
                }

                highlightEffect.enabled = true;

                highlightEffect.targetFXGroundMaxDistance = InitializedHighlightEffectValue;

                return true;
            }

            private static void RefreshHighlightRenderers(HighlightEffect highlightEffect, Renderer[] renderers) {
                if (highlightEffect && highlightEffect.highlighted) {
                    highlightEffect.Refresh();
                }
            }

            private static void InitializeTransformRenderer(Transform t) {
                if (IsHighlightTransformInitialized(t)) {
                    return;
                }

                //Necessary so we can disable occlusion culling later.
                Renderer render = t.GetComponent<Renderer>();
                foreach (var mat in render.materials) {
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
                }

                //Flag as initialized
                t.gameObject.AddComponent<HighlightInitialized>();
            }
        }


        private class HighlightInitialized : MonoBehaviour { }

    }
}
