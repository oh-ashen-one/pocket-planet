using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketPlanet
{
    public class SpaceshipFlightController : MonoBehaviour, IPocketInteractable
    {
        public static SpaceshipFlightController Instance { get; private set; }

        public Transform saucerVisualRoot;
        public Transform thrusterGlowRoot;
        public Transform cockpitPipRoot;
        public Light undersideBeamLight;

        public Vector3 planetOnePadPos = new Vector3(-16f, 2.35f, -6f);
        public Vector3 planetTwoPadPos = new Vector3(-16f, 2.35f, 862f);

        public Vector3 spaceCorridorStart = new Vector3(0f, 85f, 215f);
        public Vector3 spaceCorridorEnd = new Vector3(0f, 85f, 565f);

        public float baseFlightSpeed = 30f;
        public float boostFlightSpeed = 52f;
        public float lateralSpeed = 24f;
        public float verticalSpeed = 18f;

        public bool IsInFlight { get; private set; }
        public bool IsParkedOnPlanetTwo { get; private set; }
        public bool FlyingTowardPlanetTwo { get; private set; } = true;

        private float _currentSpeed;
        private float _rollAngle;
        private float _pitchAngle;
        private float _boostRingTimer;
        private float _flightProgress01;
        private bool _autoFlightActive;
        private Vector2 _autoSteer;
        private bool _autoBoost;

        public float FlightProgress01 => _flightProgress01;
        public float CurrentSpeed => _currentSpeed;

        public void SetAutomatedFlightInput(bool active, Vector2 steer, bool boost)
        {
            _autoFlightActive = active;
            _autoSteer = steer;
            _autoBoost = boost;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (saucerVisualRoot == null)
                saucerVisualRoot = transform;
            if (thrusterGlowRoot != null)
                thrusterGlowRoot.gameObject.SetActive(false);
            if (cockpitPipRoot != null)
                cockpitPipRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            float t = Time.time;

            if (!IsInFlight)
            {
                // Gentle idle hover oscillation on the launchpad
                Vector3 padBase = IsParkedOnPlanetTwo ? planetTwoPadPos : planetOnePadPos;
                transform.position = padBase + new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.14f, 0f);
                transform.rotation = Quaternion.Euler(0f, (t * 22f) % 360f, Mathf.Sin(t * 1.5f) * 2.5f);
                return;
            }

            HandleSpaceFlight(Time.deltaTime);
        }

        private void HandleSpaceFlight(float dt)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            float steerX = 0f;
            float steerY = 0f;

            if (_autoFlightActive)
            {
                steerX = _autoSteer.x;
                steerY = _autoSteer.y;
            }
            else
            {
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerX -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerX += 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.spaceKey.isPressed) steerY += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed || kb.leftCtrlKey.isPressed) steerY -= 1f;
                }
                if (Mathf.Abs(steerX) < 0.01f) steerX = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(steerY) < 0.01f) steerY = Input.GetAxisRaw("Vertical");

                if (pad != null)
                {
                    Vector2 ls = pad.leftStick.ReadValue();
                    if (ls.sqrMagnitude > 0.02f)
                    {
                        steerX = ls.x;
                        steerY = ls.y;
                    }
                }
            }

            bool boosting = (_autoFlightActive && _autoBoost)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
                || (pad != null && (pad.rightTrigger.ReadValue() > 0.25f || pad.buttonSouth.isPressed))
                || _boostRingTimer > 0f;

            if (_boostRingTimer > 0f)
                _boostRingTimer -= dt;

            float targetSpeed = boosting ? boostFlightSpeed : baseFlightSpeed;
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, dt * 5.5f);

            float dirZ = FlyingTowardPlanetTwo ? 1f : -1f;
            Vector3 pos = transform.position;
            pos.z += dirZ * _currentSpeed * dt;
            pos.x = Mathf.Clamp(pos.x + steerX * lateralSpeed * dt * dirZ, -32f, 32f);
            pos.y = Mathf.Clamp(pos.y + steerY * verticalSpeed * dt, 68f, 108f);
            transform.position = pos;

            _rollAngle = Mathf.Lerp(_rollAngle, -steerX * 32f, dt * 7f);
            _pitchAngle = Mathf.Lerp(_pitchAngle, -steerY * 18f + (boosting ? 8f : 3f), dt * 7f);
            float forwardYaw = FlyingTowardPlanetTwo ? 0f : 180f;
            transform.rotation = Quaternion.Euler(_pitchAngle, forwardYaw + steerX * 14f, _rollAngle);

            // Keep Pip synced inside the saucer so zone tracking works seamlessly
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && gm.player != null)
            {
                gm.player.TeleportTo(transform.position);
            }

            // Chase camera positioning
            if (gm != null && gm.thirdPersonCamera != null)
            {
                Transform camT = gm.thirdPersonCamera.transform;
                Vector3 backDir = FlyingTowardPlanetTwo ? Vector3.back : Vector3.forward;
                Vector3 desiredCamPos = transform.position + backDir * (boosting ? 11.5f : 9.6f) + Vector3.up * 3.4f;
                camT.position = Vector3.Lerp(camT.position, desiredCamPos, dt * 10f);
                Vector3 lookTarget = transform.position + (-backDir) * 14f + Vector3.up * 0.6f;
                camT.rotation = Quaternion.Slerp(camT.rotation, Quaternion.LookRotation(lookTarget - camT.position, Vector3.up), dt * 12f);
            }

            float totalDist = Mathf.Abs(spaceCorridorEnd.z - spaceCorridorStart.z);
            if (FlyingTowardPlanetTwo)
                _flightProgress01 = Mathf.Clamp01((pos.z - spaceCorridorStart.z) / totalDist);
            else
                _flightProgress01 = Mathf.Clamp01((spaceCorridorEnd.z - pos.z) / totalDist);

            // Quick-complete flight hotkey [G] or reaching destination beacon
            bool quickWarp = Input.GetKeyDown(KeyCode.G) || (kb != null && kb.gKey.wasPressedThisFrame);
            if (quickWarp || (FlyingTowardPlanetTwo && pos.z >= spaceCorridorEnd.z) || (!FlyingTowardPlanetTwo && pos.z <= spaceCorridorStart.z))
            {
                CompleteSpaceFlightAndLand(FlyingTowardPlanetTwo);
            }
        }

        public void TriggerWarpRingBoost()
        {
            _boostRingTimer = 1.6f;
            PocketPlanetAudio.PlaySfx("saucer_boost", 0.9f, 1.05f);
            if (PocketPlanetGameManager.Instance != null)
                PocketPlanetGameManager.Instance.ShowBanner("⚡ Hyper-Ring Boost Activated!", 2.2f);
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public bool CanInteract(PipCharacterController player)
        {
            if (IsInFlight || player == null) return false;
            return Vector3.Distance(player.transform.position, transform.position) <= 5.5f;
        }

        public string GetPromptText(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && !gm.HasSolarisCore && !gm.ShrineDelivered)
            {
                return "Starhopper Saucer — Retrieve the Solaris Astral Core on Starlight Ridge first!";
            }
            if (IsParkedOnPlanetTwo)
                return "Press [E] to Board Flying Saucer & Fly Back to Verdant Haven";
            return "Press [E] to Board Flying Saucer & Launch to Obsidian Deadwood!";
        }

        public void OnInteract(PipCharacterController player) => Interact(player);

        public void Interact(PipCharacterController player)
        {
            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && !gm.HasSolarisCore && !gm.ShrineDelivered)
            {
                PocketPlanetAudio.PlaySfx("lever_pull", 0.7f, 0.8f);
                gm.ShowBanner("Talk to Captain Astra and retrieve the golden Solaris Astral Core on Starlight Ridge before launching!");
                return;
            }

            StartSpaceFlight(player, !IsParkedOnPlanetTwo);
        }

        public void StartSpaceFlight(PipCharacterController player, bool toPlanetTwo)
        {
            IsInFlight = true;
            FlyingTowardPlanetTwo = toPlanetTwo;
            _currentSpeed = baseFlightSpeed;
            _flightProgress01 = 0f;

            PocketPlanetAudio.PlaySfx("saucer_launch", 1.0f, 1.0f);

            if (thrusterGlowRoot != null)
                thrusterGlowRoot.gameObject.SetActive(true);
            if (cockpitPipRoot != null)
                cockpitPipRoot.gameObject.SetActive(true);

            Vector3 startPos = toPlanetTwo ? spaceCorridorStart : spaceCorridorEnd;
            transform.position = startPos;
            transform.rotation = Quaternion.Euler(0f, toPlanetTwo ? 0f : 180f, 0f);

            if (player != null)
            {
                player.SetInVehicle(true);
                player.TeleportTo(startPos);
            }

            var gm = PocketPlanetGameManager.Instance;
            if (gm != null)
            {
                if (gm.thirdPersonCamera != null)
                {
                    gm.thirdPersonCamera.SetExternalControl(true);
                    Vector3 backDir = toPlanetTwo ? Vector3.back : Vector3.forward;
                    gm.thirdPersonCamera.transform.position = startPos + backDir * 10f + Vector3.up * 3.5f;
                    gm.thirdPersonCamera.transform.rotation = Quaternion.LookRotation((startPos - gm.thirdPersonCamera.transform.position).normalized, Vector3.up);
                }
                gm.OnEnteredSpaceFlight(toPlanetTwo);
            }
        }

        public void CompleteSpaceFlightAndLand(bool landedOnPlanetTwo)
        {
            IsInFlight = false;
            IsParkedOnPlanetTwo = landedOnPlanetTwo;
            _flightProgress01 = 1f;

            if (thrusterGlowRoot != null)
                thrusterGlowRoot.gameObject.SetActive(false);
            if (cockpitPipRoot != null)
                cockpitPipRoot.gameObject.SetActive(false);

            Vector3 padPos = landedOnPlanetTwo ? planetTwoPadPos : planetOnePadPos;
            transform.position = padPos;
            transform.rotation = Quaternion.identity;

            PocketPlanetAudio.PlaySfx("saucer_boost", 0.85f, 0.88f);

            var gm = PocketPlanetGameManager.Instance;
            if (gm != null && gm.player != null)
            {
                Vector3 disembarkPos = padPos + new Vector3(4.5f, 0.2f, 2.0f);
                gm.player.SetInVehicle(false);
                gm.player.TeleportTo(disembarkPos);
                gm.player.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

                if (gm.thirdPersonCamera != null)
                {
                    gm.thirdPersonCamera.SetExternalControl(false);
                    gm.thirdPersonCamera.yaw = 20f;
                    gm.thirdPersonCamera.pitch = 18f;
                    gm.thirdPersonCamera.SnapToTarget();
                }

                gm.OnLandedOnPlanet(landedOnPlanetTwo);
            }
        }
    }
}
