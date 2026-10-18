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

            // 1. Configure Toybox Space Atmosphere, Lighting & Dynamic Planetary Atmosphere Controller
            ConfigureEnvironmentLighting(out Light sunLight, out Light fillLight);
            GameObject worldRoot = new GameObject("PocketPlanetWorld");

            // 2. Spawn Audio, GameManager, PlanetaryAtmosphereController & Verification Harness
            GameObject systemsGo = new GameObject("GameSystems");
            systemsGo.AddComponent<PocketPlanetAudio>();
            PocketPlanetGameManager gm = systemsGo.AddComponent<PocketPlanetGameManager>();
            PlanetaryAtmosphereController atmo = systemsGo.AddComponent<PlanetaryAtmosphereController>();
            atmo.mainDirectionalLight = sunLight;
            atmo.secondaryFillLight = fillLight;

            PlaytestVerificationHarness harness = systemsGo.AddComponent<PlaytestVerificationHarness>();
            harness.gameManager = gm;

            // 3. Spawn Hero Pip & Third-Person Camera
            PipCharacterController pip = SpawnHeroPip(new Vector3(0f, 0.05f, -4.5f), out ThirdPersonCamera tpCam);
            gm.player = pip;
            gm.thirdPersonCamera = tpCam;
            atmo.mainCamera = tpCam.GetComponent<Camera>();
            harness.player = pip;
            harness.thirdPersonCamera = tpCam;

            // 4. Build Planet 1: "Verdant Haven" (Lush Sculpted Terrain, Waterfall Pond, Forest, Saucers, Captain Astra, Solaris Core & Launchpad)
            ShallowWaterZone shallowPond = BuildPlanet1VerdantHaven(worldRoot.transform, gm, harness, pip);
            harness.shallowPond = shallowPond;
            BuildDistantSpacePlanetsAndStars(worldRoot.transform);

            // 5. Classic Playful Interactions on Planet 1 (Starberry + Bloom-Plant, Bouncy Shrooms + Boulder Slope, Lever Bridge + Friendly Bloop)
            GameObject shipGo = SpawnProp("StarhopperShip", new Vector3(5.2f, 0.0f, 4.2f), Quaternion.Euler(0f, -24f, -4.5f), Vector3.one * 0.92f, worldRoot.transform, true);
            StarhopperSpaceship ship = shipGo.AddComponent<StarhopperSpaceship>();
            gm.spaceship = ship;
            harness.spaceship = ship;

            BuildZone1BloomGarden(worldRoot.transform, gm, harness);
            BuildZone2MushroomAndBoulderSlope(worldRoot.transform, gm, harness);
            BuildZone3BridgeAndPodVillage(worldRoot.transform, gm, harness, pip);
            ScatterResponsiveFlora(worldRoot.transform, harness);

            // 6. Build Deep Space 3D Flight Corridor (Z = 180 .. 620, Y = 85)
            BuildDeepSpaceFlightCorridor(worldRoot.transform);

            // 7. Build Planet 2: "Obsidian Deadwood" (Z = 840 .. 960, Dark Gnarled Forest,Glowing Crystals, Ruined Archways, Warden Nyx & Obsidian Shrine)
            BuildPlanet2ObsidianDeadwood(worldRoot.transform, gm, harness);

            Directory.CreateDirectory("Assets/PocketPlanet/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PocketPlanetSceneBuilder] Saved complete Two-Planet + Space Flight scene at " + ScenePath);
        }

        public static void BuildMacStandalonePlayer()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.macRetinaSupport = true;
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

        private static void ConfigureEnvironmentLighting(out Light sun, out Light fill)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.56f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.44f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.34f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.18f, 0.24f, 0.42f);
            RenderSettings.fogStartDistance = 52f;
            RenderSettings.fogEndDistance = 165f;

            // Main warm golden-peach sun light
            GameObject sunGo = new GameObject("DirectionalSunLight");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.94f, 0.84f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sunGo.transform.rotation = Quaternion.Euler(44f, -38f, 0f);

            // Subtle cool-lavender fill light for toybox shadow tint
            GameObject fillGo = new GameObject("SkyFillLight");
            fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.72f, 0.96f);
            fill.intensity = 0.26f;
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
            cam.backgroundColor = new Color(0.11f, 0.14f, 0.28f);
            cam.fieldOfView = 54f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 380f;
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

        private static ShallowWaterZone BuildPlanet1VerdantHaven(
            Transform parent,
            PocketPlanetGameManager gm,
            PlaytestVerificationHarness harness,
            PipCharacterController pip)
        {
            GameObject p1Root = new GameObject("Planet1_VerdantHaven");
            p1Root.transform.SetParent(parent, false);

            // 1. Sculpted Multi-Hill Unity Terrain (180m x 180m) with rolling outer mountain peaks & valley basin
            TerrainData td = new TerrainData
            {
                heightmapResolution = 129,
                size = new Vector3(180f, 22f, 180f)
            };

            float[,] heights = new float[129, 129];
            for (int y = 0; y < 129; y++)
            {
                for (int x = 0; x < 129; x++)
                {
                    float nx = (x / 128f) * 2f - 1f;
                    float ny = (y / 128f) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);

                    // Base playable valley height = 0.08 (which places terrain surface at y ~ 0.04m)
                    float h = 0.078f;
                    if (r > 0.36f)
                    {
                        float rim = Mathf.Pow((r - 0.36f) / 0.64f, 1.65f);
                        float ridgeWaves = 0.5f + 0.5f * Mathf.Sin(angle * 5f + r * 4f) * Mathf.Cos(angle * 3f - r * 2f);
                        h += rim * (0.28f + 0.24f * ridgeWaves);
                    }
                    heights[y, x] = Mathf.Clamp01(h);
                }
            }
            td.SetHeights(0, 0, heights);

            string terrainAssetPath = "Assets/PocketPlanet/Art/Materials/PocketTerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(terrainAssetPath) != null)
                AssetDatabase.DeleteAsset(terrainAssetPath);
            AssetDatabase.CreateAsset(td, terrainAssetPath);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "VerdantHavenTerrain";
            terrainGo.transform.SetParent(p1Root.transform, false);
            terrainGo.transform.position = new Vector3(-90f, -1.72f, -90f);

            Terrain terrainComp = terrainGo.GetComponent<Terrain>();
            Material grassMat = CreateSolidMaterial("TurquoiseGrassMat", new Color(0.14f, 0.44f, 0.37f), 0.14f);
            terrainComp.materialTemplate = grassMat;

            // Main lush meadow plateau cylinder in center so footing is 100% crisp and colourful
            GameObject mainMeadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mainMeadow.name = "CentralGardenPlateau";
            mainMeadow.transform.SetParent(p1Root.transform, false);
            mainMeadow.transform.position = new Vector3(0f, -0.50f, 0f);
            mainMeadow.transform.localScale = new Vector3(46f, 0.50f, 46f);
            mainMeadow.GetComponent<Renderer>().sharedMaterial = grassMat;
            ReplaceCylinderColliderWithMeshCollider(mainMeadow);

            // Second layered emerald terrace ring for visual depth
            Material emeraldHillMat = CreateSolidMaterial("EmeraldHillMat", new Color(0.18f, 0.50f, 0.40f), 0.16f);
            Material lavenderCliffMat = CreateSolidMaterial("LavenderCliffMat", new Color(0.54f, 0.44f, 0.72f), 0.22f);
            Material pathMat = CreateSolidMaterial("PastelPathMat", new Color(0.76f, 0.58f, 0.44f), 0.18f);

            // Warm coral-sand cobblestone pathways connecting Captain Astra, Launchpad, Starlight Ridge, and the 3 zones
            for (int i = 0; i < 9; i++)
            {
                // Path West toward Launchpad & Bloom Garden
                CreateSteppingStone(p1Root.transform, new Vector3(-1.8f - i * 1.35f, 0.02f, -1.2f + i * 0.55f), new Vector3(1.5f, 0.08f, 1.5f), pathMat);
                // Path East toward Starlight Ridge & Mushroom Trail
                CreateSteppingStone(p1Root.transform, new Vector3(1.8f + i * 1.55f, 0.02f, -1.0f + i * 1.15f), new Vector3(1.5f, 0.08f, 1.5f), pathMat);
                // Path North toward Bridge & Alien Village
                CreateSteppingStone(p1Root.transform, new Vector3(0f, 0.02f, 6.2f + i * 1.45f), new Vector3(1.6f, 0.08f, 1.4f), pathMat);
            }

            // 2. Sparkling Shallow Crystal Pond + Glowing Cyan Waterfall Cliff (South-West of clearing)
            GameObject pondRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pondRim.name = "ShallowPondRim";
            UnityEngine.Object.DestroyImmediate(pondRim.GetComponent<Collider>());
            pondRim.transform.SetParent(p1Root.transform, false);
            pondRim.transform.position = new Vector3(-6.2f, 0.01f, -6.5f);
            pondRim.transform.localScale = new Vector3(7.4f, 0.04f, 7.4f);
            pondRim.GetComponent<Renderer>().sharedMaterial = pathMat;

            GameObject waterDisk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waterDisk.name = "SparklingShallowPond";
            waterDisk.transform.SetParent(p1Root.transform, false);
            waterDisk.transform.position = new Vector3(-6.2f, 0.03f, -6.5f);
            waterDisk.transform.localScale = new Vector3(6.8f, 0.05f, 6.8f);
            Material waterMat = CreateSolidMaterial("SparklingPondMat", new Color(0.22f, 0.76f, 0.90f), 0.88f, new Color(0.08f, 0.30f, 0.42f));
            waterDisk.GetComponent<Renderer>().sharedMaterial = waterMat;
            UnityEngine.Object.DestroyImmediate(waterDisk.GetComponent<Collider>());
            BoxCollider wCol = waterDisk.AddComponent<BoxCollider>();
            wCol.size = new Vector3(1.0f, 8.0f, 1.0f);
            wCol.isTrigger = true;
            ShallowWaterZone swz = waterDisk.AddComponent<ShallowWaterZone>();

            // Cascading Waterfall Cliff behind the pond
            SpawnProp("PastelRockPlatform", new Vector3(-9.2f, 0f, -9.4f), Quaternion.Euler(0f, 35f, 0f), new Vector3(1.45f, 2.2f, 1.45f), p1Root.transform, true);
            GameObject waterfall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterfall.name = "GlowingWaterfallStream";
            UnityEngine.Object.DestroyImmediate(waterfall.GetComponent<Collider>());
            waterfall.transform.SetParent(p1Root.transform, false);
            waterfall.transform.position = new Vector3(-7.9f, 1.85f, -8.1f);
            waterfall.transform.rotation = Quaternion.Euler(-16f, 40f, 0f);
            waterfall.transform.localScale = new Vector3(1.35f, 3.8f, 0.18f);
            waterfall.GetComponent<Renderer>().sharedMaterial = waterMat;

            // Glowing bioluminescent lily pads & lotus buds floating on the shallow pond surface
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
                pad.transform.SetParent(p1Root.transform, false);
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

            // 3. Captain Astra's Starport Outpost & Campfire Beacon (Right near Pip's spawn!)
            GameObject astraCamp = new GameObject("CaptainAstraOutpost");
            astraCamp.transform.SetParent(p1Root.transform, false);

            GameObject astraGo = SpawnProp(
                "TripoNpcAstra",
                new Vector3(-3.2f, 0.02f, 0.8f),
                Quaternion.Euler(0f, -25f, 0f),
                Vector3.one,
                astraCamp.transform,
                false);
            NpcCharacterDialogue astraNpc = astraGo.AddComponent<NpcCharacterDialogue>();
            astraNpc.npcName = "Captain Astra";
            astraNpc.npcRole = "Starport Pathfinder";
            astraNpc.isPlanetOneQuestGiver = true;
            astraNpc.isPlanetTwoRecipient = false;
            gm.captainAstra = astraNpc;
            harness.captainAstra = astraNpc;

            // Cozy glowing Campfire Beacon next to Captain Astra
            Material goldGlowMat = CreateSolidMaterial("GoldBeaconMat", new Color(1.0f, 0.84f, 0.32f), 0.65f, new Color(0.85f, 0.52f, 0.14f));
            GameObject campBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            campBase.name = "StarportCampBeacon";
            campBase.transform.SetParent(astraCamp.transform, false);
            campBase.transform.position = new Vector3(-4.6f, 0.18f, 1.4f);
            campBase.transform.localScale = new Vector3(1.1f, 0.18f, 1.1f);
            campBase.GetComponent<Renderer>().sharedMaterial = lavenderCliffMat;
            ReplaceCylinderColliderWithMeshCollider(campBase);

            GameObject campFlame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            campFlame.name = "CampBeaconCore";
            UnityEngine.Object.DestroyImmediate(campFlame.GetComponent<Collider>());
            campFlame.transform.SetParent(campBase.transform, false);
            campFlame.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            campFlame.transform.localScale = new Vector3(0.42f, 2.1f, 0.42f);
            campFlame.GetComponent<Renderer>().sharedMaterial = goldGlowMat;

            Light campLight = campFlame.AddComponent<Light>();
            campLight.type = LightType.Point;
            campLight.color = new Color(1.0f, 0.82f, 0.42f);
            campLight.intensity = 1.25f;
            campLight.range = 8.5f;

            // 4. Starlight Ridge (North-East elevated terrace) & The Solaris Astral Core!
            GameObject ridgeGroup = new GameObject("StarlightRidgeAndSolarisCore");
            ridgeGroup.transform.SetParent(p1Root.transform, false);

            // Stepping terraces leading up to Starlight Ridge so player can either bounce on Jelly-Shrooms OR climb steps
            SpawnProp("PastelRockPlatform", new Vector3(5.8f, 0f, 7.2f), Quaternion.Euler(0f, 15f, 0f), new Vector3(1.15f, 0.45f, 1.15f), ridgeGroup.transform, true);
            SpawnProp("PastelRockPlatform", new Vector3(7.6f, 0f, 9.8f), Quaternion.Euler(0f, 40f, 0f), new Vector3(1.65f, 0.85f, 1.65f), ridgeGroup.transform, true);

            // Solaris Core Relic perched atop Starlight Ridge (top of platform is y ~ 1.48m)
            GameObject coreGo = SpawnProp(
                "TripoSolarisCore",
                new Vector3(7.6f, 1.52f, 9.8f),
                Quaternion.Euler(0f, -35f, 0f),
                Vector3.one * 0.92f,
                ridgeGroup.transform,
                false);
            SolarisCoreRelic coreRelic = coreGo.AddComponent<SolarisCoreRelic>();
            gm.solarisCore = coreRelic;
            harness.solarisCore = coreRelic;

            // 5. Starport Launchpad & Playable 3D Flying Saucer (West / North-West of Captain Astra)
            GameObject launchpadGroup = new GameObject("StarportLaunchpadGroup");
            launchpadGroup.transform.SetParent(p1Root.transform, false);

            Material padStoneMat = CreateSolidMaterial("LaunchpadStoneMat", new Color(0.44f, 0.48f, 0.64f), 0.38f);
            Material padCyanMat = CreateSolidMaterial("LaunchpadCyanRingMat", new Color(0.28f, 0.92f, 1.0f), 0.82f, new Color(0.18f, 0.78f, 0.96f));

            Vector3 padCenter = new Vector3(-7.8f, 0.08f, 4.8f);
            GameObject padDisk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padDisk.name = "SaucerLaunchpadBase";
            padDisk.transform.SetParent(launchpadGroup.transform, false);
            padDisk.transform.position = padCenter;
            padDisk.transform.localScale = new Vector3(5.6f, 0.12f, 5.6f);
            padDisk.GetComponent<Renderer>().sharedMaterial = padStoneMat;
            ReplaceCylinderColliderWithMeshCollider(padDisk);

            GameObject padGlowRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padGlowRing.name = "SaucerLaunchpadGlowRing";
            UnityEngine.Object.DestroyImmediate(padGlowRing.GetComponent<Collider>());
            padGlowRing.transform.SetParent(launchpadGroup.transform, false);
            padGlowRing.transform.position = padCenter + Vector3.up * 0.13f;
            padGlowRing.transform.localScale = new Vector3(4.6f, 0.02f, 4.6f);
            padGlowRing.GetComponent<Renderer>().sharedMaterial = padCyanMat;

            GameObject saucerGo = SpawnProp(
                "TripoFlyingSaucer",
                padCenter + Vector3.up * 0.22f,
                Quaternion.Euler(0f, 135f, 0f),
                Vector3.one,
                launchpadGroup.transform,
                false);
            SpaceshipFlightController flightCtrl = saucerGo.AddComponent<SpaceshipFlightController>();
            flightCtrl.planetOnePadPos = padCenter + Vector3.up * 0.22f;
            flightCtrl.planetTwoPadPos = new Vector3(-11.5f, 0.32f, 868f);
            flightCtrl.spaceCorridorStart = new Vector3(0f, 85f, 215f);
            flightCtrl.spaceCorridorEnd = new Vector3(0f, 85f, 565f);
            gm.saucerFlight = flightCtrl;
            harness.flyingSaucer = flightCtrl;

            // 6. Rich Tripo Lush Canopy Trees, Spiral Alien Palms & Vibrant Flower Bushes framing Verdant Haven
            Vector3[] lushTreePositions =
            {
                new Vector3(-16.5f, 0f, -4.5f),
                new Vector3(-15.0f, 0f, 9.5f),
                new Vector3(-11.5f, 0f, 15.2f),
                new Vector3(11.8f, 0f, 14.8f),
                new Vector3(17.5f, 0f, 8.5f),
                new Vector3(16.8f, 0f, -6.5f),
                new Vector3(9.5f, 0f, -13.5f),
                new Vector3(-3.5f, 0f, -15.5f),
                new Vector3(-14.5f, 0f, -12.5f),
                new Vector3(-24.0f, 0.2f, 2.0f),
                new Vector3(24.0f, 0.2f, 1.5f),
                new Vector3(-7.5f, 0f, 27.5f),
                new Vector3(7.8f, 0f, 28.0f),
                new Vector3(0f, 0f, 36.5f),
            };
            for (int i = 0; i < lushTreePositions.Length; i++)
            {
                float s = 0.85f + (i % 3) * 0.16f;
                SpawnProp("TripoLushTree", lushTreePositions[i], Quaternion.Euler(0f, i * 67f, 0f), Vector3.one * s, p1Root.transform, true);
            }

            Vector3[] palmTreePositions =
            {
                new Vector3(-10.2f, 0f, -3.5f),
                new Vector3(-3.8f, 0f, -9.8f),
                new Vector3(6.2f, 0f, -7.8f),
                new Vector3(14.5f, 0f, -2.2f),
                new Vector3(4.5f, 0f, 13.5f),
                new Vector3(-5.8f, 0f, 12.8f),
                new Vector3(-18.5f, 0f, 4.2f),
                new Vector3(19.2f, 0f, 13.5f),
                new Vector3(-6.2f, 0f, 33.5f),
                new Vector3(6.4f, 0f, 34.0f),
            };
            for (int i = 0; i < palmTreePositions.Length; i++)
            {
                float s = 0.88f + (i % 3) * 0.14f;
                SpawnProp("TripoAlienPalmTree", palmTreePositions[i], Quaternion.Euler(0f, i * 53f + 20f, 0f), Vector3.one * s, p1Root.transform, true);
            }

            Vector3[] flowerBushPositions =
            {
                new Vector3(-2.2f, 0f, 2.2f),
                new Vector3(-5.4f, 0f, 0.2f),
                new Vector3(-9.5f, 0f, 2.8f),
                new Vector3(-5.5f, 0f, 7.2f),
                new Vector3(3.2f, 0f, 2.5f),
                new Vector3(5.2f, 0f, 5.2f),
                new Vector3(8.8f, 0f, 6.8f),
                new Vector3(9.2f, 0f, 12.2f),
                new Vector3(3.8f, 0f, -4.5f),
                new Vector3(-2.2f, 0f, -6.8f),
                new Vector3(-8.8f, 0f, -5.2f),
                new Vector3(-12.2f, 0f, -1.5f),
                new Vector3(10.5f, 0f, -4.8f),
                new Vector3(-3.2f, 0f, 15.2f),
                new Vector3(3.2f, 0f, 15.5f),
                new Vector3(-3.5f, 0f, 29.5f),
                new Vector3(3.5f, 0f, 29.8f),
            };
            for (int i = 0; i < flowerBushPositions.Length; i++)
            {
                float s = 0.78f + (i % 4) * 0.12f;
                SpawnProp("TripoFlowerBush", flowerBushPositions[i], Quaternion.Euler(0f, i * 41f, 0f), Vector3.one * s, p1Root.transform, false);
            }

            // 7. Decorative pastel rock formations, alien pods & mushrooms framing the central clearing
            SpawnProp("PastelRockPlatform", new Vector3(-13.5f, 0f, -9.5f), Quaternion.Euler(0f, 25f, 0f), new Vector3(1.3f, 1.2f, 1.3f), p1Root.transform, true);
            SpawnProp("PastelRockPlatform", new Vector3(13.5f, 0f, -10.5f), Quaternion.Euler(0f, -40f, 0f), new Vector3(1.4f, 1.5f, 1.4f), p1Root.transform, true);
            SpawnProp("AlienPodHome", new Vector3(-10.5f, 0f, -11.2f), Quaternion.Euler(0f, 135f, 0f), Vector3.one * 0.88f, p1Root.transform, true);
            SpawnProp("CustomuseFloraStatue", new Vector3(-9.8f, 0f, 8.5f), Quaternion.Euler(0f, 35f, 0f), Vector3.one * 1.15f, p1Root.transform, true);
            SpawnProp("CustomuseFloraStatue", new Vector3(8.5f, 0f, -8.5f), Quaternion.Euler(0f, -55f, 0f), Vector3.one * 1.05f, p1Root.transform, true);

            // 8. Patrolling Sky Saucers overhead on Planet 1 so the sky feels alive!
            Vector3[] skySaucerCenters =
            {
                new Vector3(-12f, 13.5f, 18f),
                new Vector3(16f, 15.5f, 12f),
                new Vector3(0f, 18.0f, 34f),
                new Vector3(-18f, 14.5f, -14f),
            };
            for (int i = 0; i < skySaucerCenters.Length; i++)
            {
                GameObject skyUfo = SpawnProp(
                    "TripoFlyingSaucer",
                    skySaucerCenters[i],
                    Quaternion.Euler(0f, i * 90f, 0f),
                    Vector3.one * 0.85f,
                    p1Root.transform,
                    false);
                skyUfo.name = $"PatrollingSkySaucer_{i}";
                PatrollingSaucerWalker walker = skyUfo.AddComponent<PatrollingSaucerWalker>();
                walker.orbitCenter = skySaucerCenters[i];
                walker.orbitRadiusX = 7.5f + i * 2.0f;
                walker.orbitRadiusZ = 5.5f + i * 1.5f;
                walker.angularSpeed = 0.55f + i * 0.12f;
                walker.phaseOffset = i * 1.57f;
            }

            return swz;
        }

        private static void BuildDeepSpaceFlightCorridor(Transform parent)
        {
            GameObject spaceRoot = new GameObject("DeepSpaceFlightCorridor");
            spaceRoot.transform.SetParent(parent, false);

            Material verdGlobeMat = CreateSolidMaterial("VerdantPlanetGlobeMat", new Color(0.18f, 0.68f, 0.58f), 0.45f, new Color(0.06f, 0.26f, 0.22f));
            Material verdRingMat = CreateSolidMaterial("VerdantGlobeRingMat", new Color(0.45f, 0.92f, 0.84f), 0.65f, new Color(0.18f, 0.48f, 0.44f));
            Material obsGlobeMat = CreateSolidMaterial("ObsidianPlanetGlobeMat", new Color(0.16f, 0.10f, 0.26f), 0.35f, new Color(0.24f, 0.08f, 0.42f));
            Material obsRingMat = CreateSolidMaterial("ObsidianGlobeRingMat", new Color(0.68f, 0.28f, 0.96f), 0.72f, new Color(0.46f, 0.16f, 0.78f));
            Material warpGateMat = CreateSolidMaterial("SpaceWarpGateMat", new Color(0.28f, 0.94f, 1.0f), 0.88f, new Color(0.24f, 0.86f, 1.0f));
            Material starOrbMat = CreateSolidMaterial("DeepSpaceStarMat", new Color(1.0f, 0.92f, 0.68f), 0.80f, new Color(0.85f, 0.74f, 0.42f));

            // 1. Departing Verdant Haven Planet Globe behind the corridor start
            GameObject startPlanet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            startPlanet.name = "VerdantHavenOrbitalGlobe";
            UnityEngine.Object.DestroyImmediate(startPlanet.GetComponent<Collider>());
            startPlanet.transform.SetParent(spaceRoot.transform, false);
            startPlanet.transform.position = new Vector3(-24f, 74f, 175f);
            startPlanet.transform.localScale = Vector3.one * 42f;
            startPlanet.GetComponent<Renderer>().sharedMaterial = verdGlobeMat;

            GameObject startRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            startRing.name = "VerdantAtmosphereRing";
            UnityEngine.Object.DestroyImmediate(startRing.GetComponent<Collider>());
            startRing.transform.SetParent(startPlanet.transform, false);
            startRing.transform.localRotation = Quaternion.Euler(18f, 12f, -16f);
            startRing.transform.localScale = new Vector3(1.85f, 0.015f, 1.85f);
            startRing.GetComponent<Renderer>().sharedMaterial = verdRingMat;

            // 2. Destination Obsidian Deadwood Planet Globe ahead at end of corridor (Z = 635)
            GameObject destPlanet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            destPlanet.name = "ObsidianDeadwoodOrbitalGlobe";
            UnityEngine.Object.DestroyImmediate(destPlanet.GetComponent<Collider>());
            destPlanet.transform.SetParent(spaceRoot.transform, false);
            destPlanet.transform.position = new Vector3(0f, 84f, 632f);
            destPlanet.transform.localScale = Vector3.one * 56f;
            destPlanet.GetComponent<Renderer>().sharedMaterial = obsGlobeMat;

            GameObject destRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            destRing.name = "ObsidianShroudRing";
            UnityEngine.Object.DestroyImmediate(destRing.GetComponent<Collider>());
            destRing.transform.SetParent(destPlanet.transform, false);
            destRing.transform.localRotation = Quaternion.Euler(-22f, 0f, 18f);
            destRing.transform.localScale = new Vector3(2.05f, 0.018f, 2.05f);
            destRing.GetComponent<Renderer>().sharedMaterial = obsRingMat;

            // 3. Five Glowing Warp Boost Rings along the flight path (Z = 250 .. 525)
            float[] ringZ = { 255f, 320f, 385f, 450f, 515f };
            float[] ringX = { 0f, 4.5f, -4.0f, 3.0f, 0f };
            float[] ringY = { 85f, 86.5f, 84.0f, 86.0f, 85f };
            for (int i = 0; i < ringZ.Length; i++)
            {
                GameObject gateRoot = new GameObject($"SpaceWarpBoostGate_{i}");
                gateRoot.transform.SetParent(spaceRoot.transform, false);
                gateRoot.transform.position = new Vector3(ringX[i], ringY[i], ringZ[i]);

                // Build a glowing octagonal/beacon ring out of 8 segments around the flight path
                for (int seg = 0; seg < 8; seg++)
                {
                    float ang = seg * 45f * Mathf.Deg2Rad;
                    float rad = 5.2f;
                    GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = $"GateArc_{seg}";
                    UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
                    bar.transform.SetParent(gateRoot.transform, false);
                    bar.transform.localPosition = new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, 0f);
                    bar.transform.localRotation = Quaternion.Euler(0f, 0f, seg * 45f + 90f);
                    bar.transform.localScale = new Vector3(4.2f, 0.38f, 0.45f);
                    bar.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? warpGateMat : obsRingMat;
                }

                gateRoot.AddComponent<SpaceWarpBoostRing>();
            }

            // 4. 28 Drifting 3D Space Asteroids (TripoSpaceAsteroid) flanking the flight corridor
            UnityEngine.Random.InitState(108);
            for (int i = 0; i < 28; i++)
            {
                float z = 225f + i * 12.2f;
                float side = (i % 2 == 0) ? 1f : -1f;
                float x = side * UnityEngine.Random.Range(6.5f, 24f) + Mathf.Sin(i * 1.3f) * 3.5f;
                float y = 85f + UnityEngine.Random.Range(-11f, 12f);
                float scale = UnityEngine.Random.Range(0.75f, 1.65f);

                GameObject ast = SpawnProp(
                    "TripoSpaceAsteroid",
                    new Vector3(x, y, z),
                    Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), 0f),
                    Vector3.one * scale,
                    spaceRoot.transform,
                    false);
                ast.name = $"SpaceAsteroid_{i}";
                PatrollingSaucerWalker drifter = ast.AddComponent<PatrollingSaucerWalker>();
                drifter.orbitCenter = ast.transform.position;
                drifter.orbitRadiusX = 1.2f;
                drifter.orbitRadiusZ = 1.2f;
                drifter.angularSpeed = 0.35f;
                drifter.spinSpeed = 28f + (i % 5) * 8f;
                drifter.phaseOffset = i * 0.7f;
            }

            // 5. Escort Flying Saucers patrolling the Deep Space Corridor
            for (int i = 0; i < 3; i++)
            {
                Vector3 escortCenter = new Vector3((i - 1) * 14f, 91f + i * 2f, 310f + i * 85f);
                GameObject escort = SpawnProp(
                    "TripoFlyingSaucer",
                    escortCenter,
                    Quaternion.identity,
                    Vector3.one * 0.95f,
                    spaceRoot.transform,
                    false);
                escort.name = $"CorridorEscortSaucer_{i}";
                PatrollingSaucerWalker w = escort.AddComponent<PatrollingSaucerWalker>();
                w.orbitCenter = escortCenter;
                w.orbitRadiusX = 9f;
                w.orbitRadiusZ = 14f;
                w.angularSpeed = 0.65f;
                w.phaseOffset = i * 2.1f;
            }

            // 6. Starfield Orbs surrounding the Space Flight Corridor
            for (int i = 0; i < 96; i++)
            {
                float z = UnityEngine.Random.Range(190f, 610f);
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float rad = UnityEngine.Random.Range(28f, 68f);
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = $"CorridorStar_{i}";
                UnityEngine.Object.DestroyImmediate(star.GetComponent<Collider>());
                star.transform.SetParent(spaceRoot.transform, false);
                star.transform.position = new Vector3(Mathf.Cos(ang) * rad, 85f + Mathf.Sin(ang) * rad * 0.65f, z);
                star.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.35f, 0.95f);
                star.GetComponent<Renderer>().sharedMaterial = (i % 3 == 0) ? obsRingMat : starOrbMat;
            }
        }

        private static void BuildPlanet2ObsidianDeadwood(
            Transform parent,
            PocketPlanetGameManager gm,
            PlaytestVerificationHarness harness)
        {
            GameObject p2Root = new GameObject("Planet2_ObsidianDeadwood");
            p2Root.transform.SetParent(parent, false);

            Vector3 p2Center = new Vector3(0f, 0f, 900f);

            // 1. Sculpted Dark Basalt & Obsidian Unity Terrain (180m x 180m centered at Z = 900)
            TerrainData td2 = new TerrainData
            {
                heightmapResolution = 129,
                size = new Vector3(180f, 26f, 180f)
            };

            float[,] heights2 = new float[129, 129];
            for (int y = 0; y < 129; y++)
            {
                for (int x = 0; x < 129; x++)
                {
                    float nx = (x / 128f) * 2f - 1f;
                    float ny = (y / 128f) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);

                    float h = 0.066f;
                    if (r > 0.34f)
                    {
                        float rim = Mathf.Pow((r - 0.34f) / 0.66f, 1.5f);
                        float crags = Mathf.Abs(Mathf.Sin(angle * 7f + r * 5f));
                        h += rim * (0.34f + 0.28f * crags);
                    }
                    heights2[y, x] = Mathf.Clamp01(h);
                }
            }
            td2.SetHeights(0, 0, heights2);

            string terrain2AssetPath = "Assets/PocketPlanet/Art/Materials/ObsidianTerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(terrain2AssetPath) != null)
                AssetDatabase.DeleteAsset(terrain2AssetPath);
            AssetDatabase.CreateAsset(td2, terrain2AssetPath);

            GameObject terrain2Go = Terrain.CreateTerrainGameObject(td2);
            terrain2Go.name = "ObsidianDeadwoodTerrain";
            terrain2Go.transform.SetParent(p2Root.transform, false);
            terrain2Go.transform.position = new Vector3(-90f, -1.72f, 810f);

            Material obsidianGroundMat = CreateSolidMaterial("ObsidianBasaltGroundMat", new Color(0.11f, 0.09f, 0.18f), 0.32f);
            Material obsidianSanctuaryMat = CreateSolidMaterial("ObsidianSanctuaryStoneMat", new Color(0.16f, 0.12f, 0.26f), 0.42f);
            Material violetRuneMat = CreateSolidMaterial("ObsidianVioletRuneMat", new Color(0.58f, 0.26f, 0.92f), 0.72f, new Color(0.32f, 0.12f, 0.56f));
            Material torchFlameMat = CreateSolidMaterial("SanctuaryTorchFlameMat", new Color(1.0f, 0.86f, 0.38f), 0.85f, new Color(1.2f, 0.78f, 0.22f));
            terrain2Go.GetComponent<Terrain>().materialTemplate = obsidianGroundMat;

            // Main Obsidian Valley Floor Cylinder (so player movement on Planet 2 is 100% solid)
            GameObject valleyFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            valleyFloor.name = "ObsidianValleyFloor";
            valleyFloor.transform.SetParent(p2Root.transform, false);
            valleyFloor.transform.position = new Vector3(0f, -0.50f, 898f);
            valleyFloor.transform.localScale = new Vector3(54f, 0.50f, 78f);
            valleyFloor.GetComponent<Renderer>().sharedMaterial = obsidianGroundMat;
            ReplaceCylinderColliderWithMeshCollider(valleyFloor);

            // Northern Obsidian Sanctuary Terrace (gentle raised dais at Z = 924, top at Y = 0.42m)
            GameObject sanctuaryDais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sanctuaryDais.name = "ObsidianSanctuaryDais";
            sanctuaryDais.transform.SetParent(p2Root.transform, false);
            sanctuaryDais.transform.position = new Vector3(0f, -0.08f, 924f);
            sanctuaryDais.transform.localScale = new Vector3(22f, 0.50f, 22f);
            sanctuaryDais.GetComponent<Renderer>().sharedMaterial = obsidianSanctuaryMat;
            ReplaceCylinderColliderWithMeshCollider(sanctuaryDais);

            // Wide sloped ramp up onto the Sanctuary Dais
            GameObject daisRamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            daisRamp.name = "SanctuaryApproachRamp";
            daisRamp.transform.SetParent(p2Root.transform, false);
            daisRamp.transform.position = new Vector3(0f, 0.18f, 912.5f);
            daisRamp.transform.rotation = Quaternion.Euler(-6.5f, 0f, 0f);
            daisRamp.transform.localScale = new Vector3(5.2f, 0.18f, 5.5f);
            daisRamp.GetComponent<Renderer>().sharedMaterial = obsidianSanctuaryMat;

            // 2. Planet 2 Landing Pad at (-11.5, 0.08, 868)
            Vector3 p2PadPos = new Vector3(-11.5f, 0.08f, 868f);
            GameObject p2Pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p2Pad.name = "ObsidianLandingPad";
            p2Pad.transform.SetParent(p2Root.transform, false);
            p2Pad.transform.position = p2PadPos;
            p2Pad.transform.localScale = new Vector3(5.8f, 0.12f, 5.8f);
            p2Pad.GetComponent<Renderer>().sharedMaterial = obsidianSanctuaryMat;
            ReplaceCylinderColliderWithMeshCollider(p2Pad);

            GameObject p2PadRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p2PadRing.name = "ObsidianLandingPadRing";
            UnityEngine.Object.DestroyImmediate(p2PadRing.GetComponent<Collider>());
            p2PadRing.transform.SetParent(p2Root.transform, false);
            p2PadRing.transform.position = p2PadPos + Vector3.up * 0.13f;
            p2PadRing.transform.localScale = new Vector3(4.8f, 0.02f, 4.8f);
            p2PadRing.GetComponent<Renderer>().sharedMaterial = violetRuneMat;

            // Glowing violet rune stepping stones leading from the Landing Pad through the Deadwood Forest up to the Sanctuary
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                Vector3 stonePos = Vector3.Lerp(new Vector3(-7.0f, 0.03f, 870f), new Vector3(0f, 0.03f, 910f), t);
                stonePos.x += Mathf.Sin(t * Mathf.PI * 2f) * 1.4f;
                CreateSteppingStone(p2Root.transform, stonePos, new Vector3(1.65f, 0.08f, 1.65f), (i % 3 == 0) ? violetRuneMat : obsidianSanctuaryMat);
            }

            // 3. Crashed Flying Saucer Wreckage Landmark in the Western Bog (-15.5, 0.05, 892)
            SpawnProp(
                "TripoCrashedSaucer",
                new Vector3(-14.5f, 0.05f, 892f),
                Quaternion.Euler(8f, 42f, -14f),
                Vector3.one * 1.05f,
                p2Root.transform,
                true);

            // 4. Ancient Obsidian Ruined Archways / Monoliths flanking the Sacred Path
            Vector3[] ruinPositions =
            {
                new Vector3(-6.2f, 0f, 882f),
                new Vector3(5.8f, 0f, 884f),
                new Vector3(-5.8f, 0f, 898f),
                new Vector3(5.8f, 0f, 900f),
                new Vector3(-6.8f, 0.40f, 918f),
                new Vector3(6.8f, 0.40f, 918f),
                new Vector3(-8.2f, 0.40f, 930f),
                new Vector3(8.2f, 0.40f, 930f),
            };
            for (int i = 0; i < ruinPositions.Length; i++)
            {
                float rotY = (i % 2 == 0) ? 28f : -28f;
                SpawnProp("TripoObsidianRuin", ruinPositions[i], Quaternion.Euler(0f, rotY, 0f), Vector3.one * 0.95f, p2Root.transform, true);
            }

            // 5. Dense Gnarled Deadwood Forest (18 TripoDeadwoodTree instances)
            Vector3[] deadwoodPositions =
            {
                new Vector3(-18.5f, 0f, 876f),
                new Vector3(-16.2f, 0f, 885f),
                new Vector3(-17.8f, 0f, 902f),
                new Vector3(-13.5f, 0f, 912f),
                new Vector3(-15.0f, 0f, 926f),
                new Vector3(-9.5f, 0f, 936f),
                new Vector3(9.5f, 0f, 936f),
                new Vector3(15.2f, 0f, 925f),
                new Vector3(13.8f, 0f, 910f),
                new Vector3(16.8f, 0f, 896f),
                new Vector3(14.5f, 0f, 882f),
                new Vector3(8.5f, 0f, 870f),
                new Vector3(-2.5f, 0f, 862f),
                new Vector3(-10.5f, 0f, 888f),
                new Vector3(10.2f, 0f, 892f),
                new Vector3(0f, 0f, 942f),
            };
            for (int i = 0; i < deadwoodPositions.Length; i++)
            {
                float s = 0.86f + (i % 3) * 0.16f;
                SpawnProp("TripoDeadwoodTree", deadwoodPositions[i], Quaternion.Euler(0f, i * 57f, 0f), Vector3.one * s, p2Root.transform, true);
            }

            // 6. Glowing Violet Obsidian Crystal Clusters & Spooky Bog Mushrooms
            Vector3[] crystalPositions =
            {
                new Vector3(-5.2f, 0f, 874f),
                new Vector3(4.8f, 0f, 878f),
                new Vector3(-8.4f, 0f, 890f),
                new Vector3(7.6f, 0f, 894f),
                new Vector3(-4.6f, 0f, 906f),
                new Vector3(4.6f, 0f, 906f),
                new Vector3(-9.2f, 0.40f, 922f),
                new Vector3(9.2f, 0.40f, 922f),
                new Vector3(-5.5f, 0.40f, 932f),
                new Vector3(5.5f, 0.40f, 932f),
            };
            for (int i = 0; i < crystalPositions.Length; i++)
            {
                float s = 0.78f + (i % 3) * 0.15f;
                GameObject crys = SpawnProp("TripoObsidianCrystal", crystalPositions[i], Quaternion.Euler(0f, i * 63f, 0f), Vector3.one * s, p2Root.transform, true);
                if (i % 2 == 0)
                {
                    GameObject glowNode = new GameObject("CrystalVioletLight");
                    glowNode.transform.SetParent(crys.transform, false);
                    glowNode.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                    Light cl = glowNode.AddComponent<Light>();
                    cl.type = LightType.Point;
                    cl.color = new Color(0.62f, 0.28f, 0.98f);
                    cl.intensity = 1.35f;
                    cl.range = 8.5f;
                }
            }

            Vector3[] spookyShroomPositions =
            {
                new Vector3(-8.2f, 0f, 878f),
                new Vector3(2.8f, 0f, 882f),
                new Vector3(-3.8f, 0f, 892f),
                new Vector3(6.2f, 0f, 888f),
                new Vector3(-7.2f, 0f, 904f),
                new Vector3(6.8f, 0f, 904f),
                new Vector3(-4.2f, 0.40f, 916f),
                new Vector3(4.2f, 0.40f, 916f),
            };
            for (int i = 0; i < spookyShroomPositions.Length; i++)
            {
                float s = 0.72f + (i % 3) * 0.14f;
                SpawnProp("TripoSpookyMushroom", spookyShroomPositions[i], Quaternion.Euler(0f, i * 49f, 0f), Vector3.one * s, p2Root.transform, false);
            }

            // Also include a Bouncy Jelly-Shroom on Planet 2 near the sanctuary approach
            GameObject p2Bounce = SpawnProp("BouncyShroom", new Vector3(-4.5f, 0f, 910f), Quaternion.identity, Vector3.one, p2Root.transform, false);
            p2Bounce.AddComponent<BouncyMushroom>();

            // 7. Warden Nyx & The Dying Obsidian Shrine Lantern at the Northern Sanctuary (Z = 922..926)
            GameObject nyxGo = SpawnProp(
                "TripoNpcNyx",
                new Vector3(-2.6f, 0.42f, 922.5f),
                Quaternion.Euler(0f, -20f, 0f),
                Vector3.one,
                p2Root.transform,
                false);
            NpcCharacterDialogue nyxNpc = nyxGo.AddComponent<NpcCharacterDialogue>();
            nyxNpc.npcName = "Warden Nyx";
            nyxNpc.npcRole = "Keeper of the Obsidian Shrine";
            nyxNpc.isPlanetOneQuestGiver = false;
            nyxNpc.isPlanetTwoRecipient = true;
            gm.wardenNyx = nyxNpc;
            harness.wardenNyx = nyxNpc;

            // Dying Obsidian Shrine Lantern at center of Sanctuary Dais (0, 0.42, 925.5)
            GameObject shrineGo = SpawnProp(
                "TripoShrineLantern",
                new Vector3(0f, 0.42f, 925.5f),
                Quaternion.Euler(0f, 0f, 0f),
                Vector3.one,
                p2Root.transform,
                true);
            ObsidianShrineAltar shrineAltar = shrineGo.AddComponent<ObsidianShrineAltar>();

            // Shrine Light & Ignited Starlight Beam Column
            GameObject shrineLightNode = new GameObject("ShrineAltarPointLight");
            shrineLightNode.transform.SetParent(shrineGo.transform, false);
            shrineLightNode.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            shrineAltar.shrineLight = shrineLightNode.AddComponent<Light>();
            shrineAltar.shrineLight.type = LightType.Point;

            GameObject beamCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamCol.name = "IgnitedStarlightBeam";
            UnityEngine.Object.DestroyImmediate(beamCol.GetComponent<Collider>());
            beamCol.transform.SetParent(shrineGo.transform, false);
            beamCol.transform.localPosition = new Vector3(0f, 14f, 0f);
            beamCol.transform.localScale = new Vector3(0.85f, 14f, 0.85f);
            beamCol.GetComponent<Renderer>().sharedMaterial = torchFlameMat;
            beamCol.SetActive(false);
            shrineAltar.ignitedBeamColumn = beamCol;

            gm.obsidianShrine = shrineAltar;
            harness.obsidianShrine = shrineAltar;

            // Ring of 6 Sanctuary Torch Pillars around the Shrine that ignite when the Solaris Core is delivered!
            var torchObjects = new GameObject[6];
            for (int i = 0; i < 6; i++)
            {
                float ang = (i / 6f) * Mathf.PI * 2f;
                Vector3 torchPos = new Vector3(Mathf.Cos(ang) * 6.2f, 0.42f, 925.5f + Mathf.Sin(ang) * 6.2f);
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"SanctuaryTorchPillar_{i}";
                pillar.transform.SetParent(p2Root.transform, false);
                pillar.transform.position = torchPos + Vector3.up * 0.7f;
                pillar.transform.localScale = new Vector3(0.45f, 0.7f, 0.45f);
                pillar.GetComponent<Renderer>().sharedMaterial = obsidianSanctuaryMat;
                ReplaceCylinderColliderWithMeshCollider(pillar);

                GameObject flameOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flameOrb.name = "TorchStarlightOrb";
                UnityEngine.Object.DestroyImmediate(flameOrb.GetComponent<Collider>());
                flameOrb.transform.SetParent(pillar.transform, false);
                flameOrb.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                flameOrb.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                flameOrb.GetComponent<Renderer>().sharedMaterial = torchFlameMat;

                Light tl = flameOrb.AddComponent<Light>();
                tl.type = LightType.Point;
                tl.color = new Color(1.0f, 0.85f, 0.38f);
                tl.intensity = 1.15f;
                tl.range = 8.5f;
                flameOrb.SetActive(false);
                torchObjects[i] = flameOrb;
            }
            shrineAltar.sanctuaryTorches = torchObjects;

            // Distant Eerie Violet Moon hanging over Obsidian Deadwood's northern horizon
            GameObject darkMoon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            darkMoon.name = "ObsidianEerieVioletMoon";
            UnityEngine.Object.DestroyImmediate(darkMoon.GetComponent<Collider>());
            darkMoon.transform.SetParent(p2Root.transform, false);
            darkMoon.transform.position = new Vector3(-22f, 24f, 995f);
            darkMoon.transform.localScale = Vector3.one * 22f;
            darkMoon.GetComponent<Renderer>().sharedMaterial = violetRuneMat;
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

            // Springy Jelly-Shrooms leading up to Starlight Ridge & East Boulder Ridge
            Vector3[] shroomPositions =
            {
                new Vector3(4.8f, 0.0f, 4.2f),
                new Vector3(7.2f, 0.0f, -2.5f),
                new Vector3(10.5f, 1.15f, 0.8f),
                new Vector3(-3.8f, 0.0f, 7.2f),
            };
            for (int i = 0; i < shroomPositions.Length; i++)
            {
                if (i == 2)
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

            // Unfolding Petal Bridge spanning from Z = +17.5 to Z = +23.5
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
                new Vector3(-1.2f, 0f, 0.5f),
                new Vector3(3.4f, 0f, 5.8f),
                new Vector3(6.2f, 0f, 3.2f),
                new Vector3(-8.5f, 0f, 6.5f),
                new Vector3(6.8f, 0f, -5.2f),
                new Vector3(-1.5f, 0f, -8.5f),
                new Vector3(2.2f, 0f, -6.2f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject tuft = SpawnProp("GlowingFloraTuft", positions[i], Quaternion.Euler(0f, i * 47f, 0f), Vector3.one * UnityEngine.Random.Range(0.95f, 1.35f), floraGroup.transform, false);
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
            planet1.transform.position = new Vector3(-28f, 16.5f, 92f);
            planet1.transform.localScale = Vector3.one * 16f;
            planet1.GetComponent<Renderer>().sharedMaterial = coralPlanetMat;

            GameObject ring1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring1.name = "PlanetRing";
            UnityEngine.Object.DestroyImmediate(ring1.GetComponent<Collider>());
            ring1.transform.SetParent(planet1.transform, false);
            ring1.transform.localRotation = Quaternion.Euler(22f, 0f, -18f);
            ring1.transform.localScale = new Vector3(2.2f, 0.016f, 2.2f);
            ring1.GetComponent<Renderer>().sharedMaterial = ringMat;

            // 2. North-East Horizon Moon (Mint-Turquoise moon, visible in Central, Mushroom & Starlight Ridge views)
            GameObject planet2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet2.name = "DistantMoon_Mint";
            UnityEngine.Object.DestroyImmediate(planet2.GetComponent<Collider>());
            planet2.transform.SetParent(skyGroup.transform, false);
            planet2.transform.position = new Vector3(36f, 15.0f, 86f);
            planet2.transform.localScale = Vector3.one * 10.5f;
            planet2.GetComponent<Renderer>().sharedMaterial = mintMoonMat;

            // 3. West Horizon Ringed Planet (Lavender gas giant, visible behind Launchpad & Bloom-Plant Garden)
            GameObject planet3 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet3.name = "DistantRingedPlanet_Lavender";
            UnityEngine.Object.DestroyImmediate(planet3.GetComponent<Collider>());
            planet3.transform.SetParent(skyGroup.transform, false);
            planet3.transform.position = new Vector3(-82f, 14.5f, 8f);
            planet3.transform.localScale = Vector3.one * 14.5f;
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
            planet4.transform.position = new Vector3(-22f, 14.5f, -86f);
            planet4.transform.localScale = Vector3.one * 11.5f;
            planet4.GetComponent<Renderer>().sharedMaterial = coralPlanetMat;

            // Twinkling starlight orbs across the horizon and upper sky dome
            UnityEngine.Random.InitState(42);
            for (int i = 0; i < 84; i++)
            {
                float angle = (i / 84f) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.05f, 0.05f);
                float dist = UnityEngine.Random.Range(95f, 135f);
                float height = (i < 56) ? UnityEngine.Random.Range(6.5f, 26f) : UnityEngine.Random.Range(26f, 62f);
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = $"SkyStar_{i}";
                UnityEngine.Object.DestroyImmediate(star.GetComponent<Collider>());
                star.transform.SetParent(skyGroup.transform, false);
                star.transform.position = new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);
                float s = UnityEngine.Random.Range(0.45f, 1.15f);
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
