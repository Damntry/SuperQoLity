using System;
using System.ComponentModel;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.UtilsUnity.ExtensionMethods;
using StarterAssets;
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

	//TODO 5 - Ideally this could be a Globals Bepinex class where all data and the target objects are 
	//		added on initialization, but then I would lose the compile time enum names.
	public static class SMTGameObjectManager {

		public const string RootGameObject = "MasterOBJ";

		private static readonly string superQolityPrefix = "SuperQoL_";

		public static GameObject CreateSuperQoLGameObject(string name, TargetObject parentTarget, bool active = true) {
			if (GetGameObjectFrom(parentTarget, out GameObject parentObject)) {
				return CreateSuperQoLGameObject(name, parentObject, active);
			}

			return null;
		}

		public static GameObject CreateSuperQoLGameObject(string name, GameObject parentObject, bool active = true) {
			if (parentObject == null) {
				TimeLogger.Logger.LogTimeError($"The parameter {nameof(parentObject)} cannot be null.",
					LogCategories.Other);
				return null;
			}
			GameObject gameObj = new(superQolityPrefix + name);
			gameObj.SetActive(active);
			gameObj.transform.SetParent(parentObject.transform);

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
			//Try fast method using children component instance as a reference
			gameObject = GetComponentForTargetObject(targetObject).GameObject();

			//If the component is not alive, search for the GameObject.
			gameObject ??= GameObject.Find(targetObject.GetDescription());

			if (gameObject == null) {
				TimeLogger.Logger.LogTimeError($"The GameObject for target " +
					$"\"{targetObject}\" ({targetObject.GetDescription()}) could not be retrieved. Maybe you need " +
					$"to do this it at a later point?", LogCategories.Other);

				TimeLogger.Logger.LogTimeError($"On the other hand, the GameObject.Find is null? " +
					$"{GameObject.Find(targetObject.GetDescription()) == null}", LogCategories.TempTest);
			}

			return gameObject != null;
		}

		private static Component GetComponentForTargetObject(TargetObject targetObject) =>
			targetObject switch {
				TargetObject.UI_MasterCanvas => SMTComponentInstances.GameCanvasInstance(),
				TargetObject.LocalGamePlayer => SMTComponentInstances.FirstPersonControllerInstance(),
				TargetObject.GameDataManager => SMTComponentInstances.GameDataManagerInstance(),
				_ => throw new NotImplementedException($"{targetObject} value is not implemented.")
			};

	}


	public struct SMTComponentInstances {
		public static GameCanvas GameCanvasInstance() => GameCanvas.Instance;
		public static FirstPersonController FirstPersonControllerInstance() => FirstPersonController.Instance;
		public static PlayerNetwork PlayerNetworkInstance() => FirstPersonControllerInstance()?.GetComponent<PlayerNetwork>();
		public static GameData GameDataManagerInstance() => GameData.Instance;
	}
}
