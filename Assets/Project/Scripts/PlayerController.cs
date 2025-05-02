using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 2f;

    private bool _cursorLocked = false;

    private void Awake()
    {
        // Verify components
        if (_kcc == null)
        {
            Debug.LogError("SimpleKCC component not assigned to PlayerController");
            _kcc = GetComponent<SimpleKCC>();
        }

        if (_camera == null)
        {
            Debug.LogError("Camera not assigned to PlayerController");
            _camera = GetComponentInChildren<Camera>();
        }
    }

    public override void Spawned()
    {
        Debug.Log($"Player spawned - HasInputAuthority: {HasInputAuthority}, HasStateAuthority: {HasStateAuthority}");

        if (HasInputAuthority)
        {
            // Disable main camera and enable player camera
            if (Camera.main != null && Camera.main != _camera)
            {
                Camera.main.gameObject.SetActive(false);
            }

            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                Debug.Log("Player camera activated");
            }

            LockCursor();
        }
        else
        {
            if (_camera != null)
            {
                _camera.gameObject.SetActive(false);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // Debug log input data
            if (data.HorizontalInput != 0 || data.VerticalInput != 0 || data.MouseDelta.sqrMagnitude > 0)
            {
                Debug.Log($"Network Input: Move({data.HorizontalInput}, {data.VerticalInput}), Mouse({data.MouseDelta.x}, {data.MouseDelta.y})");
            }

            // Calculate movement direction
            Vector3 moveDirection = new Vector3(data.HorizontalInput, 0, data.VerticalInput).normalized;

            // Apply movement to SimpleKCC
            Vector3 moveVelocity = transform.rotation * moveDirection * _moveSpeed;
            _kcc.Move(moveVelocity);

            // Apply look rotation
            _kcc.AddLookRotation(new Vector2(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * _lookSensitivity));

            // Debug log movement
            if (moveDirection.sqrMagnitude > 0)
            {
                Debug.Log($"Moving: {moveVelocity}, Rotation: {transform.rotation.eulerAngles}");
            }
        }
    }

    public void LateUpdate()
    {
        // Only update camera for the local player
        if (HasInputAuthority && _camera != null)
        {
            // Get current pitch rotation from KCC for camera
            Vector2 lookRotation = _kcc.GetLookRotation(true, false);
            _camera.transform.localRotation = Quaternion.Euler(lookRotation.x, 0, 0);
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

    private void Update()
    {
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.Escape))
        {
            if (_cursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }
    }
}