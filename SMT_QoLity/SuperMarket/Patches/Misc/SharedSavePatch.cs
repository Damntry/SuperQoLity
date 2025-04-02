using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable;
using Damntry.UtilsUnity.Components;
using HutongGames.PlayMaker;
using Mirror;
using UnityEngine;

namespace SuperQoLity.SuperMarket.Patches.Misc {

	public class SharedSavePatch : FullyAutoPatchedInstance {

		public override bool IsAutoPatchEnabled => Plugin.IsSolutionInDebugMode;

		public override string ErrorMessageOnAutoPatchFail { get; protected set; } = $"{MyPluginInfo.PLUGIN_NAME} - TestAndDebugPatch FAILED. Disabled";

		public override void OnPatchFinishedVirtual(bool IsPatchActive) {
			if (IsPatchActive) {
				KeyPressDetection.AddHotkey(KeyCode.Less, 1000, () => { SaveAsClientX(); });
			}
		}


		public static async void SaveAsClientX() {
			//Based on BetterSMT and GameData.WaitUntilNewDay()
			LOG.TEMPWARNING("Save started");

			if (NetworkManager.singleton != null) {
				TimeLogger.Logger.LogTimeWarning("You can only save in a loaded world.", LogCategories.Other);
			}

			NetworkSpawner nSpawnerComponent = GameData.Instance.GetComponent<NetworkSpawner>();
			if (nSpawnerComponent.isSaving) {
				TimeLogger.Logger.LogTimeWarning("Saving is already in progress.", LogCategories.Other);
				return;
			}

			if (NetworkManager.singleton.mode == NetworkManagerMode.ClientOnly) {
				//TODO 0 - Once tests are done, move all logic in here
			}

			string loadedSaveFileName = FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value;
			string newSaveFileName = "StoreFile12.es3";

			//TODO 0 - Should probably be doing this? BetterSMT does it twice for some reason.
			//GameData.Instance.DoDaySaveBackup();

			LOG.TEMPWARNING($"CurrentFilename: {loadedSaveFileName} - " +
				$"City save name (host only) {GetCityName(loadedSaveFileName)}");

			FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value = newSaveFileName;

			await SavePersistentValues();

			await SavePropsCoroutine();
			//IEnumerator saveProps = save.gameDataOBJ.GetComponent<NetworkSpawner>().SavePropsCoroutine();
			//while (saveProps.MoveNext());

			//For safety more than anything else, since this only gets executed in the client.
			FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value = loadedSaveFileName;

			//TODO 0 - Notify the user of save finished.
		}

		private static string GetCityName(string loadedSaveFileName) {
			string currentSaveFile = Path.Combine(Application.persistentDataPath, loadedSaveFileName);
			ES3Settings settings = new(ES3.EncryptionType.AES, "g#asojrtg@omos)^yq");
			ES3File file = new(currentSaveFile, settings, false);
			return file.Load<string>("StoreName", null);

			/*
			ES3Settings settings = new ES3Settings(ES3.EncryptionType.AES, "g#asojrtg@omos)^yq");
			ES3.CacheFile(currentSaveFile, settings);
			ES3Settings settings2 = new ES3Settings(currentSaveFile, ES3.Location.Cache);
			if (ES3.KeyExists("StoreName", settings)) {
				ES3.Save("StoreName", ES3.Load<string>("StoreName", settings), settings);
			} else {
				LOG.TEMPFATAL($"Save slot city name not found.");
			}
			
			string newSaveFilePath = Path.Combine(Application.persistentDataPath, newSaveFileName);
			ES3.CacheFile(newSaveFilePath, settings);
			ES3.StoreCachedFile(newSaveFilePath, settings);
			*/
		}

		private static async Task SavePersistentValues() {
			LOG.TEMPDEBUG("1. " + FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value);

			PlayMakerFSM fsm = GameData.Instance.SaveOBJ.GetComponent<PlayMakerFSM>();
			fsm.FsmVariables.GetFsmBool("IsSaving").Value = true;
			LOG.TEMPDEBUG("2. " + FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value);
			fsm.SendEvent("Send_Data");
			LOG.TEMPDEBUG("3. " + FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value);
			while (fsm.FsmVariables.GetFsmBool("IsSaving").Value) {
				LOG.TEMPDEBUG("4. " + FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value);
				await Task.Delay(10);
				LOG.TEMPDEBUG("5. " + FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value);
			}

			/*
			SaveBehaviour save = new() { gameDataOBJ = GameObject.Find("GameDataManager") };
			save.SavePersistentValues();
			netSpawner = save.gameDataOBJ.GetComponent<NetworkSpawner>();
			*/
		}

