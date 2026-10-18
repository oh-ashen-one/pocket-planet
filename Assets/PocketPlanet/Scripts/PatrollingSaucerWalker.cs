using UnityEngine;

namespace PocketPlanet
{
    public class PatrollingSaucerWalker : MonoBehaviour
    {
        public Vector3 orbitCenter = new Vector3(0f, 24f, 0f);
        public float orbitRadiusX = 38f;
        public float orbitRadiusZ = 28f;
        public float angularSpeed = 0.38f;
        public float phaseOffset = 0f;
        public float spinSpeed = 110f;
        public bool figureEight = false;

        private void Update()
        {
            float t = Time.time * angularSpeed + phaseOffset;
            float x = Mathf.Cos(t) * orbitRadiusX;
            float z = figureEight ? Mathf.Sin(t * 2f) * orbitRadiusZ * 0.65f : Mathf.Sin(t) * orbitRadiusZ;
            float y = Mathf.Sin(t * 2.3f) * 1.8f;

            Vector3 nextPos = orbitCenter + new Vector3(x, y, z);
            Vector3 vel = nextPos - transform.position;
            transform.position = nextPos;

            float bankZ = Mathf.Clamp(-vel.x * 18f, -18f, 18f);
            float pitchX = Mathf.Clamp(vel.z * 12f, -12f, 12f);
            float yaw = (Time.time * spinSpeed + phaseOffset * 57.3f) % 360f;
            transform.rotation = Quaternion.Euler(pitchX, yaw, bankZ);
        }
    }
}
