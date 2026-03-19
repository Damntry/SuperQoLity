using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsUnity.ExtensionMethods;
using SuperQoLity.SuperMarket.Patches.BroomShotgun;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers {

    public enum SoundBite {
        Equip,
        Shoot,
        Pump,
        All
    }

    public enum AudioAction {
        Play,
        Stop,
        Pause,
        UnPause
    }

    public class WeaponAudioSystem {


        private static AudioClip shotgunEquipAudioClip;

        private AudioSource shotgunEquipAudio;

        private static AudioClip shotgunShotAudioClip;

        private AudioSource shotgunShotAudio;

        private static AudioClip shotgunPumpAudioClip;

        private AudioSource shotgunPumpAudio;


        public void Initialize(GameObject fpController) {
            shotgunEquipAudio = GetShotgunAudioSourceByName(nameof(shotgunEquipAudio), fpController.transform);
            shotgunShotAudio = GetShotgunAudioSourceByName(nameof(shotgunShotAudio), fpController.transform);
            shotgunPumpAudio = GetShotgunAudioSourceByName(nameof(shotgunPumpAudio), fpController.transform);
        }

        public static AudioSource GetShotgunAudioSourceByName(string name, Transform obj) =>
            obj.GetComponents<AudioSource>()
                .Where(a => a.clip.name == name)
                .FirstOrDefault();


        public void SetPlaybackState(SoundBite soundBite, AudioAction audioAction) {
            if (soundBite == SoundBite.Equip || soundBite == SoundBite.All) {
                DoAudioAction(shotgunEquipAudio.NullableObject(), audioAction);
            }
            if (soundBite == SoundBite.Shoot || soundBite == SoundBite.All) {
                DoAudioAction(shotgunShotAudio.NullableObject(), audioAction);
            }
            if (soundBite == SoundBite.Pump || soundBite == SoundBite.All) {
                DoAudioAction(shotgunPumpAudio.NullableObject(), audioAction);
            }
        }

        private void DoAudioAction(AudioSource audioSource, AudioAction audioAction) {
            if (!audioSource) {
                return;
            }
            if (audioAction == AudioAction.Play) {
                audioSource.Play();
            } else if (audioAction == AudioAction.Stop) {
                audioSource.Stop();
            } else if (audioAction == AudioAction.Pause) {
                audioSource.Pause();
            } else if (audioAction == AudioAction.UnPause) {
                audioSource.UnPause();
            }
        }


        public static async Task LoadSoundFiles() {
            shotgunEquipAudioClip = await GetAudioClipFromFile(typeof(BroomShotgunPatch), 
                $"{WeaponManager.WeaponsAssetsBundlePath}\\EquipWeapon.mp3");
            shotgunShotAudioClip = await GetAudioClipFromFile(typeof(BroomShotgunPatch), 
                $"{WeaponManager.WeaponsAssetsBundlePath}\\ShotgunShot.mp3");
            shotgunPumpAudioClip = await GetAudioClipFromFile(typeof(BroomShotgunPatch), 
                $"{WeaponManager.WeaponsAssetsBundlePath}\\ShotgunPump.mp3");
        }

        public static void AddAudioSourceComponents(GameObject targetObject) {
            CreateAudioSourceComponent(targetObject, nameof(shotgunEquipAudio), shotgunEquipAudioClip, volume: 1f);
            CreateAudioSourceComponent(targetObject, nameof(shotgunShotAudio), shotgunShotAudioClip, volume: 0.34f);
            CreateAudioSourceComponent(targetObject, nameof(shotgunPumpAudio), shotgunPumpAudioClip, volume: 0.31f);
        }

        private static AudioSource CreateAudioSourceComponent(GameObject targetObj, string objName, AudioClip clip, float volume) {
            AudioSource audioSource = targetObj.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.clip.name = objName;
            audioSource.playOnAwake = false;
            audioSource.priority = 100;         //Lower value is higher priority. 128 is default.
            audioSource.spatialBlend = 1;       //Enable effect of the 3D engine on this audio
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = volume;
            audioSource.maxDistance = 150f;      //500 default
            audioSource.minDistance = 0.5f;
            //audioSource.pitch = 1.1f;			//Playback speed. Default 1.

            //Assign it to the same volume slider used for SFX.
            Transform hitSound = targetObj.transform.Find("HitSound");
            if (hitSound && hitSound.TryGetComponent(out AudioSource hitAudioSource) && hitAudioSource) {
                audioSource.outputAudioMixerGroup = hitAudioSource.outputAudioMixerGroup;
            }

            return audioSource;
        }

        public void PlayShootAudioFromPlayer(Transform playerSourceT) {
            AudioSource shotAudio = GetShotgunAudioSourceByName(nameof(shotgunShotAudio), playerSourceT);
            if (shotAudio) {
                shotAudio.Play();
            }
        }

        private static async Task<AudioClip> GetAudioClipFromFile(Type projectType, string pathFromModRoot) {
            string audioFilePath = AssemblyUtils.GetCombinedPathFromAssemblyFolder(projectType, pathFromModRoot);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioFilePath, AudioType.MPEG)) {
                await www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError
                        || www.result == UnityWebRequest.Result.ProtocolError) {
                    TimeLogger.Logger.LogError($"File in path \"{audioFilePath}\" " +
                        $"couldnt be loaded. The error message is: {www.error}", LogCategories.Loading);
                } else {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    if (string.IsNullOrEmpty(audioClip.name)) {
                        audioClip.name = System.IO.Path.GetFileNameWithoutExtension(pathFromModRoot);
                    }
                    return audioClip;
                }
            }
            return null;
        }

    }
}
