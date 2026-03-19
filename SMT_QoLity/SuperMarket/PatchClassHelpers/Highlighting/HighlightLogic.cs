using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using HighlightPlus;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Caching;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting.Definitions;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting {

    public static class HighlightLogic {

        public static bool IsHighlightActive(Transform t, HighlightEffect highlightEffect, ContainerType containerType) {
            if (containerType.UsesObjActivation()) {
                return IsSeparatedHighlightAndRender(containerType) && t.gameObject.activeSelf ||
                !IsSeparatedHighlightAndRender(containerType) && highlightEffect.gameObject.activeSelf;
            } else {
                //When a highlight is fading out, it doesnt set the highlighted property to false until
                //  it finishes, so we use the fading property to really know whats the real target state.
                //  This doesnt happen when the highlight is fading in because the highlighted property
                //  is set immediately regardless of fading, for some reason.
                return highlightEffect.highlighted && highlightEffect.fading != HighlightEffect.FadingState.FadingOut;
            }
        }
            


        /// <summary>
        /// If it uses the method of GameObject activation/deactivation. 
        /// Otherwise the highlight component gets enabled/disabled.
        /// </summary>
        private static bool UsesObjActivation(this ContainerType containerType) =>
            containerType == ContainerType.StorageSlot || containerType == ContainerType.GroundBox;

        public static bool IsSlotOrBox(this ContainerType containerType) =>
            containerType == ContainerType.StorageSlot ||
                containerType == ContainerType.ProdShelfSlot ||
                containerType == ContainerType.GroundBox;

        public static bool IsBox(this ContainerType containerType) =>
            containerType == ContainerType.StorageSlot ||
                containerType == ContainerType.GroundBox;


        public static bool IsSeparatedHighlightAndRender(ContainerType containerType) =>
            containerType == ContainerType.StorageSlot;


        public static void SetHighlighting(HighlightEffect highlightEffect, HighlightTargetCollection highlightTargets, bool? isEnableHighlight = null) {
            if (!HighlightInitialization.IsHighlightEffectInitialized(highlightEffect)) {
                //At this point in the call stack, if it reaches here without being
                //  initialized is because I fucked up something.
                TimeLogger.Logger.LogError($"Highlight '{highlightEffect.transform.parent}' of type " +
                    $"{highlightTargets.ContainerType} should have been initialized already but its not.", 
                    LogCategories.Highlight);
            }

            bool highlightChanged = false;
            foreach (HighlightTarget hObject in highlightTargets.GetActiveObjectSet()) {
                bool enableHighlight = isEnableHighlight ?? hObject.HighlightStatus;
                highlightChanged |= SetHighlighting(hObject.Transform, highlightEffect,
                    enableHighlight, highlightTargets.ContainerType);
            }

            if (highlightChanged) {
                SetChangeableHighlightProperties(highlightEffect, highlightTargets.ContainerType);
            }
        }

        public static void SetSingleTransformHighlighting(Transform t, ContainerType containerType, bool isEnableHighlight) {
            if (!GetInitializedSingleTransformHighlight(t, containerType, out HighlightEffect highlightEffect)) {
                //For those types of objects that we dont initialize on load
                highlightEffect = HighlightInitialization.CreateInitializedSingleTransformHighlight(t, containerType);
            }

            bool highlightChanged = SetHighlighting(t, highlightEffect, isEnableHighlight, containerType);
            if (highlightChanged) {
                SetChangeableHighlightProperties(highlightEffect, containerType);
            }
        }


        private static bool SetHighlighting(Transform t, HighlightEffect highlightEffect, bool isEnableHighlight, ContainerType containerType) {
            if (CanSkipHighlighting(t, highlightEffect, isEnableHighlight, containerType)) {
                return false;
            }

            SetHighlightState(t, highlightEffect, isEnableHighlight, containerType, initialization: false);
            return true;
        }

        private static bool CanSkipHighlighting(Transform t, HighlightEffect highlightEffect,
                bool isEnableHighlight, ContainerType containerType) {

            if (isEnableHighlight && ContainerHighlightManager.GetCurrentHighlightMode() == HighlightMode.Disabled) {
                TimeLogger.Logger.LogWarning($"Transform {t} was going to enable its highlighting but the " +
                    $"current highlight mode is {nameof(HighlightMode.Disabled)}. Skipping." +
                    $"Make sure to check earlier in the call chain if highlights are disabled to avoid " +
                    $"unnecessary work.", LogCategories.Highlight);
                return true;
            }

            if (IsHighlightActive(t, highlightEffect, containerType) == isEnableHighlight) {
                //No need to do anything as the current status is already the target status.
                return true;
            }

            return false;
        }

        public static void SetToBaseHighlightState(Transform t,
                HighlightEffect highlightEffect, ContainerType containerType) {
            SetHighlightState(t, highlightEffect, isEnableHighlight: false, containerType, initialization: true);
        }

        private static void SetHighlightState(Transform t, HighlightEffect highlightEffect,
                bool isEnableHighlight, ContainerType containerType, bool initialization) {

            //Force highlight fading to finish if there was one in progress.
            if (highlightEffect.fading == HighlightEffect.FadingState.FadingOut && isEnableHighlight) {
                highlightEffect.ImmediateFadeOut();
            } else if (highlightEffect.fading == HighlightEffect.FadingState.FadingIn && !isEnableHighlight) {
                highlightEffect.fading = HighlightEffect.FadingState.NoFading;
            }

            //To show or hide most highlights, the HighlightEffect component "SetHighlight" method is called, but 
            //with ones using object activation its different, because there are so many of them that all the extra active
            //GameObjects impact performance, even when nothing is being highlighted.
            //For them, the highlightinging is always enabled in the HighlightEffect component, but its
            //  GameObject is the one that is activated and deactivated on demand.
            if (containerType.UsesObjActivation()) {
                if (initialization) {
                    //Force build up rms from renderers added from a prefab.
                    highlightEffect.Refresh();

                    highlightEffect.SetHighlighted(true);
                }

                t.gameObject.SetActive(isEnableHighlight);
            } else {
                highlightEffect.SetHighlighted(isEnableHighlight);
            }

            if (isEnableHighlight) {
                ContainerHighlightManager.HighlightCache.TryAddHighlightedTarget(highlightEffect, t, containerType);
            }

            UpdateTransformHighlightRendering(t, highlightEffect, containerType, isEnableHighlight);
        }

        private static bool GetInitializedSingleTransformHighlight(Transform t,
                ContainerType containerType, out HighlightEffect highlightEffect) {

            if (IsSeparatedHighlightAndRender(containerType)) {
                TimeLogger.Logger.LogError($"This method is for single transforms and not valid " +
                    $"for object type {containerType}.", LogCategories.Highlight);
            }

            if (!t.TryGetComponent(out highlightEffect)) {
                return false;
            }

            HighlightInitialization.InitializeSingleTransformHighlightIfNeeded(t, highlightEffect, containerType);

            return true;
        }

        public static void SetChangeableHighlightProperties(HighlightEffect highlightEffect, ContainerType containerType) {

            if (ContainerHighlightManager.GetCurrentHighlightMode() == HighlightMode.Disabled) {
                return;
            }

            ChangeHighlightColor(highlightEffect, containerType);

            //Force a refresh:
            //  - Fixes changing box positions inside storage, where it would still keep old slots highlighted.
            //  - Updates Renderers in highlightEffect.rms, so we can loop them to change occlusion.
            //  - Updates see-through Shaders internally.
            //  - Probably helps for cases I dont even know about, since this refresh was one of the first
            //      things added in this mod to fix the original betterSMT.
            highlightEffect.Refresh();

            //In product shelves, I need to set the allowOcclusionWhenDynamic of all combined meshes
            //  of products renderers too, so they dont dissapear at certain angles.
            if (containerType == ContainerType.ProdShelf) {
                for (int j = 0; j < highlightEffect.rmsCount; j++) {
                    Renderer renderer = highlightEffect.rms[j].renderer;
                    if (renderer.enabled) {
                        bool isEnabledHighlight = IsHighlightActive(highlightEffect.transform, 
                            highlightEffect, containerType);

                        renderer.allowOcclusionWhenDynamic = !isEnabledHighlight;
                    }
                }
            }
        }

        private static void ChangeHighlightColor(HighlightEffect highlightEffect, ContainerType containerType) {
            Color color = containerType switch {
                ContainerType.ProdShelf => ModConfig.Instance.ShelfHighlightColorRGBA.Value,
                ContainerType.ProdShelfSlot => ModConfig.Instance.ShelfLabelHighlightColorRGBA.Value,
                ContainerType.Storage => ModConfig.Instance.StorageHighlightColorRGBA.Value,
                ContainerType.StorageSlot => ModConfig.Instance.StorageSlotHighlightColorRGBA.Value,
                ContainerType.GroundBox => ModConfig.Instance.StorageSlotHighlightColorRGBA.Value,
                _ => throw new NotImplementedException(containerType.ToString()),
            };

            highlightEffect.outlineColor = color;
            highlightEffect.glowHQColor = color;
            highlightEffect.seeThroughTintColor = color;

            float alphaStrength = color.a;

            HighlightMode highlightMode = ContainerHighlightManager.GetCurrentHighlightMode();
            highlightEffect.outline = alphaStrength * HighlightValueDefinitions.GetOutlineStrength(highlightMode, containerType);
            highlightEffect.glow = alphaStrength * HighlightValueDefinitions.GetGlowStrength(highlightMode, containerType, alphaStrength);
            highlightEffect.outlineWidth = HighlightValueDefinitions.GetOutlineWidth(highlightMode, containerType, alphaStrength);

            if (highlightMode == HighlightMode.SeeThrough) {
                //How much of the see-through effect is converted into seeThroughTintColor.
                //No matter how much I tried, the storage slot boxes see-through only has a red tint. I give up.
                highlightEffect.seeThroughTintAlpha = Mathf.Lerp(0, 0.8f, alphaStrength);
                //Opacity. Even though it can go up to 5, everything above 1 is just heavily overbloomed
                highlightEffect.seeThroughIntensity = alphaStrength;
            }
        }

        private static void UpdateTransformHighlightRendering(Transform t, HighlightEffect highlightEffect,
                ContainerType containerType, bool isEnableHighlight) {

            //Make the highlighted object ignore occlusion culling, so it keeps showing up behind walls
            t.GetComponent<Renderer>().allowOcclusionWhenDynamic = !isEnableHighlight;

            //Disable the LODGroup while its label is highlighted, so it doesnt dissapear with distance.
            if (containerType == ContainerType.ProdShelfSlot && t.TryGetComponent(out LODGroup lodGroup)) {
                lodGroup.enabled = !isEnableHighlight;
            }
        }

    }

}
