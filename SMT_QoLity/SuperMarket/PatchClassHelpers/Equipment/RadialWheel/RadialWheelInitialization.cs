using Damntry.Utils.Logging;
using Damntry.UtilsUnity.Rendering;
using Damntry.UtilsUnity.Resources;
using Damntry.UtilsUnity.UI.Extensions;
using Rito.RadialMenu_v3;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel {

    /// <summary>
    /// References to GameObjects and data resulting of the initialization of the Radial functionality.
    /// </summary>
    /// <param name="RadialObj">The main GameObject that holds all the components for radial functionality.</param>
    /// <param name="RadialMenu">The instance of the initialized radial component</param>
    /// <param name="IndexMapping">
    /// The correlation between the Key, representing the index number of a tool inside the
    /// radial object, and Value, as its corresponding internal index in the game.
    /// </param>
    public record RadialRefs(GameObject RadialObj, RadialMenu RadialMenu, Dictionary<int, int> IndexMapping);


    public static class RadialWheelInitialization {

        private static readonly string iconBundlePath = "Assets\\EquipmentWheel\\WheelIcons";

        private static readonly string baseUnityPath = "assets\\RadialWheel\\";


        /// <summary>Loads all assets and objects used for the equipment wheel.</summary>
        public static RadialRefs LoadEquipmentWheel(out Dictionary<int, WheelKeyBind> quickKeysData) {
            GameObject mainRadialObj = null;
            try {
                AssetBundleElement bundleElement = new (typeof(RadialWheelManager), iconBundlePath);

                //Main container of all things related to the radial
                mainRadialObj = GameObjectManager.CreateSuperQoLGameObject(
                        "RadialWheel", TargetObject.UI_MasterCanvas,
                        new(active: true, TransformType.RectTransformUI, TransformLocals.Generic));

                //Object containing the Radial component
                GameObject radialObj = GameObjectManager.CreateSuperQoLGameObject("GearWheel", mainRadialObj,
                    new(active: true, TransformType.RectTransformUI, TransformLocals.Generic));

                //Arrow selection point
                GameObject arrowObj = GenerateWheelGameObjectWithSprite(bundleElement, mainRadialObj,
                    iconName: baseUnityPath + "Arrow_512.png", objectName: "RadialArrow", active: false);

                //Center X to indicate no selection
                GameObject centerXObj = GenerateWheelGameObjectWithSprite(bundleElement, mainRadialObj,
                    iconName: baseUnityPath + "CenterX_512.png", objectName: "RadialCenterX", active: false);

                //Background circle for tool icons to stand out better
                GameObject backgroundObj = GenerateWheelGameObjectWithSprite(bundleElement, radialObj,
                    iconName: baseUnityPath + "BackgroundCircle.png", objectName: "WheelBackground", active: true);
                if (backgroundObj) {
                    backgroundObj.GetComponent<Image>().color = new(0.08f, 0.08f, 0.08f, 0.85f);
                    backgroundObj.transform.localScale = new(8.26f, 8.26f, 8.26f);
                    //Move it to first position, so its rendered earlier in the UI and gets overlaped by others.
                    backgroundObj.transform.SetAsFirstSibling();
                }

                //The icon used as a sample to hold each of the wheel tool icons
                GameObject pieceSampleObj = GenerateWheelGameObjectWithSprite(bundleElement, mainRadialObj,
                    iconName: baseUnityPath + "Square_512.png", objectName: "pieceSample", active: false);

                List<Sprite> sprites = LoadSpawnableToolIcons(bundleElement, out var indexMapping);

                RadialMenu radialMenu = InitializeRadialComponent(radialObj, sprites, arrowObj, centerXObj, 
                    pieceSampleObj, out quickKeysData);

                radialMenu.SetPieceImageSprites(sprites.ToArray());

                return new(radialObj, radialMenu, indexMapping);
            } catch {
                if (mainRadialObj) {
                    mainRadialObj.SetActive(false);
                }
                throw;
            }
        }

        private static RadialMenu InitializeRadialComponent(GameObject radialObj, List<Sprite> sprites, 
                GameObject arrowObj, GameObject centerXObj, GameObject pieceSampleObj, 
                out Dictionary<int, WheelKeyBind> quickKeysData) {

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            TextSettings textSettings = new (
                Font: defaultFont,
                FontSize: 20,
                FontStyle: FontStyle.Normal,
                FontColor: new Color(0.9f, 0.9f, 0.92f),
                BackgroundColor: new Color(0.125f, 0.125f, 0.14f),
                Anchor: AnchorPresets.BottonCenter,
                AnchorOffsets: new Vector2(0f, -15f),
                BackgroundSize: new Vector2(150f, 25f)
            );

            //TODO 2 - Think of a less cursed way of doing this. Im badly replicating the way Unity properties 
            //  are initialized from editor values, where it deserializes them automatically when it awakes.
            //  This is the main blocker to allow me to have multiple radial wheels.
            RadialMenu.InitializeProperties(
                sprites.Count,
                pieceSampleObj,
                arrowObj ? arrowObj.GetComponent<RectTransform>() : null,
                centerXObj ? centerXObj.GetComponent<RectTransform>() : null,
                quickKeysEnabled: ModConfig.Instance.EnableRadialQuickToolsKeys.Value,
                modifiers: [(KeyCode)ModConfig.Instance.RadialQuickToolsModifierKey.Value],
                textSettings,
                out quickKeysData,
                pieceDist: 325f,
                centerRange: 0.1725f,
                appearanceDuration: 0.60f,
                mainType: RadialMenu.MainType.AlphaAndScaleChange,
                appearanceType: RadialMenu.AppearanceType.ScaleChange,
                appearanceEasing: EasingType.OutExpo,
                disppearanceDuration: 0.1f,
                disappearanceType: RadialMenu.AppearanceType.Fade,
                disappearanceEasing: EasingType.OutElastic
            );

            //Add the wheel component. We do it this late so we can ready all objects needed for the RadialMenu 
            //  static values, that will be embedded into it as if they were Unity Editor serialized properties.
            return radialObj.AddComponent<RadialMenu>();
        }

        private static GameObject GenerateWheelGameObjectWithSprite(AssetBundleElement bundleElement,
                GameObject mainRadialObj, string iconName, string objectName, bool active) {

            if (bundleElement.TryLoadObject(iconName, out Texture2D iconTexture)) {
                Sprite sprite = AssetLoading.GetSpriteFromTexture(iconTexture, Vector2.zero);
                GameObject gameObject = GameObjectManager.CreateSuperQoLGameObject(objectName, mainRadialObj,
                        new(active: active, TransformType.RectTransformUI, TransformLocals.Generic));
                Image image = gameObject.AddComponent<Image>();
                image.sprite = sprite;

                return gameObject;
            }
            return null;
        }

        private static List<Sprite> LoadSpawnableToolIcons(AssetBundleElement bundleElement, out Dictionary<int, int> indexMapping) {
            List<Sprite> sprites = new();
            indexMapping = new();

            int wheelIndex = 0;

            if (!GetUserConfigToolDisplayList(out List<ToolWheelDefinition> toolDisplayList)) {
                //User string wasnt valid. Use defaults.
                toolDisplayList = ToolWheelDefinitions.GetAllSpawnableToolDefinitions().ToList();
            }

            foreach (ToolWheelDefinition toolDef in toolDisplayList) {
                if (toolDef.IsRadialSpawnable &&
                        bundleElement.TryLoadObject(toolDef.IconUnityPath, out Texture2D iconTexture)) {

                    Sprite sprite = AssetLoading.GetSpriteFromTexture(iconTexture, Vector2.zero);
                    sprites.Add(sprite);
                    indexMapping.Add(wheelIndex++, (int)toolDef.Index);
                }
            }

            return sprites;
        }

        private static bool GetUserConfigToolDisplayList(out List<ToolWheelDefinition> toolDisplayList) {
            toolDisplayList = null;

            string displayControlString = ModConfig.Instance.RadialDisplayControl.Value;
            if (!string.IsNullOrEmpty(displayControlString)) {

                string[] userDefinedTools = displayControlString
                    .Split([','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => GetComparableDisplayName(t))
                    .ToArray();

                var toolsDict = ToolWheelDefinitions
                    .GetAllSpawnableToolDefinitions()
                    .ToDictionary(t => GetComparableDisplayName(t.DisplayName), t => t);

                toolDisplayList = new();
                foreach (string toolName in userDefinedTools) {
                    if (toolsDict.TryGetValue(toolName, out ToolWheelDefinition tool)) {
                        toolDisplayList.Add(tool);
                    } else {
                        //Exit when any tool name from the user is not correct.
                        string displayControlConfigName = ModConfig.Instance.RadialDisplayControl.Definition.Key;
                        TimeLogger.Logger.LogWarning($"The user defined value '{toolName}' in " +
                            $"the setting '{displayControlConfigName}' is not a valid tool name.", LogCategories.UI);
                        toolDisplayList.Clear();

                        return false;
                    }
                }

                return toolDisplayList.Count > 0;
            }

            return false;
        }


        private static string GetComparableDisplayName(string toolDisplayName) =>
            toolDisplayName.Replace(" ", "").ToLower();

    }
}
