using System.Collections.Generic;
using UnityEngine;

namespace PocketPlanet
{
    public class PocketPlanetAudio : MonoBehaviour
    {
        public static PocketPlanetAudio Instance { get; private set; }

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioSource _loopSfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.volume = 0.28f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume = 0.72f;

            _loopSfxSource = gameObject.AddComponent<AudioSource>();
            _loopSfxSource.loop = true;
            _loopSfxSource.spatialBlend = 0f;
            _loopSfxSource.volume = 0f;

            LoadAllClips();
        }

        private void Start()
        {
            if (_clips.TryGetValue("bgm_pocket_planet", out AudioClip bgm) && bgm != null)
            {
                _bgmSource.clip = bgm;
                _bgmSource.Play();
            }
        }

        private void LoadAllClips()
        {
            AudioClip[] loaded = Resources.LoadAll<AudioClip>("Audio");
            if (loaded == null) return;
            foreach (AudioClip c in loaded)
            {
                if (c != null)
                    _clips[c.name] = c;
            }
        }

        public static void PlaySfx(string clipName, float volumeScale = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            if (Instance._clips.TryGetValue(clipName, out AudioClip clip) && clip != null)
            {
                Instance._sfxSource.pitch = pitch;
                Instance._sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            }
        }

        public static void PlayFootstep(bool inShallowWater)
        {
            if (Instance == null) return;
            if (inShallowWater)
            {
                int idx = Random.Range(0, 2);
                PlaySfx($"footstep_water_{idx}", 0.65f, Random.Range(0.94f, 1.08f));
            }
            else
            {
                int idx = Random.Range(0, 3);
                PlaySfx($"footstep_grass_{idx}", 0.50f, Random.Range(0.93f, 1.07f));
            }
        }
    }
}
