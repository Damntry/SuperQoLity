using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;
using Damntry.UtilsUnity.ExtensionMethods;
using HutongGames.PlayMaker;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment;
using SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Weapons {

    public enum ShotgunStatus {
        Unequipped,
        EquipAnimation,
        EquipReady,
        FireAnimation,
        IdleCooldown,
        None,
    }

    public class WeaponManager {

        public const string WeaponsAssetsBundlePath = "Assets\\Weapon";


        private static WeaponManager instance;

        public static ShotgunStatus LocalShotgunStatus {
            get => WorldState.IsWorldLoaded ? BroomShotgunNetwork.LocalInstance.ShotgunCurrentStatus : ShotgunStatus.None;
            private set => BroomShotgunNetwork.LocalInstance.ShotgunCurrentStatus.Value = value;
        }

        private readonly static float VanillaGlobalInteractionRange = 2.5f;


        private static CustomCameraController customCamControl;


        private PlayerNetwork playerNetwork;

        private readonly WeaponLogic weaponLogic;
        
        private readonly WeaponAudioSystem audioSystem;

        private readonly WeaponAnimationSystem animationSystem;

        private float nextShotAvailableTime;

        private FsmFloat interactionDistanceFSM;


        public WeaponManager() {
            customCamControl = null;
            instance = this;

            weaponLogic = new();
            audioSystem = new();
            animationSystem = new();

            TargetObjectResolver.ResetCache();
        }


        /// <summary>
        /// Returns if the shotgun state associated to a player is holding the broom and aiming.
        /// </summary>
        public static bool IsPlayerInShotgunmode(BroomShotgunNetwork playerShotgunNetwork) =>
            IsBroomEquipped(playerShotgunNetwork.GetComponent<PlayerNetwork>()) &&
            playerShotgunNetwork.ShotgunCurrentStatus != ShotgunStatus.None &&
                playerShotgunNetwork.ShotgunCurrentStatus != ShotgunStatus.Unequipped;


        public static bool CanUseWeapon() {
            if (!BroomShotgunNetwork.ShotgunModuleEnabledSync || !GetCameraController()) {
                return false;
            }

            return !customCamControl.inEmoteEvent && !customCamControl.IsInOptions;
        }

        public static bool CanAimWeapon() =>
            !customCamControl.isInCameraEvent && !customCamControl.inVehicle;

        private static bool GetCameraController() {
            if (!customCamControl) {
                customCamControl = Camera.main.NullableObject()?.GetComponent<CustomCameraController>();
            }

            return customCamControl;
        }

        public static bool IsBroomEquipped(PlayerNetwork pNetwork = null) =>
            (pNetwork ?? SMTInstances.LocalPlayerNetwork()).equippedItem == (int)ToolIndexes.Broom;


        public void Destroy() {
            LocalShotgunStatus = ShotgunStatus.None;
            WorldState.OnGamePauseChanged -= OnGamePauseChanged;
            weaponLogic.Destroy();
        }

        public void InitializeShotgunData() {
            LocalShotgunStatus = ShotgunStatus.Unequipped;

            GameObject fpController = SMTInstances.FirstPersonController().gameObject;
            audioSystem.Initialize(fpController);
            animationSystem.Initialize(fpController);

            interactionDistanceFSM = GetGlobalInteractionDistanceFloat();

            WorldState.OnGamePauseChanged += OnGamePauseChanged;

            //Ignore invisible Base_Collider walls
            RemoveBaseCollidersRaycast();
        }

        private void RemoveBaseCollidersRaycast() {
            GameObject baseColliders = GameObject.Find("Level_Exterior/Colliders");
            if (!baseColliders) {
                TimeLogger.Logger.LogError("The base collider object couldnt be found. Shotgun " +
                    "pellets will stop at the invisible wall.", LogCategories.Other);
                return;
            }

            foreach (Transform baseCollider in baseColliders.transform) {
                baseCollider.gameObject.layer = (int)SMT_Layers.IgnoreRaycast;
            }
        }

        private FsmFloat GetGlobalInteractionDistanceFloat() {
            PlayMakerFSM[] fsmChildren = SMTInstances.FirstPersonController().NullableObject()?
                .transform.Find("ExtraLocalBehaviours").NullableObject()?
                .GetComponents<PlayMakerFSM>();

            if (fsmChildren != null && fsmChildren.Length > 0) {
                foreach (PlayMakerFSM fsm in fsmChildren) {
                    if (fsm.FsmName == "Interact_behaviour") {
                        return fsm.FsmVariables.FindFsmFloat("InteractDistance");
                    }
                }
            }

            return null;
        }

        private void OnGamePauseChanged(bool isPaused, bool isMainMenuOpen) {
            //Pause all AudioSources when game is paused, and continue on resume.
            if (isPaused) {
                audioSystem.SetPlaybackState(SoundBite.All, AudioAction.Pause);
            } else {
                audioSystem.SetPlaybackState(SoundBite.All, AudioAction.UnPause);
            }
        }

        public void ToggleShotgunMode() {
            playerNetwork ??= SMTInstances.LocalPlayerNetwork();

            if (!playerNetwork || !CanUseWeapon()) {
                if (LocalShotgunStatus == ShotgunStatus.Unequipped || LocalShotgunStatus == ShotgunStatus.None) {
                    if (IsBroomEquipped(playerNetwork) && WorldState.CurrentOnlineMode == GameOnlineMode.Client && 
                            BroomShotgunNetwork.ShotgunModuleEnabledSync.Status == EnableStatus.LocallyOnly) {

                        TimeLogger.Logger.SendMessageNotification(LogTier.Message, $"Broom shotgun is " +
                            $"disabled. Host does not have SuperQoLity or this feature enabled", skipQueue: true);
                    }
                } else {
                    LeaveShotgunAimMode();
                }

                return;
            }

            if (IsBroomEquipped(playerNetwork)) {
                if (LocalShotgunStatus == ShotgunStatus.Unequipped) {
                    //Extra check, since without security permission it shouldnt even be holding a broom.
                    if (!HostPermissions.HasPermission(PlayerPermissionsEnum.Security)) {
                        return;
                    }

                    EnterShotgunAimMode();
                    return;
                }
            }

            LeaveShotgunAimMode();
        }

        private void EnterShotgunAimMode() {
            if (!CanUseWeapon() || !CanAimWeapon()) {
                return;
            }

            audioSystem.SetPlaybackState(SoundBite.Equip, AudioAction.Play);

            LocalShotgunStatus = ShotgunStatus.EquipAnimation;

            animationSystem.StartShotgunAimAnimation(playerNetwork, 
                onComplete: () => {
                    LocalShotgunStatus = ShotgunStatus.EquipReady;
                });

            if (interactionDistanceFSM != null) {
                //Remove melee and container interactions while in broom aim mode. The idea was to
                //  only disable melee brooming, but since we are dealing with FSM I would have to
                //  add new states and actions, and fuck that.
                interactionDistanceFSM.Value = 0f;
            }
        }


        public void LeaveShotgunAimMode() {
            if (LocalShotgunStatus == ShotgunStatus.Unequipped) {
                return;
            }

            LocalShotgunStatus = ShotgunStatus.Unequipped;

            if (interactionDistanceFSM != null) {
                interactionDistanceFSM.Value = VanillaGlobalInteractionRange;
            }

            //Stop any ongoing animations
            animationSystem.CancelAnimations();

            audioSystem.SetPlaybackState(SoundBite.All, AudioAction.Stop);
        }

        public void LocalPlayerFire() {
            if (!CanUseWeapon() || !HostPermissions.HasPermission(PlayerPermissionsEnum.Security)) {
                return;
            }

            if (LocalShotgunStatus == ShotgunStatus.IdleCooldown && nextShotAvailableTime <= Time.time) {
                LocalShotgunStatus = ShotgunStatus.EquipReady;
            }

            if (LocalShotgunStatus != ShotgunStatus.EquipReady) {
                return;
            }

            LocalShotgunStatus = ShotgunStatus.FireAnimation;

            nextShotAvailableTime = Time.time + WeaponLogic.ShotgunDefinition.RoundsPerSecond;

            weaponLogic.BeginLocalPlayerFire(out GameObject broomObj);

            audioSystem.SetPlaybackState(SoundBite.Shoot, AudioAction.Play);

            animationSystem.StartShotgunRecoilAnimation(broomObj, 
                onComplete: () => {
                    LocalShotgunStatus = ShotgunStatus.IdleCooldown;
                    audioSystem.SetPlaybackState(SoundBite.Pump, AudioAction.Play);
                    animationSystem.SpawnShotgunShell(playerNetwork);
                });
        }

        public static void RemotePlayerFire(Vector3 endPoint, uint playerSourceNetid, FireNetworkData[] fireNetDataArray) {
            //Depending on network delay, there will be a disconnect between what the local player sees and
            //  where entities are for remote players. If we only used the remote endpoint, a local player might 
            //  see tracers going nowhere, and still watch the effect of a target getting shot, which is jarring.
            //To avoid this, if a target got shot the tracers will be generated using a local end point calculated
            //  from the target/s hit, instead of the remote end point.

            PlayerNetwork playerSource;
            if (TargetObjectResolver.FindTargetObjectByNetid(playerSourceNetid, TargetType.Player, out Transform playerSourceT)) {
                playerSource = playerSourceT.GetComponent<PlayerNetwork>();
            } else {
                return;
            }

            if (!playerSource) {
                TimeLogger.Logger.LogError($"The GameObject {playerSource} does not " +
                    $"contain a PlayerNetwork component", LogCategories.Other);
                return;
            }

            Vector3 targetPoint = endPoint;

            if (fireNetDataArray.Length == 1) {
                //Get center point of target (and a bit of added offset), to simulate tracers hitting it
                if (TargetObjectResolver.GetTargetAdjustedPosition(fireNetDataArray[0], out Vector3 position)) {
                    targetPoint = position;
                }
            } else if (fireNetDataArray.Length > 1) {
                //Calculage averages to get the center point between all hit targets.
                Vector3 centerPoint = Vector3.zero;

                foreach (FireNetworkData fireNetData in fireNetDataArray) {
                    if (TargetObjectResolver.GetTargetAdjustedPosition(fireNetData, out Vector3 position)) {
                        centerPoint += position;
                    }
                }

                targetPoint = centerPoint / fireNetDataArray.Length;
            }

            instance.weaponLogic.BeginRemotePlayerFire(playerSource, targetPoint);

            instance.audioSystem.PlayShootAudioFromPlayer(playerSourceT);
        }

    }
}
