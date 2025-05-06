using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

public class PlayerInput : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Input Settings")]
    [SerializeField] private float _mouseSensitivityMultiplier = 1.0f;
    [SerializeField] private bool _invertMouseY = false;

    [Header("Debug Options")]
    [SerializeField] private bool _debugInput = false;
    [SerializeField] private float _debugLogInterval = 3f;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpInput;

    private NetworkRunner _runner;
    private float _lastLogTime = 0f;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();

        if (Application.isPlaying)
        {
            Debug.Log("PlayerInput initialized");
        }
    }

    private void Start()
    {
        // Ensure the cursor is properly configured in the game scene
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!currentScene.Contains("Lobby") && !currentScene.Contains("MainMenu"))
        {
            Debug.Log("PlayerInput detected game scene - cursor should be locked by PlayerController");
        }
    }

    private void Update()
    {
        // Only collect input if the game is running
        if (!Application.isFocused) return;

        // Get direct input from Unity's Input system - these will be passed to network
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        // Explicitly track raw mouse movement with sensitivity applied
        float mouseX = Input.GetAxisRaw("Mouse X") * _mouseSensitivityMultiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * _mouseSensitivityMultiplier;

        // Apply inversion if needed
        if (_invertMouseY)
            mouseY = -mouseY;

        // Store raw delta for this frame
        _lookInput = new Vector2(mouseX, mouseY);

        _jumpInput = Input.GetKey(KeyCode.Space);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Make sure we have a valid runner and we're connected
        if (runner == null || !runner.IsRunning)
            return;

        // Only provide input if this is the local player
        if (!runner.IsPlayer)
            return;

        var data = new NetworkInputData();

        // Transfer input to networked struct
        data.HorizontalInput = _moveInput.x;
        data.VerticalInput = _moveInput.y;

        // Important: Pass the raw mouse delta, not accumulated rotation
        data.MouseDelta = _lookInput;

        data.Jump = _jumpInput;

        // Submit to network
        input.Set(data);

        // Debug output - throttled to avoid spam
        if (_debugInput && (_moveInput.magnitude > 0.1f || _lookInput.magnitude > 0.1f))
        {
            if (Time.time - _lastLogTime >= _debugLogInterval)
            {
                Debug.Log($"Submitting network input: H={data.HorizontalInput:F2}, V={data.VerticalInput:F2}, MouseX={_lookInput.x:F2}, MouseY={_lookInput.y:F2}");
                _lastLogTime = Time.time;
            }
        }
    }

    // Required INetworkRunnerCallbacks methods
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}