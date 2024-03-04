using System.Linq;
using System.Reflection;
using UnityEngine;

using System.IO;
using System.Collections.Generic;
using System;

namespace BDArmory.Settings
{
  public class RWPSettings
  {
    // Settings that are set when the RWP toggle is enabled or the slider is moved.
    // Further adjustments to the settings in-game are allowed, but do not get saved to the RWP_settings.cfg file.
    // This should avoid the need for many "if (SETTING || (RUNWAY_PROJECT && RUNWAY_PROJECT_ROUND == xx))" checks, though its main purpose is for resetting settings back to what they should be when toggling the RWP toggle / adjusting the RWP slider.

    static readonly string RWPSettingsPath = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/RWP_settings.cfg"));
    static bool RWPEnabled = false;
    static readonly Dictionary<int, Dictionary<string, object>> RWPOverrides = new()
    {
      {0, new(){ // Global RWP settings.
        {"AUTONOMOUS_COMBAT_SEATS", false},
        {"DESTROY_UNCONTROLLED_WMS", true},
        {"DISABLE_RAMMING", false},
        {"HACK_INTAKES", true},
        {"HP_THRESHOLD", 2000},
        {"INFINITE_AMMO", false},
        {"INFINITE_ORDINANCE", false}, // Note: don't set inf fuel or inf EC as those are used during autotuning and are handled differently in order to sync with the cheats menu.
        {"PWING_THICKNESS_AFFECT_MASS_HP", true},
        {"VESSEL_SPAWN_FILL_SEATS", 1},
        {"VESSEL_SPAWN_RANDOM_ORDER", true},
        {"VESSEL_SPAWN_REASSIGN_TEAMS", true},
        {"OUT_OF_AMMO_KILL_TIME", 60},
      }},
      // Round specific overrides (also overrides global settings).
      {42, new(){ // Fly the Unfriendly Skies
        {"VESSEL_SPAWN_FILL_SEATS", 3},
      }},
      {46, new(){ // Vertigo to Jool
        {"NO_ENGINES", true},
      }},
      {50, new(){ // Mach-ing Bird
        {"WAYPOINTS_MODE", true},
      }},
      {55, new(){ // Boonta Eve Classic
        {"WAYPOINTS_MODE", true},
      }},
      {60, new(){ // Legacy of the Void
        {"SPACE_HACKS", true},
        {"SF_FRICTION", true},
        {"SF_GRAVITY", true},
        {"SF_DRAGMULT", 30},
        {"MAX_SAS_TORQUE", 10},
      }},
      {61, new(){ // Gun-Game
        {"MUTATOR_MODE", true},
        {"MUTATOR_DURATION", 0},
        {"MUTATOR_APPLY_KILL", true},
        {"MUTATOR_APPLY_GUNGAME", true},
        {"MUTATOR_APPLY_GLOBAL", false},
        {"MUTATOR_APPLY_TIMER", false},
        {"MUTATOR_APPLY_NUM", 1},
        {"MUTATOR_ICONS", true},
        {"GG_PERSISTANT_PROGRESSION", false},
        {"GG_CYCLE_LIST", false},
        {"MUTATOR_LIST", new List<string>{ "Brownings", "Chainguns", "Vulcans", "Mausers", "GAU-22s", "N-37s", "AT Guns", "Railguns", "GAU-8s", "Rockets" }},
      }},
      {62, new(){ // Retrofuturistic on Eve
        {"ASTEROID_FIELD", true},
        {"ASTEROID_FIELD_ALTITUDE", 20},
        {"ASTEROID_FIELD_ANOMALOUS_ATTRACTION", true},
        {"ASTEROID_FIELD_ANOMALOUS_ATTRACTION_STRENGTH", 0.5},
        {"ASTEROID_FIELD_NUMBER", 200},
        {"ASTEROID_FIELD_RADIUS", 7},
        {"VESSEL_SPAWN_CS_FOLLOWS_CENTROID", false},
        {"VESSEL_SPAWN_WORLDINDEX", 5}, // Eve
        {"VESSEL_SPAWN_GEOCOORDS", new Vector2d(33.3616, -67.2242)}, // Poison Pond
        {"VESSEL_SPAWN_ALTITUDE", 2500},
      }
      }
    };
    public static Dictionary<int, int> RWPRoundToIndex = new() { { 0, 0 } }, RWPIndexToRound = new() { { 0, 0 } }; // Helpers for the UI slider.

