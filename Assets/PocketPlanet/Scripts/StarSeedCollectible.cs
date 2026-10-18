using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class StarSeedCollectible : MonoBehaviour
    {
        public int seedIndex = 1;
        public string seedTitle = "Bloom Garden Star Seed";
        public bool startHidden = false;
        public bool IsCollected { get; private set; }
        public bool IsRevealed { get; private set; }

        private Vector3 _basePos;

        private void Awake()
        {
            _basePos = transform.position;
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.center = new Vector3(0f, 0.45f, 0f);
                sc.radius = 0.75f;
            }
        }

        private void Start()
        {
            if (startHidden)
            {
                IsRevealed = false;
                gameObject.SetActive(false);
            }
            else
            {
                IsRevealed = true;
            }
        }

        private void Update()
        {
            if (IsCollected) return;
            transform.Rotate(0f, 95f * Time.deltaTime, 0f, Space.World);
            float bob = Mathf.Sin(Time.time * 3.2f + seedIndex) * 0.14f;
            transform.position = _basePos + Vector3.up * bob;
        }

        public void RevealSeed(Vector3 worldPos)
        {
            _basePos = worldPos;
            transform.position = worldPos;
            IsRevealed = true;
            gameObject.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsCollected) return;
            PipCharacterController pip = other.GetComponentInParent<PipCharacterController>();
            if (pip != null)
            {
                Collect(pip);
            }
        }

        public void Collect(PipCharacterController pip)
        {
            if (IsCollected) return;
            IsCollected = true;
            PocketPlanetAudio.PlaySfx("star_seed_collect", 0.95f);
            pip?.TriggerSquash(new Vector3(0.85f, 1.22f, 0.85f));
            PocketPlanetGameManager.Instance?.OnStarSeedCollected(this);
            gameObject.SetActive(false);
        }
    }
}
