using Damntry.Utils.Reflection;
using Damntry.Utils.Tasks;
using Damntry.Utils.Tasks.AsyncDelay;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsUnity.Components.InputManagement;
using HarmonyLib;
using HighlightPlus;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.ModUtils.ExternalMods;
using SuperQoLity.SuperMarket.PatchClassHelpers.ContainerEntities;
using SuperQoLity.SuperMarket.PatchClassHelpers.Highlighting;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.Highlighting {


    /// <summary>
    /// Adds highlighting of shelves and storage slots.
    /// </summary>
    public class HighlightStorageSlotsPatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => ModConfig.Instance.EnablePatchHighlight.Value;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - Highlight patching failed. Disabled";


		private static DelayedSingleTask<AsyncDelay> delayChangeColorTask;

        //Cursed static. I should change it sometime.
        private static ContainerTypeFlags lastColorSettingsChanged;

        public override void OnPatchFinishedVirtual(bool IsActive) {
			if (IsActive) {

				WorldState.BuildingEvents.OnShelfBuiltOrLoaded += HighlightInitialization.OnShelfBuiltOrLoaded;
                WorldState.ContainerEvents.OnBoxSpawned += HighlightInitialization.BoxSpawned;
                WorldState.ContainerEvents.OnBoxEquippedOrUpdatedLocalPlayer += BoxEventsHighlighting.BoxEquippedOrUpdatedLocalPlayer;
                WorldState.ContainerEvents.OnBoxDroppedByPlayer += BoxEventsHighlighting.BoxDroppedByPlayer;
                WorldState.ContainerEvents.OnBoxEquippedRemotePlayerOrEmployee += BoxEventsHighlighting.BoxEquippedRemotePlayerOrEmployee;
                WorldState.ContainerEvents.OnBoxIntoStorage += BoxEventsHighlighting.BoxIntoStorage;
                WorldState.ContainerEvents.OnProdShelfAssigned += BoxEventsHighlighting.ProdShelfAssigned;
                WorldState.ContainerEvents.OnProdShelfUnassigned += BoxEventsHighlighting.ProdShelfUnassigned;
                
                ContainerHighlightManager.InitHighlightManager();
           

                ModConfig config = ModConfig.Instance;
				//Setting to change highlight visual mode
				config.HighlightVisualMode.SettingChanged += (_, _) => ContainerHighlightManager.UpdateHighlightMode();

                //Settings to change highlight colors
                config.ShelfHighlightColorRGBA.SettingChanged += 
					(_, _) => StartColorChangeDelay(ContainerTypeFlags.ProdShelf);
                config.ShelfLabelHighlightColorRGBA.SettingChanged +=
                    (_, _) => StartColorChangeDelay(ContainerTypeFlags.ProdShelfSlot);
                config.StorageHighlightColorRGBA.SettingChanged +=
                    (_, _) => StartColorChangeDelay(ContainerTypeFlags.Storage);
				//Storage slot and groud boxes share the same color setting
				config.StorageSlotHighlightColorRGBA.SettingChanged +=
					(_, _) => StartColorChangeDelay(ContainerTypeFlags.StorageSlot | ContainerTypeFlags.GroundBox);

                delayChangeColorTask = new(UpdateHighlightColors);


				InputManagerSMT.Instance.AddHotkeyFromConfig(config.HotkeyToggleAimedHighlight, InputState.KeyDown, 
                    HotkeyActiveContext.WorldLoaded, 100, ContainerHighlightManager.ToggleCrosshairInProductHighlight);

                InputManagerSMT.Instance.AddHotkeyFromConfig(config.HotkeyCycleHighlightMode, InputState.KeyDown, 
                    HotkeyActiveContext.WorldLoaded, 100, ContainerHighlightManager.ChangeToNextHighlightMode);

                //Fixes the z-fighting flickers with storage slots, and should be faster.
                HighlightEffect.customSorting = true;
            }
        }
        

        private static void StartColorChangeDelay(ContainerTypeFlags colorSettingsChanged) {
			if (WorldState.IsWorldLoaded) {
				if (lastColorSettingsChanged == ContainerTypeFlags.None) {
                    lastColorSettingsChanged = colorSettingsChanged;
                } else {
					//Add onto it, so a new delay from a different setting wont cancel a previous one that was still waiting.
                    lastColorSettingsChanged |= colorSettingsChanged;
                }
					
				//Method is fairly fast, so we can afford a short delay
				delayChangeColorTask?.Start(150);
			}
        }

		private static void UpdateHighlightColors() {
            ContainerHighlightManager.UpdateHighlightColorsFromSettings(lastColorSettingsChanged);
            lastColorSettingsChanged = ContainerTypeFlags.None;
        }

        /// <summary>
        /// Handles all pickup/drop box events to update highlighting.
        /// </summary>
        private class BoxEventsHighlighting {

			public static void BoxDroppedByPlayer(Transform t, CharacterSourceType charSource) {
				if (charSource == CharacterSourceType.LocalPlayer) { 
					ContainerHighlightManager.ClearHighlightedContainers(clearProductIdFlag: true);
				}
			}
            
			public static void BoxEquippedOrUpdatedLocalPlayer(Transform transform, int productIndex) {
                //TODO 2 - As a client, when I pick up a box from storage, the storage slot from where
                //  I picked it up doesnt highlight at all, and when I put it back, the storage highlights, and
                //  then immediately fades out.
                //The problem is that in the host, the storage is updated after this method is called, while in
                //  the client its the opposite. This goes combined with the events of dropping a box/placing it
                //  into storage call order also being reversed for host/client, which causes the strange fade out.
                //There isnt much I can do about this without heavy messy patching for a relatively niche case, so
                //  Im going to leave this as a casualty of war for now.
                ContainerHighlightManager.HighlightContainersByProduct(productIndex);
			}

            public static void BoxEquippedRemotePlayerOrEmployee(Transform charTransform, Transform boxTransform, int productIdBox, CharacterSourceType charSource) {
                ContainerHighlightManager.UpdateHeldBoxHighlighting(charTransform, boxTransform, productIdBox, charSource);
            }

            public static void BoxIntoStorage(Data_Container dataContainer) {
                ContainerHighlightManager.UpdateContainerHighlighting(dataContainer.transform, ParentContainerType.Storage);
            }

            public static void ProdShelfAssigned(Data_Container dataContainer) {
                ContainerHighlightManager.UpdateContainerHighlighting(dataContainer.transform, ParentContainerType.ProductDisplay);
            }

            public static void ProdShelfUnassigned(Data_Container dataContainer) {
                ContainerHighlightManager.UpdateContainerHighlighting(dataContainer.transform, ParentContainerType.ProductDisplay);
            }

        }

		private class HighlightInitialization {

			public static void OnShelfBuiltOrLoaded(Transform shelfTransform, ParentContainerType parentContainerType) {
                if (parentContainerType == ParentContainerType.Storage) {
                    ContainerHighlightManager.AddHighlightMarkersToStorage(shelfTransform);
                }
			}

            //Empty boxes spawn in a different vanilla method, but are not added to the
            //	boxesObj parent and we dont want to highlight them, since they ll get their 
            //	highlight when placed on storage.
            
            public static void BoxSpawned(Transform newBox) {
                ContainerHighlightManager.UpdateBoxHighlight(newBox);
            }

        }


        /// <summary>
        /// If BetterSMT is loaded, patch his methods so we use our highlighting instead.
        /// </summary>
        private class BetterSMTRemoveHighlighting {

			[HarmonyPrepare]
            public static bool HarmonyPrepare() => 
					BetterSMT_Helper.Instance.IsModLoadedAndEnabled && 
						BetterSMT_Helper.Instance.IsVersionWithHighlighting();

			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "ChangeEquipmentPatch")]
			[HarmonyBefore(ModInfoBetterSMT.HarmonyId)]
			[HarmonyPrefix]
            //Yo dawg, I heard you like patches, so I patched the patch so it doesnt patch.
            public static bool ChangeEquipmentBetterSMTPatch(PlayerNetwork __instance, int newEquippedItem) {
				//In reality, both patches still exist. What happens is that my prefix patch replaces his
				//	patch code, and when the original ChangeEquipment method is called, his patch
				//	code is invoked, which is just my code now.
				return false;
			}

            /// <summary>
            /// For post-Viviko BetterSMT versions (> 1.6.2), patches the BetterSMT method 
            /// UpdateBoxContentsPatch itself, so it uses my updated highlight method instead.
            /// </summary>

            [HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "UpdateBoxContentsPatch")]
			[HarmonyBefore(ModInfoBetterSMT.HarmonyId, ModInfoBetterSMT.HarmonyId)]
			[HarmonyPrefix]
            public static bool UpdateBoxContentsPatch(PlayerNetwork __instance, int productIndex) {
				//Overwrite BetterSMT patch so the game only uses my code instead.
				return false;
			}

		}

		private class BetterSMTRemoveMarkerHighlighting {

			[HarmonyPrepare]
			private static bool HarmonyPrepare() => 
				BetterSMT_Helper.Instance.IsModLoadedAndEnabled &&
                    BetterSMT_Helper.Instance.IsVersionWithHighlighting() && IsBetterSMTVersionWithMarkers();

			private static bool IsBetterSMTVersionWithMarkers() =>
				AssemblyUtils.GetMethodFromLoadedAssembly(
					$"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "BoxSpawnerPatch", true
				) != null;


			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "BoxSpawnerPatch")]
			[HarmonyPrefix]
			private static bool BoxSpawnerPatch(Data_Container __instance) {
				return false;
			}

			[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "NewBuildableConstructed")]
			[HarmonyPrefix]
			private static bool NewBuildableConstructed(NetworkSpawner __instance, int prefabID) {
				return false;
			}

		}


        /* This is to see how HighlightEffect handles rendering logic internally
         
        [HarmonyPatch(typeof(HighlightEffect), nameof(HighlightEffect.SetupMaterial), [typeof(Renderer[])])]
        [HarmonyPrefix]
        public static bool SetupMaterial(HighlightEffect __instance, Renderer[] rr) {
            if (rr == null) {
                rr = new Renderer[0];
            }

            if (__instance.rms == null || __instance.rms.Length < rr.Length) {
                __instance.rms = new HighlightEffect.ModelMaterials[rr.Length];
            }

            __instance.InitCommandBuffer();
            __instance.spriteMode = false;
            __instance.rmsCount = 0;
            for (int i = 0; i < rr.Length; i++) {
                __instance.rms[__instance.rmsCount].Init();
                Renderer renderer = rr[i];
                if (renderer == null) {
                    continue;
                }

                if (__instance.effectGroup != TargetOptions.OnlyThisObject && !string.IsNullOrEmpty(__instance.effectNameFilter)) {
                    if (__instance.effectNameUseRegEx) {
                        try {
                            __instance.lastRegExError = "";
                            if (!Regex.IsMatch(renderer.name, __instance.effectNameFilter)) {
                                continue;
                            }
                        } catch (Exception ex) {
                            __instance.lastRegExError = ex.Message;
                            continue;
                        }
                    } else if (!renderer.name.Contains(__instance.effectNameFilter)) {
                        continue;
                    }
                }

                __instance.rms[__instance.rmsCount].renderer = renderer;
                __instance.rms[__instance.rmsCount].renderWasVisibleDuringSetup = renderer.isVisible;
                __instance.sortingOffset = (float)renderer.gameObject.GetInstanceID() % 0.0001f;
                if (renderer.transform != __instance.target) {
                    HighlightEffect component = renderer.GetComponent<HighlightEffect>();
                    if (component != null && component.enabled && component.ignore) {
                        continue;
                    }
                }
				
                //if (__instance.OnRendererHighlightStart != null && !__instance.OnRendererHighlightStart(renderer)) {
                //    __instance.rmsCount++;
                //    continue;
                //}
				
                __instance.rms[__instance.rmsCount].isCombined = false;
                bool flag = renderer is SkinnedMeshRenderer;
                __instance.rms[__instance.rmsCount].isSkinnedMesh = flag;
                bool num = renderer is SpriteRenderer;
                __instance.rms[__instance.rmsCount].normalsOption = (flag ? NormalsOption.PreserveOriginal : __instance.normalsOption);
                if (num) {
                    __instance.rms[__instance.rmsCount].mesh = HighlightEffect.quadMesh;
                    __instance.spriteMode = true;
                } else if (flag) {
                    __instance.rms[__instance.rmsCount].mesh = ((SkinnedMeshRenderer)renderer).sharedMesh;
                } else if (Application.isPlaying && renderer.isPartOfStaticBatch) {
                    MeshCollider component2 = renderer.GetComponent<MeshCollider>();
                    if (component2 != null) {
                        __instance.rms[__instance.rmsCount].mesh = component2.sharedMesh;
                    }
                } else {
                    MeshFilter component3 = renderer.GetComponent<MeshFilter>();
                    if (component3 != null) {
                        __instance.rms[__instance.rmsCount].mesh = component3.sharedMesh;
                    }
                }

                if (__instance.rms[__instance.rmsCount].mesh == null) {
                    continue;
                }

                __instance.rms[__instance.rmsCount].transform = renderer.transform;
                __instance.Fork(HighlightEffect.fxMatMask, ref __instance.rms[__instance.rmsCount].fxMatMask, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(__instance.fxMatOutlineTemplate, ref __instance.rms[__instance.rmsCount].fxMatOutline, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(__instance.fxMatGlowTemplate, ref __instance.rms[__instance.rmsCount].fxMatGlow, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(HighlightEffect.fxMatSeeThrough, ref __instance.rms[__instance.rmsCount].fxMatSeeThroughInner, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(HighlightEffect.fxMatSeeThroughBorder, ref __instance.rms[__instance.rmsCount].fxMatSeeThroughBorder, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(HighlightEffect.fxMatOverlay, ref __instance.rms[__instance.rmsCount].fxMatOverlay, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(__instance.fxMatInnerGlow, ref __instance.rms[__instance.rmsCount].fxMatInnerGlow, __instance.rms[__instance.rmsCount].mesh);
                __instance.Fork(HighlightEffect.fxMatSolidColor, ref __instance.rms[__instance.rmsCount].fxMatSolidColor, __instance.rms[__instance.rmsCount].mesh);
                __instance.rms[__instance.rmsCount].originalMesh = __instance.rms[__instance.rmsCount].mesh;
                if (!__instance.rms[__instance.rmsCount].preserveOriginalMesh && (__instance.innerGlow > 0f || (__instance.glow > 0f && __instance.glowQuality != HighlightPlus.QualityLevel.Highest) || (__instance.outline > 0f && __instance.outlineQuality != HighlightPlus.QualityLevel.Highest))) {
                    if (__instance.normalsOption == NormalsOption.Reorient) {
                        __instance.ReorientNormals(__instance.rmsCount);
                    } else {
                        __instance.AverageNormals(__instance.rmsCount);
                    }
                }

                __instance.rmsCount++;
            }

            if (__instance.spriteMode) {
                __instance.outlineIndependent = false;
                __instance.outlineQuality = HighlightPlus.QualityLevel.Highest;
                __instance.glowQuality = HighlightPlus.QualityLevel.Highest;
                __instance.innerGlow = 0f;
                __instance.cullBackFaces = false;
                __instance.seeThrough = SeeThroughMode.Never;
                if (__instance.alphaCutOff <= 0f) {
                    __instance.alphaCutOff = 0.5f;
                }
            } else if (__instance.combineMeshes) {
                __instance.CombineMeshes();
            }

            __instance.UpdateMaterialProperties();

			return false;
        }
		*/

        /* Not needed since integrating highlighting from BetterSMT
		
		[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "HighlightContainersByProduct")]
		[HarmonyBefore(ModInfoBetterSMT.HarmonyId)]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> ModifyShelfColorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			//Changes color passed by parameter so it uses my config settings instead of a fixed color.

			///Old C#:
			///		PlayerNetworkPatch.HighlightContainerTypeByProduct(productID, 
			///			Color.yellow, 
			///			PlayerNetworkPatch.ParentContainerType.ProductDisplay);
			///		PlayerNetworkPatch.HighlightContainerTypeByProduct(productID, 
			///			Color.red, 
			///			PlayerNetworkPatch.ParentContainerType.Storage);
			///New C#:
			///		PlayerNetworkPatch.HighlightContainerTypeByProduct(productID, 
			///			ModConfig.Instance.BetterSMT_ShelfHighlightColor.Value, 
			///			PlayerNetworkPatch.ParentContainerType.ProductDisplay);
			///		PlayerNetworkPatch.HighlightContainerTypeByProduct(productID, 
			///			ModConfig.Instance.BetterSMT_StorageHighlightColor.Value, 
			///			PlayerNetworkPatch.ParentContainerType.Storage);
			///		}

			//First color
			codeMatcher.MatchForward(true,
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(inst => inst.opcode == OpCodes.Call));

			if (!codeMatcher.IsValid) {
				throw new TranspilerDefaultMsgException($"IL line to get the first color could not be found.");
			}

			codeMatcher.SetInstruction(Transpilers.EmitDelegate(() => 
				ModConfig.Instance.BetterSMT_ShelfHighlightColor.Value));

			//Second color
			codeMatcher.MatchForward(true,
					new CodeMatch(inst => inst.IsLdarg()),
					new CodeMatch(inst => inst.opcode == OpCodes.Call));

			if (!codeMatcher.IsValid) {
				throw new TranspilerDefaultMsgException($"IL line to get the second color could not be found.");
			}

			codeMatcher.SetInstruction(Transpilers.EmitDelegate(() =>
				ModConfig.Instance.BetterSMT_StorageHighlightColor.Value));

			return codeMatcher.InstructionEnumeration();
		}

		
		[HarmonyPatchStringTypes($"{ModInfoBetterSMT.PatchesNamespace}.PlayerNetworkPatch", "HighlightContainerTypeByProduct")]
		[HarmonyBefore(ModInfoBetterSMT.HarmonyId)]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> ModifySlotColorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			CodeMatcher codeMatcher = new CodeMatcher(instructions);

			//Changes color passed by parameter so it uses my config settings instead of a fixed color.

			///Old C#:
			///		HighlightShelf(specificHighlight, true, Color.yellow);
			///New C#:
			///		Color slotColor = shelfType == ParentContainerType.Storage ? 
			///			ModConfig.Instance.BetterSMT_StorageSlotHighlightColor.Value : 
			///			ModConfig.Instance.BetterSMT_ShelfLabelHighlightColor.Value;
			///		HighlightShelf(specificHighlight, true, slotColor);

			//First color
			codeMatcher.MatchForward(true,
					new CodeMatch(inst => inst.IsLdloc()),
					new CodeMatch(inst => inst.opcode == OpCodes.Ldc_I4_1),
					new CodeMatch(inst => inst.opcode == OpCodes.Call));

			if (!codeMatcher.IsValid) {
				throw new TranspilerDefaultMsgException($"IL line to get the slot color could not be found.");
			}

			//Replace existing instruction to get the fixed color, with the argument with the current shelf type.
			codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldarg_2))
				.Advance(1)
				//Call the function that returns the color depending on the shelf type we just added on the stack.
				.Insert(Transpilers.EmitDelegate(GetShelfSlotColor));

			return codeMatcher.InstructionEnumeration();
		}

		private static Color GetShelfSlotColor(int shelfType) =>
			shelfType == 0 ? 
				ModConfig.Instance.BetterSMT_ShelfLabelHighlightColor.Value :
				ModConfig.Instance.BetterSMT_StorageSlotHighlightColor.Value;
		*/

    }
}
