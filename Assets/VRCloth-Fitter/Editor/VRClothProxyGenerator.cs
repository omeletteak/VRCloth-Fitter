using UnityEngine;
using System.Collections.Generic;

namespace VRClothFitter
{
    public static class VRClothProxyGenerator
    {
        public static List<BodyCapsule> Generate(GameObject avatarObject)
        {
            if (avatarObject == null)
            {
                Debug.LogError("Avatar is not specified.");
                return null;
            }

            Animator animator = avatarObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("The specified object does not have a Humanoid Animator.");
                return null;
            }

            var capsules = new List<BodyCapsule>();

            // Helper function to add a capsule between two bones
            void AddCapsule(HumanBodyBones startBone, HumanBodyBones endBone, float radius)
            {
                Transform start = animator.GetBoneTransform(startBone);
                Transform end = animator.GetBoneTransform(endBone);
                if (start && end)
                {
                    capsules.Add(new BodyCapsule(start.position, end.position, radius, $"{startBone}→{endBone}"));
                }
            }

            // --- 胴体 (Torso) ---
            AddCapsule(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.12f);
            AddCapsule(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.12f);
            AddCapsule(HumanBodyBones.Chest, HumanBodyBones.UpperChest, 0.12f);
            AddCapsule(HumanBodyBones.UpperChest, HumanBodyBones.Neck, 0.08f);
            AddCapsule(HumanBodyBones.Neck, HumanBodyBones.Head, 0.08f);

            // --- 脚 (Legs) ---
            AddCapsule(HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, 0.08f);
            AddCapsule(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.06f);
            AddCapsule(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.05f);
            
            AddCapsule(HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, 0.08f);
            AddCapsule(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.06f);
            AddCapsule(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.05f);

            // --- 腕 (Arms) ---
            AddCapsule(HumanBodyBones.UpperChest, HumanBodyBones.LeftShoulder, 0.06f);
            AddCapsule(HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, 0.06f);
            AddCapsule(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.05f);
            AddCapsule(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.04f);

            AddCapsule(HumanBodyBones.UpperChest, HumanBodyBones.RightShoulder, 0.06f);
            AddCapsule(HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, 0.06f);
            AddCapsule(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.05f);
            AddCapsule(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.04f);

            Debug.Log($"[VRClothFitter] Generated {capsules.Count} capsules for the proxy body.");
            return capsules;
        }
    }
}
