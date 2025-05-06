using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network References")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private NetworkPrefabRef _gameStateManagerPrefab;

    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "Lobby";
    [SerializeField] private string _gameSceneName = "Game";

    [Header("Network Settings")]
    [SerializeField] private int _maxPlayers = 6;

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

    // Replace your existing StartHostGame method in NetworkRunnerHandler.cs

    public async Task StartHostGame(string sessionName)
    {
        try
        {
            _runner.ProvideInput = true;

            // Set up session codes using our new system
            SetupSessionCode(sessionName);

            // Create session properties
            var sessionProps = new Dictionary<string, SessionProperty>();
            sessionProps.Add("DisplayName", SessionDisplayName);
            sessionProps.Add("Hash", SessionHash);
            sessionProps.Add("StartTime", (int)SessionStartTime);

            // Start the game in host mode
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID, // Use internal ID for actual session name
                SceneManager = _sceneManager,
                SessionProperties = sessionProps,
                PlayerCount = _maxPlayers,
                IsVisible = true,
                IsOpen = true
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Host game started successfully");

                // Save session info for reconnection
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID);
                PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName);
                PlayerPrefs.Save();

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

    // New method to join directly by hash
    // Update your StartClientGameByHash method in NetworkRunnerHandler.cs

    public async Task StartClientGameByHash(string roomCode)
    {
        try
        {
            Debug.Log($"Attempting to join room with code: {roomCode}");

            if (string.IsNullOrEmpty(roomCode))
            {
                Debug.LogError("Room code is empty or null");
                return;
            }

            // Ensure we have an updated session list
            await RefreshSessionList();

            // Find the session with matching hash
            SessionInfo targetSession = null;

            foreach (var session in _availableSessions)
            {
                bool hasHashProperty = session.Properties.TryGetValue("Hash", out var hashProperty);

                if (hasHashProperty)
                {
                    string sessionHash = hashProperty.PropertyValue.ToString();

                    if (sessionHash.Equals(roomCode, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSession = session;
                        Debug.Log($"Found matching session: {session.Name}");
                        break;
                    }
                }
            }

            if (targetSession != null)
            {
                await StartClientGameBySessionInfo(targetSession);
            }
            else
            {
                Debug.LogError($"No session found with code: {roomCode}");
                // Show error to user - implement UI feedback here
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error joining game by room code: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Add this helper method to NetworkRunnerHandler.cs

    private async Task RefreshSessionList()
    {
        // If we need to start a runner to get the session list
        if (_runner == null || !_runner.IsRunning)
        {
            var tempArgs = new StartGameArgs()
            {
                GameMode = GameMode.AutoHostOrClient,
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(tempArgs);
            if (!result.Ok)
            {
                Debug.LogError($"Failed to start temporary session for discovery: {result.ShutdownReason}");
                return;
            }

            // Give some time for session list to populate
            await Task.Delay(1000);
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

    // Add these methods to your NetworkRunnerHandler.cs

    // Method to generate and set session codes
    private void SetupSessionCode(string sessionNameBase)
    {
        // Generate a memorable room code
        SessionHash = SessionCodeManager.Instance.GenerateNewSessionCode();

        // Get the internal unique ID
        SessionUniqueID = SessionCodeManager.Instance.GetInternalId(SessionHash);

        // Use provided display name or default
        SessionDisplayName = string.IsNullOrEmpty(sessionNameBase) ?
            "Game Session" : sessionNameBase;

        SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Debug.Log($"Session initialized: {SessionDisplayName} | ID: {SessionUniqueID} | Code: {SessionHash}");
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

    // Add this method to force a manual refresh
    public void ForceRefreshSessions()
    {
        Debug.Log("Force refreshing session list...");

        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("Cannot refresh sessions - Runner not active");
            return;
        }

        // Nothing to do since OnSessionListUpdated will be called automatically
        Debug.Log("Runner active, waiting for session list updates");
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

    // Debug function to analyze session discovery
    public void DebugSessionDiscovery()
    {
        Debug.Log("===== SESSION DISCOVERY DEBUG =====");

        // Log current runner state
        Debug.Log($"Runner State: {(_runner != null ? _runner.IsRunning.ToString() : "Runner null")}");
        Debug.Log($"Game Mode: {(_runner != null ? _runner.GameMode.ToString() : "N/A")}");

        // Log available sessions
        Debug.Log($"Available Sessions Count: {_availableSessions.Count}");

        foreach (var session in _availableSessions)
        {
            Debug.Log($"Session: {session.Name}");
            Debug.Log($"  Region: {session.Region}");
            Debug.Log($"  Players: {session.PlayerCount}/{session.MaxPlayers}");

            foreach (var prop in session.Properties)
            {
                Debug.Log($"  Property: {prop.Key} = {prop.Value.PropertyValue}");
            }
        }

        // Log current session details if hosting
        if (_runner != null && _runner.IsRunning && _runner.IsServer)
        {
            Debug.Log("HOSTING SESSION DETAILS:");
            Debug.Log($"  Display Name: {SessionDisplayName}");
            Debug.Log($"  Unique ID: {SessionUniqueID}");
            Debug.Log($"  Hash: {SessionHash}");

            if (_runner.SessionInfo != null)
            {
                Debug.Log($"  Actual Session Name: {_runner.SessionInfo.Name}");
                Debug.Log($"  Is Visible: {_runner.SessionInfo.IsVisible}");
                Debug.Log($"  Is Open: {_runner.SessionInfo.IsOpen}");
            }
        }

        Debug.Log("===================================");
    }

    // Required INetworkRunnerCallbacks methods
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    // Update your OnShutdown method in NetworkRunnerHandler.cs

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Network shutdown: {shutdownReason}");

        // End the session to start the cooldown timer
        if (!string.IsNullOrEmpty(SessionHash))
        {
            if (SessionCodeManager.Instance != null)
            {
                SessionCodeManager.Instance.EndSession(SessionHash);
            }
        }

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