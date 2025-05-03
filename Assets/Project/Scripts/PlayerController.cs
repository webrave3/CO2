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
    [SerializeField] private float _lookSensitivity = 2f;
    [SerializeField] private float _jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private float _cameraMaxY = 80f;
    [SerializeField] private float _cameraMinY = -80f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    private bool _cursorLocked = false;

    // For jump
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
            // Disable camera for remote players
            if (_camera != null)
                _camera.gameObject.SetActive(false);
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

            // CAMERA ROTATION - For local player only, use SimpleKCC's methods
            if (HasInputAuthority && _cursorLocked && data.MouseDelta.magnitude > 0.01f)
            {
                // Apply mouse delta using SimpleKCC's AddLookRotation
                // NOTE: AddLookRotation takes (pitchDelta, yawDelta) - NOT the other way around!
                Vector2 lookDelta = data.MouseDelta * _lookSensitivity;

                // Correct parameter order: pitch first, yaw second
                // Note: Negative Y so mouse up = look up (standard FPS behavior)
                _kcc.AddLookRotation(-lookDelta.y, lookDelta.x);

                // Debug rotation if enabled
                if (_showDebugInfo)
                {
                    Vector2 currentRotation = _kcc.GetLookRotation(true, true);
                    Debug.Log($"Mouse Delta: {data.MouseDelta}, Applied: Pitch={-lookDelta.y:F2}, Yaw={lookDelta.x:F2}, Current: Pitch={currentRotation.x:F1}, Yaw={currentRotation.y:F1}");
                }
            }
        }
    }

    private void LateUpdate()
    {
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
                Debug.Log($"Camera pitch set to: {pitch:F1}");
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
    }

    private void LockCursor()
    {
        _cursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        _cursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}