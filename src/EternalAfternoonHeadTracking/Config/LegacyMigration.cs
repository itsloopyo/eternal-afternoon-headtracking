using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using EternalAfternoonHeadTracking.Legacy;
using UnityEngine;

namespace EternalAfternoonHeadTracking.Config
{
    /// <summary>
    /// The legacy import: the frozen v0.2.0 reader on HeadTracking.cfg, then a map, field by
    /// field, from what it read into the canonical settings. The owner runs it while
    /// CameraUnlock.ini is absent; it writes nothing.
    /// </summary>
    internal static class LegacyMigration
    {
        // What v0.2.0 shipped, and so what a pose-shaping value is compared with.
        private static readonly LegacyConfig Shipped = new LegacyConfig();

        // The line the frozen reader logs when reading the file threw. v0.2.0 then ran on what it
        // had read up to that point, which the import hands the session without creating a file.
        private const string LoadError = "Config load error (using defaults): ";

        public static LegacyImport<HeadTrackingConfigData> Import()
        {
            return new LegacyImport<HeadTrackingConfigData>(Run, LegacyConfigReader.Keys);
        }

        private static ImportResult Run(LegacyImportInput input, HeadTrackingConfigData config)
        {
            bool exists = File.Exists(input.Path);
            var log = new List<string>();
            LegacyConfig legacy = LegacyConfigReader.Read(input.Path, log.Add);

            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            Map(legacy, config, dropped, poseShaping);

            foreach (string line in log)
            {
                if (line.StartsWith(LoadError, StringComparison.Ordinal))
                {
                    return ImportResult.Refused("it could not be read (" + line.Substring(LoadError.Length) + ")");
                }
            }
            return exists ? ImportResult.Imported(dropped, poseShaping) : ImportResult.Absent(dropped, poseShaping);
        }

        public static void Map(LegacyConfig legacy, HeadTrackingConfigData config, ICollection<DroppedValue> dropped,
            ICollection<PoseShapingValue> poseShaping)
        {
            config.UdpPort = legacy.UdpPort;
            // v0.2.0 always started with tracking on, in rotation and position.
            config.EnableOnStartup = true;
            config.RotationEnabled = true;
            config.PositionEnabled = true;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            config.Position = config.Position.WithSmoothing(legacy.LocalSmoothing, legacy.RemoteSmoothing);

            config.ToggleKeyName = KeyList(legacy.ToggleKey, KeyCode.Y);
            config.CycleTrackingModeKeyName = KeyList(legacy.PositionToggleKey, KeyCode.G);
            config.YawModeKeyName = KeyList(legacy.YawModeKey, KeyCode.H);

            // HeadTracking.cfg has no sections.
            const string s = "";
            LegacyPoseShaping.Record(legacy.YawSensitivity, Shipped.YawSensitivity, s, "YawSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, Shipped.PitchSensitivity, s, "PitchSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, Shipped.RollSensitivity, s, "RollSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityX, Shipped.PositionSensitivityX, s, "PositionSensitivityX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityY, Shipped.PositionSensitivityY, s, "PositionSensitivityY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityZ, Shipped.PositionSensitivityZ, s, "PositionSensitivityZ", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPositionX, Shipped.InvertPositionX, s, "InvertPositionX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPositionY, Shipped.InvertPositionY, s, "InvertPositionY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertTrackerZ, Shipped.InvertTrackerZ, s, "InvertTrackerZ", poseShaping, dropped);

            dropped.Add(new DroppedValue(DropRule.Reticle, s, "ShowReticle", legacy.ShowReticle ? "true" : "false"));
            dropped.Add(new DroppedValue(DropRule.Reticle, s, "ReticleColor", ColorText(legacy.ReticleColor)));
            dropped.Add(new DroppedValue(DropRule.Reticle, s, "ReticleToggleKey", legacy.ReticleToggleKey.ToString()));
        }

        /// <summary>
        /// A legacy hotkey as a key list: the key the player set, then the Ctrl+Shift letter
        /// ChordHotkeys polled beside it. KeyCode.None bound nothing. Core's data/keys.json holds
        /// every KeyCode name of Eternal Afternoon's Unity (2022.3.62), so every other key has one.
        /// </summary>
        private static string KeyList(KeyCode primary, KeyCode chordLetter)
        {
            var items = new List<string>();
            if (primary != KeyCode.None) items.Add(KeyName(primary));
            items.Add("Ctrl+Shift+" + KeyName(chordLetter));
            return string.Join(", ", items.ToArray());
        }

        private static string KeyName(KeyCode key)
        {
            string text = key.ToString();
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(text, out bindings, out error))
            {
                throw new InvalidOperationException("KeyCode." + text + " has no key name: " + error);
            }
            return KeyBindings.Format(bindings);
        }

        private static string ColorText(Color c)
        {
            return string.Join(",", new[]
            {
                c.r.ToString("R", CultureInfo.InvariantCulture),
                c.g.ToString("R", CultureInfo.InvariantCulture),
                c.b.ToString("R", CultureInfo.InvariantCulture),
                c.a.ToString("R", CultureInfo.InvariantCulture),
            });
        }
    }
}
