using System;
using System.IO;

namespace EternalAfternoonHeadTracking.Tests.ConfigDifferential
{
    internal static class RepoPaths
    {
        /// <summary>The repo checkout the test assembly was built from.</summary>
        public static readonly string Root = Find();

        private static string Find()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "pixi.toml")) && Directory.Exists(Path.Combine(dir, "src", "EternalAfternoonHeadTracking")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            throw new InvalidOperationException("no repo root above " + AppDomain.CurrentDomain.BaseDirectory);
        }

        /// <summary>A new empty folder under the system temp folder.</summary>
        public static string Scratch()
        {
            string dir = Path.Combine(Path.GetTempPath(), "EternalAfternoonHeadTracking.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
