using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class BouncyMushroom : MonoBehaviour
    {
        public float launchSpeed = 14.8f;
        private Vector3 _baseScale;
        private float _recoilTimer;
        private float _cooldown;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (GetComponent<Collider>() == null)
            {
                CylinderColliderSetup();
            }
        }

        private void CylinderColliderSetup()
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.50f, 0f);
            bc.size = new Vector3(1.65f, 1.00f, 1.65f);
        }

        private void Update()
        {
            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            if (_recoilTimer > 0f)
            {
                _recoilTimer -= Time.deltaTime;
                float elapsed = 0.65f - _recoilTimer;
                // Damped spring oscillation for squash -> stretch recoil -> settle
                float wave = Mathf.Sin(elapsed * 26f) * Mathf.Exp(-elapsed * 5.5f);
                float sx = 1f + wave * 0.34f;
                float sy = 1f - wave * 0.38f;
                transform.localScale = new Vector3(_baseScale.x * sx, _baseScale.y * sy, _baseScale.z * sx);
            }
            else
            {
                transform.localScale = _baseScale;
            }

            // Detect player landing or stepping onto the mushroom cap
            if (_cooldown <= 0f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.92f, 0.95f);
                foreach (Collider c in hits)
                {
                    PipCharacterController pip = c.GetComponentInParent<PipCharacterController>();
                    if (pip != null && pip.transform.position.y >= transform.position.y + 0.45f)
                    {
                        TriggerBounce(pip);
                        break;
                    }
                }
            }
        }

        public void TriggerBounce(PipCharacterController pip)
        {
            _cooldown = 0.28f;
            _recoilTimer = 0.65f;
            pip.BounceFromMushroom(launchSpeed);
            PocketPlanetGameManager.Instance?.RecordInteraction("Bounced on Jelly-Shroom");
        }
    }
}
