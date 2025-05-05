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

    // Session information
    public string SessionDisplayName { get; private set; }
    public string SessionUniqueID { get; private set; }
    public string SessionHash { get; private set; }
    public long SessionStartTime { get; private set; }

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // Store available sessions locally since we can't access them directly
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;

    // Expose available sessions for the room browser
    public List<SessionInfo> GetAvailableSessions()
    {
        return new List<SessionInfo>(_availableSessions);
    }

    private void Awake()
    {
        // Cache the network runner at startup if it exists
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
        {
            UnityEngine.Debug.LogError("NetworkRunner component not found. Please add it in the Inspector.");
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null)
        {
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
    }

    private void Update()
    {
        // Press F1 for session debug info
        if (Input.GetKeyDown(KeyCode.F1))
        {
            DebugSessionsList();
        }
    }

    private void DebugSessionsList()
    {
        if (_runner == null)
        {
            UnityEngine.Debug.LogError("SESSIONS DEBUG: NetworkRunner is null");
            return;
        }

        UnityEngine.Debug.Log("==== SESSIONS DEBUG ====");
        UnityEngine.Debug.Log($"Runner State: {_runner.IsRunning}, GameMode: {_runner.GameMode}");

        if (_runner.SessionInfo != null)
        {
            UnityEngine.Debug.Log($"Current Session: {_runner.SessionInfo.Name}, Players: {_runner.SessionInfo.PlayerCount}/{_runner.SessionInfo.MaxPlayers}");
        }
        else
        {
            UnityEngine.Debug.Log("Not connected to any session");
        }

        // List all available sessions from our cached list
        if (_availableSessions != null && _availableSessions.Count > 0)
        {
            UnityEngine.Debug.Log($"Available Sessions ({_availableSessions.Count}):");
            foreach (var session in _availableSessions)
            {
                string displayName = session.Name;
                string hash = "N/A";

                if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                    displayName = nameObj.PropertyValue.ToString();

                if (session.Properties.TryGetValue("Hash", out var hashObj))
                    hash = hashObj.PropertyValue.ToString();

                UnityEngine.Debug.Log($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers} | Region: {session.Region}");
            }
        }
        else
        {
            UnityEngine.Debug.Log("No sessions available in session list");
        }

        UnityEngine.Debug.Log("========================");
    }

    public async Task StartHostGame(string sessionName)
    {
        try
        {
            _runner.ProvideInput = true;

            // Generate unique session identification
            SessionDisplayName = sessionName;
            SessionUniqueID = $"{sessionName}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            SessionHash = ComputeSessionHash(SessionUniqueID);
            SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            UnityEngine.Debug.Log($"Starting game as Host with name: {SessionDisplayName} | ID: {SessionUniqueID} | Hash: {SessionHash}");

            // Create session properties dictionary - using direct assignment
            var sessionProps = new Dictionary<string, SessionProperty>();

            // Add properties using direct assignment (implicit conversion handles it)
            sessionProps.Add("DisplayName", SessionDisplayName);
            sessionProps.Add("Hash", SessionHash);
            sessionProps.Add("StartTime", (int)SessionStartTime); // Convert long to int

            // Start the game in host mode
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID, // Use unique ID for actual session name
                SceneManager = _sceneManager,
                SessionProperties = sessionProps // Add the properties
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                UnityEngine.Debug.Log($"Host game started successfully");

                // Save session info for reconnection
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID);
                PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName);
                PlayerPrefs.Save();

                await LoadScene(_lobbySceneName);
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to start host game: {result.ShutdownReason}");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error starting host game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // New method to join directly by hash
    public async Task StartClientGameByHash(string hash)
    {
        try
        {
            // Find the session with matching hash
            SessionInfo targetSession = null;
            foreach (var session in _availableSessions)
            {
                // We need to check if the property exists and then compare values
                if (session.Properties.TryGetValue("Hash", out var sessionHash) &&
                    sessionHash.PropertyValue.ToString() == hash)
                {
                    targetSession = session;
                    Debug.Log($"Found session with matching hash: {session.Name}");
                    break;
                }
            }

            if (targetSession != null)
            {
                await StartClientGameBySessionInfo(targetSession);
            }
            else
            {
                Debug.LogError($"No session found with hash: {hash}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error joining game by hash: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Method to join by SessionInfo
    public async Task StartClientGameBySessionInfo(SessionInfo sessionInfo)
    {
        try
        {
            _runner.ProvideInput = true;
            _isJoining = true;

            // Get info from session properties if available
            if (sessionInfo.Properties.TryGetValue("DisplayName", out var displayNameObj))
                SessionDisplayName = displayNameObj.PropertyValue.ToString();
            else
                SessionDisplayName = sessionInfo.Name;

            if (sessionInfo.Properties.TryGetValue("Hash", out var hashObj))
                SessionHash = hashObj.PropertyValue.ToString();

            Debug.Log($"Joining session: {SessionDisplayName} with hash: {SessionHash}");

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionInfo.Name, // Use the exact session name
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Client game started successfully");

                // Update SessionUniqueID from connected session
                if (_runner.SessionInfo != null)
                {
                    SessionUniqueID = _runner.SessionInfo.Name;
                    Debug.Log($"Connected to session: {SessionDisplayName} | ID: {SessionUniqueID} | Hash: {SessionHash}");
                }
                else
                {
                    Debug.LogError("Connected but SessionInfo is null!");
                }
            }
            else
            {
                Debug.LogError($"Failed to join game: {result.ShutdownReason}");
            }

            _isJoining = false;
        }
        catch (Exception ex)
        {
            _isJoining = false;
            Debug.LogError($"Error joining game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Original join method (keeping for backward compatibility)
    public async Task StartClientGame(string sessionName)
    {
        try
        {
            _runner.ProvideInput = true;
            _isJoining = true;

            UnityEngine.Debug.Log($"Looking for session containing name: {sessionName}");

            // Store the session name for display purposes
            SessionDisplayName = sessionName;

            // Try to find a session with matching name in our cached list
            SessionInfo targetSession = null;
            foreach (var session in _availableSessions)
            {
                // Check if this session has the DisplayName property with our target name
                if (session.Properties.TryGetValue("DisplayName", out var nameObj) &&
                    nameObj.PropertyValue.ToString() == sessionName)
                {
                    targetSession = session;
                    break;
                }

                // Fallback to checking if the actual session name contains our target
                if (session.Name.Contains(sessionName))
                {
                    targetSession = session;
                    break;
                }
            }

            if (targetSession != null)
            {
                await StartClientGameBySessionInfo(targetSession);
                return;
            }

            // If we couldn't find it in the cache, try direct connection with the name
            UnityEngine.Debug.LogWarning($"No session found with name '{sessionName}', attempting direct connection");

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                UnityEngine.Debug.Log($"Client game started successfully via direct connection");

                // The session ID is the actual session name used internally
                if (_runner.SessionInfo != null)
                {
                    SessionUniqueID = _runner.SessionInfo.Name;

                    // For the client, we'll generate a hash based on the session name
                    SessionHash = ComputeSessionHash(SessionUniqueID);

                    UnityEngine.Debug.Log($"Connected to session: {SessionDisplayName} | ID: {SessionUniqueID} | Hash: {SessionHash}");
                }
                else
                {
                    UnityEngine.Debug.LogError("Connected but SessionInfo is null!");
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to join game: {result.ShutdownReason}");
            }

            _isJoining = false;
        }
        catch (Exception ex)
        {
            _isJoining = false;
            UnityEngine.Debug.LogError($"Error joining game: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Compute a hash for the session ID
    private string ComputeSessionHash(string input)
    {
        // Simple hash function for demo purposes
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert to hex string and take first 8 characters
            return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
        }
    }

    public async Task LoadScene(string sceneName)
    {
        if (_runner != null && _runner.IsRunning)
        {
            UnityEngine.Debug.Log($"Loading scene: {sceneName}");

            // Use the runner's LoadScene method directly
            await _runner.LoadScene(sceneName);
        }
    }

    public async Task ShutdownGame()
    {
        if (_runner != null && _runner.IsRunning)
        {
            UnityEngine.Debug.Log("Shutting down network session");
            await _runner.Shutdown();
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        UnityEngine.Debug.Log($"Player {player} joined");

        if (SceneManager.GetActiveScene().name == _lobbySceneName ||
            SceneManager.GetActiveScene().name == _gameSceneName)
        {
            if (runner.IsServer)
            {
                UnityEngine.Debug.Log($"Spawning player: {player}");

                // Get spawn point
                Transform spawnPoint = GetSpawnPoint();
                Vector3 spawnPosition;
                Quaternion spawnRotation = Quaternion.identity;

                if (spawnPoint != null)
                {
                    // Use exact spawn point position
                    spawnPosition = spawnPoint.position;
                    spawnRotation = spawnPoint.rotation;
                }
                else
                {
                    // Default spawn position - capsule bottom at Y=0
                    spawnPosition = new Vector3((player.RawEncoded % 4) * 2, 1.0f, 0);
                }

                try
                {
                    NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, spawnRotation, player);

                    if (networkPlayerObject != null)
                    {
                        _spawnedCharacters.Add(player, networkPlayerObject);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Exception when spawning player: {ex.Message}");
                }

                // Spawn GameStateManager if first player
                if (runner.IsServer && FindObjectOfType<GameStateManager>() == null)
                {
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
            UnityEngine.Debug.Log($"Player {player} despawned and removed from dictionary");
        }
    }

    // This callback receives session list updates
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        UnityEngine.Debug.Log($"Session list updated: {sessionList.Count} sessions available");
        _availableSessions = sessionList;

        // If we're in joining mode, log all available sessions to help with debugging
        if (_isJoining)
        {
            UnityEngine.Debug.Log("Available sessions while joining:");
            foreach (var session in sessionList)
            {
                string displayName = session.Name;
                string hash = "N/A";

                if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                    displayName = nameObj.PropertyValue.ToString();

                if (session.Properties.TryGetValue("Hash", out var hashObj))
                    hash = hashObj.PropertyValue.ToString();

                UnityEngine.Debug.Log($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers}");
            }
        }
    }

    // Required INetworkRunnerCallbacks methods
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        UnityEngine.Debug.Log($"Network shutdown: {shutdownReason}");

        // Clear player dictionary
        _spawnedCharacters.Clear();

        // If we're not in the main menu, go back there
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
    public void OnConnectedToServer(NetworkRunner runner) { UnityEngine.Debug.Log("Connected to server"); }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        UnityEngine.Debug.Log($"Disconnected from server: {reason}");
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        UnityEngine.Debug.LogError($"Connection failed: {reason}");
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { UnityEngine.Debug.Log("Scene load completed"); }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}