using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;

    private void Awake()
    {
        // Cache the network runner at startup if it exists
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
        {
            Debug.LogError("NetworkRunner component not found. Please add it in the Inspector.");
        }
    }

    private void Start()
    {
        // Add a simple UI to start or join a session
        StartGameUI.OnCreateGame += OnCreateGame;
        StartGameUI.OnJoinGame += OnJoinGame;
    }

    private void OnDestroy()
    {
        StartGameUI.OnCreateGame -= OnCreateGame;
        StartGameUI.OnJoinGame -= OnJoinGame;
    }

    private async void OnCreateGame()
    {
        try
        {
            // Use the existing NetworkRunner component
            if (_runner == null)
            {
                Debug.LogError("NetworkRunner component not found.");
                return;
            }

            _runner.ProvideInput = true;

            Debug.Log("Starting game as Host...");

            // Get or add the NetworkSceneManagerDefault component
            NetworkSceneManagerDefault sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            // Start the game in host mode
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = "TestSession",
                SceneManager = sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            Debug.Log($"Host game started: {result.Ok}, Error: {result.ShutdownReason}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error starting host game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void OnJoinGame()
    {
        try
        {
            // Use the existing NetworkRunner component
            if (_runner == null)
            {
                Debug.LogError("NetworkRunner component not found.");
                return;
            }

            _runner.ProvideInput = true;

            Debug.Log("Joining game as Client...");

            // Get or add the NetworkSceneManagerDefault component
            NetworkSceneManagerDefault sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            // Start the game in client mode
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = "TestSession",
                SceneManager = sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            Debug.Log($"Client game started: {result.Ok}, Error: {result.ShutdownReason}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error joining game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Spawn the player
        if (runner.IsServer)
        {
            Debug.Log($"Spawning player: {player}");

            // Create a unique position for each player
            Vector3 spawnPosition = new Vector3((player.RawEncoded % 4) * 2, 1, 0);

            try
            {
                NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

                if (networkPlayerObject != null)
                {
                    Debug.Log($"Player {player} spawned successfully");
                    _spawnedCharacters.Add(player, networkPlayerObject);
                }
                else
                {
                    Debug.LogError($"Failed to spawn player {player}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception when spawning player: {ex.Message}");
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Find and remove the player
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
            Debug.Log($"Player {player} despawned and removed from dictionary");
        }
    }

    // Required but unused INetworkRunnerCallbacks methods
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Network shutdown: {shutdownReason}");
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server");
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connection failed: {reason}");
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("Scene load completed");
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}