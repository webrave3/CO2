using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using System;
using System.Collections;
using TMPro;

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
    private float _verticalLookRotation = 0f;  // Track vertical look angle

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

        // Create player name display
        UpdatePlayerDisplayName();

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
            // Calculate movement direction
            Vector3 moveDirection = new Vector3(data.HorizontalInput, 0, data.VerticalInput).normalized;

            // Apply movement to SimpleKCC
            Vector3 moveVelocity = transform.rotation * moveDirection * _moveSpeed;
            _kcc.Move(moveVelocity);

            // Debug any non-zero mouse input
            if (data.MouseDelta.magnitude > 0.01f)
            {
                Debug.Log($"Mouse input: X={data.MouseDelta.x:F2}, Y={data.MouseDelta.y:F2}");
            }

            // Only apply look rotation if cursor is locked
            if (_cursorLocked)
            {
                // Horizontal rotation (yaw) - rotate the whole character
                _kcc.AddLookRotation(new Vector2(data.MouseDelta.x * _lookSensitivity, 0));

                // Vertical rotation (pitch) - only rotate the camera
                if (_camera != null)
                {
                    // Update our tracked vertical angle
                    _verticalLookRotation -= data.MouseDelta.y * _lookSensitivity;

                    // Clamp to avoid over-rotation
                    _verticalLookRotation = Mathf.Clamp(_verticalLookRotation, -85f, 85f);

                    // Apply rotation to camera
                    _camera.transform.localRotation = Quaternion.Euler(_verticalLookRotation, 0, 0);
                }
            }
        }
    }

    private void UpdatePlayerDisplayName()
    {
        // Create a simple text display if none exists
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

        // Set the name
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void SetNetworkNameRpc(string name)
    {
        // This is just a placeholder in case we want to sync names later
        Debug.Log($"Player {Object.InputAuthority.PlayerId} set name to: {name}");
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

            // Make name display face the camera
            Transform nameDisplay = transform.Find("NameDisplay");
            if (nameDisplay != null && Camera.main != null)
            {
                nameDisplay.rotation = Camera.main.transform.rotation;
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