using System;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.ModHelpers;


namespace SuperQoLity.SuperMarket.ModUtils {

	public class ModInfoSMTAntiCheat : ModInfoData {
		public ModInfoSMTAntiCheat(ModInfoData modInfo) :
				base(modInfo.SupportedVersion) { }

		public new const string GUID = "ika.smtanticheat";

		public new const string Name = "Ika SMT Anti Cheat";

	}

	public class SMTAntiCheat_Helper : ExternalModHelper {

		private static SMTAntiCheat_Helper instance;
		public static SMTAntiCheat_Helper Instance {
			get {
				if (instance == null) {
					instance = new SMTAntiCheat_Helper(
							GUID: ModInfoSMTAntiCheat.GUID,
							modName: ModInfoSMTAntiCheat.Name,
							//TODO 2 - Move this into the AssemblyInfo.tt way of the Globals
							supportedVersion: new Version("0.0.1")
						);
				}
				return instance;
			}
		}

		private SMTAntiCheat_Helper(string GUID, string modName, Version supportedVersion) :
				base(GUID, modName, supportedVersion) {
			ModInfo = new ModInfoSMTAntiCheat(base.ModInfo);
		}

		public new ModInfoSMTAntiCheat ModInfo { get; private set; }

		/// <summary>
		/// When the anticheat mod is loaded, change the method used to add funds to call the one Ika is using.
		/// </summary>
		public void CmdAlterFunds(float funds) {
			if (IsModLoadedAndEnabled) {
				ReflectionHelper.CallMethod(GameData.Instance, "UserCode_CmdAlterFunds__Single");
			} else {
				GameData.Instance.CmdAlterFunds(funds);
			}
		}

	}
}
