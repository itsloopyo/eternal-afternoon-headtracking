using UnityEngine;

namespace EternalAfternoonHeadTracking.Legacy
{
    /// <summary>
    /// The settings v0.2.0 read from HeadTracking.cfg, with the defaults it ran on where the file
    /// had no usable value. Frozen: a default the runtime moves later changes what a new file
    /// holds, never what an old file without the key meant.
    /// </summary>
    public sealed class LegacyConfig
    {
        // Network
        public int UdpPort = 4242;

        // Sensitivity
        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;
        public float RollSensitivity = 1.0f;

        // Smoothing
        public float LocalSmoothing = 0.0f;
        public float RemoteSmoothing = 0.15f;

        // Hotkeys
        public KeyCode ToggleKey = KeyCode.End;
        public KeyCode PositionToggleKey = KeyCode.PageUp;
        public KeyCode ReticleToggleKey = KeyCode.Insert;
        public KeyCode YawModeKey = KeyCode.PageDown;

        // Yaw mode
        public bool WorldSpaceYaw = true;

        // Position tracking
        public float PositionSensitivityX = 1.0f;
        public float PositionSensitivityY = 1.0f;
        public float PositionSensitivityZ = 1.0f;
        public bool InvertPositionX = true;
        public bool InvertPositionY = false;
        public bool InvertTrackerZ = false;

        // Aim decoupling
        public bool ShowReticle = true;
        public Color ReticleColor = Color.white;
    }
}
