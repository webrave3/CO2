using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Linq; // Keep System.Linq

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network References")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private NetworkPrefabRef _gameStateManagerPrefab;
    [SerializeField] private NetworkPrefabRef _vehiclePrefab; // ADDED: Vehicle prefab reference

    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "Lobby";
    [SerializeField] private string _gameSceneName = "Game";

    [Header("Network Settings")]
    [SerializeField] private int _maxPlayers = 6;
    [SerializeField] private bool _enableDiscovery = true; // Kept from original
    [SerializeField] private bool _debugIncludeSelfHostedSessions = true; // Kept from original

    // Session information
    public string SessionDisplayName { get; private set; }
    public string SessionUniqueID { get; private set; }
    public string SessionHash { get; private set; }
    public long SessionStartTime { get; private set; }

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // Discovery-specific fields (Kept from original)
    private NetworkRunner _discoveryRunner;
    private bool _isDiscoveryRunning = false;
    private DateTime _lastRefreshTime = DateTime.MinValue;

    // Store available sessions locally (Kept from original)
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;
    public bool IsDiscoveryRunning => _isDiscoveryRunning && _discoveryRunner != null && _discoveryRunner.IsRunning;

    public List<SessionInfo> GetAvailableSessions() => new List<SessionInfo>(_availableSessions);

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
        {
            Debug.LogError("NetworkRunner component not found. Adding one.");
            _runner = gameObject.AddComponent<NetworkRunner>();
        }
        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null)
        {
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
    }

    private void Start()
    {
        // FIX: Use FindObjectsByType and destroy duplicates
        if (FindObjectsByType<NetworkRunnerHandler>(FindObjectsSortMode.None).Length > 1)
        {
            Debug.LogWarning("[NetworkRunnerHandler] Multiple instances detected. Destroying this one.");
            Destroy(gameObject);
            return; // Stop execution if this is a duplicate
        }
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    private void Update() // Kept from original for F1 debug
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            DebugSessionsList();
        }
    }

    private void DebugSessionsList() // Kept from original
    {
        if (_runner == null) { Debug.LogError("SESSIONS DEBUG: NetworkRunner is null"); return; }
        Debug.Log("==== SESSIONS DEBUG ====");
        Debug.Log($"Runner State: {_runner.IsRunning}, GameMode: {_runner.GameMode}");
        if (_runner.SessionInfo != null) Debug.Log($"Current Session: {_runner.SessionInfo.Name}, Players: {_runner.SessionInfo.PlayerCount}/{_runner.SessionInfo.MaxPlayers}");
        else Debug.Log("Not connected to any session");

        if (_availableSessions != null && _availableSessions.Count > 0)
        {
            Debug.Log($"Available Sessions ({_availableSessions.Count}):");
            foreach (var session in _availableSessions)
            {
                string displayName = session.Name; string hash = "N/A";
                if (session.Properties.TryGetValue("DisplayName", out var nameObj)) displayName = nameObj.PropertyValue.ToString();
                if (session.Properties.TryGetValue("Hash", out var hashObj)) hash = hashObj.PropertyValue.ToString();
                Debug.Log($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers} | Region: {session.Region}");
            }
        }
        else Debug.Log("No sessions available in session list");
        Debug.Log("========================");
    }

    public async Task StartHostGame(string sessionName, string region = "auto", bool allowAllRegions = true)
    {
        try
        {
            // Debug.Log($"Starting host game: {sessionName}, region: {region}");
            await ResetNetworkRunner();
            _runner.ProvideInput = true;
            SetupSessionCode(sessionName);

            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>
            { { "DisplayName", SessionDisplayName }, { "Hash", SessionHash }, { "StartTime", (int)SessionStartTime } };
            if (!string.IsNullOrEmpty(region) && region != "auto" && region != "best") sessionProps.Add("Region", region);

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID,
                SceneManager = _sceneManager,
                SessionProperties = sessionProps,
                PlayerCount = _maxPlayers,
                IsVisible = true,
                IsOpen = true
            };
            var result = await _runner.StartGame(startGameArgs);
            if (result.Ok)
            {
                // Debug.Log($"Host game started successfully in region: {region}");
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID); PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName); PlayerPrefs.Save();
                await LoadScene(_lobbySceneName);
            }
            else Debug.LogError($"Failed to start host game: {result.ShutdownReason}");
        }
        catch (Exception ex) { Debug.LogError($"Error starting host game: {ex.Message}\n{ex.StackTrace}"); }
    }

    public async Task StartClientGameByHash(string roomCode) // Kept from original
    {
        try
        {
            // Debug.Log($"Attempting to join room with code: {roomCode}");
            if (string.IsNullOrEmpty(roomCode)) { Debug.LogError("Room code is empty or null"); return; }
            await RefreshSessionList(); // Ensure list is fresh (original logic)
            SessionInfo targetSession = _availableSessions.FirstOrDefault(session =>
                 session.Properties.TryGetValue("Hash", out var hashProperty) &&
                 hashProperty.PropertyValue.ToString().Equals(roomCode, StringComparison.OrdinalIgnoreCase));

            if (targetSession != null)
            {
                // Debug.Log($"Found matching session: {targetSession.Name}");
                await StartClientGameBySessionInfo(targetSession);
            }
            else Debug.LogError($"No session found with code: {roomCode}");
        }
        catch (Exception ex) { Debug.LogError($"Error joining game by room code: {ex.Message}\n{ex.StackTrace}"); }
    }

    private async Task RefreshSessionList() // Kept from original, might need adjustments based on Fusion version
    {
        // Debug.Log("Refreshing session list...");
        // Ensure runner is running to receive updates
        if (_runner == null || !_runner.IsRunning || !_runner.IsCloudReady)
        {
            // Start a temporary client if needed (careful not to create sessions)
            // *** CORRECTED: Removed DisableClientSessionCreation ***
            var tempArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = "TempDiscoverySession_" + Guid.NewGuid(), // Use a unique name
                SceneManager = _sceneManager
                // Removed: DisableClientSessionCreation = true
            };
            try
            {
                if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>(); // Ensure runner exists
                var result = await _runner.StartGame(tempArgs);
                if (!result.Ok) { Debug.LogError($"Failed temporary session for discovery: {result.ShutdownReason}"); return; }
                // Debug.Log("Started temporary session for discovery");
                await Task.Delay(500); // Give a moment to connect
            }
            catch (Exception ex) { Debug.LogError($"Error starting temp session: {ex.Message}"); return; }
        }
        // Rely on OnSessionListUpdated to populate _availableSessions
        // Debug.Log("Waiting for session list to update...");
        await Task.Delay(1500); // Allow time for updates
    }

    public async Task ForceActiveSessionRefresh() // Kept from original
    {
        // Debug.Log("[BROWSER] Starting session refresh...");
        try
        {
            if (_discoveryRunner == null) { /*Debug.Log("[BROWSER] Creating new discovery runner");*/ _discoveryRunner = gameObject.AddComponent<NetworkRunner>(); }
            if ((DateTime.Now - _lastRefreshTime).TotalSeconds < 2) { /*Debug.Log("[BROWSER] Refresh requested too soon");*/ return; }
            _lastRefreshTime = DateTime.Now;
            if (_isDiscoveryRunning && _discoveryRunner != null && !_discoveryRunner.IsShutdown) { /*Debug.Log("[BROWSER] Shutting down existing discovery runner");*/ await _discoveryRunner.Shutdown(); await Task.Delay(200); }

            // Debug.Log("[BROWSER] Starting new discovery runner");
            string sessionBrowserId = "BROWSER_" + DateTime.Now.Ticks;
            var startGameArgs = new StartGameArgs() { GameMode = GameMode.Client, SessionName = sessionBrowserId, SceneManager = _sceneManager, IsVisible = false };
            var result = await _discoveryRunner.StartGame(startGameArgs);
            if (!result.Ok) { Debug.LogError($"[BROWSER] Failed to start discovery runner: {result.ShutdownReason}"); _isDiscoveryRunning = false; return; }
            _isDiscoveryRunning = true;
            // Debug.Log("[BROWSER] Started discovery runner successfully");
            // Debug.Log("[BROWSER] Waiting for session list updates...");
            await Task.Delay(2000); // Wait for session list updates
        }
        catch (Exception ex) { Debug.LogError($"[BROWSER] Error creating discovery runner: {ex.Message}"); _isDiscoveryRunning = false; }
    }

    public async Task ResetNetworkRunner() // Kept from original
    {
        // Debug.Log("Resetting NetworkRunner...");
        if (Runner != null && Runner.IsRunning) { try { /*Debug.Log("Shutting down previous session");*/ await Runner.Shutdown(); await Task.Delay(300); } catch (Exception ex) { Debug.LogError($"Error during runner shutdown: {ex.Message}"); } }
        SessionDisplayName = string.Empty; SessionUniqueID = string.Empty; SessionHash = string.Empty; SessionStartTime = 0;
        _availableSessions.Clear();
        // Debug.Log("NetworkRunner reset complete");
    }

    public async Task StartClientGameBySessionInfo(SessionInfo sessionInfo) // Kept from original
    {
        try
        {
            if (_runner == null) { Debug.LogError("Runner is null, cannot join game!"); return; } // Safety check
            _runner.ProvideInput = true; _isJoining = true;
            if (sessionInfo.Properties.TryGetValue("DisplayName", out var displayNameObj)) SessionDisplayName = displayNameObj.PropertyValue.ToString(); else SessionDisplayName = sessionInfo.Name;
            if (sessionInfo.Properties.TryGetValue("Hash", out var hashObj)) SessionHash = hashObj.PropertyValue.ToString(); else SessionHash = ComputeSessionHash(sessionInfo.Name); // Fallback hash generation

            // Debug.Log($"Joining session: {SessionDisplayName} with hash: {SessionHash} (Internal Name: {sessionInfo.Name})");
            var startGameArgs = new StartGameArgs() { GameMode = GameMode.Client, SessionName = sessionInfo.Name, SceneManager = _sceneManager };
            var result = await _runner.StartGame(startGameArgs);
            if (result.Ok)
            {
                // Debug.Log($"Client game started successfully");
                if (_runner.SessionInfo != null) { SessionUniqueID = _runner.SessionInfo.Name; /*Debug.Log($"Connected: {SessionDisplayName} | ID: {SessionUniqueID} | Hash: {SessionHash}");*/ }
                else Debug.LogError("Connected but SessionInfo is null!");
            }
            else Debug.LogError($"Failed to join game: {result.ShutdownReason}");
            _isJoining = false;
        }
        catch (Exception ex) { _isJoining = false; Debug.LogError($"Error joining game: {ex.Message}\n{ex.StackTrace}"); }
    }

    private void SetupSessionCode(string sessionNameBase) // Kept from original
    {
        // FIX: Use FindFirstObjectByType and check for null
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (scm == null) { Debug.LogError("SessionCodeManager instance not found!"); SessionHash = "NOHASH"; SessionUniqueID = Guid.NewGuid().ToString(); }
        else { SessionHash = scm.GenerateNewSessionCode(); SessionUniqueID = scm.GetInternalId(SessionHash); }
        SessionDisplayName = string.IsNullOrEmpty(sessionNameBase) ? "Game Session" : sessionNameBase;
        SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Debug.Log($"Session initialized: {SessionDisplayName} | ID: {SessionUniqueID} | Code: {SessionHash}");
    }

    public async Task StartClientGame(string sessionName) // Kept from original
    {
        try
        {
            if (_runner == null) { Debug.LogError("Runner is null!"); return; }
            _runner.ProvideInput = true; _isJoining = true;
            // Debug.Log($"Looking for session containing name: {sessionName}");
            SessionDisplayName = sessionName;
            await RefreshSessionList(); // Get latest list before searching

            SessionInfo targetSession = _availableSessions.FirstOrDefault(s =>
                (s.Properties.TryGetValue("DisplayName", out var nameObj) && nameObj.PropertyValue.ToString().Equals(sessionName, StringComparison.OrdinalIgnoreCase)) || // Match DisplayName property case-insensitive
                s.Name.Equals(sessionName, StringComparison.OrdinalIgnoreCase)); // Match internal Name property case-insensitive

            if (targetSession != null) { await StartClientGameBySessionInfo(targetSession); return; }

            Debug.LogWarning($"No cached session found with name '{sessionName}', attempting direct connection (may fail if name is not unique internal ID)");
            var startGameArgs = new StartGameArgs() { GameMode = GameMode.Client, SessionName = sessionName, SceneManager = _sceneManager };
            var result = await _runner.StartGame(startGameArgs);
            if (result.Ok)
            {
                // Debug.Log($"Client game started successfully via direct connection");
                if (_runner.SessionInfo != null) { SessionUniqueID = _runner.SessionInfo.Name; SessionHash = ComputeSessionHash(SessionUniqueID); /*Debug.Log($"Connected: {SessionDisplayName} | ID: {SessionUniqueID} | Hash: {SessionHash}");*/ }
                else Debug.LogError("Connected but SessionInfo is null!");
            }
            else Debug.LogError($"Failed to join game: {result.ShutdownReason}");
            _isJoining = false;
        }
        catch (Exception ex) { _isJoining = false; Debug.LogError($"Error joining game: {ex.Message}\n{ex.StackTrace}"); }
    }

    private string ComputeSessionHash(string input) // Kept from original
    {
        if (string.IsNullOrEmpty(input)) return "NOHASH"; // Handle null/empty case
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        { byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input); byte[] hashBytes = md5.ComputeHash(inputBytes); return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8); }
    }

    public async Task LoadScene(string sceneName) // Kept from original
    {
        if (_runner != null && _runner.IsRunning) { /* Debug.Log($"Loading scene: {sceneName}");*/ await _runner.LoadScene(sceneName); }
    }

    public async Task ShutdownGame() // Kept from original
    {
        if (_runner != null && _runner.IsRunning) { /* Debug.Log("Shutting down network session"); */ await _runner.Shutdown(); }
    }

    public void ForceRefreshSessions() // Kept from original
    {
        // Debug.Log("Force refreshing session list...");
        if (_runner == null || !_runner.IsRunning) { Debug.LogError("Cannot refresh sessions - Runner not active"); return; }
        _ = ForceActiveSessionRefresh(); // Fire-and-forget async call
    }

    public void DebugRoomDiscovery() // Kept from original
    {
        Debug.Log("===== ROOM DISCOVERY DEBUG =====");
        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log($"Runner active: {_runner.IsRunning}");
            if (_runner.IsServer) { Debug.Log($"Hosting room: {SessionDisplayName}"); Debug.Log($"Room hash: {SessionHash}"); Debug.Log($"Session advertised: {_runner.SessionInfo?.IsVisible}"); }
            ForceRefreshSessions();
        }
        else Debug.Log("Runner not initialized");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Debug.Log($"Player {player} joined");
        string currentScene = SceneManager.GetActiveScene().name;
        if ((currentScene == _lobbySceneName || currentScene == _gameSceneName) && runner.IsServer)
        {
            // Debug.Log($"Spawning player: {player}");
            Transform spawnPoint = GetSpawnPoint();
            Vector3 spawnPos = spawnPoint ? spawnPoint.position : new Vector3((player.RawEncoded % 4) * 2, 1.0f, 0);
            Quaternion spawnRot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

            try
            {
                NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPos, spawnRot, player);
                if (networkPlayerObject != null) _spawnedCharacters.Add(player, networkPlayerObject);
            }
            catch (Exception ex) { Debug.LogError($"Exception when spawning player: {ex.Message}"); }

            // FIX: Use FindFirstObjectByType
            if (FindFirstObjectByType<GameStateManager>() == null) runner.Spawn(_gameStateManagerPrefab);

            // --- ADDED: Spawn Vehicle if needed ---
            if (_vehiclePrefab != null && _vehiclePrefab.IsValid && FindFirstObjectByType<BasicVehicleController>() == null)
            {
                // Example spawn position, adjust as needed
                Vector3 vehicleSpawnPos = spawnPoint ? spawnPoint.position + spawnPoint.forward * 5f + Vector3.up * 0.5f : new Vector3(0, 0.5f, 5);
                runner.Spawn(_vehiclePrefab, vehicleSpawnPos, Quaternion.identity);
                // Debug.Log("Spawning BasicVehicleController instance.");
            }
            else if (_vehiclePrefab == null || !_vehiclePrefab.IsValid)
            {
                Debug.LogWarning("Vehicle Prefab is not assigned or invalid in NetworkRunnerHandler.");
            }
            // --- End ADDED ---
        }
    }

    private Transform GetSpawnPoint() // Kept from original
    {
        // FIX: Use FindObjectsByType
        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        return spawnPoints.Length > 0 ? spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform : null;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) // Kept from original
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            if (networkObject != null) runner.Despawn(networkObject); // Check if object still exists before despawn
            _spawnedCharacters.Remove(player);
            // Debug.Log($"Player {player} despawned and removed");
        }
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) // Kept from original
    {
        // Debug.Log($"Session list updated: {sessionList.Count} sessions available");
        List<SessionInfo> updatedList = new List<SessionInfo>(sessionList);
        if (_debugIncludeSelfHostedSessions && _runner != null && _runner.IsRunning && _runner.IsServer && _runner.SessionInfo != null)
        {
            bool foundOwnSession = sessionList.Any(session => session.Name == _runner.SessionInfo.Name);
            if (!foundOwnSession) { /* Debug.Log($"ADDING self-hosted session: {_runner.SessionInfo.Name}");*/ updatedList.Add(_runner.SessionInfo); }
            // else { Debug.Log("Found self-hosted session in list already"); }
        }
        _availableSessions = updatedList;

        if (_isJoining) // Original debug log when joining
        {
            Debug.Log("Available sessions while joining:");
            foreach (var session in updatedList)
            {
                string displayName = session.Name; string hash = "N/A";
                if (session.Properties.TryGetValue("DisplayName", out var nameObj)) displayName = nameObj.PropertyValue.ToString();
                if (session.Properties.TryGetValue("Hash", out var hashObj)) hash = hashObj.PropertyValue.ToString();
                Debug.Log($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers}");
            }
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) // Kept from original
    {
        // Debug.Log($"Network shutdown: {shutdownReason}");
        // FIX: Use FindFirstObjectByType and check null
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (!string.IsNullOrEmpty(SessionHash) && scm != null) scm.EndSession(SessionHash);
        _spawnedCharacters.Clear();
        if (runner == _runner && SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadScene("MainMenu");
    }

    public void RecoverNetworkState() // Kept from original
    {
        // Debug.Log("Attempting to recover network state...");
        if (_runner != null && _runner.IsShutdown) { /*Debug.Log("Cleaning up shutdown runner");*/ Destroy(_runner); _runner = gameObject.AddComponent<NetworkRunner>(); }
        if (_discoveryRunner != null && _discoveryRunner.IsShutdown) { /*Debug.Log("Cleaning up shutdown discovery runner");*/ Destroy(_discoveryRunner); _discoveryRunner = null; _isDiscoveryRunning = false; }
        if (_runner == null) { /*Debug.Log("Creating new main NetworkRunner");*/ _runner = gameObject.AddComponent<NetworkRunner>(); }
        // Debug.Log("Network state recovery complete");
    }

    // FIX: Use FindFirstObjectByType
    public static NetworkRunnerHandler GetInstance() // Kept from original
    {
        NetworkRunnerHandler instance = FindFirstObjectByType<NetworkRunnerHandler>();
        if (instance == null)
        {
            Debug.LogWarning("NetworkRunnerHandler not found, creating new instance.");
            GameObject go = new GameObject("NetworkRunnerHandler");
            instance = go.AddComponent<NetworkRunnerHandler>();
            DontDestroyOnLoad(go); // Ensure it persists
        }
        return instance;
    }

    public void LogNetworkState() // Kept from original
    {
        Debug.Log("======= NETWORK STATE =======");
        Debug.Log($"Main Runner: {(_runner != null ? "Valid" : "NULL")}");
        if (_runner != null) { Debug.Log($"- Running: {_runner.IsRunning}, Server: {_runner.IsServer}, Shutdown: {_runner.IsShutdown}, Session: {(_runner.SessionInfo != null ? _runner.SessionInfo.Name : "NULL")}"); }
        Debug.Log($"Discovery Runner: {(_discoveryRunner != null ? "Valid" : "NULL")}");
        if (_discoveryRunner != null) { Debug.Log($"- Running: {_discoveryRunner.IsRunning}, Shutdown: {_discoveryRunner.IsShutdown}"); }
        Debug.Log($"IsDiscoveryRunning Flag: {_isDiscoveryRunning}");
        Debug.Log($"Available Sessions Cached: {_availableSessions.Count}");
        Debug.Log($"SessionHash: {SessionHash}"); Debug.Log($"SessionUniqueID: {SessionUniqueID}");
        Debug.Log("============================");
    }


    // --- Callbacks ---
    // Keep required empty implementations
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.LogError($"Connection failed: {reason}"); }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { /* Debug.Log("Scene load completed"); */ }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    // FIX: Corrected Signatures from original INetworkRunnerCallbacks
    public void OnConnectedToServer(NetworkRunner runner) { /* Debug.Log("Connected to server"); */ }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { /* Debug.Log($"Disconnected from server: {reason}"); */ }
    // --- End FIX ---
}