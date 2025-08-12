using UnityEngine;

namespace VRClothFitter
{
    public static class VRClothProxyGenerator
    {
        public static void Generate(GameObject avatarObject)
        {
            if (avatarObject == null)
            {
                Debug.LogError("Avatar is not specified.");
                return;
            }

            Animator animator = avatarObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("The specified object does not have a Humanoid Animator.");
                return;
            }

            Debug.Log("[VRClothFitter] Generating capsule proxy...");
            Debug.Log("Finding bones...");

            // 主要なボーンを取得してログに出力
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips) Debug.Log("Found Hips: " + hips.name);

            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine) Debug.Log("Found Spine: " + spine.name);

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest) Debug.Log("Found Chest: " + chest.name);
            
            Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperChest) Debug.Log("Found UpperChest: " + upperChest.name);

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (neck) Debug.Log("Found Neck: " + neck.name);

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head) Debug.Log("Found Head: " + head.name);
        }
    }
}
