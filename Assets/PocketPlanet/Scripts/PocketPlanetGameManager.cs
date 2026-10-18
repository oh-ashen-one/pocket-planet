using System;
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
        public SpaceshipFlightController saucerFlight;
        public NpcCharacterDialogue captainAstra;
        public NpcCharacterDialogue wardenNyx;
        public SolarisCoreRelic solarisCore;
        public ObsidianShrineAltar obsidianShrine;
        public List<StarSeedCollectible> allSeeds = new List<StarSeedCollectible>();

        // Main Delivery Quest State
        public bool TalkedToAstra { get; private set; }
        public bool HasSolarisCore { get; private set; }
        public bool HasFlownToPlanetTwo { get; private set; }
        public bool TalkedToNyx { get; private set; }
        public bool ShrineDelivered { get; private set; }

        public int CollectedSeeds { get; private set; }
        public bool ShipRepaired { get; private set; }
        public bool ShowControlsHelp { get; set; } = true;
        public string BannerMessage { get; private set; } = "Talk to Captain Astra by the Starport Campfire to begin your interplanetary mission!";
        private float _bannerTimer = 6.5f;

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
            if ((ShipRepaired || ShrineDelivered) && keepExploringKey)
                KeepExploring();
            if ((ShipRepaired || ShrineDelivered) && restartKey)
                RestartAdventure();
        }

        public void RecordInteraction(string label)
        {
            if (_completedInteractions.Add(label))
            {
                ShowBanner($"✨ {label}!");
            }
        }

        public void OnTalkedToCaptainAstra()
        {
            if (!TalkedToAstra)
            {
                TalkedToAstra = true;
                CollectedSeeds = Mathf.Max(CollectedSeeds, 1);
                RecordInteraction("Briefed by Captain Astra");
                ShowBanner("🧭 Quest Updated: Climb Starlight Ridge (NE) to retrieve the golden Solaris Astral Core!");
            }
        }

        public void OnSolarisCoreCollected()
        {
            TalkedToAstra = true;
            HasSolarisCore = true;
            CollectedSeeds = Mathf.Max(CollectedSeeds, 2);
            RecordInteraction("Retrieved the Solaris Astral Core");
            ShowBanner("🌟 SOLARIS CORE ACQUIRED! Board the Starhopper Flying Saucer on the launchpad & press [E]!");
        }

        public void OnEnteredSpaceFlight(bool towardPlanetTwo)
        {
            RecordInteraction("Launched Flying Saucer into Deep Space");
            if (towardPlanetTwo)
                ShowBanner("🛸 SPACE FLIGHT: Pilot through the Crystal Asteroid Belt toward Obsidian Deadwood! ([Shift] Boost)");
            else
                ShowBanner("🛸 SPACE FLIGHT: Returning through the Asteroid Belt toward Verdant Haven!");
        }

        public void OnLandedOnPlanet(bool onPlanetTwo)
        {
            if (onPlanetTwo)
            {
                HasFlownToPlanetTwo = true;
                CollectedSeeds = Mathf.Max(CollectedSeeds, 3);
                RecordInteraction("Landed on Obsidian Deadwood Planet");
                ShowBanner("🌑 Touchdown on OBSIDIAN DEADWOOD! Follow the glowing violet crystals to Warden Nyx & the Dying Shrine!");
            }
            else
            {
                ShowBanner("🌿 Touchdown on VERDANT HAVEN!");
            }
        }

        public void OnTalkedToWardenNyx()
        {
            if (!TalkedToNyx)
            {
                TalkedToNyx = true;
                RecordInteraction("Spoke with Warden Nyx in Obsidian Deadwood");
            }
        }

        public void OnObsidianShrineIgnited()
        {
            ShrineDelivered = true;
            ShipRepaired = true;
            CollectedSeeds = 3;
            RecordInteraction("Ignited the Obsidian Shrine Lantern");
            ShowBanner("🎉 QUEST COMPLETE! The Solaris Core has banished the gloom from Obsidian Deadwood! 🎉", 7.5f);
        }

        public void OnStarSeedCollected(StarSeedCollectible seed)
        {
            CollectedSeeds = Mathf.Clamp(CollectedSeeds + 1, 0, 3);
            if (CollectedSeeds < 3)
                ShowBanner($"⭐ Collected {seed.seedTitle}! ({CollectedSeeds}/3 Star Seeds)");
            else
                ShowBanner("🌟 All 3 Star Seeds Collected!");
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
            ShowBanner("Continuing Free Exploration — Fly between Verdant Haven & Obsidian Deadwood anytime!");
        }

        public void RestartAdventure()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public int GetQuestStepIndex()
        {
            if (ShrineDelivered || ShipRepaired) return 4;
            if (HasFlownToPlanetTwo) return 3;
            if (HasSolarisCore) return 2;
            if (TalkedToAstra) return 1;
            return 0;
        }

        private void OnGUI()
        {
            GUIStyle pillStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 7, 7)
            };
            pillStyle.normal.textColor = new Color(1f, 0.96f, 0.82f);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };
            subStyle.normal.textColor = new Color(0.88f, 0.96f, 0.95f);

            string zoneTitle = "PLANET 1: VERDANT HAVEN";
            if (PlanetaryAtmosphereController.Instance != null)
            {
                switch (PlanetaryAtmosphereController.Instance.CurrentZone)
                {
                    case PlanetaryZone.DeepSpaceCorridor:
                        zoneTitle = "SECTOR: DEEP SPACE ASTEROID BELT";
                        break;
                    case PlanetaryZone.ObsidianDeadwood:
                        zoneTitle = "PLANET 2: OBSIDIAN DEADWOOD";
                        break;
                }
            }

            // Top-Left: Sector & Quest Objective Card
            GUI.color = new Color(0.08f, 0.11f, 0.22f, 0.88f);
            GUI.Box(new Rect(18, 18, 430, 96), GUIContent.none);
            GUI.color = Color.white;

            int step = GetQuestStepIndex();
            GUI.Label(new Rect(30, 24, 405, 28), $"{zoneTitle}   |   QUEST: {step}/4", pillStyle);

            string objText;
            if (ShrineDelivered || ShipRepaired)
                objText = "★ QUEST COMPLETE: The Solaris Astral Core illuminates Obsidian Deadwood!";
            else if (saucerFlight != null && saucerFlight.IsInFlight)
                objText = "▸ Pilot the Starhopper Flying Saucer through the Asteroid Belt to the Obsidian Planet Beacon!";
            else if (HasFlownToPlanetTwo)
                objText = "▸ Follow the dark deadwood trail to Warden Nyx & press [E] at the Dying Obsidian Shrine!";
            else if (HasSolarisCore)
                objText = "▸ Return to the stone launchpad and press [E] to board the Starhopper Flying Saucer!";
            else if (TalkedToAstra)
                objText = "▸ Climb Starlight Ridge (northeast behind the waterfall) to retrieve the Solaris Astral Core!";
            else
                objText = "▸ Talk to Captain Astra by the Starport Campfire (press [E]) for your mission briefing!";

            GUI.Label(new Rect(32, 56, 402, 50), objText, subStyle);

            // Top-Right: Controls Card ([H] to toggle)
            if (ShowControlsHelp)
            {
                GUI.color = new Color(0.08f, 0.11f, 0.22f, 0.84f);
                GUI.Box(new Rect(Screen.width - 322, 18, 304, 128), GUIContent.none);
                GUI.color = Color.white;
                string controlsText = (saucerFlight != null && saucerFlight.IsInFlight)
                    ? "SPACE FLIGHT CONTROLS ([H] Toggle)\n" +
                      "• Steer Left/Right: A / D or Left Stick\n" +
                      "• Pitch Climb/Dive: W / S or Up / Down\n" +
                      "• Hyper-Thruster Boost: Shift / RT\n" +
                      "• Quick Warp to Landing Pad: G"
                    : "CONTROLS ([H] Toggle)\n" +
                      "• Move & Run: WASD + Shift / Left Stick\n" +
                      "• Jump: Space / South Button (A/Cross)\n" +
                      "• Talk / Board Ship / Interact: E / West\n" +
                      "• Camera Orbit: Mouse / Right Stick";
                GUI.Label(new Rect(Screen.width - 308, 24, 282, 114), controlsText, subStyle);
            }

            // Space Flight Telemetry HUD when flying the Saucer
            if (saucerFlight != null && saucerFlight.IsInFlight)
            {
                GUI.color = new Color(0.06f, 0.14f, 0.26f, 0.90f);
                Rect fRect = new Rect(Screen.width * 0.5f - 270f, Screen.height - 118f, 540f, 64f);
                GUI.Box(fRect, GUIContent.none);
                GUI.color = Color.white;

                int pct = Mathf.RoundToInt(saucerFlight.FlightProgress01 * 100f);
                string destName = saucerFlight.FlyingTowardPlanetTwo ? "OBSIDIAN DEADWOOD" : "VERDANT HAVEN";
                GUIStyle flightStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                flightStyle.normal.textColor = new Color(0.55f, 0.98f, 1.0f);
                GUI.Label(new Rect(fRect.x, fRect.y + 8f, fRect.width, 24f),
                    $"🛸 PILOTING STARHOPPER SAUCER  →  {destName} ({pct}%)   |   SPEED: {saucerFlight.CurrentSpeed:F0} m/s",
                    flightStyle);

                // Progress bar track
                GUI.color = new Color(0.15f, 0.25f, 0.38f, 1f);
                GUI.Box(new Rect(fRect.x + 28f, fRect.y + 38f, fRect.width - 56f, 14f), GUIContent.none);
                GUI.color = new Color(0.25f, 0.95f, 0.88f, 1f);
                GUI.Box(new Rect(fRect.x + 28f, fRect.y + 38f, (fRect.width - 56f) * Mathf.Clamp01(saucerFlight.FlightProgress01), 14f), GUIContent.none);
                GUI.color = Color.white;
            }

            // Active NPC Dialogue Modal
            NpcCharacterDialogue activeNpc = null;
            if (captainAstra != null && captainAstra.IsDialogueOpen) activeNpc = captainAstra;
            else if (wardenNyx != null && wardenNyx.IsDialogueOpen) activeNpc = wardenNyx;

            if (activeNpc != null)
            {
                GUI.color = new Color(0.07f, 0.09f, 0.20f, 0.94f);
                Rect dRect = new Rect(Screen.width * 0.5f - 310f, Screen.height - 210f, 620f, 102f);
                GUI.Box(dRect, GUIContent.none);
                GUI.color = Color.white;

                GUIStyle speakerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                speakerStyle.normal.textColor = new Color(1.0f, 0.88f, 0.42f);
                GUI.Label(new Rect(dRect.x + 18f, dRect.y + 10f, 580f, 22f),
                    $"💬 {activeNpc.npcName.ToUpper()} — {activeNpc.npcRole} ({activeNpc.GetActiveLineNumber()}/{activeNpc.GetTotalLineCount()})",
                    speakerStyle);

                GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    wordWrap = true
                };
                bodyStyle.normal.textColor = new Color(0.95f, 0.97f, 1.0f);
                GUI.Label(new Rect(dRect.x + 18f, dRect.y + 36f, 584f, 44f), activeNpc.GetActiveLineText(), bodyStyle);

                GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Italic,
                    alignment = TextAnchor.MiddleRight
                };
                hintStyle.normal.textColor = new Color(0.65f, 0.92f, 0.88f);
                GUI.Label(new Rect(dRect.x + 18f, dRect.y + 76f, 584f, 20f), "Press [E] to continue...", hintStyle);
            }

            // Bottom Interaction Prompt
            if (player != null && !player.IsInVehicle)
            {
                string prompt = "";
                if (player.FocusedInteractable != null)
                    prompt = player.FocusedInteractable.GetPromptText(player);
                else if (player.CarriedFruit != null)
                    prompt = "Carrying Starberry — Press [E] near Hungry Plant to Feed, or [Q]/[Click] to Throw";

                if (!string.IsNullOrEmpty(prompt))
                {
                    GUI.color = new Color(0.10f, 0.16f, 0.28f, 0.92f);
                    Rect pRect = new Rect(Screen.width * 0.5f - 280f, Screen.height - 92f, 560f, 40f);
                    GUI.Box(pRect, GUIContent.none);
                    GUIStyle centerBold = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    centerBold.normal.textColor = new Color(1f, 0.95f, 0.68f);
                    GUI.color = Color.white;
                    GUI.Label(pRect, prompt, centerBold);
                }
            }

            // Banner toast notification
            if (_bannerTimer > 0f && !string.IsNullOrEmpty(BannerMessage))
            {
                GUI.color = new Color(0.14f, 0.10f, 0.26f, 0.90f);
                Rect bRect = new Rect(Screen.width * 0.5f - 310f, 22f, 620f, 38f);
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

            // Celebration Modal Panel when Quest is Completed!
            if ((ShrineDelivered || ShipRepaired) && activeNpc == null)
            {
                GUI.color = new Color(0.08f, 0.11f, 0.22f, 0.93f);
                Rect cRect = new Rect(Screen.width * 0.5f - 245f, Screen.height - 205f, 490f, 96f);
                GUI.Box(cRect, GUIContent.none);
                GUI.color = Color.white;

                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = new Color(1f, 0.92f, 0.48f);
                GUI.Label(new Rect(cRect.x, cRect.y + 8f, cRect.width, 28f),
                    "🎉 SOLARIS CORE DELIVERED — SHRINE IGNITED! 🎉", titleStyle);

                if (GUI.Button(new Rect(cRect.x + 40f, cRect.y + 46f, 195f, 34f), "Keep Exploring [C]"))
                {
                    KeepExploring();
                }
                if (GUI.Button(new Rect(cRect.x + 255f, cRect.y + 46f, 195f, 34f), "Restart Adventure [P]"))
                {
                    RestartAdventure();
                }
            }
        }
    }
}
