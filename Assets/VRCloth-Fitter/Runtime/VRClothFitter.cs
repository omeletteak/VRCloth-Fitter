using UnityEngine;

namespace VRClothFitter
{
    public enum FittingMode
    {
        Fallback,
        HighPrecision
    }

    /// <summary>
    /// The main component to be attached to a cloth GameObject.
    /// It holds the reference to the target avatar and acts as an entry point for the custom editor.
    /// </summary>
    [AddComponentMenu("VRCloth Fitter/VRCloth Fitter")]
    public class VRClothFitter : MonoBehaviour
    {
        [Tooltip("The fitting mode to use. Fallback is recommended if you don't have the source avatar.")]
        public FittingMode fittingMode = FittingMode.Fallback;

        [Tooltip("The avatar you want to fit the cloth to.")]
        public GameObject targetAvatarObject;
        
        [Tooltip("The original avatar the cloth was made for. Used in High-Precision mode.")]
        public GameObject sourceAvatarObject;
    }
}
