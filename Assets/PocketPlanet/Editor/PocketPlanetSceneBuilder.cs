using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PocketPlanet.Editor
{
    public static class PocketPlanetSceneBuilder
    {
        private const string ScenePath = "Assets/PocketPlanet/Scenes/PocketPlanet.unity";
        private const string BuildAppPath = "Builds/PocketPlanet.app";

        [MenuItem("PocketPlanet/Build Complete Scene & Configure Build Settings")]
        public static void BuildCompleteSceneMenu()
        {
            PocketPlanetAssetImporter.ImportAll();
            BuildScene();
        }

        public static void BuildAllBatch()
        {
            try
            {
                PocketPlanetAssetImporter.ImportAll();
                BuildScene();
                BuildMacStandalonePlayer();
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PocketPlanetSceneBuilder] BuildAllBatch failed: " + ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Configure Toybox Space Atmosphere, Lighting & Starry Sky Elements
            ConfigureEnvironmentLighting();
            GameObject worldRoot = new GameObject("PocketPlanetWorld");

            // 2. Build sculpted Unity Terrain + Pastel Terraced Cliffs + Sparkling Shallow Pond + Chasm
            ShallowWaterZone shallowPond = BuildGardenTerrainAndLandmarks(worldRoot.transform);
            BuildDistantSpacePlanetsAndStars(worldRoot.transform);

            // 3. Spawn Audio & GameManager
            GameObject systemsGo = new GameObject("GameSystems");
            systemsGo.AddComponent<PocketPlanetAudio>();
            PocketPlanetGameManager gm = systemsGo.AddComponent<PocketPlanetGameManager>();
            PlaytestVerificationHarness harness = systemsGo.AddComponent<PlaytestVerificationHarness>();
            harness.gameManager = gm;
            harness.shallowPond = shallowPond;

            // 4. Spawn Hero Pip & Third-Person Camera
            PipCharacterController pip = SpawnHeroPip(new Vector3(0f, 0.05f, -4.5f), out ThirdPersonCamera tpCam);
            gm.player = pip;
            gm.thirdPersonCamera = tpCam;
            harness.player = pip;
            harness.thirdPersonCamera = tpCam;

            // 5. Central Clearing: Broken SS Starhopper Spaceship (Customuse 3D asset)
            GameObject shipGo = SpawnProp("StarhopperShip", new Vector3(0f, 0.0f, 3.2f), Quaternion.Euler(0f, 18f, -4.5f), Vector3.one, worldRoot.transform, true);
            StarhopperSpaceship ship = shipGo.AddComponent<StarhopperSpaceship>();
            gm.spaceship = ship;
            harness.spaceship = ship;

            // 6. Zone 1 (West / North-West): Starberry Grove & Hungry Bloom-Plant -> Star Seed #1
            BuildZone1BloomGarden(worldRoot.transform, gm, harness);

            // 7. Zone 2 (East): Bouncy Jelly-Shroom Trail, High Ridge, PushBall Slope & Domino Route -> Star Seed #2
            BuildZone2MushroomAndBoulderSlope(worldRoot.transform, gm, harness);

            // 8. Zone 3 (North across Chasm): Crystal Lever, Unfolding Petal Bridge & Alien Pod Village with Friendly Bloop -> Star Seed #3
            BuildZone3BridgeAndPodVillage(worldRoot.transform, gm, harness, pip);

            // 9. Scatter Responsive Glowing Flora Tufts across paths
            ScatterResponsiveFlora(worldRoot.transform, harness);

            Directory.CreateDirectory("Assets/PocketPlanet/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PocketPlanetSceneBuilder] Saved complete scene at " + ScenePath);
        }

        public static void BuildMacStandalonePlayer()
        {
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildAppPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new InvalidOperationException("macOS Standalone build failed: " + report.summary.result);
            }
            Debug.Log("[PocketPlanetSceneBuilder] Built macOS Standalone app at " + BuildAppPath);
        }

        private static void ConfigureEnvironmentLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.42f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.38f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.28f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.14f, 0.12f, 0.28f);
            RenderSettings.fogStartDistance = 46f;
            RenderSettings.fogEndDistance = 130f;

            // Main warm golden-peach sun light (balanced so pastel materials and ground never blow out)
            GameObject sunGo = new GameObject("DirectionalSunLight");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.93f, 0.82f);
            sun.intensity = 0.92f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sunGo.transform.rotation = Quaternion.Euler(44f, -38f, 0f);

            // Subtle cool-lavender fill light for toybox shadow tint
            GameObject fillGo = new GameObject("SkyFillLight");
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.68f, 0.95f);
            fill.intensity = 0.20f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(32f, 145f, 0f);
        }

        private static PipCharacterController SpawnHeroPip(Vector3 pos, out ThirdPersonCamera tpCam)
        {
            GameObject root = new GameObject("PipPlayer");
            root.transform.position = pos;

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.12f;
            cc.radius = 0.30f;
            cc.center = new Vector3(0f, 0.56f, 0f);

            GameObject pipPrefab = Resources.Load<GameObject>("Characters/PipHero");
            GameObject visual;
            if (pipPrefab != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(pipPrefab);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }
            visual.name = "PipVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Create Main Camera with ThirdPersonCamera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.08f, 0.22f); // Deep twilight space indigo
            cam.fieldOfView = 54f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 260f;
            camGo.AddComponent<AudioListener>();

            tpCam = camGo.AddComponent<ThirdPersonCamera>();
            tpCam.target = root.transform;
            tpCam.targetOffset = new Vector3(0f, 0.85f, 0f);
            tpCam.distance = 4.8f;

            PipCharacterController pip = root.AddComponent<PipCharacterController>();
            pip.cameraTransform = camGo.transform;
            pip.visualRoot = visual.transform;
            pip.animator = visual.GetComponentInChildren<Animator>();

            return pip;
        }

        private static ShallowWaterZone BuildGardenTerrainAndLandmarks(Transform parent)
        {
            // Sculpted Unity Terrain underneath as lush outer garden bowl + crisp toybox platform islands
            TerrainData td = new TerrainData
            {
                heightmapResolution = 129,
                size = new Vector3(120f, 14f, 120f)
            };

            float[,] heights = new float[129, 129];
            for (int y = 0; y < 129; y++)
            {
                for (int x = 0; x < 129; x++)
                {
                    float nx = (x / 128f) * 2f - 1f;
                    float ny = (y / 128f) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    // Gentle bowl rim around the outer edge, flat playable center at base height 0.12
                    float h = 0.12f;
                    if (r > 0.55f)
                        h += Mathf.Pow((r - 0.55f) / 0.45f, 1.8f) * 0.32f;
                    heights[y, x] = Mathf.Clamp01(h);
                }
            }
            td.SetHeights(0, 0, heights);

            string terrainAssetPath = "Assets/PocketPlanet/Art/Materials/PocketTerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(terrainAssetPath) != null)
                AssetDatabase.DeleteAsset(terrainAssetPath);
            AssetDatabase.CreateAsset(td, terrainAssetPath);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "GardenPlanetTerrain";
            terrainGo.transform.SetParent(parent, false);
            terrainGo.transform.position = new Vector3(-60f, -1.72f, -60f);

            Terrain terrainComp = terrainGo.GetComponent<Terrain>();
            Material grassMat = CreateSolidMaterial("TurquoiseGrassMat", new Color(0.15f, 0.48f, 0.43f), 0.18f);
            terrainComp.materialTemplate = grassMat;

            // Main toybox meadow disk in center so footing is 100% crisp and colourful
            GameObject mainMeadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mainMeadow.name = "CentralGardenPlateau";
            mainMeadow.transform.SetParent(parent, false);
            mainMeadow.transform.position = new Vector3(0f, -0.50f, 0f);
            mainMeadow.transform.localScale = new Vector3(38f, 0.50f, 38f);
            mainMeadow.GetComponent<Renderer>().sharedMaterial = grassMat;
            ReplaceCylinderColliderWithMeshCollider(mainMeadow);

            // Warm coral-sand cobblestone pathways connecting the 3 zones
            Material pathMat = CreateSolidMaterial("PastelPathMat", new Color(0.78f, 0.60f, 0.45f), 0.22f);
            for (int i = 0; i < 8; i++)
            {
                CreateSteppingStone(parent, new Vector3(-2.0f - i * 1.4f, 0.02f, -1.5f + i * 0.6f), new Vector3(1.4f, 0.08f, 1.4f), pathMat);
                CreateSteppingStone(parent, new Vector3(2.0f + i * 1.4f, 0.02f, -1.0f + i * 0.4f), new Vector3(1.4f, 0.08f, 1.4f), pathMat);
                CreateSteppingStone(parent, new Vector3(0f, 0.02f, 6.5f + i * 1.45f), new Vector3(1.6f, 0.08f, 1.4f), pathMat);
            }

            // Sparkling Shallow Crystal Pond (South-West of clearing) with circular stone rim
            GameObject pondRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pondRim.name = "ShallowPondRim";
            UnityEngine.Object.DestroyImmediate(pondRim.GetComponent<Collider>());
            pondRim.transform.SetParent(parent, false);
            pondRim.transform.position = new Vector3(-6.2f, 0.01f, -6.5f);
            pondRim.transform.localScale = new Vector3(6.9f, 0.04f, 6.9f);
            pondRim.GetComponent<Renderer>().sharedMaterial = pathMat;

            GameObject waterDisk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waterDisk.name = "SparklingShallowPond";
            waterDisk.transform.SetParent(parent, false);
            waterDisk.transform.position = new Vector3(-6.2f, 0.03f, -6.5f);
            waterDisk.transform.localScale = new Vector3(6.3f, 0.05f, 6.3f);
            Material waterMat = CreateSolidMaterial("SparklingPondMat", new Color(0.24f, 0.72f, 0.86f), 0.85f, new Color(0.06f, 0.24f, 0.34f));
            waterDisk.GetComponent<Renderer>().sharedMaterial = waterMat;
            UnityEngine.Object.DestroyImmediate(waterDisk.GetComponent<Collider>());
            BoxCollider wCol = waterDisk.AddComponent<BoxCollider>();
            wCol.size = new Vector3(1.0f, 8.0f, 1.0f);
            wCol.isTrigger = true;
            ShallowWaterZone swz = waterDisk.AddComponent<ShallowWaterZone>();

            // Glowing bioluminescent lily pads & lotus buds floating on the shallow pond surface (top of water is y=0.08)
            Material lilyMat = CreateSolidMaterial("PondLilyPadMat", new Color(0.28f, 0.88f, 0.66f), 0.45f, new Color(0.06f, 0.28f, 0.18f));
            Material lotusMat = CreateSolidMaterial("PondLotusBloomMat", new Color(0.98f, 0.58f, 0.78f), 0.55f, new Color(0.42f, 0.16f, 0.28f));
            Vector3[] lilyOffsets =
            {
                new Vector3(-7.4f, 0.095f, -5.4f),
                new Vector3(-4.9f, 0.095f, -7.2f),
                new Vector3(-7.1f, 0.095f, -7.8f),
                new Vector3(-5.2f, 0.095f, -5.1f),
            };
            for (int i = 0; i < lilyOffsets.Length; i++)
            {
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = $"GlowingLilyPad_{i}";
                UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(parent, false);
                pad.transform.position = lilyOffsets[i];
                pad.transform.localScale = new Vector3(0.78f, 0.025f, 0.78f);
                pad.GetComponent<Renderer>().sharedMaterial = lilyMat;

                if (i % 2 == 0)
                {
                    GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bloom.name = "LotusGlowBud";
                    UnityEngine.Object.DestroyImmediate(bloom.GetComponent<Collider>());
                    bloom.transform.SetParent(pad.transform, false);
                    bloom.transform.localPosition = new Vector3(0f, 3.2f, 0f);
                    bloom.transform.localScale = new Vector3(0.32f, 4.5f, 0.32f);
                    bloom.GetComponent<Renderer>().sharedMaterial = lotusMat;
                }
            }

            // Decorative pastel rock formations, alien pods & mushrooms framing the central clearing
            SpawnProp("PastelRockPlatform", new Vector3(-13.5f, 0f, -9.5f), Quaternion.Euler(0f, 25f, 0f), new Vector3(1.3f, 1.2f, 1.3f), parent, true);
            SpawnProp("PastelRockPlatform", new Vector3(13.5f, 0f, -10.5f), Quaternion.Euler(0f, -40f, 0f), new Vector3(1.4f, 1.5f, 1.4f), parent, true);
            SpawnProp("PastelRockPlatform", new Vector3(-6.8f, 0f, 9.8f), Quaternion.Euler(0f, 15f, 0f), new Vector3(1.1f, 1.1f, 1.1f), parent, true);
            SpawnProp("PastelRockPlatform", new Vector3(6.8f, 0f, 10.2f), Quaternion.Euler(0f, -25f, 0f), new Vector3(1.2f, 1.3f, 1.2f), parent, true);
            SpawnProp("AlienPodHome", new Vector3(-10.5f, 0f, -11.2f), Quaternion.Euler(0f, 135f, 0f), Vector3.one * 0.88f, parent, true);
            SpawnProp("CustomuseFloraStatue", new Vector3(-9.8f, 0f, 6.5f), Quaternion.Euler(0f, 35f, 0f), Vector3.one * 1.15f, parent, true);
            SpawnProp("CustomuseFloraStatue", new Vector3(8.5f, 0f, -8.5f), Quaternion.Euler(0f, -55f, 0f), Vector3.one * 1.05f, parent, true);

            return swz;
        }

        private static void BuildZone1BloomGarden(Transform parent, PocketPlanetGameManager gm, PlaytestVerificationHarness harness)
        {
            GameObject zone1 = new GameObject("Zone1_BloomGarden");
            zone1.transform.SetParent(parent, false);

            // Low pastel terrace for the Hungry Bloom-Plant (top at Y = 0.43m)
            SpawnProp("PastelRockPlatform", new Vector3(-12.5f, 0f, 1.5f), Quaternion.identity, new Vector3(1.35f, 0.25f, 1.35f), zone1.transform, true);

            // Hungry Bloom-Plant facing East toward the clearing
            GameObject plantGo = SpawnProp("BloomPlant", new Vector3(-12.5f, 0.43f, 1.5f), Quaternion.Euler(0f, -90f, 0f), Vector3.one, zone1.transform, false);
            HungryBloomPlant plant = plantGo.AddComponent<HungryBloomPlant>();
            harness.hungryPlant = plant;

            // Star Seed #1 (Revealed when Hungry Bloom-Plant is fed!)
            GameObject seed1Go = SpawnProp("StarSeed", new Vector3(-12.5f, 2.55f, 1.5f), Quaternion.identity, Vector3.one, zone1.transform, false);
            StarSeedCollectible seed1 = seed1Go.AddComponent<StarSeedCollectible>();
            seed1.seedIndex = 1;
            seed1.seedTitle = "Bloom Garden Star Seed";
            seed1.startHidden = true;
            plant.rewardSeed = seed1;
            gm.allSeeds.Add(seed1);

            // Plump Starberries nearby ready to pick up, carry, drop & throw
            Vector3[] berryPositions =
            {
                new Vector3(-6.8f, 0.15f, 1.5f),
                new Vector3(-8.6f, 0.15f, -2.5f),
                new Vector3(-5.2f, 0.15f, 4.2f),
            };
            for (int i = 0; i < berryPositions.Length; i++)
            {
                GameObject berryGo = SpawnProp("Starberry", berryPositions[i], Quaternion.Euler(0f, i * 55f, 0f), Vector3.one, zone1.transform, false);
                StarberryItem berry = berryGo.AddComponent<StarberryItem>();
                if (i == 0)
                    harness.primaryBerry = berry;
            }
        }

        private static void BuildZone2MushroomAndBoulderSlope(Transform parent, PocketPlanetGameManager gm, PlaytestVerificationHarness harness)
        {
            GameObject zone2 = new GameObject("Zone2_MushroomAndBoulderSlope");
            zone2.transform.SetParent(parent, false);

            // Springy Jelly-Shrooms leading up to the East Boulder Ridge
            Vector3[] shroomPositions =
            {
                new Vector3(7.2f, 0.0f, -2.5f),
                new Vector3(10.5f, 1.15f, 0.8f),
                new Vector3(-3.8f, 0.0f, 7.2f),
            };
            for (int i = 0; i < shroomPositions.Length; i++)
            {
                if (i == 1)
                {
                    SpawnProp("PastelRockPlatform", new Vector3(10.5f, 0f, 0.8f), Quaternion.identity, new Vector3(0.95f, 0.67f, 0.95f), zone2.transform, true);
                }
                GameObject shroomGo = SpawnProp("BouncyShroom", shroomPositions[i], Quaternion.Euler(0f, i * 40f, 0f), Vector3.one, zone2.transform, false);
                BouncyMushroom bm = shroomGo.AddComponent<BouncyMushroom>();
                if (i == 0)
                    harness.primaryMushroom = bm;
            }

            // Sloped Stone Ramp (runs from South-East high terrace down toward North-East domino gate)
            Material rampMat = CreateSolidMaterial("LavenderSlopeMat", new Color(0.54f, 0.46f, 0.72f), 0.25f);
            GameObject slopeRamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slopeRamp.name = "BoulderSlopeRamp";
            slopeRamp.transform.SetParent(zone2.transform, false);
            slopeRamp.transform.position = new Vector3(12.5f, 1.25f, 6.2f);
            slopeRamp.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
            slopeRamp.transform.localScale = new Vector3(3.4f, 0.36f, 9.8f);
            slopeRamp.GetComponent<Renderer>().sharedMaterial = rampMat;

            // Side guide rails on the ramp so the boulder rolls cleanly into the dominoes
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "SlopeGuideRail";
                rail.transform.SetParent(slopeRamp.transform, false);
                rail.transform.localPosition = new Vector3(side * 0.46f, 0.85f, 0f);
                rail.transform.localScale = new Vector3(0.08f, 1.1f, 1.0f);
                rail.GetComponent<Renderer>().sharedMaterial = rampMat;
            }

            // Top platform behind the boulder where Pip can stand & push (top at Y = 2.32m)
            SpawnProp("PastelRockPlatform", new Vector3(12.5f, 0f, -0.5f), Quaternion.identity, new Vector3(1.25f, 1.35f, 1.25f), zone2.transform, true);

            // PushBall Boulder perched at the top of the slope (facing +Z down the ramp)
            GameObject boulderGo = SpawnProp("PushBall", new Vector3(12.5f, 2.95f, 2.2f), Quaternion.identity, Vector3.one, zone2.transform, false);
            PushableBoulder boulder = boulderGo.AddComponent<PushableBoulder>();
            harness.slopeBoulder = boulder;

            // Row of 4 DominoBlocks at the bottom of the slope
            for (int i = 0; i < 4; i++)
            {
                Vector3 dPos = new Vector3(12.5f, 0.02f, 11.5f + i * 1.08f);
                GameObject domGo = SpawnProp("DominoBlock", dPos, Quaternion.identity, Vector3.one, zone2.transform, false);
                DominoBlock dom = domGo.AddComponent<DominoBlock>();
                boulder.linkedDominoes.Add(dom);
            }

            // Star Seed #2 perched right beyond the dominoes
            GameObject seed2Go = SpawnProp("StarSeed", new Vector3(12.5f, 0.65f, 16.5f), Quaternion.identity, Vector3.one, zone2.transform, false);
            StarSeedCollectible seed2 = seed2Go.AddComponent<StarSeedCollectible>();
            seed2.seedIndex = 2;
            seed2.seedTitle = "Boulder Domino Star Seed";
            seed2.startHidden = false;
            boulder.rewardSeed = seed2;
            gm.allSeeds.Add(seed2);
        }

        private static void BuildZone3BridgeAndPodVillage(Transform parent, PocketPlanetGameManager gm, PlaytestVerificationHarness harness, PipCharacterController pip)
        {
            GameObject zone3 = new GameObject("Zone3_BridgeAndAlienVillage");
            zone3.transform.SetParent(parent, false);

            // North Island Plateau (Alien Pod Village across the gap at Z = +24..+36)
            Material islandMat = CreateSolidMaterial("VillageGrassMat", new Color(0.18f, 0.54f, 0.46f), 0.22f);
            GameObject northIsland = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            northIsland.name = "NorthAlienVillageIsland";
            northIsland.transform.SetParent(zone3.transform, false);
            northIsland.transform.position = new Vector3(0f, -0.45f, 29.5f);
            northIsland.transform.localScale = new Vector3(18f, 0.50f, 18f);
            northIsland.GetComponent<Renderer>().sharedMaterial = islandMat;
            ReplaceCylinderColliderWithMeshCollider(northIsland);

            // Unfolding Petal Bridge spanning from Z = +17.5 to Z = +23.5 (note: bridge local -Z is forward in Blender export, so rotate 180 Y to span +Z)
            GameObject bridgeGo = SpawnProp("UnfoldingBridge", new Vector3(0f, 0.02f, 17.2f), Quaternion.Euler(0f, 180f, 0f), Vector3.one, zone3.transform, false);

            // Crystal Lever on the south edge of the gap (Z = +16.2)
            GameObject leverGo = SpawnProp("CrystalLever", new Vector3(-2.2f, 0.0f, 16.2f), Quaternion.identity, Vector3.one, zone3.transform, false);
            CrystalLeverBridge lever = leverGo.AddComponent<CrystalLeverBridge>();
            lever.bridgeRoot = bridgeGo.transform;
            harness.crystalLever = lever;

            // Cute Alien Pod Homes on the North Village Island
            SpawnProp("AlienPodHome", new Vector3(-4.2f, 0.0f, 31.5f), Quaternion.Euler(0f, 35f, 0f), Vector3.one, zone3.transform, true);
            SpawnProp("AlienPodHome", new Vector3(4.4f, 0.0f, 32.0f), Quaternion.Euler(0f, -30f, 0f), Vector3.one * 0.92f, zone3.transform, true);

            // Friendly Alien Bloop waiting in the village plaza
            GameObject bloopGo = SpawnProp("BloopCreature", new Vector3(0f, 0.05f, 27.5f), Quaternion.Euler(0f, 180f, 0f), Vector3.one, zone3.transform, false);
            FriendlyAlienBloop bloop = bloopGo.AddComponent<FriendlyAlienBloop>();
            bloop.followTarget = pip;
            harness.friendlyBloop = bloop;

            // Star Seed #3 (Revealed when Pip pets Friendly Alien Bloop!)
            GameObject seed3Go = SpawnProp("StarSeed", new Vector3(0f, 1.25f, 29.2f), Quaternion.identity, Vector3.one, zone3.transform, false);
            StarSeedCollectible seed3 = seed3Go.AddComponent<StarSeedCollectible>();
            seed3.seedIndex = 3;
            seed3.seedTitle = "Friendly Bloop Star Seed";
            seed3.startHidden = true;
            bloop.rewardSeed = seed3;
            gm.allSeeds.Add(seed3);
        }

        private static void ScatterResponsiveFlora(Transform parent, PlaytestVerificationHarness harness)
        {
            GameObject floraGroup = new GameObject("ResponsiveFloraGroup");
            floraGroup.transform.SetParent(parent, false);

            Vector3[] positions =
            {
                new Vector3(-2.5f, 0f, -3.2f),
                new Vector3(2.8f, 0f, -2.8f),
                new Vector3(-4.5f, 0f, -4.8f),
                new Vector3(-7.8f, 0f, -4.2f),
                new Vector3(-5.0f, 0f, -8.2f),
                new Vector3(4.5f, 0f, 2.2f),
                new Vector3(-6.2f, 0f, 2.8f),
                new Vector3(1.8f, 0f, 9.5f),
                new Vector3(-1.8f, 0f, 12.2f),
                new Vector3(-2.5f, 0f, 26.2f),
                new Vector3(2.5f, 0f, 26.8f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject tuft = SpawnProp("GlowingFloraTuft", positions[i], Quaternion.Euler(0f, i * 47f, 0f), Vector3.one * UnityEngine.Random.Range(0.9f, 1.25f), floraGroup.transform, false);
                ResponsiveFlora rf = tuft.AddComponent<ResponsiveFlora>();
                if (i == 0)
                    harness.sampleFlora = rf;
            }
        }

        private static void BuildDistantSpacePlanetsAndStars(Transform parent)
        {
            GameObject skyGroup = new GameObject("SpaceSkyboxPlanets");
            skyGroup.transform.SetParent(parent, false);

            Material coralPlanetMat = CreateSolidMaterial("SkyPlanetCoralMat", new Color(0.96f, 0.56f, 0.68f), 0.35f, new Color(0.38f, 0.18f, 0.26f));
            Material ringMat = CreateSolidMaterial("SkyPlanetRingMat", new Color(0.98f, 0.88f, 0.56f), 0.55f, new Color(0.48f, 0.38f, 0.18f));
            Material mintMoonMat = CreateSolidMaterial("SkyPlanetMintMat", new Color(0.54f, 0.90f, 0.86f), 0.35f, new Color(0.20f, 0.42f, 0.38f));
            Material lavenderPlanetMat = CreateSolidMaterial("SkyPlanetLavenderMat", new Color(0.76f, 0.62f, 0.96f), 0.35f, new Color(0.28f, 0.20f, 0.44f));

            // 1. North-West Horizon Ringed Planet (Coral-Pink gas giant, visible in North & Central views)
            GameObject planet1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet1.name = "DistantRingedPlanet_Coral";
            UnityEngine.Object.DestroyImmediate(planet1.GetComponent<Collider>());
            planet1.transform.SetParent(skyGroup.transform, false);
            planet1.transform.position = new Vector3(-28f, 11.5f, 76f);
            planet1.transform.localScale = Vector3.one * 15f;
            planet1.GetComponent<Renderer>().sharedMaterial = coralPlanetMat;

            GameObject ring1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring1.name = "PlanetRing";
            UnityEngine.Object.DestroyImmediate(ring1.GetComponent<Collider>());
            ring1.transform.SetParent(planet1.transform, false);
            ring1.transform.localRotation = Quaternion.Euler(22f, 0f, -18f);
            ring1.transform.localScale = new Vector3(2.2f, 0.016f, 2.2f);
            ring1.GetComponent<Renderer>().sharedMaterial = ringMat;

            // 2. North-East Horizon Moon (Mint-Turquoise moon, visible in Central, Mushroom & Boulder views)
            GameObject planet2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet2.name = "DistantMoon_Mint";
            UnityEngine.Object.DestroyImmediate(planet2.GetComponent<Collider>());
            planet2.transform.SetParent(skyGroup.transform, false);
            planet2.transform.position = new Vector3(32f, 12.0f, 72f);
            planet2.transform.localScale = Vector3.one * 9.5f;
            planet2.GetComponent<Renderer>().sharedMaterial = mintMoonMat;

            // 3. West Horizon Ringed Planet (Lavender gas giant, visible behind Bloom-Plant Garden)
            GameObject planet3 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet3.name = "DistantRingedPlanet_Lavender";
            UnityEngine.Object.DestroyImmediate(planet3.GetComponent<Collider>());
            planet3.transform.SetParent(skyGroup.transform, false);
            planet3.transform.position = new Vector3(-68f, 9.5f, 8f);
            planet3.transform.localScale = Vector3.one * 12.5f;
            planet3.GetComponent<Renderer>().sharedMaterial = lavenderPlanetMat;

            GameObject ring3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring3.name = "PlanetRing";
            UnityEngine.Object.DestroyImmediate(ring3.GetComponent<Collider>());
            ring3.transform.SetParent(planet3.transform, false);
            ring3.transform.localRotation = Quaternion.Euler(18f, 15f, 20f);
            ring3.transform.localScale = new Vector3(2.1f, 0.016f, 2.1f);
            ring3.GetComponent<Renderer>().sharedMaterial = ringMat;

            // 4. South Horizon Moon (visible in Pond & South views)
            GameObject planet4 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet4.name = "DistantMoon_South";
            UnityEngine.Object.DestroyImmediate(planet4.GetComponent<Collider>());
            planet4.transform.SetParent(skyGroup.transform, false);
            planet4.transform.position = new Vector3(-22f, 10.5f, -72f);
            planet4.transform.localScale = Vector3.one * 10.5f;
            planet4.GetComponent<Renderer>().sharedMaterial = coralPlanetMat;

            // Twinkling starlight orbs across the horizon and upper sky dome
            UnityEngine.Random.InitState(42);
            for (int i = 0; i < 84; i++)
            {
                float angle = (i / 84f) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.05f, 0.05f);
                float dist = UnityEngine.Random.Range(82f, 118f);
                float height = (i < 56) ? UnityEngine.Random.Range(2.5f, 22f) : UnityEngine.Random.Range(22f, 58f);
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = $"SkyStar_{i}";
                UnityEngine.Object.DestroyImmediate(star.GetComponent<Collider>());
                star.transform.SetParent(skyGroup.transform, false);
                star.transform.position = new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);
                float s = UnityEngine.Random.Range(0.42f, 1.05f);
                star.transform.localScale = Vector3.one * s;
                star.GetComponent<Renderer>().sharedMaterial = ringMat;
            }
        }

        private static void CreateSteppingStone(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stone.name = "PathSteppingStone";
            stone.transform.SetParent(parent, false);
            stone.transform.position = pos;
            stone.transform.localScale = scale;
            stone.GetComponent<Renderer>().sharedMaterial = mat;
            ReplaceCylinderColliderWithMeshCollider(stone);
        }

        private static void ReplaceCylinderColliderWithMeshCollider(GameObject cylinderGo)
        {
            Collider oldCol = cylinderGo.GetComponent<Collider>();
            if (oldCol != null)
                UnityEngine.Object.DestroyImmediate(oldCol);
            MeshFilter mf = cylinderGo.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = cylinderGo.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        private static GameObject SpawnProp(string propName, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent, bool addMeshColliders)
        {
            GameObject prefab = Resources.Load<GameObject>($"Props/{propName}");
            GameObject inst;
            if (prefab != null)
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                inst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            }
            inst.name = propName;
            inst.transform.SetParent(parent, false);
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.transform.localScale = scale;

            if (addMeshColliders)
            {
                foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                }
            }
            return inst;
        }

        private static Material CreateSolidMaterial(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = $"Assets/PocketPlanet/Art/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = s;
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
