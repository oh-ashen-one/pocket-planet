using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketPlanet
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 0.88f, 0f);
        public float distance = 5.2f;
        public float minDistance = 2.6f;
        public float maxDistance = 9.0f;
        public float yaw = 0f;
        public float pitch = 19f;
        public float minPitch = -8f;
        public float maxPitch = 68f;
        public float mouseSensitivity = 0.14f;
        public float gamepadYawSpeed = 125f;
        public float gamepadPitchSpeed = 85f;
        public float followSmoothTime = 0.03f;
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
                if (!hit.transform.IsChildOf(target) && hit.collider is TerrainCollider)
                {
                    targetDist = Mathf.Clamp(hit.distance - 0.15f, minDistance, distance);
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

            bool escPressed = Input.GetKeyDown(KeyCode.Escape) || (kb != null && kb.escapeKey.wasPressedThisFrame);
            bool rightClickDown = Input.GetMouseButtonDown(1) || (mouse != null && mouse.rightButton.wasPressedThisFrame);

            if (escPressed)
            {
                _cursorLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (rightClickDown)
            {
                _cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            bool rotateMouse = _cursorLocked
                || Input.GetMouseButton(0)
                || Input.GetMouseButton(1)
                || (mouse != null && (mouse.rightButton.isPressed || mouse.leftButton.isPressed));

            if (rotateMouse)
            {
                Vector2 delta = Vector2.zero;
                if (mouse != null)
                    delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude < 0.0001f)
                    delta = new Vector2(Input.GetAxisRaw("Mouse X") * 12f, Input.GetAxisRaw("Mouse Y") * 12f);

                yaw += delta.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);
            }

            float scroll = 0f;
            if (mouse != null)
                scroll = mouse.scroll.ReadValue().y * 0.003f;
            if (Mathf.Abs(scroll) < 0.0001f)
                scroll = Input.mouseScrollDelta.y * 0.25f;

            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance = Mathf.Clamp(distance - scroll, minDistance, maxDistance);
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

            bool orbitLeft = Input.GetKey(KeyCode.Q) || (kb != null && kb.qKey.isPressed);
            bool orbitRight = Input.GetKey(KeyCode.E) || (kb != null && kb.eKey.isPressed);
            if (orbitLeft) yaw -= 85f * Time.deltaTime;
            if (orbitRight) yaw += 85f * Time.deltaTime;
        }
    }
}
