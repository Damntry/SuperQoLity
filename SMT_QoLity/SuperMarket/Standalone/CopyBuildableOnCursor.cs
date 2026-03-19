using Damntry.UtilsUnity.Components.InputManagement;
using HighlightPlus;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Standalone {

    /// <summary>
    /// Satisfactory like behaviour to clone or start moving the object we are currently looking at when pressing a hotkey
    /// </summary>
    public class CopyBuildableOnCursor {

        private enum BuildTargetAction {
            Clone,
            Move
        }


        public static void AddHotkeys() {
            //These are a bit special, being the only functionality so far that doesnt depend
            //  on any patch for them to work, so we initialize them no matter what.
            InputManagerSMT.Instance.AddHotkeyFromConfig(ModConfig.Instance.CloneBuildHotkey, InputState.KeyDown,
                HotkeyActiveContext.WorldLoadedNotPaused, BeginCloneBuildTarget);
            InputManagerSMT.Instance.AddHotkeyFromConfig(ModConfig.Instance.MoveBuildHotkey, InputState.KeyDown,
                HotkeyActiveContext.WorldLoadedNotPaused, BeginMoveBuildTarget);
        }

        public static void BeginCloneBuildTarget() {
            BeginBuildTargetAction(BuildTargetAction.Clone);
        }

        public static void BeginMoveBuildTarget() {
            BeginBuildTargetAction(BuildTargetAction.Move);
        }

        private static void BeginBuildTargetAction(BuildTargetAction copyMode) {
            Builder_Main builderMain = SMTInstances.BuilderMain();

            try {
                //Conditions where the builder menu cannot be opened, so nothing can be cloned or moved.
                if (builderMain.cCameraController.isInCameraEvent || FirstPersonController.Instance.inVehicle ||
                                !builderMain.pComponent.RequestGP() || !builderMain.pComponent.RequestMP() ||
                                builderMain.pNetworkComponent.equippedItem == 5) {
                    return;
                }
                if (copyMode == BuildTargetAction.Move && builderMain.recentlyMoved) {

                    if (builderMain.hEffect) {
                        builderMain.hEffect.enabled = false;
                    }
                    if (builderMain.hEffect2) {
                        builderMain.hEffect2.highlighted = false;
                    }

                    return;
                }

                Transform camTransform = Camera.main.transform;
                if (Physics.Raycast(camTransform.position, Camera.main.transform.forward,
                            out RaycastHit rayHit, ModConfig.Instance.CloneMoveTargetDistance.Value, builderMain.lMask)) {

                    Transform rayHitT = rayHit.transform;
                    IndexSearchResult searchResult = FindBuildMenuIndexes(builderMain, ref rayHitT);
                    if (!searchResult.Found) {
                        return;
                    }

                    builderMain.currentTabIndex = searchResult.TabIndex;

                    if (copyMode == BuildTargetAction.Clone) {
                        builderMain.currentElementIndex = searchResult.MenuIndex;
                    } else if (copyMode == BuildTargetAction.Move) {
                        builderMain.currentElementIndex = 0;    //Fixed index of the Placement Mode menu
                    }

                    //Since we can switch from Move to Clone mode seamlessly, without performing a vanilla
                    //  menu action that would reset the build state, we force it.
                    builderMain.DeactivateBuilder();

                    builderMain.SetDummy(builderMain.currentTabIndex, builderMain.currentElementIndex);

                    //Open builder menu for the vanilla code in the Update() to do its work.
                    builderMain.cCameraController.ChangeLayerMask(set: true);
                    builderMain.canvasBuilderOBJ.SetActive(true);

                    if (copyMode == BuildTargetAction.Move && !MoveBehaviour(rayHitT)) {
                        //Couldnt start the Placement mode of the targeted object.
                        //  Close build menu so it gets cleaned up on the finally
                        builderMain.canvasBuilderOBJ.SetActive(false);
                    }
                }
            } finally {
                //Make sure to clear leftover effects if the build menu ends up not being open
                if (!builderMain.canvasBuilderOBJ.activeSelf) {
                    builderMain.DeactivateBuilder();
                }
            }
        }

        /// <summary>
        /// Tries to find the buildObj in the building menu, and if successful,
        /// returns its tab index and element index.
        /// </summary>
        /// <param name="builderMain">Instance of the Builder_Main class</param>
        /// <param name="buildObj">
        /// Transform of the build object. This value can be updated by reference when searching up its object hierarchy
        /// </param>
        private static IndexSearchResult FindBuildMenuIndexes(Builder_Main builderMain, ref Transform buildObj) {
            if (buildObj.CompareTag(Tags.Interactable)) {
                //This is so I can get the storage shelf when pointing at any of its box
                //  subcontainers, but it might aswell be useful for something else.
                while (buildObj.parent && !buildObj.CompareTag(Tags.Movable) && !buildObj.CompareTag(Tags.Decoration)) {
                    buildObj = buildObj.parent;
                }
            }

            if (buildObj.CompareTag(Tags.Movable)) {
                Data_Container dataContainer = buildObj.GetComponent<Data_Container>();
                return FindItemIdInBuildMenu(builderMain, 0, dataContainer.containerID);
            } else if (buildObj.CompareTag(Tags.Decoration)) {
                BuildableInfo buildInfo = buildObj.GetComponent<BuildableInfo>();

                for (int i = 1; i < builderMain.tabContainerOBJ.transform.childCount; i++) {
                    IndexSearchResult result = FindItemIdInBuildMenu(builderMain, i, buildInfo.decorationID);
                    if (result.Found) {
                        return result;
                    }
                }
            }

            return new(false, -1, -1);
        }

        private static IndexSearchResult FindItemIdInBuildMenu(Builder_Main builderMain, int tabIndex, int itemId) {
            Transform container = builderMain.tabContainerOBJ.transform.GetChild(tabIndex).transform.Find("Container");
            for (int menuIndex = 0; menuIndex < container.childCount; menuIndex++) {
                Transform menuItem = container.GetChild(menuIndex);

                if (!menuItem.GetComponent<PlayMakerFSM>()) {
                    continue;
                }

                int menuId = menuItem.GetComponent<PlayMakerFSM>().FsmVariables.GetFsmInt("PropIndex").Value;

                if (menuId == itemId) {
                    return new(true, tabIndex, menuIndex);
                }
            }
            return new(false, -1, -1);
        }


        private static bool MoveBehaviour(Transform rayHit) {
            Builder_Main builderMain = SMTInstances.BuilderMain();
            
            if (rayHit.CompareTag(Tags.Movable)) {
                if (builderMain.oldHitOBJ2 && rayHit.gameObject != builderMain.oldHitOBJ2 && builderMain.hEffect2) {
                    builderMain.hEffect2.highlighted = false;
                }
                builderMain.hEffect2 = rayHit.GetComponent<HighlightEffect>();
                builderMain.hEffect2.highlighted = true;
                builderMain.oldHitOBJ2 = rayHit.gameObject;
                //if (MainPlayer.GetButtonDown("Clone")) {
                    builderMain.currentMovedOBJ = rayHit.gameObject;
                    Data_Container component = builderMain.currentMovedOBJ.GetComponent<Data_Container>();
                    component.AddMoveEffect();
                    builderMain.buildableTag = component.buildableTag;
                    GameObject dummyPrefab = component.dummyPrefab;
                    builderMain.dummyOBJ = Object.Instantiate(dummyPrefab, 
                        Vector3.zero, builderMain.currentMovedOBJ.transform.rotation);
                    builderMain.canPlace = false;
                    builderMain.pmakerFSM = builderMain.dummyOBJ.GetComponent<PlayMakerFSM>();
                    if (builderMain.hEffect2) {
                        builderMain.hEffect2.highlighted = false;
                    }
                //}
                return true;
            } else if (rayHit.CompareTag(Tags.Decoration)) {
                if (builderMain.oldHitOBJ && rayHit.gameObject != builderMain.oldHitOBJ && builderMain.hEffect) {
                    builderMain.hEffect.enabled = false;
                }
                builderMain.hEffect = rayHit.Find("Mesh").GetComponent<HighlightEffect>();
                builderMain.hEffect.enabled = true;
                builderMain.oldHitOBJ = rayHit.gameObject;
                //if (MainPlayer.GetButtonDown("Clone")) {
                    builderMain.currentMovedOBJ = rayHit.gameObject;
                    BuildableInfo component2 = builderMain.currentMovedOBJ.GetComponent<BuildableInfo>();
                    GameObject dummyPrefabOBJ = component2.dummyPrefabOBJ;
                    builderMain.dummyOBJ = Object.Instantiate(dummyPrefabOBJ, 
                        Vector3.zero, builderMain.currentMovedOBJ.transform.rotation);
                    builderMain.RetrieveBuilderInfo(component2);
                    builderMain.canPlace = false;
                    builderMain.pmakerFSM = builderMain.dummyOBJ.GetComponent<PlayMakerFSM>();
                    if (builderMain.hEffect) {
                        builderMain.hEffect.enabled = false;
                    }
                //}
                return true;
            }

            return false;
        }


        private record IndexSearchResult(bool Found, int TabIndex, int MenuIndex);

    }
}
