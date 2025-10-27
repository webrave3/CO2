// Filename: NetworkRunnerHandler.cs
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Linq filtering
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    public const string SESSION_LANGUAGE_KEY = "lang"; // Key for language property
    public const string SESSION_REGION_KEY = "reg";   // Key for region property (Using short keys)

    // Public properties
    public NetworkRunner Runner => _runner;
    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public List<SessionInfo> GetAvailableSessions() => new List<SessionInfo>(_availableSessions); // Return a copy


    private void Awake()
    {
        Debug.Log("NetworkRunnerHandler Awake: Initializing...");

        // Ensure only one instance exists (basic singleton pattern)
        if (FindObjectsByType<NetworkRunnerHandler>(FindObjectsSortMode.None).Length > 1 && transform.parent == null)
        {
            Debug.LogWarning("NetworkRunnerHandler Awake: Duplicate instance detected. Destroying self.");
            Destroy(gameObject);
            return;
        }

        // Setup NetworkRunner
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null) { _runner = gameObject.AddComponent<NetworkRunner>(); Debug.Log("Added NetworkRunner component."); }
        _runner.AddCallbacks(this); // Register callbacks

        // Setup Scene Manager
        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null) { _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(); Debug.Log("Added NetworkSceneManagerDefault component."); }

        if (transform.parent == null) DontDestroyOnLoad(gameObject); // Persist across scenes if it's a root object
        Debug.Log("NetworkRunnerHandler Awake: Initialization complete.");
    }

    // --- MODIFIED: StartHostGame now accepts visibility and custom properties ---
    public async Task StartHostGame(string sessionNameBase, bool isVisible = true, Dictionary<string, SessionProperty> customProps = null)
    {
        Debug.Log($"--- [HOST] StartHostGame ---");
        Debug.Log($"SessionName base: '{sessionNameBase}', IsVisible: {isVisible}");

        try
        {
            await ResetNetworkRunner(); // Ensure a clean runner
            _runner.ProvideInput = true;

            // Generate session codes/IDs using SessionCodeManager (make sure it's in the scene or accessible)
            SetupSessionCode(sessionNameBase);
            Debug.Log($"[HOST] SessionCode setup. Hash: '{SessionHash}', UniqueID: '{SessionUniqueID}'");

            // --- Prepare Session Properties ---
            // Start with mandatory properties
            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>
            {
                { "DisplayName", SessionDisplayName },
                { "Hash", SessionHash }, // Your user-friendly code
                { "StartTime", (int)SessionStartTime }
                // Add your custom properties passed in
            };
            if (customProps != null)
            {
                foreach (var prop in customProps)
                {
                    if (!sessionProps.ContainsKey(prop.Key)) // Avoid overwriting base props
                    {
                        sessionProps.Add(prop.Key, prop.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"[HOST] Custom property key '{prop.Key}' conflicts with a base property. Ignoring custom value.");
                    }
                }
            }
            // Add Region if needed (though often handled by AppSettings)
            // sessionProps.Add("Region", runner.SessionInfo?.Region ?? "unknown");


            // Log the properties being sent
            Debug.Log($"[HOST] StartGame Args - Properties to send:");
            foreach (var prop in sessionProps) { Debug.Log($"  > Key: '{prop.Key}', Value: '{prop.Value?.PropertyValue}'"); }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID, // Use the internal unique ID for Photon SessionName
                SceneManager = _sceneManager,
                SessionProperties = sessionProps,
                PlayerCount = _maxPlayers,
                IsVisible = isVisible, // Control if it appears in public listings
                IsOpen = true         // Allow players to join
                // Scene = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath($"Assets/Project/Scenes/{_lobbySceneName}.unity")) // Optional: Load scene directly
            };

            Debug.Log($"[HOST] Calling StartGame...");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"[HOST] StartGame OK. Runner Active. Session: '{_runner.SessionInfo?.Name}'");
                // Save session info if needed for reconnects (optional)
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID);
                PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName);
                PlayerPrefs.Save();
                // Scene loading is now often handled by NetworkSceneManager automatically if Scene is set in args,
                // or you can load manually after StartGame succeeds.
                await LoadScene(_lobbySceneName); // Example: Manually load lobby scene
            }
            else
            {
                Debug.LogError($"[HOST] StartGame FAILED: {result.ShutdownReason}");
                await ResetNetworkRunner(); // Clean up failed runner
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HOST] StartGame EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner(); // Clean up on exception
        }
    }

    // --- Direct Join by Hash/Code (Corrected Implementation) ---
    public async Task<bool> StartClientGameByHash(string roomCode)
    {
        Debug.Log($"--- [DIRECT JOIN] StartClientGameByHash ---");
        Debug.Log($"Received roomCode: '{roomCode}' (Target Hash)");

        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning($"[DIRECT JOIN] Room code is empty. Aborting.");
            return false;
        }
        if (_isJoining) { Debug.LogWarning("[DIRECT JOIN] Already attempting to join. Aborting."); return false; }

        _isJoining = true;
        try
        {
            await ResetNetworkRunner();
            _runner.ProvideInput = true;

            // Create a filter to find a session with a matching "Hash" property
            var sessionPropsFilter = new Dictionary<string, SessionProperty> { { "Hash", roomCode } };

            // Start game in Client mode, using the filter
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = null, // Null = Join using filter or random (if no filter)
                SceneManager = _sceneManager,
                SessionProperties = sessionPropsFilter // The matchmaking filter
            };

            Debug.Log($"[DIRECT JOIN] Calling StartGame to FIND session with Hash: '{roomCode}'");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner != null && _runner.SessionInfo != null)
            {
                Debug.Log($"[DIRECT JOIN] StartGame OK. Joined session: '{_runner.SessionInfo.Name}'");
                // Store joined session info locally
                UpdateLocalSessionInfoFromRunner();
                _isJoining = false;
                // Scene should load automatically via NetworkSceneManager or callbacks
                return true;
            }
            else
            {
                Debug.LogError($"[DIRECT JOIN] StartGame FAILED: {result.ShutdownReason}");
                await ResetNetworkRunner(); // Clean up failed runner
                _isJoining = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DIRECT JOIN] StartClientGameByHash EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner(); // Clean up on exception
            _isJoining = false;
            return false;
        }
    }


    // --- Join specific game using SessionInfo (Used by Matchmaking) ---
    public async Task<bool> StartClientGameBySessionInfo(SessionInfo sessionInfo)
    {
        Debug.Log($"--- [MATCH JOIN] StartClientGameBySessionInfo ---");
        Debug.Log($"Attempting to join session: '{sessionInfo.Name}'");

        if (sessionInfo == null) { Debug.LogError("[MATCH JOIN] SessionInfo is null."); return false; }
        if (_isJoining) { Debug.LogWarning("[MATCH JOIN] Already attempting to join. Aborting."); return false; }

        _isJoining = true;
        try
        {
            // If we were just querying the list, the runner might be in lobby mode. Shut it down first.
            if (_runner != null && (_runner.IsRunning || _runner.IsCloudReady))
            {
                Debug.Log($"[MATCH JOIN] Shutting down existing runner before joining specific session.");
                await _runner.Shutdown();
                await Task.Delay(200); // Small delay after shutdown
            }

            // Ensure we have a runner instance (might have been destroyed by shutdown)
            if (_runner == null || _runner.IsShutdown)
            {
                Debug.LogWarning("[MATCH JOIN] Runner was shut down or null. Re-adding component.");
                _runner = GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
                _runner.AddCallbacks(this); // Re-add callbacks if runner was re-created
            }

            _runner.ProvideInput = true;

            // Start game in Client mode targeting the specific SessionName
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionInfo.Name, // Target the specific session
                SceneManager = _sceneManager
            };

            Debug.Log($"[MATCH JOIN] Calling StartGame. Mode: '{startGameArgs.GameMode}', SessionName: '{startGameArgs.SessionName}'");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner != null && _runner.SessionInfo != null)
            {
                Debug.Log($"[MATCH JOIN] StartGame OK. Joined Session: '{_runner.SessionInfo.Name}'");
                UpdateLocalSessionInfoFromRunner(); // Store joined session info
                _isJoining = false;
                // Scene should load automatically
                return true;
            }
            else
            {
                Debug.LogError($"[MATCH JOIN] StartGame FAILED: {result.ShutdownReason}");
                await ResetNetworkRunner(); // Clean up failed runner
                _isJoining = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MATCH JOIN] StartClientGameBySessionInfo EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner(); // Clean up on exception
            _isJoining = false;
            return false;
        }
    }


    // --- NEW: Matchmaking Method ---
    /// <summary>
    /// Attempts to find a suitable public game based on filters and join it.
    /// </summary>
    /// <param name="filters">Dictionary of SessionProperties to filter by (e.g., "lang" = "English"). Can be null or empty for no filtering.</param>
    /// <returns>True if a suitable game was found and the joining process was started, false otherwise.</returns>
    public async Task<bool> FindAndJoinPublicGame(Dictionary<string, SessionProperty> filters = null)
    {
        Debug.Log("--- [MATCHMAKING] FindAndJoinPublicGame ---");
        if (_isJoining) { Debug.LogWarning("[MATCHMAKING] Already attempting to join/host. Aborting."); return false; }

        _isJoining = true; // Set joining flag early
        try
        {
            // 1. Refresh the session list
            Debug.Log("[MATCHMAKING] Refreshing session list...");
            bool refreshSuccess = await RefreshSessionList(); // Ensure RefreshSessionList returns success/failure
            if (!refreshSuccess)
            {
                Debug.LogError("[MATCHMAKING] Failed to refresh session list.");
                _isJoining = false;
                return false;
            }
            // Optional delay might still be useful depending on Photon timings
            await Task.Delay(500);

            // 2. Get the latest list (populated by OnSessionListUpdated)
            List<SessionInfo> currentSessions = GetAvailableSessions();
            Debug.Log($"[MATCHMAKING] Found {currentSessions.Count} sessions after refresh.");

            // 3. Filter the list
            List<SessionInfo> potentialMatches = currentSessions.Where(session =>
            {
                // Basic checks
                if (!session.IsVisible || !session.IsOpen || session.PlayerCount >= session.MaxPlayers) return false;

                // Apply custom filters
                if (filters != null && filters.Count > 0)
                {
                    foreach (var filter in filters)
                    {
                        if (!session.Properties.TryGetValue(filter.Key, out SessionProperty sessionValue) ||
                            !Equals(sessionValue?.PropertyValue, filter.Value?.PropertyValue)) // Check for null property value too
                        {
                            return false; // Property missing or value doesn't match
                        }
                    }
                }
                return true; // Passed all checks

            }).ToList();

            Debug.Log($"[MATCHMAKING] Found {potentialMatches.Count} potential matches after filtering.");

            // 4. Select and join a match
            if (potentialMatches.Count > 0)
            {
                // Simple selection: take the first one. Could add sorting later (e.g., by player count).
                SessionInfo bestMatch = potentialMatches[0];
                Debug.Log($"[MATCHMAKING] Attempting to join session: {bestMatch.Name}");

                // Use the specific join method
                bool joinStarted = await StartClientGameBySessionInfo(bestMatch);

                // StartClientGameBySessionInfo now handles _isJoining flag on its own path.
                // We return the result directly. If it failed, _isJoining should be false.
                return joinStarted;
            }
            else
            {
                Debug.Log("[MATCHMAKING] No suitable public games found matching criteria.");
                _isJoining = false; // Reset flag as no join was attempted
                return false; // No match found
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MATCHMAKING] FindAndJoinPublicGame EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner(); // Clean up on exception
            _isJoining = false;
            return false;
        }
    }


    // --- Refreshes the session list by joining the lobby ---
    // Returns true if lobby join started successfully AND list was received, false otherwise.
    public async Task<bool> RefreshSessionList()
    {
        Debug.Log($"--- RefreshSessionList ---");

        // Shutdown existing runner ONLY if it's NOT already just a Client (lobby)
        // **** FIX 1: Use SimulationModes enum for comparison ****
        if (_runner != null && _runner.IsRunning && _runner.Mode != SimulationModes.Client)
        {
            Debug.Log($"[Refresh] Runner is active in Mode '{_runner.Mode}'. Shutting down...");
            await _runner.Shutdown();
            await Task.Delay(200); // Give time for shutdown
                                   // Destroying and recreating might be necessary if Shutdown doesn't fully clean up
            Destroy(_runner);
            await Task.Yield(); // Wait a frame for destroy to process
            _runner = null;
        }

        // Ensure we have a runner instance, create if needed
        if (_runner == null || _runner.IsShutdown)
        {
            Debug.Log($"[Refresh] Runner is null or shutdown. Adding/re-adding component for lobby join.");
            _runner = GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this); // Ensure callbacks are registered
        }

        // --- Critical: Disable Scene Manager for Lobby Join ---
        if (_sceneManager != null) _sceneManager.enabled = false;
        Debug.Log($"[Refresh] Disabled NetworkSceneManager component.");

        _sessionListTask = new TaskCompletionSource<bool>(); // Reset task for waiting

        try
        {
            // Start game in Client mode with NULL session name to join the lobby
            var args = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = null, // JOIN LOBBY
                SceneManager = null // DO NOT manage scenes when joining lobby
                // **** FIX 2: Removed DisableClientSessionCreation ****
                // Use AppSettings region by default
            };

            Debug.Log($"[Refresh] Calling StartGame to join lobby...");
            var result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Refresh] FAILED to start client to join lobby: {result.ShutdownReason}");
                if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable scene manager on failure
                _sessionListTask.TrySetResult(false);
                await ResetNetworkRunner(); // Cleanup failed lobby runner
                return false;
            }

            Debug.Log($"[Refresh] Client started for lobby join. Waiting for session list (30s timeout)...");
            // Wait for OnSessionListUpdated callback OR timeout
            var timeoutTask = Task.Delay(30000);
            var completedTask = await Task.WhenAny(_sessionListTask.Task, timeoutTask);

            if (completedTask == _sessionListTask.Task && _sessionListTask.Task.Result)
            {
                Debug.Log($"[Refresh] Session list received successfully.");
                return true; // Success!
            }
            else
            {
                Debug.LogWarning($"[Refresh] Timed out or failed waiting for session list update.");
                _sessionListTask.TrySetCanceled(); // Mark as canceled if timed out
                                                   // Don't ResetNetworkRunner here, as the lobby connection might still be useful or shutdown elsewhere
                return false; // Failure (timeout or explicit false result)
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Refresh] RefreshSessionList EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable on exception
            _sessionListTask.TrySetException(ex);
            await ResetNetworkRunner(); // Cleanup on exception
            return false;
        }
    }


    // --- Utility to clean up and recreate the runner ---
    public async Task ResetNetworkRunner()
    {
        Debug.Log($"ResetNetworkRunner called.");
        if (_runner != null)
        {
            if (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown)
            {
                Debug.Log($"[Reset] Shutting down existing runner...");
                await _runner.Shutdown(destroyGameObject: false); // Shutdown without destroying host GO
                await Task.Delay(200); // Allow time for shutdown process
            }

            // Check if component still exists before destroying (Shutdown might handle it)
            if (_runner != null && !_runner.IsUnityNull())
            {
                Debug.Log($"[Reset] Destroying runner component.");
                Destroy(_runner);
                await Task.Yield(); // Wait a frame for destroy
            }
            _runner = null;
        }

        Debug.Log($"[Reset] Adding new runner component and callbacks.");
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);

        // --- Critical: Re-enable Scene Manager after reset ---
        if (_sceneManager != null)
        {
            _sceneManager.enabled = true;
            Debug.Log($"[Reset] Ensured SceneManager component is enabled.");
        }
        else
        {
            Debug.LogWarning("[Reset] SceneManager component is null!");
        }

        // Clear local session state
        SessionDisplayName = string.Empty;
        SessionUniqueID = string.Empty;
        SessionHash = string.Empty;
        SessionStartTime = 0;
        _availableSessions.Clear();
        _isJoining = false; // Reset joining flag
        Debug.Log("[Reset] Cleared session info and reset joining flag.");
    }

    // --- Helper to setup session codes ---
    private void SetupSessionCode(string sessionNameBase)
    {
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (scm == null)
        {
            Debug.LogWarning("SessionCodeManager not found! Generating fallback codes.");
            SessionHash = ComputeSessionHash(Guid.NewGuid().ToString()).Substring(0, 6).ToUpper(); // Simple random hash
            SessionUniqueID = Guid.NewGuid().ToString(); // Internal Photon name needs to be unique
        }
        else
        {
            SessionHash = scm.GenerateNewSessionCode(); // Your 6-char code
            SessionUniqueID = scm.GetInternalId(SessionHash); // Get the unique ID Photon needs
        }
        SessionDisplayName = string.IsNullOrEmpty(sessionNameBase) ? $"Session {SessionHash}" : sessionNameBase;
        SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    // --- Helper to update local session vars after joining ---
    private void UpdateLocalSessionInfoFromRunner()
    {
        if (_runner == null || _runner.SessionInfo == null) return;

        SessionUniqueID = _runner.SessionInfo.Name;
        // Try get properties stored by host
        if (_runner.SessionInfo.Properties.TryGetValue("Hash", out var hashObj)) SessionHash = hashObj?.PropertyValue?.ToString() ?? "N/A"; else SessionHash = "N/A";
        if (_runner.SessionInfo.Properties.TryGetValue("DisplayName", out var displayNameObj)) SessionDisplayName = displayNameObj?.PropertyValue?.ToString() ?? SessionUniqueID; else SessionDisplayName = SessionUniqueID;
        if (_runner.SessionInfo.Properties.TryGetValue("StartTime", out var startTimeObj) && startTimeObj.PropertyValue is int stVal) SessionStartTime = stVal; else SessionStartTime = 0;
        Debug.Log($"Updated local session info: ID='{SessionUniqueID}', Hash='{SessionHash}', Name='{SessionDisplayName}'");
    }

    // Simple hash fallback if SessionCodeManager isn't used
    private string ComputeSessionHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return "NOHASH";
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        { byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input); byte[] hashBytes = md5.ComputeHash(inputBytes); return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8); }
    }

    // Load scene via runner if active
    public async Task LoadScene(string sceneName)
    {
        if (SceneUtility.GetBuildIndexByScenePath($"Assets/Project/Scenes/{sceneName}.unity") < 0)
        {
            Debug.LogError($"Scene '{sceneName}' not found in build settings!");
            return;
        }

        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log($"Runner loading scene: {sceneName}");
            // Ensure Scene Manager is enabled before loading scenes via runner
            if (_sceneManager != null) _sceneManager.enabled = true;
            await _runner.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"Runner not active, cannot load scene '{sceneName}' via runner.");
            // Fallback or error handling needed?
        }
    }

    // Shutdown the current game/runner
    public async Task ShutdownGame()
    {
        Debug.Log("ShutdownGame called.");
        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log("Shutting down active runner...");
            await _runner.Shutdown(destroyGameObject: false); // Shutdown runner, keep handler GO
            // ResetNetworkRunner might be called automatically by OnShutdown callback
        }
        else
        {
            Debug.Log("No active runner to shut down.");
            // Ensure we are back at main menu even if no runner was active
            if (SceneManager.GetActiveScene().name != _mainMenuSceneName)
            {
                SceneManager.LoadScene(_mainMenuSceneName);
            }
        }
        // Clear session info after shutdown
        SessionDisplayName = string.Empty; SessionUniqueID = string.Empty; SessionHash = string.Empty; SessionStartTime = 0; _isJoining = false;
    }


    // --- INetworkRunnerCallbacks Implementation ---

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined.");
        if (runner.IsServer) // Only server spawns objects
        {
            Debug.Log($"Spawning character for player {player}");
            Transform spawnPoint = GetSpawnPoint(); // Find a spawn point
            Vector3 spawnPos = spawnPoint ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

            try
            {
                // Spawn Player Prefab
                NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPos, spawnRot, player);
                if (networkPlayerObject != null)
                {
                    _spawnedCharacters.Add(player, networkPlayerObject);
                    Debug.Log($"Successfully spawned character for player {player}");

                    // Optional: Assign player name if PlayerController script has a method
                    // PlayerController pc = networkPlayerObject.GetComponent<PlayerController>();
                    // if (pc != null) pc.SetPlayerName(GetPlayerName()); // You need a way to get the joining player's name
                }
                else
                {
                    Debug.LogError($"Failed to spawn character for player {player}");
                }

                // Spawn Game State Manager if it doesn't exist (only once)
                if (FindFirstObjectByType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid)
                {
                    Debug.Log("Spawning GameStateManager...");
                    runner.Spawn(_gameStateManagerPrefab, Vector3.zero, Quaternion.identity);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception during player spawn: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} left.");
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            if (networkObject != null)
            {
                Debug.Log($"Despawning character for player {player}");
                runner.Despawn(networkObject);
            }
            _spawnedCharacters.Remove(player);
        }
        else
        {
            Debug.LogWarning($"Player {player} left, but no spawned character found in dictionary.");
        }
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"--- OnSessionListUpdated --- Received {sessionList.Count} raw sessions.");

        // Clear previous list and repopulate with valid, visible sessions
        _availableSessions.Clear();
        foreach (var session in sessionList)
        {
            // Basic filtering (can add more checks here)
            if (session.IsVisible && !session.Name.StartsWith("BROWSER_") && !session.Name.StartsWith("TempDiscoverySession_"))
            {
                _availableSessions.Add(session);

                // Log details of valid sessions
                string hash = session.Properties.TryGetValue("Hash", out var hashProp) ? hashProp?.PropertyValue?.ToString() ?? "NULL_PROP" : "NO_HASH";
                string dispName = session.Properties.TryGetValue("DisplayName", out var nameProp) ? nameProp?.PropertyValue?.ToString() ?? "NULL_PROP" : session.Name;
                Debug.Log($"  > Valid Session: Name='{session.Name}', Display='{dispName}', Hash='{hash}', Players={session.PlayerCount}/{session.MaxPlayers}, Open={session.IsOpen}");
            }
            else
            {
                // Debug.Log($"  > Ignoring Session: Name='{session.Name}', Visible={session.IsVisible}");
            }
        }
        Debug.Log($"Filtered list contains {_availableSessions.Count} sessions.");

        // Signal completion if waiting
        if (_sessionListTask != null && !_sessionListTask.Task.IsCompleted)
        {
            Debug.Log($"[OnSessionListUpdated] Setting session list task to complete (true).");
            _sessionListTask.TrySetResult(true);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"--- OnShutdown --- Reason: {shutdownReason}");

        // Cleanup session code if host
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (!string.IsNullOrEmpty(SessionHash) && scm != null) scm.EndSession(SessionHash); // Notify manager

        // Clear local state
        _spawnedCharacters.Clear();
        _isJoining = false; // Ensure joining flag is reset

        Debug.Log($"Current Scene: {SceneManager.GetActiveScene().name}");
        // Return to Main Menu if not already there
        if (SceneManager.GetActiveScene().name != _mainMenuSceneName)
        {
            Debug.Log($"Loading {_mainMenuSceneName} scene after shutdown.");
            SceneManager.LoadScene(_mainMenuSceneName);
        }
        else
        {
            Debug.Log("Already in MainMenu scene or scene name mismatch.");
            // If shutdown happened *in* main menu (e.g., failed join), potentially reset UI
            MainMenuUI mainMenu = FindFirstObjectByType<MainMenuUI>();
            mainMenu?.ShowPanel(mainMenu.transform.Find("MainPanel")?.gameObject); // Try to show main panel
        }

        // Ensure scene manager is enabled for future operations
        if (_sceneManager != null) _sceneManager.enabled = true;

        // Optionally destroy the runner component ONLY if the handler GO persists
        // If the handler GO is destroyed on scene change, this is not needed.
        if (runner != null && !_runner.IsUnityNull() && gameObject != null && gameObject.scene.isLoaded) // Check if GO still valid
        {
            //Destroy(runner); // Be careful with this if runner is on the same GO as handler
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[OnSceneLoadDone] Callback triggered. IsServer: {runner.IsServer}. Loaded Scene: '{currentSceneName}'.");

        // Server-specific actions after scene load (like spawning scene objects)
        if (runner.IsServer)
        {
            // Spawn Vehicle in Game or Lobby scene if it doesn't exist
            if (currentSceneName == _lobbySceneName || currentSceneName == _gameSceneName)
            {
                Debug.Log($"Server loaded scene '{currentSceneName}'. Checking for vehicle...");
                if (FindFirstObjectByType<NetworkedPrometeoCar>() == null) // Check by specific vehicle script
                {
                    Debug.Log("No vehicle found. Spawning...");
                    if (_vehiclePrefab != null && _vehiclePrefab.IsValid)
                    {
                        Transform spawnPoint = GetSpawnPoint(); // Use player spawn or dedicated vehicle spawn
                        Vector3 vehicleSpawnPos = spawnPoint ? spawnPoint.position + spawnPoint.forward * 2f + Vector3.up * 0.5f : new Vector3(0, 0.5f, 5);
                        Debug.Log($"Spawning vehicle prefab '{_vehiclePrefab}' at {vehicleSpawnPos}");
                        runner.Spawn(_vehiclePrefab, vehicleSpawnPos, Quaternion.identity);
                    }
                    else { Debug.LogError("Vehicle Prefab is invalid or not assigned!"); }
                }
                else { Debug.Log("Vehicle already exists. Skipping spawn."); }

                // Spawn Game State Manager if needed (redundant check, also in OnPlayerJoined)
                if (FindFirstObjectByType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid)
                {
                    Debug.Log("Spawning GameStateManager in OnSceneLoadDone...");
                    runner.Spawn(_gameStateManagerPrefab);
                }
            }
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"--- OnConnectedToServer --- Region: '{runner.SessionInfo?.Region ?? "N/A"}'");
        // Called when client successfully connects (before session join confirmation)
        UpdateLocalSessionInfoFromRunner(); // Update session info when connected
    }

    // --- Helper to find spawn points ---
    private Transform GetSpawnPoint()
    {
        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints.Length > 0)
        {
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform;
        }
        Debug.LogWarning("No SpawnPoint objects found in the scene! Defaulting to Vector3.zero.");
        return null;
    }

    // --- Other INetworkRunnerCallbacks (can be left empty if not needed) ---
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.LogError($"Connect failed: {reason}"); _isJoining = false; }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { Debug.Log("[OnSceneLoadStart] Scene load initiated by runner."); }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"Disconnected from server: {reason}"); _isJoining = false; }

}