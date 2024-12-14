using System;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.ModHelpers;


namespace SuperQoLity.SuperMarket.ModUtils {

	public class ModInfoBetterSMT : ModInfoData {
		public ModInfoBetterSMT(ModInfoData modInfo) :
				base(modInfo.SupportedVersion) { }

		public new const string GUID = "BetterSMT";

		public new const string Name = "BetterSMT";

		public const string PatchesNamespace = "BetterSMT.Patches";

		public const string HarmonyId = "BetterSMT";

	}

	public class BetterSMT_Helper : ExternalModHelper {


		private static BetterSMT_Helper instance;
		public static BetterSMT_Helper Instance { get {
				if (instance == null) {
					instance = new BetterSMT_Helper(
							GUID: ModInfoBetterSMT.GUID,
							modName: ModInfoBetterSMT.Name,
							//TODO 2 - Move this into the AssemblyInfo.tt way of the Globals
							supportedVersion: new Version(MyPluginInfo.BETTERSMT_SUPPORTED_VERSION)
						);
				}
				return instance;
			}
		}

		private BetterSMT_Helper(string GUID, string modName, Version supportedVersion) : 
				base(GUID, modName, supportedVersion) {
			ModInfo = new ModInfoBetterSMT(base.ModInfo);
		}

		public new ModInfoBetterSMT ModInfo { get; private set; }


		public void LogCurrentBetterSMTStatus() {
			switch (ModStatus) {
				case ModLoadStatus.DifferentVersion:
					TimeLogger.Logger.LogTimeWarningShowInGame(GetDifferentVersionLogMessage(), TimeLogger.LogCategories.Loading);
					break;
				case ModLoadStatus.NotLoaded:
					TimeLogger.Logger.LogTimeMessage($"Mod {ModInfoBetterSMT.Name} seems to be missing. Skipping its patches.", TimeLogger.LogCategories.Loading);
					break;
				case ModLoadStatus.LoadedOk:
					TimeLogger.Logger.LogTimeInfo($"Mod {ModInfoBetterSMT.Name} exists. {MyPluginInfo.PLUGIN_NAME} patches will be applied if its setting is enabled.", TimeLogger.LogCategories.Loading);
					break;
				default:
					throw new NotImplementedException($"The switch case {ModStatus} is not implemented.");
			}
		}

		private string GetDifferentVersionLogMessage() {
			string versionDiff = ModInfo.LoadedVersion > ModInfo.SupportedVersion ? "higher" : "lower";

			string message = $"Mod {ModInfoBetterSMT.Name} exists but its version ({ModInfo.LoadedVersion}) is " +
						$"{versionDiff} than the supported version ({ModInfo.SupportedVersion}).\n";

			if (ModInfo.LoadedVersion < ModInfo.SupportedVersion) {
				message += $"It is recommended to upgrade {ModInfoBetterSMT.Name}, at least to the supported version. Otherwise, this ";
			} else if (ModInfo.LoadedVersion > ModInfo.SupportedVersion) {
				message += "This ";
			}

			message += $"could cause problems and bugs in-game. If you encounter any errors, you can " +
							$"disable these patches in the \"{MyPluginInfo.PLUGIN_NAME}\" -> " +
							$"\"{ModConfig.Instance.EnablePatchBetterSMT_ExtraHighlightFunctions.Definition.Section}\" " +
							$"section of the config.";

			return message;
		}

	}
}
