using System;
using CameraUnlock.Core.Processing;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// The two fields of view the zoom correction is built from, read from the game.
    /// <para>
    /// The live one comes off the projection matrix the frame is drawn with: m11 is
    /// 1 / tan(vertical fov / 2) by construction, so it is a vertical half-angle tangent with
    /// no unit assumption in the path.
    /// </para>
    /// <para>
    /// The base is what PlayerScript writes to the Cinemachine lens when nothing is zoomed:
    /// <c>defaultFOV + CameraOptions.costumFOVValue</c>, the options screen's field of view plus
    /// the player's offset. Cinemachine's lens field of view is vertical, in degrees, and is
    /// what it hands the camera, so the two tangents are of the same axis. Zooming (and looking
    /// at the watch) eases the lens toward <c>zoomedFOV</c>, which is what this corrects for.
    /// </para>
    /// </summary>
    internal sealed class GameFieldOfView
    {
        // The options only change on the options screen, where tracking is suppressed, so the
        // two reflected reads (each boxing a float) run on an interval rather than every frame.
        private const int BaseReadIntervalFrames = 30;

        private readonly GamePlayer _player;
        private readonly Action<string> _log;
        private int _framesUntilBaseRead;
        private float _baseVerticalFov = -1f;
        private string _loggedMissingReason;
        private int _loggedBasisForCamera;

        internal GameFieldOfView(GamePlayer player, Action<string> log)
        {
            _player = player;
            _log = log;
        }

        /// <summary>
        /// What yaw, pitch and the lean are scaled by this frame. Exactly 1.0 when the game
        /// renders its un-zoomed field of view, and 1.0 when the base cannot be read.
        /// </summary>
        internal float Factor { get; private set; } = 1f;

        /// <summary>Re-reads both fields of view. Call once per frame, before the pose is applied.</summary>
        internal void Update(Camera camera)
        {
            float m11 = camera.projectionMatrix.m11;
            if (m11 <= 0f)
            {
                Factor = 1f;
                return;
            }
            float tanHalfLive = 1f / m11;

            if (--_framesUntilBaseRead <= 0)
            {
                _framesUntilBaseRead = BaseReadIntervalFrames;
                _baseVerticalFov = ReadBaseVerticalFov();
            }

            if (_baseVerticalFov <= 0f)
            {
                Factor = 1f;
                return;
            }

            float tanHalfBase = Mathf.Tan(_baseVerticalFov * 0.5f * Mathf.Deg2Rad);
            Factor = ZoomCompensation.FovZoomFactor(tanHalfLive, tanHalfBase);
            LogBasisOnce(camera, tanHalfLive, tanHalfBase);
        }

        private float ReadBaseVerticalFov()
        {
            var defaultFov = GameTypeResolver.DefaultFovProperty;
            var fovOffset = GameTypeResolver.FovOffsetProperty;
            if (NullHelper.IsNull(defaultFov) || NullHelper.IsNull(fovOffset))
            {
                LogMissing("PlayerScript.defaultFOV or CameraOptions.costumFOVValue is not in this build of the game");
                return -1f;
            }

            Component player = _player.Current;
            if (player == null) return -1f;

            float fov = (float)defaultFov.GetValue(player, null) + (float)fovOffset.GetValue(null, null);
            if (!(fov > 0f && fov < 180f))
            {
                LogMissing("the game's un-zoomed field of view reads " + fov.ToString("F3") + " degrees");
                return -1f;
            }
            return fov;
        }

        private void LogMissing(string reason)
        {
            if (reason == _loggedMissingReason) return;
            _loggedMissingReason = reason;
            _log("Zoom compensation off: " + reason + ".");
        }

        /// <summary>
        /// The line that proves the factor is built from two numbers of the same axis. It must
        /// read 1.0000 in ordinary play; a factor wrong by a constant looks right from inside
        /// the game. Written per camera, from the camera path, so it is on the log with no
        /// tracker connected.
        /// </summary>
        private void LogBasisOnce(Camera camera, float tanHalfLive, float tanHalfBase)
        {
            int cameraId = camera.GetInstanceID();
            if (_loggedBasisForCamera == cameraId) return;
            _loggedBasisForCamera = cameraId;

            _log(string.Format(
                "Zoom basis ({0}): live vertical fov {1:F3} deg (projection m11, aspect {2:F4}), " +
                "base vertical fov {3:F3} deg (defaultFOV + costumFOVValue), tan live {4:F5}, tan base {5:F5}, factor {6:F4}",
                camera.name, Mathf.Atan(tanHalfLive) * 2f * Mathf.Rad2Deg, camera.aspect,
                _baseVerticalFov, tanHalfLive, tanHalfBase, Factor));
        }
    }
}
