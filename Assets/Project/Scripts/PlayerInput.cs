using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

public class PlayerInput : MonoBehaviour, INetworkRunnerCallbacks
{
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpInput;

    private NetworkRunner _runner;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
    }

    private void Update()
    {
        // Get direct input from Unity's Input system
        _moveInput.x = Input.GetAxis("Horizontal");
        _moveInput.y = Input.GetAxis("Vertical");

        // Explicitly track raw mouse movement regardless of cursor state
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        _lookInput = new Vector2(mouseX, mouseY);

        // Force debug output for mouse movement
        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            Debug.Log($"RAW MOUSE: X={mouseX:F3}, Y={mouseY:F3}");
        }

        _jumpInput = Input.GetKey(KeyCode.Space);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Make sure we have a valid runner
        if (runner == null || !runner.IsRunning)
            return;

        var data = new NetworkInputData();

        // Transfer input to networked struct
        data.HorizontalInput = _moveInput.x;
        data.VerticalInput = _moveInput.y;
        data.MouseDelta = _lookInput;
        data.Jump = _jumpInput;

        // Submit to network
        input.Set(data);

        // Debug output
        if (_moveInput.magnitude > 0.1f || _lookInput.magnitude > 0.1f)
        {
            Debug.Log($"Submitting network input: H={data.HorizontalInput:F2}, V={data.VerticalInput:F2}, MouseX={_lookInput.x:F2}, MouseY={_lookInput.y:F2}");
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