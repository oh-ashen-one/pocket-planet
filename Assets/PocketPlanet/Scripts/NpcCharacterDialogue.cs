using UnityEngine;

namespace PocketPlanet
{
    public class NpcCharacterDialogue : MonoBehaviour, IPocketInteractable
    {
        public string npcName = "Captain Astra";
        public string npcRole = "Starport Pathfinder";
        public bool isPlanetOneQuestGiver = true;
        public bool isPlanetTwoRecipient = false;
        public Transform visualModel;
        public Transform questMarkerRoot;

        public string[] initialLines = new[]
        {
            "Hey there, Pip! Our sister sanctuary on the Obsidian Deadwood planet has gone dark!",
            "Thick violet shadow-fog swallowed their forest, and the ancient Obsidian Shrine Lantern is dying.",
            "Climb the terraced Starlight Ridge behind the waterfall pond to retrieve the golden SOLARIS ASTRAL CORE!",
            "Once you have the Core, board the Starhopper Flying Saucer on the launchpad and pilot it across the asteroid belt to Warden Nyx!"
        };

        public string[] reminderLines = new[]
        {
            "The Solaris Astral Core is glowing atop Starlight Ridge to the northeast!",
            "Use the Bouncy Jelly-Shrooms along the cliff path, grab the Core, and board the Flying Saucer!"
        };

        public string[] readyToFlyLines = new[]
        {
            "You've got the Solaris Astral Core! Look how warmly it glows!",
            "Step up to the Starhopper Flying Saucer on the stone launchpad and press [E] to launch into deep space!"
        };

        private Vector3 _baseModelLocalPos;
        private Quaternion _baseRot;
        private int _lineIndex = -1;
        private float _talkBounceTimer;

        public bool IsDialogueOpen => _lineIndex >= 0;

        private void Start()
        {
            if (visualModel == null)
                visualModel = transform;
            _baseModelLocalPos = visualModel.localPosition;
            _baseRot = transform.rotation;
        }

        private void Update()
        {
            float t = Time.time;
            if (visualModel != null)
            {
                float bob = Mathf.Sin(t * 2.6f) * 0.035f;
                float bounce = _talkBounceTimer > 0f ? Mathf.Abs(Mathf.Sin(t * 16f)) * 0.14f : 0f;
                visualModel.localPosition = _baseModelLocalPos + new Vector3(0f, bob + bounce, 0f);
            }

            if (_talkBounceTimer > 0f)
                _talkBounceTimer -= Time.deltaTime;

            if (questMarkerRoot != null)
            {
                questMarkerRoot.localPosition = new Vector3(0f, 1.85f + Mathf.Sin(t * 3.8f) * 0.12f, 0f);
                questMarkerRoot.Rotate(0f, 95f * Time.deltaTime, 0f, Space.Self);
            }

            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && gm.player != null)
            {
                Vector3 toPlayer = gm.player.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 36f && toPlayer.sqrMagnitude > 0.05f)
                {
                    // Tripo FBX character models have their front face along local -Z
                    Quaternion look = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 6.5f);
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, _baseRot, Time.deltaTime * 3.0f);
                }

                if (IsDialogueOpen && toPlayer.sqrMagnitude > 42f)
                {
                    _lineIndex = -1;
                }
            }
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position + Vector3.up * 0.8f;
        }

        public bool CanInteract(PipCharacterController player)
        {
            return player != null && Vector3.Distance(player.transform.position, transform.position) <= 4.2f;
        }

        public string GetPromptText(PipCharacterController player)
        {
            if (IsDialogueOpen)
                return $"Press [E] to Continue Talking to {npcName}";
            return $"Press [E] to Talk to {npcName} ({npcRole})";
        }

        public void OnInteract(PipCharacterController player) => Interact(player);

        public void Interact(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            _talkBounceTimer = 0.55f;
            PocketPlanetAudio.PlaySfx("npc_chirp", 0.85f, Random.Range(0.95f, 1.08f));

            string[] activeLines = GetCurrentDialogueLines();
            if (_lineIndex < 0)
            {
                _lineIndex = 0;
                if (gm != null)
                {
                    if (isPlanetOneQuestGiver)
                        gm.OnTalkedToCaptainAstra();
                    else if (isPlanetTwoRecipient)
                        gm.OnTalkedToWardenNyx();
                }
            }
            else
            {
                _lineIndex++;
                if (_lineIndex >= activeLines.Length)
                {
                    _lineIndex = -1;
                }
            }
        }

        public string GetActiveLineText()
        {
            string[] lines = GetCurrentDialogueLines();
            if (_lineIndex < 0 || _lineIndex >= lines.Length)
                return string.Empty;
            return lines[_lineIndex];
        }

        public int GetActiveLineNumber() => _lineIndex + 1;
        public int GetTotalLineCount() => GetCurrentDialogueLines().Length;

        public void CloseDialogue()
        {
            _lineIndex = -1;
        }

        private string[] GetCurrentDialogueLines()
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm == null) return initialLines;

            if (isPlanetOneQuestGiver)
            {
                if (gm.HasSolarisCore || gm.ShrineDelivered)
                    return readyToFlyLines;
                if (gm.TalkedToAstra)
                    return initialLines;
                return initialLines;
            }

            if (isPlanetTwoRecipient)
            {
                if (gm.ShrineDelivered)
                {
                    return new[]
                    {
                        "The Solaris Astral Core shines so brightly inside the Obsidian Shrine!",
                        "You saved our sanctuary, Pip! The violet gloom is receding across Obsidian Deadwood!"
                    };
                }
                if (gm.HasSolarisCore)
                {
                    return new[]
                    {
                        "By the stars... you flew all the way from Verdant Haven carrying the Solaris Astral Core!",
                        "Place it into the Obsidian Shrine Lantern right beside me to banish the shadow gloom!"
                    };
                }
            }

            return initialLines;
        }
    }
}
