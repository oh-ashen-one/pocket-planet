using UnityEngine;

namespace PocketPlanet
{
    public class ObsidianBogHazardZone : MonoBehaviour
    {
        public Vector3 safeRespawnPosition = new Vector3(0f, 0.35f, 892.5f);
        public Transform[] mistRipples;

        private float _lastRespawnTime = -5f;

        private void Update()
        {
            float t = Time.time;
            if (mistRipples != null)
            {
                for (int i = 0; i < mistRipples.Length; i++)
                {
                    if (mistRipples[i] != null)
                    {
                        float phase = (t * 0.45f + i * 0.33f) % 1.0f;
                        float s = Mathf.Lerp(1.2f, 5.2f, phase);
                        mistRipples[i].localScale = new Vector3(s, 0.03f, s);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PipCharacterController>();
            if (player != null && Time.time - _lastRespawnTime > 0.8f)
            {
                _lastRespawnTime = Time.time;
                PocketPlanetAudio.PlaySfx("footstep_water_0", 0.85f, 0.78f);
                player.TeleportTo(safeRespawnPosition, Quaternion.Euler(0f, 0f, 0f));
            }
        }
    }
}
