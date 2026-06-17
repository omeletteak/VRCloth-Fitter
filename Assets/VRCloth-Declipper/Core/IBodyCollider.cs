using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// The collision backend the fitting pipeline talks to. A body is reduced
    /// to three queries — signed distance, push-out gradient, and a local
    /// feature scale — so detection, push-out, smoothing and preflight stay
    /// independent of how the body is represented (bone capsules or a mesh
    /// SDF). See docs/DESIGN.md §6 "衝突表現をカプセルからメッシュSDFへ".
    /// </summary>
    public interface IBodyCollider
    {
        /// <summary>
        /// Signed distance from <paramref name="point"/> to the body surface:
        /// negative inside, positive outside, zero on the surface, in meters.
        /// </summary>
        float SignedDistance(Vector3 point);

        /// <summary>
        /// Unit gradient of <see cref="SignedDistance"/> at
        /// <paramref name="point"/> — the push-out direction (away from the
        /// surface) everywhere, inside and outside. Callers always receive a
        /// usable unit vector; degenerate points fall back to a fixed axis.
        /// </summary>
        Vector3 Gradient(Vector3 point);

        /// <summary>
        /// A local feature scale at <paramref name="point"/>, in meters, used
        /// by the preflight diagnostic to normalize penetration depth (the
        /// capsule radius for the capsule backend; a nominal body thickness for
        /// the mesh SDF). See docs/DESIGN.md §9.
        /// </summary>
        float LocalThickness(Vector3 point);
    }

    /// <summary>
    /// Shared push-out / containment math derived from the two collider
    /// primitives, so every backend behaves identically without reimplementing
    /// it. Mirrors <see cref="BodyCapsule.PushOut"/> and
    /// <see cref="BodyCapsule.Contains"/>.
    /// </summary>
    public static class BodyColliderExtensions
    {
        /// <summary>
        /// The position <paramref name="point"/> should move to so it sits
        /// exactly <paramref name="margin"/> above the body surface, moving
        /// along the SDF gradient. Intended for points where
        /// <see cref="Contains"/> is true.
        /// </summary>
        public static Vector3 PushOut(this IBodyCollider collider, Vector3 point, float margin)
        {
            return point + (margin - collider.SignedDistance(point)) * collider.Gradient(point);
        }

        /// <summary>
        /// True if <paramref name="point"/> is inside the body inflated by
        /// <paramref name="margin"/>.
        /// </summary>
        public static bool Contains(this IBodyCollider collider, Vector3 point, float margin = 0f)
        {
            return collider.SignedDistance(point) < margin;
        }
    }
}
