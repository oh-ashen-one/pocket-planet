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
        public float mouseSensitivity = 0.22f;
        public float gamepadYawSpeed = 125f;
        public float gamepadPitchSpeed = 85f;
        public float followSmoothTime = 0.03f;
        public float collisionRadius = 0.24f;
        public LayerMask collisionMask = ~0;

        private Vector3 _smoothedPivot;
        private Vector3 _pivotVel;
        private float _currentDist;
        private bool _mouseLookPaused;
        private bool _externalControl;
        private Vector2 _lastMousePos;
        private bool _hasLastMousePos;
        private int _lockTransitionFrames;

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

            _lastMousePos = ReadScreenMousePos();
            _hasLastMousePos = true;
            _mouseLookPaused = false;
        }

        private static Vector2 ReadScreenMousePos()
        {
            Vector2 legacyPos = (Vector2)Input.mousePosition;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 newPos = mouse.position.ReadValue();
                if (newPos.sqrMagnitude > 0.01f)
                    return newPos;
            }
            return legacyPos;
        }

        public static Vector2 ReadCombinedMouseDelta(ref Vector2 lastScreenPos, ref bool hasLastPos, int skipPosDiffFrames = 0)
        {
            Vector2 bestDelta = Vector2.zero;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 sysDelta = mouse.delta.ReadValue();
                if (sysDelta.sqrMagnitude > bestDelta.sqrMagnitude)
                    bestDelta = sysDelta;
            }

            Vector2 axisDelta = new Vector2(Input.GetAxisRaw("Mouse X") * 16f, Input.GetAxisRaw("Mouse Y") * 16f);
            if (axisDelta.sqrMagnitude > bestDelta.sqrMagnitude)
            {
                bestDelta = axisDelta;
            }

            Vector2 curPos = ReadScreenMousePos();
            if (hasLastPos && skipPosDiffFrames <= 0)
            {
                Vector2 posDiff = curPos - lastScreenPos;
                if (posDiff.magnitude < 200f && posDiff.sqrMagnitude > bestDelta.sqrMagnitude)
                {
                    bestDelta = posDiff;
                }
            }
            lastScreenPos = curPos;
            hasLastPos = true;
            return bestDelta;
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

            if (_lockTransitionFrames > 0)
                _lockTransitionFrames--;

            var gm = PocketPlanetGameManager.Instance;
            bool victoryModalActive = gm != null && (gm.ShrineDelivered || gm.ShipRepaired);

            if (victoryModalActive)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    _lockTransitionFrames = 2;
                }
            }
            else
            {
                bool escPressed = Input.GetKeyDown(KeyCode.Escape) || (kb != null && kb.escapeKey.wasPressedThisFrame);
                bool anyMouseClick = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                    || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));

                if (escPressed)
                {
                    _mouseLookPaused = !_mouseLookPaused;
                    Cursor.lockState = _mouseLookPaused ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = _mouseLookPaused;
                    _lockTransitionFrames = 2;
                }
                else if (anyMouseClick && (_mouseLookPaused || Cursor.lockState != CursorLockMode.Locked))
                {
                    _mouseLookPaused = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    _lockTransitionFrames = 2;
                }
            }

            Vector2 delta = ReadCombinedMouseDelta(ref _lastMousePos, ref _hasLastMousePos, _lockTransitionFrames);
            if (!_mouseLookPaused && delta.sqrMagnitude > 0.0001f)
            {
                float sens = Mathf.Max(mouseSensitivity, 0.25f);
                yaw += delta.x * sens;
                pitch = Mathf.Clamp(pitch - delta.y * sens, minPitch, maxPitch);
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

            bool orbitLeft = Input.GetKey(KeyCode.LeftBracket) || (kb != null && kb.leftBracketKey.isPressed);
            bool orbitRight = Input.GetKey(KeyCode.RightBracket) || (kb != null && kb.rightBracketKey.isPressed);
            if (orbitLeft) yaw -= 95f * Time.deltaTime;
            if (orbitRight) yaw += 95f * Time.deltaTime;
        }
    }
}
