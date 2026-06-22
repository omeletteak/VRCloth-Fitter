using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Covers the pure transform-path helpers of <see cref="VRClothBakeTestScene"/>
    /// (record a garment's name-path under the avatar, resolve it inside the baked
    /// clone). The NDMF bake / AAO toggling need a real avatar and are verified by
    /// hand in the test project.
    /// </summary>
    public class VRClothBakeTestSceneTests
    {
        GameObject root;
        Transform b, leaf;

        [SetUp]
        public void SetUp()
        {
            // root / A / B / leaf  (+ root / A / sibling)
            root = new GameObject("root");
            var a = new GameObject("A").transform;
            a.SetParent(root.transform);
            b = new GameObject("B").transform;
            b.SetParent(a);
            leaf = new GameObject("leaf").transform;
            leaf.SetParent(b);
            var sibling = new GameObject("sibling").transform;
            sibling.SetParent(a);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void TransformPath_DescendantChain()
        {
            List<string> path = VRClothBakeTestScene.TransformPath(root.transform, leaf);
            Assert.AreEqual(new[] { "A", "B", "leaf" }, path);
        }

        [Test]
        public void TransformPath_TargetIsRoot_ReturnsEmpty()
        {
            List<string> path = VRClothBakeTestScene.TransformPath(root.transform, root.transform);
            Assert.IsNotNull(path);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TransformPath_NotUnderRoot_ReturnsNull()
        {
            var other = new GameObject("other");
            try
            {
                Assert.IsNull(VRClothBakeTestScene.TransformPath(root.transform, other.transform));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ResolvePath_RoundTripsToSameTransform()
        {
            List<string> path = VRClothBakeTestScene.TransformPath(root.transform, leaf);
            Transform resolved = VRClothBakeTestScene.ResolvePath(root.transform, path);
            Assert.AreSame(leaf, resolved);
        }

        [Test]
        public void ResolvePath_EmptyPath_ReturnsRoot()
        {
            Transform resolved = VRClothBakeTestScene.ResolvePath(root.transform, new string[0]);
            Assert.AreSame(root.transform, resolved);
        }

        [Test]
        public void ResolvePath_MissingName_ReturnsNull()
        {
            Transform resolved = VRClothBakeTestScene.ResolvePath(root.transform, new[] { "A", "nope" });
            Assert.IsNull(resolved);
        }
    }
}
