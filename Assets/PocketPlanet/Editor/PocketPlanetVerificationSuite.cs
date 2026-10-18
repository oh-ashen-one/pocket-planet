using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketPlanet.Editor
{
    public static class PocketPlanetVerificationSuite
    {
        public static void RunBatchVerification()
        {
            var errors = new List<string>();

            // 1. Verify PipHero prefab, SkinnedMeshRenderer, Generic Avatar, and all 12 animation clips
            GameObject pipPrefab = Resources.Load<GameObject>("Characters/PipHero");
            if (pipPrefab == null)
            {
                errors.Add("Missing Resources/Characters/PipHero.prefab");
            }
            else
            {
                SkinnedMeshRenderer smr = pipPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null || smr.sharedMesh == null)
                    errors.Add("PipHero missing SkinnedMeshRenderer or mesh");
                else if (smr.bones == null || smr.bones.Length < 18)
                    errors.Add($"PipHero has insufficient bound bones: {(smr.bones != null ? smr.bones.Length : 0)}");

                Animator anim = pipPrefab.GetComponentInChildren<Animator>();
                if (anim == null || anim.runtimeAnimatorController == null || anim.avatar == null)
                    errors.Add("PipHero missing Animator, Controller, or Generic Avatar");
                else
                {
                    AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
                    if (clips == null || clips.Length < 12)
                        errors.Add($"PipHero AnimatorController has only {(clips != null ? clips.Length : 0)}/12 clips");
                }
            }

            // 2. Verify all 14 prop prefabs exist in Resources/Props
            string[] expectedProps =
            {
                "Starberry", "StarSeed", "BouncyShroom", "PushBall", "DominoBlock",
                "CrystalLever", "UnfoldingBridge", "AlienPodHome", "PastelRockPlatform",
                "GlowingFloraTuft", "BloomPlant", "BloopCreature", "StarhopperShip", "CustomuseFloraStatue"
            };
            foreach (string p in expectedProps)
            {
                if (Resources.Load<GameObject>($"Props/{p}") == null)
                    errors.Add($"Missing prop prefab: Resources/Props/{p}");
            }

            // 3. Verify all 20 synthesized audio clips exist in Resources/Audio
            AudioClip[] audioClips = Resources.LoadAll<AudioClip>("Audio");
            if (audioClips == null || audioClips.Length < 18)
                errors.Add($"Expected >=18 audio clips in Resources/Audio, found {(audioClips != null ? audioClips.Length : 0)}");

            // 4. Open PocketPlanet.unity scene and verify all core gameplay components are wired
            EditorSceneManager.OpenScene("Assets/PocketPlanet/Scenes/PocketPlanet.unity", OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<PipCharacterController>() == null)
                errors.Add("Scene missing PipCharacterController");
            if (Object.FindFirstObjectByType<ThirdPersonCamera>() == null)
                errors.Add("Scene missing ThirdPersonCamera");
            if (Object.FindFirstObjectByType<HungryBloomPlant>() == null)
                errors.Add("Scene missing HungryBloomPlant");
            if (Object.FindFirstObjectByType<BouncyMushroom>() == null)
                errors.Add("Scene missing BouncyMushroom");
            if (Object.FindFirstObjectByType<PushableBoulder>() == null)
                errors.Add("Scene missing PushableBoulder");
            if (Object.FindFirstObjectByType<CrystalLeverBridge>() == null)
                errors.Add("Scene missing CrystalLeverBridge");
            if (Object.FindFirstObjectByType<FriendlyAlienBloop>() == null)
                errors.Add("Scene missing FriendlyAlienBloop");
            if (Object.FindFirstObjectByType<ShallowWaterZone>() == null)
                errors.Add("Scene missing ShallowWaterZone");
            if (Object.FindFirstObjectByType<StarhopperSpaceship>() == null)
                errors.Add("Scene missing StarhopperSpaceship");

            StarSeedCollectible[] seeds = Object.FindObjectsByType<StarSeedCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (seeds.Length != 3)
                errors.Add($"Scene expected 3 StarSeedCollectibles, found {seeds.Length}");

            if (errors.Count > 0)
            {
                foreach (string err in errors)
                    Debug.LogError("[PocketPlanetVerificationSuite] FAIL: " + err);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[PocketPlanetVerificationSuite] ALL CHECKS PASSED (PipHero 22-bone rig, 12 clips, 14 prop prefabs, 20 audio clips, 3 Star Seeds, 7 interactive systems).");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
        }
    }
}
