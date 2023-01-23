using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShipStats
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ShipStatsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ShipStats";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource ShipStatsLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);
        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            FontSize = config("1 - Text", "Stats Font Size", 15,
                "Font Size of the stats text. Default is 15.");
            TextColor = config("1 - Text", "Stats Text Color", new Color(1,1,1,1),
                "Color of the stats text. Default is white.");
            TextFormat = config("1 - Text", "Stats Text Format", "Ship Speed:\n\t{0:0.#} knots\n\t{1:0.#} mph\nWind Speed: {2:0.#} knots\nWind Direction: {3:0.#}° {4}\n{5}",
                "{0} is ship speed in knots\n{1} is ship speed in mph\n{2} is wind speed in knots\n{3} is wind direction in degrees\n{4} is wind direction in cardinal directions\n{5} is ship inventory count and percent");
            
            AnchoredPosition = config("2 - UI", "Stats Anchored Position", new Vector2(200,-27),
                "Anchored position of the stats text. Please note that this is relative to the rudder icon. Default is 200,-27.");
            PanelColor = config("2 - UI", "UI Background Color", new Color(0,0,0,0.5f),
                "Color of panel background. Default is black with half transparency.");
            
            
            // Create event handlers for setting changed
            FontSize.SettingChanged += (sender, args) => UIElementChanged();
            TextColor.SettingChanged += (sender, args) => UIElementChanged();
            AnchoredPosition.SettingChanged += (sender, args) => UIElementChanged();
            PanelColor.SettingChanged += (sender, args) => UIElementChanged();



            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }
        
        private void UIElementChanged()
        {
            if (HudAwakePatch.contentText != null)
            {
                HudAwakePatch.contentText.fontSize = FontSize.Value;
                HudAwakePatch.contentText.color = TextColor.Value;
            }
            if (HudAwakePatch.contentText2 != null)
            {
                HudAwakePatch.contentText2.fontSize = FontSize.Value;
                HudAwakePatch.contentText2.color = TextColor.Value;
            }

            if (HudAwakePatch.Go != null)
            {
                HudAwakePatch.Go.GetComponent<Image>().color = PanelColor.Value;
                HudAwakePatch.Go.GetComponent<RectTransform>().anchoredPosition = AnchoredPosition.Value;
            }
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
                ShipStatsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ShipStatsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ShipStatsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions
        public static ConfigEntry<int> FontSize = null!;
        public static ConfigEntry<Color> TextColor = null!;
        public static ConfigEntry<string> TextFormat = null!;
        public static ConfigEntry<Vector2> AnchoredPosition = null!;
        public static ConfigEntry<Color> PanelColor = null!;
        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            //var configEntry = Config.Bind(group, name, value, description);

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
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
    }
}