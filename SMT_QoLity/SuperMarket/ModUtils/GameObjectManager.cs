using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using HutongGames.PlayMaker;
using StarterAssets;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using System;
using System.ComponentModel;
using UnityEngine;
using Component = UnityEngine.Component;

namespace SuperQoLity.SuperMarket.ModUtils {

	public enum TargetObject {
		[Description("MasterOBJ/MasterCanvas")]
		UI_MasterCanvas,
		[Description("LocalGamePlayer")]
		LocalGamePlayer,
		[Description("GameDataManager")]
		GameDataManager,
	}

    public enum TransformType {
        Transform,
        RectTransformUI
    }

    //TODO 5 - Ideally this could be a Globals Bepinex class where all data and the target objects are 
    //		added on initialization, but then I would lose the compile time enum names.
    public static class GameObjectManager {

		//This is only really the Root of a bunch of important stuff in the
		//	DontDestroyOnLoad scene, but not a root itself.
		//public const string RootGameObject = "MasterOBJ";

		private static readonly string superQolityPrefix = "SuperQoL_";

		public static GameObject CreateSuperQoLGameObject(string name, 
				TargetObject parentTarget, CreationSettings creationSettings) {
			if (GetGameObjectFrom(parentTarget, out GameObject parentObject)) {
				return CreateSuperQoLGameObject(name, parentObject, creationSettings);
			}
			
			return null;
		}


		public static GameObject CreateSuperQoLGameObject(string name, 
				GameObject parentObject, CreationSettings creationSettings) {
			if (parentObject == null) {
				TimeLogger.Logger.LogError($"The parameter {nameof(parentObject)} cannot be null.",
					LogCategories.Other);
				return null;
			}

            GameObject gameObj = new(superQolityPrefix + name);
			if (creationSettings.TransformType == TransformType.RectTransformUI) {
				gameObj.AddComponent<RectTransform>();
            }

			Transform transform = gameObj.transform;

            gameObj.SetActive(creationSettings.Active);
			transform.SetParent(parentObject.transform);

            //Set transform values only when not null. Otherwise values will be left as
            //	default so they keep being automatically inherited from the parent.
            if (creationSettings.TransformLocals.LocalPosition.HasValue) {
                transform.localPosition = creationSettings.TransformLocals.LocalPosition.Value;
            }
            if (creationSettings.TransformLocals.LocalRotation.HasValue) {
                transform.localRotation = creationSettings.TransformLocals.LocalRotation.Value;
            }
            if (creationSettings.TransformLocals.LocalScale.HasValue) {
                transform.localScale = creationSettings.TransformLocals.LocalScale.Value;
            }

            return gameObj;
		}

        public static T AddComponentTo<T>(TargetObject parentTarget)
				where T : Component {

			if (GetGameObjectFrom(parentTarget, out GameObject parentObject)) {
				return parentObject.AddComponent<T>();
			}

			return null;
		}

		public static bool GetGameObjectFrom(TargetObject targetObject, out GameObject gameObject) {
			//Try fast method using children component Instance as a reference
			gameObject = GetComponentForTargetObject(targetObject).GameObject();

			//If the component is not alive, search for the GameObject.
			gameObject ??= GameObject.Find(targetObject.GetDescription());

			if (gameObject == null) {
				TimeLogger.Logger.LogError($"The GameObject for target " +
					$"\"{targetObject}\" ({targetObject.GetDescription()}) could not be retrieved. Maybe you need " +
					$"to do this it at a later point?", LogCategories.Other);

				TimeLogger.Logger.LogError($"On the other hand, the GameObject.Find is null? " +
					$"{GameObject.Find(targetObject.GetDescription()) == null}", LogCategories.TempTest);
			}

			return gameObject != null;
		}

		private static Component GetComponentForTargetObject(TargetObject targetObject) =>
			targetObject switch {
				TargetObject.UI_MasterCanvas => SMTInstances.GameCanvas(),
				TargetObject.LocalGamePlayer => SMTInstances.FirstPersonController(),
				TargetObject.GameDataManager => SMTInstances.GameDataManager(),
				_ => throw new NotImplementedException($"{targetObject} value is not implemented.")
			};

	}

    public readonly struct CreationSettings(bool? active, TransformType? transformType, TransformLocals? transformLocals) {

        /// <summary>Active: true, transformType: Transform, transformLocals: Inherited</summary>
        public static CreationSettings Defaults { get; } = new(null, null, null);


		public bool Active { get; } = active ?? true;

        public TransformType TransformType { get; } = transformType ?? TransformType.Transform;

        public TransformLocals TransformLocals { get; } = transformLocals ?? TransformLocals.Inherited;
    }

    /// <summary>
    /// Local transform properties.
    /// </summary>
    /// <param name="localPosition">The local position. If null, it will inherited from the parent.</param>
    /// <param name="localRotation">The local rotation. If null, it will inherited from the parent.</param>
    /// <param name="localScale">The local scale. If null, it will inherited from the parent.</param>
    public readonly struct TransformLocals(Vector3? localPosition, Quaternion? localRotation, Vector3? localScale) {

        /// <summary>Values will be inherited from its parent.</summary>
        public static TransformLocals Inherited { get; } = new(null, null, null);
        /// <summary>Vector3(0, 0, 0) position, Quaternion(0, 0, 0, 1) rotation, and Vector3(1, 1, 1) scale.</summary>
        public static TransformLocals Generic { get; } = new(Vector3.zero, Quaternion.identity, Vector3.one);

        public Vector3? LocalPosition { get; } = localPosition;
        public Vector3? LocalScale { get; } = localScale;
        public Quaternion? LocalRotation { get; } = localRotation;
    }


    public struct SMTInstances {
		public static GameCanvas GameCanvas() => global::GameCanvas.Instance;
		public static FirstPersonController FirstPersonController() => StarterAssets.FirstPersonController.Instance;
		public static PlayerNetwork LocalPlayerNetwork() => FirstPersonController().NullableObject()?.GetComponent<PlayerNetwork>();
        public static BroomShotgunNetwork LocalBroomShotgunNetwork() => FirstPersonController().NullableObject()?.GetComponent<BroomShotgunNetwork>();
        public static GameData GameDataManager() => GameData.Instance;
		public static Builder_Main BuilderMain() => GameCanvas().NullableObject()?.GetComponent<Builder_Main>();
        public static NetworkSpawner NetworkSpawner() => GameDataManager().NullableObject()?.GetComponent<NetworkSpawner>();
		public static GameObject MasterObject() => FsmVariables.GlobalVariables.FindFsmGameObject("MasterOBJ").Value;
		public static PlayerPermissions PlayerPermissions() => FirstPersonController().NullableObject()?.GetComponent<PlayerPermissions>();
        public static ManagerBlackboard ManagerBlackboard() => GameDataManager().NullableObject()?.GetComponent<ManagerBlackboard>();


        private static CustomCameraController cameraControllerCache;

        public static CustomCameraController GetCustomCameraController() {
            if (!cameraControllerCache) {
                if (GameObject.Find("Player_Camera").TryGetComponent(out CustomCameraController cameraControl)) {
                    cameraControllerCache = cameraControl;
                }
            }

            return cameraControllerCache;
        }

    }
}
