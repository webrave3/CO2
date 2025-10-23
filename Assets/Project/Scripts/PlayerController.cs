using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC; // Make sure this is present

public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameObject _playerModel; // Assign your player's visual mesh GameObject

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 0.5f;
    [SerializeField] private float _jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private float _cameraMaxY = 80f;
    [SerializeField] private float _cameraMinY = -80f;

    // Vehicle Interaction
    private VehicleInteraction _vehicleInteraction; // Reference to interaction script

    private bool _jumpConsumed = false;

    // --- ADDED for Vehicle State ---
    [Networked] public NetworkBool IsInVehicle { get; private set; }
    [Networked] public NetworkId VehicleNetworkId { get; private set; }
    // --- End ADDED ---

    private void Awake()
    {
        // Ensure required components (Original logic)
        if (_kcc == null) _kcc = GetComponent<SimpleKCC>();
        if (_camera == null) _camera = GetComponentInChildren<Camera>();

        // Get or add VehicleInteraction
        _vehicleInteraction = GetComponent<VehicleInteraction>();
        if (_vehicleInteraction == null)
        {
            Debug.LogWarning("VehicleInteraction component not found on player. Adding one.");
            _vehicleInteraction = gameObject.AddComponent<VehicleInteraction>();
        }
    }

    public override void Spawned()
    {
        // Debug.Log($"Player Spawned - HasInputAuthority: {HasInputAuthority}, ID: {Object.InputAuthority}");

        if (HasInputAuthority)
        {
            // Debug.Log("This is the local player");
            if (Camera.main != null && Camera.main != _camera) Camera.main.gameObject.SetActive(false);

            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                _camera.tag = "MainCamera";
                _camera.enabled = !IsInVehicle; // Initial state based on vehicle status

                if (!_camera.GetComponent<SessionInfoDisplay>()) // Add if missing
                {
                    _camera.gameObject.AddComponent<SessionInfoDisplay>();
                    // Debug.Log("Added SessionInfoDisplay to player camera");
                }
            }
            else Debug.LogError("Player Camera reference is not set!");

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else // Remote player setup
        {
            // Debug.Log($"This is a remote player (ID: {Object.InputAuthority})");
            if (_camera != null) _camera.gameObject.SetActive(false);
        }

        // --- ADDED: Ensure components start in correct state based on IsInVehicle ---
        _kcc.enabled = !IsInVehicle;
        if (_playerModel != null) _playerModel.SetActive(!IsInVehicle);
        else Debug.LogWarning("Player Model reference is not set on PlayerController!");
        // --- End ADDED ---
    }

    // *** USING ORIGINAL FIXEDUPDATENETWORK LOGIC ***
    public override void FixedUpdateNetwork()
    {
        // --- MODIFIED: Only run if NOT in vehicle ---
        if (!IsInVehicle && GetInput(out NetworkInputData data))
        {
            // Apply look rotation (Original logic)
            // Corrected vertical sensitivity application based on original script
            _kcc.AddLookRotation(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * _lookSensitivity);

            // Calculate movement direction (Original logic)
            Vector3 moveDirection = _kcc.TransformRotation * new Vector3(data.HorizontalInput, 0.0f, data.VerticalInput);
            Vector3 moveVelocity = moveDirection.normalized * _moveSpeed;

            // Handle jumping (Original logic)
            float jumpImpulse = 0f;
            if (_kcc.IsGrounded)
            {
                _jumpConsumed = false; // Reset when grounded
            }

            // Check jump input and if not consumed
            if (data.Jump && _kcc.IsGrounded && !_jumpConsumed)
            {
                jumpImpulse = _jumpForce; // Set impulse value
                _jumpConsumed = true;     // Mark as consumed
            }

            // Apply movement (Original KCC Move call - takes velocity and impulse)
            _kcc.Move(moveVelocity, jumpImpulse); // Pass velocity and impulse
        }
        else if (!IsInVehicle) // Stop movement if no input and not in vehicle
        {
            // Call Move with zero velocity and zero impulse to stop
            _kcc.Move(Vector3.zero, 0f);
            // Optionally reset jumpConsumed if needed when input is lost
            // if (GetInput(out var lostData) && !lostData.Jump) _jumpConsumed = false;
        }
        // --- End MODIFIED ---
    }
    // *** END ORIGINAL FIXEDUPDATENETWORK LOGIC ***

    private void LateUpdate()
    {
        // --- MODIFIED: Only run camera logic if local player AND NOT in vehicle ---
        if (!HasInputAuthority || _camera == null || IsInVehicle) return;

        // Apply vertical look rotation locally (Original logic)
        Vector2 lookRotation = _kcc.GetLookRotation(true, false);
        float pitch = Mathf.Clamp(lookRotation.x, _cameraMinY, _cameraMaxY);
        _camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        // --- End MODIFIED ---
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // --- Cursor Lock Logic ---
        // FIX: Use FindFirstObjectByType for singleton access
        GameMenuManager menuManager = FindFirstObjectByType<GameMenuManager>(); // Corrected obsolete call
        bool isMenuOpen = menuManager != null && menuManager.IsMenuActive;

        if (IsInVehicle || !isMenuOpen) // Lock if in vehicle OR menu is closed
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else // Unlock if menu is open AND not in vehicle
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // --- End Cursor Lock ---

        // Debug key F3 (Original logic with FindObjectOfType correction)
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Debug.Log($"==== PLAYER DEBUG ====");
            Debug.Log($"Player ID: {Object.InputAuthority.PlayerId}");
            Debug.Log($"HasInputAuthority: {HasInputAuthority}");
            Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");
            Debug.Log($"IsInVehicle: {IsInVehicle}"); // Added vehicle state debug
            Debug.Log($"VehicleNetworkId: {VehicleNetworkId}"); // Added vehicle ID debug

            // FIX: Use FindFirstObjectByType for singleton access
            GameStateManager gsm = FindFirstObjectByType<GameStateManager>(); // Corrected obsolete call
            if (gsm != null)
            {
                Debug.Log($"Game State: {gsm.State}");
                Debug.Log($"Session: {gsm.SessionDisplayName} | {gsm.SessionHash}");
                Debug.Log($"Players Ready: {gsm.PlayersReady.Count}");
            }
            Debug.Log($"Position: {transform.position}");
            Debug.Log($"Rotation: {transform.rotation.eulerAngles}");
            Debug.Log("=======================");
        }
    }

    // --- ADDED: Method to handle entering/exiting vehicle ---
    // Called via RPC from BasicVehicleController
    public void SetInVehicle(NetworkBool inVehicle, NetworkId vehicleId = default, Vector3 exitPos = default)
    {
        IsInVehicle = inVehicle;
        VehicleNetworkId = vehicleId;

        BasicVehicleController vehicle = null;
        if (inVehicle && Runner != null && VehicleNetworkId.IsValid)
        {
            if (Runner.TryFindObject(VehicleNetworkId, out var vehicleObj))
            {
                vehicle = vehicleObj.GetComponent<BasicVehicleController>();
            }
        }

        // Apply changes locally if Input Authority
        if (HasInputAuthority)
        {
            if (_camera != null) _camera.enabled = !inVehicle;

            // Teleport KCC *before* enabling/disabling if needed when exiting
            if (!inVehicle)
            {
                _kcc.SetPosition(exitPos); // Teleport first
            }
            _kcc.enabled = !inVehicle; // Then toggle KCC state
        }

        // Toggle player model visibility for everyone
        if (_playerModel != null)
        {
            _playerModel.SetActive(!inVehicle);
        }

        // Update VehicleInteraction's current vehicle reference
        if (_vehicleInteraction != null)
        {
            _vehicleInteraction.SetCurrentVehicle(vehicle);
        }
        // Debug.Log($"Player {Object.InputAuthority} IsInVehicle set to: {inVehicle}");
    }
    // --- End ADDED ---


    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Debug.Log($"Player despawned - InputAuthority: {Object.InputAuthority}");
        if (HasInputAuthority) // Original cleanup
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}