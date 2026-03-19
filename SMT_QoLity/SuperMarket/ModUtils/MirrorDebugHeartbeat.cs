using Damntry.Utils.Logging;
using Mirror;
using System.Threading.Tasks;

namespace SuperQoLity.SuperMarket.ModUtils {

    /// <summary>
    /// Tries to keep a client-to-host connection from disconnecting while debugging by sending periodic messages. 
    /// For when the disconnect time cant be set in the network transport being used (steamworks).
    /// NOTE: No matter what, debugging still stops all threads including this heartbeat. What this does is to
    /// make sure that a heartbeat is sent between debugging steps, but if you stay still in the same line of code
    /// for too long you will still get disconnected.
    /// </summary>
    public class MirrorDebugHeartbeat {

        private static bool IsConnectedAsClient;

        public static void Init() {
            WorldState.OnWorldLoaded += () => {
                IsConnectedAsClient = WorldState.CurrentOnlineMode == GameOnlineMode.Client;
                if (IsConnectedAsClient) {
                    StartHeartBeat();
                }
            };
            WorldState.OnQuitOrMainMenu += () => { 
                IsConnectedAsClient = false;
            };
        }

        private static void StartHeartBeat() {
            Task.Run(async () => {
                while (IsConnectedAsClient) {
                    //Send with time value that will make it get ignored by host
                    NetworkClient.Send(new NetworkPongMessage(double.MaxValue, 0d, 0d));
                    await Task.Delay(4000);
                }
            });
        }

    }
}
