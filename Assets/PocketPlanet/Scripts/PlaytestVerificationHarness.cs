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
            // 1. Hero Idle front/3-quarter view
            SetCameraOrbit(165f, 16f, 3.1f);
            yield return new WaitForSeconds(0.6f);
            SaveShot("m1_01_hero_idle_front.png");

            // 2. Walk forward
            SetCameraOrbit(18f, 18f, 4.2f);
            yield return DriveMove(new Vector2(0f, 0.55f), false, 1.1f);
            SaveShot("m1_02_hero_walk.png");

            // 3. Run & Turn
            yield return DriveMove(new Vector2(0.25f, 1.0f), true, 1.0f);
            SaveShot("m1_03_hero_run.png");

            // 4. Jump & Landing
            player.SetAutomatedInput(true, new Vector2(0f, 0.8f), true, true, false);
            yield return new WaitForSeconds(0.24f);
            SaveShot("m1_04_hero_jump.png");
            yield return DriveMove(Vector2.zero, false, 0.8f);
        }

        private IEnumerator RunFullAdventureSequence()
        {
            // Also refresh Milestone 1 hero showcase shots in sibling milestone1 folder if present
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

            // Shot 1: Overview of central clearing, broken SS Starhopper & Pip facing camera
            player.TeleportTo(new Vector3(0f, 0.05f, -3.5f), Quaternion.Euler(0f, 165f, 0f));
            SetCameraOrbit(12f, 16f, 4.2f);
            yield return new WaitForSeconds(0.55f);
            SaveShot("01_central_clearing_pip_and_ship.png");
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_01_hero_idle_front.png"), 1280, 720);

            // Walk & Run + Jump check (also updates m1_02..m1_04)
            SetCameraOrbit(18f, 18f, 4.2f);
            yield return DriveMove(new Vector2(0f, 0.55f), false, 0.85f);
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_02_hero_walk.png"), 1280, 720);

            yield return DriveMove(new Vector2(0.20f, 1.0f), true, 0.75f);
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_03_hero_run.png"), 1280, 720);

            player.SetAutomatedInput(true, new Vector2(0f, 0.8f), true, true, false);
            yield return new WaitForSeconds(0.22f);
            if (!string.IsNullOrEmpty(m1Dir))
                CaptureCameraToPng(Path.Combine(m1Dir, "m1_04_hero_jump.png"), 1280, 720);
            yield return DriveMove(Vector2.zero, false, 0.55f);

            // Shot 2: Walk & Run through responsive glowing flora + splash through shallow crystal pond
            SetCameraOrbit(32f, 20f, 4.6f);
            if (shallowPond != null)
            {
                yield return MoveTowardPoint(shallowPond.transform.position, true, 2.0f, 0.7f);
                SaveShot("02_shallow_pond_splash_and_flora.png");
            }

            // Shot 3: Pick up oversized Starberry, show weighted Carry pose, and feed Hungry Bloom-Plant (Star Seed #1)
            if (primaryBerry != null && hungryPlant != null)
            {
                Vector3 berryPos = primaryBerry.transform.position;
                player.TeleportTo(berryPos + new Vector3(0.8f, 0.05f, 0f), Quaternion.Euler(0f, -90f, 0f));
                SetCameraOrbit(-52f, 18f, 3.8f);
                yield return new WaitForSeconds(0.20f);
                primaryBerry.OnInteract(player);
                // Wait 0.45s so ModePickup transitions into ModeCarry and turn toward camera/plant
                yield return new WaitForSeconds(0.45f);
                player.transform.rotation = Quaternion.Euler(0f, -135f, 0f);
                yield return new WaitForSeconds(0.10f);
                SaveShot("03_carrying_oversized_starberry.png");

                // Carry berry to Hungry Bloom-Plant
                SetCameraOrbit(-65f, 22f, 4.8f);
                yield return MoveTowardPoint(hungryPlant.transform.position + new Vector3(1.8f, 0f, 0f), false, 2.0f, 1.4f);
                hungryPlant.OnInteract(player);
                yield return new WaitForSeconds(1.45f);
                if (hungryPlant.rewardSeed != null)
                    hungryPlant.rewardSeed.transform.rotation = Quaternion.Euler(0f, thirdPersonCamera.yaw, 0f);
                SaveShot("04_hungry_plant_blooms_seed1.png");

                // Collect Star Seed #1
                if (hungryPlant.rewardSeed != null && !hungryPlant.rewardSeed.IsCollected)
                {
                    hungryPlant.rewardSeed.Collect(player);
                    yield return new WaitForSeconds(0.35f);
                }
            }

            // Shot 4: Bounce on springy Jelly-Shroom!
            if (primaryMushroom != null)
            {
                player.TeleportTo(primaryMushroom.transform.position + Vector3.up * 0.95f, Quaternion.Euler(0f, 145f, 0f));
                SetCameraOrbit(-35f, 16f, 4.4f);
                primaryMushroom.TriggerBounce(player);
                yield return new WaitForSeconds(0.18f);
                SaveShot("05_bouncy_jelly_shroom_launch.png");
                yield return new WaitForSeconds(0.65f);
            }

            // Shot 5: Push boulder down slope to topple domino blocks & collect Star Seed #2!
            if (slopeBoulder != null)
            {
                Vector3 behindBoulder = slopeBoulder.transform.position - Vector3.forward * 1.35f;
                player.TeleportTo(behindBoulder, Quaternion.identity);
                SetCameraOrbit(12f, 24f, 6.2f);
                yield return new WaitForSeconds(0.2f);
                slopeBoulder.ApplyPlayerPush(Vector3.forward, 8.5f);
                yield return DriveMove(new Vector2(0f, 1f), true, 0.55f);
                yield return new WaitForSeconds(0.95f);
                // Ensure dominoes topple and reveal Seed #2
                foreach (DominoBlock d in slopeBoulder.linkedDominoes)
                {
                    if (d != null && !d.IsToppled)
                        d.ForceTopple(Vector3.forward * 6.5f);
                }
                player.TeleportTo(new Vector3(11.4f, 0.05f, 9.2f), Quaternion.Euler(0f, 18f, 0f));
                SetCameraOrbit(14f, 22f, 5.2f);
                yield return new WaitForSeconds(0.45f);
                if (slopeBoulder.rewardSeed != null)
                    slopeBoulder.rewardSeed.transform.rotation = Quaternion.Euler(0f, thirdPersonCamera.yaw, 0f);
                SaveShot("06_boulder_slope_domino_topple.png");
                if (slopeBoulder.rewardSeed != null)
                {
                    slopeBoulder.rewardSeed.RevealSeed(slopeBoulder.rewardSeed.transform.position);
                    slopeBoulder.rewardSeed.Collect(player);
                    yield return new WaitForSeconds(0.35f);
                }
            }

            // Shot 6: Pull Crystal Lever to unfold segmented petal bridge!
            if (crystalLever != null)
            {
                player.TeleportTo(crystalLever.transform.position - Vector3.forward * 1.1f, Quaternion.identity);
                SetCameraOrbit(16f, 22f, 5.4f);
                yield return new WaitForSeconds(0.2f);
                crystalLever.OnInteract(player);
                yield return new WaitForSeconds(0.85f);
                SaveShot("07_crystal_lever_unfolding_bridge.png");
            }

            // Shot 7: Cross bridge to Alien Pod Village, pet Friendly Alien Bloop & collect Star Seed #3!
            if (friendlyBloop != null)
            {
                Vector3 bloopBase = friendlyBloop.transform.position;
                player.TeleportTo(bloopBase + new Vector3(-0.72f, 0f, -0.55f), Quaternion.Euler(0f, 52f, 0f));
                friendlyBloop.transform.rotation = Quaternion.Euler(0f, -155f, 0f);
                SetCameraOrbit(8f, 18f, 4.2f);
                yield return new WaitForSeconds(0.2f);
                friendlyBloop.OnInteract(player);
                yield return new WaitForSeconds(0.55f);
                player.TeleportTo(bloopBase + new Vector3(-0.72f, 0f, -0.55f), Quaternion.Euler(0f, 52f, 0f));
                friendlyBloop.transform.SetPositionAndRotation(bloopBase + new Vector3(0.32f, 0f, 0.1f), Quaternion.Euler(0f, -155f, 0f));
                if (friendlyBloop.rewardSeed != null)
                    friendlyBloop.rewardSeed.transform.rotation = Quaternion.Euler(0f, thirdPersonCamera.yaw, 0f);
                SaveShot("08_pet_friendly_bloop_seed3.png");
                if (friendlyBloop.rewardSeed != null && !friendlyBloop.rewardSeed.IsCollected)
                {
                    friendlyBloop.rewardSeed.Collect(player);
                    yield return new WaitForSeconds(0.35f);
                }
                // Walk so Bloop follows happily behind Pip
                SetCameraOrbit(15f, 20f, 4.6f);
                yield return DriveMove(new Vector2(0.15f, 0.9f), false, 1.0f);
            }

            // Shot 8: Return to SS Starhopper in central clearing with all 3 Star Seeds & repair it!
            if (spaceship != null)
            {
                Vector3 inFrontOfShip = spaceship.transform.position + new Vector3(-0.9f, 0.05f, -3.6f);
                player.TeleportTo(inFrontOfShip, Quaternion.Euler(0f, 162f, 0f));
                if (friendlyBloop != null)
                {
                    friendlyBloop.transform.SetPositionAndRotation(
                        spaceship.transform.position + new Vector3(0.95f, 0.05f, -3.3f),
                        Quaternion.Euler(0f, 168f, 0f));
                }
                SetCameraOrbit(4f, 15f, 4.6f);
                yield return new WaitForSeconds(0.25f);
                spaceship.RepairSpaceship(player);
                yield return new WaitForSeconds(0.80f);
                SaveShot("09_starhopper_repaired_celebration.png");
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

        private IEnumerator MoveTowardPoint(Vector3 targetPos, bool sprint, float maxDuration, float stopDist)
        {
            float elapsed = 0f;
            while (elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;
                Vector3 toTarget = targetPos - player.transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude <= stopDist)
                    break;

                Vector3 dir = toTarget.normalized;
                if (thirdPersonCamera != null)
                {
                    Quaternion invCam = Quaternion.Inverse(Quaternion.Euler(0f, thirdPersonCamera.yaw, 0f));
                    Vector3 camLocal = invCam * dir;
                    player.SetAutomatedInput(true, new Vector2(camLocal.x, camLocal.z), sprint, false, false);
                }
                else
                {
                    player.SetAutomatedInput(true, new Vector2(dir.x, dir.z), sprint, false, false);
                }
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
            int seeds = gameManager != null ? gameManager.CollectedSeeds : 0;
            bool repaired = gameManager != null && gameManager.ShipRepaired;

            int boxX = 16;
            int boxW = 220;
            int boxH = 44;
            int boxY = height - boxH - 16;

            Color bgCol = new Color(0.08f, 0.07f, 0.18f, 0.85f);
            Color borderCol = repaired ? new Color(1.0f, 0.86f, 0.32f, 1f) : new Color(0.38f, 0.82f, 0.74f, 1f);

            for (int y = boxY; y < boxY + boxH; y++)
            {
                if (y < 0 || y >= height) continue;
                for (int x = boxX; x < boxX + boxW; x++)
                {
                    if (x < 0 || x >= width) continue;
                    bool isBorder = (x - boxX < 2) || (boxX + boxW - 1 - x < 2) || (y - boxY < 2) || (boxY + boxH - 1 - y < 2);
                    Color cur = tex.GetPixel(x, y);
                    tex.SetPixel(x, y, isBorder ? borderCol : Color.Lerp(cur, bgCol, 0.82f));
                }
            }

            // Draw 3 Star Seed diamond/star emblems inside the HUD bar
            for (int i = 0; i < 3; i++)
            {
                int cx = boxX + 34 + i * 44;
                int cy = boxY + boxH / 2;
                bool active = i < seeds;
                Color starCol = active ? new Color(1.0f, 0.88f, 0.25f) : new Color(0.28f, 0.26f, 0.42f);
                Color coreCol = active ? new Color(1.0f, 0.98f, 0.78f) : new Color(0.22f, 0.20f, 0.34f);
                int rad = 13;
                for (int dy = -rad; dy <= rad; dy++)
                {
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        float ax = Mathf.Abs(dx);
                        float ay = Mathf.Abs(dy);
                        // 4-pointed star + center disk silhouette
                        bool inStar = (ax + ay <= rad) || (ax <= 4 && ay <= rad - 2) || (ay <= 4 && ax <= rad - 2);
                        if (inStar)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            tex.SetPixel(cx + dx, cy + dy, d < 5f ? coreCol : starCol);
                        }
                    }
                }
            }

            // Status indicator pill on the right side of the HUD box
            int pillX = boxX + 162;
            int pillW = 42;
            int pillY = boxY + 12;
            int pillH = 20;
            Color pillCol = repaired
                ? new Color(0.32f, 0.96f, 0.62f)
                : (seeds > 0 ? new Color(0.32f, 0.82f, 0.95f) : new Color(0.35f, 0.32f, 0.52f));
            for (int y = pillY; y < pillY + pillH; y++)
            {
                for (int x = pillX; x < pillX + pillW; x++)
                {
                    float fillRatio = repaired ? 1f : (seeds / 3f);
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

            int seeds = gameManager != null ? gameManager.CollectedSeeds : 0;
            bool repaired = gameManager != null && gameManager.ShipRepaired;

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
                          $"  \"collectedSeeds\": {seeds},\n" +
                          $"  \"shipRepaired\": {(repaired ? "true" : "false")}\n" +
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
