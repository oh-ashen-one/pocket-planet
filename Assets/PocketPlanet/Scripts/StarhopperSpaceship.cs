using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class StarhopperSpaceship : MonoBehaviour, IPocketInteractable
    {
        public bool IsRepaired { get; private set; }
        private Vector3 _basePos;
        private Vector3 _baseScale;
        private float _wobbleTime;

        private void Awake()
        {
            _basePos = transform.position;
            _baseScale = transform.localScale;
            if (GetComponent<SphereCollider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 1.2f, 0f);
                sc.radius = 2.15f;
                sc.isTrigger = true;
            }
        }

        private void Update()
        {
            _wobbleTime += Time.deltaTime;
            if (!IsRepaired)
            {
                // Subtle sputter tilt while broken
                float sputter = Mathf.Sin(_wobbleTime * 6.5f) * 1.2f;
                transform.localRotation = Quaternion.Euler(sputter, 18f, -4.5f);
            }
            else
            {
                // Happy hover bob and gentle spin when repaired!
                float hover = Mathf.Sin(_wobbleTime * 2.8f) * 0.24f;
                transform.position = _basePos + Vector3.up * (0.45f + hover);
                transform.Rotate(0f, 28f * Time.deltaTime, 0f, Space.World);
            }
        }

        public string GetPromptText(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            int seeds = gm != null ? gm.CollectedSeeds : 0;
            if (IsRepaired)
                return "SS Starhopper Repaired! Ready for Launch!";
            if (seeds >= 3)
                return "Press [E] / [West] to Repair SS Starhopper with 3 Star Seeds!";
            return $"SS Starhopper (Broken) — Needs {3 - seeds} more Star Seed(s) to repair";
        }

        public bool CanInteract(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            return !IsRepaired && gm != null && gm.CollectedSeeds >= 3;
        }

        public void OnInteract(PipCharacterController player)
        {
            if (IsRepaired) return;
            var gm = PocketPlanetGameManager.Instance;
            if (gm == null || gm.CollectedSeeds < 3) return;

            RepairSpaceship(player);
        }

        public void RepairSpaceship(PipCharacterController player)
        {
            if (IsRepaired) return;
            IsRepaired = true;
            PocketPlanetAudio.PlaySfx("ship_repair_fanfare", 1.0f);
            if (player != null)
                player.SetCelebrating(true);
            PocketPlanetGameManager.Instance?.OnShipRepaired();
        }
    }
}
