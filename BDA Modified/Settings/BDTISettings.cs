using System.IO;

namespace BDArmory.Settings
{
	public class BDTISettings
	{
		public static string settingsConfigURL = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/settings.cfg");

		[SettingsDataField] public static bool TEAMICONS = true;
		[SettingsDataField] public static bool SHOW_SELF = false;
		[SettingsDataField] public static bool TEAMNAMES = false;
		[SettingsDataField] public static bool VESSELNAMES = true;
		[SettingsDataField] public static bool SCORE = false;
		[SettingsDataField] public static bool HEALTHBAR = false;
		[SettingsDataField] public static bool THREATICON = false;
		[SettingsDataField] public static bool MISSILES = false;
		[SettingsDataField] public static bool MISSILE_TEXT = true;
		[SettingsDataField] public static bool DEBRIS = true;
		[SettingsDataField] public static bool PERSISTANT = true;
		[SettingsDataField] public static bool POINTERS = true;
		[SettingsDataField] public static bool TELEMETRY = false;
		[SettingsDataField] public static float ICONSCALE = 1.0f;
		[SettingsDataField] public static float OPACITY = 0.5f;
		[SettingsDataField] public static float DISTANCE_THRESHOLD = 100f;
		[SettingsDataField] public static float MAX_DISTANCE_THRESHOLD = 0f;
	}
}