    public static void SetRWP(bool enabled, int round)
    {
      if (enabled)
      {
        if (!RWPEnabled) StoreSettings(); // Enabling RWP. Store the non-RWP settings.
        else RestoreSettings(); // Was previously enabled. Restore the non-RWP settings before applying new ones.
        SetOverrides(0); // Set global RWP settings.
        SetOverrides(round); // Set the round-specific settings.
      }
      else if (!enabled && RWPEnabled) // Disabling RWP.
      {
        RestoreSettings(); // Restore the non-RWP settings.
      }
      RWPEnabled = enabled;
    }

        static Dictionary<string, object> storedSettings = new Dictionary<string, object>(); // The base stored settings.
        static Dictionary<string, object> tempSettings = new Dictionary<string, object>(); // Temporary settings for shuffling things around.

        /// <summary>
        /// Store the non-RWP settings.
        /// </summary>
        public static void StoreSettings(bool temp = false)
    {
      Debug.Log($"[BDArmory.RWPSettings]: Storing {(temp ? "temporary" : "base")} settings.");
      var settings = temp ? tempSettings : storedSettings;
      settings.Clear();
      var fields = typeof(BDArmorySettings).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
      foreach (var field in fields)
      {
        if (field.Name.EndsWith("_SETTINGS_TOGGLE")) continue; // Don't toggle the section headers.
        if (field.Name.StartsWith("RUNWAY_PROJECT")) continue; // Skip settings beginning with RWP (toggle and slider) to avoid recursion.
        if (field.Name.StartsWith("VESSEL_SPAWN_")) continue; // Skip spawn settings so they are the same between RWP and non-RWP.
        settings.Add(field.Name, field.GetValue(null));
      }
      if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.RWPSettings]: Stored settings: " + string.Join(", ", settings.Select(kvp => $"{kvp.Key}={kvp.Value}")));
    }

    /// <summary>
    /// Restore the non-RWP settings.
    /// </summary>
    public static void RestoreSettings(bool temp = false)
    {
      Debug.Log($"[BDArmory.RWPSettings]: Restoring {(temp ? "temporary" : "base")} settings.");
      var settings = temp ? tempSettings : storedSettings;
      foreach (var setting in settings.Keys)
      {
        var field = typeof(BDArmorySettings).GetField(setting, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (field == null) continue;
        field.SetValue(null, settings[setting]);
      }
      if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.RWPSettings]: Restored settings: " + string.Join(", ", settings.Select(kvp => $"{kvp.Key}={kvp.Value}")));
    }

