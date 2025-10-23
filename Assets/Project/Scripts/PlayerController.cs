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
        if (_kcc == null) _kcc = GetComponent<SimpleKCC>();
        if (_camera == null) _camera = GetComponentInChildren<Camera>();
        _vehicleInteraction = GetComponent<VehicleInteraction>();
    }

    public override void Spawned()
    {
        UnityEngine.Debug.Log($"Player Spawned - HasInputAuthority: {HasInputAuthority}, ID: {Object.InputAuthority}");

        if (HasInputAuthority)
        {
            // Initial camera and cursor setup 
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

        // Ensure KCC starts enabled (needed because it's disabled on exit/start)
        _kcc.enabled = !IsInVehicle;
    }

    public override void FixedUpdateNetwork()
    {
        // Only process input if NOT in a vehicle
        if (!IsInVehicle && GetInput(out NetworkInputData data))
        {
            // Apply KCC movement logic here (omitted for brevity, keep your original logic)
            _kcc.AddLookRotation(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * data.MouseDelta.x);
            // ... apply move velocity and jump impulse ...
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

        // FIX: Access the public property IsMenuActive
        bool isMenuOpen = GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuActive;

        if (IsInVehicle || !isMenuOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Called by BasicVehicleController RPC to update state across all clients
    public void SetInVehicle(NetworkBool inVehicle, NetworkId vehicleId = default, Vector3 exitPos = default)
    {
        IsInVehicle = inVehicle;
        VehicleNetworkId = vehicleId;

        BasicVehicleController vehicle = null;

        if (inVehicle && Runner != null && VehicleNetworkId.IsValid)
        {
            NetworkObject vehicleObj;
            // FIX: Use Runner.TryFindObject
            if (Runner.TryFindObject(VehicleNetworkId, out vehicleObj))
            {
                vehicle = vehicleObj.GetComponent<BasicVehicleController>();
            }
        }
        _lastVehicleRef = vehicle;

        if (HasInputAuthority)
        {
            _kcc.enabled = !inVehicle;
            // Disabling the player camera handles the switch. VehicleController enables its own camera.
            if (_camera != null) _camera.enabled = !inVehicle;
        }

        if (_playerModel != null)
        {
            _playerModel.SetActive(!inVehicle);
        }

        if (!inVehicle && HasInputAuthority)
        {
            _kcc.SetPosition(exitPos);
        }

        if (_vehicleInteraction != null)
        {
            _vehicleInteraction.SetCurrentVehicle(vehicle);
        }

        Debug.Log($"Player {Object.InputAuthority} IsInVehicle set to: {inVehicle}");
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
