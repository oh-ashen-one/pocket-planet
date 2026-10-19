using System.Collections;
using UnityEngine;

namespace PocketPlanet
{
    public class ObsidianShadowBarrier : MonoBehaviour
    {
        public Transform barrierDome;
        public Transform[] sealRings;
        public Collider barrierCollider;
        public ObsidianCrystalPylon[] linkedPylons;

        private bool _isDissolved;
        private int _attunedCount;

        public bool IsDissolved => _isDissolved;
        public int AttunedCount => _attunedCount;

        private void Update()
        {
            if (_isDissolved)
                return;

            float t = Time.time;
            if (barrierDome != null)
            {
                barrierDome.Rotate(0f, 24f * Time.deltaTime, 0f, Space.Self);
                float pulse = 1.0f + Mathf.Sin(t * 2.4f) * 0.03f;
                barrierDome.localScale = new Vector3(4.6f * pulse, 3.8f * pulse, 4.6f * pulse);
            }

            if (sealRings != null)
            {
                for (int i = 0; i < sealRings.Length; i++)
                {
                    if (sealRings[i] != null && sealRings[i].gameObject.activeSelf)
                    {
                        float dir = (i % 2 == 0) ? 1f : -1f;
                        sealRings[i].Rotate(18f * dir * Time.deltaTime, 52f * dir * Time.deltaTime, 12f * Time.deltaTime, Space.Self);
                    }
                }
            }
        }

        public void NotifyPylonAttuned(int pylonIdx)
        {
            if (_isDissolved)
                return;

            if (sealRings != null && pylonIdx >= 0 && pylonIdx < sealRings.Length && sealRings[pylonIdx] != null)
            {
                sealRings[pylonIdx].gameObject.SetActive(false);
            }

            _attunedCount = 0;
            if (linkedPylons != null)
            {
                for (int i = 0; i < linkedPylons.Length; i++)
                {
                    if (linkedPylons[i] != null && linkedPylons[i].IsAttuned)
                        _attunedCount++;
                }
            }
            else
            {
                _attunedCount++;
            }

            int required = (linkedPylons != null && linkedPylons.Length > 0) ? linkedPylons.Length : 3;
            if (_attunedCount >= required)
            {
                DissolveBarrier();
            }
        }

        public void DissolveBarrier()
        {
            if (_isDissolved)
                return;

            _isDissolved = true;
            if (barrierCollider != null)
                barrierCollider.enabled = false;

            PocketPlanetAudio.PlaySfx("barrier_dissolve", 0.95f, 1.0f);
            StartCoroutine(AnimateDissolveRoutine());
        }

        private IEnumerator AnimateDissolveRoutine()
        {
            if (linkedPylons != null)
            {
                foreach (var p in linkedPylons)
                {
                    if (p != null && p.tetherBeam != null)
                        p.tetherBeam.enabled = false;
                }
            }

            Vector3 startScale = barrierDome != null ? barrierDome.localScale : Vector3.one * 4.5f;
            float elapsed = 0f;
            while (elapsed < 0.75f)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / 0.75f);
                if (barrierDome != null)
                {
                    float expandThenShrink = u < 0.25f
                        ? Mathf.Lerp(1.0f, 1.22f, u / 0.25f)
                        : Mathf.Lerp(1.22f, 0.01f, (u - 0.25f) / 0.75f);
                    barrierDome.localScale = startScale * expandThenShrink;
                    barrierDome.Rotate(0f, 260f * Time.deltaTime, 0f, Space.Self);
                }
                yield return null;
            }

            if (barrierDome != null)
                barrierDome.gameObject.SetActive(false);
            if (sealRings != null)
            {
                foreach (var sr in sealRings)
                {
                    if (sr != null)
                        sr.gameObject.SetActive(false);
                }
            }
        }
    }
}
