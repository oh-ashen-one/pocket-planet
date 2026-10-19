using System.Collections;
using UnityEngine;

namespace PocketPlanet
{
    public class ObsidianCrystalPylon : MonoBehaviour, IPocketInteractable
    {
        public int pylonIndex = 0;
        public string pylonTitle = "Resonant Crystal Pylon";
        public Renderer[] crystalRenderers;
        public Transform floatingRing;
        public Light pylonLight;
        public LineRenderer tetherBeam;
        public ObsidianShadowBarrier targetBarrier;

        private bool _isAttuned;
        private Material _attunedMat;

        public bool IsAttuned => _isAttuned;

        private void Awake()
        {
            if (GetComponent<Collider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 1.0f, 0f);
                sc.radius = 1.6f;
                sc.isTrigger = true;
            }
        }

        private void Update()
        {
            float t = Time.time;
            if (floatingRing != null)
            {
                floatingRing.Rotate(0f, (_isAttuned ? 115f : 38f) * Time.deltaTime, 0f, Space.Self);
                Vector3 lp = floatingRing.localPosition;
                lp.y = 2.05f + Mathf.Sin(t * 2.8f + pylonIndex) * 0.12f;
                floatingRing.localPosition = lp;
            }

            if (tetherBeam != null && targetBarrier != null)
            {
                tetherBeam.SetPosition(0, transform.position + Vector3.up * 2.15f);
                tetherBeam.SetPosition(1, targetBarrier.transform.position + Vector3.up * 2.4f);
            }

            // Auto-attune when Pip walks close with the Solaris Core so platforming feels fluid and responsive
            if (!_isAttuned)
            {
                var gm = PocketPlanetGameManager.Instance;
                if (gm != null && gm.player != null && gm.HasSolarisCore)
                {
                    float dist = Vector3.Distance(gm.player.transform.position, transform.position);
                    if (dist <= 2.75f)
                    {
                        AttunePylon(gm.player);
                    }
                }
            }
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position + Vector3.up * 1.4f;
        }

        public bool CanInteract(PipCharacterController player)
        {
            if (_isAttuned || player == null)
                return false;
            return Vector3.Distance(player.transform.position, transform.position) <= 4.3f;
        }

        public string GetPromptText(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && !gm.HasSolarisCore)
                return $"{pylonTitle} — Needs Solaris Astral Core";
            return $"Press [E] to Channel Solaris Core into {pylonTitle}";
        }

        public void OnInteract(PipCharacterController player)
        {
            AttunePylon(player);
        }

        public void AttunePylon(PipCharacterController player)
        {
            if (_isAttuned)
                return;

            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && !gm.HasSolarisCore)
            {
                PocketPlanetAudio.PlaySfx("npc_chirp", 0.65f, 0.8f);
                return;
            }

            _isAttuned = true;
            PocketPlanetAudio.PlaySfx("pylon_attune", 0.92f, 1.0f + pylonIndex * 0.08f);
            StartCoroutine(AnimateAttunementRoutine());

            if (gm != null)
            {
                gm.OnAttunedObsidianPylon(pylonIndex, this);
            }
            if (targetBarrier != null)
            {
                targetBarrier.NotifyPylonAttuned(pylonIndex);
            }
        }

        private IEnumerator AnimateAttunementRoutine()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _attunedMat = new Material(sh);
            _attunedMat.color = new Color(1.0f, 0.88f, 0.34f, 1f);
            if (_attunedMat.HasProperty("_BaseColor"))
                _attunedMat.SetColor("_BaseColor", new Color(1.0f, 0.88f, 0.34f, 1f));
            if (_attunedMat.HasProperty("_EmissionColor"))
            {
                _attunedMat.EnableKeyword("_EMISSION");
                _attunedMat.SetColor("_EmissionColor", new Color(0.95f, 0.68f, 0.18f) * 1.15f);
            }

            if (crystalRenderers != null)
            {
                foreach (var r in crystalRenderers)
                {
                    if (r != null)
                        r.sharedMaterial = _attunedMat;
                }
            }

            if (tetherBeam != null)
            {
                tetherBeam.startColor = new Color(1.0f, 0.86f, 0.32f, 0.95f);
                tetherBeam.endColor = new Color(0.32f, 0.96f, 1.0f, 0.95f);
                tetherBeam.startWidth = 0.16f;
                tetherBeam.endWidth = 0.11f;
            }

            Vector3 baseScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < 0.55f)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / 0.55f);
                float pulse = 1.0f + Mathf.Sin(u * Mathf.PI) * 0.18f;
                transform.localScale = baseScale * pulse;
                if (pylonLight != null)
                {
                    pylonLight.color = Color.Lerp(new Color(0.68f, 0.25f, 1.0f), new Color(1.0f, 0.85f, 0.36f), u);
                    pylonLight.intensity = Mathf.Lerp(0.8f, 1.85f, u);
                    pylonLight.range = Mathf.Lerp(6.5f, 12.5f, u);
                }
                yield return null;
            }
            transform.localScale = baseScale;
        }
    }
}
