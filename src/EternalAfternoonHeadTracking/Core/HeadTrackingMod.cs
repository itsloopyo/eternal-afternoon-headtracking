using System;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using EternalAfternoonHeadTracking.Config;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Main head tracking MonoBehaviour - standalone version without BepInEx.
    /// Orchestrates UDP receiver, camera controller, and aim system components.
    /// </summary>
    public sealed class HeadTrackingMod : MonoBehaviour
    {
        public const string ModName = "Head Tracking";
        public const string ModVersion = "0.2.0";

        public static HeadTrackingMod Instance { get; private set; }

        private OpenTrackReceiver _receiver;
        private CameraController _cameraController;
        private AimController _aimController;
        private GameCrosshair _gameCrosshair;
        private bool _isEnabled;
        private TrackingMode _trackingMode;

        // Configuration
        private ConfigOwner<HeadTrackingConfigData> _configOwner;
        private HeadTrackingConfigData _config;
        private KeyBinding[] _toggleKeys;
        private KeyBinding[] _cycleTrackingModeKeys;
        private KeyBinding[] _yawModeKeys;

        // State
        private bool _wasConnected;
        private bool _aimSystemInitialized;
        private CameraTrackingHook _cameraHook;
        private Camera _cachedMainCamera;
        private int _cameraCheckCounter;
        private const int CameraCheckInterval = 30;

        private void Awake()
        {
            Instance = this;
            Log($"Initializing {ModName} v{ModVersion}...");

            LoadConfig();

            // Initialize components
            _receiver = new OpenTrackReceiver();
            _receiver.Log = Log;
            _receiver.Start(_config.UdpPort);

            var processor = new TrackingProcessor
            {
                LocalSmoothing = _config.LocalSmoothing,
                RemoteSmoothing = _config.RemoteSmoothing,
                Sensitivity = SensitivitySettings.Default,
                Deadzone = DeadzoneSettings.None
            };
            var interpolator = new PoseInterpolator();
            var positionProcessor = new PositionProcessor
            {
                TrackerPivotForward = 0.01f,
                // Every build before the canonical config shipped InvertPositionX = true with the
                // sensitivities at 1 and the other inversions off: the tracker's x arrives mirrored
                // against Unity's. Folded in here so the view moves as it did at those defaults.
                Settings = PositionSettings.Symmetric(
                    1f, 1f, 1f,
                    float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue,
                    _config.LocalSmoothing, _config.RemoteSmoothing,
                    invertX: true, invertY: false, invertZ: false
                )
            };
            var positionInterpolator = new PositionInterpolator();
            _cameraController = new CameraController(_receiver, processor, interpolator, positionProcessor, positionInterpolator)
            {
                WorldSpaceYaw = _config.WorldSpaceYaw,
            };
            TrackingMode? mode = TrackingModeChannels.Decode(_config.RotationEnabled, _config.PositionEnabled);
            if (!mode.HasValue)
            {
                throw new InvalidOperationException("the config table let through RotationEnabled=false and PositionEnabled=false");
            }
            ApplyTrackingMode(mode.Value);

            // Aim system will be initialized lazily in Update() to avoid early init issues
            _aimSystemInitialized = false;

            _isEnabled = _config.EnableOnStartup;

            Log($"{ModName} loaded! Port: {_config.UdpPort}, Toggle: {_config.ToggleKeyName}, tracking {(_isEnabled ? "on" : "off")} at startup");
        }

        /// <summary>
        /// Settings live in CameraUnlock.ini beside the mod's DLL, read and written by core's config
        /// owner, with rows set to default following the player's Defaults.ini. HeadTracking.cfg,
        /// which earlier builds read, is imported once while CameraUnlock.ini is absent and never
        /// written.
        /// </summary>
        private void LoadConfig()
        {
            string folder = Path.GetDirectoryName(typeof(HeadTrackingMod).Assembly.Location);
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidOperationException(
                    "Cannot resolve the config folder: Assembly.Location is empty. The mod assembly must be loaded from disk.");
            }

            _configOwner = new ConfigOwner<HeadTrackingConfigData>(
                ModConfig.Options(folder, DefaultsFile.PerUser(), message => Log("Config: " + message)));
            ConfigLoadResult<HeadTrackingConfigData> loaded = _configOwner.Load();
            foreach (string line in loaded.Log) Log(line);
            Log("Config: " + loaded.Status);
            _config = loaded.Config;

            _toggleKeys = ParseKeys("ToggleKey", _config.ToggleKeyName);
            _cycleTrackingModeKeys = ParseKeys("CycleTrackingModeKey", _config.CycleTrackingModeKeyName);
            _yawModeKeys = ParseKeys("YawModeKey", _config.YawModeKeyName);
        }

        // The table's hotkey codec refuses a list KeyBindings cannot read, so a loaded list always parses.
        private static KeyBinding[] ParseKeys(string row, string keyList)
        {
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(keyList, out bindings, out error))
            {
                throw new InvalidOperationException(row + "=" + keyList + " passed the config table and does not parse: " + error);
            }
            return bindings;
        }

        /// <summary>
        /// Called after a toggle has applied its new value. A save that fails is logged and the
        /// session keeps the new value.
        /// </summary>
        private void SaveConfig(Action<HeadTrackingConfigData> change)
        {
            ConfigSaveResult saved = _configOwner.Save(change);
            foreach (string line in saved.Log) Log(line);
            if (saved.Status != ConfigSaveStatus.Saved)
            {
                Log("Config not saved (" + saved.Status + "): " + saved.Reason + " The change applies to this session only.");
            }
        }

        private void Update()
        {
            // Lazy init aim system after game is loaded
            if (!_aimSystemInitialized && _cameraController != null)
            {
                InitializeAimSystem();
            }

            // Hotkey checks: Input.anyKeyDown short-circuits the key lookups on the
            // overwhelming majority of frames where no key transition occurs. Each action
            // fires on any binding in its key list, the Ctrl+Shift chord included.
            if (Input.anyKeyDown)
            {
                if (KeyBindingInput.IsTriggered(_toggleKeys))
                {
                    ToggleTracking();
                }

                if (KeyBindingInput.IsTriggered(_cycleTrackingModeKeys))
                {
                    CycleTrackingMode();
                }

                if (KeyBindingInput.IsTriggered(_yawModeKeys))
                {
                    ToggleYawMode();
                }
            }

            // Monitor connection state. Connection edges only log: the tracker app
            // owns centring, and the mod applies what it sends as absolute.
            bool isConnected = _receiver != null && _receiver.IsReceiving;
            if (isConnected != _wasConnected)
            {
                _wasConnected = isConnected;
                Log(isConnected ? "OpenTrack connected" : "OpenTrack disconnected");
            }
        }

        private void LateUpdate()
        {
            // Ensure camera hook is attached to the main camera.
            // The hook uses OnPreCull() which runs after all LateUpdate() calls,
            // ensuring Cinemachine's camera code can't overwrite our tracking rotation.

            // Fast path: cached camera still valid
            if (_cameraHook != null && _cachedMainCamera != null)
            {
                _cameraCheckCounter++;
                if (_cameraCheckCounter < CameraCheckInterval)
                    return;
                _cameraCheckCounter = 0;
            }

            // Slow path: validate or find camera via Camera.main
            Camera currentMain = Camera.main;
            if (currentMain == null) return;

            // Check if we need to attach hook to a new camera
            if (_cameraHook == null || _cachedMainCamera != currentMain)
            {
                if (_cameraHook != null)
                {
                    Destroy(_cameraHook);
                    _cameraHook = null;
                }

                _cachedMainCamera = currentMain;
                _cameraCheckCounter = 0;

                _cameraHook = _cachedMainCamera.gameObject.AddComponent<CameraTrackingHook>();
                _cameraHook.Initialize(_cameraController, _aimController, _gameCrosshair, _receiver);
                _cameraHook.SetEnabled(_isEnabled);
                Log($"Attached CameraTrackingHook to camera: {_cachedMainCamera.name}");
            }
        }

        private void InitializeAimSystem()
        {
            if (_cameraController == null) return;

            _aimController = new AimController();
            _gameCrosshair = new GameCrosshair();

            // Update hook with aim components
            if (_cameraHook != null)
            {
                _cameraHook.SetAimComponents(_aimController, _gameCrosshair);
            }

            _aimSystemInitialized = true;
        }

        /// <summary>The master on/off. It changes this session only and never writes the config.</summary>
        public void ToggleTracking()
        {
            _isEnabled = !_isEnabled;
            Log(_isEnabled ? "Tracking enabled" : "Tracking disabled");

            if (_cameraHook != null)
            {
                _cameraHook.SetEnabled(_isEnabled);
            }

            if (!_isEnabled)
            {
                _cameraController?.ResetCamera();
                _gameCrosshair?.ResetPosition();
            }
        }

        /// <summary>
        /// Rotation and position, then rotation only, then position only. Saved as the
        /// RotationEnabled/PositionEnabled pair.
        /// </summary>
        private void CycleTrackingMode()
        {
            TrackingMode next;
            switch (_trackingMode)
            {
                case TrackingMode.RotationAndPosition: next = TrackingMode.RotationOnly; break;
                case TrackingMode.RotationOnly: next = TrackingMode.PositionOnly; break;
                default: next = TrackingMode.RotationAndPosition; break;
            }
            ApplyTrackingMode(next);

            bool rotationEnabled, positionEnabled;
            TrackingModeChannels.Encode(next, out rotationEnabled, out positionEnabled);
            SaveConfig(c =>
            {
                c.RotationEnabled = rotationEnabled;
                c.PositionEnabled = positionEnabled;
            });
        }

        private void ApplyTrackingMode(TrackingMode mode)
        {
            _trackingMode = mode;
            switch (mode)
            {
                case TrackingMode.RotationAndPosition:
                    _cameraController.RotationEnabled = true;
                    _cameraController.PositionEnabled = true;
                    Log("Tracking mode: rotation + position");
                    break;
                case TrackingMode.RotationOnly:
                    _cameraController.RotationEnabled = true;
                    _cameraController.PositionEnabled = false;
                    Log("Tracking mode: rotation only (position disabled)");
                    break;
                case TrackingMode.PositionOnly:
                    _cameraController.RotationEnabled = false;
                    _cameraController.PositionEnabled = true;
                    Log("Tracking mode: position only (rotation disabled)");
                    break;
            }
        }

        /// <summary>World-locked or camera-local yaw. Saved as WorldSpaceYaw.</summary>
        public void ToggleYawMode()
        {
            bool worldSpaceYaw = !_cameraController.WorldSpaceYaw;
            _cameraController.WorldSpaceYaw = worldSpaceYaw;
            Log($"Yaw mode: {(worldSpaceYaw ? "world-space (horizon-locked)" : "camera-local")}");
            SaveConfig(c => c.WorldSpaceYaw = worldSpaceYaw);
        }

        private void OnDestroy()
        {
            if (_cameraHook != null)
            {
                Destroy(_cameraHook);
                _cameraHook = null;
            }

            _gameCrosshair?.ResetPosition();
            _receiver?.Dispose();
            _cameraController?.ResetCamera();
            Instance = null;

            ModLoader.ScheduleRecreate();
        }

        private static void Log(string message)
        {
            ModLoader.Log($"[Mod] {message}");
        }
    }
}
