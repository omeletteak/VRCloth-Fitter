using NUnit.Framework;
using UnityEngine;
using VRClothDeclipper.Core;

namespace VRClothDeclipper.Tests
{
    public class BodyProportionsTests
    {
        [Test]
        public void Measure_ComputesHeadCountFromBoundsAndChin()
        {
            // top at y=1.6, bottom at y=0.0, chin at y=1.4
            // → height 1.6, head 0.2, head-count 8.0
            var verts = new[]
            {
                new Vector3(0f, 1.6f, 0f),
                new Vector3(0.1f, 0f, 0f),
                new Vector3(-0.1f, 0.8f, 0.2f),
            };

            BodyProportions.HeadCount m = BodyProportions.Measure(verts, 1.4f);

            Assert.AreEqual(1.6f, m.height, 1e-4f);
            Assert.AreEqual(0.2f, m.headHeight, 1e-4f);
            Assert.AreEqual(8f, m.headCount, 1e-3f);
            Assert.AreEqual(1.6f, m.topY, 1e-4f);
            Assert.AreEqual(0f, m.bottomY, 1e-4f);
        }

        [Test]
        public void Measure_EmptyVertices_ReturnsZero()
        {
            BodyProportions.HeadCount m = BodyProportions.Measure(new Vector3[0], 1.4f);
            Assert.AreEqual(0f, m.headCount);
            Assert.AreEqual(0f, m.height);
        }

        [Test]
        public void Measure_ChinAtOrAboveTop_GuardsHeadCountToZero()
        {
            // Degenerate: chin >= top → headHeight <= 0 → head-count guarded to 0
            var verts = new[] { new Vector3(0f, 1.0f, 0f), new Vector3(0f, 0f, 0f) };

            BodyProportions.HeadCount m = BodyProportions.Measure(verts, 1.5f);

            Assert.AreEqual(0f, m.headCount);
        }
    }
}
