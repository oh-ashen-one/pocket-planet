using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPlanet
{
    public class ShallowWaterZone : MonoBehaviour
    {
        private float _rippleTimer;

        private void OnTriggerEnter(Collider other)
        {
            PipCharacterController pip = other.GetComponentInParent<PipCharacterController>();
            if (pip != null)
            {
                pip.InShallowWater = true;
                PocketPlanetAudio.PlayFootstep(true);
                SpawnSplashRing(pip.transform.position);
                PocketPlanetGameManager.Instance?.RecordInteraction("Splashed in Sparkling Shallow Pond");
            }
        }

        private void OnTriggerStay(Collider other)
        {
            PipCharacterController pip = other.GetComponentInParent<PipCharacterController>();
            if (pip != null)
            {
                pip.InShallowWater = true;
                if (pip.PlanarVelocity.magnitude > 0.6f)
                {
                    _rippleTimer -= Time.deltaTime;
                    if (_rippleTimer <= 0f)
                    {
                        _rippleTimer = 0.26f;
                        SpawnSplashRing(pip.transform.position);
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PipCharacterController pip = other.GetComponentInParent<PipCharacterController>();
            if (pip != null)
            {
                pip.InShallowWater = false;
            }
        }

        private void SpawnSplashRing(Vector3 worldPos)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "WaterRippleRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(worldPos.x, transform.position.y + 0.04f, worldPos.z);
            ring.transform.localScale = new Vector3(0.25f, 0.008f, 0.25f);
            Renderer r = ring.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material m = new Material(s);
                Color cyan = new Color(0.75f, 0.97f, 1.0f, 1f);
                m.color = cyan;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cyan);
                r.sharedMaterial = m;
            }
            ring.AddComponent<WaterRippleAnimator>();
        }
    }
}
