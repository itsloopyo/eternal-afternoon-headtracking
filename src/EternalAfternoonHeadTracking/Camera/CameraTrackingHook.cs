using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;
using UnityEngine.Rendering;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Helper component attached to the main camera to apply head tracking at the right time.
    ///
    /// CRITICAL FOR LOOK/AIM DECOUPLING:
    /// We apply tracking ONLY during rendering and restore the original rotation after.
    /// - Game logic sees the un-tracked camera direction = AIM
    /// - Rendering sees the tracked camera direction = LOOK (where head is pointing)
    ///
    /// Uses RenderPipelineManager events (URP-compatible) instead of OnPreCull/OnPostRender
    /// which are NOT called in Scriptable Render Pipelines (URP/HDRP).
    /// </summary>
    public sealed class CameraTrackingHook : MonoBehaviour
    {
        private CameraController _cameraController;
        private AimController _aimController;
        private GameCrosshair _gameCrosshair;
        private OpenTrackReceiver _receiver;
        private Camera _camera;
        private bool _isEnabled;

        private bool _trackingAppliedThisFrame;
        internal bool ShowReticle { get; set; } = true;

        // Gameplay detection via CinemachineInputProvider.enabled
        private static bool _staticIsInGameplay;
        private Behaviour _cachedInputProviderBehaviour;
        private bool _inputProviderSearched;
        private bool _isInGameplay;

        /// <summary>
        /// Returns true if the player currently has camera control.
        /// Used by GameCrosshair to skip repositioning during cutscenes/menus.
        /// </summary>
        public static bool IsInGameplay => _staticIsInGameplay;

        internal void Initialize(
            CameraController cameraController,
            AimController aimController,
            GameCrosshair gameCrosshair,
            OpenTrackReceiver receiver)
        {
            _cameraController = cameraController;
            _aimController = aimController;
            _gameCrosshair = gameCrosshair;
            _receiver = receiver;
            _camera = GetComponent<Camera>();
            _isEnabled = true;
        }

        internal void SetAimComponents(AimController aimController, GameCrosshair gameCrosshair)
        {
            _aimController = aimController;
            _gameCrosshair = gameCrosshair;
        }

        internal void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        /// <summary>
        /// Checks if we're in gameplay by looking for an enabled CinemachineInputProvider.
        /// When the game disables CinemachineInputProvider (menus, dialogue, sitting transitions,
        /// cutscenes), we stop applying tracking.
        /// Caches the component reference to avoid expensive reflection every frame.
        /// </summary>
        private bool CheckInGameplay()
        {
            // Fast path: isActiveAndEnabled is a single native call that folds
            // `enabled` AND `gameObject.activeInHierarchy` together, and returns
            // false on destroyed objects — so we don't need the separate null/hop.
            Behaviour cachedBehaviour = _cachedInputProviderBehaviour;
            if (cachedBehaviour != null)
            {
                return cachedBehaviour.isActiveAndEnabled;
            }

            // Only search once per hook instance
            if (_inputProviderSearched) return false;

            // Find CinemachineInputProvider via UserInput.Instance reflection
            var userInputType = GameTypeResolver.UserInputType;
            if (NullHelper.NotNull(userInputType) && NullHelper.NotNull(GameTypeResolver.UserInputInstanceField))
            {
                object userInputInstance = GameTypeResolver.UserInputInstanceField.GetValue(null);
                if (NullHelper.NotNull(userInputInstance) && NullHelper.NotNull(GameTypeResolver.CinemachineInputProviderProperty))
                {
                    object provider = GameTypeResolver.CinemachineInputProviderProperty.GetValue(userInputInstance);
                    if (NullHelper.NotNull(provider))
                    {
                        _inputProviderSearched = true;
                        var behaviour = provider as Behaviour;
                        if (behaviour != null)
                        {
                            _cachedInputProviderBehaviour = behaviour;
                            return behaviour.isActiveAndEnabled;
                        }
                        return false;
                    }
                }
            }

            _inputProviderSearched = true;
            return false;
        }

        /// <summary>
        /// URP equivalent of OnPreCull - called just before a camera renders.
        ///
        /// LOOK/AIM DECOUPLING:
        /// ViewMatrixModifier only changes worldToCameraMatrix, never camera.transform.
        /// Game logic sees the untouched transform (AIM), rendering sees the modified matrix (LOOK).
        /// endCameraRendering resets the matrix so Unity auto-updates it from transform next frame.
        /// </summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;

            _trackingAppliedThisFrame = false;

            _isInGameplay = CheckInGameplay();
            _staticIsInGameplay = _isInGameplay;
            if (!_isInGameplay) return;

            if (!_isEnabled || NullHelper.IsNull(_cameraController) || NullHelper.IsNull(_receiver) || !_receiver.IsReceiving)
                return;

            if (_camera == null) return;

            _trackingAppliedThisFrame = true;

            // When the reticle is hidden, skip the aim work entirely: it's a
            // Physics.Raycast + WorldToScreenPoint + smoothing, and no consumer
            // would read the resulting ScreenOffset. Capture aim rotation only
            // in the path that will use it.
            bool aimWorkNeeded = ShowReticle && NullHelper.NotNull(_aimController);

            Quaternion aimRotation = aimWorkNeeded
                ? _camera.transform.rotation
                : default;

            // Apply head tracking to view matrix - rendering sees the LOOK direction
            _cameraController.ApplyTracking(_camera);

            if (aimWorkNeeded)
            {
                _aimController.UpdateAim(_camera, aimRotation);
                _gameCrosshair?.SetOffset(_aimController.ScreenOffset);
            }
        }

        /// <summary>
        /// URP equivalent of OnPostRender - called after a camera finishes rendering.
        /// Resets the view matrix so Unity auto-calculates it from the (untouched) transform next frame.
        /// </summary>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;
            if (!_trackingAppliedThisFrame) return;

            if (_camera != null)
                ViewMatrixModifier.Reset(_camera);
        }
    }
}