		public static async Task SavePropsCoroutine() {
			NetworkSpawner instance = GameObject.Find("GameDataManager").GetComponent<NetworkSpawner>();

			instance.isSaving = true;

			GameCanvas.Instance.transform.Find("SavingContainer").gameObject.SetActive(value: true);
			await Task.Delay(500);
			int counter = 0;
			string value = FsmVariables.GlobalVariables.GetFsmString("CurrentFilename").Value;
			string filepath = Application.persistentDataPath + "/" + value;
			LOG.TEMPWARNING($"We will save props in file \"{value}\"");
			ES3Settings cacheSettings = new ES3Settings(ES3.EncryptionType.AES, "g#asojrtg@omos)^yq");
			ES3.CacheFile(filepath, cacheSettings);
			ES3Settings settings = new ES3Settings(filepath, ES3.Location.Cache);
			CultureInfo cultureInfo = new CultureInfo(Thread.CurrentThread.CurrentCulture.Name);
			if (cultureInfo.NumberFormat.NumberDecimalSeparator != ",") {
				cultureInfo.NumberFormat.NumberDecimalSeparator = ",";
				Thread.CurrentThread.CurrentCulture = cultureInfo;
			}

			for (int i = 0; i < 4; i++) {
				GameObject gameObject = instance.levelPropsOBJ.transform.GetChild(i).gameObject;
				if (gameObject.transform.childCount != 0) {
					LOG.TEMPWARNING($"Saving {gameObject.transform.childCount} levelPropsOBJ objects for index {i}.");
					for (int j = 0; j < gameObject.transform.childCount; j++) {
						GameObject gameObject2 = gameObject.transform.GetChild(j).gameObject;
						string value2 = i + "|" + gameObject2.GetComponent<Data_Container>().containerID + "|" + gameObject2.transform.position.x + "|" + gameObject2.transform.position.y + "|" + gameObject2.transform.position.z + "|" + gameObject2.transform.rotation.eulerAngles.y;
						ES3.Save("propdata" + counter, value2, filepath, settings);
						string key = "propinfoproduct" + counter;
						int[] productInfoArray = gameObject2.GetComponent<Data_Container>().productInfoArray;
						ES3.Save(key, productInfoArray, filepath, settings);
						counter++;
					}
				}
			}

			for (int k = counter; (float)k < float.PositiveInfinity; k++) {
				string key2 = "propdata" + counter;
				if (!ES3.KeyExists(key2, filepath, settings)) {
					break;
				}

				ES3.DeleteKey(key2, filepath, settings);
			}

			counter = 0;
			int num = 0;
			GameObject parentOBJ2 = instance.levelPropsOBJ.transform.GetChild(7).gameObject;
			for (int l = 0; (float)l < float.PositiveInfinity; l++) {
				string key3 = "decopropdata" + num;
				if (!ES3.KeyExists(key3, filepath, settings)) {
					break;
				}

				ES3.DeleteKey(key3, filepath, settings);
				num++;
			}

			LOG.TEMPWARNING($"Saving {parentOBJ2.transform.childCount} decorative objects.");
			for (int m = 0; m < parentOBJ2.transform.childCount; m++) {
				GameObject gameObject3 = parentOBJ2.transform.GetChild(m).gameObject;
				string value3 = "7|" + gameObject3.GetComponent<BuildableInfo>().decorationID + "|" + gameObject3.transform.position.x + "|" + gameObject3.transform.position.y + "|" + gameObject3.transform.position.z + "|" + gameObject3.transform.rotation.eulerAngles.y;
				ES3.Save("decopropdata" + counter, value3, filepath, settings);
				if (gameObject3.GetComponent<BuildableInfo>().decorationID == 4) {
					string key4 = "decopropdataextra" + counter;
					string value4 = gameObject3.GetComponent<DecorationExtraData>().intValue + "|" + gameObject3.GetComponent<DecorationExtraData>().stringValue;
					ES3.Save(key4, value4, filepath, settings);
				}

				if ((bool)gameObject3.GetComponent<PaintableDecoration>()) {
					string key5 = "decopaintabledata" + counter;
					string value5 = gameObject3.GetComponent<PaintableDecoration>().mainValue + "|" + gameObject3.GetComponent<PaintableDecoration>().secondaryValue;
					ES3.Save(key5, value5, filepath, settings);
				}

				counter++;
			}

			ES3.StoreCachedFile(filepath, cacheSettings);
			await Task.Delay(20);
			GameCanvas.Instance.transform.Find("SavingContainer").gameObject.SetActive(value: false);
			LOG.TEMPWARNING($"Prop saving finished.");
			instance.isSaving = false;
		}

	}
}
