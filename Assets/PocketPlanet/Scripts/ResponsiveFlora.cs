using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class ResponsiveFlora : MonoBehaviour
    {
        private Quaternion _baseRot;
        private Vector3 _baseScale;
        private Vector3 _tiltAxis = Vector3.right;
        private float _bendAmount;
        private float _bendVel;
        private float _soundCooldown;

        private void Awake()
        {
            _baseRot = transform.localRotation;
            _baseScale = transform.localScale;
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.center = new Vector3(0f, 0.35f, 0f);
                sc.radius = 0.68f;
            }
        }

        private void Update()
        {
            if (_soundCooldown > 0f) _soundCooldown -= Time.deltaTime;
            _bendAmount = Mathf.SmoothDamp(_bendAmount, 0f, ref _bendVel, 0.16f);
            float wind = Mathf.Sin(Time.time * 2.2f + transform.position.x * 0.5f) * 3.5f;
            transform.localRotation = _baseRot * Quaternion.AngleAxis(_bendAmount + wind, _tiltAxis);
        }

        private void OnTriggerEnter(Collider other)
        {
            PipCharacterController pip = other.GetComponentInParent<PipCharacterController>();
            FriendlyAlienBloop bloop = other.GetComponentInParent<FriendlyAlienBloop>();
            if (pip != null || bloop != null)
            {
                Vector3 away = (transform.position - other.transform.position).normalized;
                _tiltAxis = Vector3.Cross(Vector3.up, away).normalized;
                if (_tiltAxis.sqrMagnitude < 0.01f) _tiltAxis = Vector3.right;
                _bendAmount = 32f;
                if (pip != null && _soundCooldown <= 0f)
                {
                    _soundCooldown = 0.35f;
                    PocketPlanetAudio.PlaySfx("grass_rustle", 0.50f, UnityEngine.Random.Range(0.92f, 1.12f));
                    PocketPlanetGameManager.Instance?.RecordInteraction("Brushed Through Responsive Glowing Flora");
                }
            }
        }
    }
}
