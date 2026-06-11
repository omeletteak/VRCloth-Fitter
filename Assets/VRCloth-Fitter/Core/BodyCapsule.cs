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

        public BodyCapsule(Vector3 start, Vector3 end, float radius)
        {
            this.start = start;
            this.end = end;
            this.radius = radius;
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
        /// The position <paramref name="point"/> should move to so that it sits
        /// exactly <paramref name="margin"/> above the capsule surface, moving
        /// directly away from the axis. Intended for points where
        /// <see cref="Contains"/> is true; an outside point would be pulled in.
        /// A point exactly on the axis is pushed along a perpendicular fallback
        /// direction.
        /// </summary>
        public Vector3 PushOut(Vector3 point, float margin = 0f)
        {
            Vector3 axisPoint = ClosestPointOnAxis(point);
            Vector3 direction = point - axisPoint;
            float distance = direction.magnitude;
            if (distance < 1e-9f)
            {
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
                direction.Normalize();
            }
            else
            {
                direction /= distance;
            }
            return axisPoint + direction * (radius + margin);
        }
    }
}
