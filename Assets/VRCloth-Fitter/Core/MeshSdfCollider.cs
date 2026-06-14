using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// The mesh-SDF body backend (docs/DESIGN.md §6, §9). Treats the avatar's
    /// body mesh as a signed distance field built in memory at fit time and
    /// thrown away after — never serialized, so it stays compatible with the
    /// No Cache principle.
    ///
    /// Distance is the unsigned distance to the closest triangle (closest point
    /// on a triangle, Ericson). The sign comes from the generalized winding
    /// number (the sum of triangles' signed solid angles), which is robust on
    /// the non-watertight, self-intersecting meshes avatar bodies tend to be —
    /// the failure mode docs/DESIGN.md §9 flags for this backend. Where a face
    /// normal alone would mis-sign concave regions or leak through holes, the
    /// winding number stays correct because it integrates over the whole
    /// surface.
    ///
    /// Brute force (every query against every triangle): correct first, as the
    /// rest of the pipeline is. Spatial acceleration (a BVH) can replace the
    /// inner loops later without changing this contract — tracked in ROADMAP
    /// phase 1.
    /// </summary>
    public sealed class MeshSdfCollider : IBodyCollider
    {
        /// <summary>
        /// Default local thickness handed to the preflight diagnostic when the
        /// body is a mesh. Unlike a capsule, a mesh has no single radius, so we
        /// use a nominal limb thickness; the absolute-depth thresholds carry
        /// most of the §9 verdict. Calibrated during E2E.
        /// </summary>
        public const float DefaultNominalThickness = 0.08f;

        readonly Vector3[] vertices;
        readonly int[] triangles;
        readonly int triangleCount;
        readonly float nominalThickness;

        // One-entry memo: PushOut asks for SignedDistance then Gradient at the
        // same point, and detection then push-out revisit recently scanned
        // points; caching the last full query halves the brute-force cost on
        // those hot paths without a spatial structure.
        Vector3 cachedPoint;
        bool hasCache;
        float cachedDistance;
        Vector3 cachedGradient;

        public MeshSdfCollider(Vector3[] vertices, int[] triangles, float nominalThickness = DefaultNominalThickness)
        {
            this.vertices = vertices ?? new Vector3[0];
            this.triangles = triangles ?? new int[0];
            this.triangleCount = this.triangles.Length / 3;
            this.nominalThickness = nominalThickness;
        }

        /// <summary>True when the mesh has at least one triangle to query.</summary>
        public bool IsValid => triangleCount > 0;

        public float SignedDistance(Vector3 point)
        {
            Query(point);
            return cachedDistance;
        }

        public Vector3 Gradient(Vector3 point)
        {
            Query(point);
            return cachedGradient;
        }

        public float LocalThickness(Vector3 point)
        {
            return nominalThickness;
        }

        /// <summary>
        /// Computes signed distance and gradient at <paramref name="point"/> in
        /// one pass and stores them in the memo. No-op when the point matches
        /// the cached one.
        /// </summary>
        void Query(Vector3 point)
        {
            if (hasCache && point == cachedPoint)
            {
                return;
            }

            if (triangleCount == 0)
            {
                cachedPoint = point;
                cachedDistance = float.MaxValue;
                cachedGradient = Vector3.up;
                hasCache = true;
                return;
            }

            float bestSq = float.MaxValue;
            Vector3 bestSurface = point;
            Vector3 bestNormal = Vector3.up;

            for (int t = 0; t < triangleCount; t++)
            {
                int i0 = triangles[t * 3];
                int i1 = triangles[t * 3 + 1];
                int i2 = triangles[t * 3 + 2];
                Vector3 a = vertices[i0];
                Vector3 b = vertices[i1];
                Vector3 c = vertices[i2];

                Vector3 surface = ClosestPointOnTriangle(point, a, b, c);
                float distSq = (point - surface).sqrMagnitude;
                if (distSq < bestSq)
                {
                    bestSq = distSq;
                    bestSurface = surface;
                    bestNormal = Vector3.Cross(b - a, c - a);
                }
            }

            float distance = Mathf.Sqrt(bestSq);
            // |winding| ≈ 1 inside a closed mesh, ≈ 0 outside, for either global
            // orientation — so the magnitude classifies inside/outside without
            // depending on whether the mesh is wound outward or inward.
            float sign = Mathf.Abs(WindingNumber(point)) > 0.5f ? -1f : 1f;

            Vector3 outward = point - bestSurface;
            if (outward.sqrMagnitude >= 1e-12f)
            {
                // sign·normalize(point − surface): outward when the point is
                // outside, flipped to outward when it is inside (so the field
                // grows away from the surface in both cases).
                cachedGradient = sign * outward.normalized;
            }
            else
            {
                // On the surface: fall back to the closest face normal,
                // oriented to point outward.
                Vector3 n = bestNormal.sqrMagnitude >= 1e-12f ? bestNormal.normalized : Vector3.up;
                cachedGradient = sign < 0f ? -n : n;
            }

            cachedPoint = point;
            cachedDistance = sign * distance;
            hasCache = true;
        }

        /// <summary>
        /// Generalized winding number at <paramref name="point"/>: the sum of
        /// every triangle's signed solid angle, divided by 4π. Magnitude ≈1
        /// inside a closed mesh and ≈0 outside (sign depends on the mesh's
        /// global winding, which the caller cancels by taking the absolute
        /// value). Robust to holes and self-intersection. Solid angle via the
        /// van Oosterom–Strackee formula.
        /// </summary>
        float WindingNumber(Vector3 point)
        {
            double sum = 0.0;
            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 a = vertices[triangles[t * 3]] - point;
                Vector3 b = vertices[triangles[t * 3 + 1]] - point;
                Vector3 c = vertices[triangles[t * 3 + 2]] - point;

                double la = a.magnitude;
                double lb = b.magnitude;
                double lc = c.magnitude;
                if (la < 1e-12 || lb < 1e-12 || lc < 1e-12)
                {
                    continue; // point coincides with a vertex; skip its term
                }

                double numerator = Vector3.Dot(a, Vector3.Cross(b, c));
                double denominator = la * lb * lc
                    + Vector3.Dot(a, b) * lc
                    + Vector3.Dot(b, c) * la
                    + Vector3.Dot(c, a) * lb;
                sum += 2.0 * System.Math.Atan2(numerator, denominator);
            }
            return (float)(sum / (4.0 * System.Math.PI));
        }

        /// <summary>
        /// Closest point to <paramref name="p"/> on triangle (a, b, c).
        /// Voronoi-region method from Ericson, Real-Time Collision Detection.
        /// </summary>
        public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v0 = d1 / (d1 - d3);
                return a + v0 * ab;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w0 = d2 / (d2 - d6);
                return a + w0 * ac;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w1 = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + w1 * (c - b);
            }

            float denom = 1f / (va + vb + vc);
            float vv = vb * denom;
            float ww = vc * denom;
            return a + ab * vv + ac * ww;
        }
    }
}
