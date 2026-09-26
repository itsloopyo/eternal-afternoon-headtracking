using System.Collections.Generic;
using System.IO;
using CameraUnlock.Core.Config.Testing;
using EternalAfternoonHeadTracking.Legacy;

namespace EternalAfternoonHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// The differential test's inputs: no file, an empty file, the first-run output of every
    /// published build, and core's mutation corpus over the newest one. The published builds are
    /// the GitHub releases v0.1.0 to v0.2.0; v0.1.3, v0.1.6 and v0.1.7 are tags with no release.
    /// No build shipped a config file or a launcher seed, and there is no predecessor repo.
    /// </summary>
    internal static class Corpus
    {
        public static readonly string[] PublishedTags =
        {
            "v0.1.0", "v0.1.1", "v0.1.2", "v0.1.4", "v0.1.5", "v0.1.8", "v0.2.0",
        };

        private static readonly string[] None = new string[0];

        /// <summary>
        /// One descriptor per key the frozen reader reads, in <see cref="LegacyConfigReader.Keys"/>
        /// order: an alternate value it reads, and a value outside each range it refuses or
        /// clamps. No legacy key names a chord: ChordHotkeys polled the Ctrl+Shift letter in code.
        /// </summary>
        public static readonly MutationKey[] Keys =
        {
            Value("UdpPort", "5555", "0", "65536"),
            Value("YawSensitivity", "1.5"),
            Value("PitchSensitivity", "1.5"),
            Value("RollSensitivity", "1.5"),
            Value("LocalSmoothing", "0.3", "-0.1", "1.1"),
            Value("RemoteSmoothing", "0.3", "-0.1", "1.1"),
            Hotkey("ToggleKey", "F8"),
            Hotkey("PositionToggleKey", "F9"),
            Hotkey("ReticleToggleKey", "F10"),
            Hotkey("YawModeKey", "F7"),
            Value("WorldSpaceYaw", "false"),
            Value("PositionSensitivityX", "2.0"),
            Value("PositionSensitivityY", "2.0"),
            Value("PositionSensitivityZ", "2.0"),
            Value("InvertPositionX", "false"),
            Value("InvertPositionY", "true"),
            Value("InvertTrackerZ", "true"),
            Value("ShowReticle", "false"),
            Value("ReticleColor", "1.0,0.0,0.0,1.0", "1.5,1.0,1.0,1.0"),
        };

        /// <summary>Every input as (name, bytes); null bytes is no file.</summary>
        public static IEnumerable<KeyValuePair<string, byte[]>> Inputs()
        {
            yield return new KeyValuePair<string, byte[]>("no file", null);
            yield return new KeyValuePair<string, byte[]>("empty file", new byte[0]);
            foreach (string tag in PublishedTags)
            {
                yield return new KeyValuePair<string, byte[]>(tag + " first run", FirstRun(tag));
            }
            foreach (IniMutation m in IniMutations.Generate(FirstRun("v0.2.0"), LegacyConfigReader.Keys, Keys))
            {
                yield return new KeyValuePair<string, byte[]>("corpus: " + m.Name, m.Bytes);
            }
        }

        /// <summary>
        /// The HeadTracking.cfg a published build wrote at its first start: the one string literal
        /// its WriteDefaults wrote, extracted once from the release's EternalAfternoonHeadTracking.dll.
        /// </summary>
        public static byte[] FirstRun(string tag)
        {
            return File.ReadAllBytes(Path.Combine(RepoPaths.Root, "tests", "config_differential", "data", "first-run", tag + ".cfg"));
        }

        private static MutationKey Value(string key, string alternate, params string[] outOfRange)
        {
            return new MutationKey("", key, alternate, outOfRange, false, new ChordSwitch[0]);
        }

        private static MutationKey Hotkey(string key, string alternate)
        {
            return new MutationKey("", key, alternate, None, true, new ChordSwitch[0]);
        }
    }
}
