using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using HighlightPlus;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Caching;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Definitions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting {

    //TODO 0 Performance - Test performance again in the dedicated server, with just the big store and no extreme highlighting.
    /*  This was a extreme test with hundreds of storage slots highlighted. 
     *  Basically impossible conditions in normal play, and still not all that much
     *  performance difference between them. But on a worse gpu it ll have more of an impact.
     
        Disabled:              95
        OutlineOnly:        29-31
        OutlineGlow:        29-31
        OutlineBlurredGlow: 26-27
        SeeThrough:         24-25

        Values were much different when I did an unrelated test with ground boxes on the dedicated server. 
        There were like 160 boxes or so? and performance went to shit from 50something to 12 fps.
        I guess its more about numbers than mesh complexity.
    */
    public enum HighlightMode {
        [Description("Disabled")]
        Disabled,
        /// <summary>Shows only the outline of the objects. Closest thing to the original highlighting.</summary>
        [Description("Outline only")]        
        OutlineOnly,
        /* This mode didnt quite work out. Colors didnt work with storage, labels wouldnt show, and a 
         * series of things that I solved for the other modes, in this they somehow didnt work.
        /// <summary>Subtle glow. Uses High quality instead of Highest. Enables Dithering options.</summary>
        [Description("Lower quality glow with outline")]
        PerformanceGlow,
        */
        /// <summary>High quality glow using Kawase</summary>
        [Description("Outline & glow")]
        OutlineGlow,
        /// <summary>High quality glow using Gaussian. Gets increassingly blurrier with distance.</summary>
        [Description("Outline & blurred glow")]
        OutlineBlurredGlow,
        /// <summary>Uses the see-through mode to show the entire mesh through occluders.</summary>
        [Description("See-through")]
        SeeThrough,
    }


    //TODO 0 Highlight - If you have the build menu open, many things happen.
    //  Targetting a storage shelf will enable the vanilla highlighting, overwritting some settings, but whatever
    //  property is not overwritting will stay the same way as the previous visual mode. This is specially notorious
    //  In see-through mode.
    //  Some storage show as red? I guess this is a vanilla something but I have no idea right now.
    //  Some existing highlights will stop showing, like storage.
    //It ll be easier to have an event when the build menu opens/closes, and disable/restore
    //  my highlights and thats it. The restoring might have to reinitialize ALL highlights again? Since I dont know
    //  which ones the user has hovered over (supposing thats really fucking up stuff permanently).
    //  Remember that I have a patch for opening the building menu with moving/cloning. Whatever way I hook the menu open
    //  event, it has to work with that too.

    //TODO 3 - This is something for the future, but I could change the highlight mode cycling to a radial wheel.
    //  Right now the radial has no support for multiple radials until I fix how initial attributes are set.

    //TODO 5 - Even after the rework, this whole thing ended patched to hell after I had to change the storage slots
    //  to use a single HighlightRenderer for all of them. I have to branch everywhere depending on if its
    //  using the old type or the new one, and the code is a fucking mess of similar methods doing something
    //  with different parameters, like the "SingleTransform" methods in HighlightLogic.
    //  - Old: Each transform has its own HighlightEffect that enables or disables on demand.
    //  - New: Single HighlightEffect always enabled. Each slot has its own Renderer and their GameObjects 
    //      are activated and deactivated so the renderer is included or not in the HighlightEffect.
    //      Changing highlighting is a little slower, though still not noticeable, and performance is better
    //      because there are less active GameObjects with their MeshRenderers.
    //  Make all highlights use the new system, not for performance but for code sanity and maintainability.

    //TODO 0 Performance - Test highlight performance but also general superqolity performance,
    //  - Does the game run worse just having superqolity installed?
    //  - Does the game run worse just having superqolity installed but all modules disabled?
    //  If true to any of the above, start disabling modules and try to find whats doing it. Use Mono Profiler too.

    public static class ContainerHighlightManager {

        private static readonly int maxHighlightEnumValue = Enum.GetValues(typeof(HighlightMode)).Cast<int>().Max();


        private static int highlightedProductId;

        public static HighlightContainerCache HighlightCache { get; private set; }


        public static void InitHighlightManager() {
            WorldState.OnLoadingWorld += () => {
                HighlightCache = new();
                highlightedProductId = -1;
            };

            WorldState.OnFPControllerStarted += () => HighlightInitialization.PopulateNonParentedBoxes();

            BoxPrefabPatching.PrepareBoxPrefabPatching();
            var a = GameObject.FindObjectsByType<Material>(FindObjectsSortMode.None);
            foreach (var mat in a) {
                if (mat.name == "Back3 (Instance)") {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                    break;
                }
            }
        }


        public static HighlightMode GetCurrentHighlightMode() => ModConfig.Instance.HighlightVisualMode.Value;


        public static bool IsAnyContainerHighlighted() => highlightedProductId >= 0 && HighlightCache.HasActiveObjects();

        public static void ChangeToNextHighlightMode() {
            //Cycle to next highlight mode
            var highlightMode = ModConfig.Instance.HighlightVisualMode;

            //Changing Value triggers the SettingChanged event and calls UpdateHighlightMode.
            if ((int)highlightMode.Value >= maxHighlightEnumValue) {
                highlightMode.Value = 0;
            } else {
                highlightMode.Value++;
            }

            TimeLogger.Logger.SendMessageNotification(LogTier.Info, 
                $"Highlight mode =>  '{highlightMode.Value.GetDescription()}'", skipQueue: true);

            ConfigManagerController.RefreshGUI();
        }

        public static void UpdateHighlightMode() {
            Dictionary<HighlightEffect, HighlightTargetCollection> highlightedObjectsTemp = null;
            if (IsAnyContainerHighlighted()) {
                //Shallow clone the highlight list to keep for reference.
                highlightedObjectsTemp = new(HighlightCache.GetActiveCachedObjects());

                ClearHighlightedContainers(clearProductIdFlag: false);
            }

            if (GetCurrentHighlightMode() == HighlightMode.Disabled) {
                return;
            }

            //Apply to all container highlights the values of the current visual mode.
            HighlightInitialization.ReinitializeAllContainerHighlights();

            //If any containers were highlighted before, enable those again.
            if (highlightedObjectsTemp != null) {
                foreach (var highlightEffectGroup in highlightedObjectsTemp) {
                    HighlightLogic.SetHighlighting(highlightEffectGroup.Key, highlightEffectGroup.Value);
                }
            } else if (GetCurrentHighlightMode() != HighlightMode.Disabled &&
                    AuxUtils.IsPlayerHoldingBox(out int productId)) {
                //When cycling from HighlightMode.Disabled to the next one, there is no recorded cache of
                //  highlighted objects anymore, so we manually do the highlight if the player is holding a box.
                HighlightContainersByProduct(productId);
            }
        }

        public static void ToggleCrosshairInProductHighlight() {
            if (GetCurrentHighlightMode() == HighlightMode.Disabled) {
                TimeLogger.Logger.SendMessageNotification(LogTier.Message, "Highlight is currently disabled in the settings", true);
                return;
            }

            PlayerNetwork pNetwork = SMTInstances.LocalPlayerNetwork();

            bool wasSomethingHighlighted = IsAnyContainerHighlighted();
            ClearHighlightedContainers(clearProductIdFlag: true);

            int targetedProductId = pNetwork.oldCanvasProductID;
            if (targetedProductId >= 0 || IsGroundBoxOnCrosshair(pNetwork, out targetedProductId)) {
                HighlightContainersByProduct(targetedProductId);
            } else if (!wasSomethingHighlighted && AuxUtils.IsPlayerHoldingBox(out int productId)) {
                //If there was no highlight showing and there is a box in your hands,
                //  highlight the box product. Basically a toggle when not looking at anything.
                //  That way you can clear and show again highlights for the box you are holding
                //  without having to drop it or pick it up again.
                HighlightContainersByProduct(productId);
            }
        }

        private static bool IsGroundBoxOnCrosshair(PlayerNetwork pNetwork, out int targetedProductId) {
            targetedProductId = -1;

            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, 
                    out var hitInfo3, 4f, pNetwork.interactableMask)) {

                if (hitInfo3.transform.TryGetComponent(out BoxData boxData)) {
                    int productId = boxData.productID;

                    if (productId >= 0) {
                        targetedProductId = productId;
                        return true;
                    }
                }
            }

            return false;
        }

        public static void UpdateHighlightColorsFromSettings(ContainerTypeFlags containerTypeFlags) {
            if (GetCurrentHighlightMode() == HighlightMode.Disabled) {
                return;
            }

            foreach (var highlightEffectGroup in HighlightCache.GetActiveCachedObjects()) {
                ContainerType containerType = highlightEffectGroup.Value.ContainerType;

                //ContainerTypeFlags and ContainerType are cross-value compatible
                if (containerTypeFlags.HasFlag((ContainerTypeFlags)containerType)) {
                    HighlightLogic.SetChangeableHighlightProperties(highlightEffectGroup.Key, containerType);
                }
            }
        }

        public static void AddHighlightMarkersToStorage(Transform storage) {
            StorageShelfHighlightData storageHighlightData = ContainerHighlightData.Storage;

            Transform highlightsMarker = storage.Find(storageHighlightData.SQoLName);

            //Skip if it already exists. This method also gets called when an employee puts a box in storage.
            if (highlightsMarker) {
                return;
            }

            GameObject vanillaHighlightsParent = storage.Find(storageHighlightData.VanillaName).gameObject;
            highlightsMarker = Object.Instantiate(vanillaHighlightsParent, storage).transform;
            highlightsMarker.name = storageHighlightData.SQoLName;

            HighlightTargetCollection objCollection = new(ContainerType.StorageSlot);

            for (int i = 0; i < highlightsMarker.childCount; i++) {
                Transform t = highlightsMarker.GetChild(i).GetChild(0);

                //Destroy the original HighlightEffect component. Instead we ll have a single
                //  HighlightEffect that will control all sub renderers, and those renderers
                //  gameobjects will be disabled/enabled as needed.
                if (t.TryGetComponent(out HighlightEffect he)) {
                    Object.Destroy(he);
                }

                objCollection.TryAddHighlightTarget(t, false);
            }

            //Initialize highlighting for slots
            HighlightEffect highlightEffect = highlightsMarker.gameObject.AddComponent<HighlightEffect>();
            HighlightInitialization.InitializeHighlightProperties(highlightEffect, objCollection);

            //Initialize storage highlighting
            HighlightInitialization.InitializeSingleTransformHighlightProperties(storage, ContainerType.Storage);
        }

        
        public static void UpdateBoxHighlight(Transform box) {
            if (!box) {
                TimeLogger.Logger.LogError("Box spawned but transform parameter is null.", LogCategories.Highlight);
                return;
            }

            UpdateContainerHighlighting(box, ParentContainerType.GroundBox);
        }

        public static void ClearHighlightedContainers(bool clearProductIdFlag) {
            if (!IsAnyContainerHighlighted()) {
                return;
            }

            if (clearProductIdFlag) {
                highlightedProductId = -1;
            }

			foreach (var highlightEffectGroup in HighlightCache.GetActiveCachedObjects()) {
                HighlightLogic.SetHighlighting(highlightEffectGroup.Key, highlightEffectGroup.Value, isEnableHighlight: false);
			}

            HighlightCache.ClearCache();
        }

        public static void HighlightContainersByProduct(int productID) {
            if (GetCurrentHighlightMode() == HighlightMode.Disabled) {
                return;
            }

            highlightedProductId = productID;

            HighlightContainerTypeByProduct(productID, ParentContainerType.ProductDisplay);
            HighlightContainerTypeByProduct(productID, ParentContainerType.Storage);
            HighlightContainerTypeByProduct(productID, ParentContainerType.GroundBox);
            UpdateAllPlayersAndEmployeesHeldBoxes();
        }
        
        private static void HighlightContainerTypeByProduct(int productID, ParentContainerType parentContainerType) {
			Transform[] parentedContainerObjects = ContainerHighlightData.GetGameObjectFromParentContainerType(parentContainerType);

			foreach (Transform containerObj in parentedContainerObjects) {
                UpdateContainerHighlighting(containerObj, productID, parentContainerType);
			}

            if (parentContainerType == ParentContainerType.GroundBox && WorldState.CurrentOnlineMode == GameOnlineMode.Client) {
                //Handle ground boxes that existed before the client joined the store.
                foreach (Transform box in HighlightInitialization.GetNonParentedBoxes()) { 
                    UpdateContainerHighlighting(box, productID, parentContainerType);
                }
            }
        }

        public static void UpdateContainerHighlighting(Transform container, ParentContainerType parentContainerType) {
            if (GetCurrentHighlightMode() == HighlightMode.Disabled) {
                return;
            }

            UpdateContainerHighlighting(container, highlightedProductId, parentContainerType);
        }

        private static void UpdateContainerHighlighting(Transform container, int productID, ParentContainerType parentContainerType) {
            if (parentContainerType == ParentContainerType.GroundBox) {
                bool enableBoxHighlight = productID >= 0 && container.GetComponent<BoxData>().productID == productID;

                GroundBoxHighlightData boxHighlightData = ContainerHighlightData.GroundBox;
                Transform highlightsMarker = container.Find(boxHighlightData.SQoLName);

                if (!highlightsMarker) {
                    TimeLogger.Logger.LogError("The highlightsMarker object for this box " +
                        "could not be found. Highlighting wont work in this ground box.", LogCategories.Highlight);
                    return;
                }

                ContainerType containerType = (ContainerType)parentContainerType;
                HighlightEffect highlightEffect = highlightsMarker.GetComponent<HighlightEffect>();
                if (HasDifferentHighlightState(enableBoxHighlight, highlightsMarker, highlightEffect, containerType)) {
                    HighlightLogic.SetSingleTransformHighlighting(highlightsMarker, containerType, enableBoxHighlight);
                }
            } else if (parentContainerType == ParentContainerType.ProductDisplay || parentContainerType == ParentContainerType.Storage) {
                //Shelf and its slots
                int[] productInfoArray = container.GetComponent<Data_Container>().productInfoArray;
                int num = productInfoArray.Length / 2;
                bool enableShelfHighlight = false;

                ContainerHighlightData shelfHighlightData = ContainerHighlightData.GetFromContainerParentType(parentContainerType);
                Transform highlightsMarker = container.Find(shelfHighlightData.SQoLName);
                if (!highlightsMarker) {
                    TimeLogger.Logger.LogError($"The highlightsMarker object for this container '{container}' " +
                        $"(instanceId {container.GetInstanceID()}) of type {shelfHighlightData.ParentContainerType} " +
                        $"could not be found. Highlighting wont work in this object.", LogCategories.Highlight);
                    return;
                }

                //Get corresponding slot object type from the shelf type.
                ContainerType containerType = parentContainerType switch{
                    ParentContainerType.ProductDisplay => ContainerType.ProdShelfSlot,
                    ParentContainerType.Storage => ContainerType.StorageSlot,
                    _ => throw new NotSupportedException($"Container type {parentContainerType} is not valid here.")
                };

                //This line below is only really used for storage right now.
                HighlightTargetCollection objCollection = new(containerType);

                for (int j = 0; j < num; j++) {
                    bool enableSlotHighlight = false;
                    if (productID >= 0 && productInfoArray[j * 2] == productID) {
                        //Slot has same product id and should be highlighted if the setting is enabled.
                        enableSlotHighlight = true;
                        enableShelfHighlight = true;
                    }

                    ProcessShelfSlotHighlight(highlightsMarker, j, containerType, enableSlotHighlight, objCollection);
                }

                if (parentContainerType == ParentContainerType.Storage) {
                    //Highlight all storage slots in one pass using the more performant system
                    HighlightEffect highlightEffect = highlightsMarker.GetComponent<HighlightEffect>();
                    HighlightLogic.SetHighlighting(highlightEffect, objCollection);
                }

                //Highlight the entire shelf
                HighlightLogic.SetSingleTransformHighlighting(container, (ContainerType)parentContainerType, enableShelfHighlight);
            }
        }

        public static void UpdateHeldBoxHighlighting(Transform charTransform, Transform boxTransform, 
                int productIdBox, CharacterSourceType charSource) {

            UpdateHeldBoxHighlighting(charTransform, charSource, productIdBox);
        }

        /// <summary>
        /// Gets held boxes from all players and employees, and updates its highlight status.
        /// </summary>
        public static void UpdateAllPlayersAndEmployeesHeldBoxes() {
            foreach (CharacterData boxData in HighlightInitialization.GetAllHeldBoxesData()) {
                UpdateHeldBoxHighlighting(boxData.CharTransform, boxData.CharSource, boxData.HeldProductId);
            }
        }

        public static void UpdateHeldBoxHighlighting(Transform charTransform, 
                CharacterSourceType charSource, int productIdBox) {

            if (HighlightInitialization.GetBoxDataFromCharacter(charTransform, 
                        charSource, out Transform boxHighlightT, out _) &&
                    IsHeldBoxNeedUpdate(boxHighlightT, productIdBox, out bool enableHighlight)) {

                HighlightLogic.SetSingleTransformHighlighting(boxHighlightT, ContainerType.GroundBox, enableHighlight);
            }
        }

        private static bool IsHeldBoxNeedUpdate(Transform boxHighlightT, int productIdBox, out bool enableHighlight) {

            if (boxHighlightT && boxHighlightT.TryGetComponent(out HighlightEffect hEffect)) {
                enableHighlight = productIdBox == highlightedProductId;

                bool isBoxHighlighted = HighlightLogic.IsHighlightActive(boxHighlightT, hEffect, ContainerType.GroundBox);
                return enableHighlight != isBoxHighlighted;
            }

            enableHighlight = false;
            return false;
        }

        private static void ProcessShelfSlotHighlight(Transform highlightsMarker, int slotIndex,
                ContainerType containerType, bool enableSlotHighlight, HighlightTargetCollection objCollection) {

            Transform t;
            HighlightEffect highlightEffect;

            if (containerType == ContainerType.StorageSlot) {
                t = highlightsMarker.GetChild(slotIndex).GetChild(0);
                highlightEffect = highlightsMarker.GetComponent<HighlightEffect>();
            } else if (containerType == ContainerType.ProdShelfSlot) {
                t = highlightsMarker.GetChild(slotIndex);
                highlightEffect = t.GetComponent<HighlightEffect>();
            } else {
                throw new NotSupportedException($"Container {containerType} is not valid.");
            }

            if (HasDifferentHighlightState(enableSlotHighlight, t, highlightEffect, containerType)) {
                if (containerType == ContainerType.StorageSlot) {
                    objCollection.TryAddHighlightTarget(highlightsMarker.GetChild(slotIndex).GetChild(0), enableSlotHighlight);
                } else {
                    HighlightLogic.SetSingleTransformHighlighting(highlightsMarker.GetChild(slotIndex),
                        ContainerType.ProdShelfSlot, enableSlotHighlight);
                }
            }
        }

        private static bool HasDifferentHighlightState(bool enableSlotHighlight,
                Transform transform, HighlightEffect highlightEffect, ContainerType containerType) {

            if (!highlightEffect && HighlightInitialization.IsPreinitializedContainer(containerType)) {
                TimeLogger.Logger.LogError($"The HighlightEffect object for this container '{transform}' " +
                    $"container of type {containerType} could not be found but it should exist.",
                    LogCategories.Highlight);
            }

            //Its ok for the highlight not to be initialized at this point
            bool isHighlightActive = highlightEffect ?
                HighlightLogic.IsHighlightActive(transform, highlightEffect, containerType) :
                false;

            return enableSlotHighlight != isHighlightActive;
        }

    }

    public record struct CharacterData(Transform CharTransform, CharacterSourceType CharSource, int HeldProductId);

}
