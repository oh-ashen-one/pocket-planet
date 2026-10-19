using UnityEngine;

namespace PocketPlanet
{
    public class ObsidianShrineAltar : MonoBehaviour, IPocketInteractable
    {
        public Transform coreSocketTransform;
        public Light shrineLight;
        public GameObject ignitedBeamColumn;
        public GameObject[] sanctuaryTorches;
        public ObsidianShadowBarrier shadowBarrier;
        public bool IsIgnited { get; private set; }

        private Transform _placedCoreVisual;

        private void Start()
        {
            if (ignitedBeamColumn != null)
                ignitedBeamColumn.SetActive(false);
            if (shrineLight != null)
            {
                shrineLight.color = new Color(0.55f, 0.25f, 0.90f);
                shrineLight.intensity = 1.1f;
                shrineLight.range = 8f;
            }
        }

        private void Update()
        {
            float t = Time.time;
            if (!IsIgnited)
            {
                if (shrineLight != null)
                {
                    shrineLight.intensity = 0.85f + Mathf.Sin(t * 5.2f) * 0.35f;
                }
            }
            else
            {
                if (_placedCoreVisual != null)
                {
                    _placedCoreVisual.Rotate(0f, 95f * Time.deltaTime, 25f * Time.deltaTime, Space.Self);
                }
                if (shrineLight != null)
                {
                    shrineLight.intensity = 1.85f + Mathf.Sin(t * 3.2f) * 0.25f;
                }
            }
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position + Vector3.up * 1.0f;
        }

        public bool CanInteract(PipCharacterController player)
        {
            if (IsIgnited || player == null) return false;
            return Vector3.Distance(player.transform.position, transform.position) <= 5.4f;
        }

        public string GetPromptText(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && !gm.HasSolarisCore)
                return "Dying Obsidian Shrine — Requires the Solaris Astral Core from Verdant Haven";
            if (shadowBarrier != null && !shadowBarrier.IsDissolved)
            {
                int count = gm != null ? gm.AttunedPylonCount : shadowBarrier.AttunedCount;
                return $"Shrine Shielded by Shadow Barrier — Attune 3 Crystal Pylons ({count}/3)";
            }
            return "Press [E] to Deliver Solaris Astral Core & Ignite the Obsidian Shrine!";
        }

        public void OnInteract(PipCharacterController player) => Interact(player);

        public void Interact(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && !gm.HasSolarisCore)
            {
                PocketPlanetAudio.PlaySfx("lever_pull", 0.7f, 0.85f);
                gm.ShowBanner("This ancient shrine needs the Solaris Astral Core from Verdant Haven!");
                return;
            }

            if (shadowBarrier != null && !shadowBarrier.IsDissolved)
            {
                int count = gm != null ? gm.AttunedPylonCount : shadowBarrier.AttunedCount;
                PocketPlanetAudio.PlaySfx("lever_pull", 0.75f, 0.9f);
                if (gm != null)
                    gm.ShowBanner($"Shadow Barrier active! Channel your Solaris Core into all 3 Resonant Crystal Pylons ({count}/3) first!");
                return;
            }

            IgniteShrine(player);
        }

        public void IgniteShrine(PipCharacterController player)
        {
            if (IsIgnited) return;
            IsIgnited = true;

            if (shadowBarrier != null && !shadowBarrier.IsDissolved)
                shadowBarrier.DissolveBarrier();

            PocketPlanetAudio.PlaySfx("shrine_ignite", 1.0f, 1.0f);
            PocketPlanetAudio.PlaySfx("ship_repair_fanfare", 0.9f, 1.0f);

            if (player != null)
            {
                Transform aura = player.transform.Find("CarriedSolarisCoreAura");
                if (aura != null)
                    aura.gameObject.SetActive(false);
                player.SetCelebrating(true);
            }

            if (ignitedBeamColumn != null)
                ignitedBeamColumn.SetActive(true);

            if (shrineLight != null)
            {
                shrineLight.color = new Color(1.0f, 0.90f, 0.52f);
                shrineLight.intensity = 1.95f;
                shrineLight.range = 28f;
            }

            // Spawn the glowing Solaris Core inside the Shrine Lantern's socket
            Vector3 socketPos = coreSocketTransform != null ? coreSocketTransform.position : (transform.position + Vector3.up * 2.1f);
            GameObject coreOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreOrb.name = "IgnitedSolarisCoreOrb";
            coreOrb.transform.SetParent(transform, true);
            coreOrb.transform.position = socketPos;
            coreOrb.transform.localScale = Vector3.one * 0.65f;
            var col = coreOrb.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = coreOrb.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(sh);
                Color gold = new Color(1.0f, 0.92f, 0.42f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", gold);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", gold * 4.5f);
                }
                mr.sharedMaterial = mat;
            }
            _placedCoreVisual = coreOrb.transform;

            if (sanctuaryTorches != null)
            {
                foreach (var t in sanctuaryTorches)
                {
                    if (t != null) t.SetActive(true);
                }
            }

            if (PlanetaryAtmosphereController.Instance != null)
            {
                PlanetaryAtmosphereController.Instance.SetShrineIgnited(true);
            }

            if (PocketPlanetGameManager.Instance != null)
            {
                PocketPlanetGameManager.Instance.OnObsidianShrineIgnited();
            }
        }
    }
}
