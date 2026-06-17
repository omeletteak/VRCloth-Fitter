using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class VertexAdjacencyTests
    {
        [Test]
        public void Build_ConnectsTriangleEdges()
        {
            // Two triangles forming a quad: (0,1,2) and (2,1,3).
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 1f),
            };
            var triangles = new[] { 0, 1, 2, 2, 1, 3 };

            var adjacency = VertexAdjacency.Build(positions, triangles);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, adjacency.NeighborsOf(0));
            CollectionAssert.AreEquivalent(new[] { 0, 2, 3 }, adjacency.NeighborsOf(1));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 3 }, adjacency.NeighborsOf(2));
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, adjacency.NeighborsOf(3));
        }

        [Test]
        public void Build_WeldsVerticesSharingAPosition()
        {
            // v4 duplicates v1's position (a UV seam): the two triangles only
            // share the seam positionally, not by index.
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f), // 0
                new Vector3(1f, 0f, 0f), // 1
                new Vector3(0f, 0f, 1f), // 2
                new Vector3(2f, 0f, 1f), // 3
                new Vector3(1f, 0f, 0f), // 4 = clone of 1
            };
            var triangles = new[] { 0, 1, 2, 2, 4, 3 };

            var adjacency = VertexAdjacency.Build(positions, triangles);

            Assert.AreEqual(1, adjacency.RepresentativeOf(4));
            CollectionAssert.AreEquivalent(new[] { 1, 4 }, adjacency.MembersOf(1));
            // The clone's edges count as the representative's edges.
            CollectionAssert.AreEquivalent(new[] { 0, 2, 3 }, adjacency.NeighborsOf(1));
        }

        [Test]
        public void Build_SelfClusterEdgesAreIgnored()
        {
            // A degenerate triangle touching the same welded position twice
            // must not create a self-neighbor.
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 0f), // clone of 1
            };
            var triangles = new[] { 0, 1, 2 };

            var adjacency = VertexAdjacency.Build(positions, triangles);

            CollectionAssert.AreEquivalent(new[] { 0 }, adjacency.NeighborsOf(1));
            Assert.IsFalse(adjacency.NeighborsOf(1).Contains(1));
        }
    }
}
