using Damntry.Utils.ExtensionMethods;

namespace SuperQoLity.SuperMarket.PatchClassHelpers.Equipment.Model {

    /// <param name="index">Vanilla index associated with the equipment.</param>
    /// <param name="displayName">
    /// Name of the tool, primarily for notifications. If empty, it will be taken from the index enum description.
    /// </param>
    public class ToolDefinition(ToolIndexes index, string displayName = null) {

        public ToolIndexes Index { get; } = index;
        public string DisplayName { get; } = displayName ?? index.GetDescription();


        //public static implicit operator int(ToolDefinition t) => t.index;
    }

}
