using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class PocketPlanetGameManager : MonoBehaviour
    {
        public static PocketPlanetGameManager Instance { get; private set; }

        public PipCharacterController player;
        public ThirdPersonCamera thirdPersonCamera;
        public StarhopperSpaceship spaceship;
        public List<StarSeedCollectible> allSeeds = new List<StarSeedCollectible>();

        public int CollectedSeeds { get; private set; }
        public bool ShipRepaired { get; private set; }
        public bool ShowControlsHelp { get; set; } = true;
        public string BannerMessage { get; private set; } = "Explore Pocket Planet & collect 3 Star Seeds to repair the SS Starhopper!";
        private float _bannerTimer = 6.0f;

        private readonly HashSet<string> _completedInteractions = new HashSet<string>();
        public IReadOnlyCollection<string> CompletedInteractions => _completedInteractions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ForceWindowedModeOnStartup()
        {
            if (!Application.isBatchMode)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.fullScreen = false;
                Screen.SetResolution(1440, 900, FullScreenMode.Windowed);
            }
        }

        private void Awake()
        {
            Instance = this;
            ForceWindowedModeOnStartup();
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
                _bannerTimer -= Time.deltaTime;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool toggleHelp = Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.Tab)
                || (kb != null && (kb.hKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame));
            bool keepExploringKey = Input.GetKeyDown(KeyCode.C) || (kb != null && kb.cKey.wasPressedThisFrame);
            bool restartKey = Input.GetKeyDown(KeyCode.P) || (kb != null && kb.pKey.wasPressedThisFrame);

            if (toggleHelp)
                ShowControlsHelp = !ShowControlsHelp;
            if (ShipRepaired && keepExploringKey)
                KeepExploring();
            if (ShipRepaired && restartKey)
                RestartAdventure();
        }

        public void RecordInteraction(string label)
        {
            if (_completedInteractions.Add(label))
            {
                ShowBanner($"✨ {label}!");
            }
        }

        public void OnStarSeedCollected(StarSeedCollectible seed)
        {
            CollectedSeeds = Mathf.Clamp(CollectedSeeds + 1, 0, 3);
            if (CollectedSeeds < 3)
                ShowBanner($"⭐ Collected {seed.seedTitle}! ({CollectedSeeds}/3 Star Seeds)");
            else
                ShowBanner("🌟 All 3 Star Seeds Collected! Return to the SS Starhopper in the central clearing!");
        }

        public void OnShipRepaired()
        {
            ShipRepaired = true;
            RecordInteraction("Repaired the SS Starhopper");
            ShowBanner("🚀 SS STARHOPPER REPAIRED! Pocket Planet Celebration! 🎉");
        }

        public void ShowBanner(string msg, float duration = 4.5f)
        {
            BannerMessage = msg;
            _bannerTimer = duration;
        }

        public void KeepExploring()
        {
            if (player != null)
                player.SetCelebrating(false);
            ShowBanner("Continuing Free Exploration across Pocket Planet!");
        }

        public void RestartAdventure()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            // Clean, playful rounded HUD cards
            GUIStyle pillStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 8, 8)
            };
            pillStyle.normal.textColor = new Color(1f, 0.97f, 0.86f);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };
            subStyle.normal.textColor = new Color(0.88f, 0.96f, 0.94f);

            // Top-Left: Star Seed Counter & Current Objective
            GUI.color = new Color(0.10f, 0.14f, 0.24f, 0.86f);
            GUI.Box(new Rect(18, 18, 390, 86), GUIContent.none);
            GUI.color = Color.white;

            string starIcons = "";
            for (int i = 0; i < 3; i++)
                starIcons += (i < CollectedSeeds) ? "★ " : "☆ ";

            GUI.Label(new Rect(32, 24, 360, 30), $"POCKET PLANET   |   STAR SEEDS: {starIcons} ({CollectedSeeds}/3)", pillStyle);
            string objText = ShipRepaired
                ? "Objective Complete! SS Starhopper is repaired and purring!"
                : (CollectedSeeds >= 3
                    ? "Return to the SS Starhopper in the central clearing and press [E]!"
                    : "Explore the garden planet: Feed the Bloom-Plant, Roll the Slope Boulder, and Befriend Bloop!");
            GUI.Label(new Rect(34, 56, 362, 42), objText, subStyle);

            // Top-Right: Compact Controls Card ([H] to toggle)
            if (ShowControlsHelp)
            {
                GUI.color = new Color(0.10f, 0.14f, 0.24f, 0.82f);
                GUI.Box(new Rect(Screen.width - 310, 18, 292, 118), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width - 296, 24, 270, 106),
                    "CONTROLS ([H] Toggle)\n" +
                    "• Move: WASD / Left Stick\n" +
                    "• Run: Shift / Right Trigger\n" +
                    "• Jump: Space / South Button (A/Cross)\n" +
                    "• Interact / Pick Up / Feed / Pet: E / West\n" +
                    "• Throw Fruit: Q or Left Click / East", subStyle);
            }

            // Bottom Interaction Prompt
            if (player != null)
            {
                string prompt = "";
                if (player.FocusedInteractable != null)
                    prompt = player.FocusedInteractable.GetPromptText(player);
                else if (player.CarriedFruit != null)
                    prompt = "Carrying Starberry — Press [E] near Hungry Plant to Feed, or [Q]/[Click] to Throw";

                if (!string.IsNullOrEmpty(prompt))
                {
                    GUI.color = new Color(0.12f, 0.20f, 0.30f, 0.90f);
                    Rect pRect = new Rect(Screen.width * 0.5f - 260f, Screen.height - 96f, 520f, 40f);
                    GUI.Box(pRect, GUIContent.none);
                    GUI.color = new Color(1f, 0.94f, 0.65f);
                    GUIStyle centerBold = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    centerBold.normal.textColor = new Color(1f, 0.95f, 0.70f);
                    GUI.Label(pRect, prompt, centerBold);
                    GUI.color = Color.white;
                }
            }

            // Banner toast notification
            if (_bannerTimer > 0f && !string.IsNullOrEmpty(BannerMessage))
            {
                GUI.color = new Color(0.16f, 0.12f, 0.28f, 0.88f);
                Rect bRect = new Rect(Screen.width * 0.5f - 290f, 22f, 580f, 38f);
                GUI.Box(bRect, GUIContent.none);
                GUIStyle bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                bannerStyle.normal.textColor = new Color(0.65f, 1.0f, 0.88f);
                GUI.color = Color.white;
                GUI.Label(bRect, BannerMessage, bannerStyle);
            }

            // Celebration Modal Panel when SS Starhopper is repaired!
            if (ShipRepaired)
            {
                GUI.color = new Color(0.08f, 0.12f, 0.22f, 0.92f);
                Rect cRect = new Rect(Screen.width * 0.5f - 230f, Screen.height - 205f, 460f, 95f);
                GUI.Box(cRect, GUIContent.none);
                GUI.color = Color.white;

                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = new Color(1f, 0.92f, 0.48f);
                GUI.Label(new Rect(cRect.x, cRect.y + 8f, cRect.width, 28f), "🎉 SS STARHOPPER REPAIRED! 🎉", titleStyle);

                if (GUI.Button(new Rect(cRect.x + 35f, cRect.y + 46f, 180f, 34f), "Keep Exploring [C]"))
                {
                    KeepExploring();
                }
                if (GUI.Button(new Rect(cRect.x + 245f, cRect.y + 46f, 180f, 34f), "Restart Adventure [P]"))
                {
                    RestartAdventure();
                }
            }
        }
    }
}
