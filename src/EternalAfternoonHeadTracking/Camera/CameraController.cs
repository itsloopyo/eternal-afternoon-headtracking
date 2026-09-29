using System;
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
    /// Delegates to shared TrackingProcessor (sensitivity, smoothing, deadzone)
    /// and PoseInterpolator (inter-sample interpolation).
    /// </summary>
    public sealed class CameraController
    {
        private readonly OpenTrackReceiver _receiver;
        private readonly TrackingProcessor _processor;
        private readonly PoseInterpolator _interpolator;
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionInterpolator _positionInterpolator;
        private readonly LeanClamp _leanClamp;
        private readonly LeanTrace _leanTrace;
        private readonly Action<string> _log;

        // Clamp state as last logged, and when a periodic sample is next due.
        private bool _loggedContact;
        private bool _loggedQueryFailed;
        private float _nextLeanSampleTime;
        private const float LeanSampleIntervalSeconds = 5f;

        /// <summary>Whether positional tracking is enabled.</summary>
        public bool PositionEnabled { get; set; } = true;

        /// <summary>Whether rotational tracking is enabled.</summary>
        public bool RotationEnabled { get; set; } = true;

        /// <summary>
        /// True = horizon-locked yaw (yaw rotates around world up-axis).
        /// False = camera-local yaw (yaw rotates around the camera's current up-axis).
        /// </summary>
        public bool WorldSpaceYaw { get; set; } = true;

        /// <param name="leanClamp">Null when CollisionEnabled is off.</param>
        internal CameraController(OpenTrackReceiver receiver, TrackingProcessor processor, PoseInterpolator interpolator,
            PositionProcessor positionProcessor, PositionInterpolator positionInterpolator,
            LeanClamp leanClamp, LeanTrace leanTrace, Action<string> log)
        {
            _receiver = receiver;
            _processor = processor;
            _interpolator = interpolator;
            _positionProcessor = positionProcessor;
            _positionInterpolator = positionInterpolator;
            _leanClamp = leanClamp;
            _leanTrace = leanTrace;
            _log = log;
        }

        /// <summary>
        /// Applies head tracking rotation to the specified camera.
        /// Called by CameraTrackingHook.OnPreCull() with the hook's camera.
        /// <paramref name="zoomFactor"/> scales yaw, pitch and the lean so a head movement moves
        /// the picture as far as it does un-zoomed; roll turns the picture by the same angle at
        /// every field of view and is left alone.
        /// </summary>
        public void ApplyTracking(Camera camera, float zoomFactor)
        {
            if (camera == null) return;

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

            float headYaw = ZoomCompensation.ScaleAngleForZoom(processed.Yaw, zoomFactor);
            float headPitch = -ZoomCompensation.ScaleAngleForZoom(processed.Pitch, zoomFactor);
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

                Vec3 positionOffset = _positionProcessor.Process(interpolatedPos, headLocal.ToQuat4(), dt);

                // Negative z is the forward lean throughout the pipeline, and the clamp is
                // built on that. Unity's transform +z is forward, so the flip belongs here,
                // at the boundary - doing it with InvertZ inverts ahead of the clamp and
                // hands the forward lean the tight backward budget.
                Vector3 trackingOffset = new Vector3(
                    positionOffset.X, positionOffset.Y, -positionOffset.Z) * zoomFactor;
                Vector3 worldOffset = ClampLean(camera, camPosition, gameRotation * trackingOffset, dt);
                Vector3 camSpaceOffset = rotViewMatrix.MultiplyVector(worldOffset);
                rotViewMatrix.m03 -= camSpaceOffset.x;
                rotViewMatrix.m13 -= camSpaceOffset.y;
                rotViewMatrix.m23 -= camSpaceOffset.z;
            }
            else
            {
                ResetLeanClamp();
            }

            camera.worldToCameraMatrix = rotViewMatrix;
        }

        /// <summary>
        /// Trims the lean to what the room leaves free, swept from the clean eye (where the game
        /// put the camera) before the offset is applied.
        /// </summary>
        private Vector3 ClampLean(Camera camera, Vector3 cleanEye, Vector3 wantedLean, float dt)
        {
            if (_leanClamp == null) return wantedLean;

            // The trace carries the standoff as its sphere's radius and hands it back on every
            // distance, and the clamp takes it off again as its skin, so the two are one number.
            LeanClampSettings settings = _leanClamp.Settings;
            settings.Skin = _leanTrace.BeginFrame(camera);
            _leanClamp.Settings = settings;

            Vec3 clamped = _leanClamp.Apply(
                new Vec3(cleanEye.x, cleanEye.y, cleanEye.z),
                new Vec3(wantedLean.x, wantedLean.y, wantedLean.z),
                dt, _leanTrace.Query);

            LogLeanClamp(wantedLean.magnitude, clamped.Magnitude);
            return new Vector3(clamped.X, clamped.Y, clamped.Z);
        }

        // Transitions alone cannot tell "the sweep runs and the room is open" from "the sweep is
        // not running", so a sample goes out on an interval as well.
        private void LogLeanClamp(float wanted, float allowed)
        {
            bool contact = _leanClamp.InContact;
            bool failed = _leanClamp.LastQueryFailed;
            float now = Time.unscaledTime;
            bool changed = contact != _loggedContact || failed != _loggedQueryFailed;
            if (!changed && now < _nextLeanSampleTime) return;

            _loggedContact = contact;
            _loggedQueryFailed = failed;
            _nextLeanSampleTime = now + LeanSampleIntervalSeconds;
            _log(string.Format("Lean clamp{0}: wanted {1:F3}m, allowed {2:F3}m, contact {3}, query failed {4}, standoff {5:F3}m",
                changed ? "" : " sample", wanted, allowed, contact, failed, _leanTrace.Standoff));
        }

        /// <summary>
        /// Forgets the clamp's allowance, so a wall from one room is not carried into the next.
        /// Called on every frame that applies no lean, and when the camera changes.
        /// </summary>
        public void ResetLeanClamp()
        {
            _leanClamp?.Reset();
        }

        public void ResetCamera()
        {
            _processor.ResetSmoothing();
            _interpolator.Reset();
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
            ResetLeanClamp();
        }
    }
}
