using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class DominoBlock : MonoBehaviour
    {
        public bool IsToppled { get; private set; }
        private Rigidbody _rb;
        private Vector3 _initialUp;
        private float _soundCooldown;

        private void Awake()
        {
            _initialUp = transform.up;
            BoxCollider bc = GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = gameObject.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 0.85f, 0f);
                bc.size = new Vector3(0.90f, 1.70f, 0.32f);
            }

            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
                _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 1.1f;
            _rb.linearDamping = 0.15f;
            _rb.angularDamping = 0.20f;
        }

        private void Update()
        {
            if (_soundCooldown > 0f) _soundCooldown -= Time.deltaTime;
            if (!IsToppled && Vector3.Angle(transform.up, _initialUp) > 32f)
            {
                IsToppled = true;
                if (_soundCooldown <= 0f)
                {
                    _soundCooldown = 0.25f;
                    PocketPlanetAudio.PlaySfx("block_topple", 0.75f, UnityEngine.Random.Range(0.92f, 1.08f));
                }
            }
        }

        public void ForceTopple(Vector3 impulse)
        {
            if (_rb != null)
                _rb.AddForceAtPosition(impulse, transform.position + Vector3.up * 1.4f, ForceMode.Impulse);
        }
    }
}
