using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using System;
using System.Collections;
using TMPro;

public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;
    [SerializeField] private GameObject _lobbyVisuals;
    [SerializeField] private GameObject _gameplayVisuals;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 2f;
    [SerializeField] private float _maxUpAngle = 85f;
    [SerializeField] private float _maxDownAngle = 85f;

    [Header("Debug Settings")]
    [SerializeField] private bool _forceGameMode = true;
    [SerializeField] private bool _debugRotation = true;

    [Networked]
    public NetworkBool IsInLobby { get; set; } = true;

    private bool _cursorLocked = false;
    private Vector2 _currentLookRotation = Vector2.zero; // x=pitch, y=yaw
    private PlayerReadyUI _playerReadyUI;

    private void Awake()
    {
        // Verify required components
        if (_kcc == null)
        {
            Debug.LogError("SimpleKCC reference missing! Attempting to find component.");
            _kcc = GetComponent<SimpleKCC>();

            if (_kcc == null)
            {
                Debug.LogError("Failed to find SimpleKCC component!");
                enabled = false;
                return;
            }
        }

        if (_camera == null)
        {
            Debug.LogError("Camera reference missing! Attempting to find in children.");
            _camera = GetComponentInChildren<Camera>();

            if (_camera == null)
            {
                Debug.LogError("Failed to find Camera component!");
            }
        }

        // Set up visuals
        if (_lobbyVisuals == null)
            Debug.LogWarning("Lobby visuals reference not set");

        if (_gameplayVisuals == null)
            Debug.LogWarning("Gameplay visuals reference not set");

        // Get or add PlayerReadyUI
        _playerReadyUI = GetComponent<PlayerReadyUI>();
        if (_playerReadyUI == null && Application.isPlaying)
        {
            _playerReadyUI = gameObject.AddComponent<PlayerReadyUI>();
        }
    }

    public override void Spawned()
    {
        Debug.Log($"Player spawned - HasInputAuthority: {HasInputAuthority}, HasStateAuthority: {HasStateAuthority}");

        // Force game mode if enabled (for testing)
        if (_forceGameMode)
        {
            IsInLobby = false;
            Debug.Log("FORCED GAME MODE: Setting IsInLobby to false");
        }
        else
        {
            // Determine state based on scene name
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            IsInLobby = currentScene.Contains("Lobby");
            Debug.Log($"Scene: {currentScene}, IsInLobby: {IsInLobby}");
        }

        // Update visuals based on state
        UpdateVisuals();

        // Create name display
        SetupNameDisplay();

        if (HasInputAuthority)
        {
            Debug.Log("This is the local player, setting up camera and input");

            // Disable main camera if it exists and isn't our camera
            if (Camera.main != null && Camera.main != _camera)
            {
                Camera.main.gameObject.SetActive(false);
            }

            // Enable our camera
            if (_camera != null)
            {
                _camera.gameObject.SetActive(true);
                Debug.Log("Player camera activated");
            }

            // Set cursor state based on game mode
            if (IsInLobby)
            {
                UnlockCursor();
                Debug.Log("Cursor unlocked due to lobby state");
            }
            else
            {
                LockCursor();
                Debug.Log("Cursor locked due to game state");
            }

            // Initialize rotation
            _currentLookRotation = Vector2.zero;
            _kcc.SetLookRotation(_currentLookRotation);
            Debug.Log($"Initial look rotation: {_currentLookRotation}");
        }
        else
        {
            // Disable camera for remote players
            if (_camera != null)
            {
                _camera.gameObject.SetActive(false);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Process network input
        if (GetInput(out NetworkInputData data))
        {
            // Handle movement with SimpleKCC
            ProcessMovement(data);

            // Handle rotation with SimpleKCC
            ProcessRotation(data);
        }
    }

    private void ProcessMovement(NetworkInputData data)
    {
        // Create movement direction vector from input
        Vector3 moveDirection = new Vector3(data.HorizontalInput, 0, data.VerticalInput).normalized;

        // Transform movement direction based on character's current yaw rotation
        // Only use yaw (horizontal rotation) for movement
        Vector2 currentRotation = _kcc.GetLookRotation(false, true);
        float yaw = currentRotation.y;
        Quaternion yawRotation = Quaternion.Euler(0, yaw, 0);
        Vector3 worldMoveDirection = yawRotation * moveDirection;

        // Apply movement velocity
        _kcc.Move(worldMoveDirection * _moveSpeed);

        // Debug movement if significant
        if (moveDirection.magnitude > 0.1f)
        {
            Debug.Log($"Moving: local={moveDirection}, yaw={yaw:F1}, world={worldMoveDirection}, velocity={worldMoveDirection * _moveSpeed}");
        }
    }

    private void ProcessRotation(NetworkInputData data)
    {
        // Only process rotation if cursor is locked
        if (!_cursorLocked)
        {
            if (data.MouseDelta.magnitude > 0.01f)
            {
                Debug.Log($"Mouse input detected ({data.MouseDelta}), but cursor is not locked. Rotation not applied.");

                // If we're in game mode but cursor isn't locked, fix it
                if (!IsInLobby && HasInputAuthority)
                {
                    LockCursor();
                }
            }
            return;
        }

        // Update rotation values (Vector2: x=pitch, y=yaw)
        _currentLookRotation.x += data.MouseDelta.y * _lookSensitivity; // Pitch
        _currentLookRotation.y += data.MouseDelta.x * _lookSensitivity; // Yaw

        // Clamp pitch to prevent looking too far up or down
        _currentLookRotation.x = Mathf.Clamp(_currentLookRotation.x, -_maxDownAngle, _maxUpAngle);

        // Apply to SimpleKCC (pitch, yaw)
        _kcc.SetLookRotation(_currentLookRotation);

        // Debug rotation values if enabled
        if (_debugRotation && data.MouseDelta.magnitude > 0.01f)
        {
            Debug.Log($"Mouse Delta: {data.MouseDelta}, Pitch: {_currentLookRotation.x}, Yaw: {_currentLookRotation.y}");
        }
    }

    private void SetupNameDisplay()
    {
        // Create a simple text display if needed
        Transform nameDisplayTransform = transform.Find("NameDisplay");
        TextMeshPro nameText = null;

        if (nameDisplayTransform == null)
        {
            GameObject nameDisplay = new GameObject("NameDisplay");
            nameDisplay.transform.SetParent(transform);
            nameDisplay.transform.localPosition = new Vector3(0, 2f, 0);
            nameDisplay.transform.localRotation = Quaternion.identity;

            nameText = nameDisplay.AddComponent<TextMeshPro>();
            nameText.fontSize = 5;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.rectTransform.sizeDelta = new Vector2(5, 1);
        }
        else
        {
            nameText = nameDisplayTransform.GetComponent<TextMeshPro>();
        }

        // Set name text
        if (nameText != null)
        {
            string playerName = "Player" + Runner.LocalPlayer.PlayerId;

            if (HasInputAuthority)
            {
                playerName = PlayerPrefs.GetString("PlayerName", playerName);
                nameText.text = playerName + "\n(YOU)";
                nameText.color = Color.green;
            }
            else
            {
                nameText.text = playerName;
                nameText.color = Color.white;
            }
        }
    }

    private void Update()
    {
        if (HasInputAuthority)
        {
            // Monitor cursor state consistency
            if (!IsInLobby && Cursor.lockState != CursorLockMode.Locked && _cursorLocked == false)
            {
                Debug.LogWarning("Detected cursor inconsistency - cursor should be locked in game mode.");
                LockCursor();
            }

            // Toggle cursor lock with Escape key in game mode
            if (!IsInLobby && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_cursorLocked)
                    UnlockCursor();
                else
                    LockCursor();
            }

            // Key for debugging state - useful for troubleshooting
            if (Input.GetKeyDown(KeyCode.L))
            {
                Debug.Log($"STATE: IsInLobby={IsInLobby}, CursorLocked={_cursorLocked}, " +
                          $"CursorState={Cursor.lockState}, LookRotation={_currentLookRotation}");

                // Force game mode with L+G combo
                if (Input.GetKey(KeyCode.G))
                {
                    IsInLobby = false;
                    UpdateVisuals();
                    LockCursor();
                    Debug.LogWarning("FORCE APPLIED: Game mode activated, cursor locked.");
                }
            }

            // Update 3D text to face camera
            Transform nameDisplay = transform.Find("NameDisplay");
            if (nameDisplay != null && Camera.main != null)
            {
                nameDisplay.rotation = Camera.main.transform.rotation;
            }
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

    private void UpdateVisuals()
    {
        if (_lobbyVisuals != null)
            _lobbyVisuals.SetActive(IsInLobby);

        if (_gameplayVisuals != null)
            _gameplayVisuals.SetActive(!IsInLobby);
    }

    // Called when game state changes to Playing
    public void OnGameStart()
    {
        Debug.Log("OnGameStart called - transitioning from lobby to game");
        IsInLobby = false;
        UpdateVisuals();

        if (HasInputAuthority)
        {
            LockCursor();
        }
    }

    // Utility method to reset camera rotation
    public void ResetCameraRotation()
    {
        if (HasInputAuthority)
        {
            // Reset rotation values
            _currentLookRotation = Vector2.zero;
            _kcc.SetLookRotation(_currentLookRotation);

            Debug.Log("Camera rotation reset");
        }
    }

    // Utility method to force cursor state
    public void ForceFixCursorState()
    {
        if (HasInputAuthority)
        {
            if (IsInLobby)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }

            Debug.Log($"Force fixed cursor state: IsInLobby={IsInLobby}, CursorLocked={_cursorLocked}");
        }
    }
}