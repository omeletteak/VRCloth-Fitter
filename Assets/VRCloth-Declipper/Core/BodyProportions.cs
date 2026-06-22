using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper.Core
{
    /// <summary>
    /// Pure body-proportion math, editor-independent. Head-count (頭身) is the
    /// scale-invariant proportion height ÷ head-height. VRChat avatars have
    /// arbitrary absolute scale, so a cm height is meaningless as a body
    /// descriptor, but head-count characterises the body family
    /// (docs/FAMILY_MODEL.md, "頭身=硬い軸").
    /// </summary>
    public static class BodyProportions
    {
        public struct HeadCount
        {
            public float topY;        // highest body-mesh vertex (頭頂; includes hair/accessories)
            public float bottomY;     // lowest body-mesh vertex (足裏)
            public float chinY;       // bone proxy for the chin (Neck or Head)
            public float height;      // topY - bottomY
            public float headHeight;  // topY - chinY
            public float headCount;   // height / headHeight (0 when headHeight <= 0)
        }

        /// <summary>
        /// Computes head-count from body-mesh world vertices and a chin
        /// reference Y (e.g. the Neck or Head bone position). Top/bottom come
        /// from the mesh's vertical bounds; the chin is a bone proxy. Both carry
        /// model error — hair/accessories raise the top, and the bone only
        /// approximates the chin — so treat the result as an estimate to
        /// calibrate, not a fixed value (docs/DIAGNOSTIC_HONESTY.md §2).
        /// </summary>
        public static HeadCount Measure(IReadOnlyList<Vector3> bodyWorldVertices, float chinY)
        {
            if (bodyWorldVertices == null || bodyWorldVertices.Count == 0)
            {
                return new HeadCount { chinY = chinY };
            }

            float top = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            for (int i = 0; i < bodyWorldVertices.Count; i++)
            {
                float y = bodyWorldVertices[i].y;
                if (y > top) top = y;
                if (y < bottom) bottom = y;
            }
            return Measure(top, bottom, chinY);
        }

        /// <summary>
        /// Computes head-count from precomputed vertical bounds. Use this when the
        /// body spans several meshes (a split body / head / hair) and the caller
        /// has unioned their bounds: <paramref name="topY"/> must be the crown, so
        /// a single mesh that omits the head collapses the head height and blows
        /// the count up (the failure that motivated this overload). Same estimate
        /// caveat as the vertex overload (docs/DIAGNOSTIC_HONESTY.md §2).
        /// </summary>
        public static HeadCount Measure(float topY, float bottomY, float chinY)
        {
            float headHeight = topY - chinY;
            return new HeadCount
            {
                topY = topY,
                bottomY = bottomY,
                chinY = chinY,
                height = topY - bottomY,
                headHeight = headHeight,
                headCount = headHeight > 1e-6f ? (topY - bottomY) / headHeight : 0f,
            };
        }
    }
}
