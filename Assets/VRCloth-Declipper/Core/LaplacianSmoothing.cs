using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Uniform-weight Laplacian smoothing of a per-vertex vector field,
    /// restricted to a vertex region. The solver smooths the push-out
    /// displacement field (not vertex positions), so the original cloth
    /// detail is preserved and only the correction is blended; vertices
    /// outside the region keep their field value (zero for untouched ones)
    /// and act as natural anchors.
    /// </summary>
    public static class LaplacianSmoothing
    {
        /// <summary>
        /// Expands the seed vertices outward by <paramref name="rings"/>
        /// neighbor hops and returns the region as cluster representatives.
        /// </summary>
        public static HashSet<int> ExpandRegion(VertexAdjacency adjacency, IEnumerable<int> seedVertices, int rings)
        {
            var region = new HashSet<int>();
            if (adjacency == null || seedVertices == null)
            {
                return region;
            }

            var frontier = new List<int>();
            foreach (int seed in seedVertices)
            {
                int rep = adjacency.RepresentativeOf(seed);
                if (region.Add(rep))
                {
                    frontier.Add(rep);
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                var next = new List<int>();
                foreach (int rep in frontier)
                {
                    foreach (int neighbor in adjacency.NeighborsOf(rep))
                    {
                        if (region.Add(neighbor))
                        {
                            next.Add(neighbor);
                        }
                    }
                }
                frontier = next;
            }
            return region;
        }

        /// <summary>
        /// Pulls every region vertex's field value toward the average of its
        /// neighbors' values: f' = Lerp(f, neighborAverage, lambda), repeated
        /// <paramref name="iterations"/> times. Neighbors outside the region
        /// participate in the average but never change, anchoring the
        /// boundary. Welded clones are written together.
        /// </summary>
        public static void Smooth(Vector3[] field, VertexAdjacency adjacency, HashSet<int> regionRepresentatives, float lambda, int iterations)
        {
            if (field == null || adjacency == null || regionRepresentatives == null || regionRepresentatives.Count == 0)
            {
                return;
            }

            var updates = new List<KeyValuePair<int, Vector3>>(regionRepresentatives.Count);
            for (int i = 0; i < iterations; i++)
            {
                updates.Clear();
                foreach (int rep in regionRepresentatives)
                {
                    var neighbors = adjacency.NeighborsOf(rep);
                    if (neighbors.Count == 0)
                    {
                        continue;
                    }
                    Vector3 sum = Vector3.zero;
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        sum += field[neighbors[n]];
                    }
                    Vector3 smoothed = Vector3.Lerp(field[rep], sum / neighbors.Count, lambda);
                    updates.Add(new KeyValuePair<int, Vector3>(rep, smoothed));
                }

                foreach (var update in updates)
                {
                    var clones = adjacency.MembersOf(update.Key);
                    for (int m = 0; m < clones.Count; m++)
                    {
                        field[clones[m]] = update.Value;
                    }
                }
            }
        }
    }
}
