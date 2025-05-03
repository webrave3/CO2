using UnityEngine;
using Fusion;
using System.Diagnostics;

/// <summary>
/// A utility tool to diagnose and fix common state issues in the game.
/// Attach to any object in the scene that persists.
/// </summary>
public class StateDebugTool : MonoBehaviour
{
    [Header("Hotkeys")]
    [SerializeField] private KeyCode _fixCursorKey = KeyCode.F9;
    [SerializeField] private KeyCode _forceGameModeKey = KeyCode.F10;
    [SerializeField] private KeyCode _printStateKey = KeyCode.F11;
    [SerializeField] private KeyCode _resetCameraKey = KeyCode.F12;

    [Header("Debug UI")]
    [SerializeField] private bool _showDebugUI = true;
    [SerializeField] private Color _uiBackgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private Color _uiTextColor = Color.white;

    private Rect _windowRect = new Rect(10, 10, 300, 180);
    private int _windowID = 100;
    private string _statusMessage = "Debug tool ready";
    private float _messageTime;

    private PlayerController _localPlayer;
    private GameStateManager _gameStateManager;

    private void Start()
    {
        DontDestroyOnLoad(gameObject);

        _messageTime = Time.time;
        UnityEngine.Debug.Log("State Debug Tool initialized. Press F9 to fix cursor, F10 to force game mode, F11 for state info.");
    }

    private void Update()
    {
        // Find references if needed
        if (_localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (_gameStateManager == null)
        {
            FindGameStateManager();
        }

        // Process input
        if (Input.GetKeyDown(_fixCursorKey))
        {
            FixCursorState();
        }

        if (Input.GetKeyDown(_forceGameModeKey))
        {
            ForceGameMode();
        }

        if (Input.GetKeyDown(_printStateKey))
        {
            PrintState();
        }

        if (Input.GetKeyDown(_resetCameraKey))
        {
            ResetCamera();
        }
    }

    private void OnGUI()
    {
        if (_showDebugUI)
        {
            _windowRect = GUI.Window(_windowID, _windowRect, DoDebugWindow, "Game State Debug");
        }
    }

    private void DoDebugWindow(int windowID)
    {
        // Set up styles
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = _uiTextColor;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.normal.textColor = _uiTextColor;

        GUI.backgroundColor = _uiBackgroundColor;

        // Status and info
        GUILayout.Label($"Status: {_statusMessage}", labelStyle);
        GUILayout.Label($"Local Player Found: {(_localPlayer != null ? "Yes" : "No")}", labelStyle);
        GUILayout.Label($"Game State Manager Found: {(_gameStateManager != null ? "Yes" : "No")}", labelStyle);

        if (_localPlayer != null)
        {
            GUILayout.Label($"Is In Lobby: {_localPlayer.IsInLobby}", labelStyle);
            GUILayout.Label($"Cursor Locked: {(Cursor.lockState == CursorLockMode.Locked)}", labelStyle);
        }

        if (_gameStateManager != null)
        {
            GUILayout.Label($"Game State: {_gameStateManager.State}", labelStyle);
        }

        // Buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fix Cursor", buttonStyle))
        {
            FixCursorState();
        }

        if (GUILayout.Button("Force Game Mode", buttonStyle))
        {
            ForceGameMode();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Print State", buttonStyle))
        {
            PrintState();
        }

        if (GUILayout.Button("Reset Camera", buttonStyle))
        {
            ResetCamera();
        }
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    private void FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                _localPlayer = player;
                UpdateStatus("Found local player");
                return;
            }
        }
    }

    private void FindGameStateManager()
    {
        _gameStateManager = FindObjectOfType<GameStateManager>();
        if (_gameStateManager != null)
        {
            UpdateStatus("Found GameStateManager");
        }
    }

    private void FixCursorState()
    {
        if (_localPlayer != null)
        {
            _localPlayer.ForceFixCursorState();
            UpdateStatus("Fixed cursor state");
        }
        else
        {
            // Direct fix if no player found
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            UpdateStatus("Direct cursor lock applied");
        }
    }

    private void ForceGameMode()
    {
        if (_localPlayer != null)
        {
            // Set player to game mode
            if (_localPlayer.IsInLobby)
            {
                _localPlayer.OnGameStart();
                UpdateStatus("Forced player to game mode");
            }
            else
            {
                UpdateStatus("Player already in game mode");
            }
        }

        if (_gameStateManager != null)
        {
            // Update game state
            if (_gameStateManager.State != GameState.Playing)
            {
                _gameStateManager.ForcePlayingState();
                UpdateStatus("Forced game state to Playing");
            }
        }
    }

    private void PrintState()
    {
        UnityEngine.Debug.LogWarning("====== GAME STATE DEBUG INFO ======");
        UnityEngine.Debug.Log($"Cursor State: Visible={Cursor.visible}, LockState={Cursor.lockState}");

        if (_localPlayer != null)
        {
            UnityEngine.Debug.Log($"Local Player State: IsInLobby={_localPlayer.IsInLobby}");
        }
        else
        {
            UnityEngine.Debug.LogError("No local player found!");
        }

        if (_gameStateManager != null)
        {
            _gameStateManager.PrintCurrentState();
        }
        else
        {
            UnityEngine.Debug.LogError("No GameStateManager found!");
        }

        UnityEngine.Debug.LogWarning("====== END DEBUG INFO ======");
        UpdateStatus("State information printed to console");
    }

    private void ResetCamera()
    {
        if (_localPlayer != null)
        {
            _localPlayer.ResetCameraRotation();
            UpdateStatus("Camera rotation reset");
        }
        else
        {
            UpdateStatus("No player found to reset camera");
        }
    }

    private void UpdateStatus(string message)
    {
        _statusMessage = message;
        _messageTime = Time.time;
        UnityEngine.Debug.Log($"Debug Tool: {message}");
    }
}