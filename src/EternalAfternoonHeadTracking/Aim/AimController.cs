using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Computes aim offset from head tracking rotation.
    /// Uses shared CanvasCompensation utilities from cameraunlock-core.
    /// </summary>
    public sealed class AimController
    {
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;

        // Raycast sample cadence. The distance smoother runs with a ~67ms time
        // constant (1/DistanceSmoothingRate), so a ~30Hz raycast rate is well
        // above the smoother's effective bandwidth but an order of magnitude
        // cheaper than per-render raycasting on a 144Hz display. The smoother
        // is fed the actual elapsed time between raycasts, so its time
        // constant is preserved exactly.
        private const float RaycastInterval = 0.033f;

        // Initialised to RaycastInterval so the very first UpdateAim call
        // performs a raycast immediately instead of waiting a full interval.
        private float _timeSinceLastRaycast = RaycastInterval;
        private float _lastHitDistance = 100f;

        private Vector2 _screenOffset;

        public Vector2 ScreenOffset => _screenOffset;

        /// <summary>
        /// Computes aim offset by projecting the pre-tracking (aim) direction
        /// through the tracked camera using WorldToScreenPoint.
        /// </summary>
        public void UpdateAim(Camera camera, Quaternion preTrackingRotation)
        {
            if (camera == null) return;

            Vector3 aimDir = preTrackingRotation * Vector3.forward;

            _timeSinceLastRaycast += Time.deltaTime;
            if (_timeSinceLastRaycast >= RaycastInterval)
            {
                float elapsed = _timeSinceLastRaycast;
                _timeSinceLastRaycast = 0f;

                RaycastHit hit;
                if (Physics.Raycast(camera.transform.position, aimDir, out hit, MaxRaycastDistance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.distance >= MinRaycastDistance)
                {
                    float t = 1f - Mathf.Exp(-DistanceSmoothingRate * elapsed);
                    _lastHitDistance = Mathf.Lerp(_lastHitDistance, hit.distance, t);
                }
            }

            _screenOffset = CanvasCompensation.CalculateAimScreenOffset(camera, aimDir, _lastHitDistance, 1f);
        }
    }
}
