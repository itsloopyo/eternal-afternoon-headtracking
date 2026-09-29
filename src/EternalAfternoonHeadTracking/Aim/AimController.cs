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

        private Vector2 _screenOffset;

        public Vector2 ScreenOffset => _screenOffset;

        /// <summary>
        /// Projects the point the clean aim ray hits through the tracked camera. The depth is
        /// this frame's own: a lean moves the render eye off the aim ray, so a smoothed or held
        /// depth puts the crosshair beside the aim point at every range but one.
        /// </summary>
        public void UpdateAim(Camera camera, Quaternion preTrackingRotation)
        {
            if (camera == null) return;

            Vector3 aimDir = preTrackingRotation * Vector3.forward;

            // Surfaces nearer than MinRaycastDistance are skipped by starting the ray there,
            // which keeps the filter every earlier build applied without reusing an old depth.
            Vector3 origin = camera.transform.position + aimDir * MinRaycastDistance;
            RaycastHit hit;
            float depth = Physics.Raycast(origin, aimDir, out hit, MaxRaycastDistance - MinRaycastDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? MinRaycastDistance + hit.distance
                : MaxRaycastDistance;

            _screenOffset = CanvasCompensation.CalculateAimScreenOffset(camera, aimDir, depth, 1f);
        }
    }
}
