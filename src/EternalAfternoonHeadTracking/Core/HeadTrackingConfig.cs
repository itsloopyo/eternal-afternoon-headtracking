using System;
using System.Globalization;
using System.IO;

using CameraUnlock.Core.Protocol;
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
        public float LocalSmoothing { get; set; } = 0.0f;
        public float RemoteSmoothing { get; set; } = 0.15f;

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
            var config = new HeadTrackingConfig();

            try
            {
                if (!File.Exists(configPath))
                {
                    WriteDefaults(configPath, log);
                    return config;
                }

                foreach (string line in File.ReadAllLines(configPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                        continue;

                    int eqIndex = trimmed.IndexOf('=');
                    if (eqIndex <= 0) continue;

                    string key = trimmed.Substring(0, eqIndex).Trim().ToLowerInvariant();
                    string value = trimmed.Substring(eqIndex + 1).Trim();

                    switch (key)
                    {
                        case "udpport":
                            // Validate range: a UDP port must fit in 1..65535. Out-of-range
                            // values would otherwise propagate to OpenTrackReceiver.Start()
                            // and throw an ArgumentOutOfRangeException at startup.
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
                                && port >= 1 && port <= 65535)
                            {
                                config.UdpPort = port;
                            }
                            else
                            {
                                log?.Invoke($"Invalid UdpPort value '{value}' (must be 1-65535) - using default {config.UdpPort}");
                            }
                            break;
                        case "yawsensitivity":
                            if (TryParseFiniteFloat(value, out float yaw))
                                config.YawSensitivity = yaw;
                            break;
                        case "pitchsensitivity":
                            if (TryParseFiniteFloat(value, out float pitch))
                                config.PitchSensitivity = pitch;
                            break;
                        case "rollsensitivity":
                            if (TryParseFiniteFloat(value, out float roll))
                                config.RollSensitivity = roll;
                            break;
                        case "localsmoothing":
                            if (TryParseFiniteFloat(value, out float localSmoothing))
                                config.LocalSmoothing = Math.Max(0f, Math.Min(1f, localSmoothing));
                            break;
                        case "remotesmoothing":
                            if (TryParseFiniteFloat(value, out float remoteSmoothing))
                                config.RemoteSmoothing = Math.Max(0f, Math.Min(1f, remoteSmoothing));
                            break;
                        case "togglekey":
                            if (TryParseKeyCode(value, out KeyCode kToggle)) config.ToggleKey = kToggle;
                            else log?.Invoke($"Invalid ToggleKey value '{value}' - using default {config.ToggleKey}");
                            break;
                        case "positiontogglekey":
                            if (TryParseKeyCode(value, out KeyCode kPosition)) config.PositionToggleKey = kPosition;
                            else log?.Invoke($"Invalid PositionToggleKey value '{value}' - using default {config.PositionToggleKey}");
                            break;
                        case "reticletogglekey":
                            if (TryParseKeyCode(value, out KeyCode kReticle)) config.ReticleToggleKey = kReticle;
                            else log?.Invoke($"Invalid ReticleToggleKey value '{value}' - using default {config.ReticleToggleKey}");
                            break;
                        case "yawmodekey":
                            if (TryParseKeyCode(value, out KeyCode kYawMode)) config.YawModeKey = kYawMode;
                            else log?.Invoke($"Invalid YawModeKey value '{value}' - using default {config.YawModeKey}");
                            break;
                        case "worldspaceyaw":
                            if (bool.TryParse(value, out bool worldYaw))
                                config.WorldSpaceYaw = worldYaw;
                            break;
                        case "positionsensitivityx":
                            if (TryParseFiniteFloat(value, out float posX))
                                config.PositionSensitivityX = posX;
                            break;
                        case "positionsensitivityy":
                            if (TryParseFiniteFloat(value, out float posY))
                                config.PositionSensitivityY = posY;
                            break;
                        case "positionsensitivityz":
                            if (TryParseFiniteFloat(value, out float posZ))
                                config.PositionSensitivityZ = posZ;
                            break;
                        case "invertpositionx":
                            if (bool.TryParse(value, out bool invX))
                                config.InvertPositionX = invX;
                            break;
                        case "invertpositiony":
                            if (bool.TryParse(value, out bool invY))
                                config.InvertPositionY = invY;
                            break;
                        case "inverttrackerz":
                            if (bool.TryParse(value, out bool invZ))
                                config.InvertTrackerZ = invZ;
                            break;
                        case "showreticle":
                            if (bool.TryParse(value, out bool show))
                                config.ShowReticle = show;
                            break;
                        case "reticlecolor":
                            config.ReticleColor = ParseColor(value);
                            break;
                    }
                }

                log?.Invoke("Config loaded from HeadTracking.cfg");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Config load error (using defaults): {ex.Message}");
            }

            return config;
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

        // Preserves the original validation: Enum.IsDefined(typeof(KeyCode), string) only
        // matches exact-case member names (e.g. "Home" but not "home"), so we keep that
        // strictness rather than switching to Enum.TryParse which would loosen it.
        private static bool TryParseKeyCode(string value, out KeyCode result)
        {
            if (Enum.IsDefined(typeof(KeyCode), value))
            {
                result = (KeyCode)Enum.Parse(typeof(KeyCode), value, true);
                return true;
            }
            result = default;
            return false;
        }

        // The cfg file is authored with '.' as the decimal separator. Parsing without
        // CultureInfo.InvariantCulture would fail silently on systems where the
        // current culture uses ',' (e.g. de-DE, fr-FR), and every numeric setting
        // would silently fall back to its default. NaN/Infinity are also rejected so
        // a malformed value can't poison downstream math.
        private static bool TryParseFiniteFloat(string value, out float result)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                && !float.IsNaN(result) && !float.IsInfinity(result))
            {
                return true;
            }
            result = 0f;
            return false;
        }

        private static Color ParseColor(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 3)
                return Color.white;

            float r = 1f, g = 1f, b = 1f, a = 1f;
            if (TryParseFiniteFloat(parts[0].Trim(), out float parsedR)) r = Mathf.Clamp01(parsedR);
            if (TryParseFiniteFloat(parts[1].Trim(), out float parsedG)) g = Mathf.Clamp01(parsedG);
            if (TryParseFiniteFloat(parts[2].Trim(), out float parsedB)) b = Mathf.Clamp01(parsedB);
            if (parts.Length >= 4 && TryParseFiniteFloat(parts[3].Trim(), out float parsedA)) a = Mathf.Clamp01(parsedA);

            return new Color(r, g, b, a);
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
