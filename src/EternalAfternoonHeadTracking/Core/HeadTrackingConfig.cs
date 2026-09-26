using System;
using System.IO;

using CameraUnlock.Core.Protocol;
using EternalAfternoonHeadTracking.Legacy;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Configuration for head tracking mod.
    /// Loaded from HeadTracking.cfg file if present.
    /// </summary>
    public sealed class HeadTrackingConfig
    {
        // Network
        public int UdpPort { get; set; } = OpenTrackReceiver.DefaultPort;

        // Sensitivity
        public float YawSensitivity { get; set; } = 1.0f;
        public float PitchSensitivity { get; set; } = 1.0f;
        public float RollSensitivity { get; set; } = 1.0f;

        // Smoothing. Selected per connection from the tracker's source address:
        // a tracker on this machine uses LocalSmoothing, a remote network device
        // uses RemoteSmoothing. Both cover rotation and position.
        public float LocalSmoothing { get; set; } = CameraUnlock.Core.Math.SmoothingUtils.DefaultLocalSmoothing;
        public float RemoteSmoothing { get; set; } = CameraUnlock.Core.Math.SmoothingUtils.DefaultRemoteSmoothing;

        // Hotkeys
        public KeyCode ToggleKey { get; set; } = KeyCode.End;
        public KeyCode PositionToggleKey { get; set; } = KeyCode.PageUp;
        public KeyCode ReticleToggleKey { get; set; } = KeyCode.Insert;
        public KeyCode YawModeKey { get; set; } = KeyCode.PageDown;

        // Yaw mode: true = world-space (horizon-locked), false = camera-local
        public bool WorldSpaceYaw { get; set; } = true;

        // Position tracking
        public float PositionSensitivityX { get; set; } = 1.0f;
        public float PositionSensitivityY { get; set; } = 1.0f;
        public float PositionSensitivityZ { get; set; } = 1.0f;
        public bool InvertPositionX { get; set; } = true;
        public bool InvertPositionY { get; set; } = false;
        /// Renamed from InvertPositionZ, which every existing config file carries as true.
        /// It used to double as the flip into Unity's +z-forward space, a job the camera
        /// controller now does at the engine boundary; left in place it would invert the
        /// lean. The key has to change so existing files fall back to this default.
        public bool InvertTrackerZ { get; set; } = false;

        // Aim decoupling
        public bool ShowReticle { get; set; } = true;
        public Color ReticleColor { get; set; } = Color.white;

        public static HeadTrackingConfig LoadFromFile(string configPath, Action<string> log = null)
        {
            if (!File.Exists(configPath))
            {
                WriteDefaults(configPath, log);
                return FromLegacy(new LegacyConfig());
            }
            return FromLegacy(LegacyConfigReader.Read(configPath, log));
        }

        private static HeadTrackingConfig FromLegacy(LegacyConfig legacy)
        {
            return new HeadTrackingConfig
            {
                UdpPort = legacy.UdpPort,
                YawSensitivity = legacy.YawSensitivity,
                PitchSensitivity = legacy.PitchSensitivity,
                RollSensitivity = legacy.RollSensitivity,
                LocalSmoothing = legacy.LocalSmoothing,
                RemoteSmoothing = legacy.RemoteSmoothing,
                ToggleKey = legacy.ToggleKey,
                PositionToggleKey = legacy.PositionToggleKey,
                ReticleToggleKey = legacy.ReticleToggleKey,
                YawModeKey = legacy.YawModeKey,
                WorldSpaceYaw = legacy.WorldSpaceYaw,
                PositionSensitivityX = legacy.PositionSensitivityX,
                PositionSensitivityY = legacy.PositionSensitivityY,
                PositionSensitivityZ = legacy.PositionSensitivityZ,
                InvertPositionX = legacy.InvertPositionX,
                InvertPositionY = legacy.InvertPositionY,
                InvertTrackerZ = legacy.InvertTrackerZ,
                ShowReticle = legacy.ShowReticle,
                ReticleColor = legacy.ReticleColor,
            };
        }

        private static void WriteDefaults(string configPath, Action<string> log)
        {
            try
            {
                File.WriteAllText(configPath,
                    "# Eternal Afternoon Head Tracking Configuration\n" +
                    "# Edit values below and restart the game to apply changes.\n" +
                    "# Lines starting with # or ; are comments.\n" +
                    "\n" +
                    "# --- Network ---\n" +
                    "UdpPort = 4242\n" +
                    "\n" +
                    "# --- Keybindings ---\n" +
                    "# See https://docs.unity3d.com/ScriptReference/KeyCode.html for key names\n" +
                    "ToggleKey = End\n" +
                    "PositionToggleKey = PageUp\n" +
                    "ReticleToggleKey = Insert\n" +
                    "YawModeKey = PageDown\n" +
                    "\n" +
                    "# --- Yaw Mode ---\n" +
                    "# true = horizon-locked yaw (default), false = camera-local yaw.\n" +
                    "# Horizon-locked keeps yaw around the world up-axis even when looking up/down.\n" +
                    "WorldSpaceYaw = true\n" +
                    "\n" +
                    "# --- Sensitivity ---\n" +
                    "YawSensitivity = 1.0\n" +
                    "PitchSensitivity = 1.0\n" +
                    "RollSensitivity = 1.0\n" +
                    "\n" +
                    "# --- Smoothing ---\n" +
                    "# Picked per connection from the tracker's source address. Both values\n" +
                    "# cover rotation and position. 0.0 = no smoothing, 1.0 = heavy.\n" +
                    "# LocalSmoothing: tracker running on this machine (loopback).\n" +
                    "# RemoteSmoothing: tracker on a remote device over the network.\n" +
                    "LocalSmoothing = 0.0\n" +
                    "RemoteSmoothing = 0.15\n" +
                    "\n" +
                    "# --- Position Tracking ---\n" +
                    "PositionSensitivityX = 1.0\n" +
                    "PositionSensitivityY = 1.0\n" +
                    "PositionSensitivityZ = 1.0\n" +
                    "InvertPositionX = true\n" +
                    "InvertPositionY = false\n" +
                    "InvertTrackerZ = false\n" +
                    "\n" +
                    "# --- Reticle ---\n" +
                    "ShowReticle = true\n" +
                    "ReticleColor = 1.0,1.0,1.0,1.0\n");
                log?.Invoke("Created default HeadTracking.cfg");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Could not create default config: {ex.Message}");
            }
        }

        public static string GetDefaultConfigPath()
        {
            string assemblyDir = Path.GetDirectoryName(typeof(HeadTrackingConfig).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                // A null/empty Assembly.Location (e.g. assembly loaded from a byte
                // array) would cause the cfg to be read from / written to the game
                // process's CWD silently. Fail-fast instead so the misconfiguration
                // is visible.
                throw new InvalidOperationException(
                    "Cannot resolve config path: Assembly.Location is empty. The mod assembly must be loaded from disk.");
            }
            return Path.Combine(assemblyDir, "HeadTracking.cfg");
        }
    }
}
