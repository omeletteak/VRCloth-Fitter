using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// An avatar's body part approximated by a capsule.
    /// </summary>
    public struct BodyCapsule
    {
        public Vector3 start;
        public Vector3 end;
        public float radius;

        /// <summary>
        /// Human-readable origin of this capsule (e.g. "LeftUpperArm→LeftLowerArm"),
        /// used by the run log to attribute hits to a body part. Optional.
        /// </summary>
        public string label;

        public BodyCapsule(Vector3 start, Vector3 end, float radius)
            : this(start, end, radius, null)
        {
        }

        public BodyCapsule(Vector3 start, Vector3 end, float radius, string label)
        {
            this.start = start;
            this.end = end;
            this.radius = radius;
            this.label = label;
        }

        /// <summary>
        /// The point on the capsule's axis segment closest to <paramref name="point"/>.
        /// A zero-length axis (start == end) makes the capsule act as a sphere.
        /// </summary>
        public Vector3 ClosestPointOnAxis(Vector3 point)
        {
            Vector3 axis = end - start;
            float lengthSq = axis.sqrMagnitude;
            if (lengthSq < 1e-12f)
            {
                return start;
            }
            float t = Mathf.Clamp01(Vector3.Dot(point - start, axis) / lengthSq);
            return start + axis * t;
        }

        /// <summary>
        /// Distance from <paramref name="point"/> to the capsule surface.
        /// Negative inside the capsule, positive outside, zero on the surface.
        /// </summary>
        public float SignedDistance(Vector3 point)
        {
            return Vector3.Distance(point, ClosestPointOnAxis(point)) - radius;
        }

        /// <summary>
        /// True if <paramref name="point"/> is inside the capsule inflated by
        /// <paramref name="margin"/>. Penetration detection marks vertices for
        /// which this is true.
        /// </summary>
        public bool Contains(Vector3 point, float margin = 0f)
        {
            return SignedDistance(point) < margin;
        }

        /// <summary>
        /// Normalized gradient of <see cref="SignedDistance"/> at
        /// <paramref name="point"/>: the unit direction of fastest distance
        /// growth, pointing away from the axis. This is the push-out
        /// direction everywhere, inside and outside. A point exactly on the
        /// axis (the gradient's only singularity) falls back to a fixed
        /// perpendicular so callers always receive a usable unit vector.
        /// </summary>
        public Vector3 Gradient(Vector3 point)
        {
            Vector3 direction = point - ClosestPointOnAxis(point);
            float distance = direction.magnitude;
            if (distance >= 1e-9f)
            {
                return direction / distance;
            }
            Vector3 axis = end - start;
            direction = Vector3.Cross(axis, Vector3.up);
            if (direction.sqrMagnitude < 1e-12f)
            {
                direction = Vector3.Cross(axis, Vector3.right);
            }
            if (direction.sqrMagnitude < 1e-12f)
            {
                direction = Vector3.right;
            }
            return direction.normalized;
        }

        /// <summary>
        /// The position <paramref name="point"/> should move to so that it sits
        /// exactly <paramref name="margin"/> above the capsule surface, moving
        /// along the SDF gradient. Intended for points where
        /// <see cref="Contains"/> is true; an outside point would be pulled in.
        /// </summary>
        public Vector3 PushOut(Vector3 point, float margin = 0f)
        {
            return point + (margin - SignedDistance(point)) * Gradient(point);
        }
    }
}
