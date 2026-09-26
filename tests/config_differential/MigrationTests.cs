using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CameraUnlock.Core.Config;
using EternalAfternoonHeadTracking.Config;
using EternalAfternoonHeadTracking.Legacy;
using Xunit;

namespace EternalAfternoonHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Comparison 2: the frozen reader (the import) against the owner's Load, which imports the
    /// legacy file into a new CameraUnlock.ini (the migration), over every input. What may differ
    /// is only what data/config-format.json approves: pose shaping (the sensitivities and axis
    /// inversions) and reticle settings. The published build had no startup-enabled or startup-mode
    /// key and always started tracking in rotation and position, which the migration writes.
    /// </summary>
    public class MigrationTests : IDisposable
    {
        private readonly string scratch = RepoPaths.Scratch();
        private readonly DefaultsFile defaults;

        public MigrationTests()
        {
            defaults = Migration.ScratchDefaults(scratch);
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(scratch, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(scratch, true);
        }

        [Fact]
        public void ComparisonTwo()
        {
            var failures = new List<string>();
            int migrated = 0, created = 0;
            foreach (KeyValuePair<string, byte[]> input in Corpus.Inputs())
            {
                string where = input.Key + ": ";
                LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", input.Value));
                Migration m = Migration.Run(Path.Combine(scratch, "game"), input.Value, defaults);

                if (import.Status == LoadStatus.Absent)
                {
                    Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Created, "status " + m.Loaded.Status + ", not Created");
                    Check(failures, where, SameFields(Migration.Defaults(), m.Loaded.Config), "a first start does not run on the defaults");
                    Check(failures, where, Names(m) == ModConfig.FileName, "the folder holds " + Names(m));
                    created++;
                    continue;
                }
                Check(failures, where, import.Status == LoadStatus.Usable, "the frozen reader failed on it");

                var expected = Migration.Defaults();
                var dropped = new List<DroppedValue>();
                var poseShaping = new List<PoseShapingValue>();
                LegacyMigration.Map(import.Values, expected, dropped, poseShaping);
                CheckRules(failures, where, import, expected, dropped, poseShaping);

                // data/keys.json holds every KeyCode name of Eternal Afternoon's Unity (2022.3.62), so
                // every hotkey the published build read writes as a key list, and no input defers.
                Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Migrated, "status " + m.Loaded.Status + ", not Migrated: " + m.Loaded.Reason);
                Check(failures, where, File.ReadAllBytes(m.LegacyPath).SequenceEqual(input.Value), "the legacy file changed");
                if (m.Loaded.Status != ConfigLoadStatus.Migrated) continue;
                Check(failures, where, SameFields(expected, m.Loaded.Config), Difference(expected, m.Loaded.Config));
                Check(failures, where, Names(m) == ModConfig.FileName + ", " + ModConfig.LegacyFileName, "the folder holds " + Names(m));
                foreach (DroppedValue d in dropped)
                {
                    string line = m.LegacyPath + ": " + d.Describe();
                    Check(failures, where, m.Loaded.Log.Contains(line), "the log does not name " + d.Describe());
                }
                string lint = Lint(File.ReadAllBytes(m.ConfigPath));
                Check(failures, where, lint == null, "the migrated file " + lint);
                SecondLoad(failures, where, m);
                migrated++;
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40).ToArray()));
            Assert.True(migrated > 500, migrated + " inputs migrated");
            Assert.True(created == 1, created + " inputs were a first start");
        }

        /// <summary>
        /// A read-only legacy file imports as a writable one does and keeps its attribute, bytes
        /// and write time. Run over the published builds' first-run files and a player's edits.
        /// </summary>
        [Fact]
        public void ReadOnlyLegacyFileImportsTheSame()
        {
            var inputs = new List<byte[]>();
            foreach (string tag in Corpus.PublishedTags) inputs.Add(Corpus.FirstRun(tag));
            inputs.Add(Edited());
            foreach (byte[] bytes in inputs)
            {
                Migration writable = Migration.Run(Path.Combine(scratch, "writable"), bytes, defaults);
                string folder = Path.Combine(scratch, "readonly");
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder)) File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(folder, true);
                }
                Directory.CreateDirectory(folder);
                string legacy = Path.Combine(folder, ModConfig.LegacyFileName);
                File.WriteAllBytes(legacy, bytes);
                File.SetAttributes(legacy, FileAttributes.ReadOnly);
                DateTime written = File.GetLastWriteTimeUtc(legacy);

                ConfigLoadResult<HeadTrackingConfigData> loaded = Migration.Reopen(folder, defaults).Load();

                Assert.Equal(ConfigLoadStatus.Migrated, loaded.Status);
                Assert.True(SameFields(writable.Loaded.Config, loaded.Config));
                Assert.Equal(File.ReadAllBytes(writable.ConfigPath), File.ReadAllBytes(Path.Combine(folder, ModConfig.FileName)));
                Assert.Equal(bytes, File.ReadAllBytes(legacy));
                Assert.Equal(written, File.GetLastWriteTimeUtc(legacy));
                Assert.True((File.GetAttributes(legacy) & FileAttributes.ReadOnly) != 0);
            }
        }

        /// <summary>
        /// Fresh equals upgrade: the first-run file of every published build imports, with
        /// Defaults.ini at the built-in values, into exactly the committed file. No build shipped a
        /// config file or a launcher seed.
        /// </summary>
        [Fact]
        public void EveryPublishedFirstRunImportsIntoTheCommittedFile()
        {
            byte[] committed = File.ReadAllBytes(RenderTests.CommittedPath);
            foreach (string tag in Corpus.PublishedTags)
            {
                Migration m = Migration.Run(Path.Combine(scratch, "game"), Corpus.FirstRun(tag), defaults);
                Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
                Assert.True(committed.SequenceEqual(File.ReadAllBytes(m.ConfigPath)), tag);
            }
        }

        /// <summary>
        /// v0.1.0 to v0.1.5 wrote Smoothing, and v0.1.0 to v0.1.8 wrote RecenterKey and
        /// InvertPositionZ. v0.2.0 reads none of them, and neither does the import, so the
        /// migration names each as not carried and the view moves as it did under v0.2.0.
        /// </summary>
        [Fact]
        public void KeysOnlyEarlierBuildsReadAreNamedAsNotCarried()
        {
            var expected = new Dictionary<string, string[]>
            {
                { "v0.1.0", new[] { "RecenterKey=Home", "Smoothing=0.0", "InvertPositionZ=true" } },
                { "v0.1.5", new[] { "RecenterKey=Home", "Smoothing=0.0", "InvertPositionZ=true" } },
                { "v0.1.8", new[] { "RecenterKey=Home", "InvertPositionZ=true" } },
                { "v0.2.0", new string[0] },
            };
            foreach (KeyValuePair<string, string[]> tag in expected)
            {
                Migration m = Migration.Run(Path.Combine(scratch, "game"), Corpus.FirstRun(tag.Key), defaults);
                string[] notCarried = m.Loaded.Log
                    .Where(l => l.StartsWith(m.LegacyPath + ": not carried: ", StringComparison.Ordinal) && l.EndsWith("this build does not read it", StringComparison.Ordinal))
                    .ToArray();
                Assert.Equal(tag.Value.Length, notCarried.Length);
                foreach (string key in tag.Value)
                {
                    Assert.Contains(notCarried, l => l.Contains(": not carried: " + key + " on line "));
                }
            }
        }

        private void SecondLoad(List<string> failures, string where, Migration first)
        {
            byte[] config = File.ReadAllBytes(first.ConfigPath);
            byte[] legacy = File.ReadAllBytes(first.LegacyPath);
            DateTime configWritten = File.GetLastWriteTimeUtc(first.ConfigPath);
            ConfigLoadResult<HeadTrackingConfigData> again = Migration.Reopen(Path.GetDirectoryName(first.ConfigPath), defaults).Load();
            Check(failures, where, again.Status == ConfigLoadStatus.Canonical, "second load " + again.Status);
            Check(failures, where, SameFields(first.Loaded.Config, again.Config), "the second load reads other values");
            Check(failures, where, again.Log.Any(l => l.Contains(first.LegacyPath + " is left as it was and is not read.")), "the second load does not say the legacy file is not read");
            Check(failures, where, config.SequenceEqual(File.ReadAllBytes(first.ConfigPath)) && configWritten == File.GetLastWriteTimeUtc(first.ConfigPath), "the second load rewrote CameraUnlock.ini");
            Check(failures, where, legacy.SequenceEqual(File.ReadAllBytes(first.LegacyPath)), "the second load changed the legacy file");
        }

        /// <summary>The approved rules, field by field, from the frozen reader to the map.</summary>
        private static void CheckRules(List<string> failures, string where, LegacyReading import, HeadTrackingConfigData mapped,
            List<DroppedValue> dropped, List<PoseShapingValue> poseShaping)
        {
            LegacyConfig l = import.Values;
            Check(failures, where, mapped.UdpPort == l.UdpPort, "UdpPort");
            Check(failures, where, mapped.EnableOnStartup == import.Enabled, "EnableOnStartup");
            Check(failures, where, mapped.WorldSpaceYaw == import.WorldSpaceYaw, "WorldSpaceYaw");
            Check(failures, where, mapped.RotationEnabled == import.RotationEnabled && mapped.PositionEnabled == import.PositionEnabled, "tracking mode");
            Check(failures, where, LegacyReading.SameValue(mapped.LocalSmoothing, l.LocalSmoothing) && LegacyReading.SameValue(mapped.RemoteSmoothing, l.RemoteSmoothing), "smoothing");
            Check(failures, where, LegacyReading.SameValue(mapped.Position.LocalSmoothing, l.LocalSmoothing) && LegacyReading.SameValue(mapped.Position.RemoteSmoothing, l.RemoteSmoothing), "position smoothing");

            var polled = new Dictionary<string, string>
            {
                { "ToggleTracking", mapped.ToggleKeyName },
                { "CycleTrackingMode", mapped.CycleTrackingModeKeyName },
                { "YawMode", mapped.YawModeKeyName },
            };
            foreach (KeyValuePair<string, string> action in polled)
            {
                string bindings = Migration.Polled(action.Value);
                Check(failures, where, bindings == import.Hotkeys[action.Key], action.Key + " polls " + (bindings ?? "an unreadable list") + ", the published build " + import.Hotkeys[action.Key]);
            }

            var shipped = new LegacyConfig();
            var expectedShaping = new[]
            {
                Shaping("YawSensitivity", l.YawSensitivity == shipped.YawSensitivity),
                Shaping("PitchSensitivity", l.PitchSensitivity == shipped.PitchSensitivity),
                Shaping("RollSensitivity", l.RollSensitivity == shipped.RollSensitivity),
                Shaping("PositionSensitivityX", l.PositionSensitivityX == shipped.PositionSensitivityX),
                Shaping("PositionSensitivityY", l.PositionSensitivityY == shipped.PositionSensitivityY),
                Shaping("PositionSensitivityZ", l.PositionSensitivityZ == shipped.PositionSensitivityZ),
                Shaping("InvertPositionX", l.InvertPositionX == shipped.InvertPositionX),
                Shaping("InvertPositionY", l.InvertPositionY == shipped.InvertPositionY),
                Shaping("InvertTrackerZ", l.InvertTrackerZ == shipped.InvertTrackerZ),
            };
            Check(failures, where, poseShaping.Count == expectedShaping.Length, poseShaping.Count + " pose-shaping values");
            var expectedDropped = new List<string>();
            foreach (KeyValuePair<string, bool> e in expectedShaping)
            {
                PoseShapingValue v = poseShaping.FirstOrDefault(p => p.Section == "" && p.Key == e.Key);
                Check(failures, where, v != null && v.Folded == e.Value, e.Key + " pose shaping");
                if (!e.Value && v != null) expectedDropped.Add("PoseShaping " + e.Key + "=" + v.Value);
            }
            expectedDropped.Add("Reticle ShowReticle=" + (l.ShowReticle ? "true" : "false"));
            expectedDropped.Add("Reticle ReticleColor");
            expectedDropped.Add("Reticle ReticleToggleKey=" + l.ReticleToggleKey);
            var actualDropped = dropped
                .Select(d => d.Rule + " " + d.Key + (d.Key == "ReticleColor" ? "" : "=" + d.Value))
                .ToList();
            Check(failures, where, dropped.All(d => d.Section == ""), "a dropped value names a section");
            Check(failures, where, expectedDropped.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(actualDropped.OrderBy(x => x, StringComparer.Ordinal)),
                "dropped " + string.Join("; ", actualDropped.ToArray()));
        }

        private static KeyValuePair<string, bool> Shaping(string key, bool folded)
        {
            return new KeyValuePair<string, bool>(key, folded);
        }

        internal static bool SameFields(HeadTrackingConfigData a, HeadTrackingConfigData b)
        {
            return Difference(a, b) == null;
        }

        private static string Difference(HeadTrackingConfigData a, HeadTrackingConfigData b)
        {
            SortedDictionary<string, string> x = Migration.Fields(a), y = Migration.Fields(b);
            foreach (KeyValuePair<string, string> f in x)
            {
                if (f.Value != y[f.Key]) return f.Key + " " + f.Value + " / " + y[f.Key];
            }
            return null;
        }

        private static string Names(Migration m)
        {
            return string.Join(", ", Directory.GetFiles(Path.GetDirectoryName(m.LegacyPath)).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// What core's canonical config lint checks that a file the owner wrote can get wrong:
        /// the reader finds nothing to report, the stamp, CRLF only, ASCII only, and the table
        /// reads every line.
        /// </summary>
        internal static string Lint(byte[] bytes)
        {
            CanonicalIni doc = CanonicalIni.Parse(bytes);
            if (!doc.IsReadable) return "is unreadable";
            if (!CanonicalIni.HasStamp(bytes) || doc.FormatVersion != CanonicalIni.ConfigFormat) return "has no [CameraUnlock] ConfigFormat=1";
            if (doc.Diagnostics.Count > 0) return "draws " + doc.Diagnostics[0].Describe();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] > 0x7E || (bytes[i] < 0x20 && bytes[i] != 0x0D && bytes[i] != 0x0A)) return "holds byte " + bytes[i] + " at " + i;
                if (bytes[i] == 0x0A && (i == 0 || bytes[i - 1] != 0x0D)) return "has an LF without CR at " + i;
                if (bytes[i] == 0x0D && (i + 1 == bytes.Length || bytes[i + 1] != 0x0A)) return "has a CR without LF at " + i;
            }
            if (bytes.Length < 2 || bytes[bytes.Length - 2] != 0x0D || bytes[bytes.Length - 1] != 0x0A) return "does not end in CRLF";
            ApplyReport report = ModConfig.Table().Apply(doc, new HeadTrackingConfigData());
            if (report.Diagnostics.Count > 0) return "draws " + report.Diagnostics[0].Describe();
            return null;
        }

        /// <summary>
        /// A legacy file a player edited away from the defaults the map carries, with a hand
        /// comment and a key no build reads.
        /// </summary>
        internal static byte[] Edited()
        {
            string text = System.Text.Encoding.ASCII.GetString(Corpus.FirstRun("v0.2.0"));
            var edits = new Dictionary<string, string>
            {
                { "UdpPort = 4242", "UdpPort = 5555" },
                { "WorldSpaceYaw = true", "WorldSpaceYaw = false" },
                { "LocalSmoothing = 0.0", "LocalSmoothing = 0.25" },
                { "RemoteSmoothing = 0.15", "RemoteSmoothing = 0.4" },
                { "ToggleKey = End", "ToggleKey = F8" },
                { "PositionToggleKey = PageUp", "PositionToggleKey = None" },
                { "YawSensitivity = 1.0", "YawSensitivity = 1.5" },
                { "ShowReticle = true", "ShowReticle = false\n# my own note\nFieldOfView = 90" },
            };
            foreach (KeyValuePair<string, string> e in edits)
            {
                if (!text.Contains(e.Key)) throw new InvalidOperationException("v0.2.0's first run has no line " + e.Key);
                text = text.Replace(e.Key, e.Value);
            }
            return System.Text.Encoding.ASCII.GetBytes(text);
        }

        private static void Check(List<string> failures, string where, bool ok, string what)
        {
            if (!ok) failures.Add(where + what);
        }
    }
}
