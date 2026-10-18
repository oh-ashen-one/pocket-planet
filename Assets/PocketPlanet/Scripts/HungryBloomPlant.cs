using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class HungryBloomPlant : MonoBehaviour, IPocketInteractable
    {
        public StarSeedCollectible rewardSeed;
        public Transform bloomPlatform;
        public bool IsBloomed { get; private set; }

        private readonly List<Transform> _petals = new List<Transform>();
        private readonly List<Quaternion> _petalBaseRots = new List<Quaternion>();
        private Vector3 _baseScale;
        private float _animTime;

        private void Awake()
        {
            _baseScale = transform.localScale;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("BloomPetal_"))
                {
                    _petals.Add(t);
                    _petalBaseRots.Add(t.localRotation);
                    // Start partially curled inward before blooming
                    t.localRotation = t.localRotation * Quaternion.Euler(28f, 0f, 0f);
                }
            }

            // Create solid platform collider at top of bloom
            if (bloomPlatform == null)
            {
                GameObject plat = new GameObject("BloomPetalPlatform");
                plat.transform.SetParent(transform, false);
                plat.transform.localPosition = new Vector3(0f, 1.32f, 0f);
                BoxCollider bc = plat.AddComponent<BoxCollider>();
                bc.size = new Vector3(2.4f, 0.24f, 2.4f);
                plat.SetActive(false);
                bloomPlatform = plat.transform;
            }

            if (GetComponent<SphereCollider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 0.65f, 0f);
                sc.radius = 0.85f;
                sc.isTrigger = true;
            }
        }

        private void Update()
        {
            _animTime += Time.deltaTime;
            if (!IsBloomed)
            {
                // Hungry gentle pulse + check if a thrown Starberry lands near mouth
                float pulse = 1f + 0.04f * Mathf.Sin(_animTime * 3.8f);
                transform.localScale = new Vector3(_baseScale.x * pulse, _baseScale.y / pulse, _baseScale.z * pulse);

                Collider[] cols = Physics.OverlapSphere(transform.position + Vector3.up * 0.8f, 1.65f);
                foreach (Collider c in cols)
                {
                    StarberryItem berry = c.GetComponentInParent<StarberryItem>();
                    if (berry != null && !berry.IsCarried && !berry.IsConsumed)
                    {
                        FeedBerry(berry, null);
                        break;
                    }
                }
            }
        }

        public string GetPromptText(PipCharacterController player)
        {
            if (IsBloomed) return "";
            return player.CarriedFruit != null
                ? "Press [E] / [West] to Feed Starberry to Hungry Bloom-Plant!"
                : "Hungry Bloom-Plant: Bring it a plump Starberry!";
        }

        public bool CanInteract(PipCharacterController player)
        {
            return !IsBloomed && player.CarriedFruit != null;
        }

        public void OnInteract(PipCharacterController player)
        {
            if (IsBloomed || player.CarriedFruit == null) return;
            StarberryItem berry = player.ReleaseCarriedFruit();
            player.PlayInteractAnimation();
            FeedBerry(berry, player);
        }

        public void FeedBerry(StarberryItem berry, PipCharacterController player)
        {
            if (IsBloomed || berry == null) return;
            IsBloomed = true;
            berry.ConsumeAndRespawnLater(6f);
            PocketPlanetAudio.PlaySfx("plant_eat_bloom", 0.95f);
            StartCoroutine(BloomSequence());
        }

        private IEnumerator BloomSequence()
        {
            // Chew / gulp squash & stretch
            float t = 0f;
            while (t < 0.55f)
            {
                t += Time.deltaTime;
                float chew = Mathf.Sin(t * 24f) * 0.18f;
                transform.localScale = new Vector3(_baseScale.x * (1f + chew), _baseScale.y * (1f - chew), _baseScale.z * (1f + chew));
                yield return null;
            }

            // Unfold petals outward into a wide glowing flower platform
            float dur = 0.75f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                transform.localScale = Vector3.Lerp(_baseScale, _baseScale * 1.18f, u);
                for (int i = 0; i < _petals.Count; i++)
                {
                    if (_petals[i] != null)
                        _petals[i].localRotation = _petalBaseRots[i] * Quaternion.Euler(Mathf.Lerp(28f, -45f, u), 0f, 0f);
                }
                yield return null;
            }

            if (bloomPlatform != null)
                bloomPlatform.gameObject.SetActive(true);

            if (rewardSeed != null)
                rewardSeed.RevealSeed(transform.position + Vector3.up * 2.15f);

            PocketPlanetGameManager.Instance?.RecordInteraction("Fed Hungry Bloom-Plant & Bloomed Flower Platform");
        }
    }
}
