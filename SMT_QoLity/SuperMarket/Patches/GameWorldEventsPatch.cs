using Damntry.Utils.Events;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsUnity.ExtensionMethods;
using HarmonyLib;
using Mirror;
using StarterAssets;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment;
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Reflection;

namespace SuperQoLity.SuperMarket.Patches {

    //TODO 6 - Find a proper way to get when the game finishes loading, instead of this all-nighter borne curse.
    //		It has to be somewhere in the FSM.
    /// <summary>
    /// Detects:
    ///		- When the game finishes loading by checking when the black fade out transition ends.
    ///		- When the user quits to the main menu.
    ///		- A lot more stuff I dont feel like laying out.
    /// </summary>
    public class GameWorldEventsPatch : FullyAutoPatchedInstance {

        /* Highlight cases:

            Cases as a host with some product highlighted:
                Employee picks up the highlighted box.
                Remote player picks up the highlighted box
                Employee puts the highlighted box into storage.
                Remote player puts the highlighted box into storage.
                Employee drops highlighted box.
                Remote player drops highlighted box.
                Remote player assigns highlighted product to an empty product shelf.
                Remote player updates box from empty to an highlighted product.
                
            Cases as a client with some product highlighted:
                Employee picks up the highlighted box.
                Remote player picks up the highlighted box
                Employee puts the highlighted box into storage.
                Remote player puts the highlighted box into storage.
                Employee drops highlighted box.
                Remote player drops highlighted box.
                Remote player assigns highlighted product to an empty product shelf.
                Remote player updates box from empty to an highlighted product.
                    ** This last one does not work and never will. Not worth the work it needs to fix.
            
            */
        public override bool IsAutoPatchEnabled => true;

        //Allow at least whatever patch functionality did work.
        public override bool IsRollbackOnAutoPatchFail => false;

        public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Detection of world events patch failed. Disabled";


        public override void OnPatchFinishedVirtual(bool IsActive) {
            if (IsActive) {
                WorldState.OnWorldLoaded += () => { 
                    PauseDetection.WasGamePaused = false; 
                };
                
            }
        }


        private class PauseDetection {

            private static readonly Stopwatch swPause = Stopwatch.StartNew();

            public static bool WasGamePaused;


            [HarmonyPatch(typeof(GameData), nameof(GameData.Update))]
            [HarmonyPostfix]
            private static void GameCanvasUpdatePostFix(GameData __instance) {
                if (swPause.ElapsedMilliseconds > 20) {
                    DetectGamePause();
                    swPause.Restart();
                }
            }

            private static void DetectGamePause() {
                if (!WorldState.IsWorldLoaded) {
                    return;
                }

                bool isGamePaused = Time.timeScale == 0f;
                if (isGamePaused != WasGamePaused) {
                    EventMethods.TryTriggerEvents(WorldState.OnGamePauseChanged, isGamePaused, AuxUtils.IsMainMenuOpen());
                }
                WasGamePaused = isGamePaused;
            }
        }


            private class DetectGameLoadFinished {

			private enum DetectionState {
				Initial,
				Success,
				Failed
			}


			private static readonly Stopwatch sw = Stopwatch.StartNew();

            private static GameObject transitionBCKobj;

			private static DetectionState state = DetectionState.Initial;

            


            [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnStartServer))]
			[HarmonyPrefix]
			public static void OnStartHostPrefixPatch() {
				SetLoadingWorld();
			}

