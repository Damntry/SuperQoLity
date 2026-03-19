using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using System;
using UnityEngine;

namespace SuperQoLity.SuperMarket.ModUtils {

    public static class ShaderUtils {

        public static readonly Lazy<Shader> LegacyParticleShader = new (() =>
            Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Unlit/Color")
        );

        /// <summary>
        /// Without a lot more setup, this URP shader can only be currently used for LineRenderer. 
        /// Use LegacyParticleShader for everything else.
        /// </summary>
        public static readonly Lazy<Shader> URP_ParticleShaderUnlit = new (() =>
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Unlit/Color")
        );

        public static readonly Lazy<Shader> SMT_Shader = new (GetGameShader);


        /// <summary>
        /// This is the main Shader used by the game, extracted directly from a vanilla object material.
        /// Useful for when we have prefab materials using the built-in shader, but this being URP it wont
        /// work right. Instead we just assign this shader.
        /// </summary>
        private static Shader GetGameShader() {
            if (GameData.Instance == null) {
                TimeLogger.Logger.LogWarning("GameData instance null", LogCategories.Visuals);
                return null;
            }

            return GameObject.Find("Level_SupermarketProps/UsableProps")
                .NullableObject()?.transform
                .GetChild(0)
                .GetComponent<MeshRenderer>()
                .material.shader;
        }

    }
}
