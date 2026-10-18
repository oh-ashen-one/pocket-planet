using UnityEngine;

namespace PocketPlanet
{
    public class SpaceWarpBoostRing : MonoBehaviour
    {
        public float spinSpeed = 65f;
        private float _cooldown;

        private void Update()
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            var flight = SpaceshipFlightController.Instance;
            if (_cooldown <= 0f && flight != null && flight.IsInFlight)
            {
                float dist = Vector3.Distance(flight.transform.position, transform.position);
                if (dist < 7.5f)
                {
                    _cooldown = 3.0f;
                    flight.TriggerWarpRingBoost();
                }
            }
        }
    }
}
