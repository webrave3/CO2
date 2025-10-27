using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

// *** KEEP THIS STRUCT DEFINITION HERE ***
// Added Vehicle/Interaction fields
public struct NetworkInputData : INetworkInput
{
    public float HorizontalInput;
    public float VerticalInput;
    public Vector2 MouseDelta;
    public NetworkBool Jump;
    public float VehicleSteer;    // Vehicle steering input (-1 to 1)
    public float VehicleThrottle; // Vehicle throttle/brake input (-1 to 1)
    public NetworkBool Use;           // Interaction button press
}


public class PlayerInput : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Input Settings")]
    [SerializeField] private float _mouseSensitivityMultiplier = 1.0f;
    [SerializeField] private bool _invertMouseY = false;

    [Header("Debug Options")]
    [SerializeField] private bool _debugInput = false;
    [SerializeField] private float _debugLogInterval = 3f;

    // Player Movement Inputs
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpInput;

    // --- ADDED Vehicle & Interaction Inputs ---
    private float _vehicleSteerInput;
    private float _vehicleThrottleInput;
    private bool _useInput;
    // --- End ADDED ---

    private NetworkRunner _runner;
    private float _lastLogTime = 0f;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        // Optional log: if (Application.isPlaying) { Debug.Log("PlayerInput initialized"); }
    }

    private void Start()
    {
        // Original Start logic - no changes needed
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!currentScene.Contains("Lobby") && !currentScene.Contains("MainMenu"))
        {
            // Debug.Log("PlayerInput detected game scene - cursor should be locked by PlayerController");
        }
    }

    private void Update()
    {
        if (!Application.isFocused) return;

        // Player Movement Input (Original)
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        float mouseX = Input.GetAxisRaw("Mouse X") * _mouseSensitivityMultiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * _mouseSensitivityMultiplier;
        if (_invertMouseY) mouseY = -mouseY;
        _lookInput = new Vector2(mouseX, mouseY);

        _jumpInput = Input.GetKey(KeyCode.Space);

        // --- ADDED Vehicle & Interaction Input ---
        _vehicleSteerInput = Input.GetAxisRaw("Horizontal"); // Using same axes
        _vehicleThrottleInput = Input.GetAxisRaw("Vertical"); // Using same axes
        _useInput = Input.GetKeyDown(KeyCode.E); // Use GetKeyDown for single press
        // --- End ADDED ---
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (runner == null || !runner.IsRunning || !runner.IsPlayer) return;

        var data = new NetworkInputData();

        // Player Movement (Original)
        data.HorizontalInput = _moveInput.x;
        data.VerticalInput = _moveInput.y;
        data.MouseDelta = _lookInput;
        data.Jump = _jumpInput;

        // --- ADDED Vehicle & Interaction ---
        data.VehicleSteer = _vehicleSteerInput;
        data.VehicleThrottle = _vehicleThrottleInput;
        data.Use = _useInput;
        // --- End ADDED ---

        input.Set(data);

        // --- ADDED: Reset single-press input ---
        _useInput = false; // Reset Use input after sending
        // --- End ADDED ---


        // Debug log (Original)
        if (_debugInput && (Time.time - _lastLogTime >= _debugLogInterval))
        {
            // Check if any significant input is happening
            if (_moveInput.magnitude > 0.1f || _lookInput.magnitude > 0.1f || data.Jump || Mathf.Abs(_vehicleSteerInput) > 0.1f || Mathf.Abs(_vehicleThrottleInput) > 0.1f || data.Use)
            {
                Debug.Log($"Input: Pl(H={data.HorizontalInput:F2}, V={data.VerticalInput:F2}, J={data.Jump}) Veh(S={data.VehicleSteer:F2}, T={data.VehicleThrottle:F2}) Use={data.Use}");
                _lastLogTime = Time.time;
            }
        }
    }

    // --- Keep empty implementations or add specific logic if needed ---
    // (Rest of INetworkRunnerCallbacks methods are empty as per original)
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { } // Corrected signature
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { } // Corrected signature
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