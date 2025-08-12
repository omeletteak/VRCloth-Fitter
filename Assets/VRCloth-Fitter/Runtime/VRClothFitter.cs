// Assets/VRCloth-Fitter/Runtime/VRClothFitter.cs

using UnityEngine;

namespace VRClothFitter
{
    public class VRClothFitter : MonoBehaviour
    {
        [Header("Targets")]
        public GameObject targetAvatar;
        public GameObject sourceAvatar; // Optional: The avatar this cloth was originally made for.
        public GameObject clothRoot;

        [HideInInspector]
        public SkinnedMeshRenderer clothToDeform;

        public enum QualityMode { Light, Medium, High }
        [Header("Settings")]
        public QualityMode mode = QualityMode.Light;

        private void Reset()
        {
            AutoDetectComponents();
        }

        private void OnValidate()
        {
            AutoDetectComponents();
        }

        public void AutoDetectComponents()
        {
            // 1. 衣装ルートの取得 (このGameObject自身)
            if (clothRoot == null) clothRoot = this.gameObject;

            // 2. 変形対象メッシュの取得 (clothRootまたはその子から)
            if (clothToDeform == null && clothRoot != null)
            {
                clothToDeform = clothRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            
            // 3. 対象アバター(変換先)の取得 (親を遡って探す)
            if (targetAvatar == null && clothRoot != null && clothRoot.transform.parent != null)
            {
                Transform parent = clothRoot.transform.parent;
                while (parent != null)
                {
                    Animator animator = parent.GetComponent<Animator>();
                    if (animator != null && animator.isHuman)
                    {
                        targetAvatar = parent.gameObject;
                        break; 
                    }
                    parent = parent.parent;
                }
            }
        }
    }
}