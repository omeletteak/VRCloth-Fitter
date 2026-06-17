using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    public class LaplacianSmoothingTests
    {
        const float Eps = 1e-5f;

        // A strip 0-1-2-3-4 along X: triangles (0,1,2) and (2,3,4).
        static Vector3[] StripPositions()
        {
            return new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
            };
        }

        static readonly int[] StripTriangles = { 0, 1, 2, 2, 3, 4 };

        [Test]
        public void ExpandRegion_GrowsByOneNeighborHopPerRing()
        {
            var adjacency = VertexAdjacency.Build(StripPositions(), StripTriangles);

            var ringsZero = LaplacianSmoothing.ExpandRegion(adjacency, new[] { 0 }, 0);
            var ringsOne = LaplacianSmoothing.ExpandRegion(adjacency, new[] { 0 }, 1);
            var ringsTwo = LaplacianSmoothing.ExpandRegion(adjacency, new[] { 0 }, 2);

            CollectionAssert.AreEquivalent(new[] { 0 }, ringsZero);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, ringsOne);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, ringsTwo);
        }

        [Test]
        public void Smooth_PullsVertexToNeighborAverage()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 1f, 0f), // spiked upward
                new Vector3(2f, 0f, 0f),
            };
            var adjacency = VertexAdjacency.Build(positions, new[] { 0, 1, 2 });
            var region = new HashSet<int> { 1 };

            LaplacianSmoothing.Smooth(positions, adjacency, region, 1f, 1);

            AssertVector(new Vector3(1f, 0f, 0f), positions[1]);
        }

        [Test]
        public void Smooth_DoesNotMoveVerticesOutsideTheRegion()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
            };
            var adjacency = VertexAdjacency.Build(positions, new[] { 0, 1, 2 });
            var region = new HashSet<int> { 1 };

            LaplacianSmoothing.Smooth(positions, adjacency, region, 1f, 3);

            AssertVector(new Vector3(0f, 0f, 0f), positions[0]);
            AssertVector(new Vector3(2f, 0f, 0f), positions[2]);
        }

        [Test]
        public void Smooth_LambdaScalesThePull()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
            };
            var adjacency = VertexAdjacency.Build(positions, new[] { 0, 1, 2 });

            LaplacianSmoothing.Smooth(positions, adjacency, new HashSet<int> { 1 }, 0.5f, 1);

            AssertVector(new Vector3(1f, 0.5f, 0f), positions[1]);
        }

        [Test]
        public void Smooth_MovesWeldedClonesTogether()
        {
            // v3 clones v1's position; the triangles touch the clone, not v1.
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(1f, 1f, 0f), // clone of 1
            };
            var adjacency = VertexAdjacency.Build(positions, new[] { 0, 1, 2, 0, 3, 2 });
            var region = new HashSet<int> { adjacency.RepresentativeOf(3) };

            LaplacianSmoothing.Smooth(positions, adjacency, region, 1f, 1);

            AssertVector(new Vector3(1f, 0f, 0f), positions[1]);
            AssertVector(new Vector3(1f, 0f, 0f), positions[3]);
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, $"x of {actual}");
            Assert.AreEqual(expected.y, actual.y, Eps, $"y of {actual}");
            Assert.AreEqual(expected.z, actual.z, Eps, $"z of {actual}");
        }
    }
}
