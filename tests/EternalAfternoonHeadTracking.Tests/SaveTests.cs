using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CameraUnlock.Core.Config;
using EternalAfternoonHeadTracking.Tests.ConfigDifferential;
using Xunit;

namespace EternalAfternoonHeadTracking.Tests
{
    /// <summary>
    /// The toggles that persist: the tracking mode writes the RotationEnabled and PositionEnabled
    /// pair and the yaw mode writes WorldSpaceYaw, each changing its own lines and no other byte,
    /// and never HeadTracking.cfg. End saves nothing and has no Writable row.
    /// </summary>
    public class SaveTests : IDisposable
    {
        private readonly string scratch = RepoPaths.Scratch();
        private readonly DefaultsFile defaults;

        public SaveTests()
        {
            defaults = Migration.ScratchDefaults(scratch);
        }

        public void Dispose()
        {
            Directory.Delete(scratch, true);
        }

        [Fact]
        public void FirstStartCreatesTheCommittedFile()
        {
            Migration m = Migration.Run(Path.Combine(scratch, "game"), null, defaults);
            Assert.Equal(ConfigLoadStatus.Created, m.Loaded.Status);
            Assert.Equal(File.ReadAllBytes(RenderTests.CommittedPath), File.ReadAllBytes(m.ConfigPath));
            Assert.False(File.Exists(m.LegacyPath));
        }

        [Fact]
        public void YawModeSaveWritesItsValueOverDefault()
        {
            Migration m = Migration.Run(Path.Combine(scratch, "game"), null, defaults);
            string[] before = Lines(m.ConfigPath);

            ConfigSaveResult saved = m.Owner.Save(c => c.WorldSpaceYaw = false);

            Assert.Equal(ConfigSaveStatus.Saved, saved.Status);
            Assert.Contains(saved.Log, l => l.Contains("WorldSpaceYaw=false is now set for this game, and no longer follows Defaults.ini."));
            Assert.Equal(new[] { "WorldSpaceYaw=default -> WorldSpaceYaw=false" }, Changed(before, Lines(m.ConfigPath)));
            Assert.False(Reopen(m).WorldSpaceYaw);
        }

        [Fact]
        public void TrackingModeSaveWritesThePair()
        {
            Migration m = Migration.Run(Path.Combine(scratch, "game"), null, defaults);
            string[] before = Lines(m.ConfigPath);

            ConfigSaveResult saved = m.Owner.Save(c =>
            {
                c.RotationEnabled = false;
                c.PositionEnabled = true;
            });

            Assert.Equal(ConfigSaveStatus.Saved, saved.Status);
            Assert.Equal(new[] { "RotationEnabled=default -> RotationEnabled=false", "PositionEnabled=default -> PositionEnabled=true" },
                Changed(before, Lines(m.ConfigPath)));
            HeadTrackingConfigData next = Reopen(m);
            Assert.False(next.RotationEnabled);
            Assert.True(next.PositionEnabled);
        }

        [Fact]
        public void SaveAfterAnImportChangesOnlyItsRowAndNeverTheLegacyFile()
        {
            byte[] legacy = MigrationTests.Edited();
            Migration m = Migration.Run(Path.Combine(scratch, "game"), legacy, defaults);
            Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
            Assert.False(m.Loaded.Config.WorldSpaceYaw);
            Assert.Equal(5555, m.Loaded.Config.UdpPort);
            Assert.Equal("F8, Ctrl+Shift+Y", m.Loaded.Config.ToggleKeyName);
            Assert.Equal("Ctrl+Shift+G", m.Loaded.Config.CycleTrackingModeKeyName);
            string[] before = Lines(m.ConfigPath);

            Assert.Equal(ConfigSaveStatus.Saved, m.Owner.Save(c => c.WorldSpaceYaw = true).Status);

            Assert.Equal(new[] { "WorldSpaceYaw=false -> WorldSpaceYaw=true" }, Changed(before, Lines(m.ConfigPath)));
            Assert.Equal(legacy, File.ReadAllBytes(m.LegacyPath));
        }

        [Fact]
        public void OnlyTheToggledRowsAreWritable()
        {
            Migration m = Migration.Run(Path.Combine(scratch, "game"), null, defaults);
            byte[] before = File.ReadAllBytes(m.ConfigPath);
            Assert.ThrowsAny<InvalidOperationException>(() => m.Owner.Save(c => c.EnableOnStartup = false));
            Assert.ThrowsAny<InvalidOperationException>(() => m.Owner.Save(c => c.UdpPort = 5555));
            Assert.Equal(before, File.ReadAllBytes(m.ConfigPath));
        }

        private HeadTrackingConfigData Reopen(Migration m)
        {
            ConfigLoadResult<HeadTrackingConfigData> loaded = Migration.Reopen(Path.GetDirectoryName(m.ConfigPath), defaults).Load();
            Assert.Equal(ConfigLoadStatus.Canonical, loaded.Status);
            return loaded.Config;
        }

        private static string[] Lines(string path)
        {
            return Encoding.ASCII.GetString(File.ReadAllBytes(path)).Split(new[] { "\r\n" }, StringSplitOptions.None);
        }

        private static string[] Changed(string[] before, string[] after)
        {
            Assert.Equal(before.Length, after.Length);
            var changed = new List<string>();
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i] != after[i]) changed.Add(before[i] + " -> " + after[i]);
            }
            return changed.ToArray();
        }
    }
}
