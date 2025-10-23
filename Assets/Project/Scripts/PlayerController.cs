using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;

public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameObject _playerModel; // Reference to the visual model GO

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 0.5f;
    [SerializeField] private float _jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private float _cameraMaxY = 80f;
    [SerializeField] private float _cameraMinY = -80f;

    private bool _jumpConsumed = false;

    [Networked] public NetworkBool IsInVehicle { get; private set; }
    [Networked] public NetworkId VehicleNetworkId { get; private set; }

    private VehicleInteraction _vehicleInteraction;
    private BasicVehicleController _lastVehicleRef;

    private void Awake()
    {
        // Ensure required components
        if (_kcc == null) _kcc = GetComponent<SimpleKCC>();
        if (_camera == null) _camera = GetComponentInChildren<Camera>();
        _vehicleInteraction = GetComponent<VehicleInteraction>();
    }

    public override void Spawned()
    {
        UnityEngine.Debug.Log($"Player Spawned - HasInputAuthority: {HasInputAuthority}, ID: {Object.InputAuthority}");

        if (HasInputAuthority)
        {
            // Initial camera and cursor setup (will be refined in SetInVehicle/Update)
            if (Camera.main != null && Camera.main != _camera) Camera.main.gameObject.SetActive(false);
            if (_camera != null) { _camera.gameObject.SetActive(true); _camera.tag = "MainCamera"; }
            if (!_camera.GetComponent<SessionInfoDisplay>())
            {
                _camera.gameObject.AddComponent<SessionInfoDisplay>();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (_camera != null) _camera.gameObject.SetActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only process movement/look input if NOT in a vehicle
        if (!IsInVehicle && GetInput(out NetworkInputData data))
        {
            // Apply look rotation
            _kcc.AddLookRotation(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * _lookSensitivity);

            // Calculate movement direction
            Vector3 moveDirection = _kcc.TransformRotation * new Vector3(data.HorizontalInput, 0.0f, data.VerticalInput);
            Vector3 moveVelocity = moveDirection.normalized * _moveSpeed;

            // Handle jumping
            float jumpImpulse = 0f;
            if (_kcc.IsGrounded) _jumpConsumed = false;
            if (data.Jump && _kcc.IsGrounded && !_jumpConsumed)
            {
                jumpImpulse = _jumpForce;
                _jumpConsumed = true;
            }

            _kcc.Move(moveVelocity, jumpImpulse);
        }
    }

    private void LateUpdate()
    {
        // Only InputAuthority needs to update camera, and only if NOT in vehicle
        if (!HasInputAuthority || _camera == null || IsInVehicle) return;

        Vector2 lookRotation = _kcc.GetLookRotation(true, false);
        float pitch = Mathf.Clamp(lookRotation.x, _cameraMinY, _cameraMaxY);
        _camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // FIX: Access the exposed public property IsMenuActive
        bool isMenuOpen = GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuActive;

        if (IsInVehicle || !isMenuOpen)
        {
            // Lock cursor for gameplay/driving
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        // If not driving, but menu is open, the GameMenuManager handles cursor

        // --- Rest of Update logic (like debug keys) ---
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.F3)) { /* ... Debug Log ... */ }
    }

    // File: PlayerController.cs

    public void SetInVehicle(NetworkBool inVehicle, NetworkId vehicleId = default, Vector3 exitPos = default)
    {
        IsInVehicle = inVehicle;
        VehicleNetworkId = vehicleId;

        // Find the vehicle object using the ID
        BasicVehicleController vehicle = null;

        // FIX: Use Runner.TryFindObject to get the NetworkObject, then GetComponent to get the script.
        if (inVehicle && Runner != null && VehicleNetworkId.IsValid)
        {
            NetworkObject vehicleObj;
            if (Runner.TryFindObject(VehicleNetworkId, out vehicleObj))
            {
                // Safely get the component once the NetworkObject is found
                vehicle = vehicleObj.GetComponent<BasicVehicleController>();
            }
        }
        _lastVehicleRef = vehicle; // Cache reference

        // --- Component Toggling ---
        if (HasInputAuthority)
        {
            _kcc.enabled = !inVehicle;
            if (_camera != null) _camera.enabled = !inVehicle;
        }

        // Toggle player model visibility
        if (_playerModel != null)
        {
            _playerModel.SetActive(!inVehicle);
        }

        // --- Teleport on Exit ---
        if (!inVehicle && HasInputAuthority)
        {
            _kcc.SetPosition(exitPos);
        }

        // Update the VehicleInteraction script
        if (_vehicleInteraction != null)
        {
            // Pass the found reference to the interaction script
            _vehicleInteraction.SetCurrentVehicle(vehicle);
        }

        Debug.Log($"Player {Object.InputAuthority} IsInVehicle set to: {inVehicle}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Debug.Log($"Player despawned - InputAuthority: {Object.InputAuthority}");
        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}