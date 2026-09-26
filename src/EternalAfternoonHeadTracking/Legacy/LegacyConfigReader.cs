using System;
using System.Globalization;
using System.IO;

using CameraUnlock.Core.Config;
using UnityEngine;

namespace EternalAfternoonHeadTracking.Legacy
{
    /// <summary>
    /// v0.2.0's HeadTracking.cfg reader, frozen so a player's file is read exactly as the build
    /// they updated from read it. It differs from that build in two ways only: it fills
    /// <see cref="LegacyConfig"/>, and it writes nothing, so a missing file reads as the defaults
    /// instead of being created. Never edit it: tests/config_differential pins its bytes.
    /// </summary>
    public static class LegacyConfigReader
    {
        /// <summary>
        /// Every key the reader reads. It ignores sections, matches a key in any letter case and
        /// keeps the last line of a key.
        /// </summary>
        public static readonly LegacyKey[] Keys =
        {
            new LegacyKey("", "UdpPort"),
            new LegacyKey("", "YawSensitivity"),
            new LegacyKey("", "PitchSensitivity"),
            new LegacyKey("", "RollSensitivity"),
            new LegacyKey("", "LocalSmoothing"),
            new LegacyKey("", "RemoteSmoothing"),
            new LegacyKey("", "ToggleKey"),
            new LegacyKey("", "PositionToggleKey"),
            new LegacyKey("", "ReticleToggleKey"),
            new LegacyKey("", "YawModeKey"),
            new LegacyKey("", "WorldSpaceYaw"),
            new LegacyKey("", "PositionSensitivityX"),
            new LegacyKey("", "PositionSensitivityY"),
            new LegacyKey("", "PositionSensitivityZ"),
            new LegacyKey("", "InvertPositionX"),
            new LegacyKey("", "InvertPositionY"),
            new LegacyKey("", "InvertTrackerZ"),
            new LegacyKey("", "ShowReticle"),
            new LegacyKey("", "ReticleColor"),
        };

        public static LegacyConfig Read(string configPath, Action<string> log = null)
        {
            var config = new LegacyConfig();

            try
            {
                if (!File.Exists(configPath))
                {
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
    }
}