    /// <summary>
    /// Override settings based on the RWP overrides.
    /// </summary>
    /// <param name="round">The RWP round number.</param>
    public static void SetOverrides(int round)
    {
      if (!RWPOverrides.ContainsKey(round)) return;
      Debug.Log($"[BDArmory.RWPSettings]: Setting overrides for RWP round {round}");
      var overrides = RWPOverrides[round];
      foreach (var setting in overrides.Keys)
      {
        var field = typeof(BDArmorySettings).GetField(setting, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        if (field == null)
        {
          Debug.LogWarning($"[BDArmory.RWPSettings]: Invalid field name {setting} for RWP round {round}.");
          continue;
        }
        field.SetValue(null, overrides[setting]);
      }

      // Add any additional round-specific setup here.
      // Note: Anything called from here has to be capable of being called during BDArmorySetup's Awake function.
      switch (round)
      {
        case 61:
          GameModes.MutatorInfo.SetupGunGame();
          break;
      }
    }

    /// <summary>
    /// Save RWP settings to file.
    /// 
    /// Currently, this will save the RWPOverrides, but we don't yet have a way to update the RWPOverrides in-game.
    /// For now, just manually edit the RWP_settings.cfg and reload the settings.
    /// </summary>
    public static void Save()
    {
      ConfigNode fileNode = ConfigNode.Load(RWPSettingsPath) ?? new ConfigNode();

      if (!fileNode.HasNode("RWPSettings"))
      {
        fileNode.AddNode("RWPSettings");
      }

      var settingsNode = fileNode.GetNode("RWPSettings");
      foreach (var round in RWPOverrides.Keys)
      {
        if (!settingsNode.HasNode(round.ToString())) settingsNode.AddNode(round.ToString());
        var roundNode = settingsNode.GetNode(round.ToString());
        foreach (var fieldName in RWPOverrides[round].Keys)
        {
          var field = typeof(BDArmorySettings).GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
          if (field == null)
          {
            Debug.LogWarning($"[BDArmory.RWPSettings]: Invalid field name {fieldName} for RWP round {round}.");
            continue;
          }
          var fieldValue = RWPOverrides[round][fieldName];
          try
          {
            if (fieldValue.GetType() == typeof(Vector3d))
            {
              roundNode.SetValue(field.Name, ((Vector3d)fieldValue).ToString("G"), true);
            }
            else if (fieldValue.GetType() == typeof(Vector2d))
            {
              roundNode.SetValue(field.Name, ((Vector2d)fieldValue).ToString("G"), true);
            }
            else if (fieldValue.GetType() == typeof(Vector2))
            {
              roundNode.SetValue(field.Name, ((Vector2)fieldValue).ToString("G"), true);
            }
            else if (fieldValue.GetType() == typeof(List<string>))
            {
              roundNode.SetValue(field.Name, string.Join("; ", (List<string>)fieldValue), true);
            }
            else
            {
              roundNode.SetValue(field.Name, fieldValue.ToString(), true);
            }
          }
          catch (Exception e)
          {
            Debug.LogError($"[BDArmory.RWPSettings]: Exception triggered while trying to save field {fieldName} with value {fieldValue}: {e.Message}");
          }
        }
      }
      fileNode.Save(RWPSettingsPath);
    }

    /// <summary>
    /// Load RWP settings from file.
    /// </summary>
    public static void Load()
    {
      // Set up the RWP round indices in case no settings get loaded.
      var sortedRoundNumbers = RWPOverrides.Keys.ToList(); sortedRoundNumbers.Sort();
      RWPRoundToIndex = sortedRoundNumbers.ToDictionary(r => r, sortedRoundNumbers.IndexOf);
      RWPIndexToRound = sortedRoundNumbers.ToDictionary(sortedRoundNumbers.IndexOf, r => r);

      if (!File.Exists(RWPSettingsPath)) return;
      ConfigNode fileNode = ConfigNode.Load(RWPSettingsPath);
      if (!fileNode.HasNode("RWPSettings")) return;

      ConfigNode settings = fileNode.GetNode("RWPSettings");

      foreach (var roundNode in settings.GetNodes())
      {
        int round = int.Parse(roundNode.name);
                if (!RWPOverrides.ContainsKey(round))
                    RWPOverrides.Add(round, new Dictionary<string, object>());
                foreach (ConfigNode.Value fieldNode in roundNode.values)
        {
          var field = typeof(BDArmorySettings).GetField(fieldNode.name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
          if (field == null)
          {
            Debug.LogError($"[BDArmory.RWPSettings]: Unknown field {fieldNode.name} when loading RWP settings.");
            continue;
          }
          var fieldValue = BDAPersistentSettingsField.ParseValue(field.FieldType, fieldNode.value);
          RWPOverrides[round][fieldNode.name] = fieldValue; // Add or set the override.
        }
      }

      // Set up the RWP round indices.
      sortedRoundNumbers = RWPOverrides.Keys.ToList(); sortedRoundNumbers.Sort();
      RWPRoundToIndex = sortedRoundNumbers.ToDictionary(r => r, sortedRoundNumbers.IndexOf);
      RWPIndexToRound = sortedRoundNumbers.ToDictionary(sortedRoundNumbers.IndexOf, r => r);

      // Set the loaded RWP settings.
      SetRWP(BDArmorySettings.RUNWAY_PROJECT, BDArmorySettings.RUNWAY_PROJECT_ROUND);
    }
  }
}