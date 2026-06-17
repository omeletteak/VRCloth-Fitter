using System.Collections.Generic;

namespace VRClothDeclipper
{
    public static class VRClothPenetrationDetector
    {
        /// <summary>
        /// Scans every captured cloth renderer against the proxy capsules.
        /// Fills <see cref="ClothSnapshot.hits"/> per renderer and returns all
        /// hits flattened, for logging and the scene-view heatmap. Vertex
        /// indices in the flattened list are local to their snapshot.
        /// </summary>
        public static List<PenetrationHit> Detect(IReadOnlyList<ClothSnapshot> cloth, IReadOnlyList<BodyCapsule> capsules, float margin)
        {
            var allHits = new List<PenetrationHit>();
            if (cloth == null)
            {
                return allHits;
            }

            foreach (var snapshot in cloth)
            {
                snapshot.hits = PenetrationDetection.Scan(snapshot.worldVertices, capsules, margin);
                allHits.AddRange(snapshot.hits);
            }
            return allHits;
        }

        /// <summary>
        /// Same as the capsule scan, against an arbitrary body collider (the
        /// mesh-SDF backend, docs/DESIGN.md §6). Hits carry no capsule index.
        /// </summary>
        public static List<PenetrationHit> Detect(IReadOnlyList<ClothSnapshot> cloth, IBodyCollider collider, float margin)
        {
            var allHits = new List<PenetrationHit>();
            if (cloth == null)
            {
                return allHits;
            }

            foreach (var snapshot in cloth)
            {
                snapshot.hits = PenetrationDetection.Scan(snapshot.worldVertices, collider, margin);
                allHits.AddRange(snapshot.hits);
            }
            return allHits;
        }
    }
}
