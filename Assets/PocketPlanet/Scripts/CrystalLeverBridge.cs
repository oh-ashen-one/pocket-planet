using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class CrystalLeverBridge : MonoBehaviour, IPocketInteractable
    {
        public Transform bridgeRoot;
        public bool IsActivated { get; private set; }

        private Transform _leverHandle;
        private Quaternion _handleBaseRot;
        private readonly List<Transform> _segments = new List<Transform>();
        private readonly List<Quaternion> _segBaseRots = new List<Quaternion>();
        private BoxCollider _bridgeWalkCollider;

        private void Awake()
        {
            _leverHandle = PipCharacterController.FindDeepChild(transform, "LeverHandle");
            if (_leverHandle != null)
            {
                _handleBaseRot = _leverHandle.localRotation;
            }

            if (GetComponent<SphereCollider>() == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 0.5f, 0f);
                sc.radius = 0.85f;
                sc.isTrigger = true;
            }

            if (bridgeRoot != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    Transform seg = PipCharacterController.FindDeepChild(bridgeRoot, $"BridgeSeg_{i}");
                    if (seg != null)
                    {
                        _segments.Add(seg);
                        Quaternion baseRot = seg.localRotation;
                        _segBaseRots.Add(baseRot);
                        // Start folded curl-up until lever is pulled
                        seg.localScale = new Vector3(1f, 0.08f, 1f);
                        seg.localRotation = baseRot * Quaternion.Euler(-75f + i * 8f, 0f, 0f);
                    }
                }

                _bridgeWalkCollider = bridgeRoot.GetComponent<BoxCollider>();
                if (_bridgeWalkCollider == null)
                    _bridgeWalkCollider = bridgeRoot.gameObject.AddComponent<BoxCollider>();
                _bridgeWalkCollider.center = new Vector3(0f, 0.0f, -3.1f);
                _bridgeWalkCollider.size = new Vector3(2.4f, 0.30f, 6.8f);
                _bridgeWalkCollider.enabled = false;
            }
        }

        public string GetPromptText(PipCharacterController player)
        {
            return IsActivated ? "" : "Press [E] / [West] to Pull Crystal Lever & Unfold Bridge";
        }

        public bool CanInteract(PipCharacterController player)
        {
            return !IsActivated;
        }

        public void OnInteract(PipCharacterController player)
        {
            if (IsActivated) return;
            IsActivated = true;
            player?.PlayInteractAnimation();
            PocketPlanetAudio.PlaySfx("lever_pull", 0.88f);
            StartCoroutine(UnfoldBridgeRoutine());
        }

        private IEnumerator UnfoldBridgeRoutine()
        {
            // Animate lever handle swing by +58 deg around its hinge
            float t = 0f;
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / 0.32f);
                if (_leverHandle != null)
                    _leverHandle.localRotation = _handleBaseRot * Quaternion.Euler(Mathf.Lerp(0f, 58f, u), 0f, 0f);
                yield return null;
            }

            PocketPlanetAudio.PlaySfx("bridge_unfold", 0.90f);

            // Unfold each bridge segment sequentially with playful spring overshoot
            for (int i = 0; i < _segments.Count; i++)
            {
                StartCoroutine(UnfoldSingleSegment(_segments[i], _segBaseRots[i], i * 0.11f));
            }

            yield return new WaitForSeconds(0.55f);
            if (_bridgeWalkCollider != null)
                _bridgeWalkCollider.enabled = true;

            PocketPlanetGameManager.Instance?.RecordInteraction("Pulled Crystal Lever & Unfolded Petal Bridge");
        }

        private IEnumerator UnfoldSingleSegment(Transform seg, Quaternion baseRot, float delay)
        {
            if (seg == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float dur = 0.38f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                float spring = 1f + Mathf.Sin(u * Mathf.PI) * 0.15f;
                seg.localScale = new Vector3(1f, Mathf.Lerp(0.08f, 1f, u) * spring, 1f);
                seg.localRotation = baseRot * Quaternion.Euler(Mathf.Lerp(-75f, 0f, u), 0f, 0f);
                yield return null;
            }
            seg.localScale = Vector3.one;
            seg.localRotation = baseRot;
        }
    }
}
