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

    // Player Movement Inputs
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpInput;

    // Vehicle & Interaction Inputs
    private float _vehicleSteerInput;
    private float _vehicleThrottleInput;
    private bool _useInput;

    private NetworkRunner _runner;
    private float _lastLogTime = 0f;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        if (Application.isPlaying) { Debug.Log("PlayerInput initialized"); }
    }

    private void Start()
    {
        // Initialization logic here
    }

    private void Update()
    {
        if (!Application.isFocused) return;

        // Player Movement Input
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        float mouseX = Input.GetAxisRaw("Mouse X") * _mouseSensitivityMultiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * _mouseSensitivityMultiplier;
        if (_invertMouseY) mouseY = -mouseY;
        _lookInput = new Vector2(mouseX, mouseY);

        _jumpInput = Input.GetKey(KeyCode.Space);

        // Vehicle & Interaction Input
        _vehicleSteerInput = Input.GetAxisRaw("Horizontal");
        _vehicleThrottleInput = Input.GetAxisRaw("Vertical");

        _useInput = Input.GetKeyDown(KeyCode.E);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (runner == null || !runner.IsRunning || !runner.IsPlayer) return;

        var data = new NetworkInputData();

        // Player Movement
        data.HorizontalInput = _moveInput.x;
        data.VerticalInput = _moveInput.y;
        data.MouseDelta = _lookInput;
        data.Jump = _jumpInput;

        // Vehicle & Interaction
        data.VehicleSteer = _vehicleSteerInput;
        data.VehicleThrottle = _vehicleThrottleInput;
        data.Use = _useInput;

        input.Set(data);

        _useInput = false;

        if (_debugInput && (Time.time - _lastLogTime >= _debugLogInterval))
        {
            if (_moveInput.magnitude > 0.1f || _lookInput.magnitude > 0.1f || Mathf.Abs(_vehicleSteerInput) > 0.1f || Mathf.Abs(_vehicleThrottleInput) > 0.1f)
            {
                Debug.Log($"Input: Pl(H={data.HorizontalInput:F2}, V={data.VerticalInput:F2}) Veh(S={data.VehicleSteer:F2}, T={data.VehicleThrottle:F2}) Use={data.Use}");
                _lastLogTime = Time.time;
            }
        }
    }
    // ... (rest of INetworkRunnerCallbacks methods) ...
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
