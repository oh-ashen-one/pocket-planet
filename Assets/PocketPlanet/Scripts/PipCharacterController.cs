using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketPlanet
{
    public interface IPocketInteractable
    {
        string GetPromptText(PipCharacterController player);
        bool CanInteract(PipCharacterController player);
        void OnInteract(PipCharacterController player);
    }

    [RequireComponent(typeof(CharacterController))]
    public class PipCharacterController : MonoBehaviour
    {
        public const int ModeIdle = 0;
        public const int ModeWalk = 1;
        public const int ModeRun = 2;
        public const int ModeTurn = 3;
        public const int ModeJump = 4;
        public const int ModeFall = 5;
        public const int ModeLanding = 6;
        public const int ModePickup = 7;
        public const int ModeCarry = 8;
        public const int ModeThrow = 9;
        public const int ModeInteract = 10;
        public const int ModeCelebrate = 11;

        [Header("Movement")]
        public float walkSpeed = 3.9f;
        public float runSpeed = 6.9f;
        public float carrySpeed = 4.6f;
        public float acceleration = 28f;
        public float deceleration = 34f;
        public float airControl = 15f;
        public float turnSpeedDeg = 760f;

        [Header("Jump & Gravity")]
        public float jumpVelocity = 9.4f;
        public float gravity = -23.5f;
        public float coyoteTime = 0.14f;
        public float jumpBufferTime = 0.14f;
        public float outOfBoundsY = -8.5f;

        [Header("References")]
        public Transform cameraTransform;
        public Transform visualRoot;
        public Animator animator;
        public Transform carrySocket;

        // Runtime state
        private CharacterController _cc;
        private Vector3 _planarVel;
        private float _verticalVel;
        private float _lastGroundedTime = -10f;
        private float _lastJumpPressedTime = -10f;
        private bool _wasGrounded = true;
        private Vector3 _safeCheckpointPos;
        private Quaternion _safeCheckpointRot;
        private float _safeRecordTimer;

        // Animation timers & state
        private int _currentMode = ModeIdle;
        private float _actionLockTimer;
        private int _lockedMode = ModeIdle;
        private float _landingTimer;
        private float _turnTimer;
        private float _footstepTimer;
        private bool _celebrating;

        // Squash & stretch
        private Vector3 _baseVisualScale = Vector3.one;
        private Vector3 _squashScale = Vector3.one;
        private Vector3 _squashVel;

        // Procedural secondary motion, blinking & look-at
        private Transform _headBone;
        private Transform _antennaL1;
        private Transform _antennaL2;
        private Transform _antennaR1;
        private Transform _antennaR2;
        private Transform _backpackBone;
        private Transform _handLBone;
        private Transform _handRBone;
        private Transform _eyelidL;
        private Transform _eyelidR;
        private float _blinkTimer = 2.4f;
        private float _blinkPhase;
        private Vector3 _lastWorldPos;
        private Vector3 _springVel;
        private Vector3 _springDisp;
        private Quaternion _smoothLookOffset = Quaternion.identity;

        // Carry & Interaction
        public StarberryItem CarriedFruit { get; private set; }
        public IPocketInteractable FocusedInteractable { get; private set; }
        public bool InShallowWater { get; set; }
        public bool IsInVehicle { get; private set; }
        public int CurrentAnimMode => _currentMode;
        public Vector3 PlanarVelocity => _planarVel;
        public bool IsGrounded => _cc != null && _cc.isGrounded;

        public void SetInVehicle(bool inVehicle)
        {
            IsInVehicle = inVehicle;
            if (_cc != null)
                _cc.enabled = !inVehicle;
            if (visualRoot != null && visualRoot != transform)
                visualRoot.gameObject.SetActive(!inVehicle);
            _planarVel = Vector3.zero;
            _verticalVel = 0f;
            FocusedInteractable = null;
        }

        // External / Automated Playtest Input Override
        private bool _overrideActive;
        private Vector2 _overrideMove;
        private bool _overrideSprint;
        private bool _overrideJump;
        private bool _overrideInteract;

        public void SetAutomatedInput(bool active, Vector2 move, bool sprint = false, bool jump = false, bool interact = false)
        {
            _overrideActive = active;
            _overrideMove = move;
            _overrideSprint = sprint;
            if (jump) _overrideJump = true;
            if (interact) _overrideInteract = true;
        }

        public void ClearAutomatedInput()
        {
            _overrideActive = false;
            _overrideMove = Vector2.zero;
            _overrideSprint = false;
            _overrideJump = false;
            _overrideInteract = false;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.height = 1.12f;
            _cc.radius = 0.30f;
            _cc.center = new Vector3(0f, 0.56f, 0f);
            _cc.stepOffset = 0.34f;
            _cc.slopeLimit = 50f;
            _cc.minMoveDistance = 0.0001f;

            _safeCheckpointPos = transform.position;
            _safeCheckpointRot = transform.rotation;
            _lastWorldPos = transform.position;

            if (visualRoot == null)
            {
                Transform child = transform.Find("PipVisual");
                visualRoot = child != null ? child : transform;
            }
            _baseVisualScale = visualRoot.localScale;

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheBonesAndBuildBlinkLids();
        }

        private void CacheBonesAndBuildBlinkLids()
        {
            _headBone = FindDeepChild(transform, "Head");
            _antennaL1 = FindDeepChild(transform, "AntennaL_1");
            _antennaL2 = FindDeepChild(transform, "AntennaL_2");
            _antennaR1 = FindDeepChild(transform, "AntennaR_1");
            _antennaR2 = FindDeepChild(transform, "AntennaR_2");
            _backpackBone = FindDeepChild(transform, "Backpack");
            _handLBone = FindDeepChild(transform, "HandL");
            _handRBone = FindDeepChild(transform, "HandR");

            if (carrySocket == null)
            {
                GameObject sock = new GameObject("CarrySocket");
                sock.transform.SetParent(transform, false);
                sock.transform.localPosition = new Vector3(0f, 0.58f, 0.44f);
                carrySocket = sock.transform;
            }

            // Create cute mint-teal blinking eyelid shells parented to Head bone
            if (_headBone != null && _eyelidL == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material lidMat = new Material(lit);
                Color mint = new Color(0.28f, 0.58f, 0.50f, 1f);
                lidMat.color = mint;
                if (lidMat.HasProperty("_BaseColor")) lidMat.SetColor("_BaseColor", mint);

                Vector3 worldEyeL = transform.TransformPoint(new Vector3(-0.082f, 0.685f, 0.205f));
                Vector3 worldEyeR = transform.TransformPoint(new Vector3(0.082f, 0.685f, 0.205f));
                _eyelidL = CreateEyelidShell("BlinkLidL", _headBone, worldEyeL, transform.rotation, lidMat);
                _eyelidR = CreateEyelidShell("BlinkLidR", _headBone, worldEyeR, transform.rotation, lidMat);
            }
        }

        private static Transform CreateEyelidShell(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.transform.localScale = new Vector3(0.075f, 0.005f, 0.035f);
            go.transform.SetParent(parent, true);
            go.SetActive(false);
            return go.transform;
        }

        public static Transform FindDeepChild(Transform root, string targetName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == targetName)
                    return t;
            }
            return null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || IsInVehicle) return;

            ReadInputs(out Vector2 moveInput, out bool sprintHeld, out bool jumpPressed, out bool jumpHeld, out bool interactPressed, out bool throwPressed);

            if (jumpPressed)
                _lastJumpPressedTime = Time.time;

            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                _lastGroundedTime = Time.time;
                if (!_wasGrounded && _verticalVel < -2.5f)
                {
                    OnLanded(Mathf.Abs(_verticalVel));
                }
                if (_verticalVel < 0f)
                    _verticalVel = -2.2f;

                _safeRecordTimer += dt;
                if (_safeRecordTimer >= 0.45f && !InShallowWater)
                {
                    _safeRecordTimer = 0f;
                    _safeCheckpointPos = transform.position;
                    _safeCheckpointRot = transform.rotation;
                }
            }

            // Compute camera-relative wish direction
            Vector3 camFwd = Vector3.forward;
            Vector3 camRight = Vector3.right;
            if (cameraTransform != null)
            {
                camFwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                if (camFwd.sqrMagnitude < 0.01f) camFwd = Vector3.forward;
            }

            Vector3 wishDir = (camRight * moveInput.x + camFwd * moveInput.y);
            float wishMag = Mathf.Clamp01(wishDir.magnitude);
            if (wishMag > 0.01f)
                wishDir.Normalize();

            float targetSpeed = 0f;
            if (!_celebrating && wishMag > 0.05f)
            {
                if (CarriedFruit != null)
                    targetSpeed = carrySpeed * wishMag;
                else if (sprintHeld || wishMag > 0.82f)
                    targetSpeed = runSpeed * wishMag;
                else
                    targetSpeed = walkSpeed * wishMag;
            }

            Vector3 targetPlanarVel = wishDir * targetSpeed;
            float accelRate = grounded
                ? (targetSpeed > _planarVel.magnitude ? acceleration : deceleration)
                : airControl;

            // Detect sharp reversal pivot for Turn animation
            if (grounded && CarriedFruit == null && _planarVel.magnitude > 2.4f && wishMag > 0.4f)
            {
                float dot = Vector3.Dot(_planarVel.normalized, wishDir);
                if (dot < -0.45f && _turnTimer <= 0f)
                {
                    _turnTimer = 0.26f;
                    TriggerSquash(new Vector3(1.12f, 0.88f, 1.12f));
                }
            }

            _planarVel = Vector3.MoveTowards(_planarVel, targetPlanarVel, accelRate * dt);

            if (wishMag > 0.05f && !_celebrating)
            {
                Quaternion desiredRot = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, turnSpeedDeg * dt);
            }

            // Jump check (Coyote Time + Jump Buffer)
            if (!_celebrating &&
                (Time.time - _lastJumpPressedTime) <= jumpBufferTime &&
                (Time.time - _lastGroundedTime) <= coyoteTime)
            {
                PerformJump(jumpVelocity);
            }

            // Variable jump height cut
            if (!grounded && _verticalVel > 2.5f && !jumpHeld && !_overrideActive)
            {
                _verticalVel += gravity * 0.85f * dt;
            }

            _verticalVel += gravity * dt;
            if (_verticalVel < -26f) _verticalVel = -26f;

            Vector3 motion = (_planarVel + Vector3.up * _verticalVel) * dt;
            _cc.Move(motion);
            grounded = _cc.isGrounded;

            // Out-of-bounds recovery
            if (transform.position.y < outOfBoundsY)
            {
                RespawnAtSafeCheckpoint();
            }

            // Update Carried Fruit position between hands
            UpdateCarriedFruitTransform();

            // Scan nearest interactable or Starberry
            ScanInteractables();

            // Handle Interact / Pickup / Drop / Throw
            if (interactPressed)
            {
                HandleInteractAction();
            }
            else if (throwPressed && CarriedFruit != null)
            {
                ThrowCarriedFruit(8.8f);
            }

            // Footstep audio & water splash
            UpdateFootsteps(dt, grounded);

            // Animation state machine
            UpdateAnimationMode(dt, grounded);

            // Smooth squash & stretch recovery
            _squashScale = Vector3.SmoothDamp(_squashScale, Vector3.one, ref _squashVel, 0.09f);
            if (visualRoot != null && visualRoot != transform)
            {
                visualRoot.localScale = Vector3.Scale(_baseVisualScale, _squashScale);
            }

            _wasGrounded = grounded;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Secondary spring motion on antennae & backpack based on character acceleration/vertical motion
            Vector3 worldVel = (transform.position - _lastWorldPos) / Mathf.Max(0.0001f, dt);
            _lastWorldPos = transform.position;
            Vector3 localVel = transform.InverseTransformDirection(worldVel);

            Vector3 targetDisp = new Vector3(
                Mathf.Clamp(-localVel.z * 3.2f - localVel.y * 2.5f, -26f, 26f),
                0f,
                Mathf.Clamp(localVel.x * 3.0f, -20f, 20f));

            _springDisp = Vector3.SmoothDamp(_springDisp, targetDisp, ref _springVel, 0.07f);
            Quaternion antSpring = Quaternion.Euler(_springDisp.x, 0f, _springDisp.z);
            if (_antennaL1 != null) _antennaL1.localRotation *= antSpring;
            if (_antennaL2 != null) _antennaL2.localRotation *= antSpring;
            if (_antennaR1 != null) _antennaR1.localRotation *= antSpring;
            if (_antennaR2 != null) _antennaR2.localRotation *= antSpring;
            if (_backpackBone != null)
                _backpackBone.localRotation *= Quaternion.Euler(_springDisp.x * 0.45f, 0f, _springDisp.z * 0.35f);

            // 2. Look-at behavior toward focused interactable or nearby Starberry
            Quaternion desiredLook = Quaternion.identity;
            Transform lookTarget = GetLookTargetTransform();
            if (lookTarget != null && _headBone != null)
            {
                Vector3 toTarget = (lookTarget.position + Vector3.up * 0.35f) - (transform.position + Vector3.up * 0.78f);
                if (toTarget.sqrMagnitude > 0.05f && toTarget.sqrMagnitude < 36f)
                {
                    Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);
                    if (localDir.z > 0.1f)
                    {
                        float yaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg * 0.55f, -28f, 28f);
                        float pitch = Mathf.Clamp(-Mathf.Asin(localDir.y) * Mathf.Rad2Deg * 0.45f, -18f, 18f);
                        desiredLook = Quaternion.Euler(pitch, yaw, 0f);
                    }
                }
            }
            _smoothLookOffset = Quaternion.Slerp(_smoothLookOffset, desiredLook, dt * 8f);
            if (_headBone != null)
                _headBone.localRotation *= _smoothLookOffset;

            // 3. Periodic expressive eye blink
            UpdateBlinking(dt);
        }

        private void UpdateBlinking(float dt)
        {
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinkPhase += dt * 14f;
                float closeAmount = Mathf.Sin(Mathf.Clamp01(_blinkPhase) * Mathf.PI);
                bool active = closeAmount > 0.08f;
                Vector3 lidScale = new Vector3(0.074f, Mathf.Lerp(0.004f, 0.074f, closeAmount), 0.036f);
                if (_eyelidL != null)
                {
                    _eyelidL.gameObject.SetActive(active);
                    _eyelidL.localScale = lidScale;
                }
                if (_eyelidR != null)
                {
                    _eyelidR.gameObject.SetActive(active);
                    _eyelidR.localScale = lidScale;
                }
                if (_blinkPhase >= 1f)
                {
                    _blinkPhase = 0f;
                    _blinkTimer = UnityEngine.Random.Range(2.2f, 4.4f);
                    if (_eyelidL != null) _eyelidL.gameObject.SetActive(false);
                    if (_eyelidR != null) _eyelidR.gameObject.SetActive(false);
                }
            }
        }

        private Transform GetLookTargetTransform()
        {
            if (FocusedInteractable is MonoBehaviour mb && mb != null)
                return mb.transform;
            return null;
        }

        private void ReadInputs(out Vector2 move, out bool sprintHeld, out bool jumpPressed, out bool jumpHeld, out bool interactPressed, out bool throwPressed)
        {
            if (_overrideActive)
            {
                move = _overrideMove;
                sprintHeld = _overrideSprint;
                jumpPressed = _overrideJump;
                jumpHeld = _overrideJump;
                interactPressed = _overrideInteract;
                throwPressed = false;
                _overrideJump = false;
                _overrideInteract = false;
                return;
            }

            move = Vector2.zero;
            sprintHeld = false;
            jumpPressed = false;
            jumpHeld = false;
            interactPressed = false;
            throwPressed = false;

            // 1. Legacy UnityEngine.Input fallback (always active when activeInputHandler is 0 or 2)
            float legX = 0f;
            float legY = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) legX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) legX += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) legY -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) legY += 1f;
            move = new Vector2(legX, legY);

            sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            jumpPressed = Input.GetKeyDown(KeyCode.Space);
            jumpHeld = Input.GetKey(KeyCode.Space);
            interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return);
            throwPressed = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.R);
            if (Input.GetMouseButtonDown(0) && CarriedFruit != null)
            {
                throwPressed = true;
            }

            // 2. New Input System (Keyboard / Mouse / Gamepad)
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 kbMove = Vector2.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y += 1f;
                if (kbMove.sqrMagnitude > move.sqrMagnitude)
                    move = kbMove;

                sprintHeld |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jumpPressed |= kb.spaceKey.wasPressedThisFrame;
                jumpHeld |= kb.spaceKey.isPressed;
                interactPressed |= kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
                throwPressed |= kb.qKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame;
            }

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && CarriedFruit != null)
            {
                throwPressed = true;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > move.sqrMagnitude)
                    move = ls;
                sprintHeld |= pad.leftShoulder.isPressed || pad.rightTrigger.ReadValue() > 0.4f;
                jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                jumpHeld |= pad.buttonSouth.isPressed;
                interactPressed |= pad.buttonWest.wasPressedThisFrame;
                throwPressed |= pad.buttonEast.wasPressedThisFrame;
            }
        }

        public void PerformJump(float upwardSpeed, bool playJumpSound = true)
        {
            _verticalVel = upwardSpeed;
            _lastGroundedTime = -10f;
            _lastJumpPressedTime = -10f;
            _landingTimer = 0f;
            TriggerSquash(new Vector3(0.84f, 1.22f, 0.84f));
            if (playJumpSound)
                PocketPlanetAudio.PlaySfx("jump", 0.72f, UnityEngine.Random.Range(0.96f, 1.06f));
        }

        public void BounceFromMushroom(float launchSpeed)
        {
            _verticalVel = launchSpeed;
            _lastGroundedTime = -10f;
            _lastJumpPressedTime = -10f;
            _landingTimer = 0f;
            TriggerSquash(new Vector3(0.78f, 1.32f, 0.78f));
            PocketPlanetAudio.PlaySfx("mushroom_boing", 0.85f, UnityEngine.Random.Range(0.95f, 1.06f));
        }

        private void OnLanded(float impactSpeed)
        {
            float t = Mathf.InverseLerp(2.5f, 14f, impactSpeed);
            TriggerSquash(new Vector3(Mathf.Lerp(1.06f, 1.24f, t), Mathf.Lerp(0.90f, 0.74f, t), Mathf.Lerp(1.06f, 1.24f, t)));
            _landingTimer = 0.22f;
            if (InShallowWater)
                PocketPlanetAudio.PlayFootstep(true);
            else
                PocketPlanetAudio.PlaySfx("land", Mathf.Lerp(0.35f, 0.75f, t), UnityEngine.Random.Range(0.94f, 1.06f));
        }

        public void TriggerSquash(Vector3 scaleMultiplier)
        {
            _squashScale = scaleMultiplier;
        }

        private void UpdateCarriedFruitTransform()
        {
            if (CarriedFruit == null) return;

            Vector3 handMid = transform.TransformPoint(new Vector3(0f, 0.56f, 0.42f));
            if (_handLBone != null && _handRBone != null)
            {
                Vector3 rawMid = 0.5f * (_handLBone.position + _handRBone.position);
                // Keep fruit right between Pip's hands and slightly forward of torso
                handMid = Vector3.Lerp(handMid, rawMid + transform.forward * 0.14f + Vector3.up * 0.04f, 0.75f);
            }
            carrySocket.position = handMid;
            carrySocket.rotation = transform.rotation;
        }

        private readonly System.Collections.Generic.List<IPocketInteractable> _cachedInteractables = new System.Collections.Generic.List<IPocketInteractable>();
        private float _nextInteractableCacheTime;

        private void RefreshInteractableCacheIfNeeded()
        {
            if (_cachedInteractables.Count > 0 && Time.time < _nextInteractableCacheTime)
                return;

            _nextInteractableCacheTime = Time.time + 1.0f;
            _cachedInteractables.Clear();
            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                if (allBehaviours[i] is IPocketInteractable inter)
                {
                    _cachedInteractables.Add(inter);
                }
            }
        }

        private void ScanInteractables()
        {
            FocusedInteractable = null;

            // If an NPC dialogue is currently open and Pip is still in range, keep focus on that NPC so [E] advances lines smoothly
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null)
            {
                if (gm.captainAstra != null && gm.captainAstra.IsDialogueOpen && gm.captainAstra.CanInteract(this))
                {
                    FocusedInteractable = gm.captainAstra;
                    return;
                }
                if (gm.wardenNyx != null && gm.wardenNyx.IsDialogueOpen && gm.wardenNyx.CanInteract(this))
                {
                    FocusedInteractable = gm.wardenNyx;
                    return;
                }
            }

            float bestScore = float.MaxValue;

            RefreshInteractableCacheIfNeeded();
            for (int i = 0; i < _cachedInteractables.Count; i++)
            {
                IPocketInteractable inter = _cachedInteractables[i];
                if (inter is MonoBehaviour mb && mb != null && mb.gameObject.activeInHierarchy && inter.CanInteract(this))
                {
                    float dist = Vector3.Distance(transform.position, mb.transform.position);
                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        FocusedInteractable = inter;
                    }
                }
            }

            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, 5.5f);
            foreach (Collider col in hits)
            {
                IPocketInteractable inter = col.GetComponentInParent<IPocketInteractable>();
                if (inter == null || !inter.CanInteract(this))
                    continue;
                if (inter is MonoBehaviour mb)
                {
                    float dist = Vector3.Distance(transform.position, mb.transform.position);
                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        FocusedInteractable = inter;
                    }
                }
            }
        }

        public void HandleInteractAction()
        {
            // Priority 0: If an NPC dialogue is currently open and in range, advance/close it
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null)
            {
                if (gm.captainAstra != null && gm.captainAstra.IsDialogueOpen && gm.captainAstra.CanInteract(this))
                {
                    gm.captainAstra.OnInteract(this);
                    PlayInteractAnimation();
                    return;
                }
                if (gm.wardenNyx != null && gm.wardenNyx.IsDialogueOpen && gm.wardenNyx.CanInteract(this))
                {
                    gm.wardenNyx.OnInteract(this);
                    PlayInteractAnimation();
                    return;
                }
            }

            // Priority 1: If carrying fruit and near a HungryBloomPlant that wants fruit, feed it!
            if (CarriedFruit != null && FocusedInteractable is HungryBloomPlant plant && plant.CanInteract(this))
            {
                plant.OnInteract(this);
                return;
            }

            // Priority 2: If carrying fruit and not near a hungry plant, throw/drop it forward
            if (CarriedFruit != null)
            {
                ThrowCarriedFruit(_planarVel.magnitude > 1.5f ? 8.5f : 3.2f);
                return;
            }

            // Priority 3: Interact with focused object (NPC, Solaris Core, Saucer, Pylon, Shrine, Starberry, Lever, Bloop, Spaceship)
            if (FocusedInteractable != null && FocusedInteractable.CanInteract(this))
            {
                FocusedInteractable.OnInteract(this);
                if (!IsInVehicle && !_celebrating && CarriedFruit == null)
                {
                    PlayInteractAnimation();
                }
            }
        }

        public void PickupFruit(StarberryItem berry)
        {
            if (berry == null || CarriedFruit != null) return;
            CarriedFruit = berry;
            berry.AttachToCarrier(carrySocket);
            LockAnimation(ModePickup, 0.36f);
            TriggerSquash(new Vector3(1.14f, 0.85f, 1.14f));
            PocketPlanetAudio.PlaySfx("pickup_fruit", 0.8f);
        }

        public StarberryItem ReleaseCarriedFruit()
        {
            StarberryItem b = CarriedFruit;
            CarriedFruit = null;
            return b;
        }

        public void ThrowCarriedFruit(float throwSpeed)
        {
            if (CarriedFruit == null) return;
            StarberryItem berry = CarriedFruit;
            CarriedFruit = null;
            Vector3 launchVel = transform.forward * throwSpeed + Vector3.up * 3.4f + _planarVel * 0.35f;
            berry.ReleaseAndThrow(launchVel);
            LockAnimation(ModeThrow, 0.36f);
            TriggerSquash(new Vector3(0.88f, 1.15f, 1.08f));
            PocketPlanetAudio.PlaySfx("throw_fruit", 0.82f);
        }

        public void PlayInteractAnimation()
        {
            LockAnimation(ModeInteract, 0.48f);
        }

        public void SetCelebrating(bool celebrating)
        {
            _celebrating = celebrating;
            if (celebrating)
            {
                _planarVel = Vector3.zero;
                TriggerSquash(new Vector3(0.88f, 1.18f, 0.88f));
            }
        }

        private void LockAnimation(int mode, float duration)
        {
            _lockedMode = mode;
            _actionLockTimer = duration;
        }

        private void UpdateFootsteps(float dt, bool grounded)
        {
            float speed = _planarVel.magnitude;
            if (grounded && speed > 0.65f)
            {
                float interval = Mathf.Lerp(0.42f, 0.22f, Mathf.InverseLerp(walkSpeed, runSpeed, speed));
                _footstepTimer -= dt;
                if (_footstepTimer <= 0f)
                {
                    _footstepTimer = interval;
                    PocketPlanetAudio.PlayFootstep(InShallowWater);
                }
            }
            else
            {
                _footstepTimer = 0.08f;
            }
        }

        private void UpdateAnimationMode(float dt, bool grounded)
        {
            if (_actionLockTimer > 0f)
                _actionLockTimer -= dt;
            if (_landingTimer > 0f)
                _landingTimer -= dt;
            if (_turnTimer > 0f)
                _turnTimer -= dt;

            int desiredMode;
            float animSpeed = 1f;
            float speed = _planarVel.magnitude;

            if (_celebrating)
            {
                desiredMode = ModeCelebrate;
            }
            else if (_actionLockTimer > 0f)
            {
                desiredMode = _lockedMode;
            }
            else if (CarriedFruit != null)
            {
                desiredMode = ModeCarry;
                animSpeed = speed > 0.25f ? Mathf.Clamp(speed / carrySpeed, 0.65f, 1.35f) : 0.35f;
            }
            else if (!grounded)
            {
                desiredMode = _verticalVel > 0.6f ? ModeJump : ModeFall;
            }
            else if (_landingTimer > 0f && speed < 1.2f)
            {
                desiredMode = ModeLanding;
            }
            else if (_turnTimer > 0f)
            {
                desiredMode = ModeTurn;
            }
            else if (speed > runSpeed * 0.70f)
            {
                desiredMode = ModeRun;
                animSpeed = Mathf.Clamp(speed / runSpeed, 0.8f, 1.25f);
            }
            else if (speed > 0.22f)
            {
                desiredMode = ModeWalk;
                animSpeed = Mathf.Clamp(speed / walkSpeed, 0.7f, 1.35f);
            }
            else
            {
                desiredMode = ModeIdle;
            }

            _currentMode = desiredMode;
            if (animator != null)
            {
                animator.SetInteger("Mode", desiredMode);
                animator.speed = animSpeed;
            }
        }

        public void TeleportTo(Vector3 position)
        {
            TeleportTo(position, transform.rotation);
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _cc.enabled;
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _lastWorldPos = position;
            _planarVel = Vector3.zero;
            _verticalVel = -2.2f;
            _cc.enabled = wasEnabled;
            if (_cc.enabled)
            {
                _cc.Move(Vector3.down * 0.12f);
            }
        }

        public void RespawnAtSafeCheckpoint()
        {
            TeleportTo(_safeCheckpointPos + Vector3.up * 0.6f, _safeCheckpointRot);
            TriggerSquash(new Vector3(1.18f, 0.80f, 1.18f));
            PocketPlanetAudio.PlaySfx("jump", 0.6f, 0.85f);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Push physical objects (PushableBoulder) naturally when Pip runs into them
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                PushableBoulder boulder = rb.GetComponent<PushableBoulder>();
                if (boulder != null)
                {
                    Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
                    if (pushDir.sqrMagnitude > 0.01f)
                    {
                        boulder.ApplyPlayerPush(pushDir.normalized, Mathf.Max(3.5f, _planarVel.magnitude));
                    }
                }
            }
        }
    }
}
