using SuperQoLity.SuperMarket.ModUtils;
using SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.Model;
using UnityEngine;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.RadialWheel.Model {

    public delegate void CmdSpawnMethodDelegate(NetworkSpawner netSpawner,
            PlayerNetwork pNetwork, Vector3 dropPosition);

    /// <param name="index">Vanilla index associated with the equipment.</param>
    /// <param name="isRadialSpawnable">If it will be show in the radial equipment selection wheel.</param>
    /// <param name="toolGameObjects">Partial names of the GameObject to search for in each category.</param>
    /// <param name="iconUnityPath">Path of the icon inside the bundle.</param>
    /// <param name="cmdSpawnMethod">
    /// Method call used to spawn the item. Can be null, in which case no spawning will take place.
    /// The advantage of using a method is that this static class can be initialized early, and it
    /// doesnt matter that some used vars have not been initialized yet, since they will be by the 
    /// time the method gets executed, while allowing me to set the Vector3 parameters in a single spot.
    /// </param>
    /// <param name="displayName">
    /// Name of the tool, primarily for notifications. If empty, it will be taken from the index enum description.
    /// </param>
    public class ToolWheelDefinition(ToolIndexes index, bool isRadialSpawnable,
            PlayerPermissionsEnum requiredPermission, ToolGameObjects toolGameObjects, 
            string iconUnityPath, CmdSpawnMethodDelegate cmdSpawnMethod, string displayName = null) : 
                ToolDefinition(index, displayName){

        public bool IsRadialSpawnable { get; } = isRadialSpawnable;
        public PlayerPermissionsEnum RequiredPermission { get; } = requiredPermission;
        public ToolGameObjects ToolGameObjects { get; } = toolGameObjects;
        public string IconUnityPath { get; } = iconUnityPath;
        public CmdSpawnMethodDelegate CmdSpawnMethod { get; } = cmdSpawnMethod;

    }

    public record ToolGameObjects(string UsablePropName, string OrganizerName);


}
