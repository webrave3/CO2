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

    // --- Vehicle State ---
    [Networked] public NetworkBool IsInVehicle { get; private set; }
    [Networked] public NetworkId VehicleNetworkId { get; private set; }
    // --- End Vehicle State ---

    // --- AUDIO LISTENER FIX: Added field ---
    private AudioListener _playerAudioListener;
    // --- End AUDIO LISTENER FIX ---

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

        // --- AUDIO LISTENER FIX: Get the listener component ---
        if (_camera != null)
        {
            _playerAudioListener = _camera.GetComponent<AudioListener>();
            if (_playerAudioListener == null)
            {
                Debug.LogWarning("AudioListener component not found on player camera. Adding one.");
                _playerAudioListener = _camera.gameObject.AddComponent<AudioListener>();
            }
        }
        // --- End AUDIO LISTENER FIX ---
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

                // --- AUDIO LISTENER FIX: Set initial listener state ---
                if (_playerAudioListener != null)
                {
                    _playerAudioListener.enabled = !IsInVehicle; // Enable if NOT in vehicle, disable if IN vehicle
                }
                // --- End AUDIO LISTENER FIX ---

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
            // --- AUDIO LISTENER FIX: Ensure remote player listener is disabled ---
            if (_playerAudioListener != null) _playerAudioListener.enabled = false;
            // --- End AUDIO LISTENER FIX ---
        }

        // Ensure components start in correct state based on IsInVehicle
        if (_kcc != null) _kcc.enabled = !IsInVehicle; // Added null check for safety
        else Debug.LogError("SimpleKCC component reference (_kcc) is null in Spawned!");

        if (_playerModel != null) _playerModel.SetActive(!IsInVehicle);
        else Debug.LogWarning("Player Model reference is not set on PlayerController!");
    }

    public override void FixedUpdateNetwork()
    {
        bool gotInput = false;
        NetworkInputData data = default; // Initialize data

        // Only run if NOT in vehicle and we have input authority
        if (!IsInVehicle && HasInputAuthority && (gotInput = GetInput(out data))) // Added HasInputAuthority check here
        {
            if (_kcc == null) return; // Added null check for safety

            // Apply look rotation
            _kcc.AddLookRotation(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * _lookSensitivity);

            // Calculate movement direction
            Vector3 moveDirection = _kcc.TransformRotation * new Vector3(data.HorizontalInput, 0.0f, data.VerticalInput);
            Vector3 moveVelocity = moveDirection.normalized * _moveSpeed;

            // Handle jumping
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

            // Apply movement
            _kcc.Move(moveVelocity, jumpImpulse);

            // --- CORRECTED: InGameDebug Calls ---
            InGameDebug.Log($"Ctrl_Auth: {HasInputAuthority} | GotInput: {gotInput}"); // Combined Auth and GotInput
            InGameDebug.Log($"Ctrl_Move: H={data.HorizontalInput:F2}, V={data.VerticalInput:F2}, J={data.Jump}"); // Combined Movement
            InGameDebug.Log($"Ctrl_KCC: Vel={moveVelocity}, JImp={jumpImpulse:F2}, Grounded={_kcc.IsGrounded}"); // Combined KCC state
            InGameDebug.Log($"Ctrl_Vehicle: IsIn={IsInVehicle}"); // Added check for Vehicle State
            // --- End CORRECTED ---
        }
        else if (!IsInVehicle) // Stop movement if no input (or no authority) and not in vehicle
        {
            if (_kcc != null && _kcc.enabled) // Only call Move if KCC is active
            {
                _kcc.Move(Vector3.zero, 0f);
            }

            // --- CORRECTED: InGameDebug Calls (No Input/No Auth/Not In Vehicle) ---
            // Log only if HasInputAuthority is true but gotInput is false, or periodically if relevant
            if (HasInputAuthority && !gotInput) // Log reason for no movement only if we should have input
            {
                InGameDebug.Log($"Ctrl_Auth: {HasInputAuthority} | GotInput: {gotInput} | IsInVehicle: {IsInVehicle}");
                InGameDebug.Log($"Ctrl_KCC_STOP: Grounded={(_kcc != null ? _kcc.IsGrounded.ToString() : "N/A")}");
            }
            else if (!HasInputAuthority) // Log lack of authority if applicable
            {
                // Maybe log this less frequently if it becomes spammy
                // InGameDebug.Log($"Ctrl_Auth: {HasInputAuthority}");
            }
            // --- End CORRECTED ---
        }
        else // Handle the case when IsInVehicle is true
        {
            // --- CORRECTED: InGameDebug Calls (In Vehicle) ---
            // Log this state only periodically or on state change if needed, otherwise it's spammy
            // InGameDebug.Log($"Ctrl_InVehicle: Auth={HasInputAuthority}, IsIn={IsInVehicle}");
            // --- End CORRECTED ---
        }
    }


    private void LateUpdate()
    {
        // Only run camera logic if local player AND NOT in vehicle
        if (!HasInputAuthority || _camera == null || IsInVehicle) return;
        if (_kcc == null) return; // Added null check

        // Apply vertical look rotation locally
        Vector2 lookRotation = _kcc.GetLookRotation(true, false);
        float pitch = Mathf.Clamp(lookRotation.x, _cameraMinY, _cameraMaxY);
        _camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // --- Cursor Lock Logic ---
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

        // Debug key F3
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Debug.Log($"==== PLAYER DEBUG ====");
            Debug.Log($"Player ID: {Object.InputAuthority.PlayerId}");
            Debug.Log($"HasInputAuthority: {HasInputAuthority}");
            Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");
            Debug.Log($"IsInVehicle: {IsInVehicle}");
            Debug.Log($"VehicleNetworkId: {VehicleNetworkId}");

            GameStateManager gsm = FindFirstObjectByType<GameStateManager>();
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

    // Called via RPC from NetworkedPrometeoCar
    public void SetInVehicle(NetworkBool inVehicle, NetworkId vehicleId = default, Vector3 exitPos = default)
    {
        IsInVehicle = inVehicle;
        VehicleNetworkId = vehicleId;

        NetworkedPrometeoCar vehicleComponent = null;
        if (inVehicle && Runner != null && VehicleNetworkId.IsValid)
        {
            if (Runner.TryFindObject(VehicleNetworkId, out var vehicleObj))
            {
                if (!vehicleObj.TryGetComponent(out vehicleComponent))
                {
                    Debug.LogError($"Vehicle object {VehicleNetworkId} found, but it is missing the NetworkedPrometeoCar component!");
                }
            }
            else
            {
                Debug.LogError($"SetInVehicle: Could not find vehicle NetworkObject with ID {VehicleNetworkId}.");
            }
        }

        if (HasInputAuthority)
        {
            if (_camera != null) _camera.enabled = !inVehicle;
            if (_playerAudioListener != null) _playerAudioListener.enabled = !inVehicle;

            if (_kcc != null)
            {
                if (!inVehicle)
                {
                    Vector3 targetPosition = (exitPos == default) ? transform.position + transform.forward : exitPos;
                    _kcc.SetPosition(targetPosition); // Teleport before enabling
                    Debug.Log($"PlayerController {Object.Id}: Teleporting KCC to exit position {targetPosition}");
                }
                _kcc.enabled = !inVehicle;
            }
            else { Debug.LogError("KCC reference is null in PlayerController!"); }
        }

        if (_playerModel != null)
        {
            _playerModel.SetActive(!inVehicle);
        }
        else { Debug.LogWarning("Player Model reference is not set on PlayerController!"); }

        if (_vehicleInteraction != null)
        {
            _vehicleInteraction.SetCurrentVehicle(vehicleComponent);
            Debug.Log($"PlayerController {Object.Id}: Updated VehicleInteraction with vehicle {(vehicleComponent == null ? "null" : vehicleComponent.Id.ToString())}");
        }
        else { Debug.LogError("VehicleInteraction reference is null in PlayerController!"); }

        Debug.Log($"PlayerController {Object.Id} IsInVehicle set to: {inVehicle} for Vehicle: {vehicleId}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}