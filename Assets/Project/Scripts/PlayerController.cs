using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;

public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 0.5f; // Reduced sensitivity
    [SerializeField] private float _jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private float _cameraMaxY = 80f;
    [SerializeField] private float _cameraMinY = -80f;

    private bool _jumpConsumed = false;

    private void Awake()
    {
        // Ensure required components
        if (_kcc == null)
            _kcc = GetComponent<SimpleKCC>();

        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
    }

    // Add this inside the Spawned method of PlayerController.cs, after camera activation

    public override void Spawned()
    {
        UnityEngine.Debug.Log($"Player Spawned - HasInputAuthority: {HasInputAuthority}, ID: {Object.InputAuthority}");

        // Configure based on authority
        if (HasInputAuthority)
        {
            UnityEngine.Debug.Log("This is the local player");

            // Disable main camera if it exists
            if (Camera.main != null && Camera.main != _camera)
                Camera.main.gameObject.SetActive(false);

            // Enable our camera
            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                _camera.tag = "MainCamera";

                // Add session info display to camera if not already present
                if (!_camera.GetComponent<SessionInfoDisplay>())
                {
                    SessionInfoDisplay infoDisplay = _camera.gameObject.AddComponent<SessionInfoDisplay>();
                    UnityEngine.Debug.Log("Added SessionInfoDisplay to player camera");
                }
            }

            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            UnityEngine.Debug.Log($"This is a remote player (ID: {Object.InputAuthority})");
            // Disable camera for remote players
            if (_camera != null)
                _camera.gameObject.SetActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only process input for objects with Input Authority
        if (GetInput(out NetworkInputData data))
        {
            // Apply look rotation with lower sensitivity
            _kcc.AddLookRotation(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * _lookSensitivity);

            // Calculate movement direction
            Vector3 moveDirection = _kcc.TransformRotation * new Vector3(data.HorizontalInput, 0.0f, data.VerticalInput);
            Vector3 moveVelocity = moveDirection.normalized * _moveSpeed;

            // Handle jumping
            float jumpImpulse = 0f;
            if (_kcc.IsGrounded)
            {
                _jumpConsumed = false;
            }

            if (data.Jump && _kcc.IsGrounded && !_jumpConsumed)
            {
                jumpImpulse = _jumpForce;
                _jumpConsumed = true;
            }

            // Apply movement with gravity - SimpleKCC handles gravity internally when enabled in settings
            _kcc.Move(moveVelocity, jumpImpulse);
        }
    }

    // Camera sync MUST happen in LateUpdate
    private void LateUpdate()
    {
        // Only InputAuthority needs to update camera
        if (!HasInputAuthority || _camera == null)
            return;

        // Get look rotation from KCC
        Vector2 lookRotation = _kcc.GetLookRotation(true, false);

        // Apply pitch to camera with proper clamping
        float pitch = Mathf.Clamp(lookRotation.x, _cameraMinY, _cameraMaxY);
        _camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // Handle cursor management
    private void Update()
    {
        // Only for local player
        if (!HasInputAuthority) return;

        // Debug key functionality preserved
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.F3))
        {
            UnityEngine.Debug.Log($"==== PLAYER DEBUG ====");
            UnityEngine.Debug.Log($"Player ID: {Object.InputAuthority.PlayerId}");
            UnityEngine.Debug.Log($"HasInputAuthority: {HasInputAuthority}");
            UnityEngine.Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");

            // Get session info from GameStateManager
            GameStateManager gsm = FindObjectOfType<GameStateManager>();
            if (gsm != null)
            {
                UnityEngine.Debug.Log($"Game State: {gsm.State}");
                UnityEngine.Debug.Log($"Session: {gsm.SessionDisplayName} | {gsm.SessionHash}");
                UnityEngine.Debug.Log($"Players Ready: {gsm.PlayersReady.Count}");
            }

            // Log position and rotation
            UnityEngine.Debug.Log($"Position: {transform.position}");
            UnityEngine.Debug.Log($"Rotation: {transform.rotation.eulerAngles}");
            UnityEngine.Debug.Log("======================");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Debug.Log($"Player despawned - InputAuthority: {Object.InputAuthority}");

        // Cleanup if needed
        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}