using UnityEngine;

namespace PocketPlanet
{
    public class SolarisCoreRelic : MonoBehaviour, IPocketInteractable
    {
        public Transform visualRoot;
        public GameObject beaconColumn;
        public Light corePointLight;
        public bool IsCollected { get; private set; }

        private Vector3 _basePos;

        private void Start()
        {
            if (visualRoot == null)
                visualRoot = transform;
            _basePos = visualRoot.position;
        }

        private void Update()
        {
            if (IsCollected) return;

            float t = Time.time;
            if (visualRoot != null)
            {
                visualRoot.position = _basePos + new Vector3(0f, Mathf.Sin(t * 2.4f) * 0.18f, 0f);
                visualRoot.Rotate(0f, 65f * Time.deltaTime, 18f * Time.deltaTime, Space.Self);
            }

            if (corePointLight != null)
            {
                corePointLight.intensity = 3.2f + Mathf.Sin(t * 4.5f) * 0.65f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsCollected) return;
            var pip = other.GetComponentInParent<PipCharacterController>();
            if (pip != null)
            {
                CollectRelic(pip);
            }
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public bool CanInteract(PipCharacterController player)
        {
            return !IsCollected && player != null && Vector3.Distance(player.transform.position, transform.position) <= 4.0f;
        }

        public string GetPromptText(PipCharacterController player)
        {
            return "Press [E] to Retrieve the Solaris Astral Core";
        }

        public void OnInteract(PipCharacterController player) => Interact(player);

        public void Interact(PipCharacterController player)
        {
            CollectRelic(player);
        }

        public void CollectRelic(PipCharacterController player)
        {
            if (IsCollected) return;
            IsCollected = true;

            PocketPlanetAudio.PlaySfx("solaris_core_pickup", 1.0f, 1.0f);

            if (beaconColumn != null)
                beaconColumn.SetActive(false);

            // Attach a glowing Solaris Core lantern orb above Pip's backpack
            if (player != null)
            {
                AttachCarriedCoreAura(player.transform);
            }

            if (PocketPlanetGameManager.Instance != null)
            {
                PocketPlanetGameManager.Instance.OnSolarisCoreCollected();
            }

            gameObject.SetActive(false);
        }

        public static GameObject AttachCarriedCoreAura(Transform playerRoot)
        {
            Transform existing = playerRoot.Find("CarriedSolarisCoreAura");
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing.gameObject;
            }

            GameObject auraRoot = new GameObject("CarriedSolarisCoreAura");
            auraRoot.transform.SetParent(playerRoot, false);
            auraRoot.transform.localPosition = new Vector3(0.26f, 0.92f, -0.34f);

            GameObject coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreSphere.name = "SolarisOrb";
            coreSphere.transform.SetParent(auraRoot.transform, false);
            coreSphere.transform.localScale = Vector3.one * 0.25f;
            var col = coreSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = coreSphere.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(sh);
                Color gold = new Color(1.0f, 0.86f, 0.28f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", gold);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", gold * 1.6f);
                }
                mr.sharedMaterial = mat;
            }

            GameObject lightGo = new GameObject("SolarisLanternLight");
            lightGo.transform.SetParent(auraRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.65f, -0.25f);
            Light pt = lightGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(1.0f, 0.84f, 0.42f);
            pt.intensity = 1.15f;
            pt.range = 14f;
            pt.shadows = LightShadows.None;

            return auraRoot;
        }
    }
}
