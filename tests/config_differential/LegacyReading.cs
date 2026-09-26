using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalAfternoonHeadTracking.Legacy;
using UnityEngine;

namespace EternalAfternoonHeadTracking.Tests.ConfigDifferential
{
    internal enum LoadStatus
    {
        /// <summary>The file was read.</summary>
        Usable,

        /// <summary>Reading threw, and the build ran on what it had read up to then.</summary>
        Failed,

        /// <summary>No file: the build ran on its defaults.</summary>
        Absent,
    }

    /// <summary>
    /// What one build ran on for one input: the load status, every setting, the startup state and
    /// the hotkeys it polled. A hotkey is written as its bindings, "modifiers:KeyCode" with the
    /// modifiers as core's KeyModifiers bits (3 is Ctrl+Shift), so a KeyCode with no name still
    /// compares.
    /// </summary>
    internal sealed class LegacyReading
    {
        public LoadStatus Status;
        public LegacyConfig Values;
        public bool Enabled;
        public bool RotationEnabled;
        public bool PositionEnabled;
        public bool WorldSpaceYaw;
        public SortedDictionary<string, string> Hotkeys = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public static readonly FieldInfo[] Fields = typeof(LegacyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private const string LoadError = "Config load error";

        /// <summary>Every difference between two readings, one line each; empty when they agree.</summary>
        public static List<string> Differences(LegacyReading a, LegacyReading b)
        {
            var d = new List<string>();
            if (a.Status != b.Status) d.Add("status " + a.Status + " / " + b.Status);
            foreach (FieldInfo f in Fields)
            {
                object x = f.GetValue(a.Values), y = f.GetValue(b.Values);
                if (!SameValue(x, y)) d.Add(f.Name + " " + Text(x) + " / " + Text(y));
            }
            if (a.Enabled != b.Enabled) d.Add("enabled " + a.Enabled + " / " + b.Enabled);
            if (a.RotationEnabled != b.RotationEnabled) d.Add("rotation " + a.RotationEnabled + " / " + b.RotationEnabled);
            if (a.PositionEnabled != b.PositionEnabled) d.Add("position " + a.PositionEnabled + " / " + b.PositionEnabled);
            if (a.WorldSpaceYaw != b.WorldSpaceYaw) d.Add("yaw " + a.WorldSpaceYaw + " / " + b.WorldSpaceYaw);
            var actions = new SortedSet<string>(a.Hotkeys.Keys, StringComparer.Ordinal);
            actions.UnionWith(b.Hotkeys.Keys);
            foreach (string action in actions)
            {
                string x, y;
                a.Hotkeys.TryGetValue(action, out x);
                b.Hotkeys.TryGetValue(action, out y);
                if (x != y) d.Add("hotkey " + action + " " + (x ?? "none") + " / " + (y ?? "none"));
            }
            return d;
        }

        public static bool SameValue(object x, object y)
        {
            if (x is float fx && y is float fy) return Bits(fx) == Bits(fy);
            if (x is Color cx && y is Color cy)
            {
                return Bits(cx.r) == Bits(cy.r) && Bits(cx.g) == Bits(cy.g) && Bits(cx.b) == Bits(cy.b) && Bits(cx.a) == Bits(cy.a);
            }
            return Equals(x, y);
        }

        public static string Text(object value)
        {
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is KeyCode k) return ((int)k).ToString(CultureInfo.InvariantCulture) + "(" + k + ")";
            if (value is Color c)
            {
                return string.Join(",", new[] { c.r, c.g, c.b, c.a }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>A primary key beside a Ctrl+Shift letter, as ChordHotkeys.IsActionPressed polled them.</summary>
        public static string Bindings(KeyCode primary, KeyCode chordLetter)
        {
            var items = new List<string>();
            if (primary != KeyCode.None) items.Add("0:" + ((int)primary).ToString(CultureInfo.InvariantCulture));
            items.Add("3:" + ((int)chordLetter).ToString(CultureInfo.InvariantCulture));
            return string.Join(", ", items.ToArray());
        }

        /// <summary>
        /// The newest published build, v0.2.0: its HeadTrackingConfig (tests/config_differential/Oracle,
        /// byte for byte the tag's file), then its HeadTrackingMod.Awake and Update. Awake started
        /// tracking on in rotation and position; Update polled four actions.
        /// </summary>
        public static LegacyReading Oracle(string path)
        {
            bool exists = File.Exists(path);
            var log = new List<string>();
            global::EternalAfternoonHeadTracking.HeadTrackingConfig config =
                global::EternalAfternoonHeadTracking.HeadTrackingConfig.LoadFromFile(path, log.Add);

            var values = new LegacyConfig();
            foreach (FieldInfo f in Fields)
            {
                PropertyInfo p = typeof(global::EternalAfternoonHeadTracking.HeadTrackingConfig).GetProperty(f.Name);
                f.SetValue(values, p.GetValue(config, null));
            }
            return Startup(StatusOf(exists, log), values);
        }

        /// <summary>
        /// The frozen reader (src/EternalAfternoonHeadTracking/Legacy), then the startup code and
        /// hotkeys of the commit that froze it.
        /// </summary>
        public static LegacyReading Import(string path)
        {
            bool exists = File.Exists(path);
            var log = new List<string>();
            LegacyConfig values = LegacyConfigReader.Read(path, log.Add);
            return Startup(StatusOf(exists, log), values);
        }

        private static LoadStatus StatusOf(bool exists, List<string> log)
        {
            if (!exists) return LoadStatus.Absent;
            return log.Exists(l => l.StartsWith(LoadError, StringComparison.Ordinal)) ? LoadStatus.Failed : LoadStatus.Usable;
        }

        private static LegacyReading Startup(LoadStatus status, LegacyConfig values)
        {
            var r = new LegacyReading
            {
                Status = status,
                Values = values,
                Enabled = true,
                RotationEnabled = true,
                PositionEnabled = true,
                WorldSpaceYaw = values.WorldSpaceYaw,
            };
            r.Hotkeys["ToggleTracking"] = Bindings(values.ToggleKey, KeyCode.Y);
            r.Hotkeys["CycleTrackingMode"] = Bindings(values.PositionToggleKey, KeyCode.G);
            r.Hotkeys["YawMode"] = Bindings(values.YawModeKey, KeyCode.H);
            r.Hotkeys["ToggleReticle"] = Bindings(values.ReticleToggleKey, KeyCode.U);
            return r;
        }

        private static int Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
