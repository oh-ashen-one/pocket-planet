using UnityEngine;

namespace PocketPlanet
{
    public class QuestWaypointCompass : MonoBehaviour
    {
        private Transform _arrowRoot;
        private Transform _beaconRingRoot;
        private Material _goldBeaconMat;
        private Material _cyanBeaconMat;

        private void Start()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _goldBeaconMat = new Material(sh);
            Color gold = new Color(1.0f, 0.88f, 0.32f, 1f);
            if (_goldBeaconMat.HasProperty("_BaseColor")) _goldBeaconMat.SetColor("_BaseColor", gold);
            if (_goldBeaconMat.HasProperty("_Color")) _goldBeaconMat.SetColor("_Color", gold);
            if (_goldBeaconMat.HasProperty("_EmissionColor"))
            {
                _goldBeaconMat.EnableKeyword("_EMISSION");
                _goldBeaconMat.SetColor("_EmissionColor", new Color(0.95f, 0.72f, 0.22f) * 1.35f);
            }

            _cyanBeaconMat = new Material(sh);
            Color cyan = new Color(0.28f, 0.95f, 1.0f, 1f);
            if (_cyanBeaconMat.HasProperty("_BaseColor")) _cyanBeaconMat.SetColor("_BaseColor", cyan);
            if (_cyanBeaconMat.HasProperty("_Color")) _cyanBeaconMat.SetColor("_Color", cyan);
            if (_cyanBeaconMat.HasProperty("_EmissionColor"))
            {
                _cyanBeaconMat.EnableKeyword("_EMISSION");
                _cyanBeaconMat.SetColor("_EmissionColor", new Color(0.22f, 0.85f, 1.0f) * 1.35f);
            }

            // Build subtle 3D directional chevron arrow near Pip
            _arrowRoot = new GameObject("StarlightCompassArrow").transform;
            _arrowRoot.SetParent(transform, false);

            GameObject pointerTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointerTip.name = "CompassChevronTip";
            pointerTip.transform.SetParent(_arrowRoot, false);
            pointerTip.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            pointerTip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            pointerTip.transform.localScale = new Vector3(0.18f, 0.045f, 0.18f);
            Destroy(pointerTip.GetComponent<Collider>());
            pointerTip.GetComponent<MeshRenderer>().sharedMaterial = _goldBeaconMat;

            GameObject pointerTail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointerTail.name = "CompassChevronTail";
            pointerTail.transform.SetParent(_arrowRoot, false);
            pointerTail.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            pointerTail.transform.localScale = new Vector3(0.09f, 0.04f, 0.22f);
            Destroy(pointerTail.GetComponent<Collider>());
            pointerTail.GetComponent<MeshRenderer>().sharedMaterial = _cyanBeaconMat;

            // Build 3D target beacon ring that hovers over the active quest objective
            _beaconRingRoot = new GameObject("ActiveObjectiveBeaconRing").transform;
            _beaconRingRoot.SetParent(transform, false);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI * 2f / 8f;
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"BeaconSeg_{i}";
                seg.transform.SetParent(_beaconRingRoot, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.72f, 0f, Mathf.Sin(ang) * 0.72f);
                seg.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                seg.transform.localScale = new Vector3(0.11f, 0.07f, 0.34f);
                Destroy(seg.GetComponent<Collider>());
                seg.GetComponent<MeshRenderer>().sharedMaterial = (i % 2 == 0) ? _goldBeaconMat : _cyanBeaconMat;
            }
        }

        private void LateUpdate()
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm == null || gm.player == null || gm.ShrineDelivered || gm.IsFlyingSaucerActive)
            {
                if (_arrowRoot != null) _arrowRoot.gameObject.SetActive(false);
                if (_beaconRingRoot != null) _beaconRingRoot.gameObject.SetActive(false);
                return;
            }

            Transform targetTransform = gm.GetActiveQuestTargetTransform(out string _);
            if (targetTransform == null)
            {
                if (_arrowRoot != null) _arrowRoot.gameObject.SetActive(false);
                if (_beaconRingRoot != null) _beaconRingRoot.gameObject.SetActive(false);
                return;
            }

            Vector3 playerPos = gm.player.transform.position;
            Vector3 targetPos = targetTransform.position;
            Vector3 flatToTarget = targetPos - playerPos;
            flatToTarget.y = 0f;
            float flatDist = flatToTarget.magnitude;

            float t = Time.time;

            if (_arrowRoot != null)
            {
                bool showArrow = flatDist > 3.6f;
                _arrowRoot.gameObject.SetActive(showArrow);
                if (showArrow)
                {
                    Vector3 dir = flatToTarget.normalized;
                    _arrowRoot.position = playerPos + Vector3.up * (0.18f + Mathf.Sin(t * 3.5f) * 0.03f) + dir * 1.05f;
                    _arrowRoot.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
            }

            if (_beaconRingRoot != null)
            {
                _beaconRingRoot.gameObject.SetActive(true);
                _beaconRingRoot.position = targetPos + Vector3.up * (2.65f + Mathf.Sin(t * 3.0f) * 0.16f);
                _beaconRingRoot.Rotate(0f, 75f * Time.deltaTime, 0f, Space.Self);
            }
        }
    }
}
