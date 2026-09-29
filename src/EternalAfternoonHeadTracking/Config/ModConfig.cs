using System;
using System.IO;
using CameraUnlock.Core.Config;

namespace EternalAfternoonHeadTracking.Config
{
    /// <summary>
    /// The settings in CameraUnlock.ini, beside the mod's DLL in Eternal Afternoon_Data\Managed,
    /// and the owner that reads and writes them. HeadTracking.cfg beside it, which earlier builds
    /// read, is imported once while CameraUnlock.ini is absent and never written.
    /// </summary>
    public static class ModConfig
    {
        public const string DisplayName = "Eternal Afternoon";
        public const string FileName = "CameraUnlock.ini";
        public const string LegacyFileName = "HeadTracking.cfg";

        public static ConfigTable<HeadTrackingConfigData> Table()
        {
            return HeadTrackingConfigTable.Create(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.PositionLimitX,
                    ConfigConcepts.PositionLimitY,
                    ConfigConcepts.PositionLimitYDown,
                    ConfigConcepts.PositionLimitZ,
                    ConfigConcepts.PositionLimitZBack,
                    ConfigConcepts.CollisionEnabled,
                    ConfigConcepts.CollisionMargin,
                    ConfigConcepts.CollisionReleaseSmoothing,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable()
                .Select(ConfigConcepts.CollisionMargin)
                .Comment("How far, in metres, the view is held off a wall when you lean into it.\n" +
                         "The mod raises it where the corners of the camera's near clip plane need more room.");
        }

        /// <summary>
        /// The owner's options for the files in <paramref name="folder"/>. The mod passes
        /// <see cref="DefaultsFile.PerUser"/>, a test a scratch file.
        /// </summary>
        public static ConfigOwnerOptions<HeadTrackingConfigData> Options(string folder, DefaultsFile defaults, Action<string> statusSink)
        {
            return new ConfigOwnerOptions<HeadTrackingConfigData>
            {
                Path = Path.Combine(folder, FileName),
                Table = Table(),
                Header = new RenderHeader(DisplayName),
                Import = LegacyMigration.Import(),
                LegacySourcePath = Path.Combine(folder, LegacyFileName),
                Defaults = defaults,
                StatusSink = statusSink,
            };
        }
    }
}
