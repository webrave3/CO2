using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 2f;
    [SerializeField] private float _jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private float _cameraMaxY = 80f;
    [SerializeField] private float _cameraMinY = -80f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = false;

    private bool _cursorLocked = false;
    private bool _jumpConsumed = false;

    private void Awake()
    {
        // Ensure required components
        if (_kcc == null)
            _kcc = GetComponent<SimpleKCC>();

        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
    }

    public override void Spawned()
    {
        Debug.Log($"Player Spawned - HasInputAuthority: {HasInputAuthority}, InputAuthority: {Object.InputAuthority}, Runner.LocalPlayer: {Runner.LocalPlayer}");

        if (HasInputAuthority)
        {
            Debug.Log("This is the local player, setting up input and camera");

            // Disable main camera if it exists
            if (Camera.main != null && Camera.main != _camera)
                Camera.main.gameObject.SetActive(false);

            // Enable our camera
            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                _camera.tag = "MainCamera";
            }

            // Lock cursor for FPS controls
            LockCursor();
        }
        else
        {
            Debug.Log($"This is a remote player (ID: {Object.InputAuthority}), disabling camera");
            // Disable camera for remote players
            if (_camera != null)
                _camera.gameObject.SetActive(false);
        }

        // Add delayed authority check to handle timing issues
        StartCoroutine(DelayedAuthorityCheck());
    }

    private IEnumerator DelayedAuthorityCheck()
    {
        yield return null; // Wait one frame
        Debug.Log($"Delayed Authority Check - HasInputAuth: {HasInputAuthority}, InputAuthority: {Object.InputAuthority}");

        if (HasInputAuthority)
        {
            // Ensure proper setup for local player
            if (_camera != null) _camera.gameObject.SetActive(true);
            LockCursor();
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Get network input data
        if (GetInput(out NetworkInputData data))
        {
            // MOVEMENT - Simple and direct
            Vector3 moveDirection = transform.right * data.HorizontalInput + transform.forward * data.VerticalInput;

            // Move and handle jumping
            HandleMovement(moveDirection, data.Jump);

            // CAMERA ROTATION - For local player only
            if (HasInputAuthority && _cursorLocked && data.MouseDelta.magnitude > 0.01f)
            {
                // Apply mouse delta using SimpleKCC's AddLookRotation
                Vector2 lookDelta = data.MouseDelta * _lookSensitivity;

                // Apply rotation to SimpleKCC
                _kcc.AddLookRotation(-lookDelta.y, lookDelta.x);

                // Debug rotation if enabled
                if (_showDebugInfo)
                {
                    Vector2 currentRotation = _kcc.GetLookRotation(true, true);
                    Debug.Log($"Mouse Delta: {data.MouseDelta}, Applied: Pitch={-lookDelta.y:F2}, Yaw={lookDelta.x:F2}, Current Rotation: {currentRotation}");
                }
            }
        }
    }

    public override void Render()
    {
        // Camera sync for local player only - uses interpolated/predicted values
        if (HasInputAuthority && _camera != null)
        {
            // Get the current pitch rotation from SimpleKCC
            Vector2 lookRotation = _kcc.GetLookRotation(true, false);
            float pitch = lookRotation.x;

            // Clamp pitch to prevent looking too far up/down
            pitch = Mathf.Clamp(pitch, _cameraMinY, _cameraMaxY);

            // Apply pitch to camera
            _camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (_showDebugInfo)
            {
                Debug.Log($"Render - Camera pitch: {pitch:F1}, HasInputAuth: {HasInputAuthority}");
            }
        }
    }

    private void HandleMovement(Vector3 moveDirection, bool jumpInput)
    {
        // Calculate jump impulse
        float jumpImpulse = 0f;

        if (_kcc.IsGrounded)
        {
            _jumpConsumed = false;
        }

        if (jumpInput && _kcc.IsGrounded && !_jumpConsumed)
        {
            jumpImpulse = _jumpForce;
            _jumpConsumed = true;
        }

        // Apply movement with SimpleKCC's Move method
        _kcc.Move(moveDirection.normalized * _moveSpeed, jumpImpulse);
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // Toggle cursor lock with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_cursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

        // Debug authority with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log($"Authority Check - HasInputAuth: {HasInputAuthority}, InputAuthority: {Object.InputAuthority}, LocalPlayer: {Runner.LocalPlayer}");
            Debug.Log($"Camera active: {_camera != null && _camera.gameObject.activeSelf}");
        }

        // Debug input with F2
        if (Input.GetKeyDown(KeyCode.F2))
        {
            _showDebugInfo = !_showDebugInfo;
            Debug.Log($"Debug info toggled: {_showDebugInfo}");
        }
    }

    private void LockCursor()
    {
        _cursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Cursor locked");
    }

    private void UnlockCursor()
    {
        _cursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Cursor unlocked");
    }

    // Called when the object is about to be despawned
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Debug.Log($"Player despawned - InputAuthority: {Object.InputAuthority}");
    }
}