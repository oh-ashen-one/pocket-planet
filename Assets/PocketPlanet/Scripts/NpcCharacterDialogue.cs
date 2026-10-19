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
        private Transform _speechBubbleRoot;
        private TextMesh _speechBubbleText;
        private float _bubbleScaleLerp;

        public bool IsDialogueOpen => _lineIndex >= 0;

        private void Start()
        {
            if (visualModel == null)
                visualModel = transform;
            _baseModelLocalPos = visualModel.localPosition;
            _baseRot = transform.rotation;
            BuildWorldSpeechBubble();
        }

        private void BuildWorldSpeechBubble()
        {
            _speechBubbleRoot = new GameObject("WorldSpeechBubble").transform;
            _speechBubbleRoot.SetParent(transform, false);
            _speechBubbleRoot.localPosition = new Vector3(0f, 2.35f, 0f);
            _speechBubbleRoot.localScale = Vector3.zero;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material borderMat = new Material(sh);
            Color borderCol = isPlanetOneQuestGiver
                ? new Color(1.0f, 0.84f, 0.28f)
                : new Color(0.68f, 0.42f, 1.0f);
            if (borderMat.HasProperty("_BaseColor")) borderMat.SetColor("_BaseColor", borderCol);
            if (borderMat.HasProperty("_Color")) borderMat.SetColor("_Color", borderCol);
            if (borderMat.HasProperty("_EmissionColor"))
            {
                borderMat.EnableKeyword("_EMISSION");
                borderMat.SetColor("_EmissionColor", borderCol * 0.85f);
            }

            Material cardMat = new Material(sh);
            Color cardCol = new Color(0.08f, 0.11f, 0.22f);
            if (cardMat.HasProperty("_BaseColor")) cardMat.SetColor("_BaseColor", cardCol);
            if (cardMat.HasProperty("_Color")) cardMat.SetColor("_Color", cardCol);

            GameObject borderPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            borderPlate.name = "BubbleBorder";
            borderPlate.transform.SetParent(_speechBubbleRoot, false);
            borderPlate.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            borderPlate.transform.localScale = new Vector3(2.45f, 0.72f, 0.05f);
            Destroy(borderPlate.GetComponent<Collider>());
            borderPlate.GetComponent<MeshRenderer>().sharedMaterial = borderMat;

            GameObject innerPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            innerPlate.name = "BubbleCard";
            innerPlate.transform.SetParent(_speechBubbleRoot, false);
            innerPlate.transform.localPosition = Vector3.zero;
            innerPlate.transform.localScale = new Vector3(2.33f, 0.60f, 0.06f);
            Destroy(innerPlate.GetComponent<Collider>());
            innerPlate.GetComponent<MeshRenderer>().sharedMaterial = cardMat;

            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tail.name = "BubbleTail";
            tail.transform.SetParent(_speechBubbleRoot, false);
            tail.transform.localPosition = new Vector3(0f, -0.38f, 0.01f);
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.transform.localScale = new Vector3(0.18f, 0.18f, 0.05f);
            Destroy(tail.GetComponent<Collider>());
            tail.GetComponent<MeshRenderer>().sharedMaterial = borderMat;

            GameObject textGo = new GameObject("BubbleText");
            textGo.transform.SetParent(_speechBubbleRoot, false);
            textGo.transform.localPosition = new Vector3(0f, 0.02f, -0.045f);
            textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            textGo.transform.localScale = Vector3.one * 0.046f;
            _speechBubbleText = textGo.AddComponent<TextMesh>();
            _speechBubbleText.anchor = TextAnchor.MiddleCenter;
            _speechBubbleText.alignment = TextAlignment.Center;
            _speechBubbleText.fontStyle = FontStyle.Bold;
            _speechBubbleText.fontSize = 34;
            _speechBubbleText.color = new Color(0.10f, 0.06f, 0.24f, 1f);
            _speechBubbleText.text = $"{npcName}\n[E] Talk";
        }

        private static string WrapBubbleText(string title, string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine))
                return $"{title}\n[E] Talk";
            string trimmed = rawLine.Length > 64 ? rawLine.Substring(0, 61) + "..." : rawLine;
            if (trimmed.Length > 34)
            {
                int split = trimmed.LastIndexOf(' ', 34);
                if (split > 10)
                    trimmed = trimmed.Substring(0, split) + "\n" + trimmed.Substring(split + 1);
            }
            return $"{title}:\n{trimmed}";
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
                questMarkerRoot.gameObject.SetActive(!IsDialogueOpen);
            }

            var gm = PocketPlanetGameManager.Instance;
            bool playerNear = false;
            if (gm != null && gm.player != null)
            {
                Vector3 toPlayer = gm.player.transform.position - transform.position;
                toPlayer.y = 0f;
                playerNear = toPlayer.sqrMagnitude < 28f;
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

            if (_speechBubbleRoot != null)
            {
                bool shrineFinaleActive = gm != null && gm.ShrineDelivered && !IsDialogueOpen;
                bool showBubble = IsDialogueOpen || (playerNear && !shrineFinaleActive);
                float targetScale = IsDialogueOpen ? 1.0f : (showBubble ? 0.68f : 0.0f);
                _bubbleScaleLerp = Mathf.Lerp(_bubbleScaleLerp, targetScale, Time.deltaTime * 11f);
                _speechBubbleRoot.gameObject.SetActive(_bubbleScaleLerp > 0.03f);
                if (showBubble && _speechBubbleText != null)
                {
                    _speechBubbleText.text = IsDialogueOpen
                        ? WrapBubbleText(npcName, GetActiveLineText())
                        : $"{npcName}\nPress [E] to Talk";
                }
                _speechBubbleRoot.localPosition = new Vector3(0f, 2.38f + Mathf.Sin(t * 3.2f) * 0.06f, 0f);
                _speechBubbleRoot.localScale = Vector3.one * _bubbleScaleLerp;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - _speechBubbleRoot.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.01f)
                        _speechBubbleRoot.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
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
