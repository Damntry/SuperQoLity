using Damntry.Utils.Logging;
using Damntry.UtilsMirror.Attributes;
using Damntry.UtilsMirror.Components;
using Damntry.UtilsMirror.Helpers;
using Damntry.UtilsMirror.SyncVar;
using Mirror;
using Mirror.RemoteCalls;
using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons;
using SuperQoLity.SuperMarket.PatchClassHelpers.Weapons.Helpers;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Networking.SyncVarBehaviours {

	public class BroomShotgunNetwork : SyncVarNetworkBehaviour<BroomShotgunNetwork> {

        public static uint NetworkAssetId => 102201;

        public static BroomShotgunNetwork LocalInstance { get; private set; }


        [SyncVarNetwork]
		public static BoolSyncVarSetting ShotgunModuleEnabledSync { get; private set; }

        [SyncVarNetwork]
        public SyncVar<ShotgunStatus> ShotgunCurrentStatus { get; set; }


        /// <summary>
        /// Since I need to create more than one instance of this network object, one for each player, and
        ///     I dont support this behaviour, instead in BroomShotgunPatch.OnBeforeSpawn I modify the original 
        ///     player prefab before its spawned, so the game itself initializes the netIdentity as if
        ///     this was an original component.
        /// </summary>
        static BroomShotgunNetwork() {
            //As a client, the broom will only work as a shotgun if the host has the mod with this setting active too.
            ShotgunModuleEnabledSync = new(defaultValue: false, ModConfig.Instance.BroomShotgunModeEnabled);

            RemoteProcedureCalls.RegisterCommand(typeof(BroomShotgunNetwork), nameof(CmdPlayerFire), InvokeUserCode_CmdPlayerFire, requiresAuthority: true);
            RemoteProcedureCalls.RegisterRpc(typeof(BroomShotgunNetwork), nameof(RpcPlayerFire), InvokeUserCode_RpcPlayerFire);

            //Register custom network reader/writers
            Writer<FireNetworkData[]>.write = (writer, value) => writer.WriteFireNetworkDataArray(value);
            Reader<FireNetworkData[]>.read = (reader) => reader.ReadFireNetworkDataArray();
        }
        
        public BroomShotgunNetwork() {
            ShotgunCurrentStatus = new(defaultValue: ShotgunStatus.None);
        }

        protected override void Awake() {
            StartNetworkSession(NetworkSpawnManager.GetCurrentNetworkMode());

            base.Awake();
        }

        public override void OnStartClient() {
            if (isLocalPlayer) {
                //Convenience instance shortcut to use elsewhere.
                LocalInstance = this;
                syncDirection = WorldState.CurrentOnlineMode == GameOnlineMode.Host 
                    ? SyncDirection.ServerToClient : SyncDirection.ClientToServer;
            } else {
                syncDirection = WorldState.CurrentOnlineMode == GameOnlineMode.Host
                    ? SyncDirection.ClientToServer : SyncDirection.ServerToClient;
            }

            base.OnStartClient();
        }


        public void CmdPlayerFire(Vector3 endPoint, uint playerSourceNetid, FireNetworkData[] fireNetData) {
            CmdCall(nameof(CmdPlayerFire), requiresAuthority: true, endPoint, playerSourceNetid, fireNetData);
        }

        protected static void InvokeUserCode_CmdPlayerFire(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection) {
            if (!NetworkServer.active) {
                TimeLogger.Logger.LogError($"Command {nameof(CmdPlayerFire)} called on client.", LogCategories.Network);
            } else {
                ((BroomShotgunNetwork)obj).UserCode_CmdPlayerFire(reader.ReadVector3(), reader.ReadUInt(), reader.Read<FireNetworkData[]>());
            }
        }

        protected void UserCode_CmdPlayerFire(Vector3 endPoint, uint playerSourceNetid, FireNetworkData[] fireNetData) {
            RpcPlayerFire(endPoint, playerSourceNetid, fireNetData);
        }


        public void RpcPlayerFire(Vector3 endPoint, uint playerSourceNetid, FireNetworkData[] fireNetData) {
            RpcCall(nameof(RpcPlayerFire), includeOwner: false, endPoint, playerSourceNetid, fireNetData);
        }

        protected static void InvokeUserCode_RpcPlayerFire(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection) {
            ((BroomShotgunNetwork)obj).UserCode_RpcPlayerFire(reader.ReadVector3(), reader.ReadUInt(), reader.Read<FireNetworkData[]>());
        }

        protected void UserCode_RpcPlayerFire(Vector3 endPoint, uint playerSourceNetid, FireNetworkData[] fireNetData) {
            PlayerFire(endPoint, playerSourceNetid, fireNetData);
        }

        private void PlayerFire(Vector3 endPoint, uint playerSourceNetid, FireNetworkData[] fireNetData) {
            WeaponManager.RemotePlayerFire(endPoint, playerSourceNetid, fireNetData);
        }

    }

    public static class CustomNetworkDataTransfer {

        public static void WriteFireNetworkData(this NetworkWriter writer, FireNetworkData value) {
            writer.WriteUInt(value.TargetNetid);
            writer.WriteInt((int)value.TargetType);
        }

        public static FireNetworkData ReadFireNetworkData(this NetworkReader reader) {
            return new FireNetworkData(reader.ReadUInt(), (TargetType)reader.ReadInt());
        }

        public static void WriteFireNetworkDataArray(this NetworkWriter writer, FireNetworkData[] value) {
            writer.WriteInt(value.Length);
            for (int i = 0; i < value.Length; i++) {
                writer.WriteFireNetworkData(value[i]);
            }
        }

        public static FireNetworkData[] ReadFireNetworkDataArray(this NetworkReader reader) {
            int num = reader.ReadInt();
            
            FireNetworkData[] array = new FireNetworkData[num];
            for (int i = 0; i < num; i++) {
                array[i] = reader.ReadFireNetworkData();
            }

            return array;
            
        }

    }

}
