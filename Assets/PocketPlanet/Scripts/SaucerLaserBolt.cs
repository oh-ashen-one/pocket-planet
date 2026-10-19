using UnityEngine;

namespace PocketPlanet
{
    public class SaucerLaserBolt : MonoBehaviour
    {
        public Vector3 velocity;
        public float maxLifetime = 1.6f;
        public SpaceshipFlightController ownerSaucer;
        public DestructibleSpaceAsteroid homingTarget;

        private float _age;

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (homingTarget != null && !homingTarget.IsShattered)
            {
                Vector3 toTarget = (homingTarget.transform.position - transform.position).normalized;
                velocity = Vector3.Slerp(velocity.normalized, toTarget, Time.deltaTime * 10f) * velocity.magnitude;
            }

            transform.position += velocity * Time.deltaTime;
            if (velocity.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            }

            var asteroids = DestructibleSpaceAsteroid.ActiveAsteroids;
            for (int i = asteroids.Count - 1; i >= 0; i--)
            {
                var ast = asteroids[i];
                if (ast == null || ast.IsShattered)
                    continue;

                float dist = Vector3.Distance(transform.position, ast.transform.position);
                if (dist <= ast.hitRadius)
                {
                    ast.ShatterAsteroid(ownerSaucer);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
