using System;
using BepInEx.Bootstrap;
using BepInEx;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;
using System.Text;

namespace SuperQoLity.SuperMarket.ModUtils {

	public static class BetterSMT_Helper {

		public static Lazy<BetterSMT_Status> BetterSMTLoadStatus { get; private set; } = new Lazy<BetterSMT_Status>(() => ISBetterSMTLoaded());

		public static Lazy<bool> IsBetterSMTLoadedAndPatchEnabled { get; private set; } = new Lazy<bool>(() => CheckBetterSMTLoadedAndPatchEnabled());

		public static Lazy<bool> IsEnableBetterSMTHightlightFixes { get; private set; } = new Lazy<bool>(
			() => IsBetterSMTLoadedAndPatchEnabled.Value && BetterSMTInfo.LoadedVersion <= BetterSMTInfo.LastVivikoVersion &&
			//If extra highlights functions are enabled, we instead use my own patch with modified methods that already implement the fixes.
			!ModConfig.Instance.EnablePatchBetterSMT_ExtraHighlightFunctions.Value
		);


		public struct BetterSMTInfo {
			public const string GUID = "ViViKo.BetterSMT";
			public const string HarmonyId = "ViViKo.BetterSMT";
			public const string Name = "BetterSMT";
			public const string PatchesNamespace = "BetterSMT.Patches";
			//Last Viviko version is also the last one that needed the highlight fixes.
			public readonly static Version LastVivikoVersion = new Version(1, 6, 2);

			//TODO 5 - This probably belongs in MyPlugin.info.
			public readonly static Version SupportedVersion = new Version(1, 6, 3);

			public static Version LoadedVersion;
		}


		public enum BetterSMT_Status {
			NotLoaded,
			DifferentVersion,
			LoadedOk
		}


		public static void LogCurrentBetterSMTStatus() {
			switch (BetterSMTLoadStatus.Value) {
				case BetterSMT_Status.DifferentVersion:
					BepInExTimeLogger.Logger.LogTimeWarningShowInGame(GetDifferentVersionLogMessage(), TimeLoggerBase.LogCategories.Loading);
					break;
				case BetterSMT_Status.NotLoaded:
					BepInExTimeLogger.Logger.LogTime(TimeLoggerBase.LogTier.Message, $"Mod {BetterSMTInfo.Name} seems to be missing. Skipping its patches.", TimeLoggerBase.LogCategories.Loading);
					break;
				case BetterSMT_Status.LoadedOk:
					BepInExTimeLogger.Logger.LogTimeInfo($"Mod {BetterSMTInfo.Name} exists. {MyPluginInfo.PLUGIN_NAME} patches will be applied.", TimeLoggerBase.LogCategories.Loading);
					break;
				default:
					throw new NotImplementedException($"The switch case {BetterSMTLoadStatus.Value} is not implemented.");
			}
		}

		private static string GetDifferentVersionLogMessage() {
			string versionDiff = BetterSMTInfo.LoadedVersion > BetterSMTInfo.SupportedVersion ? "higher" : "lower";

			string message = $"Mod {BetterSMTInfo.Name} exists but its version ({BetterSMTInfo.LoadedVersion}) is " +
						$"{versionDiff} than the supported version ({BetterSMTInfo.SupportedVersion}).\n";

			if (BetterSMTInfo.LoadedVersion < BetterSMTInfo.SupportedVersion) {

				if (BetterSMTInfo.LoadedVersion <= BetterSMTInfo.LastVivikoVersion) {
					message += "A new version of the BetterSMT mod was released by Seiko on the Thunderstore mod." +
						"It is recommended that you to switch to it since the old one wont be updated anymore. ";
				} else {
					message += "It is recommended to upgrade BetterSMT, at least to the supported version. ";
				}
				message += "Otherwise, this ";
			} else if (BetterSMTInfo.LoadedVersion > BetterSMTInfo.SupportedVersion) {
				message += $"This ";
			}

			message += $"could cause problems and bugs in-game. If you encounter any errors, you can " +
							$"disable these patches in the \"{MyPluginInfo.PLUGIN_NAME}\" -> " +
							$"\"{ModConfig.Instance.EnablePatchBetterSMT_General.Definition.Section}\" " +
							$"section of the config.";

			return message;
		}

		private static bool CheckBetterSMTLoadedAndPatchEnabled() {
			if (!ModConfig.Instance.EnablePatchBetterSMT_General.Value) {
				BepInExTimeLogger.Logger.LogTimeInfo($"{BetterSMTInfo.Name} mod patches are disabled in {MyPluginInfo.PLUGIN_NAME} config. Skipping.", TimeLoggerBase.LogCategories.Loading);
				return false;
			}

			return BetterSMTLoadStatus.Value != BetterSMT_Status.NotLoaded;
		}

		private static BetterSMT_Status ISBetterSMTLoaded() {
			bool betterSMTLoaded = Chainloader.PluginInfos.TryGetValue(BetterSMTInfo.GUID, out PluginInfo betterSMTInfo);

			if (betterSMTLoaded) {
				//Check loaded version against the one we support.
				BetterSMTInfo.LoadedVersion = betterSMTInfo.Metadata.Version;

				if (BetterSMTInfo.LoadedVersion != BetterSMTInfo.SupportedVersion) {
					return BetterSMT_Status.DifferentVersion;
				}

				return BetterSMT_Status.LoadedOk;
			}

			return BetterSMT_Status.NotLoaded;
		}

	}
}
