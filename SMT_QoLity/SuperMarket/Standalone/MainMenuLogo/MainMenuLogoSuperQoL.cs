using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsUnity.Rendering;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SuperQoLity.SuperMarket.Standalone.MainMenuLogo.Fsm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperQoLity.SuperMarket.Standalone.MainMenuLogo {

    public static class MainMenuLogos {

        private static readonly string RelativeLogosFolderPath = "Assets\\MainMenuLogos";

        public static readonly int ProductsInBetween = 5;

        public static readonly (float min, float max) LogoRotationYRange = (-30, 70);

        public static readonly Vector3 LogoLocalPositionOffset = new (0, 0.05f, 0);

        private static readonly float LogoTargetSize = 32;
                

        public async static void StartProcess() {
            int failCounter = 5;
            while (failCounter > 0) {
                if (SceneManager.GetActiveScene().name == "A_Intro") {

                    GameObject spawnObj = GameObject.Find("SpawnProductBehaviour");
                    if (spawnObj && spawnObj.TryGetComponent(out PlayMakerFSM fsm)) {

                        GameObject vanillaMainMenuProduct = GetVanillaMainMenuProduct(fsm);
                        List<GameObject> mainMenuLogos = GetObjectsFromLogos(vanillaMainMenuProduct);

                        if (mainMenuLogos.Count != 0) {
                            ReplaceFsmActions(fsm, mainMenuLogos);
                        } else {
                            TimeLogger.Logger.LogWarning("No logo images where found in the subfolder " +
                                $"'{RelativeLogosFolderPath}'. Make sure that the files have png or jpg extension.",
                                LogCategories.Visuals);
                        }

                        break;
                    }

                    failCounter++;
                }

                await Task.Delay(500);
            }
        }

        private static GameObject GetVanillaMainMenuProduct(PlayMakerFSM fsm) {
            FsmArray productArray = fsm.Fsm.GetFsmArray("PRoductArray"); //Not a typo

            //Each reference in productArray is a product prefab.
            return (GameObject)productArray.Get(0);
        }

        private static List<GameObject> GetObjectsFromLogos(GameObject vanillaMainMenuProduct) {
            List<GameObject> listObj = new();

            string logosFolder = AssemblyUtils.GetCombinedPathFromAssemblyFolder(typeof(Plugin), RelativeLogosFolderPath);
            var logoFiles = Directory.EnumerateFiles(logosFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => file.ToLower().EndsWith(".png") || file.ToLower().EndsWith(".jpg"));

            foreach (string logoPath in logoFiles) {
                TimeLogger.Logger.LogInfo($"Found logo file: {logoPath}", LogCategories.Visuals);
                listObj.Add(CreateProductObject(vanillaMainMenuProduct, logoPath));
            }

            return listObj;
        }

        private static GameObject CreateProductObject(GameObject vanillaMainMenuProduct, string filePath) {
            GameObject newProduct = UnityEngine.Object.Instantiate(vanillaMainMenuProduct);

            //Remove the optimizer that uses the MeshRenderer
            Component combinable = newProduct.GetComponents<Component>()
                .FirstOrDefault(c => c.ToString().Contains("Combinable"));
            if (combinable) {
                UnityEngine.Object.DestroyImmediate(combinable);
            }

            //Remove mesh
            UnityEngine.Object.DestroyImmediate(newProduct.GetComponent<MeshRenderer>());
            UnityEngine.Object.DestroyImmediate(newProduct.GetComponent<MeshFilter>());

            //Add sprite with logo
            SpriteRenderer spriteRender = newProduct.AddComponent<SpriteRenderer>();
            spriteRender.sprite = AssetLoading.GetSpriteFromFile(filePath, pivot: new(0.5f, 0f));

            //Scale to target size
            Rect spriteRect = spriteRender.sprite.rect;
            float maxDimensionSize = Math.Max(spriteRect.width, spriteRect.height);
            float scale = (float)Math.Round(LogoTargetSize / maxDimensionSize, 4);

            spriteRender.transform.localScale = new(scale, scale, scale);

            return newProduct;
        }

        private static void ReplaceFsmActions(PlayMakerFSM fsm, List<GameObject> mainMenuLogos) {
            //Modify some of the existing FsmStateAction so we can do some changes in the product spawning logic.
            FsmState fsmSpawnState = fsm.FsmStates.FirstOrDefault(f => f.Name == "SpawnRandom");
            if (fsmSpawnState != null) {
                for (int i = 0; i < fsmSpawnState.Actions.Length; i++) {
                    FsmStateAction stateAction = fsmSpawnState.Actions[i];
                    if (stateAction is ArrayGetRandom stateActionArrayRandom) {
                        fsmSpawnState.Actions[i] = new ArrayGetRandomButNotReally(
                            stateActionArrayRandom, mainMenuLogos);
                    } else if (stateAction is RandomFloat stateActionRandomFloat) {
                        fsmSpawnState.Actions[i] = new RandomFloatButNotReally(
                            stateActionRandomFloat);
                    } else if (stateAction is SetPosition stateActionSetPosition) {
                        fsmSpawnState.Actions[i] = new SetPositionOffset(
                            stateActionSetPosition);
                    }

                }
                fsmSpawnState.SaveActions();
            }
        }

    }
}
