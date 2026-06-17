using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
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
    /// the failure mode docs/DESIGN.md §9 flags for this backend.
    ///
    /// Both queries are accelerated by a BVH so the collider is usable on real
    /// body meshes (tens of thousands of triangles): the closest point uses
    /// branch-and-bound over the tree (exact), and the winding number uses a
    /// Barnes–Hut expansion (Barill et al., "Fast Winding Numbers") — far
    /// triangle clusters collapse to a single area-weighted-normal dipole term,
    /// while clusters near the query point recurse to exact per-triangle solid
    /// angles, so the sign stays correct where it matters (near the surface).
    /// A brute-force reference (<see cref="SignedDistanceBruteForce"/>) is kept
    /// for tests to pin this contract.
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

        const int LeafSize = 8;
        const float Beta = 2.0f; // Barnes–Hut acceptance: approximate when |p−c̄| > β·radius

        struct Node
        {
            public Vector3 boundsMin, boundsMax;
            public Vector3 weightedCentroid; // area-weighted centroid c̄
            public Vector3 weightedNormalSum; // Σ area·normal (dipole moment P)
            public float maxDist;            // c̄ to the farthest vertex in the subtree
            public int start, count;         // range into triOrder (leaves)
            public int left, right;          // child node indices, -1 for a leaf
        }

        readonly Vector3[] vertices;
        readonly int[] triangles;
        readonly int triangleCount;
        readonly float nominalThickness;

        readonly Vector3[] triCentroid;
        readonly Vector3[] triWeightedNormal; // 0.5·cross(ab,ac): |·| = area, dir = normal
        int[] triOrder;
        readonly List<Node> nodes = new List<Node>();
        readonly int root = -1;

        // One-entry memo: PushOut asks for SignedDistance then Gradient at the
        // same point, and detection then push-out revisit recently scanned
        // points; caching the last full query avoids recomputing it.
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

            triCentroid = new Vector3[triangleCount];
            triWeightedNormal = new Vector3[triangleCount];
            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 a = this.vertices[this.triangles[t * 3]];
                Vector3 b = this.vertices[this.triangles[t * 3 + 1]];
                Vector3 c = this.vertices[this.triangles[t * 3 + 2]];
                triCentroid[t] = (a + b + c) / 3f;
                triWeightedNormal[t] = 0.5f * Vector3.Cross(b - a, c - a);
            }

            if (triangleCount > 0)
            {
                triOrder = new int[triangleCount];
                for (int t = 0; t < triangleCount; t++) triOrder[t] = t;
                root = BuildNode(0, triangleCount);
            }
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
            ClosestPoint(root, point, ref bestSq, ref bestSurface, ref bestNormal);

            float distance = Mathf.Sqrt(bestSq);
            // |winding| ≈ 1 inside a closed mesh, ≈ 0 outside, for either global
            // orientation — magnitude classifies inside/outside without
            // depending on whether the mesh is wound outward or inward.
            float sign = Mathf.Abs(WindingNumber(root, point)) > 0.5f ? -1f : 1f;

            Vector3 outward = point - bestSurface;
            if (outward.sqrMagnitude >= 1e-12f)
            {
                cachedGradient = sign * outward.normalized;
            }
            else
            {
                Vector3 n = bestNormal.sqrMagnitude >= 1e-12f ? bestNormal.normalized : Vector3.up;
                cachedGradient = sign < 0f ? -n : n;
            }

            cachedPoint = point;
            cachedDistance = sign * distance;
            hasCache = true;
        }

        // --- BVH build -----------------------------------------------------

        int BuildNode(int start, int count)
        {
            int idx = nodes.Count;
            nodes.Add(default);

            var n = new Node { start = start, count = count, left = -1, right = -1 };

            Vector3 bMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 bMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 weightedCentroidSum = Vector3.zero;
            Vector3 normalSum = Vector3.zero;
            float areaSum = 0f;
            Vector3 cMin = bMin, cMax = bMax; // centroid bounds, for the split axis

            for (int i = start; i < start + count; i++)
            {
                int t = triOrder[i];
                Vector3 a = vertices[triangles[t * 3]];
                Vector3 b = vertices[triangles[t * 3 + 1]];
                Vector3 c = vertices[triangles[t * 3 + 2]];
                Encapsulate(ref bMin, ref bMax, a);
                Encapsulate(ref bMin, ref bMax, b);
                Encapsulate(ref bMin, ref bMax, c);

                float area = triWeightedNormal[t].magnitude;
                areaSum += area;
                weightedCentroidSum += area * triCentroid[t];
                normalSum += triWeightedNormal[t];
                Encapsulate(ref cMin, ref cMax, triCentroid[t]);
            }

            n.boundsMin = bMin;
            n.boundsMax = bMax;
            n.weightedNormalSum = normalSum;
            n.weightedCentroid = areaSum > 1e-12f ? weightedCentroidSum / areaSum : 0.5f * (bMin + bMax);

            float maxDistSq = 0f;
            for (int i = start; i < start + count; i++)
            {
                int t = triOrder[i];
                for (int k = 0; k < 3; k++)
                {
                    float dsq = (vertices[triangles[t * 3 + k]] - n.weightedCentroid).sqrMagnitude;
                    if (dsq > maxDistSq) maxDistSq = dsq;
                }
            }
            n.maxDist = Mathf.Sqrt(maxDistSq);

            if (count <= LeafSize)
            {
                nodes[idx] = n;
                return idx;
            }

            // Split on the longest axis of the centroid bounds.
            Vector3 extent = cMax - cMin;
            int axis = extent.x >= extent.y ? (extent.x >= extent.z ? 0 : 2) : (extent.y >= extent.z ? 1 : 2);
            SortByCentroidAxis(start, count, axis);

            int mid = count / 2;
            n.left = BuildNode(start, mid);
            n.right = BuildNode(start + mid, count - mid);
            nodes[idx] = n;
            return idx;
        }

        void SortByCentroidAxis(int start, int count, int axis)
        {
            // Insertion-free: delegate to Array.Sort over the sub-range.
            System.Array.Sort(triOrder, start, count, Comparer<int>.Create((x, y) =>
                triCentroid[x][axis].CompareTo(triCentroid[y][axis])));
        }

        static void Encapsulate(ref Vector3 min, ref Vector3 max, Vector3 p)
        {
            if (p.x < min.x) min.x = p.x; if (p.x > max.x) max.x = p.x;
            if (p.y < min.y) min.y = p.y; if (p.y > max.y) max.y = p.y;
            if (p.z < min.z) min.z = p.z; if (p.z > max.z) max.z = p.z;
        }

        // --- closest point (exact, branch-and-bound) -----------------------

        void ClosestPoint(int nodeIdx, Vector3 p, ref float bestSq, ref Vector3 bestSurface, ref Vector3 bestNormal)
        {
            Node node = nodes[nodeIdx];
            if (node.left < 0)
            {
                for (int i = node.start; i < node.start + node.count; i++)
                {
                    int t = triOrder[i];
                    Vector3 a = vertices[triangles[t * 3]];
                    Vector3 b = vertices[triangles[t * 3 + 1]];
                    Vector3 c = vertices[triangles[t * 3 + 2]];
                    Vector3 surface = ClosestPointOnTriangle(p, a, b, c);
                    float dsq = (p - surface).sqrMagnitude;
                    if (dsq < bestSq)
                    {
                        bestSq = dsq;
                        bestSurface = surface;
                        bestNormal = triWeightedNormal[t];
                    }
                }
                return;
            }

            float dl = AabbDistanceSq(nodes[node.left], p);
            float dr = AabbDistanceSq(nodes[node.right], p);
            int near = dl <= dr ? node.left : node.right;
            int far = dl <= dr ? node.right : node.left;
            float nearD = Mathf.Min(dl, dr);
            float farD = Mathf.Max(dl, dr);

            if (nearD < bestSq) ClosestPoint(near, p, ref bestSq, ref bestSurface, ref bestNormal);
            if (farD < bestSq) ClosestPoint(far, p, ref bestSq, ref bestSurface, ref bestNormal);
        }

        static float AabbDistanceSq(Node node, Vector3 p)
        {
            float dx = Mathf.Max(Mathf.Max(node.boundsMin.x - p.x, p.x - node.boundsMax.x), 0f);
            float dy = Mathf.Max(Mathf.Max(node.boundsMin.y - p.y, p.y - node.boundsMax.y), 0f);
            float dz = Mathf.Max(Mathf.Max(node.boundsMin.z - p.z, p.z - node.boundsMax.z), 0f);
            return dx * dx + dy * dy + dz * dz;
        }

        // --- winding number (Barnes–Hut) -----------------------------------

        float WindingNumber(int nodeIdx, Vector3 p)
        {
            double sum = WindingAccumulate(nodeIdx, p);
            return (float)(sum / (4.0 * System.Math.PI));
        }

        double WindingAccumulate(int nodeIdx, Vector3 p)
        {
            Node node = nodes[nodeIdx];

            if (node.left >= 0)
            {
                // Far cluster: collapse to a single dipole term. The signed
                // solid angle of a patch seen from p is ≈ (c̄−p)·P / |c̄−p|³,
                // with P the area-weighted normal sum.
                Vector3 d = node.weightedCentroid - p;
                float dist = d.magnitude;
                if (dist > Beta * node.maxDist && dist > 1e-9f)
                {
                    return Vector3.Dot(d, node.weightedNormalSum) / (dist * dist * dist);
                }
                return WindingAccumulate(node.left, p) + WindingAccumulate(node.right, p);
            }

            double leaf = 0.0;
            for (int i = node.start; i < node.start + node.count; i++)
            {
                int t = triOrder[i];
                leaf += SolidAngle(
                    vertices[triangles[t * 3]] - p,
                    vertices[triangles[t * 3 + 1]] - p,
                    vertices[triangles[t * 3 + 2]] - p);
            }
            return leaf;
        }

        static double SolidAngle(Vector3 a, Vector3 b, Vector3 c)
        {
            double la = a.magnitude, lb = b.magnitude, lc = c.magnitude;
            if (la < 1e-12 || lb < 1e-12 || lc < 1e-12)
            {
                return 0.0; // p coincides with a vertex; skip its term
            }
            double numerator = Vector3.Dot(a, Vector3.Cross(b, c));
            double denominator = la * lb * lc
                + Vector3.Dot(a, b) * lc
                + Vector3.Dot(b, c) * la
                + Vector3.Dot(c, a) * lb;
            return 2.0 * System.Math.Atan2(numerator, denominator);
        }

        // --- brute-force reference (tests) ---------------------------------

        /// <summary>
        /// The unaccelerated signed distance — closest point over every
        /// triangle, sign from the full-mesh generalized winding number. Kept
        /// as the reference the accelerated path is tested against.
        /// </summary>
        public static float SignedDistanceBruteForce(Vector3[] verts, int[] tris, Vector3 point)
        {
            int count = tris != null ? tris.Length / 3 : 0;
            if (count == 0) return float.MaxValue;

            float bestSq = float.MaxValue;
            for (int t = 0; t < count; t++)
            {
                Vector3 surface = ClosestPointOnTriangle(point,
                    verts[tris[t * 3]], verts[tris[t * 3 + 1]], verts[tris[t * 3 + 2]]);
                float dsq = (point - surface).sqrMagnitude;
                if (dsq < bestSq) bestSq = dsq;
            }

            double sum = 0.0;
            for (int t = 0; t < count; t++)
            {
                sum += SolidAngle(verts[tris[t * 3]] - point, verts[tris[t * 3 + 1]] - point, verts[tris[t * 3 + 2]] - point);
            }
            float winding = (float)(sum / (4.0 * System.Math.PI));
            float sign = Mathf.Abs(winding) > 0.5f ? -1f : 1f;
            return sign * Mathf.Sqrt(bestSq);
        }

        // --- geometry ------------------------------------------------------

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
