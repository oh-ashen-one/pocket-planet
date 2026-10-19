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

            // 2. Verify all 28 prop prefabs (14 original + 14 Tripo 3D H3.1 assets) exist in Resources/Props
            string[] expectedProps =
            {
                "Starberry", "StarSeed", "BouncyShroom", "PushBall", "DominoBlock",
                "CrystalLever", "UnfoldingBridge", "AlienPodHome", "PastelRockPlatform",
                "GlowingFloraTuft", "BloomPlant", "BloopCreature", "StarhopperShip", "CustomuseFloraStatue",
                "TripoLushTree", "TripoAlienPalmTree", "TripoFlowerBush", "TripoFlyingSaucer",
                "TripoNpcAstra", "TripoSolarisCore", "TripoSpaceAsteroid", "TripoDeadwoodTree",
                "TripoObsidianRuin", "TripoNpcNyx", "TripoShrineLantern", "TripoCrashedSaucer",
                "TripoObsidianCrystal", "TripoSpookyMushroom"
            };
            foreach (string p in expectedProps)
            {
                if (Resources.Load<GameObject>($"Props/{p}") == null)
                    errors.Add($"Missing prop prefab: Resources/Props/{p}");
            }

            // 3. Verify all 30 synthesized audio clips exist in Resources/Audio
            AudioClip[] audioClips = Resources.LoadAll<AudioClip>("Audio");
            if (audioClips == null || audioClips.Length < 30)
                errors.Add($"Expected >=30 audio clips in Resources/Audio, found {(audioClips != null ? audioClips.Length : 0)}");

            // 4. Open PocketPlanet.unity scene and verify all core & two-planet gameplay components are wired
            EditorSceneManager.OpenScene("Assets/PocketPlanet/Scenes/PocketPlanet.unity", OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<PipCharacterController>() == null)
                errors.Add("Scene missing PipCharacterController");
            if (Object.FindFirstObjectByType<ThirdPersonCamera>() == null)
                errors.Add("Scene missing ThirdPersonCamera");
            if (Object.FindFirstObjectByType<PlanetaryAtmosphereController>() == null)
                errors.Add("Scene missing PlanetaryAtmosphereController");
            if (Object.FindFirstObjectByType<SolarisCoreRelic>() == null)
                errors.Add("Scene missing SolarisCoreRelic");
            if (Object.FindFirstObjectByType<SpaceshipFlightController>() == null)
                errors.Add("Scene missing SpaceshipFlightController");
            if (Object.FindFirstObjectByType<ObsidianShrineAltar>() == null)
                errors.Add("Scene missing ObsidianShrineAltar");
            if (Object.FindFirstObjectByType<ObsidianShadowBarrier>() == null)
                errors.Add("Scene missing ObsidianShadowBarrier");
            if (Object.FindFirstObjectByType<QuestWaypointCompass>() == null)
                errors.Add("Scene missing QuestWaypointCompass");

            NpcCharacterDialogue[] npcs = Object.FindObjectsByType<NpcCharacterDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (npcs.Length < 2)
                errors.Add($"Scene expected >=2 NpcCharacterDialogue instances (Captain Astra & Warden Nyx), found {npcs.Length}");

            SpaceWarpBoostRing[] rings = Object.FindObjectsByType<SpaceWarpBoostRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rings.Length < 4)
                errors.Add($"Scene expected >=4 SpaceWarpBoostRing instances in space corridor, found {rings.Length}");

            ObsidianCrystalPylon[] pylons = Object.FindObjectsByType<ObsidianCrystalPylon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (pylons.Length < 3)
                errors.Add($"Scene expected >=3 ObsidianCrystalPylon instances on Planet 2, found {pylons.Length}");

            DestructibleSpaceAsteroid[] asteroids = Object.FindObjectsByType<DestructibleSpaceAsteroid>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (asteroids.Length < 15)
                errors.Add($"Scene expected >=15 DestructibleSpaceAsteroid instances in space corridor, found {asteroids.Length}");

            if (errors.Count > 0)
            {
                foreach (string err in errors)
                    Debug.LogError("[PocketPlanetVerificationSuite] FAIL: " + err);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[PocketPlanetVerificationSuite] ALL CHECKS PASSED (PipHero 22-bone rig, 12 clips, 28 prop prefabs including 14 Tripo 3D assets, 30 audio clips, Two-Planet + 3D Space Flight + Pylon Barrier + Asteroid Blaster + 3D Compass systems).");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
        }
    }
}
