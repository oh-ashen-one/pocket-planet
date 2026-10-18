using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class PushableBoulder : MonoBehaviour
    {
        public float outOfBoundsY = -8.0f;
        public List<DominoBlock> linkedDominoes = new List<DominoBlock>();
        public StarSeedCollectible rewardSeed;

        private Rigidbody _rb;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;
        private float _rollSoundCooldown;
        private bool _seedRevealed;

        private void Awake()
        {
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;

            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = gameObject.AddComponent<SphereCollider>();
                sc.center = Vector3.zero;
                sc.radius = 0.75f;
            }

            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
                _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 4.5f;
            _rb.linearDamping = 0.22f;
            _rb.angularDamping = 0.25f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Update()
        {
            if (_rollSoundCooldown > 0f)
                _rollSoundCooldown -= Time.deltaTime;

            if (_rb.linearVelocity.magnitude > 1.8f && _rollSoundCooldown <= 0f)
            {
                _rollSoundCooldown = 0.42f;
                PocketPlanetAudio.PlaySfx("boulder_roll", 0.55f, UnityEngine.Random.Range(0.9f, 1.1f));
            }

            if (transform.position.y < outOfBoundsY)
            {
                RespawnBoulder();
            }

            if (!_seedRevealed && linkedDominoes.Count > 0)
            {
                int knocked = 0;
                foreach (DominoBlock d in linkedDominoes)
                {
                    if (d != null && d.IsToppled)
                        knocked++;
                }
                if (knocked >= Mathf.Max(1, linkedDominoes.Count / 2))
                {
                    _seedRevealed = true;
                    if (rewardSeed != null)
                        rewardSeed.RevealSeed(rewardSeed.transform.position);
                    PocketPlanetGameManager.Instance?.RecordInteraction("Rolled Boulder & Toppled Domino Route");
                }
            }
        }

        public void ApplyPlayerPush(Vector3 pushDir, float playerSpeed)
        {
            _rb.AddForce(pushDir * (playerSpeed * 4.2f), ForceMode.Impulse);
            if (_rollSoundCooldown <= 0f)
            {
                _rollSoundCooldown = 0.35f;
                PocketPlanetAudio.PlaySfx("boulder_roll", 0.75f);
            }
        }

        public void RespawnBoulder()
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_spawnPos + Vector3.up * 0.2f, _spawnRot);
        }
    }
}
