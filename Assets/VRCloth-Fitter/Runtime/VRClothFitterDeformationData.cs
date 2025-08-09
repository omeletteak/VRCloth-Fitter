using UnityEngine;
using System.Collections.Generic;

namespace VRClothFitter
{
    [System.Serializable]
    public class DeformationAnchorPair
    {
        [Tooltip("Name for reference in the editor.")]
        public string name;
        
        [Tooltip("Anchor position on the Avatar mesh, in local space.")]
        public Vector3 avatarAnchor;

        [Tooltip("Corresponding anchor position on the Cloth mesh, in local space.")]
        public Vector3 clothAnchor;
    }

    [AddComponentMenu("VRCloth Fitter/Deformation Data")]
    public class VRClothFitterDeformationData : MonoBehaviour
    {
        [Tooltip("List of anchor pairs that define the mesh deformation.")]
        public List<DeformationAnchorPair> anchorPairs = new List<DeformationAnchorPair>();
    }
}
