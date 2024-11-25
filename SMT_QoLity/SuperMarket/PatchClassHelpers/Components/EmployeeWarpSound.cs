using System;
using System.Threading.Tasks;
using Damntry.UtilsBepInEx.Logging;
using UnityEngine.Networking;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Components {

	public class EmployeeWarpSound : MonoBehaviour {

		private static readonly Lazy<Task<AudioClip>> warpAudioClip = new(() => GetWarpAudioClip());

		private GameObject audioGameObject;


		public EmployeeWarpSound() {
			audioGameObject = new GameObject("audioGameObject");
		}

		public static void PlayEmployeeWarpSound(NPC_Info employee) {
			if (!employee.gameObject.TryGetComponent(out EmployeeWarpSound employeeWarpSound)) {
				employeeWarpSound = employee.gameObject.AddComponent<EmployeeWarpSound>();
			}

			employeeWarpSound.PlayWarpSound();
		}

		private static async Task<AudioClip> GetWarpAudioClip() {
			LOG.DEBUG($"Getting warp audio clip from path.");
			//TODO 1 - Get audio path from current assembly path
			string warpAudioFilePath = "H:\\!SteamLibrary\\steamapps\\common\\Supermarket Together\\BepInEx\\plugins\\es.damntry.SuperQoLity\\Sounds\\Warp.mp3";
			UnityWebRequest audioWebRequest = UnityWebRequestMultimedia.GetAudioClip(warpAudioFilePath, AudioType.MPEG);
			LOG.DEBUG($"Audio clip obtained.");
			await audioWebRequest.SendWebRequest();
			LOG.DEBUG($"Sent web request.");

			if (audioWebRequest.result == UnityWebRequest.Result.Success) {
				LOG.DEBUG($"Audio clip optained from path.");
				return DownloadHandlerAudioClip.GetContent(audioWebRequest);
			} else {
				throw new InvalidOperationException($"Request ended with result {audioWebRequest.result} and error: {audioWebRequest.error}.");
			}
		}

		public async void PlayWarpSound() {
			if (!audioGameObject.TryGetComponent(out AudioSource warpSound)) {
				warpSound = audioGameObject.AddComponent<AudioSource>();
				warpSound.clip = await warpAudioClip.Value;
				warpSound.volume = 0.02f;   //TODO 3 - This is already the ceiling. Now I need to reduce based on the SFX in game volume.
				warpSound.maxDistance = 500;	//500 default
			}

			//TODO 4 - Make some system so it can play more than one at the same time, but not too many.
			//		I think just allowing that it plays for X ms before I allow another one should be enough?
			if (!warpSound.isPlaying) {
				warpSound.Play();
			}
		}


		/*
		private static AudioClip GetWarpAudioClip2() {
			AudioClip audioClip = null;
			//TODO 1 - Change to use current assembly path to search.
			string warpAudioFilePath = "H:\\!SteamLibrary\\steamapps\\common\\Supermarket Together\\BepInEx\\plugins\\es.damntry.SuperQoLity\\Sounds\\Warp.mp3";

			var warpAudioClipHandler = new DownloadHandlerAudioClip($"file://{warpAudioFilePath}", AudioType.MPEG);
			warpAudioClipHandler.compressed = true;

			using (UnityWebRequest wr = new UnityWebRequest($"file://{warpAudioFilePath}", "GET", warpAudioClipHandler, null)) {
				yield return wr.SendWebRequest();
				if (wr.responseCode == 200) {
					audioClip = warpAudioClipHandler.audioClip;
				}
			}

			return audioClip;
		}
		*/

	}

}
