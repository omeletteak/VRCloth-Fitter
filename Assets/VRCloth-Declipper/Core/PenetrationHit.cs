using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// A cloth vertex found inside a body capsule (inflated by the detection
    /// margin). Produced by penetration detection, consumed by push-out and
    /// the scene-view visualizer.
    /// </summary>
    public struct PenetrationHit
    {
        /// <summary>Index of the vertex in the cloth mesh.</summary>
        public int vertexIndex;

        /// <summary>Vertex position in world space at detection time.</summary>
        public Vector3 position;

        /// <summary>
        /// How far the vertex sits below the margin surface, in meters.
        /// Equals margin minus the signed distance to the capsule, so it is
        /// positive for every detected vertex and grows with depth.
        /// </summary>
        public float depth;

        /// <summary>Index of the closest capsule in the proxy capsule list.</summary>
        public int capsuleIndex;

        public PenetrationHit(int vertexIndex, Vector3 position, float depth, int capsuleIndex)
        {
            this.vertexIndex = vertexIndex;
            this.position = position;
            this.depth = depth;
            this.capsuleIndex = capsuleIndex;
        }
    }
}
