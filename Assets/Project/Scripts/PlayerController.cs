using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _lookSensitivity = 2f;
    [SerializeField] private GameObject _lobbyModel;
    [SerializeField] private GameObject _gameplayModel;

    [Networked]
    public NetworkBool IsInLobby { get; set; } = true;

    private bool _cursorLocked = false;
    private PlayerReadyUI _playerReadyUI;

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

        _playerReadyUI = GetComponent<PlayerReadyUI>();
        if (_playerReadyUI == null && Application.isPlaying)
        {
            _playerReadyUI = gameObject.AddComponent<PlayerReadyUI>();
        }
    }

    public override void Spawned()
    {
        Debug.Log($"Player spawned - HasInputAuthority: {HasInputAuthority}, HasStateAuthority: {HasStateAuthority}");

        // Determine if we're in the lobby or game scene
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        IsInLobby = currentScene.Contains("Lobby");

        // Update player models based on scene
        UpdatePlayerVisuals();

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

            // In lobby, cursor should be visible for UI interaction
            if (IsInLobby)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
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
            // Apply gameplay input only if the cursor is locked (active gameplay)
            if (_cursorLocked || IsInLobby)
            {
                // Calculate movement direction
                Vector3 moveDirection = new Vector3(data.HorizontalInput, 0, data.VerticalInput).normalized;

                // Apply movement to SimpleKCC
                Vector3 moveVelocity = transform.rotation * moveDirection * _moveSpeed;
                _kcc.Move(moveVelocity);

                // Apply look rotation (only if cursor is locked)
                if (_cursorLocked)
                {
                    _kcc.AddLookRotation(new Vector2(-data.MouseDelta.y * _lookSensitivity, data.MouseDelta.x * _lookSensitivity));
                }
            }
        }
    }

    public void LateUpdate()
    {
        // Only update camera for the local player
        if (HasInputAuthority && _camera != null && _cursorLocked)
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
        if (HasInputAuthority)
        {
            // In the lobby, we want the cursor to be visible for UI interaction
            // But in the game, we toggle with Escape
            if (!IsInLobby && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_cursorLocked)
                    UnlockCursor();
                else
                    LockCursor();
            }
        }
    }

    public void UpdatePlayerVisuals()
    {
        if (_lobbyModel != null)
            _lobbyModel.SetActive(IsInLobby);

        if (_gameplayModel != null)
            _gameplayModel.SetActive(!IsInLobby);
    }

    // Called when the game state changes from Lobby to Playing
    public void OnGameStart()
    {
        IsInLobby = false;
        UpdatePlayerVisuals();

        if (HasInputAuthority)
        {
            LockCursor();
        }
    }
}