using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using Steamworks;
using UnityEngine;

namespace NPCFinder
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class NpcFinderPlugin : BaseUnityPlugin
    {
        internal const string ModName = "NPCFinder";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        public static bool SShowNpcesp = false;
        public static GameObject? Dialog;
        internal static bool CanUse = false;

        private readonly Harmony _harmony = new(ModGUID);

        internal static readonly ManualLogSource NpcFinderLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public void Awake()
        {
            ConfigSync.IsLocked = true;

            _menuHotkey = config("Hotkey", "Show NPC", new KeyboardShortcut(KeyCode.F, KeyCode.LeftControl),
                new ConfigDescription("The shortcut used to toggle the npc window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."),
                false);

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                NpcFinderLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                NpcFinderLogger.LogError($"There was an issue loading your {ConfigFileName}");
                NpcFinderLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        internal static string? readLocalSteamID() => Type.GetType("Steamworks.SteamUser, assembly_steamworks")
            ?.GetMethod("GetSteamID")!.Invoke(null, Array.Empty<object>()).ToString();

        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked = null!;
        private static ConfigEntry<KeyboardShortcut> _menuHotkey = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion

        private void OnGUI()
        {
            Npcesp.DisplayGUI();
        }

        public void Update()
        {
            Npcesp.Update();
        }

        private void LateUpdate()
        {
            if (_menuHotkey.Value.IsDown() && CanUse)
            {
                if (Player.m_localPlayer != null && Dialog)
                    if (Dialog != null)
                        Dialog.SetActive(true);
            }
        }
    }
}