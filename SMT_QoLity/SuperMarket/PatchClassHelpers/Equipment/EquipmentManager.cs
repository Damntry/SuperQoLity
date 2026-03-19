using Damntry.Utils.Logging;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel.Model;
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Equipment {

    public enum ToolIndexes {
        [Description("Nothing")]
        Nothing = 0,
        [Description("Box")]
        Box = 1,
        [Description("Pricing Scanner")]
        PricingScanner = 2,
        [Description("Broom")]
        Broom = 3,
        [Obsolete($"Replaced with {nameof(PaintablesTablet)}")]
        [Description("Decoration Tablet")]
        DecoTablet = 4,
        [Description("Paintables Tablet")]
        /// <summary>DLC Only</summary>
        PaintablesTablet = 5,
        [Description("Order Device")]
        OrderingDevice = 6,
        [Description("SledgeHammer")]
        SledgeHammer = 7,
        [Description("Ladder")]
        Ladder = 8,
        [Description("Blue Tray")]
        BlueTray = 9,
        [Description("Sales Device")]
        SalesDevice = 10,
        [Description("Cardboard Bale")]
        CardboardBale = 11,
        [Description("Order Box")]
        OrderBox = 12,
        [Description("Net Catcher")]
        /// <summary>From past events</summary>
        NetCatcher = 13,
        [Description("Toolbox")]
        Toolbox = 14,
        [Description("Fire Extinguisher")]
        Extinguisher = 15,
    }

    public static class EquipmentManager {


        /// <summary>
        /// Finds if the tool with the toolIndex passed by parameter currently exists in the store to be taken.
        /// If it is the case, returns true with the Action to remove it as an out parameter. Otherwise returns false.
        /// </summary>
        /// <param name="toolIndex">Index of the tool.</param>
        /// <param name="toolRequestMethod">
        /// The function to take the found tool. It does not equip it though, just removes the targeted tool.
        /// If it returns false, there was an error in the request method and we shouldnt proceed further.
        /// </param>
        public static bool ExistsUnusedToolOfIndex(PlayerNetwork pNetwork, 
                int toolIndex, out Func<bool> toolRequestMethod) {

            toolRequestMethod = null;

            ToolWheelDefinition toolData = ToolWheelDefinitions.FromIndex(toolIndex);

            if (!FirstPersonController.Instance.GetComponent<PlayerPermissions>().RequestGP()) {
                //Player does not have general permission.
                TimeLogger.Logger.SendMessageNotification(LogTier.Message,
                    $"You do not have the basic multiplayer permission to equip any tools", skipQueue: true);
                return false;
            } else if (!HostPermissions.HasPermission(toolData.RequiredPermission)){
                TimeLogger.Logger.SendMessageNotification(LogTier.Message,
                    $"You need '{toolData.RequiredPermission}' permission to equip this tool", skipQueue: true);
                return false;
            }

            if (FindToolInWorld(toolData, out GameObject toolObj)) {    //Search first from the tools lying around
                toolRequestMethod = () => RequestToolFromWorld(pNetwork, toolObj, toolData);
                return true;
            } else if (FindToolInOrganizer(toolData, out toolRequestMethod)) { //Search in tool organizers
                return true;
            }

            TimeLogger.Logger.SendMessageNotification(LogTier.Message,
                $"No free {toolData.DisplayName} found in map or organizer", skipQueue: true);

            return false;
        }

        private static bool RequestToolFromWorld(PlayerNetwork pNetwork, 
                GameObject toolObj, ToolWheelDefinition toolData) {

            if (toolData.Index == ToolIndexes.Extinguisher) {
                AchievementsManager.Instance.grabbedAnExtinguisher = true;            
            } else if (toolData.Index == ToolIndexes.BlueTray) {
                if (!toolObj.TryGetComponent(out TrayData trayData)) {
                    TimeLogger.Logger.SendMessageNotification(LogTier.Error, "Error reading tray contents", true);
                    return false;
                }
                pNetwork.trayData = trayData.itemsData;
            }

            //CmdDestroyBox works for anything, not just boxes.
            SMTInstances.NetworkSpawner().CmdDestroyBox(toolObj);

            return true;
        }

        public static bool FindToolInWorld(ToolWheelDefinition toolData, out GameObject foundObj) {
            bool foundTool = false;
            foundObj = null;

            string toolGameobjectName = toolData.ToolGameObjects.UsablePropName;
            if (toolGameobjectName == null) {
                return false;
            }

            if (PropsCache.UsablePropsObjReference) {
                foreach (Transform freePropT in PropsCache.UsablePropsObjReference.transform) {
                    if (freePropT && freePropT.gameObject.activeSelf && freePropT.name.Contains(toolGameobjectName)) {
                        foundTool = true;
                        foundObj = freePropT.gameObject;
                        break;
                    }
                }
            }
            
            if (!foundTool) {
                foreach (GameObject rootObj in SceneManager.GetActiveScene().GetRootGameObjects()) {
                    if (rootObj && rootObj.activeSelf && rootObj.name.Contains(toolGameobjectName)) {
                        foundTool = true;
                        foundObj = rootObj.gameObject;
                        break;
                    }
                }
            }
            
            return foundTool;
        }

        public static bool FindToolInOrganizer(ToolWheelDefinition toolData, out Func<bool> toolRequestMethod) {
            bool foundTool = false;
            toolRequestMethod = null;

            string orgObjName = toolData.ToolGameObjects.OrganizerName;
            if (orgObjName == null) {
                //No organizer associated to this tool
                return false;
            }

            if (PropsCache.DecoPropsObjCache) {
                foreach (Transform orgPropsContainer in PropsCache.DecoPropsObjCache.transform) {
                    if (IsRequestedToolMethod(orgPropsContainer.gameObject, orgObjName, toolData, out toolRequestMethod)) {
                        foundTool = true;
                        break;
                    }
                }
            }

            if (!foundTool) {
                foreach (GameObject rootObj in SceneManager.GetActiveScene().GetRootGameObjects()) {
                    if (IsRequestedToolMethod(rootObj, orgObjName, toolData, out toolRequestMethod)) {
                        foundTool = true;
                        break;
                    }
                }
            }

            return foundTool;
        }

        private static bool IsRequestedToolMethod(GameObject orgPropsContainer, string orgObjName,
                ToolWheelDefinition toolData, out Func<bool> toolRequestMethod) {

            toolRequestMethod = null;

            if (!orgPropsContainer || !orgPropsContainer.activeSelf || 
                    !orgPropsContainer.name.Contains(orgObjName)) {
                return false;
            }

            if (toolData.Index == ToolIndexes.Extinguisher) {
                if (orgPropsContainer.TryGetComponent(out FireExtinguisherTake fireExt) &&
                        !fireExt.extinguisherTaken) {

                    toolRequestMethod = () => {
                        fireExt.CmdRequestItem(takingItem: true);
                        AchievementsManager.Instance.grabbedAnExtinguisher = true;
                        return true;
                    };

                    return true;
                }
            } else if (orgPropsContainer.TryGetComponent(out ToolsOrganizer toolOrg) && toolOrg.itemsInOrganizer > 0) {

                toolRequestMethod = () => {
                    toolOrg.CmdRequestItem(takingItem: true);
                    return true;
                };

                return true;
            }

            return false;
        }

        public static bool CanDropItemInFront(PlayerNetwork pNetwork, out Vector3 dropPosition, out ToolWheelDefinition equippedTool) {
            dropPosition = Vector3.zero;
            equippedTool = null;

            if (pNetwork && pNetwork.equippedItem > 0) {
                if (!Enum.IsDefined(typeof(ToolIndexes), pNetwork.equippedItem)) {
                    TimeLogger.Logger.LogFatal($"Player has the item '{pNetwork.equippedItem}' " +
                        $"equipped, but it has not beed added yet to the definitions.", LogCategories.Vanilla);
                    return false;
                }

                equippedTool = ToolWheelDefinitions.FromIndex(pNetwork.equippedItem);

                if (equippedTool.CmdSpawnMethod == null) {
                    //Has no drop method associated, we can skip the rest.
                    return false;
                }

                Transform cameraTransform = Camera.main.transform;
                Vector3 cameraPos = cameraTransform.position;

                if (Physics.Raycast(cameraPos, cameraTransform.forward, out RaycastHit rayHit, 4f, pNetwork.lMask)) {

                    dropPosition = rayHit.point + rayHit.normal.normalized * 0.5f;
                    if (rayHit.transform.gameObject.tag == Tags.Interactable && !rayHit.transform.GetComponent<BoxData>() ||
                            Physics.Raycast(cameraPos, cameraTransform.forward,
                                out RaycastHit val3, 4f, pNetwork.interactableMask) &&
                                val3.transform.gameObject.tag == Tags.Interactable) {

                        TimeLogger.Logger.SendMessageNotification(LogTier.Message, 
                            "Item cant be dropped here", skipQueue: true);
                        return false;
                    }
                } else {
                    dropPosition = cameraPos + cameraTransform.forward * 3.5f;
                }

                return true;
            }

            return false;
        }

        public static bool DropCurrentEquippedItem(PlayerNetwork pNetwork, 
                Vector3 dropPosition, ToolWheelDefinition equippedTool) {  
            if (pNetwork.equippedItem > 0) {

                //Spawn dropped item
                equippedTool.CmdSpawnMethod(SMTInstances.NetworkSpawner(), pNetwork, dropPosition);

                //Remove item from hand
                pNetwork.CmdChangeEquippedItem(0);

                return true;
            }

            return false;
        }

        private class PropsCache {

            private static GameObject usablePropsObjCache;
            private static GameObject decoPropsObjCache;

            private static readonly string usablePropsPath = "Level_SupermarketProps/UsableProps";
            private static readonly string decorationPropsPath = "Level_SupermarketProps/DecorationProps";


            public static GameObject UsablePropsObjReference => GeGameObjectReference(ref usablePropsObjCache, usablePropsPath);

            public static GameObject DecoPropsObjCache => GeGameObjectReference(ref decoPropsObjCache, decorationPropsPath);


            private static GameObject GeGameObjectReference(ref GameObject propsObjCache, string propsPath) {
                if (!propsObjCache) {
                    propsObjCache = GameObject.Find(propsPath);

                    if (!propsObjCache) {
                        TimeLogger.Logger.LogError($"A GameObject couldnt be found with path {propsPath}",
                            LogCategories.Vanilla);
                        return null;
                    }
                }
                return propsObjCache;
            }
        }

    }
}
