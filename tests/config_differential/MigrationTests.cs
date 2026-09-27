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
                LegacyFollowsDefaultsIni follows = LegacyMigration.Map(import.Values, expected, dropped, poseShaping);
                CheckRules(failures, where, import, expected, dropped, poseShaping);
                CheckFollows(failures, where, import.Values, follows);

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

        // The global rows the table does not keep for the game, in the order the import gives them.
        private static readonly string[] FollowingRows =
        {
            "[Network] UdpPort", "[General] EnableOnStartup", "[General] WorldSpaceYaw",
            "[General] RotationEnabled", "[Position] PositionEnabled",
            "[Smoothing] LocalSmoothing", "[Smoothing] RemoteSmoothing",
            "[Hotkeys] ToggleKey", "[Hotkeys] CycleTrackingModeKey", "[Hotkeys] YawModeKey",
        };

        // A Defaults.ini that differs from the built-in values on every row in FollowingRows (the
        // tracking mode as a pair: position only, where the built-in mode is both), and from the
        // corpus alternate on every row but WorldSpaceYaw, where no third value exists.
        private const string OtherDefaults =
            "[CameraUnlock]\r\nConfigFormat=1\r\n" +
            "[Network]\r\nUdpPort=4250\r\n" +
            "[General]\r\nEnableOnStartup=false\r\nWorldSpaceYaw=false\r\nRotationEnabled=false\r\n" +
            "[Smoothing]\r\nLocalSmoothing=0.25\r\nRemoteSmoothing=0.6\r\n" +
            "[Position]\r\nPositionEnabled=true\r\n" +
            "[Hotkeys]\r\nToggleKey=F2\r\nCycleTrackingModeKey=F3\r\nYawModeKey=F4\r\n";

        // Each legacy key the map carries into a global row, with the row it sets. v0.2.0 had no
        // startup-enabled or tracking-mode setting, so those rows always follow Defaults.ini.
        private static readonly KeyValuePair<string, string>[] LegacyRows =
        {
            new KeyValuePair<string, string>("UdpPort", "[Network] UdpPort"),
            new KeyValuePair<string, string>("WorldSpaceYaw", "[General] WorldSpaceYaw"),
            new KeyValuePair<string, string>("LocalSmoothing", "[Smoothing] LocalSmoothing"),
            new KeyValuePair<string, string>("RemoteSmoothing", "[Smoothing] RemoteSmoothing"),
            new KeyValuePair<string, string>("ToggleKey", "[Hotkeys] ToggleKey"),
            new KeyValuePair<string, string>("PositionToggleKey", "[Hotkeys] CycleTrackingModeKey"),
            new KeyValuePair<string, string>("YawModeKey", "[Hotkeys] YawModeKey"),
        };

        // The row behind each field Migration.Fields lists.
        private static readonly Dictionary<string, string> FieldRows = new Dictionary<string, string>
        {
            { "UdpPort", "[Network] UdpPort" },
            { "EnableOnStartup", "[General] EnableOnStartup" },
            { "WorldSpaceYaw", "[General] WorldSpaceYaw" },
            { "RotationEnabled", "[General] RotationEnabled" },
            { "PositionEnabled", "[Position] PositionEnabled" },
            { "LocalSmoothing", "[Smoothing] LocalSmoothing" },
            { "Position.LocalSmoothing", "[Smoothing] LocalSmoothing" },
            { "RemoteSmoothing", "[Smoothing] RemoteSmoothing" },
            { "Position.RemoteSmoothing", "[Smoothing] RemoteSmoothing" },
            { "ToggleKey", "[Hotkeys] ToggleKey" },
            { "CycleTrackingModeKey", "[Hotkeys] CycleTrackingModeKey" },
            { "YawModeKey", "[Hotkeys] YawModeKey" },
        };

        /// <summary>
        /// A setting the player never changed follows Defaults.ini (owner rule of 2026-09-26): the
        /// empty file and the first-run file of every published build leave every global row to
        /// it, and under a Defaults.ini that differs on every such row the migrated file holds
        /// default on each and the session runs on Defaults.ini's values.
        /// </summary>
        [Fact]
        public void AnUntouchedFileFollowsDefaultsIni()
        {
            DefaultsFile other = OtherDefaultsFile();
            HeadTrackingConfigData fresh = FreshUnder(other);
            SortedDictionary<string, string> builtIn = Migration.Fields(Migration.Defaults());
            foreach (KeyValuePair<string, string> f in Migration.Fields(fresh))
            {
                if (f.Key != "PositionEnabled") Assert.True(f.Value != builtIn[f.Key], f.Key + " does not differ in the other Defaults.ini");
            }

            var inputs = new List<KeyValuePair<string, byte[]>> { new KeyValuePair<string, byte[]>("empty file", new byte[0]) };
            foreach (string tag in Corpus.PublishedTags) inputs.Add(new KeyValuePair<string, byte[]>(tag + " first run", Corpus.FirstRun(tag)));
            foreach (KeyValuePair<string, byte[]> input in inputs)
            {
                Assert.Equal(FollowingRows, RowsOf(MapOf(input.Value, new List<DroppedValue>())));
                AssertMigration(input.Key, input.Value, other, fresh, new string[0]);
            }
        }

        /// <summary>
        /// A setting the player changed keeps the player's value: v0.2.0's first run with one
        /// legacy key at the corpus alternate leaves every global row but the one it sets to
        /// Defaults.ini, and that one takes the value the import read.
        /// </summary>
        [Fact]
        public void AChangedSettingKeepsThePlayersValue()
        {
            DefaultsFile other = OtherDefaultsFile();
            HeadTrackingConfigData fresh = FreshUnder(other);
            foreach (KeyValuePair<string, string> legacyKey in LegacyRows)
            {
                string alternate = Corpus.Keys.Single(k => k.Key == legacyKey.Key).Alternate;
                byte[] edited = Edit(Corpus.FirstRun("v0.2.0"), legacyKey.Key, alternate);
                Assert.Equal(FollowingRows.Where(r => r != legacyKey.Value).ToArray(), RowsOf(MapOf(edited, new List<DroppedValue>())));
                AssertMigration(legacyKey.Key + " = " + alternate, edited, other, fresh, new[] { legacyKey.Value });
            }
        }

        /// <summary>
        /// N3: a legacy hotkey on a Ctrl, Shift or Alt key alone imports as unbound, logged as
        /// ModifierKey, and the player keeps the Ctrl+Shift chord ChordHotkeys polled beside it.
        /// </summary>
        [Fact]
        public void AModifierKeyHotkeyUnbindsAndKeepsTheChord()
        {
            byte[] edited = Edit(Corpus.FirstRun("v0.2.0"), "ToggleKey", "LeftShift");
            var dropped = new List<DroppedValue>();
            LegacyFollowsDefaultsIni follows = MapOf(edited, dropped);
            Assert.DoesNotContain(ConfigConcepts.ToggleKey, follows.Concepts);
            DroppedValue modifier = dropped.Single(d => d.Rule == DropRule.ModifierKey);
            Assert.Equal("", modifier.Section);
            Assert.Equal("ToggleKey", modifier.Key);
            Assert.Equal("LeftShift", modifier.Value);

            Migration m = Migration.Run(Path.Combine(scratch, "modifier"), edited, defaults);
            Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
            Assert.Equal("Ctrl+Shift+Y", m.Loaded.Config.ToggleKeyName);
            Assert.Equal("Ctrl+Shift+Y", FileRows(m.ConfigPath)["[Hotkeys] ToggleKey"]);
            Assert.Contains(m.LegacyPath + ": " + modifier.Describe(), m.Loaded.Log);
        }

        private void AssertMigration(string name, byte[] legacy, DefaultsFile other, HeadTrackingConfigData fresh, string[] changed)
        {
            LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", legacy));
            HeadTrackingConfigData expected = Migration.Defaults();
            LegacyMigration.Map(import.Values, expected, new List<DroppedValue>(), new List<PoseShapingValue>());

            Migration m = Migration.Run(Path.Combine(scratch, "other"), legacy, other);
            Assert.True(m.Loaded.Status == ConfigLoadStatus.Migrated, name + ": " + m.Loaded.Status);
            Dictionary<string, string> written = FileRows(m.ConfigPath);
            foreach (string row in FollowingRows.Except(changed))
            {
                Assert.True(written[row] == "default", name + ": " + row + "=" + written[row] + " does not follow Defaults.ini");
            }
            SortedDictionary<string, string> want = Migration.Fields(expected), ini = Migration.Fields(fresh);
            foreach (KeyValuePair<string, string> f in Migration.Fields(m.Loaded.Config))
            {
                string wanted = changed.Contains(FieldRows[f.Key]) ? want[f.Key] : ini[f.Key];
                Assert.True(f.Value == wanted, name + ": " + f.Key + " is " + f.Value + ", not " + wanted);
            }
        }

        private DefaultsFile OtherDefaultsFile()
        {
            string path = Path.Combine(scratch, "other-profile", "CameraUnlock", "Defaults.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, OtherDefaults, new System.Text.UTF8Encoding(false));
            return DefaultsFile.At(path);
        }

        // What a first start runs on under the given Defaults.ini.
        private HeadTrackingConfigData FreshUnder(DefaultsFile file)
        {
            Migration m = Migration.Run(Path.Combine(scratch, "fresh"), null, file);
            Assert.Equal(ConfigLoadStatus.Created, m.Loaded.Status);
            return m.Loaded.Config;
        }

        private LegacyFollowsDefaultsIni MapOf(byte[] legacy, List<DroppedValue> dropped)
        {
            LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", legacy));
            return LegacyMigration.Map(import.Values, Migration.Defaults(), dropped, new List<PoseShapingValue>());
        }

        private static string[] RowsOf(LegacyFollowsDefaultsIni follows)
        {
            return follows.Concepts.Select(c => "[" + c.Section + "] " + c.Key).ToArray();
        }

        // The legacy file with one key's value replaced.
        private static byte[] Edit(byte[] legacy, string key, string value)
        {
            string[] lines = System.Text.Encoding.ASCII.GetString(legacy).Split('\n');
            int hits = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(key + " = ", StringComparison.Ordinal)) continue;
                lines[i] = key + " = " + value + (lines[i].EndsWith("\r", StringComparison.Ordinal) ? "\r" : "");
                hits++;
            }
            if (hits != 1) throw new InvalidOperationException("v0.2.0's first run holds " + hits + " lines of " + key);
            return System.Text.Encoding.ASCII.GetBytes(string.Join("\n", lines));
        }

        /// <summary>Each row of a canonical file as "[Section] Key" to its value text.</summary>
        private static Dictionary<string, string> FileRows(string path)
        {
            var rows = new Dictionary<string, string>();
            string section = null;
            foreach (string line in File.ReadAllText(path).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(";", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    section = line;
                    continue;
                }
                int eq = line.IndexOf('=');
                rows[section + " " + line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            return rows;
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

        /// <summary>
        /// The rows left to Defaults.ini: every global row but the ones whose legacy key holds
        /// another value than v0.2.0 shipped.
        /// </summary>
        private static void CheckFollows(List<string> failures, string where, LegacyConfig l, LegacyFollowsDefaultsIni follows)
        {
            var shipped = new LegacyConfig();
            var changed = new List<string>();
            if (l.UdpPort != shipped.UdpPort) changed.Add("[Network] UdpPort");
            if (l.WorldSpaceYaw != shipped.WorldSpaceYaw) changed.Add("[General] WorldSpaceYaw");
            if (!LegacyReading.SameValue(l.LocalSmoothing, shipped.LocalSmoothing)) changed.Add("[Smoothing] LocalSmoothing");
            if (!LegacyReading.SameValue(l.RemoteSmoothing, shipped.RemoteSmoothing)) changed.Add("[Smoothing] RemoteSmoothing");
            if (l.ToggleKey != shipped.ToggleKey) changed.Add("[Hotkeys] ToggleKey");
            if (l.PositionToggleKey != shipped.PositionToggleKey) changed.Add("[Hotkeys] CycleTrackingModeKey");
            if (l.YawModeKey != shipped.YawModeKey) changed.Add("[Hotkeys] YawModeKey");
            string[] want = FollowingRows.Except(changed).ToArray();
            string[] got = RowsOf(follows);
            Check(failures, where, want.SequenceEqual(got), "follows Defaults.ini " + string.Join(", ", got));
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