			[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnStartClient))]
			[HarmonyPrefix]
			public static void OnStartClientPrefixPatch() {
				//A host also triggers OnStartClient, and we only need to trigger once.
				if (WorldState.CurrentOnlineMode != GameOnlineMode.Host) {
					SetLoadingWorld();
				}
			}

			private static void SetLoadingWorld() {
				GameOnlineMode mode = NetworkServer.activeHost ? GameOnlineMode.Host : GameOnlineMode.Client;
				WorldState.CurrentOnlineMode = mode;
				WorldState.SetGameWorldState(GameWorldEvent.LoadingWorld);
			}

			[HarmonyPatch(typeof(GameCanvas), nameof(GameCanvas.Awake))]
			[HarmonyPostfix]
			private static void GameCanvasAwake(GameCanvas __instance) {
				WorldState.SetGameWorldState(GameWorldEvent.CanvasAwake);
			}


			[HarmonyPatch(typeof(GameCanvas), nameof(GameCanvas.Update))]
			[HarmonyPostfix]
			private static void GameCanvasUpdatePostFix(GameCanvas __instance) {
                if (sw.ElapsedMilliseconds < 100) { //Reduce check frequency for performance.
					return;
				}

				try {
					//Every time we exit to main menu, transitionBCKobj becomes null again. So at
					//	this point where GameCanvas is updating, we know we are loading the game.
					if (transitionBCKobj == null) {
						state = DetectionState.Initial;

						transitionBCKobj = GameObject.Find("MasterOBJ/MasterCanvas/TransitionBCK");
					}

					//The moment transitionBCKobj is not null, and then its activeSelf becomes false, is
					//	when the loading black fadeout has finished and the game has started fully.
					//	Seems like transitionBCKobj is controlled in some FSM with a timer that is not
					//	hooked (or badly hooked), to when the scene finishes loading. Maybe even by design.
					if (transitionBCKobj != null && !transitionBCKobj.activeSelf && state == DetectionState.Initial) {
						state = DetectionState.Success;

						WorldState.SetGameWorldState(GameWorldEvent.WorldLoaded);
					}
				} catch (Exception ex) {
					state = DetectionState.Failed;
					TimeLogger.Logger.LogExceptionWithMessage("Error while trying to detect game finished loading.", ex, LogCategories.Loading);
				} finally {
					GameWorldEventsPatch instance = Container<GameWorldEventsPatch>.Instance;
					if (state == DetectionState.Failed) {
						//Something changed and it wont work anymore without a mod update. Unpatch everything and forget it exists.
						TimeLogger.Logger.LogError(instance.ErrorMessageOnAutoPatchFail, LogCategories.Loading);
						instance.UnpatchInstance();

						sw.Stop();
					} else {
						sw.Restart();
					}
				}

			}
        }

		private class FirstPersonControllerStart {

			[HarmonyPatch(typeof(FirstPersonController), "Start")]
			[HarmonyPostfix]
			public static void OnFirstPersonControllerStart(FirstPersonController __instance) {
				WorldState.SetGameWorldState(GameWorldEvent.FPControllerStarted);
			}

		}

		private class DetectQuitToMainMenu {

			[HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnClientDisconnect))]
			[HarmonyPostfix]
			public static void OnClientDisconnectPatch(CustomNetworkManager __instance) {
				WorldState.CurrentOnlineMode = GameOnlineMode.None;
				WorldState.SetGameWorldState(GameWorldEvent.QuitOrMenu);
			}

		}

        private class BuildEvents {

            //TODO 2 - Transpile instead of this crap.
            private static Data_Container lastDataContainerActivated;

            /// <summary>Buildable loaded from save</summary>
            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.ActivateShelvesFromLoad))]
            [HarmonyPostfix]
            private static void NewBuildableLoadedHost(Data_Container __instance) {
                DataContainerType.TypeIndex containerTypeIndex = __instance.GetContainerType();

                if (containerTypeIndex == DataContainerType.TypeIndex.ProductShelf ||
                        containerTypeIndex == DataContainerType.TypeIndex.StorageShelf) {

                    EventMethods.TryTriggerEvents(WorldState.BuildingEvents.OnShelfBuiltOrLoaded,
                        __instance.transform, containerTypeIndex.ToParentContainerType());
                }    
            }

            /// <summary>A buildable was manually placed. Called only on the host.</summary>
            [HarmonyPatch(typeof(NetworkSpawner), nameof(NetworkSpawner.UserCode_CmdSpawn__Int32__Vector3__Vector3))]
            [HarmonyPostfix]
            private static void NewBuildableBuiltHost(NetworkSpawner __instance, int prefabID, Vector3 pos, Vector3 rot) {
                DataContainerType.TypeIndex containerTypeIndex = DataContainerType.GetContainerType(prefabID, out int parentIndex);
                if (containerTypeIndex == DataContainerType.TypeIndex.ProductShelf ||
                        containerTypeIndex == DataContainerType.TypeIndex.StorageShelf) {

                    Transform buildableParent = __instance.levelPropsOBJ.transform.GetChild(parentIndex);
                    GameObject lastStorageObject = buildableParent.GetChild(buildableParent.childCount - 1).gameObject;

                    EventMethods.TryTriggerEvents(WorldState.BuildingEvents.OnShelfBuiltOrLoaded, 
                        lastStorageObject.transform, containerTypeIndex.ToParentContainerType());
                }
            }


            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.DelayActivationShelves))]
            [HarmonyPostfix]
            private static void ProdShelfBuiltOrLoadedClientGetInstance(Data_Container __instance) {
                lastDataContainerActivated = __instance;
            }

            /// <summary>Storage shelf built or loaded. Called only on the client.</summary>
            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.DelayActivationShelves))]
            [HarmonyPriority(Priority.VeryLow)]
            [HarmonyPostfix]
            public static IEnumerator ProdShelfBuiltOrLoadedClient(IEnumerator result) {
                Data_Container __instance = lastDataContainerActivated;

                while (result.MoveNext()) {
                    yield return result.Current;
                }

                EventMethods.TryTriggerEvents(WorldState.BuildingEvents.OnShelfBuiltOrLoaded,
                    __instance.transform, ParentContainerType.ProductDisplay);
            }


            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.DelayActivationStorage))]
            [HarmonyPostfix]
            private static void StorageShelfBuiltOrLoadedClientGetInstance(Data_Container __instance) {
                lastDataContainerActivated = __instance;
            }

            /// <summary>Product shelf built or loaded. Called only on the client.</summary>
            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.DelayActivationStorage))]
            [HarmonyPriority(Priority.VeryLow)]
            [HarmonyPostfix]
            public static IEnumerator StorageShelfBuiltOrLoadedClient(IEnumerator result) {
                Data_Container __instance = lastDataContainerActivated;

                while (result.MoveNext()) {
                    yield return result.Current;
                }

                EventMethods.TryTriggerEvents(WorldState.BuildingEvents.OnShelfBuiltOrLoaded,
                    __instance.transform, ParentContainerType.Storage);
            }

        }

        private class ContainerEvents {

            /// <summary>Storage shelf built, loaded, or box added/removed</summary>
            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.BoxSpawner))]
            [HarmonyPostfix]
            private static void BoxSpawnerPatch(Data_Container __instance) {
                EventMethods.TryTriggerEvents(WorldState.BuildingEvents.OnStorageLoadedBuiltOrUpdated, __instance);
            }

            /// <summary>Product shelf built, loaded, or product quantity changed</summary>
            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.ItemSpawner))]
            [HarmonyPostfix]
            private static void ItemSpawnerPatch(Data_Container __instance) {
                EventMethods.TryTriggerEvents(WorldState.BuildingEvents.OnProductShelfLoadedBuiltOrUpdated, __instance);
            }

			/// <summary>Ground box spawned. Doesnt include empty ones.</summary>
            [HarmonyPatch(typeof(ManagerBlackboard), nameof(ManagerBlackboard.UserCode_RpcParentBoxOnClient__GameObject))]
            [HarmonyPostfix]
            private static void BoxSpawned(ManagerBlackboard __instance, GameObject boxOBJ) {
                EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxSpawned, boxOBJ.transform);
            }

            [HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.UpdateBoxContents))]
            [HarmonyPostfix]
            private static void BoxPickUpOrUpdatedLocalPlayerPatch(PlayerNetwork __instance, int productIndex) {
                //This only triggers on the local player, but its the most complete since it also
                //  triggers when the box is updated (when it goes empty and then filled with a different productId).
                //  I cant see an easy way to detect this specific case, so we ll use this event for
                //  anything host related, and OnBoxEquipped for more specific cases.
                EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxEquippedOrUpdatedLocalPlayer, 
                    __instance.instantiatedOBJ.transform, productIndex);
            }

            private static Transform droppedInstantiatedObject;
            [HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.OnChangeEquipment))]
            [HarmonyPrefix]
            private static void OnChangeEquipmentPrefix(PlayerNetwork __instance) {
                //Save the previously equipped object before the var gets reassigned to the new equipped one.
                //  If no object was dropped, then droppedInstantiatedObject will ne null.
                droppedInstantiatedObject = __instance.instantiatedOBJ.NullableObject()?.transform;
            }

            [HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.OnChangeEquipment))]
            [HarmonyPostfix]
            private static void OnChangeEquipmentPostfix(PlayerNetwork __instance, int oldEquippedItem, int newEquippedItem) {
                if (oldEquippedItem == 1) {
                    CharacterSourceType charSource = ToCharacterSource(__instance.isLocalPlayer);
                    EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxDroppedByPlayer,
                        droppedInstantiatedObject, charSource);
                } else if (newEquippedItem == 1 && !__instance.isLocalPlayer && 
                        WorldState.CurrentOnlineMode == GameOnlineMode.Client) {
                    //Box picked up by the remote host player. This method could actually work for all kinds of players but:
                    //  - For the local player, BoxPickUpOrUpdatePatch is a more complete method.
                    //  - For remote client players, extraParameter1 is not updated and instead we need to read from 
                    //      PlayerSyncCharacter.syncedProductID when synced, which happens later than this call.
                    int productId = __instance.GetComponent<PlayerSyncCharacter>().syncedProductID;
                    EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxEquippedRemotePlayerOrEmployee, __instance.transform,
                        __instance.instantiatedOBJ.transform, productId, CharacterSourceType.RemotePlayer);
                }

                EventMethods.TryTriggerEvents(WorldState.PlayerEvents.OnChangeEquipment, __instance, 
                    droppedInstantiatedObject, __instance.instantiatedOBJ.NullableObject()?.transform, 
                    oldEquippedItem, newEquippedItem, __instance.isLocalPlayer);
            }

            [HarmonyPatch(typeof(PlayerSyncCharacter), nameof(PlayerSyncCharacter.DeserializeSyncVars))]
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> CallOnChangeSyncedProductIDTranspiler(
                    IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                ///Old C#
                ///     if ((num & 4L) != 0L) {
                ///         GeneratedSyncVarDeserialize(ref syncedProductID, null, reader.ReadInt());
                ///     }
                ///
                ///New C#w
                ///     if ((num & 4L) != 0L) {
                ///         GeneratedSyncVarDeserialize(ref syncedProductID, OnChangeSyncedProductID(this), reader.ReadInt());
                ///     }

                CodeMatcher codeMatcher = new(instructions);

                FieldInfo fInfo = AccessTools.Field(typeof(PlayerSyncCharacter), nameof(PlayerSyncCharacter.syncedProductID));
                if (fInfo == null) {
                    TimeLogger.Logger.LogError($"The field {nameof(PlayerSyncCharacter)}." +
                        $"{nameof(PlayerSyncCharacter.syncedProductID)} could not be found. " +
                        $"Highlighting of remote player held boxes wont work.", LogCategories.Highlight);
                    return codeMatcher.InstructionEnumeration();
                }

                //Find last match of syncedProductID being used.
                codeMatcher.End();
                codeMatcher.MatchEndBackwards(
                    new CodeMatch(c => CodeInstructionExtensions.LoadsField(c, fInfo, byAddress: true)),
                    new CodeMatch(c => c.opcode == OpCodes.Ldnull)
                );

                if (codeMatcher.IsInvalid) {
                    TimeLogger.Logger.LogError($"The IL line loading the field {nameof(PlayerSyncCharacter)}." +
                        $"{nameof(PlayerSyncCharacter.syncedProductID)} could not be found. " +
                        $"Highlighting of remote player held boxes wont work.", LogCategories.Highlight);
                    return codeMatcher.InstructionEnumeration();
                }

                MethodInfo onChange = AccessTools.Method(typeof(ContainerEvents), nameof(OnChangeSyncedProductID));
                var actionCtor = AccessTools.Constructor(
                    typeof(Action<PlayerSyncCharacter>), 
                    [typeof(object), typeof(IntPtr)]
                );

                codeMatcher.RemoveInstruction() //Remove Ldnull
                    .Insert(
                        //Put the Action var into the stack
                        new CodeInstruction(OpCodes.Ldarg_0),   //Pass current instance as parameter
                        new CodeInstruction(OpCodes.Ldftn, onChange),
                        new CodeInstruction(OpCodes.Newobj, actionCtor)
                    );

                return codeMatcher.InstructionEnumeration();
            }

            private static void OnChangeSyncedProductID(PlayerSyncCharacter playerSync) {
                if (WorldState.CurrentOnlineMode != GameOnlineMode.Host) {
                    //Wont work for clients, since PlayerNetwork hasnt synced yet.
                    return;
                }

                PlayerNetwork pNetwork = playerSync.GetComponent<PlayerNetwork>();
                
                if (pNetwork && pNetwork.equippedItem == (int)ToolIndexes.Box) {
                    EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxEquippedRemotePlayerOrEmployee, playerSync.transform,
                        pNetwork.instantiatedOBJ.transform, playerSync.syncedProductID, CharacterSourceType.RemotePlayer);
                }
            }

            [HarmonyPatch(typeof(NPC_Info), nameof(NPC_Info.UserCode_RpcEquipNPCItem__Int32__Int32))]
            [HarmonyPostfix]
            private static void UserCode_RpcEquipNPCItem__Int32__Int32(NPC_Info __instance, int equippedIndex, int productID) {
                if (equippedIndex != 1) {
                    return;
                }

                EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxEquippedRemotePlayerOrEmployee, __instance.transform,
                    __instance.instantiatedOBJ.transform, productID, CharacterSourceType.Employee);
            }


            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.UserCode_RpcUpdateArrayValuesStorage__Int32__Int32__Int32))]
            [HarmonyPostfix]
            private static void UserCode_RpcUpdateArrayValuesStorage__Int32__Int32__Int32(Data_Container __instance, int index, int PID, int PNUMBER) {
                if (PNUMBER < 0) {
                    return;
                }

                EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnBoxIntoStorage, __instance);
            }

            [HarmonyPatch(typeof(Data_Container), nameof(Data_Container.UserCode_RpcUpdateObjectOnClients__Int32__Int32__Int32__Int32))]
            [HarmonyPostfix]
            private static void UserCode_RpcUpdateObjectOnClients__Int32__Int32__Int32__Int32(Data_Container __instance, int index, int PID, int PNUMBER, int oldPID) {
                if (oldPID < 0 && PID >= 0) {
                    EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnProdShelfAssigned, __instance);
                } else if (oldPID >= 0 && PID < 0) {
                    EventMethods.TryTriggerEvents(WorldState.ContainerEvents.OnProdShelfUnassigned, __instance);
                }
            }

        }

        public static CharacterSourceType ToCharacterSource(bool isLocalPlayer) =>
                isLocalPlayer ? CharacterSourceType.LocalPlayer : CharacterSourceType.RemotePlayer;

        public class NPC_Events {

            [HarmonyPatch(typeof(NPC_Manager), nameof(NPC_Manager.SpawnEmployeeByIndex))]
            [HarmonyPriority(Priority.VeryLow)] //So this patch runs after other postfixes from mods that might modify their values.
            [HarmonyPostfix]
            private static void SpawnEmployeeByIndexPostfix(NPC_Manager __instance, int index) {
                NPC_Info npcInfo = __instance.employeesArray[index].GetComponent<NPC_Info>();
                EventMethods.TryTriggerEvents(WorldState.NPC_Events.OnEmployeeSpawned, npcInfo, index);
            }

        }

    }
}
