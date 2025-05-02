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
    [SerializeField] private NetworkPrefabRef _gameStateManagerPrefab;
    [SerializeField] private string _lobbySceneName = "Lobby";
    [SerializeField] private string _gameSceneName = "Game";

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;

    private void Awake()
    {
        // Cache the network runner at startup if it exists
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
        {
            Debug.LogError("NetworkRunner component not found. Please add it in the Inspector.");
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null)
        {
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
    }

    public async Task StartHostGame(string sessionName)
    {
        try
        {
            _runner.ProvideInput = true;

            Debug.Log($"Starting game as Host with session name: {sessionName}");

            // Start the game in host mode
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = sessionName,
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Host game started successfully");
                await LoadScene(_lobbySceneName);
            }
            else
            {
                Debug.LogError($"Failed to start host game: {result.ShutdownReason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error starting host game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public async Task StartClientGame(string sessionName)
    {
        try
        {
            _runner.ProvideInput = true;

            Debug.Log($"Joining game as Client with session name: {sessionName}");

            // Start the game in client mode
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Client game started successfully");
                // Client will receive scene load instructions from host
            }
            else
            {
                Debug.LogError($"Failed to join game: {result.ShutdownReason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error joining game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public async Task LoadScene(string sceneName)
    {
        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log($"Loading scene: {sceneName}");

            // Use the runner's LoadScene method directly
            await _runner.LoadScene(sceneName);
        }
    }

    public async Task ShutdownGame()
    {
        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log("Shutting down network session");
            await _runner.Shutdown();
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined");

        // If we're in the lobby scene and the player doesn't have a character yet, spawn one
        if (SceneManager.GetActiveScene().name == _lobbySceneName ||
            SceneManager.GetActiveScene().name == _gameSceneName)
        {
            // Spawn the player
            if (runner.IsServer)
            {
                Debug.Log($"Spawning player: {player}");

                // Find a spawn point
                Transform spawnPoint = GetSpawnPoint();
                Vector3 spawnPosition = (spawnPoint != null) ? spawnPoint.position : new Vector3((player.RawEncoded % 4) * 2, 1, 0);
                Quaternion spawnRotation = (spawnPoint != null) ? spawnPoint.rotation : Quaternion.identity;

                try
                {
                    NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, spawnRotation, player);

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

                // If we're the first player to join and we don't have a GameStateManager yet, spawn one
                if (runner.IsServer && FindObjectOfType<GameStateManager>() == null)
                {
                    Debug.Log("Spawning GameStateManager");
                    runner.Spawn(_gameStateManagerPrefab);
                }
            }
        }
    }

    private Transform GetSpawnPoint()
    {
        // Find a spawn point in the scene
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        if (spawnPoints.Length > 0)
        {
            // Get a random spawn point
            int index = UnityEngine.Random.Range(0, spawnPoints.Length);
            return spawnPoints[index].transform;
        }
        return null;
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

    // Required INetworkRunnerCallbacks methods
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Network shutdown: {shutdownReason}");

        // Clear player dictionary
        _spawnedCharacters.Clear();

        // If we're not in the main menu, go back there
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("Connected to server"); }
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
    public void OnSceneLoadDone(NetworkRunner runner) { Debug.Log("Scene load completed"); }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}