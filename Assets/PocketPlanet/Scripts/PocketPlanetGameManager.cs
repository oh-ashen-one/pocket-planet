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
        public ObsidianShadowBarrier obsidianShadowBarrier;
        public List<ObsidianCrystalPylon> obsidianPylons = new List<ObsidianCrystalPylon>();
        public List<StarSeedCollectible> allSeeds = new List<StarSeedCollectible>();

        // Main Delivery Quest State
        public bool TalkedToAstra { get; private set; }
        public bool HasSolarisCore { get; private set; }
        public bool HasFlownToPlanetTwo { get; private set; }
        public bool TalkedToNyx { get; private set; }
        public bool ShrineDelivered { get; private set; }
        public int AttunedPylonCount { get; private set; }
        public int AsteroidsShatteredCount { get; private set; }
        public bool IsFlyingSaucerActive => saucerFlight != null && saucerFlight.IsInFlight;

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

        private void Start()
        {
            if (GetComponent<QuestWaypointCompass>() == null)
            {
                gameObject.AddComponent<QuestWaypointCompass>();
            }
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

        public Transform GetActiveQuestTargetTransform(out string label)
        {
            label = "";
            if (ShrineDelivered || ShipRepaired)
                return null;

            if (!TalkedToAstra && captainAstra != null)
            {
                label = "Captain Astra";
                return captainAstra.transform;
            }
            if (!HasSolarisCore && solarisCore != null)
            {
                label = "Solaris Astral Core";
                return solarisCore.transform;
            }
            if (!HasFlownToPlanetTwo && saucerFlight != null)
            {
                label = "Starhopper Flying Saucer";
                return saucerFlight.transform;
            }
            if (obsidianPylons != null && obsidianPylons.Count > 0 && AttunedPylonCount < obsidianPylons.Count)
            {
                ObsidianCrystalPylon nearest = null;
                float bestDist = float.MaxValue;
                Vector3 pPos = player != null ? player.transform.position : Vector3.zero;
                for (int i = 0; i < obsidianPylons.Count; i++)
                {
                    var p = obsidianPylons[i];
                    if (p != null && !p.IsAttuned)
                    {
                        float d = Vector3.Distance(pPos, p.transform.position);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            nearest = p;
                        }
                    }
                }
                if (nearest != null)
                {
                    label = $"{nearest.pylonTitle} ({AttunedPylonCount}/{obsidianPylons.Count})";
                    return nearest.transform;
                }
            }
            if (obsidianShrine != null)
            {
                label = "Obsidian Shrine Lantern";
                return obsidianShrine.transform;
            }
            return null;
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
                ShowBanner("🛸 SPACE FLIGHT: Shoot Crystal Asteroids ([F]/[Space]/Click) & Boost ([Shift]) to Obsidian Deadwood!");
            else
                ShowBanner("🛸 SPACE FLIGHT: Returning through the Asteroid Belt toward Verdant Haven!");
        }

        public void OnSpaceAsteroidShattered()
        {
            AsteroidsShatteredCount++;
            RecordInteraction("Shattered Crystal Space Asteroid");
        }

        public void OnLandedOnPlanet(bool onPlanetTwo)
        {
            if (onPlanetTwo)
            {
                HasFlownToPlanetTwo = true;
                CollectedSeeds = Mathf.Max(CollectedSeeds, 3);
                RecordInteraction("Landed on Obsidian Deadwood Planet");
                ShowBanner("🌑 Touchdown on OBSIDIAN DEADWOOD! Attune the 3 Resonant Crystal Pylons to unshield the Shrine!");
            }
            else
            {
                ShowBanner("🌿 Touchdown on VERDANT HAVEN!");
            }
        }

        public void OnAttunedObsidianPylon(int index, ObsidianCrystalPylon pylon)
        {
            int count = 0;
            if (obsidianPylons != null && obsidianPylons.Count > 0)
            {
                for (int i = 0; i < obsidianPylons.Count; i++)
                {
                    if (obsidianPylons[i] != null && obsidianPylons[i].IsAttuned)
                        count++;
                }
            }
            else
            {
                count = AttunedPylonCount + 1;
            }
            AttunedPylonCount = count;
            RecordInteraction($"Attuned Obsidian Crystal Pylon #{index + 1}");

            int total = (obsidianPylons != null && obsidianPylons.Count > 0) ? obsidianPylons.Count : 3;
            if (AttunedPylonCount < total)
            {
                ShowBanner($"🔮 Resonant Pylon Attuned ({AttunedPylonCount}/{total})! Follow the Starlight Compass to the next Pylon!");
            }
            else
            {
                ShowBanner("⚡ ALL 3 CRYSTAL PYLONS ATTUNED — Shadow Barrier Dissolved! Ignite the Obsidian Shrine!");
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
            GUI.Box(new Rect(18, 18, 445, 98), GUIContent.none);
            GUI.color = Color.white;

            int step = GetQuestStepIndex();
            GUI.Label(new Rect(30, 24, 420, 28), $"{zoneTitle}   |   QUEST: {step}/4", pillStyle);

            string objText;
            if (ShrineDelivered || ShipRepaired)
                objText = "★ QUEST COMPLETE: The Solaris Astral Core illuminates Obsidian Deadwood!";
            else if (saucerFlight != null && saucerFlight.IsInFlight)
                objText = $"▸ Pilot Saucer to Obsidian Deadwood! Fire Star-Pulse Blaster [F]/[Click] ({AsteroidsShatteredCount} shattered)";
            else if (HasFlownToPlanetTwo && obsidianPylons != null && obsidianPylons.Count > 0 && AttunedPylonCount < obsidianPylons.Count)
                objText = $"▸ Cross the Spooky Bog Chasm & Attune 3 Resonant Crystal Pylons ({AttunedPylonCount}/{obsidianPylons.Count}) to unshield the Shrine!";
            else if (HasFlownToPlanetTwo)
                objText = "▸ Shadow Barrier Dissolved! Speak to Warden Nyx & press [E] at the Obsidian Shrine!";
            else if (HasSolarisCore)
                objText = "▸ Return to the stone launchpad and press [E] to board the Starhopper Flying Saucer!";
            else if (TalkedToAstra)
                objText = "▸ Climb Starlight Ridge (northeast behind the waterfall) to retrieve the Solaris Astral Core!";
            else
                objText = "▸ Talk to Captain Astra by the Starport Campfire (press [E]) for your mission briefing!";

            GUI.Label(new Rect(32, 56, 418, 52), objText, subStyle);

            // Top-Right: Controls Card ([H] to toggle)
            if (ShowControlsHelp)
            {
                GUI.color = new Color(0.08f, 0.11f, 0.22f, 0.84f);
                GUI.Box(new Rect(Screen.width - 332, 18, 314, 134), GUIContent.none);
                GUI.color = Color.white;
                string controlsText = (saucerFlight != null && saucerFlight.IsInFlight)
                    ? "SPACE FLIGHT CONTROLS ([H] Toggle)\n" +
                      "• Steer & Pitch: WASD / Left Stick\n" +
                      "• Star-Pulse Blaster: Space / F / Click\n" +
                      "• Hyper-Thruster Boost: Shift / RT\n" +
                      "• Quick Warp to Landing Pad: G"
                    : "CONTROLS ([H] Toggle)\n" +
                      "• Move & Run: WASD + Shift / Left Stick\n" +
                      "• Jump: Space / South Button (A/Cross)\n" +
                      "• Talk / Board / Attune / Ignite: E / West\n" +
                      "• Camera Orbit: Mouse / Right Stick";
                GUI.Label(new Rect(Screen.width - 318, 24, 292, 120), controlsText, subStyle);
            }

            // Space Flight Telemetry HUD when flying the Saucer
            if (saucerFlight != null && saucerFlight.IsInFlight)
            {
                GUI.color = new Color(0.06f, 0.14f, 0.26f, 0.92f);
                Rect fRect = new Rect(Screen.width * 0.5f - 310f, Screen.height - 122f, 620f, 68f);
                GUI.Box(fRect, GUIContent.none);
                GUI.color = Color.white;

                int pct = Mathf.RoundToInt(saucerFlight.FlightProgress01 * 100f);
                string destName = saucerFlight.FlyingTowardPlanetTwo ? "OBSIDIAN DEADWOOD" : "VERDANT HAVEN";
                GUIStyle flightStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                flightStyle.normal.textColor = new Color(0.55f, 0.98f, 1.0f);
                GUI.Label(new Rect(fRect.x, fRect.y + 8f, fRect.width, 24f),
                    $"🛸 SAUCER → {destName} ({pct}%)  |  SHIELD: {saucerFlight.HullShieldPercent:F0}%  |  METEORS SHATTERED: {saucerFlight.AsteroidsShatteredCount}",
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
