using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Extensions;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Applies head tracking rotation to the game camera additively.
    /// Rotation is applied on top of existing Cinemachine look to preserve normal controls.
    /// Delegates to shared TrackingProcessor (sensitivity, recenter, smoothing, deadzone)
    /// and PoseInterpolator (inter-sample interpolation).
    /// </summary>
    public sealed class CameraController
    {
        private readonly OpenTrackReceiver _receiver;
        private readonly TrackingProcessor _processor;
        private readonly PoseInterpolator _interpolator;
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionInterpolator _positionInterpolator;

        private Vec3 _lastPositionOffset;
        private bool _hasCentered;

        /// <summary>Whether positional tracking is enabled.</summary>
        public bool PositionEnabled { get; set; } = true;

        /// <summary>Whether rotational tracking is enabled.</summary>
        public bool RotationEnabled { get; set; } = true;

        /// <summary>
        /// True = horizon-locked yaw (yaw rotates around world up-axis).
        /// False = camera-local yaw (yaw rotates around the camera's current up-axis).
        /// </summary>
        public bool WorldSpaceYaw { get; set; } = true;

        /// <summary>Last applied position offset for transition fadeout.</summary>
        public Vec3 LastPositionOffset => _lastPositionOffset;

        public CameraController(OpenTrackReceiver receiver, TrackingProcessor processor, PoseInterpolator interpolator,
            PositionProcessor positionProcessor, PositionInterpolator positionInterpolator)
        {
            _receiver = receiver;
            _processor = processor;
            _interpolator = interpolator;
            _positionProcessor = positionProcessor;
            _positionInterpolator = positionInterpolator;
        }

        public void Recenter()
        {
            var rawPose = _receiver.GetLatestPose();
            _processor.RecenterTo(rawPose);
            _interpolator.Reset();
            _positionProcessor?.SetCenter(_receiver.GetLatestPosition());
            _positionInterpolator?.Reset();
            _lastPositionOffset = Vec3.Zero;
        }

        /// <summary>
        /// Applies head tracking rotation to the specified camera.
        /// Called by CameraTrackingHook.OnPreCull() with the hook's camera.
        /// </summary>
        public void ApplyTracking(Camera camera)
        {
            if (camera == null) return;

            // Auto-recenter on first valid frame
            if (!_hasCentered)
            {
                _hasCentered = true;
                Recenter();
            }

            float dt = Time.deltaTime;

            var rawPose = _receiver.GetLatestPose();

            // Sample-rate-to-frame-rate interpolation is gated on receiving data, never on
            // the smoothing value: LocalSmoothing defaults to 0.0, and a smoothing-based gate
            // would leave every local user with stepped motion on a high-refresh display.
            rawPose = _interpolator.Update(rawPose, dt);

            // A connection change (local tracker <-> remote device) swaps which smoothing
            // parameter applies, so refresh the flag every frame from the receiver.
            bool isRemoteConnection = _receiver.IsRemoteConnection;
            _processor.IsRemoteConnection = isRemoteConnection;
            if (_positionProcessor != null)
                _positionProcessor.IsRemoteConnection = isRemoteConnection;

            var processed = _processor.Process(rawPose, dt);

            float headYaw = processed.Yaw;
            float headPitch = -processed.Pitch;
            float headRoll = processed.Roll;

            if (!RotationEnabled)
            {
                headYaw = 0f;
                headPitch = 0f;
                headRoll = 0f;
            }

            // Cache transform values once; each Unity transform access is a managed->native call.
            Transform camXform = camera.transform;
            Quaternion gameRotation = camXform.rotation;
            Vector3 camPosition = camXform.position;

            bool positionActive = PositionEnabled && _positionProcessor != null;

            // headLocal (YXZ order) is required by PositionProcessor and for camera-local
            // composition. In WorldSpaceYaw mode with position disabled, nothing consumes it,
            // so we skip the construction entirely.
            Quaternion modifiedRot;
            Quaternion headLocal;
            if (WorldSpaceYaw)
            {
                // World-space yaw: yaw pre-multiplies in world space around world up,
                // pitch/roll apply camera-locally. Keeps yaw horizon-stable when the
                // game camera is pitched up or down. Matches ApplyHeadRotationDecomposed.
                Quaternion worldYaw = Quaternion.AngleAxis(headYaw, Vector3.up);
                Quaternion localPitchRoll = Quaternion.Euler(headPitch, 0f, headRoll);
                modifiedRot = worldYaw * gameRotation * localPitchRoll;
                // Quaternion.Euler(p, y, r) decomposes as Ry(y) * Rx(p) * Rz(r) in Unity's
                // YXZ convention, which is exactly worldYaw * localPitchRoll. Reusing the
                // already-built quaternions skips an extern Quaternion.Euler call.
                headLocal = positionActive ? worldYaw * localPitchRoll : default;
            }
            else
            {
                // Camera-local composition: all three axes apply in the camera's own
                // frame, so yaw at extreme pitch produces roll/lean (the "aerial" feel).
                headLocal = Quaternion.Euler(headPitch, headYaw, headRoll);
                modifiedRot = gameRotation * headLocal;
            }

            // Build the view matrix directly instead of Matrix4x4.TRS(...).inverse.
            // For a TRS with unit scale: inverse(T(pos)*R(rot)) == R(inv(rot)) with translation column = inv(rot) * -pos.
            // This avoids the generic 4x4 Matrix4x4.inverse (~100 FLOPs) every render callback.
            // modifiedRot is a product of unit quaternions, so its inverse equals its conjugate;
            // skipping Quaternion.Inverse avoids one extern call and the sqrMagnitude divide.
            Quaternion invRot = new Quaternion(-modifiedRot.x, -modifiedRot.y, -modifiedRot.z, modifiedRot.w);
            Matrix4x4 rotViewMatrix = Matrix4x4.Rotate(invRot);
            Vector3 rotatedPos = invRot * camPosition;
            rotViewMatrix.m03 = -rotatedPos.x;
            rotViewMatrix.m13 = -rotatedPos.y;
            // Unity cameras look down -Z; flip the Z row to match engine convention.
            // m23 lands at +rotatedPos.z after the row flip, so write the post-flip value directly.
            rotViewMatrix.m20 = -rotViewMatrix.m20;
            rotViewMatrix.m21 = -rotViewMatrix.m21;
            rotViewMatrix.m22 = -rotViewMatrix.m22;
            rotViewMatrix.m23 = rotatedPos.z;

            // Fold position tracking into the same local matrix so we only assign
            // worldToCameraMatrix once (the assignment is a managed->native setter).
            if (positionActive)
            {
                var rawPos = _receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, dt);

                _lastPositionOffset = _positionProcessor.Process(interpolatedPos, headLocal.ToQuat4(), dt);

                // Negative z is the forward lean throughout the pipeline, and the clamp is
                // built on that. Unity's transform +z is forward, so the flip belongs here,
                // at the boundary - doing it with InvertZ inverts ahead of the clamp and
                // hands the forward lean the tight backward budget.
                Vector3 trackingOffset = new Vector3(
                    _lastPositionOffset.X, _lastPositionOffset.Y, -_lastPositionOffset.Z);
                Vector3 worldOffset = gameRotation * trackingOffset;
                Vector3 camSpaceOffset = rotViewMatrix.MultiplyVector(worldOffset);
                rotViewMatrix.m03 -= camSpaceOffset.x;
                rotViewMatrix.m13 -= camSpaceOffset.y;
                rotViewMatrix.m23 -= camSpaceOffset.z;
            }

            camera.worldToCameraMatrix = rotViewMatrix;
        }

        public void ResetCamera()
        {
            _processor.ResetSmoothing();
            _interpolator.Reset();
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
            _lastPositionOffset = Vec3.Zero;
        }
    }
}
