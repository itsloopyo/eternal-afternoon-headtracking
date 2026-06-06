using System;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Extensions;
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
        public const string ModVersion = "0.1.2";

        public static HeadTrackingMod Instance { get; private set; }

        private enum TrackingMode { Both, RotationOnly, PositionOnly }

        private OpenTrackReceiver _receiver;
        private CameraController _cameraController;
        private AimController _aimController;
        private GameCrosshair _gameCrosshair;
        private bool _isEnabled;
        private TrackingMode _trackingMode = TrackingMode.Both;

        // Configuration
        private HeadTrackingConfig _config;

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

            // Load config
            _config = HeadTrackingConfig.LoadFromFile(HeadTrackingConfig.GetDefaultConfigPath(), Log);

            // Initialize components
            _receiver = new OpenTrackReceiver();
            _receiver.Log = Log;
            _receiver.Start(_config.UdpPort);

            var processor = new TrackingProcessor
            {
                SmoothingFactor = _config.Smoothing,
                Sensitivity = new SensitivitySettings(
                    _config.YawSensitivity,
                    _config.PitchSensitivity,
                    _config.RollSensitivity,
                    invertYaw: false, invertPitch: false, invertRoll: false
                ),
                Deadzone = DeadzoneSettings.None
            };
            var interpolator = new PoseInterpolator();
            var positionProcessor = new PositionProcessor
            {
                TrackerPivotForward = 0.01f,
                Settings = new PositionSettings(
                    _config.PositionSensitivityX, _config.PositionSensitivityY, _config.PositionSensitivityZ,
                    float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue,
                    0.15f,
                    invertX: _config.InvertPositionX, invertY: _config.InvertPositionY, invertZ: _config.InvertPositionZ
                )
            };
            var positionInterpolator = new PositionInterpolator();
            _cameraController = new CameraController(_receiver, processor, interpolator, positionProcessor, positionInterpolator)
            {
                WorldSpaceYaw = _config.WorldSpaceYaw,
            };

            // Aim system will be initialized lazily in Update() to avoid early init issues
            _aimSystemInitialized = false;

            _isEnabled = true;

            Log($"{ModName} loaded! Port: {_config.UdpPort}, Toggle: {_config.ToggleKey}, Recenter: {_config.RecenterKey}");
        }

        private void Update()
        {
            // Lazy init aim system after game is loaded
            if (!_aimSystemInitialized && _cameraController != null)
            {
                InitializeAimSystem();
            }

            // Hotkey checks: Input.anyKeyDown short-circuits four dictionary lookups on the
            // overwhelming majority of frames where no key transition occurs.
            // Two equivalent binding sets per the project standard: the configurable
            // nav-cluster key, OR the fixed Ctrl+Shift chord from ChordHotkeys.
            if (Input.anyKeyDown)
            {
                if (ChordHotkeys.IsActionPressed(_config.RecenterKey, ChordHotkeys.RecenterLetter))
                {
                    Recenter();
                }

                if (ChordHotkeys.IsActionPressed(_config.ToggleKey, ChordHotkeys.ToggleLetter))
                {
                    ToggleTracking();
                }

                if (ChordHotkeys.IsActionPressed(_config.PositionToggleKey, ChordHotkeys.PositionLetter))
                {
                    CycleTrackingMode();
                }

                // Yaw mode takes the 4th chord slot per the project's standard action
                // order (Recenter/Toggle/Position/Yaw). Reticle is a non-standard extra
                // for this mod and bumps to the 5th slot.
                if (ChordHotkeys.IsActionPressed(_config.YawModeKey, ChordHotkeys.FourthToggleLetter))
                {
                    ToggleYawMode();
                }

                if (ChordHotkeys.IsActionPressed(_config.ReticleToggleKey, ChordHotkeys.FifthToggleLetter))
                {
                    _config.ShowReticle = !_config.ShowReticle;
                    if (_cameraHook != null)
                        _cameraHook.ShowReticle = _config.ShowReticle;
                    if (!_config.ShowReticle)
                        _gameCrosshair?.ResetPosition();
                    Log($"Reticle {(_config.ShowReticle ? "shown" : "hidden")}");
                }
            }

            // Monitor connection state
            bool isConnected = _receiver != null && _receiver.IsReceiving;
            if (isConnected != _wasConnected)
            {
                _wasConnected = isConnected;
                Log(isConnected ? "OpenTrack connected" : "OpenTrack disconnected");

                if (isConnected)
                {
                    Recenter();
                }
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
                _cameraHook.ShowReticle = _config.ShowReticle;
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

        public void Recenter()
        {
            if (_cameraController == null)
            {
                throw new InvalidOperationException("Cannot recenter: CameraController not initialized. Mod initialization failed.");
            }

            _cameraController.Recenter();
            Log("Recentered");
        }

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

        private void CycleTrackingMode()
        {
            if (_cameraController == null) return;

            _trackingMode = (TrackingMode)(((int)_trackingMode + 1) % 3);
            switch (_trackingMode)
            {
                case TrackingMode.Both:
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

        public void ToggleYawMode()
        {
            if (_cameraController == null) return;
            _cameraController.WorldSpaceYaw = !_cameraController.WorldSpaceYaw;
            _config.WorldSpaceYaw = _cameraController.WorldSpaceYaw;
            Log($"Yaw mode: {(_cameraController.WorldSpaceYaw ? "world-space (horizon-locked)" : "camera-local")}");
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
