using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Vertex neighbor map built from a triangle list. Vertices that share a
    /// position (meshes duplicate vertices along UV/normal seams) are welded
    /// into one cluster, so smoothing moves the clones together instead of
    /// tearing the mesh open along seams.
    /// </summary>
    public class VertexAdjacency
    {
        readonly int[] representatives;
        readonly Dictionary<int, List<int>> members;
        readonly Dictionary<int, List<int>> neighbors;

        static readonly List<int> Empty = new List<int>();

        VertexAdjacency(int[] representatives, Dictionary<int, List<int>> members, Dictionary<int, List<int>> neighbors)
        {
            this.representatives = representatives;
            this.members = members;
            this.neighbors = neighbors;
        }

        public int VertexCount => representatives.Length;

        /// <summary>The welded cluster this vertex belongs to.</summary>
        public int RepresentativeOf(int vertex) => representatives[vertex];

        /// <summary>All vertices sharing the representative's position (itself included).</summary>
        public IReadOnlyList<int> MembersOf(int representative) =>
            members.TryGetValue(representative, out var list) ? list : Empty;

        /// <summary>Neighboring clusters (as representatives) connected by a triangle edge.</summary>
        public IReadOnlyList<int> NeighborsOf(int representative) =>
            neighbors.TryGetValue(representative, out var list) ? list : Empty;

        public static VertexAdjacency Build(IReadOnlyList<Vector3> positions, int[] triangles)
        {
            int count = positions != null ? positions.Count : 0;
            var representatives = new int[count];
            var firstAtPosition = new Dictionary<Vector3, int>(count);
            var members = new Dictionary<int, List<int>>();

            for (int v = 0; v < count; v++)
            {
                if (!firstAtPosition.TryGetValue(positions[v], out int rep))
                {
                    rep = v;
                    firstAtPosition.Add(positions[v], rep);
                    members.Add(rep, new List<int>());
                }
                representatives[v] = rep;
                members[rep].Add(v);
            }

            var neighborSets = new Dictionary<int, HashSet<int>>();
            void Connect(int a, int b)
            {
                int ra = representatives[a];
                int rb = representatives[b];
                if (ra == rb)
                {
                    return;
                }
                if (!neighborSets.TryGetValue(ra, out var setA))
                {
                    neighborSets.Add(ra, setA = new HashSet<int>());
                }
                if (!neighborSets.TryGetValue(rb, out var setB))
                {
                    neighborSets.Add(rb, setB = new HashSet<int>());
                }
                setA.Add(rb);
                setB.Add(ra);
            }

            if (triangles != null)
            {
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Connect(triangles[t], triangles[t + 1]);
                    Connect(triangles[t + 1], triangles[t + 2]);
                    Connect(triangles[t + 2], triangles[t]);
                }
            }

            var neighbors = new Dictionary<int, List<int>>(neighborSets.Count);
            foreach (var pair in neighborSets)
            {
                neighbors.Add(pair.Key, new List<int>(pair.Value));
            }
            return new VertexAdjacency(representatives, members, neighbors);
        }
    }
}
