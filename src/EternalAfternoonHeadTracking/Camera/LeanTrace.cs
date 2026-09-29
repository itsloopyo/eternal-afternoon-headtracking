using System;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// The engine half of the lean clamp: asks Unity's physics what lies between the clean eye
    /// and where the lean wants to put it. Core's <see cref="LeanClamp"/> owns what is done with
    /// the answer.
    /// <para>
    /// Two casts, because each misses something the other catches. A sphere of the standoff's
    /// radius swept along the lean is the shape the eye needs kept clear, so it catches a door
    /// frame's edge the eye would pass beside; it leaves out every collider it already overlaps
    /// at the start, so it cannot see a surface already within the standoff. A line down the
    /// centre does see that surface, since a ray only skips a collider its origin is inside,
    /// and for a flat surface met at an angle it gives the exact travel that holds the eye the
    /// standoff off it.
    /// </para>
    /// <para>
    /// The player is a rigidbody capsule, and the eye can sit outside it (above its top, for
    /// one), where a lean back or down would sweep into the player's own body. Every collider
    /// under PlayerScript's transform is skipped for that reason. Distances come back in core's
    /// convention: the eye's travel plus the standoff, which the clamp takes off as its skin.
    /// </para>
    /// </summary>
    internal sealed class LeanTrace
    {
        // The floor on the cosine between the lean and a surface's normal. Holding the eye r
        // off a flat surface met at an angle means stopping r / cos short of it along the lean,
        // unbounded at grazing incidence. 0.25 is 75 degrees off the normal, past which the eye
        // slides along the surface rather than into it; the same floor as core's line sweep.
        private const float MinApproachCosine = 0.25f;

        // How far past the corners of the near clip plane the standoff must reach at the least.
        // A turned head can put a corner of the near plane, not its centre, nearest the wall,
        // and geometry inside the near plane is culled.
        private const float NearPlaneStandoffFactor = 1.25f;

        private const int MaxHits = 32;

        private readonly GamePlayer _player;
        private readonly float _configuredStandoff;
        private readonly int _mask;
        private readonly Action<string> _log;
        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private Transform _playerRoot;
        private bool _loggedStandoffRaise;

        internal LeanTrace(GamePlayer player, float standoff, int mask, Action<string> log)
        {
            _player = player;
            _configuredStandoff = standoff;
            _mask = mask;
            _log = log;
            Query = Trace;
        }

        /// <summary>Held once, so handing the query to the clamp allocates nothing per frame.</summary>
        internal LeanQuery Query { get; }

        /// <summary>The standoff in use: the configured one, or more where the near plane needs it.</summary>
        internal float Standoff { get; private set; }

        /// <summary>
        /// Sets this frame's standoff from the camera's live projection and the player to skip.
        /// Returns the standoff, for the clamp's skin, which must be the same number.
        /// </summary>
        internal float BeginFrame(Camera camera)
        {
            Component player = _player.Current;
            _playerRoot = player != null ? player.transform : null;

            // m00 and m11 are 1 / tan of the horizontal and vertical half angles, so a corner of
            // the near plane sits near * sqrt(1 + tanH^2 + tanV^2) from the eye. Read off the
            // matrix the frame is projected with, so a zoom is followed.
            Matrix4x4 projection = camera.projectionMatrix;
            float tanH = 1f / projection.m00;
            float tanV = 1f / projection.m11;
            float corner = camera.nearClipPlane * Mathf.Sqrt(1f + tanH * tanH + tanV * tanV);
            float floor = corner * NearPlaneStandoffFactor;

            if (_configuredStandoff >= floor)
            {
                Standoff = _configuredStandoff;
                return Standoff;
            }

            if (!_loggedStandoffRaise)
            {
                _loggedStandoffRaise = true;
                _log(string.Format(
                    "CollisionMargin {0:F3}m would let a wall inside the corners of the camera's near clip plane " +
                    "({1:F3}m from the eye at near={2:F3}m) be culled - holding walls {3:F3}m off instead",
                    _configuredStandoff, corner, camera.nearClipPlane, floor));
            }
            Standoff = floor;
            return Standoff;
        }

        private LeanObstruction Trace(Vec3 start, Vec3 direction, float maxDistance)
        {
            // Without the player to skip, the player's own capsule can block the lean, so the
            // query reports that it could not run rather than answering wrongly.
            if (_playerRoot == null) return LeanObstruction.Failed;

            float radius = Standoff;
            float lean = maxDistance - radius;
            var origin = new Vector3(start.X, start.Y, start.Z);
            var along = new Vector3(direction.X, direction.Y, direction.Z);

            float travel = lean;

            // A swept hit's distance is where the sphere's CENTRE stopped, already one radius off
            // the surface, so it is the eye's travel as it stands.
            int count = Physics.SphereCastNonAlloc(origin, radius, along, _hits, lean, _mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                // The NonAlloc cast reports colliders the sphere overlaps at the start with
                // distance 0 and a zero point, where SphereCast leaves them out. Leave them out
                // too: the line below sees any of them that lies ahead.
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
                if (hit.transform.IsChildOf(_playerRoot)) continue;
                if (hit.distance < travel) travel = hit.distance;
            }

            // Overreaches the lean by the standoff at the steepest angle allowed, or the ray
            // stops where the lean stops and cannot see the surface the eye comes to rest against.
            count = Physics.RaycastNonAlloc(origin, along, _hits, lean + radius / MinApproachCosine, _mask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.transform.IsChildOf(_playerRoot)) continue;
                float cosine = Mathf.Max(Mathf.Abs(Vector3.Dot(along, hit.normal)), MinApproachCosine);
                float stop = hit.distance - radius / cosine;
                if (stop < travel) travel = stop;
            }

            if (travel >= lean) return LeanObstruction.Clear;
            return LeanObstruction.Hit(Mathf.Max(travel, 0f) + radius);
        }
    }
}
