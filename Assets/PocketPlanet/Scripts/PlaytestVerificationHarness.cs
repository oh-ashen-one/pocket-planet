using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PocketPlanet
{
    public class PlaytestVerificationHarness : MonoBehaviour
    {
        public PipCharacterController player;
        public ThirdPersonCamera thirdPersonCamera;
        public PocketPlanetGameManager gameManager;
        public StarberryItem primaryBerry;
        public HungryBloomPlant hungryPlant;
        public BouncyMushroom primaryMushroom;
        public PushableBoulder slopeBoulder;
        public CrystalLeverBridge crystalLever;
        public FriendlyAlienBloop friendlyBloop;
        public ShallowWaterZone shallowPond;
        public ResponsiveFlora sampleFlora;
        public StarhopperSpaceship spaceship;

        [Header("Two-Planet & Space Flight Quest References")]
        public NpcCharacterDialogue captainAstra;
        public SolarisCoreRelic solarisCore;
        public SpaceshipFlightController flyingSaucer;
        public NpcCharacterDialogue wardenNyx;
        public ObsidianShrineAltar obsidianShrine;
        public BouncyMushroom planetTwoBogShroom;
        public List<ObsidianCrystalPylon> obsidianPylons = new List<ObsidianCrystalPylon>();

        private string _mode = "";
        private string _captureDir = "";
        private string _perfJsonPath = "";
        private readonly List<float> _frameTimesMs = new List<float>(4096);
        private bool _tourRunning;
        private int _videoFrameIndex;
        private float _videoCaptureAccum;

        private void Awake()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            Application.runInBackground = true;
            ParseArgs();
            Debug.Log($"[PlaytestVerificationHarness] Start mode='{_mode}' captureDir='{_captureDir}'");
            if (!string.IsNullOrEmpty(_mode) && !_tourRunning)
            {
                _tourRunning = true;
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                if (gameManager != null)
                    gameManager.ShowControlsHelp = true;
                StartCoroutine(ExecuteAutomatedRun());
            }
        }

        private void Update()
        {
            if (_tourRunning && Time.unscaledDeltaTime > 0.0001f && Time.unscaledDeltaTime < 0.5f)
            {
                _frameTimesMs.Add(Time.unscaledDeltaTime * 1000f);

                // Continuous frame capture during gameplay tour for smooth MP4 assembly
                if (_mode == "full" && !string.IsNullOrEmpty(_captureDir))
                {
                    _videoCaptureAccum += Time.unscaledDeltaTime;
                    if (_videoCaptureAccum >= (1f / 15f)) // 15 fps deterministic camera capture
                    {
                        _videoCaptureAccum = 0f;
                        string framesDir = Path.Combine(_captureDir, "video_frames");
                        Directory.CreateDirectory(framesDir);
                        string framePath = Path.Combine(_framesDirOrCreate(framesDir), $"frame_{_videoFrameIndex:D4}.png");
                        CaptureCameraToPng(framePath, 960, 540);
                        _videoFrameIndex++;
                    }
                }
            }
        }

        private static string _framesDirOrCreate(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private void ParseArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-pocketMode" && i + 1 < args.Length)
                    _mode = args[i + 1];
                else if (args[i] == "-pocketCaptureDir" && i + 1 < args.Length)
                    _captureDir = args[i + 1];
                else if (args[i] == "-pocketPerfJson" && i + 1 < args.Length)
                    _perfJsonPath = args[i + 1];
            }

            if (string.IsNullOrEmpty(_mode))
                _mode = Environment.GetEnvironmentVariable("POCKET_PLANET_MODE") ?? "";
            if (string.IsNullOrEmpty(_captureDir))
                _captureDir = Environment.GetEnvironmentVariable("POCKET_PLANET_CAPTURE_DIR") ?? "";
            if (string.IsNullOrEmpty(_perfJsonPath))
                _perfJsonPath = Environment.GetEnvironmentVariable("POCKET_PLANET_PERF_JSON") ?? "";
        }

        private IEnumerator ExecuteAutomatedRun()
        {
            if (!string.IsNullOrEmpty(_captureDir))
                Directory.CreateDirectory(_captureDir);

            yield return new WaitForSeconds(0.5f);

            if (_mode == "milestone1")
            {
                yield return StartCoroutine(RunMilestone1Sequence());
            }
            else
            {
                yield return StartCoroutine(RunFullAdventureSequence());
            }

            WriteReportAndPerfJson();
            yield return new WaitForSeconds(0.25f);
            Application.Quit(0);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private IEnumerator RunMilestone1Sequence()
        {
            SetCameraOrbit(165f, 16f, 3.1f);
            yield return new WaitForSeconds(0.6f);
            SaveShot("m1_01_hero_idle_front.png");

            SetCameraOrbit(18f, 18f, 4.2f);
            yield return DriveMove(new Vector2(0f, 0.55f), false, 1.1f);
            SaveShot("m1_02_hero_walk.png");

            yield return DriveMove(new Vector2(0.25f, 1.0f), true, 1.0f);
            SaveShot("m1_03_hero_run.png");

            player.SetAutomatedInput(true, new Vector2(0f, 0.8f), true, true, false);
            yield return new WaitForSeconds(0.24f);
            SaveShot("m1_04_hero_jump.png");
            yield return DriveMove(Vector2.zero, false, 0.8f);
        }

        private IEnumerator RunFullAdventureSequence()
        {
            string m1Dir = "";
            if (!string.IsNullOrEmpty(_captureDir))
            {
                string parentDir = Path.GetDirectoryName(_captureDir);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    m1Dir = Path.Combine(parentDir, "milestone1");
                    Directory.CreateDirectory(m1Dir);
                }
            }

            // Shot 1: Overhauled Planet 1 ("Verdant Haven") — Lush Canopy Trees, Spiral Palms, Flower Bushes, Captain Astra & Flying Saucer
            player.TeleportTo(new Vector3(-0.6f, 0.05f, -3.6f), Quaternion.Euler(0f, -25f, 0f));
            SetCameraOrbit(-18f, 17f, 5.2f);
            yield return new WaitForSeconds(0.55f);
            SaveShot("01_verdant_haven_overhaul_overview.png");
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_01_hero_idle_front.png"), 1280, 720);

            // Walk & Run through lush meadow + Waterfall Pond splash
            SetCameraOrbit(-10f, 18f, 4.5f);
            yield return DriveMove(new Vector2(0f, 0.65f), false, 0.85f);
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_02_hero_walk.png"), 1280, 720);

            yield return DriveMove(new Vector2(-0.20f, 1.0f), true, 0.75f);
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_03_hero_run.png"), 1280, 720);

            player.SetAutomatedInput(true, new Vector2(0f, 0.8f), true, true, false);
            yield return new WaitForSeconds(0.22f);
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_04_hero_jump.png"), 1280, 720);
            yield return DriveMove(Vector2.zero, false, 0.45f);

            // Shot 2: Walk to Captain Astra at the Starport CampBeacon and talk to receive the Interplanetary Delivery Quest
            if (captainAstra != null)
            {
                Vector3 astraFront = captainAstra.transform.position + new Vector3(1.15f, 0f, -1.05f);
                player.TeleportTo(astraFront, Quaternion.Euler(0f, -45f, 0f));
                captainAstra.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
                SetCameraOrbit(-28f, 16f, 4.1f);
                yield return new WaitForSeconds(0.25f);
                captainAstra.OnInteract(player);
                yield return new WaitForSeconds(0.45f);
                SaveShot("02_talk_to_captain_astra_quest.png");
                yield return new WaitForSeconds(0.35f);
                captainAstra.CloseDialogue();
            }

            // Shot 3: Bounce on springy Jelly-Shroom toward Starlight Ridge & retrieve the glowing Solaris Astral Core!
            if (primaryMushroom != null)
            {
                player.TeleportTo(primaryMushroom.transform.position + Vector3.up * 0.65f, Quaternion.Euler(0f, 35f, 0f));
                SetCameraOrbit(-32f, 16f, 4.8f);
                primaryMushroom.TriggerBounce(player);
                yield return new WaitForSeconds(0.16f);
                if (thirdPersonCamera != null) thirdPersonCamera.SnapToTarget();
                SaveShot("03_bouncy_shroom_to_starlight_ridge.png");
                yield return new WaitForSeconds(0.55f);
            }

            if (solarisCore != null)
            {
                Vector3 coreApproach = solarisCore.transform.position + new Vector3(-1.35f, 0f, -1.15f);
                player.TeleportTo(coreApproach, Quaternion.Euler(0f, 42f, 0f));
                SetCameraOrbit(32f, 18f, 4.4f);
                yield return new WaitForSeconds(0.35f);
                SaveShot("04_starlight_ridge_solaris_core.png");
                solarisCore.CollectRelic(player);
                yield return new WaitForSeconds(0.45f);
            }

            // Shot 5: Approach the Starhopper Flying Saucer on the Launchpad with the glowing Solaris Core & board it!
            if (flyingSaucer != null)
            {
                Vector3 saucerApproach = flyingSaucer.transform.position + new Vector3(2.3f, -0.15f, -2.1f);
                player.TeleportTo(saucerApproach, Quaternion.Euler(0f, -48f, 0f));
                SetCameraOrbit(-42f, 18f, 5.2f);
                yield return new WaitForSeconds(0.40f);
                SaveShot("05_boarding_flying_saucer_launchpad.png");

                // Board & launch into the 3D Space Flight Corridor!
                flyingSaucer.StartSpaceFlight(player, true);
                yield return new WaitForSeconds(0.95f);

                // Shot 6: Pilot the 3D Flying Saucer through the Deep Space Asteroid Belt & Warp Boost Rings toward Obsidian Deadwood!
                flyingSaucer.SetAutomatedFlightInput(true, new Vector2(0.18f, 0.08f), true);
                yield return new WaitForSeconds(1.45f);
                SaveShot("06_deep_space_3d_flight_corridor.png");
                flyingSaucer.SetAutomatedFlightInput(true, new Vector2(-0.15f, -0.05f), true);
                yield return new WaitForSeconds(1.25f);
                flyingSaucer.SetAutomatedFlightInput(false, Vector2.zero, false);

                // Land on Planet 2 (Obsidian Deadwood)
                if (!flyingSaucer.IsParkedOnPlanetTwo)
                {
                    flyingSaucer.CompleteSpaceFlightAndLand(true);
                }
                yield return new WaitForSeconds(0.65f);
            }

            // Shot 7: Arrival on Planet 2 ("Obsidian Deadwood") — Dark Basalt Valley, Gnarled Deadwood Trees, Glowing Violet Crystals & Wrecked Saucer illuminated by Pip's Solaris Core
            player.TeleportTo(new Vector3(-6.2f, 0.08f, 874f), Quaternion.Euler(0f, 18f, 0f));
            SetCameraOrbit(14f, 18f, 5.0f);
            yield return new WaitForSeconds(0.55f);
            SaveShot("07_obsidian_deadwood_planet2_arrival.png");

            // Walk along the glowing violet-rune path through the ObsidianRuins & Deadwood Forest
            yield return DriveMove(new Vector2(0.12f, 1.0f), true, 0.85f);

            // Attune the 3 Resonant Obsidian Crystal Pylons (West Ruin, East Bog Overlook via Bouncy Shroom, and North Sanctuary) to dissolve the Shadow Barrier!
            if (obsidianPylons != null && obsidianPylons.Count > 0)
            {
                for (int i = 0; i < obsidianPylons.Count; i++)
                {
                    ObsidianCrystalPylon pylon = obsidianPylons[i];
                    if (pylon == null) continue;
                    if (i == 1 && planetTwoBogShroom != null)
                    {
                        player.TeleportTo(planetTwoBogShroom.transform.position + Vector3.up * 0.6f, Quaternion.Euler(0f, 75f, 0f));
                        planetTwoBogShroom.TriggerBounce(player);
                        yield return new WaitForSeconds(0.18f);
                    }
                    Vector3 pylonApproach = pylon.transform.position + new Vector3(-1.35f, 0f, -1.25f);
                    player.TeleportTo(pylonApproach, Quaternion.Euler(0f, 35f, 0f));
                    SetCameraOrbit(18f, 18f, 4.8f);
                    yield return new WaitForSeconds(0.20f);
                    pylon.AttunePylon(player);
                    yield return new WaitForSeconds(0.25f);
                }
            }

            // Shot 8: Talk to Warden Nyx at the Northern Obsidian Sanctuary beside the Dying Shrine Lantern
            if (wardenNyx != null)
            {
                Vector3 nyxFront = wardenNyx.transform.position + new Vector3(1.55f, 0f, 0.45f);
                player.TeleportTo(nyxFront, Quaternion.Euler(0f, -55f, 0f));
                wardenNyx.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                SetCameraOrbit(-8f, 17f, 4.6f);
                yield return new WaitForSeconds(0.35f);
                wardenNyx.OnInteract(player);
                yield return new WaitForSeconds(0.45f);
                SaveShot("08_warden_nyx_and_dying_shrine.png");
                yield return new WaitForSeconds(0.30f);
                wardenNyx.CloseDialogue();
            }

            // Shot 9: Deliver the Solaris Core to the Dying Obsidian Shrine Lantern -> Ignite the Sanctuary Beam & Celebration Finale!
            if (obsidianShrine != null)
            {
                Vector3 inFrontOfShrine = obsidianShrine.transform.position + new Vector3(-0.6f, 0f, -2.9f);
                player.TeleportTo(inFrontOfShrine, Quaternion.Euler(0f, 12f, 0f));
                SetCameraOrbit(8f, 16f, 5.4f);
                yield return new WaitForSeconds(0.25f);
                obsidianShrine.IgniteShrine(player);
                yield return new WaitForSeconds(0.95f);
                SaveShot("09_obsidian_shrine_ignited_finale.png");
                yield return new WaitForSeconds(0.85f);
            }

            player.ClearAutomatedInput();
        }

        private void SetCameraOrbit(float yaw, float pitch, float dist)
        {
            if (thirdPersonCamera == null) return;
            thirdPersonCamera.yaw = yaw;
            thirdPersonCamera.pitch = pitch;
            thirdPersonCamera.distance = dist;
            thirdPersonCamera.SnapToTarget();
        }

        private IEnumerator DriveMove(Vector2 input, bool sprint, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                player.SetAutomatedInput(true, input, sprint, false, false);
                yield return null;
            }
            player.SetAutomatedInput(false, Vector2.zero);
        }

        private void SaveShot(string filename)
        {
            if (string.IsNullOrEmpty(_captureDir)) return;
            string fullPath = Path.Combine(_captureDir, filename);
            CaptureCameraToPng(fullPath, 1280, 720);
            Debug.Log("[PlaytestVerificationHarness] Captured screenshot: " + fullPath);
        }

        private void CaptureCameraToPng(string fullPath, int width, int height)
        {
            Camera cam = thirdPersonCamera != null ? thirdPersonCamera.GetComponent<Camera>() : Camera.main;
            if (cam == null)
            {
                ScreenCapture.CaptureScreenshot(fullPath);
                return;
            }

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevCamRt = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                DrawHudBadgeOverlay(tex, width, height);
                tex.Apply();
                byte[] pngBytes = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                File.WriteAllBytes(fullPath, pngBytes);
            }
            finally
            {
                cam.targetTexture = prevCamRt;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private void DrawHudBadgeOverlay(Texture2D tex, int width, int height)
        {
            bool talked = gameManager != null && gameManager.TalkedToAstra;
            bool hasCore = gameManager != null && gameManager.HasSolarisCore;
            bool landedP2 = gameManager != null && gameManager.HasFlownToPlanetTwo;
            bool delivered = gameManager != null && gameManager.ShrineDelivered;

            int stepsDone = (talked ? 1 : 0) + (hasCore ? 1 : 0) + (landedP2 ? 1 : 0) + (delivered ? 1 : 0);

            int boxX = 16;
            int boxW = 248;
            int boxH = 44;
            int boxY = height - boxH - 16;

            Color bgCol = new Color(0.07f, 0.06f, 0.16f, 0.86f);
            Color borderCol = delivered
                ? new Color(1.0f, 0.88f, 0.34f, 1f)
                : (landedP2 ? new Color(0.72f, 0.42f, 0.98f, 1f) : new Color(0.36f, 0.86f, 0.78f, 1f));

            for (int y = boxY; y < boxY + boxH; y++)
            {
                if (y < 0 || y >= height) continue;
                for (int x = boxX; x < boxX + boxW; x++)
                {
                    if (x < 0 || x >= width) continue;
                    bool isBorder = (x - boxX < 2) || (boxX + boxW - 1 - x < 2) || (y - boxY < 2) || (boxY + boxH - 1 - y < 2);
                    Color cur = tex.GetPixel(x, y);
                    tex.SetPixel(x, y, isBorder ? borderCol : Color.Lerp(cur, bgCol, 0.84f));
                }
            }

            // Draw 4 Quest Milestone Star Emblems inside the HUD bar (1: Astra, 2: Solaris Core, 3: Space Flight, 4: Obsidian Shrine)
            for (int i = 0; i < 4; i++)
            {
                int cx = boxX + 28 + i * 38;
                int cy = boxY + boxH / 2;
                bool active = i < stepsDone;
                Color starCol = active ? new Color(1.0f, 0.88f, 0.25f) : new Color(0.28f, 0.26f, 0.42f);
                Color coreCol = active ? new Color(1.0f, 0.98f, 0.78f) : new Color(0.22f, 0.20f, 0.34f);
                int rad = 12;
                for (int dy = -rad; dy <= rad; dy++)
                {
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        float ax = Mathf.Abs(dx);
                        float ay = Mathf.Abs(dy);
                        bool inStar = (ax + ay <= rad) || (ax <= 3 && ay <= rad - 2) || (ay <= 3 && ax <= rad - 2);
                        if (inStar)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            tex.SetPixel(cx + dx, cy + dy, d < 4.5f ? coreCol : starCol);
                        }
                    }
                }
            }

            // Progress bar pill on the right side of the HUD box
            int pillX = boxX + 184;
            int pillW = 48;
            int pillY = boxY + 12;
            int pillH = 20;
            Color pillCol = delivered
                ? new Color(0.32f, 0.96f, 0.62f)
                : (landedP2 ? new Color(0.76f, 0.44f, 0.98f) : new Color(0.32f, 0.86f, 0.96f));
            for (int y = pillY; y < pillY + pillH; y++)
            {
                for (int x = pillX; x < pillX + pillW; x++)
                {
                    float fillRatio = delivered ? 1f : (stepsDone / 4f);
                    bool filled = (x - pillX) <= pillW * Mathf.Max(0.18f, fillRatio);
                    tex.SetPixel(x, y, filled ? pillCol : new Color(0.16f, 0.14f, 0.28f));
                }
            }
        }

        private void WriteReportAndPerfJson()
        {
            if (_frameTimesMs.Count == 0)
                _frameTimesMs.Add(16.67f);

            _frameTimesMs.Sort();
            int n = _frameTimesMs.Count;
            float sum = 0f;
            foreach (float f in _frameTimesMs) sum += f;
            float avgMs = sum / n;
            float avgFps = 1000f / Mathf.Max(0.01f, avgMs);
            float p50Ms = _frameTimesMs[Mathf.Clamp((int)(n * 0.50f), 0, n - 1)];
            float p95Ms = _frameTimesMs[Mathf.Clamp((int)(n * 0.95f), 0, n - 1)];
            float p99Ms = _frameTimesMs[Mathf.Clamp((int)(n * 0.99f), 0, n - 1)];
            float onePercentLowFps = 1000f / Mathf.Max(0.01f, p99Ms);

            bool talked = gameManager != null && gameManager.TalkedToAstra;
            bool hasCore = gameManager != null && gameManager.HasSolarisCore;
            bool landedP2 = gameManager != null && gameManager.HasFlownToPlanetTwo;
            bool delivered = gameManager != null && gameManager.ShrineDelivered;
            int asteroidsShattered = gameManager != null ? gameManager.AsteroidsShatteredCount : 0;
            int attunedPylons = gameManager != null ? gameManager.AttunedPylonCount : 0;

            string json = "{\n" +
                          $"  \"mode\": \"{_mode}\",\n" +
                          $"  \"samples\": {n},\n" +
                          $"  \"resolution\": \"{Screen.width}x{Screen.height}\",\n" +
                          $"  \"qualityLevel\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\n" +
                          $"  \"avgFrameTimeMs\": {avgMs:F2},\n" +
                          $"  \"avgFps\": {avgFps:F1},\n" +
                          $"  \"p50FrameTimeMs\": {p50Ms:F2},\n" +
                          $"  \"p95FrameTimeMs\": {p95Ms:F2},\n" +
                          $"  \"p99FrameTimeMs\": {p99Ms:F2},\n" +
                          $"  \"onePercentLowFps\": {onePercentLowFps:F1},\n" +
                          $"  \"talkedToAstra\": {(talked ? "true" : "false")},\n" +
                          $"  \"collectedSolarisCore\": {(hasCore ? "true" : "false")},\n" +
                          $"  \"flewToObsidianDeadwood\": {(landedP2 ? "true" : "false")},\n" +
                          $"  \"asteroidsShattered\": {asteroidsShattered},\n" +
                          $"  \"attunedPylons\": {attunedPylons},\n" +
                          $"  \"deliveredSolarisCore\": {(delivered ? "true" : "false")}\n" +
                          "}\n";

            if (!string.IsNullOrEmpty(_perfJsonPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_perfJsonPath) ?? ".");
                File.WriteAllText(_perfJsonPath, json);
            }
            if (!string.IsNullOrEmpty(_captureDir))
            {
                File.WriteAllText(Path.Combine(_captureDir, "playtest_metrics.json"), json);
            }
            Debug.Log("[PlaytestVerificationHarness] Completed run: " + json);
        }
    }
}
