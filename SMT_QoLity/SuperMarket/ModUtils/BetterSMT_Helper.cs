using System;
using BepInEx.Bootstrap;
using BepInEx;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Logging;

namespace SuperQoLity.SuperMarket.ModUtils {

	public static class BetterSMT_Helper {

		public static Lazy<BetterSMT_Status> BetterSMTLoaded { get; set; } = new Lazy<BetterSMT_Status>(() => ISBetterSMTLoaded());

		public static Lazy<bool> IsBetterSMTLoadedAndPatchEnabled { get; set; } = new Lazy<bool>(() => CheckBetterSMTLoadedAndPatchEnabled());


		public struct BetterSMTInfo {
			public const string GUID = "ViViKo.BetterSMT";
			public const string HarmonyId = "ViViKo.BetterSMT";
			public const string Name = "BetterSMT";
			public const string PatchesNamespace = "BetterSMT.Patches";
			public const string SupportedVersion = "1.6.2";
			public static string LoadedVersion;
		}

		public enum BetterSMT_Status {
			NotLoaded,
			DifferentVersion,
			LoadedOk
		}



		private static bool CheckBetterSMTLoadedAndPatchEnabled() {
			if (!ModConfig.Instance.EnablePatchBetterSMT_General.Value) {
				BepInExTimeLogger.Logger.LogTimeInfo($"{BetterSMTInfo.Name} mod patches are disabled in {MyPluginInfo.PLUGIN_NAME} config. Skipping.", TimeLoggerBase.LogCategories.Loading);
				return false;
			}

			return BetterSMTLoaded.Value != BetterSMT_Status.NotLoaded;
		}

		public static void BetterSMTStatusLog(BetterSMT_Status status) {

			switch (status) {
				case BetterSMT_Status.DifferentVersion:
					BepInExTimeLogger.Logger.LogTimeInfo($"Mod {BetterSMTInfo.Name} exists but its version ({BetterSMTInfo.LoadedVersion}) is not " +
						$"the supported version ({BetterSMTInfo.SupportedVersion}). This could cause problems and bugs in-game. " +
						$"You can disable these patches in \"{MyPluginInfo.PLUGIN_NAME}\" -> \"{ModConfig.Instance.EnablePatchBetterSMT_General.Definition.Section}\" section of the config.", TimeLoggerBase.LogCategories.Loading);
					break;
				case BetterSMT_Status.NotLoaded:
					BepInExTimeLogger.Logger.LogTime(TimeLoggerBase.LogTier.Message, $"Mod {BetterSMTInfo.Name} seems to be missing. Skipping its patches.", TimeLoggerBase.LogCategories.Loading);
					break;
				case BetterSMT_Status.LoadedOk:
					BepInExTimeLogger.Logger.LogTimeInfo($"Mod {BetterSMTInfo.Name} exists. {MyPluginInfo.PLUGIN_NAME} patches will be applied.", TimeLoggerBase.LogCategories.Loading);
					break;
				default:
					throw new NotImplementedException($"The switch case {status} is not implemented.");
			}
		}

		private static BetterSMT_Status ISBetterSMTLoaded() {
			bool betterSMTLoaded = Chainloader.PluginInfos.TryGetValue(BetterSMTInfo.GUID, out PluginInfo betterSMTInfo);

			if (betterSMTLoaded) {
				//Check loaded version against the one we support.
				BetterSMTInfo.LoadedVersion = betterSMTInfo.Metadata.Version.ToString();

				if (BetterSMTInfo.LoadedVersion != BetterSMTInfo.SupportedVersion) {
					return BetterSMT_Status.DifferentVersion;
				}

				return BetterSMT_Status.LoadedOk;
			}

			return BetterSMT_Status.NotLoaded;
		}

	}
}
