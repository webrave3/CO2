using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using System.Diagnostics;

public class DirectRotationTestController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _bodyTransform;

    [Header("Settings")]
    [SerializeField] private float _rotationSpeed = 2.0f;
    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private bool _useDirectRotation = true;

    private float _verticalRotation = 0f;
    private float _horizontalRotation = 0f;

    private void Awake()
    {
        if (_kcc == null) _kcc = GetComponent<SimpleKCC>();
        if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>();
        if (_bodyTransform == null) _bodyTransform = transform;

        // Force cursor lock at start
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            UnityEngine.Debug.Log("Test controller spawned - I am the local player");

            // Disable main camera if we have our own
            if (Camera.main != null && Camera.main != _playerCamera)
            {
                Camera.main.gameObject.SetActive(false);
            }

            // Enable our camera
            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(true);
            }
        }
        else
        {
            // Disable camera on remote players
            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(false);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            // Calculate movement
            Vector3 moveDirection = new Vector3(data.HorizontalInput, 0, data.VerticalInput).normalized;
            Quaternion moveRotation = Quaternion.Euler(0, _horizontalRotation, 0);
            Vector3 moveVelocity = moveRotation * moveDirection * _moveSpeed;

            // Apply movement
            _kcc.Move(moveVelocity);

            // Handle rotation differently based on setting
            if (_useDirectRotation)
            {
                ApplyDirectRotation(data.MouseDelta);
            }
            else
            {
                ApplyKCCRotation(data.MouseDelta);
            }
        }
    }

    private void ApplyDirectRotation(Vector2 mouseDelta)
    {
        if (HasInputAuthority && mouseDelta.magnitude > 0.01f)
        {
            // Update rotation angles
            _horizontalRotation += mouseDelta.x * _rotationSpeed;
            _verticalRotation -= mouseDelta.y * _rotationSpeed;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -89f, 89f);

            // Apply horizontal rotation directly to the transform
            _bodyTransform.rotation = Quaternion.Euler(0, _horizontalRotation, 0);

            // Apply vertical rotation to camera only
            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0, 0);
            }

            UnityEngine.Debug.Log($"Direct rotation applied - H: {_horizontalRotation:F1}, V: {_verticalRotation:F1}");
        }
    }

    private void ApplyKCCRotation(Vector2 mouseDelta)
    {
        if (HasInputAuthority && mouseDelta.magnitude > 0.01f)
        {
            // Get current rotation
            Vector2 currentLookRotation = _kcc.GetLookRotation(true, false);

            // Calculate new rotation
            float newYaw = currentLookRotation.y + (mouseDelta.x * _rotationSpeed);
            _verticalRotation -= mouseDelta.y * _rotationSpeed;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -89f, 89f);

            // Apply to KCC
            _kcc.SetLookRotation(new Vector2(_verticalRotation, newYaw));

            // Make sure camera matches
            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0, 0);
            }

            UnityEngine.Debug.Log($"KCC rotation applied - Yaw: {newYaw:F1}, Pitch: {_verticalRotation:F1}");
        }
    }

    private void Update()
    {
        // Toggle cursor lock with Escape
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // Toggle rotation method with T
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.T))
        {
            _useDirectRotation = !_useDirectRotation;
            UnityEngine.Debug.Log($"Rotation method changed: {(_useDirectRotation ? "Direct" : "KCC")}");
        }

        // Print debug info with I
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.I))
        {
            UnityEngine.Debug.Log($"ROTATION DEBUG: H={_horizontalRotation:F1}, V={_verticalRotation:F1}");
            UnityEngine.Debug.Log($"KCC Look Rotation: {_kcc.GetLookRotation(true, false)}");
            UnityEngine.Debug.Log($"Transform Rotation: {transform.eulerAngles}");
            UnityEngine.Debug.Log($"Camera Local Rotation: {_playerCamera.transform.localRotation.eulerAngles}");
        }
    }
}