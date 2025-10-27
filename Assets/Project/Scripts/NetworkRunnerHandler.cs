// Filename: NetworkRunnerHandler.cs
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Linq filtering
using System.Threading.Tasks;
// using Unity.VisualScripting; // Remove if not needed
using UnityEngine;
using UnityEngine.SceneManagement;

// *** REMOVED Photon.Realtime / Fusion.Photon namespaces ***

// Keep INetworkRunnerCallbacks interface
public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Prefabs")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private NetworkPrefabRef _gameStateManagerPrefab;
    [SerializeField] private NetworkPrefabRef _vehiclePrefab;

    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "Lobby";
    [SerializeField] private string _gameSceneName = "Game";
    [SerializeField] private string _mainMenuSceneName = "MainMenu"; // Added for shutdown return

    [Header("Network Settings")]
    [SerializeField] private int _maxPlayers = 4; // Adjusted to your game's player count
    // *** REMOVED _photonAppSettings field - Rely on Resources asset ***

    // Session information tracked by the handler
    public string SessionDisplayName { get; private set; }
    public string SessionUniqueID { get; private set; } // Internal Photon session name
    public string SessionHash { get; private set; }     // Your short, user-friendly code
    public long SessionStartTime { get; private set; }

    // Runtime references
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // Session list handling
    private TaskCompletionSource<bool> _sessionListTask;
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false; // Flag to prevent multiple join attempts

    // Session Property Keys
    public const string SESSION_PASSWORD_KEY = "pwd";
    public const string SESSION_LANGUAGE_KEY = "lang";
    public const string SESSION_REGION_KEY = "reg";   // Key for region property (Using short keys)

    // Public properties
    public NetworkRunner Runner => _runner;
    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public List<SessionInfo> GetAvailableSessions() => new List<SessionInfo>(_availableSessions); // Return a copy


    // --- Region Map & Helpers (Static - Keep These) ---
    // Maps user-friendly names to Photon region codes
    private static readonly Dictionary<string, string> RegionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Add all regions supported by Photon here, mapping UI name to Photon code
        { "Best", "" }, // Special case for Photon's best region selection
        { "US East", "us" },
        { "US West", "usw" },
        { "Canada East", "cae" },
        { "EU", "eu" },
        { "Asia", "asia" },
        { "Japan", "jp" },
        { "Australia", "au" },
        { "South America", "sa" },
        { "South Korea", "kr" },
        { "India", "in" },
        { "Russia", "ru" },
        { "Russia East", "rue" },
        // Add others as needed...
    };

    // Helper to get the Photon code from a user-friendly name (Still useful for logging/display)
    public static string GetRegionCode(string regionName)
    {
        if (string.IsNullOrEmpty(regionName) || regionName.Equals("Any", StringComparison.OrdinalIgnoreCase)) return ""; // Treat "Any" as "Best"
        if (RegionMap.TryGetValue(regionName, out var code)) return code;
        Debug.LogWarning($"Region name '{regionName}' not found in map. Defaulting to 'Best' (empty string).");
        return "";
    }

    // Helper to get all user-friendly region names for dropdowns
    public static List<string> GetRegionNames() => RegionMap.Keys.ToList();
    // --- End Static Helpers ---


    private void Awake()
    {
        Debug.Log("NetworkRunnerHandler Awake: Initializing...");

        // Ensure only one instance exists (basic singleton pattern)
        // *** CORRECTED FindObjectsOfType usage ***
        if (FindObjectsOfType<NetworkRunnerHandler>().Length > 1 && transform.parent == null)
        {
            Debug.LogWarning("NetworkRunnerHandler Awake: Duplicate instance detected. Destroying self.");
            Destroy(gameObject);
            return;
        }

        // *** REMOVED AppSettings loading/assignment logic - Relies on the Resources asset ***
        // Ensure the PhotonAppSettings.asset exists in a Resources folder.

        // Setup NetworkRunner
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null) { _runner = gameObject.AddComponent<NetworkRunner>(); Debug.Log("Added NetworkRunner component."); }
        _runner.AddCallbacks(this); // Register callbacks (AddCallbacks handles duplicates)

        // Setup Scene Manager
        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null) { _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(); Debug.Log("Added NetworkSceneManagerDefault component."); }

        if (transform.parent == null) DontDestroyOnLoad(gameObject); // Persist across scenes if it's a root object
        Debug.Log("NetworkRunnerHandler Awake: Initialization complete.");
    }

    // *** REMOVED SetConnectionRegion method - Settings handled by the asset ***


    // --- StartHostGame ---
    public async Task StartHostGame(string sessionNameBase, bool isVisible = true, Dictionary<string, SessionProperty> customProps = null)
    {
        Debug.Log($"--- [HOST] StartHostGame --- Session: '{sessionNameBase}', Visible: {isVisible}");
        // Region is determined by the PhotonAppSettings.asset in Resources

        try
        {
            await ResetNetworkRunner();
            _runner.ProvideInput = true;
            SetupSessionCode(sessionNameBase);

            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>
            { { "DisplayName", SessionDisplayName }, { "Hash", SessionHash }, { "StartTime", (int)SessionStartTime } };

            if (customProps != null) { foreach (var prop in customProps) { if (!sessionProps.ContainsKey(prop.Key)) sessionProps.Add(prop.Key, prop.Value); } }

            // Password Handling (Adds pwd property if private and passed in customProps)
            string password = null;
            if (customProps != null && customProps.TryGetValue(SESSION_PASSWORD_KEY, out var pwdProp)) password = pwdProp?.PropertyValue?.ToString();

            if (!isVisible && !string.IsNullOrEmpty(password))
            {
                sessionProps[SESSION_PASSWORD_KEY] = password; Debug.Log("[HOST] Added password property.");
            }
            else if (isVisible && sessionProps.ContainsKey(SESSION_PASSWORD_KEY))
            {
                sessionProps.Remove(SESSION_PASSWORD_KEY); Debug.LogWarning("[HOST] Public game: Removed password property.");
            }

            Debug.Log($"[HOST] Final Session Props: (Logging props)");
            foreach (var prop in sessionProps) { Debug.Log($"  > {prop.Key}: {prop.Value?.PropertyValue}"); }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID,
                SceneManager = _sceneManager,
                SessionProperties = sessionProps,
                PlayerCount = _maxPlayers,
                IsVisible = isVisible,
                IsOpen = true,
                // AppSettings are handled globally via the asset in Resources
                // No Authentication field in 2.0.8 StartGameArgs
            };

            Debug.Log($"[HOST] Calling StartGame...");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"[HOST] StartGame OK. Session: '{_runner.SessionInfo?.Name}' Connected Region: '{_runner.SessionInfo?.Region}'"); // Log connected region
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID); /* etc */ PlayerPrefs.Save();
                await LoadScene(_lobbySceneName);
            }
            else
            {
                Debug.LogError($"[HOST] StartGame FAILED: {result.ShutdownReason}");
                await ResetNetworkRunner();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HOST] StartGame EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner();
        }
    }


    // --- StartClientGameByHash (Password via Connection Token) ---
    public async Task<bool> StartClientGameByHash(string roomCode, string password = null)
    {
        Debug.Log($"--- [DIRECT JOIN] By Hash --- Code: '{roomCode}', Pwd Provided: {!string.IsNullOrEmpty(password)}");
        if (string.IsNullOrEmpty(roomCode)) { Debug.LogWarning("Room code empty."); return false; }
        if (_isJoining) { Debug.LogWarning("Already joining."); return false; }

        _isJoining = true;
        // Region is determined by the PhotonAppSettings.asset

        try
        {
            await ResetNetworkRunner();
            _runner.ProvideInput = true;

            var sessionPropsFilter = new Dictionary<string, SessionProperty> { { "Hash", roomCode } };

            byte[] connectionToken = null;
            if (!string.IsNullOrEmpty(password))
            {
                connectionToken = System.Text.Encoding.UTF8.GetBytes(password);
                Debug.Log($"[DIRECT JOIN] Preparing connection token (byte array).");
            }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = null, // Join using filter
                SceneManager = _sceneManager,
                SessionProperties = sessionPropsFilter,
                ConnectionToken = connectionToken, // <<< Use this field
            };

            Debug.Log($"[DIRECT JOIN] Calling StartGame...");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner != null && _runner.SessionInfo != null)
            {
                Debug.Log($"[DIRECT JOIN] StartGame OK. Session: '{_runner.SessionInfo.Name}', Connected Region: '{_runner.SessionInfo?.Region}'");
                UpdateLocalSessionInfoFromRunner();
                if (_sceneManager != null) _sceneManager.enabled = true;
                _isJoining = false;
                return true;
            }
            else
            {
                Debug.LogError($"[DIRECT JOIN] StartGame FAILED: {result.ShutdownReason}");
                string failureReason = $"Failed: {result.ShutdownReason}"; // Default message
                                                                           // Check common reasons available in 2.0.8
                if (result.ShutdownReason == ShutdownReason.GameClosed)
                { // GameClosed should exist
                    failureReason = "Session Closed.";
                }
                // If joining by filter (SessionName null) failed without specific reason, provide generic feedback
                else if (!result.Ok && startGameArgs.SessionName == null)
                {
                    failureReason = "Could not find or join session (Invalid Code? Password Issue? Full?).";
                }
                // Specific refusal messages (like wrong password) should come from OnConnectFailed

                await ResetNetworkRunner();
                _isJoining = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DIRECT JOIN] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner();
            _isJoining = false;
            return false;
        }
    }

    // --- Join specific game using SessionInfo (Matchmaking) ---
    public async Task<bool> StartClientGameBySessionInfo(SessionInfo sessionInfo)
    {
        Debug.Log($"--- [MATCH JOIN] By SessionInfo --- Session: '{sessionInfo?.Name}', Region Hint: '{sessionInfo?.Region}'");
        if (sessionInfo == null) { Debug.LogError("[MATCH JOIN] SessionInfo is null."); return false; }
        if (_isJoining) { Debug.LogWarning("[MATCH JOIN] Already joining."); return false; }

        _isJoining = true;
        // Region determined by PhotonAppSettings asset

        try
        {
            // Runner shutdown/recreation if needed
            if (_runner != null && (_runner.IsRunning || _runner.IsCloudReady))
            {
                Debug.Log($"[MATCH JOIN] Shutting down existing runner (Mode: {_runner.Mode}).");
                await _runner.Shutdown(); await Task.Delay(200);
            }
            if (_runner == null || _runner.IsShutdown)
            {
                _runner = GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
                _runner.AddCallbacks(this);
            }

            if (_sceneManager == null) { Debug.LogError("[MATCH JOIN] SceneManager missing!"); _isJoining = false; return false; }
            _sceneManager.enabled = true; // Enable Scene Manager BEFORE starting join
            Debug.Log("[MATCH JOIN] Ensured NetworkSceneManager is enabled.");

            _runner.ProvideInput = true;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionInfo.Name, // Target specific session
                SceneManager = _sceneManager,
                // No token/auth needed for public matchmaking
            };

            Debug.Log($"[MATCH JOIN] Calling StartGame...");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner != null && _runner.SessionInfo != null)
            {
                Debug.Log($"[MATCH JOIN] StartGame OK. Session: '{_runner.SessionInfo.Name}', Connected Region: '{_runner.SessionInfo?.Region}'");
                UpdateLocalSessionInfoFromRunner();
                _isJoining = false;
                return true;
            }
            else
            {
                Debug.LogError($"[MATCH JOIN] StartGame FAILED: {result.ShutdownReason}");
                await ResetNetworkRunner();
                _isJoining = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MATCH JOIN] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner();
            _isJoining = false;
            return false;
        }
    }


    // --- FindAndJoinPublicGame ---
    public async Task<bool> FindAndJoinPublicGame(Dictionary<string, SessionProperty> filters = null)
    {
        Debug.Log("--- [MATCHMAKING] FindAndJoinPublicGame ---");
        if (_isJoining) { Debug.LogWarning("[MATCHMAKING] Already joining/hosting."); return false; }

        // Region preference from filter might influence lobby connection if AppSettings isn't fixed
        // However, the primary control is the AppSettings asset. We won't try to change it here.
        string preferredRegionDisplay = "Best/Any";
        if (filters != null && filters.TryGetValue(SESSION_REGION_KEY, out var regionProp))
        {
            preferredRegionDisplay = regionProp?.PropertyValue?.ToString() ?? "Best/Any";
        }
        Debug.Log($"[MATCHMAKING] Searching based on AppSettings region. Filter preference (display only): '{preferredRegionDisplay}'");


        _isJoining = true;
        try
        {
            Debug.Log("[MATCHMAKING] Refreshing session list...");
            bool refreshSuccess = await RefreshSessionList();
            if (!refreshSuccess) { Debug.LogError("[MATCHMAKING] Failed refresh."); _isJoining = false; return false; }
            await Task.Delay(500);

            List<SessionInfo> currentSessions = GetAvailableSessions();
            Debug.Log($"[MATCHMAKING] Found {currentSessions.Count} sessions.");

            // Filter list - Exclude password protected!
            List<SessionInfo> potentialMatches = currentSessions.Where(session =>
                session.IsVisible && session.IsOpen && session.PlayerCount < session.MaxPlayers &&
                !session.Properties.ContainsKey(SESSION_PASSWORD_KEY) && // *** Exclude password protected ***
                (filters == null || filters.Count == 0 || filters.All(filter => {
                    // Filter by Language or other custom properties
                    if (session.Properties.TryGetValue(filter.Key, out SessionProperty sessionValue))
                    {
                        // Special handling for region display property if needed for stricter filtering
                        if (filter.Key == SESSION_REGION_KEY)
                        {
                            string sessionRegionName = sessionValue?.PropertyValue?.ToString() ?? "?";
                            string filterRegionName = filter.Value?.PropertyValue?.ToString() ?? "Best";
                            return filterRegionName.Equals("Best", StringComparison.OrdinalIgnoreCase) ||
                                    filterRegionName.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
                                    sessionRegionName.Equals(filterRegionName, StringComparison.OrdinalIgnoreCase);
                        }
                        // Standard equality check
                        return Equals(sessionValue?.PropertyValue, filter.Value?.PropertyValue);
                    }
                    return false; // Property doesn't exist in session
                }))
            ).ToList();


            Debug.Log($"[MATCHMAKING] Found {potentialMatches.Count} potential matches after filtering.");

            if (potentialMatches.Count > 0)
            {
                SessionInfo bestMatch = potentialMatches[0];
                Debug.Log($"[MATCHMAKING] Selected match: {bestMatch.Name}");
                bool joinStarted = await StartClientGameBySessionInfo(bestMatch);
                return joinStarted; // _isJoining reset internally
            }
            else
            {
                Debug.Log("[MATCHMAKING] No suitable games found."); _isJoining = false; return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MATCHMAKING] EXCEPTION: {ex.Message}"); await ResetNetworkRunner(); _isJoining = false; return false;
        }
    }


    // --- RefreshSessionList ---
    public async Task<bool> RefreshSessionList()
    {
        Debug.Log($"--- RefreshSessionList ---");
        // Region determined by PhotonAppSettings asset

        if (_runner != null && _runner.IsRunning && _runner.Mode != SimulationModes.Client)
        {
            Debug.Log($"[Refresh] Shutting down non-Client runner (Mode: {_runner.Mode}).");
            await _runner.Shutdown(); await Task.Delay(200);
            if (_runner != null) Destroy(_runner); await Task.Yield(); _runner = null;
        }
        if (_runner == null || _runner.IsShutdown)
        {
            _runner = GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this);
            Debug.Log("[Refresh] Created/re-added runner for lobby join.");
        }

        if (_sceneManager != null) _sceneManager.enabled = false;
        else { Debug.LogError("[Refresh] Scene Manager null!"); return false; }
        Debug.Log($"[Refresh] Disabled NetworkSceneManager.");

        _sessionListTask = new TaskCompletionSource<bool>();
        try
        {
            var args = new StartGameArgs() { GameMode = GameMode.Client, SessionName = null, SceneManager = null };
            Debug.Log($"[Refresh] Calling StartGame to join lobby...");
            var result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Refresh] FAILED to join lobby: {result.ShutdownReason}");
                if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable SM on fail
                _sessionListTask.TrySetResult(false);
                // Don't Reset here, let caller handle UI
                return false;
            }

            Debug.Log($"[Refresh] Waiting for session list (30s)...");
            var timeoutTask = Task.Delay(30000);
            var completedTask = await Task.WhenAny(_sessionListTask.Task, timeoutTask);

            // Scene Manager is re-enabled in the actual join methods *after* StartGame succeeds.

            if (completedTask == _sessionListTask.Task && _sessionListTask.Task.Result) { Debug.Log($"[Refresh] List received."); return true; }
            else { Debug.LogWarning($"[Refresh] Timeout/Fail waiting for list."); _sessionListTask.TrySetCanceled(); return false; }

        }
        catch (Exception ex)
        {
            Debug.LogError($"[Refresh] EXCEPTION: {ex.Message}");
            if (_sceneManager != null) _sceneManager.enabled = true;
            _sessionListTask.TrySetException(ex);
            return false;
        }
    }


    // --- ResetNetworkRunner ---
    public async Task ResetNetworkRunner()
    {
        Debug.Log($"ResetNetworkRunner called.");
        if (_runner != null)
        {
            _runner.RemoveCallbacks(this); // Remove callbacks first

            if (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown)
            {
                Debug.Log($"[Reset] Shutting down existing runner...");
                await _runner.Shutdown(destroyGameObject: false); await Task.Delay(200);
            }

            if (_runner != null)
            { // Check again before destroying component
                Debug.Log($"[Reset] Destroying runner component.");
                Destroy(_runner); await Task.Yield();
            }
            _runner = null;
        }

        Debug.Log($"[Reset] Adding new runner component and callbacks.");
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);

        if (_sceneManager == null) _sceneManager = GetComponent<NetworkSceneManagerDefault>() ?? gameObject.AddComponent<NetworkSceneManagerDefault>();
        _sceneManager.enabled = true; // Ensure Scene Manager is enabled
        Debug.Log($"[Reset] Ensured SceneManager exists and is enabled.");

        SessionDisplayName = SessionUniqueID = SessionHash = string.Empty; SessionStartTime = 0;
        _availableSessions.Clear(); _isJoining = false;
        Debug.Log("[Reset] Cleared state.");
    }

    // --- SetupSessionCode ---
    private void SetupSessionCode(string sessionNameBase)
    {
        // *** CORRECTED: Use FindObjectOfType (singular) ***
        SessionCodeManager scm = FindObjectOfType<SessionCodeManager>();
        if (scm == null)
        {
            Debug.LogWarning("SessionCodeManager not found! Using fallback codes.");
            SessionHash = ComputeSessionHash(Guid.NewGuid().ToString()).Substring(0, 6).ToUpper();
            SessionUniqueID = Guid.NewGuid().ToString();
        }
        else
        {
            SessionHash = scm.GenerateNewSessionCode();
            SessionUniqueID = scm.GetInternalId(SessionHash);
        }
        SessionDisplayName = string.IsNullOrEmpty(sessionNameBase) ? $"Session {SessionHash}" : sessionNameBase;
        SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Debug.Log($"[SetupSessionCode] Hash: {SessionHash}, UniqueID: {SessionUniqueID}");
    }

    // --- UpdateLocalSessionInfoFromRunner ---
    private void UpdateLocalSessionInfoFromRunner()
    {
        if (_runner == null || _runner.SessionInfo == null) return;
        SessionUniqueID = _runner.SessionInfo.Name;
        SessionHash = _runner.SessionInfo.Properties.TryGetValue("Hash", out var h) ? h?.PropertyValue?.ToString() ?? "N/A" : "N/A";
        SessionDisplayName = _runner.SessionInfo.Properties.TryGetValue("DisplayName", out var d) ? d?.PropertyValue?.ToString() ?? SessionUniqueID : SessionUniqueID;
        if (_runner.SessionInfo.Properties.TryGetValue("StartTime", out var s) && s.PropertyValue is int st) SessionStartTime = st; else SessionStartTime = 0;
        Debug.Log($"Updated local session info: ID='{SessionUniqueID}', Hash='{SessionHash}', Name='{SessionDisplayName}', Region='{_runner.SessionInfo?.Region}'");
    }

    // --- ComputeSessionHash ---
    private string ComputeSessionHash(string input) { if (string.IsNullOrEmpty(input)) return "NOHASH"; using (var md5 = System.Security.Cryptography.MD5.Create()) { byte[] bytes = System.Text.Encoding.ASCII.GetBytes(input); byte[] hash = md5.ComputeHash(bytes); return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8); } }

    // --- LoadScene ---
    public async Task LoadScene(string sceneName)
    {
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Project/Scenes/{sceneName}.unity");
        if (sceneIndex < 0) { sceneIndex = SceneUtility.GetBuildIndexByScenePath(sceneName); } // Fallback by name
        if (sceneIndex < 0) { Debug.LogError($"Scene '{sceneName}' not found!"); return; }

        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log($"Runner loading scene: {sceneName} (Index: {sceneIndex})");
            if (_sceneManager != null) _sceneManager.enabled = true; else { Debug.LogError("Scene Manager null!"); return; }
            await _runner.LoadScene(SceneRef.FromIndex(sceneIndex)); // Use SceneRef for safety
        }
        else
        {
            Debug.LogWarning($"Runner not active, cannot load '{sceneName}' via runner.");
            if (sceneName == _mainMenuSceneName) { Debug.Log($"Loading Main Menu directly."); SceneManager.LoadScene(sceneIndex); }
        }
    }

    // --- ShutdownGame ---
    public async Task ShutdownGame()
    {
        Debug.Log("ShutdownGame called.");
        string currentScene = SceneManager.GetActiveScene().name;
        if (_runner != null && (_runner.IsRunning || _runner.IsCloudReady))
        {
            Debug.Log("Shutting down active runner...");
            await _runner.Shutdown(destroyGameObject: false);
        }
        else
        {
            Debug.Log("No active runner.");
            if (currentScene != _mainMenuSceneName) { Debug.Log($"Loading {_mainMenuSceneName} directly."); SceneManager.LoadScene(_mainMenuSceneName); }
        }
        SessionDisplayName = SessionUniqueID = SessionHash = string.Empty; SessionStartTime = 0; _isJoining = false;
    }


    // --- INetworkRunnerCallbacks Implementation ---

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined.");
        if (runner.IsServer)
        {
            // *** CORRECTED: Use FindObjectOfType (singular) ***
            if (FindObjectOfType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid) { runner.Spawn(_gameStateManagerPrefab); }

            Transform sp = GetSpawnPoint(); Vector3 pos = sp ? sp.position : Vector3.zero; Quaternion rot = sp ? sp.rotation : Quaternion.identity;
            NetworkObject pNO = runner.Spawn(_playerPrefab, pos, rot, player);
            if (pNO != null) _spawnedCharacters.Add(player, pNO); else Debug.LogError($"Failed to spawn char for {player}");
        }
        // *** CORRECTED: Use FindObjectOfType (singular) ***
        FindObjectOfType<LobbyManager>()?.RefreshPlayerReadyList();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} left.");
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject no)) { if (no != null) runner.Despawn(no); _spawnedCharacters.Remove(player); }
        // *** CORRECTED: Use FindObjectOfType (singular) ***
        FindObjectOfType<LobbyManager>()?.RefreshPlayerReadyList();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"--- OnSessionListUpdated --- Received {sessionList.Count} raw sessions.");
        _availableSessions.Clear();
        foreach (var session in sessionList)
        {
            if (session.IsVisible && !string.IsNullOrEmpty(session.Name) && !session.Name.StartsWith("TempDiscoverySession_"))
            {
                _availableSessions.Add(session);
                string hash = session.Properties.TryGetValue("Hash", out var h) ? h?.PropertyValue?.ToString() ?? "N/A" : "N/A";
                string dispName = session.Properties.TryGetValue("DisplayName", out var d) ? d?.PropertyValue?.ToString() ?? session.Name : session.Name;
                bool hasPwd = session.Properties.ContainsKey(SESSION_PASSWORD_KEY);
                Debug.Log($"  > Session: Name='{session.Name}', Hash='{hash}', Players={session.PlayerCount}/{session.MaxPlayers}, Open={session.IsOpen}, Region='{session.Region}', HasPwd={hasPwd}");
            }
        }
        Debug.Log($"Filtered list contains {_availableSessions.Count} sessions.");
        _sessionListTask?.TrySetResult(true);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"--- OnShutdown --- Reason: {shutdownReason}");
        // *** CORRECTED: Use FindObjectOfType (singular) ***
        if (runner != null && runner.IsServer && !string.IsNullOrEmpty(SessionHash)) { FindObjectOfType<SessionCodeManager>()?.EndSession(SessionHash); }
        _spawnedCharacters.Clear(); _isJoining = false;
        if (_runner == runner) _runner = null;

        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != _mainMenuSceneName)
        {
            Debug.Log($"Loading {_mainMenuSceneName} scene after shutdown."); SceneManager.LoadSceneAsync(_mainMenuSceneName);
        }
        else
        {
            Debug.Log("Already in MainMenu.");
            // *** CORRECTED: Use FindObjectOfType (singular) ***
            MainMenuUI mainMenu = FindObjectOfType<MainMenuUI>();
            if (mainMenu != null) { GameObject mp = mainMenu.transform.Find("MainPanel")?.gameObject; if (mp != null) mainMenu.ShowPanel(mp); }
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[OnSceneLoadDone] Scene: '{sceneName}', IsServer: {runner.IsServer}");
        if (runner.IsServer)
        {
            // *** CORRECTED: Use FindObjectOfType (singular) ***
            if (FindObjectOfType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid) { runner.Spawn(_gameStateManagerPrefab); }
            // *** CORRECTED: Use FindObjectOfType (singular) ***
            if ((sceneName == _lobbySceneName || sceneName == _gameSceneName) && FindObjectOfType<NetworkedPrometeoCar>() == null)
            {
                if (_vehiclePrefab != null && _vehiclePrefab.IsValid)
                {
                    Transform sp = GetSpawnPoint(); Vector3 pos = sp ? sp.position + sp.forward * 2f + Vector3.up * 0.5f : Vector3.zero; Quaternion rot = sp ? sp.rotation : Quaternion.identity;
                    runner.Spawn(_vehiclePrefab, pos, rot);
                }
                else { Debug.LogError("Vehicle Prefab invalid!"); }
            }
        }
        // *** CORRECTED: Use FindObjectOfType (singular) ***
        if (sceneName == _lobbySceneName) { FindObjectOfType<LobbyManager>()?.RefreshPlayerReadyList(); }
    }

    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log($"--- OnConnectedToServer --- Region: '{runner.SessionInfo?.Region ?? "N/A"}'"); }

    // --- OnConnectRequest (Checks ConnectionToken byte[]) ---
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        Debug.Log($"--- OnConnectRequest --- From {request.RemoteAddress}. Token Length: {(token?.Length ?? 0)}");
        if (!runner.IsServer) return;
        string requiredPwd = null;
        if (runner.SessionInfo != null && runner.SessionInfo.Properties.TryGetValue(SESSION_PASSWORD_KEY, out var pwdProp)) { requiredPwd = pwdProp?.PropertyValue?.ToString(); }

        if (!string.IsNullOrEmpty(requiredPwd))
        {
            string providedPwd = (token != null && token.Length > 0) ? System.Text.Encoding.UTF8.GetString(token) : null;
            if (requiredPwd.Equals(providedPwd)) { Debug.Log($"Password MATCH. Accepting."); request.Accept(); }
            else { Debug.LogWarning($"Password MISMATCH. Rejecting."); request.Refuse(); }
        }
        else { Debug.Log($"No password required. Accepting."); request.Accept(); }
    }

    // --- OnConnectFailed ---
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"--- OnConnectFailed --- Address: {remoteAddress}, Reason: {reason}");
        _isJoining = false; // Reset joining flag

        string feedback = $"Connection Failed: {reason}"; // Start with the basic reason

        // Provide slightly more user-friendly messages for common failures if applicable in 2.0.8
        if (reason == NetConnectFailedReason.Timeout)
        {
            feedback = "Connection Failed: Timed Out.";
        }
        // Add checks for other NetConnectFailedReason enum values if needed and available in 2.0.8
        // e.g., if (reason == NetConnectFailedReason.ServerRefused) { ... } // ServerRefused *might* cover password issues

        // Update UI if possible
        // Use FindObjectOfType (safer during potential scene transitions/failures)
        if (SceneManager.GetActiveScene().name == _mainMenuSceneName)
        {
            JoinGameUI joinUI = FindObjectOfType<JoinGameUI>();
            joinUI?.ShowStatusMessage(feedback, Color.red); // Display the generated feedback
        }

        // Ensure runner is reset after failure
        Task.Run(async () => await ResetNetworkRunner()); // Reset async
    }

    // --- Helper to find spawn points ---
    private Transform GetSpawnPoint()
    {
        // *** CORRECTED: Use FindObjectsOfType (plural) ***
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        if (spawnPoints.Length > 0) { return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform; }
        Debug.LogWarning("No SpawnPoint objects found!"); return null;
    }

    // --- Other Callbacks ---
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"--- OnDisconnectedFromServer --- Reason: {reason}"); _isJoining = false; }
}