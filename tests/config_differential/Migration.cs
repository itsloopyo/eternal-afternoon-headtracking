using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using EternalAfternoonHeadTracking.Config;

namespace EternalAfternoonHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// The migration: the owner's Load in a folder holding only the legacy file, as the converted
    /// mod runs it from Eternal Afternoon_Data\Managed.
    /// </summary>
    internal sealed class Migration
    {
        public ConfigOwner<HeadTrackingConfigData> Owner;
        public ConfigLoadResult<HeadTrackingConfigData> Loaded;
        public string LegacyPath;
        public string ConfigPath;

        public static Migration Run(string folder, byte[] legacy, DefaultsFile defaults)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            var m = new Migration
            {
                LegacyPath = Path.Combine(folder, ModConfig.LegacyFileName),
                ConfigPath = Path.Combine(folder, ModConfig.FileName),
            };
            if (legacy != null) File.WriteAllBytes(m.LegacyPath, legacy);
            m.Owner = Reopen(folder, defaults);
            m.Loaded = m.Owner.Load();
            return m;
        }

        /// <summary>The owner the mod builds at its next start, over a folder a Run left.</summary>
        public static ConfigOwner<HeadTrackingConfigData> Reopen(string folder, DefaultsFile defaults)
        {
            return new ConfigOwner<HeadTrackingConfigData>(ModConfig.Options(folder, defaults, null));
        }

        /// <summary>
        /// Defaults.ini in a scratch profile folder, never the player's own. Load creates it with
        /// the built-in values.
        /// </summary>
        public static DefaultsFile ScratchDefaults(string scratch)
        {
            string profile = Path.Combine(scratch, "profile");
            Directory.CreateDirectory(profile);
            return DefaultsFile.At(Path.Combine(profile, "CameraUnlock", "Defaults.ini"));
        }

        /// <summary>The table's defaults: every row read from a file that sets none.</summary>
        public static HeadTrackingConfigData Defaults()
        {
            var config = new HeadTrackingConfigData();
            ModConfig.Table().Apply(CanonicalIni.Parse(new byte[0]), config);
            return config;
        }

        /// <summary>Every setting the table binds, and the position smoothing it composes, as text; floats by their bits.</summary>
        public static SortedDictionary<string, string> Fields(HeadTrackingConfigData c)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "UdpPort", c.UdpPort.ToString(CultureInfo.InvariantCulture) },
                { "EnableOnStartup", c.EnableOnStartup.ToString() },
                { "WorldSpaceYaw", c.WorldSpaceYaw.ToString() },
                { "RotationEnabled", c.RotationEnabled.ToString() },
                { "PositionEnabled", c.PositionEnabled.ToString() },
                { "LocalSmoothing", Bits(c.LocalSmoothing) },
                { "RemoteSmoothing", Bits(c.RemoteSmoothing) },
                { "Position.LocalSmoothing", Bits(c.Position.LocalSmoothing) },
                { "Position.RemoteSmoothing", Bits(c.Position.RemoteSmoothing) },
                { "ToggleKey", c.ToggleKeyName },
                { "CycleTrackingModeKey", c.CycleTrackingModeKeyName },
                { "YawModeKey", c.YawModeKeyName },
            };
        }

        /// <summary>
        /// A key list as the bindings the mod polls, written as <see cref="LegacyReading.Bindings"/>
        /// writes them; null when the list does not parse.
        /// </summary>
        public static string Polled(string keyList)
        {
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(keyList, out bindings, out error)) return null;
            var items = new List<string>();
            foreach (KeyBinding b in bindings)
            {
                items.Add(((int)b.Modifiers).ToString(CultureInfo.InvariantCulture) + ":" + b.UnityKeyCode.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(", ", items.ToArray());
        }

        private static string Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
