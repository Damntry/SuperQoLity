using System;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using Damntry.Utils.Reflection;
using SuperQoLity.SuperMarket.ModUtils;
using Damntry.UtilsUnity.Timers;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Components {

	public class EmployeeWarpSound : MonoBehaviour {

		private static readonly Lazy<Task<AudioClip>> warpAudioClip = new(() => GetWarpAudioClip());

		private GameObject warpAudioGameObject;

		/// <summary>Sound cooldown shared between all instances that can play this sound.</summary>
		private static UnityTimeStopwatch warpGlobalCooldownTimer = new UnityTimeStopwatch();

		private UnityTimeStopwatch warpLocalCooldownTimer = new UnityTimeStopwatch();

		private const int warpGlobalCooldownMillis = 100;

		private const int warLocalCooldownMillis = 250;

		//Since we dont know if this method will be called or not, to save performance we dont add this object 
		//	to the employee when it spawns, but instead initialize everything once only when this method is called.
		public static void PlayEmployeeWarpSound(NPC_Info employee) {
			if (!employee.gameObject.TryGetComponent(out EmployeeWarpSound employeeWarpSound)) {
				employeeWarpSound = employee.gameObject.AddComponent<EmployeeWarpSound>();
				employeeWarpSound.Initialize(employee.gameObject.transform);
			}

			_ = employeeWarpSound.PlayWarpSound();
		}

		public void Initialize(Transform employeeTransform) {
			warpAudioGameObject = new GameObject("WarpAudioGameObject");
			this.warpAudioGameObject.transform.SetParent(employeeTransform);
		}

		private static async Task<AudioClip> GetWarpAudioClip() {
			string warpAudioFilePath = AssemblyUtils.GetCombinedPathFromAssemblyFolder(typeof(EmployeeWarpSound), "SoundEffects\\Warp.mp3");

			UnityWebRequest audioWebRequest = UnityWebRequestMultimedia.GetAudioClip(warpAudioFilePath, AudioType.MPEG);
			await audioWebRequest.SendWebRequest();

			if (audioWebRequest.result == UnityWebRequest.Result.Success) {
				return DownloadHandlerAudioClip.GetContent(audioWebRequest);
			} else {
				throw new InvalidOperationException($"Request ended with result {audioWebRequest.result} and error: {audioWebRequest.error}.");
			}
		}

		//Async void since we dont want to keep the calling method waiting.
		private async Task<bool> PlayWarpSound() {
			//Get audio source, or create if it doesnt exist.
			if (!warpAudioGameObject.TryGetComponent(out AudioSource warpSound)) {
				warpSound = await AddAudioSourceComponent();
			}

			//TODO 4 - An alternative idea to the global cooldown, is a global limit on the number of Plays within X ms.
			//		Should probably add both to be honest. A tiny global cooldown, and then the limit per period on top,
			//		and I could use PeriodicTimeLimitedCounter for it.
			if ((!warpLocalCooldownTimer.IsRunning || warpLocalCooldownTimer.ElapsedMillisecondsPrecise >= warpGlobalCooldownMillis) &&
					(!EmployeeWarpSound.warpGlobalCooldownTimer.IsRunning || EmployeeWarpSound.warpGlobalCooldownTimer.ElapsedMillisecondsPrecise >= warLocalCooldownMillis)) {
				warpLocalCooldownTimer.Restart();
				warpGlobalCooldownTimer.Restart();

				warpSound.volume = 0.10f * ModConfig.Instance.TeleportSoundVolume.Value;

				warpSound.Play();
				return true;
			}

			return false;
		}

		private async Task<AudioSource> AddAudioSourceComponent() {
			AudioSource warpSound = warpAudioGameObject.AddComponent<AudioSource>();
			warpSound.clip = await warpAudioClip.Value;
			warpSound.playOnAwake = false;
			warpSound.priority = 256;       //Lowest priority
			warpSound.spatialBlend = 1;     //Enable effect of the 3D engine on this audio
											//warpSound.pitch = 1.1f;		//Playback speed. Default 1.
			warpSound.rolloffMode = AudioRolloffMode.Linear;
			warpSound.maxDistance = 19f;	//500 default
			warpSound.minDistance = 1f;

			return warpSound;
		}

	}

}
