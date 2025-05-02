using UnityEngine;
using Fusion;
using UnityEngine.InputSystem;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(NetworkRunner))]
public class PlayerInput : MonoBehaviour, INetworkRunnerCallbacks
{
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpInput;

    // These methods will be called automatically by the Input System
    // Make sure they match the names in your Input Actions asset
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
        Debug.Log($"Move input: {_moveInput}");
    }

    public void OnLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();
        Debug.Log($"Look input: {_lookInput}");
    }

    public void OnJump(InputValue value)
    {
        _jumpInput = value.isPressed;
        Debug.Log($"Jump input: {_jumpInput}");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // Transfer the gathered input to our networked input struct
        data.HorizontalInput = _moveInput.x;
        data.VerticalInput = _moveInput.y;
        data.MouseDelta = _lookInput;
        data.Jump = _jumpInput;

        // Submit to Fusion network
        input.Set(data);
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