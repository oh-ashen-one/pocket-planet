using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PocketPlanet
{
    public class DestructibleSpaceAsteroid : MonoBehaviour
    {
        public static readonly List<DestructibleSpaceAsteroid> ActiveAsteroids = new List<DestructibleSpaceAsteroid>();

        public Vector3 rotationSpeed = new Vector3(14f, 22f, 9f);
        public float hitRadius = 3.6f;

        private bool _isShattered;
        private Renderer[] _renderers;
        private Collider[] _colliders;

        public bool IsShattered => _isShattered;

        private void OnEnable()
        {
            if (!ActiveAsteroids.Contains(this))
                ActiveAsteroids.Add(this);
        }

        private void OnDisable()
        {
            ActiveAsteroids.Remove(this);
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            if (_isShattered)
                return;

            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }

        public void ShatterAsteroid(SpaceshipFlightController saucer)
        {
            if (_isShattered)
                return;
            _isShattered = true;

            PocketPlanetAudio.PlaySfx("asteroid_shatter", 0.9f, Random.Range(0.92f, 1.12f));

            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null) r.enabled = false;
                }
            }
            if (_colliders != null)
            {
                foreach (var c in _colliders)
                {
                    if (c != null) c.enabled = false;
                }
            }

            if (saucer != null)
                saucer.OnAsteroidShattered();

            if (PocketPlanetGameManager.Instance != null)
                PocketPlanetGameManager.Instance.OnSpaceAsteroidShattered();

            StartCoroutine(SpawnCrystalShardBurstRoutine());
        }

        private IEnumerator SpawnCrystalShardBurstRoutine()
        {
            int shardCount = 6;
            Transform[] shards = new Transform[shardCount];
            Vector3[] velocities = new Vector3[shardCount];

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material shardMat = new Material(sh);
            Color shardCol = new Color(0.35f, 0.92f, 1.0f);
            if (shardMat.HasProperty("_BaseColor")) shardMat.SetColor("_BaseColor", shardCol);
            if (shardMat.HasProperty("_Color")) shardMat.SetColor("_Color", shardCol);
            if (shardMat.HasProperty("_EmissionColor"))
            {
                shardMat.EnableKeyword("_EMISSION");
                shardMat.SetColor("_EmissionColor", new Color(0.55f, 0.32f, 1.0f) * 2.2f);
            }

            GameObject burstRoot = new GameObject("AsteroidShardBurst");
            burstRoot.transform.position = transform.position;

            for (int i = 0; i < shardCount; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.transform.SetParent(burstRoot.transform, false);
                s.transform.localPosition = Random.insideUnitSphere * 0.4f;
                s.transform.localRotation = Random.rotation;
                s.transform.localScale = new Vector3(0.45f, 0.72f, 0.45f);
                var col = s.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var mr = s.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = shardMat;

                shards[i] = s.transform;
                velocities[i] = (Random.onUnitSphere + Vector3.up * 0.25f).normalized * Random.Range(6.5f, 13.5f);
            }

            float elapsed = 0f;
            const float dur = 0.65f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                float scaleMul = 1.0f - u;
                for (int i = 0; i < shardCount; i++)
                {
                    if (shards[i] != null)
                    {
                        shards[i].position += velocities[i] * Time.deltaTime;
                        shards[i].Rotate(240f * Time.deltaTime, 320f * Time.deltaTime, 180f * Time.deltaTime, Space.Self);
                        shards[i].localScale = new Vector3(0.45f, 0.72f, 0.45f) * scaleMul;
                    }
                }
                yield return null;
            }

            Destroy(burstRoot);
        }
    }
}
