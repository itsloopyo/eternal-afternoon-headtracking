using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CameraUnlock.Core.Protocol;
using Xunit;

namespace EternalAfternoonHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Comparison 1: the newest published build's reader (the oracle) against the frozen reader
    /// (the import), over every input. What differs is what players see change that the
    /// conversion did not cause; there is nothing, since only f74f9d2 touched the reader after
    /// v0.2.0 and it restated the smoothing defaults as core's constants of the same value.
    /// </summary>
    public class DifferentialTests
    {
        // The oracle is v0.2.0's own reader, byte for byte; `git show v0.2.0:<path> | sha256sum`.
        private static readonly Dictionary<string, string> OracleHashes = new Dictionary<string, string>
        {
            { "tests/config_differential/Oracle/HeadTrackingConfig.cs", "7df44ebe6d3905541f1185a5fe37bb83603c8812b130883e99ea75f51cdfe0c6" },
        };

        // The frozen import. A change to either file changes how players' legacy files are read.
        private static readonly Dictionary<string, string> FrozenHashes = new Dictionary<string, string>
        {
            { "src/EternalAfternoonHeadTracking/Legacy/LegacyConfig.cs", "e4fb24ce10cd726ae766d8d822e8e010e9d7f3141790369c5252b0dcb79fe9a9" },
            { "src/EternalAfternoonHeadTracking/Legacy/LegacyConfigReader.cs", "f05cb3b6114c215e6710a85f39ee10c45517c7fa32f97cbf1c67759676a7e4ae" },
        };

        [Fact]
        public void OracleIsThePublishedReader()
        {
            foreach (KeyValuePair<string, string> file in OracleHashes)
            {
                Assert.Equal(file.Value, Sha256(file.Key));
            }
        }

        [Fact]
        public void FrozenImportIsUnchanged()
        {
            foreach (KeyValuePair<string, string> file in FrozenHashes)
            {
                Assert.Equal(file.Value, Sha256(file.Key));
            }
        }

        /// <summary>
        /// The one core symbol the oracle compiles is OpenTrackReceiver.DefaultPort, the UdpPort
        /// default. v0.2.0 was built on core 3465659, where it is 4242.
        /// </summary>
        [Fact]
        public void OracleDefaultPortIsThePublishedOne()
        {
            Assert.Equal(4242, OpenTrackReceiver.DefaultPort);
        }

        [Fact]
        public void ComparisonOne()
        {
            string dir = RepoPaths.Scratch();
            var unexpected = new List<string>();
            int inputs = 0;
            foreach (KeyValuePair<string, byte[]> input in Corpus.Inputs())
            {
                inputs++;
                string oraclePath = Place(dir, "oracle", input.Value);
                string importPath = Place(dir, "import", input.Value);
                DateTime written = input.Value == null ? default(DateTime) : File.GetLastWriteTimeUtc(importPath);

                LegacyReading oracle = LegacyReading.Oracle(oraclePath);
                LegacyReading import = LegacyReading.Import(importPath);

                if (input.Value == null)
                {
                    Assert.False(File.Exists(importPath), input.Key + ": the frozen reader created the file");
                }
                else
                {
                    Assert.True(File.ReadAllBytes(importPath).SequenceEqual(input.Value), input.Key + ": the frozen reader changed the file");
                    Assert.Equal(written, File.GetLastWriteTimeUtc(importPath));
                }

                List<string> differences = LegacyReading.Differences(oracle, import);
                if (differences.Count != 0)
                {
                    unexpected.Add(input.Key + ": " + string.Join("; ", differences.ToArray()));
                }
            }
            Directory.Delete(dir, true);
            Assert.True(unexpected.Count == 0, string.Join("\n", unexpected.Take(40).ToArray()));
            Assert.True(inputs > 500, "the corpus produced " + inputs + " inputs");
        }

        internal static string Place(string dir, string name, byte[] bytes)
        {
            string folder = Path.Combine(dir, name);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "HeadTracking.cfg");
            if (bytes != null) File.WriteAllBytes(path, bytes);
            return path;
        }

        internal static string Sha256(string repoPath)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(RepoPaths.Root, repoPath.Replace('/', Path.DirectorySeparatorChar)));
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte b in sha.ComputeHash(bytes)) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
