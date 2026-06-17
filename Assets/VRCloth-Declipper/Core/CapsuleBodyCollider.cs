using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// The bone-capsule backend as an <see cref="IBodyCollider"/>: a composite
    /// of <see cref="BodyCapsule"/> whose signed distance is the minimum over
    /// all capsules (their union), with gradient and local thickness taken from
    /// the closest capsule. This reproduces the original per-capsule push-out
    /// math exactly — the closest capsule by signed distance is the same one
    /// detection records — so routing the capsule pipeline through this
    /// collider is behavior-preserving.
    /// </summary>
    public sealed class CapsuleBodyCollider : IBodyCollider
    {
        readonly IReadOnlyList<BodyCapsule> capsules;

        public CapsuleBodyCollider(IReadOnlyList<BodyCapsule> capsules)
        {
            this.capsules = capsules;
        }

        /// <summary>
        /// Index of the capsule closest to <paramref name="point"/> (smallest
        /// signed distance), or -1 if there are no capsules.
        /// </summary>
        public int ClosestIndex(Vector3 point)
        {
            int closest = -1;
            float min = float.MaxValue;
            if (capsules == null)
            {
                return closest;
            }
            for (int c = 0; c < capsules.Count; c++)
            {
                float d = capsules[c].SignedDistance(point);
                if (d < min)
                {
                    min = d;
                    closest = c;
                }
            }
            return closest;
        }

        public float SignedDistance(Vector3 point)
        {
            float min = float.MaxValue;
            if (capsules != null)
            {
                for (int c = 0; c < capsules.Count; c++)
                {
                    float d = capsules[c].SignedDistance(point);
                    if (d < min)
                    {
                        min = d;
                    }
                }
            }
            return min;
        }

        public Vector3 Gradient(Vector3 point)
        {
            int closest = ClosestIndex(point);
            return closest >= 0 ? capsules[closest].Gradient(point) : Vector3.up;
        }

        public float LocalThickness(Vector3 point)
        {
            int closest = ClosestIndex(point);
            return closest >= 0 ? capsules[closest].radius : 0f;
        }
    }
}
