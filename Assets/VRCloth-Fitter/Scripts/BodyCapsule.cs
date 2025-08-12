using UnityEngine;

namespace VRClothFitter
{
    /// <summary>
    /// An avatar's body part approximated by a capsule.
    /// </summary>
    public struct BodyCapsule
    {
        public Vector3 start;
        public Vector3 end;
        public float radius;

        public BodyCapsule(Vector3 start, Vector3 end, float radius)
        {
            this.start = start;
            this.end = end;
            this.radius = radius;
        }
    }
}
