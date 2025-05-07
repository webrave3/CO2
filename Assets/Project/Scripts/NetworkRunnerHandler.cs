using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

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
    [SerializeField] private bool _enableDiscovery = true;
    [SerializeField] private bool _debugIncludeSelfHostedSessions = true;

    // Session information
    public string SessionDisplayName { get; private set; }
    public string SessionUniqueID { get; private set; }
    public string SessionHash { get; private set; }
    public long SessionStartTime { get; private set; }

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // Discovery-specific fields
    private NetworkRunner _discoveryRunner;
    private bool _isDiscoveryRunning = false;
    private DateTime _lastRefreshTime = DateTime.MinValue;

    // Store available sessions locally since we can't access them directly
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;

    // Add this property to check discovery status
    public bool IsDiscoveryRunning => _isDiscoveryRunning &&
        _discoveryRunner != null && _discoveryRunner.IsRunning;

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
            Debug.LogError("NetworkRunner component not found. Please add it in the Inspector.");
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

    private void Start()
    {
        Debug.Log("[NetworkRunnerHandler] Start method executing");

        // Make sure we're properly preserved across scenes
        if (FindObjectsOfType<NetworkRunnerHandler>().Length > 1)
        {
            Debug.LogWarning("[NetworkRunnerHandler] Multiple instances found - this might cause issues");
        }

        // Ensure we have DontDestroyOnLoad set
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log("[NetworkRunnerHandler] DontDestroyOnLoad applied to ensure persistence");
        }
    }

    private void DebugSessionsList()
    {
        if (_runner == null)
        {
            Debug.LogError("SESSIONS DEBUG: NetworkRunner is null");
            return;
        }

        Debug.Log("==== SESSIONS DEBUG ====");
        Debug.Log($"Runner State: {_runner.IsRunning}, GameMode: {_runner.GameMode}");

        if (_runner.SessionInfo != null)
        {
            Debug.Log($"Current Session: {_runner.SessionInfo.Name}, Players: {_runner.SessionInfo.PlayerCount}/{_runner.SessionInfo.MaxPlayers}");
        }
        else
        {
            Debug.Log("Not connected to any session");
        }

        // List all available sessions from our cached list
        if (_availableSessions != null && _availableSessions.Count > 0)
        {
            Debug.Log($"Available Sessions ({_availableSessions.Count}):");
            foreach (var session in _availableSessions)
            {
                string displayName = session.Name;
                string hash = "N/A";

                if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                    displayName = nameObj.PropertyValue.ToString();

                if (session.Properties.TryGetValue("Hash", out var hashObj))
                    hash = hashObj.PropertyValue.ToString();

                Debug.Log($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers} | Region: {session.Region}");
            }
        }
        else
        {
            Debug.Log("No sessions available in session list");
        }

        Debug.Log("========================");
    }

    public async Task StartHostGame(string sessionName, string region = "auto", bool allowAllRegions = true)
    {
        try
        {
            Debug.Log($"Starting host game with session name: {sessionName}, region: {region}, allowAllRegions: {allowAllRegions}");

            // Reset the runner first to ensure clean state
            await ResetNetworkRunner();

            _runner.ProvideInput = true;

            // Set up session codes using our system
            SetupSessionCode(sessionName);

            // Create session properties
            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>();
            sessionProps.Add("DisplayName", SessionDisplayName);
            sessionProps.Add("Hash", SessionHash);
            sessionProps.Add("StartTime", (int)SessionStartTime);

            // Important: Add region to session properties
            if (!string.IsNullOrEmpty(region) && region != "auto" && region != "best")
            {
                sessionProps.Add("Region", region);
            }

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

            // Note: Fusion 2.0.5 handles region differently - there's no direct property
            // Instead, we have to configure via the Photon App Settings or
            // it uses the closest/best region automatically

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Host game started successfully in region: {region}");

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

    private async Task RefreshSessionList()
    {
        Debug.Log("Refreshing session list...");

        // If we need to start a runner to get the session list
        if (_runner == null || !_runner.IsRunning)
        {
            var tempArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = string.Empty, // Empty string instead of null to avoid auto-joining
                SceneManager = _sceneManager
            };

            try
            {
                var result = await _runner.StartGame(tempArgs);
                if (!result.Ok)
                {
                    Debug.LogError($"Failed to start temporary session for discovery: {result.ShutdownReason}");
                    return;
                }
                Debug.Log("Started temporary session for discovery");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error starting temp session: {ex.Message}");
                return;
            }
        }

        // In Fusion 2.0.5, we can't directly control session discovery
        // Session list updates will be received via OnSessionListUpdated callback

        // Give network time to respond
        Debug.Log("Waiting for session list to update...");
        await Task.Delay(1500); // Longer delay to receive updates
    }

    public async Task ForceActiveSessionRefresh()
    {
        Debug.Log("[BROWSER] Starting session refresh...");

        try
        {
            // Create discovery runner if it doesn't exist
            if (_discoveryRunner == null)
            {
                Debug.Log("[BROWSER] Creating new discovery runner");
                _discoveryRunner = gameObject.AddComponent<NetworkRunner>();
            }

            // Don't refresh too frequently (rate limiting)
            if ((DateTime.Now - _lastRefreshTime).TotalSeconds < 2)
            {
                Debug.Log("[BROWSER] Refresh requested too soon after previous refresh");
                return;
            }

            _lastRefreshTime = DateTime.Now;

            // If we already have a discovery session running, shut it down
            if (_isDiscoveryRunning && !_discoveryRunner.IsShutdown)
            {
                Debug.Log("[BROWSER] Shutting down existing discovery runner");
                await _discoveryRunner.Shutdown();
                await Task.Delay(200); // Short delay to ensure clean shutdown
            }

            // Start a fresh discovery session
            Debug.Log("[BROWSER] Starting new discovery runner");

            // Use a unique session name for discovery
            string sessionBrowserId = "BROWSER_" + DateTime.Now.Ticks;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionBrowserId,
                SceneManager = _sceneManager,
                IsVisible = false // Don't advertise our temporary session
            };

            var result = await _discoveryRunner.StartGame(startGameArgs);

            if (!result.Ok)
            {
                Debug.LogError($"[BROWSER] Failed to start discovery runner: {result.ShutdownReason}");
                _isDiscoveryRunning = false;
                return;
            }

            _isDiscoveryRunning = true;
            Debug.Log("[BROWSER] Started discovery runner successfully");

            // Wait for network to respond with session list
            Debug.Log("[BROWSER] Waiting for session list updates...");
            await Task.Delay(2000); // Wait for session list updates
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BROWSER] Error creating discovery runner: {ex.Message}");
            _isDiscoveryRunning = false;
        }
    }

    public async Task ResetNetworkRunner()
    {
        Debug.Log("Resetting NetworkRunner...");

        // If we have an active session, shut it down properly
        if (Runner != null && Runner.IsRunning)
        {
            try
            {
                Debug.Log("Shutting down previous session");
                await Runner.Shutdown();

                // Wait a bit to ensure clean shutdown
                await Task.Delay(300);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during runner shutdown: {ex.Message}");
            }
        }

        // Reset session info
        SessionDisplayName = string.Empty;
        SessionUniqueID = string.Empty;
        SessionHash = string.Empty;
        SessionStartTime = 0;

        // Clear available sessions cache
        _availableSessions.Clear();

        Debug.Log("NetworkRunner reset complete");
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

            Debug.Log($"Looking for session containing name: {sessionName}");

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
            Debug.LogWarning($"No session found with name '{sessionName}', attempting direct connection");

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Client game started successfully via direct connection");

                // The session ID is the actual session name used internally
                if (_runner.SessionInfo != null)
                {
                    SessionUniqueID = _runner.SessionInfo.Name;

                    // For the client, we'll generate a hash based on the session name
                    SessionHash = ComputeSessionHash(SessionUniqueID);

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

    // Add this method to force a manual refresh
    public void ForceRefreshSessions()
    {
        Debug.Log("Force refreshing session list...");

        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("Cannot refresh sessions - Runner not active");
            return;
        }

        // In Fusion 2.0.5, we have to rely on the automatic session list updates
        // Start async refresh
        _ = ForceActiveSessionRefresh();
    }

    // Debug function to analyze session discovery
    public void DebugRoomDiscovery()
    {
        Debug.Log("===== ROOM DISCOVERY DEBUG =====");

        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log($"Runner active: {_runner.IsRunning}");

            if (_runner.IsServer)
            {
                Debug.Log($"Hosting room: {SessionDisplayName}");
                Debug.Log($"Room hash: {SessionHash}");
                Debug.Log($"Session advertised: {_runner.SessionInfo?.IsVisible}");
            }

            // Force refresh
            ForceRefreshSessions();
        }
        else
        {
            Debug.Log("Runner not initialized");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined");

        if (SceneManager.GetActiveScene().name == _lobbySceneName ||
            SceneManager.GetActiveScene().name == _gameSceneName)
        {
            if (runner.IsServer)
            {
                Debug.Log($"Spawning player: {player}");

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
                    Debug.LogError($"Exception when spawning player: {ex.Message}");
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
            Debug.Log($"Player {player} despawned and removed from dictionary");
        }
    }

    // This callback receives session list updates
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Session list updated: {sessionList.Count} sessions available");

        // Create a new list to avoid modifying the original
        List<SessionInfo> updatedList = new List<SessionInfo>(sessionList);

        // CRITICAL FIX: More robust self-hosted session detection
        // Check if this update came from our discovery runner and we're hosting
        if (_debugIncludeSelfHostedSessions && _runner != null &&
            _runner.IsRunning && _runner.IsServer && _runner.SessionInfo != null)
        {
            Debug.Log($"Self-hosted session check: Name={_runner.SessionInfo.Name}, Runner={_runner != null}");

            // Check if our session is already in the list
            bool foundOwnSession = false;
            foreach (var session in sessionList)
            {
                // Compare by Name which is the unique identifier
                if (session.Name == _runner.SessionInfo.Name)
                {
                    foundOwnSession = true;
                    Debug.Log("Found self-hosted session in list already");
                    break;
                }
            }

            // If our session isn't in the list, add it
            if (!foundOwnSession)
            {
                Debug.Log($"ADDING self-hosted session to room list: {_runner.SessionInfo.Name}");
                updatedList.Add(_runner.SessionInfo);
            }
        }

        _availableSessions = updatedList;

        // If we're in joining mode, log all available sessions to help with debugging
        if (_isJoining)
        {
            Debug.Log("Available sessions while joining:");
            foreach (var session in updatedList)
            {
                string displayName = session.Name;
                string hash = "N/A";

                if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                    displayName = nameObj.PropertyValue.ToString();

                if (session.Properties.TryGetValue("Hash", out var hashObj))
                    hash = hashObj.PropertyValue.ToString();

                Debug.Log($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers}");
            }
        }
    }

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

        // Only go back to main menu if this was the main runner, not the discovery runner
        if (runner == _runner && SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    public void RecoverNetworkState()
    {
        Debug.Log("Attempting to recover network state...");

        // First check if runner needs cleanup
        if (_runner != null && _runner.IsShutdown)
        {
            Debug.Log("Cleaning up shutdown runner");
            Destroy(_runner);
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        // Do the same for discovery runner
        if (_discoveryRunner != null && _discoveryRunner.IsShutdown)
        {
            Debug.Log("Cleaning up shutdown discovery runner");
            Destroy(_discoveryRunner);
            _discoveryRunner = null;
            _isDiscoveryRunning = false;
        }

        // Ensure we have valid runner instances
        if (_runner == null)
        {
            Debug.Log("Creating new main NetworkRunner");
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        Debug.Log("Network state recovery complete");
    }

    // Add this to handle null NetworkRunnerHandler exceptions
    public static NetworkRunnerHandler GetInstance()
    {
        NetworkRunnerHandler instance = FindObjectOfType<NetworkRunnerHandler>();

        if (instance == null)
        {
            Debug.LogWarning("NetworkRunnerHandler not found, creating new instance");
            GameObject go = new GameObject("NetworkRunnerHandler");
            instance = go.AddComponent<NetworkRunnerHandler>();
            DontDestroyOnLoad(go);
        }

        return instance;
    }

    public void LogNetworkState()
    {
        Debug.Log("======= NETWORK STATE =======");
        Debug.Log($"Main Runner: {(_runner != null ? "Valid" : "NULL")}");
        if (_runner != null)
        {
            Debug.Log($"- IsRunning: {_runner.IsRunning}");
            Debug.Log($"- IsServer: {_runner.IsServer}");
            Debug.Log($"- IsShutdown: {_runner.IsShutdown}");
            Debug.Log($"- SessionInfo: {(_runner.SessionInfo != null ? _runner.SessionInfo.Name : "NULL")}");
        }

        Debug.Log($"Discovery Runner: {(_discoveryRunner != null ? "Valid" : "NULL")}");
        if (_discoveryRunner != null)
        {
            Debug.Log($"- IsRunning: {_discoveryRunner.IsRunning}");
            Debug.Log($"- IsShutdown: {_discoveryRunner.IsShutdown}");
        }

        Debug.Log($"IsDiscoveryRunning: {_isDiscoveryRunning}");
        Debug.Log($"Available Sessions: {_availableSessions.Count}");
        Debug.Log($"SessionHash: {SessionHash}");
        Debug.Log($"SessionUniqueID: {SessionUniqueID}");
        Debug.Log("============================");
    }

    // Required INetworkRunnerCallbacks methods
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
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
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { Debug.Log("Scene load completed"); }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}