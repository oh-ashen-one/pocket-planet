using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketPlanet
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 0.88f, 0f);
        public float distance = 5.2f;
        public float minDistance = 1.25f;
        public float maxDistance = 9.0f;
        public float yaw = 0f;
        public float pitch = 19f;
        public float minPitch = -8f;
        public float maxPitch = 68f;
        public float mouseSensitivity = 0.14f;
        public float gamepadYawSpeed = 125f;
        public float gamepadPitchSpeed = 85f;
        public float followSmoothTime = 0.06f;
        public float collisionRadius = 0.24f;
        public LayerMask collisionMask = ~0;

        private Vector3 _smoothedPivot;
        private Vector3 _pivotVel;
        private float _currentDist;
        private bool _cursorLocked;
        private bool _externalControl;

        public void SetExternalControl(bool enabled)
        {
            _externalControl = enabled;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            _smoothedPivot = target.position + targetOffset;
            _pivotVel = Vector3.zero;
            _currentDist = distance;
            Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredDir = orbitRot * Vector3.back;
            transform.position = _smoothedPivot + desiredDir * _currentDist;
            transform.rotation = Quaternion.LookRotation(_smoothedPivot - transform.position, Vector3.up);
        }

        private void Start()
        {
            _currentDist = distance;
            if (target != null)
                _smoothedPivot = target.position + targetOffset;
        }

        private void LateUpdate()
        {
            if (_externalControl || target == null)
                return;

            HandleInput();

            Vector3 desiredPivot = target.position + targetOffset;
            _smoothedPivot = Vector3.SmoothDamp(_smoothedPivot, desiredPivot, ref _pivotVel, followSmoothTime);

            Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredDir = orbitRot * Vector3.back;

            float targetDist = distance;
            if (Physics.SphereCast(
                _smoothedPivot,
                collisionRadius,
                desiredDir,
                out RaycastHit hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(target))
                {
                    targetDist = Mathf.Clamp(hit.distance - 0.12f, minDistance, distance);
                }
            }

            _currentDist = Mathf.Lerp(_currentDist, targetDist, Time.deltaTime * 16f);
            transform.position = _smoothedPivot + desiredDir * _currentDist;
            transform.rotation = Quaternion.LookRotation(_smoothedPivot - transform.position, Vector3.up);
        }

        private void HandleInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                _cursorLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                _cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            bool rotateMouse = _cursorLocked || (mouse != null && (mouse.rightButton.isPressed || mouse.leftButton.isPressed));
            if (mouse != null && rotateMouse)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distance = Mathf.Clamp(distance - scroll * 0.003f, minDistance, maxDistance);
                }
            }

            if (pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.02f)
                {
                    yaw += rs.x * gamepadYawSpeed * Time.deltaTime;
                    pitch = Mathf.Clamp(pitch - rs.y * gamepadPitchSpeed * Time.deltaTime, minPitch, maxPitch);
                }
            }

            if (kb != null)
            {
                if (kb.qKey.isPressed) yaw -= 85f * Time.deltaTime;
                if (kb.eKey.isPressed) yaw += 85f * Time.deltaTime;
            }
        }
    }
}
