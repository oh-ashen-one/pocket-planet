using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class StarberryItem : MonoBehaviour, IPocketInteractable
    {
        public float outOfBoundsY = -8.0f;
        private Rigidbody _rb;
        private SphereCollider _col;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;
        private Transform _carrierSocket;
        private bool _consumed;

        public bool IsCarried => _carrierSocket != null;
        public bool IsConsumed => _consumed;

        private void Awake()
        {
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;

            _col = GetComponent<SphereCollider>();
            if (_col == null)
            {
                _col = gameObject.AddComponent<SphereCollider>();
                _col.radius = 0.34f;
                _col.center = new Vector3(0f, 0.34f, 0f);
            }

            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
                _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 1.4f;
            _rb.linearDamping = 0.45f;
            _rb.angularDamping = 0.85f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Update()
        {
            if (_consumed) return;

            if (_carrierSocket != null)
            {
                transform.position = _carrierSocket.position - Vector3.up * 0.26f;
                transform.rotation = _carrierSocket.rotation;
                return;
            }

            if (transform.position.y < outOfBoundsY)
            {
                RespawnToOrigin();
            }
        }

        public string GetPromptText(PipCharacterController player)
        {
            return "Press [E] / [West] to Pick Up Starberry";
        }

        public bool CanInteract(PipCharacterController player)
        {
            return !_consumed && !IsCarried && player.CarriedFruit == null;
        }

        public void OnInteract(PipCharacterController player)
        {
            if (!CanInteract(player)) return;
            player.PickupFruit(this);
        }

        public void AttachToCarrier(Transform socket)
        {
            _carrierSocket = socket;
            _rb.isKinematic = true;
            _col.enabled = false;
        }

        public void ReleaseAndThrow(Vector3 velocity)
        {
            _carrierSocket = null;
            _col.enabled = true;
            _rb.isKinematic = false;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f));
        }

        public void ConsumeAndRespawnLater(float delaySeconds = 4.0f)
        {
            _carrierSocket = null;
            _consumed = true;
            _rb.isKinematic = true;
            _col.enabled = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            StartCoroutine(RespawnRoutine(delaySeconds));
        }

        private IEnumerator RespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            RespawnToOrigin();
        }

        public void RespawnToOrigin()
        {
            _consumed = false;
            _carrierSocket = null;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
            _col.enabled = true;
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_spawnPos + Vector3.up * 0.25f, _spawnRot);
        }
    }
}
