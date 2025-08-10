using System.Collections.Generic;
using UnityEngine;

namespace VRClothFitter
{
    [AddComponentMenu("VRCloth Fitter/Cage Deformer")]
    public class VRClothFitterCageDeformer : MonoBehaviour
    {
        [Tooltip("The mesh renderer to be deformed.")]
        public SkinnedMeshRenderer targetRenderer;

        [Tooltip("The number of control points on each axis of the cage.")]
        public Vector3Int cageDivisions = new Vector3Int(4, 4, 4);

        [HideInInspector]
        public Vector3[] cageControlPoints;
        
        [HideInInspector]
        public bool isEditing = false;
    }
}
