using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class FriendlyAlienBloop : MonoBehaviour, IPocketInteractable
    {
        public PipCharacterController followTarget;
        public StarSeedCollectible rewardSeed;
        public float followStopDistance = 2.1f;
        public float followSpeed = 4.6f;
        public float teleportCatchupDistance = 16.0f;
        public bool IsBefriended { get; private set; }

        private Vector3 _baseScale;
        private float _hopPhase;
        private float _stuckTimer;
        private Vector3 _lastPos;
        private float _happySpinTimer;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _lastPos = transform.position;
            if (GetComponent<SphereCollider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 0.38f, 0f);
                sc.radius = 0.55f;
                sc.isTrigger = true;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_happySpinTimer > 0f)
            {
                _happySpinTimer -= dt;
                float hop = Mathf.Abs(Mathf.Sin((0.75f - _happySpinTimer) * 14f));
                transform.Rotate(0f, 540f * dt, 0f);
                transform.localScale = new Vector3(_baseScale.x * (1f - 0.12f * hop), _baseScale.y * (1f + 0.22f * hop), _baseScale.z * (1f - 0.12f * hop));
                return;
            }

            if (IsBefriended && followTarget != null)
            {
                Vector3 toPlayer = followTarget.transform.position - transform.position;
                Vector3 planar = new Vector3(toPlayer.x, 0f, toPlayer.z);
                float dist = planar.magnitude;

                // Anti-stuck recovery: if too far or blocked for > 2.2s, hop-warp behind player
                if (dist > teleportCatchupDistance)
                {
                    WarpNearPlayer();
                    return;
                }

                if (dist > followStopDistance)
                {
                    Vector3 moveDir = planar.normalized;
                    // Obstacle whisker avoidance so Bloop steers around rocks/trees smoothly
                    if (Physics.Raycast(transform.position + Vector3.up * 0.35f, moveDir, 1.1f))
                    {
                        Vector3 leftDir = Quaternion.Euler(0f, -45f, 0f) * moveDir;
                        Vector3 rightDir = Quaternion.Euler(0f, 45f, 0f) * moveDir;
                        moveDir = !Physics.Raycast(transform.position + Vector3.up * 0.35f, leftDir, 1.2f) ? leftDir : rightDir;
                    }

                    Vector3 nextPos = transform.position + moveDir * (followSpeed * dt);
                    // Snap Y to ground surface
                    if (Physics.Raycast(nextPos + Vector3.up * 3.0f, Vector3.down, out RaycastHit hit, 8.0f))
                    {
                        nextPos.y = Mathf.Lerp(transform.position.y, hit.point.y, dt * 14f);
                    }

                    transform.position = nextPos;
                    Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 10f);

                    _hopPhase += dt * 12f;
                    float hop = Mathf.Abs(Mathf.Sin(_hopPhase));
                    transform.localScale = new Vector3(
                        _baseScale.x * (1f - 0.08f * hop),
                        _baseScale.y * (1f + 0.16f * hop),
                        _baseScale.z * (1f - 0.08f * hop));

                    if (Vector3.Distance(transform.position, _lastPos) < 0.02f * dt)
                    {
                        _stuckTimer += dt;
                        if (_stuckTimer > 2.0f)
                        {
                            WarpNearPlayer();
                        }
                    }
                    else
                    {
                        _stuckTimer = 0f;
                    }
                    _lastPos = transform.position;
                }
                else
                {
                    // Gentle idle breathing while facing Pip
                    _stuckTimer = 0f;
                    if (planar.sqrMagnitude > 0.05f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar.normalized, Vector3.up), dt * 6f);
                    }
                    float breath = Mathf.Sin(Time.time * 3.2f) * 0.04f;
                    transform.localScale = new Vector3(_baseScale.x * (1f + breath), _baseScale.y * (1f - breath), _baseScale.z * (1f + breath));
                }
            }
        }

        private void WarpNearPlayer()
        {
            if (followTarget == null) return;
            _stuckTimer = 0f;
            Vector3 safePos = followTarget.transform.position - followTarget.transform.forward * 1.6f + Vector3.up * 0.4f;
            if (Physics.Raycast(safePos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f))
                safePos.y = hit.point.y;
            transform.position = safePos;
            _lastPos = safePos;
        }

        public string GetPromptText(PipCharacterController player)
        {
            return IsBefriended
                ? "Press [E] / [West] to Pet Bloop Again!"
                : "Press [E] / [West] to Pet Friendly Alien Bloop!";
        }

        public bool CanInteract(PipCharacterController player)
        {
            return true;
        }

        public void OnInteract(PipCharacterController player)
        {
            followTarget = player;
            player?.PlayInteractAnimation();
            _happySpinTimer = 0.75f;
            PocketPlanetAudio.PlaySfx("bloop_happy", 0.90f, UnityEngine.Random.Range(0.96f, 1.06f));

            if (!IsBefriended)
            {
                IsBefriended = true;
                if (rewardSeed != null)
                {
                    rewardSeed.RevealSeed(transform.position + Vector3.up * 1.5f + transform.forward * 1.1f);
                }
                PocketPlanetGameManager.Instance?.RecordInteraction("Petted & Befriended Bloop");
            }
        }
    }
}
