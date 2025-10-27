// Filename: NetworkRunnerHandler.cs
using Fusion;
using Fusion.Photon.Realtime;
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
    // --- ADD THIS CONSTANT ---
    public const string SESSION_LANGUAGE_KEY = "language";

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

    // --- ADD THIS HELPER METHOD ---
    private string GetPhotonRegionCode(string regionText)
    {
        // Use PhotonAppSettings regions if available and sensible, otherwise map manually
        // Note: PhotonAppSettings.Instance.AvailableRegions is often just a list of Region Pings
        // It's usually better to define your supported regions explicitly.
        switch (regionText?.ToLower()) // Added null check and ToLower for robustness
        {
            case "na east":
            case "us east":
            case "us": // Common abbreviation
                return "us"; // Or "use" if you specifically configured US East in Photon Dashboard
            case "eu":
                return "eu";
            case "asia":
                return "asia";
            case "sa": // South America
                return "sa";
            case "jp": // Japan
                return "jp";
            // Add other regions your dropdown supports
            default:
                Debug.LogWarning($"Unknown region text '{regionText}', defaulting to 'best'. You might want Photon to auto-select.");
                // return "best"; // Let Photon pick based on ping
                return string.Empty; // Often better: Use string.Empty for AppSettings FixedRegion to let Photon pick best ping.
        }
    }


    // --- MODIFIED: StartHostGame now accepts visibility, region, language, and custom properties ---
    // Note: Combined args from StartGameUI into this one method for clarity
    public async Task StartHostGame(string sessionNameBase, bool isVisible = true, string regionText = "best", string language = "English", Dictionary<string, SessionProperty> customProps = null)
    {
        Debug.Log($"--- [HOST] StartHostGame ---");
        Debug.Log($"SessionName base: '{sessionNameBase}', IsVisible: {isVisible}, RegionText: '{regionText}', Language: '{language}'");

        try
        {
            await ResetNetworkRunner(); // Ensure a clean runner
            _runner.ProvideInput = true;

            // Generate session codes/IDs using SessionCodeManager
            SetupSessionCode(sessionNameBase);
            Debug.Log($"[HOST] SessionCode setup. Hash: '{SessionHash}', UniqueID: '{SessionUniqueID}'");

            // --- Prepare Session Properties ---
            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>
            {
                { "DisplayName", SessionDisplayName },
                { "Hash", SessionHash },
                { "StartTime", (int)SessionStartTime },
                // --- ADD LANGUAGE PROPERTY ---
                { SESSION_LANGUAGE_KEY, language }
            };

            // Add custom properties passed in
            if (customProps != null)
            {
                foreach (var prop in customProps)
                {
                    if (!sessionProps.ContainsKey(prop.Key)) // Avoid overwriting base/language props
                    {
                        sessionProps.Add(prop.Key, prop.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"[HOST] Custom property key '{prop.Key}' conflicts with a base/language property. Ignoring custom value.");
                    }
                }
            }

            // --- Prepare AppSettings for Region ---
            string regionCode = GetPhotonRegionCode(regionText);
            var appSettings = new AppSettings
            {
                AppIdFusion = Photon.Realtime.PhotonAppSettings.Instance.AppIdFusion, // Get AppId correctly
                AppVersion = Application.version, // Use actual app version
                FixedRegion = regionCode // Use the mapped code (or empty string for 'best')
                // UseNameServer = true // Usually default, ensure PhotonAppSettings resource exists
            };
            Debug.Log($"[HOST] Using Region Code: '{regionCode}' (from text: '{regionText}'). FixedRegion set to: '{appSettings.FixedRegion}'");


            // Log the properties being sent
            Debug.Log($"[HOST] StartGame Args - Properties to send:");
            foreach (var prop in sessionProps) { Debug.Log($"  > Key: '{prop.Key}', Value: '{prop.Value?.PropertyValue}' Type: {prop.Value?.PropertyType}"); }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID, // Use the internal unique ID
                SceneManager = _sceneManager,
                SessionProperties = sessionProps,
                PlayerCount = _maxPlayers,
                IsVisible = isVisible,
                IsOpen = true,
                AppSettings = appSettings // Pass the configured AppSettings
                // Scene = SceneRef.FromIndex(...) // Optional: Load scene directly
            };

            Debug.Log($"[HOST] Calling StartGame...");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"[HOST] StartGame OK. Runner Active. Session: '{_runner.SessionInfo?.Name}', Region: '{_runner.SessionInfo?.Region}'");
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID);
                PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName);
                PlayerPrefs.Save();
                await LoadScene(_lobbySceneName); // Load lobby scene after successful host start
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
            // Important: Let AppSettings handle the region for joining (don't override here usually)
            var appSettings = new AppSettings
            {
                AppIdFusion = Photon.Realtime.PhotonAppSettings.Instance.AppIdFusion,
                AppVersion = Application.version,
                // FixedRegion = "" // Leave empty or null to use PhotonAppSettings default / best ping for joining
            };

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = null, // Null = Join using filter
                SceneManager = _sceneManager,
                SessionProperties = sessionPropsFilter, // The matchmaking filter
                AppSettings = appSettings
            };

            Debug.Log($"[DIRECT JOIN] Calling StartGame to FIND session with Hash: '{roomCode}'");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner != null && _runner.SessionInfo != null)
            {
                Debug.Log($"[DIRECT JOIN] StartGame OK. Joined session: '{_runner.SessionInfo.Name}', Region: '{_runner.SessionInfo.Region}'");
                UpdateLocalSessionInfoFromRunner();
                _isJoining = false;
                // Scene should load automatically
                return true;
            }
            else
            {
                Debug.LogError($"[DIRECT JOIN] StartGame FAILED: {result.ShutdownReason}");
                await ResetNetworkRunner();
                _isJoining = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DIRECT JOIN] StartClientGameByHash EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner();
            _isJoining = false;
            return false;
        }
    }


    // --- Join specific game using SessionInfo (Used by Matchmaking List Click) ---
    public async Task<bool> StartClientGameBySessionInfo(SessionInfo sessionInfo)
    {
        Debug.Log($"--- [MATCH JOIN] StartClientGameBySessionInfo ---");
        Debug.Log($"Attempting to join session: '{sessionInfo.Name}' in region '{sessionInfo.Region}'");

        if (sessionInfo == null) { Debug.LogError("[MATCH JOIN] SessionInfo is null."); return false; }
        if (_isJoining) { Debug.LogWarning("[MATCH JOIN] Already attempting to join. Aborting."); return false; }

        _isJoining = true;
        try
        {
            // If runner was used for lobby query, shut it down first.
            if (_runner != null && (_runner.IsRunning || _runner.IsCloudReady))
            {
                Debug.Log($"[MATCH JOIN] Shutting down existing runner before joining specific session.");
                await _runner.Shutdown();
                await Task.Delay(200); // Small delay
            }

            // Ensure runner exists
            if (_runner == null || _runner.IsShutdown)
            {
                Debug.LogWarning("[MATCH JOIN] Runner was shut down or null. Re-adding component.");
                _runner = GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
                _runner.AddCallbacks(this);
            }

            _runner.ProvideInput = true;

            // Configure AppSettings to target the specific region of the selected session
            var appSettings = new AppSettings
            {
                AppIdFusion = Photon.Realtime.PhotonAppSettings.Instance.AppIdFusion,
                AppVersion = Application.version,
                FixedRegion = sessionInfo.Region // CRITICAL: Target the correct region
            };
            Debug.Log($"[MATCH JOIN] Setting FixedRegion to: '{appSettings.FixedRegion}'");


            // Start game in Client mode targeting the specific SessionName
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionInfo.Name, // Target the specific session
                SceneManager = _sceneManager,
                AppSettings = appSettings // Use region-specific settings
            };

            Debug.Log($"[MATCH JOIN] Calling StartGame. Mode: '{startGameArgs.GameMode}', SessionName: '{startGameArgs.SessionName}'");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner != null && _runner.SessionInfo != null)
            {
                Debug.Log($"[MATCH JOIN] StartGame OK. Joined Session: '{_runner.SessionInfo.Name}', Region: '{_runner.SessionInfo.Region}'");
                UpdateLocalSessionInfoFromRunner();
                _isJoining = false;
                // Scene should load automatically
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
            Debug.LogError($"[MATCH JOIN] StartClientGameBySessionInfo EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner();
            _isJoining = false;
            return false;
        }
    }


    // --- Matchmaking: Find and Join Public Game based on Filters ---
    /// <param name="regionText">Region preference (e.g., "NA East", "EU"). "best" or null/empty for any region.</param>
    /// <param name="filters">Other filters like language {"language", "English"}.</param>
    public async Task<bool> FindAndJoinPublicGame(string regionText = null, Dictionary<string, SessionProperty> filters = null)
    {
        Debug.Log($"--- [MATCHMAKING] FindAndJoinPublicGame --- Region Pref: '{regionText}'");
        if (_isJoining) { Debug.LogWarning("[MATCHMAKING] Already attempting. Aborting."); return false; }

        _isJoining = true;
        try
        {
            // 1. Refresh the session list in the desired region (or all regions)
            string targetRegionCode = string.IsNullOrEmpty(regionText) || regionText.ToLower() == "best" ? null : GetPhotonRegionCode(regionText);
            Debug.Log($"[MATCHMAKING] Refreshing session list for region code: '{targetRegionCode ?? "All"}'.");
            bool refreshSuccess = await RefreshSessionList(targetRegionCode); // Pass region code
            if (!refreshSuccess)
            {
                Debug.LogError("[MATCHMAKING] Failed to refresh session list.");
                _isJoining = false;
                return false;
            }
            await Task.Delay(500); // Give list time to populate

            // 2. Get the latest list
            List<SessionInfo> currentSessions = GetAvailableSessions();
            Debug.Log($"[MATCHMAKING] Found {currentSessions.Count} total sessions after refresh.");

            // 3. Filter the list
            List<SessionInfo> potentialMatches = currentSessions.Where(session =>
            {
                if (!session.IsVisible || !session.IsOpen || session.PlayerCount >= session.MaxPlayers) return false;

                // Region filter (if specific region was requested AND list wasn't pre-filtered)
                // If RefreshSessionList was called with a region code, this check is technically redundant but harmless.
                if (!string.IsNullOrEmpty(targetRegionCode) && session.Region != targetRegionCode)
                {
                    // Debug.Log($"Filtering out session '{session.Name}' due to region mismatch ({session.Region} != {targetRegionCode})");
                    return false;
                }


                // Apply custom filters (e.g., language)
                if (filters != null && filters.Count > 0)
                {
                    foreach (var filter in filters)
                    {
                        if (!session.Properties.TryGetValue(filter.Key, out SessionProperty sessionValue) ||
                            !Equals(sessionValue?.PropertyValue, filter.Value?.PropertyValue))
                        {
                            // Debug.Log($"Filtering out session '{session.Name}' due to property mismatch on key '{filter.Key}'");
                            return false;
                        }
                    }
                }
                // Debug.Log($"Session '{session.Name}' passed filters.");
                return true;

            }).OrderBy(s => s.PlayerCount) // Optional: Prioritize less full sessions
              .ToList();

            Debug.Log($"[MATCHMAKING] Found {potentialMatches.Count} potential matches after filtering.");

            // 4. Select and join a match
            if (potentialMatches.Count > 0)
            {
                SessionInfo bestMatch = potentialMatches[0];
                Debug.Log($"[MATCHMAKING] Attempting to join session: {bestMatch.Name} in region {bestMatch.Region}");

                // Use the specific join method (which now sets the region correctly)
                bool joinStarted = await StartClientGameBySessionInfo(bestMatch);
                return joinStarted; // This method handles the _isJoining flag reset
            }
            else
            {
                Debug.Log("[MATCHMAKING] No suitable public games found matching criteria.");
                _isJoining = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MATCHMAKING] FindAndJoinPublicGame EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            await ResetNetworkRunner();
            _isJoining = false;
            return false;
        }
    }


    // --- MODIFIED: Refreshes the session list, optionally for a specific region ---
    public async Task<bool> RefreshSessionList(string regionCode = null) // Added optional region parameter
    {
        Debug.Log($"--- RefreshSessionList --- Target Region Code: '{regionCode ?? "All"}'");

        // Shutdown existing runner ONLY if it's NOT a Client (lobby mode is Client)
        if (_runner != null && _runner.IsRunning && _runner.Mode != SimulationModes.Client)
        {
            Debug.Log($"[Refresh] Runner is active in Mode '{_runner.Mode}'. Shutting down...");
            await _runner.Shutdown();
            await Task.Delay(200);
            Destroy(_runner); // Consider destroying component to ensure clean state
            await Task.Yield();
            _runner = null;
        }

        // Ensure runner exists
        if (_runner == null || _runner.IsShutdown)
        {
            Debug.Log($"[Refresh] Runner is null or shutdown. Adding component for lobby join.");
            _runner = GetComponent<NetworkRunner>() ?? gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this);
        }

        // Disable Scene Manager for Lobby Join
        if (_sceneManager != null) _sceneManager.enabled = false;
        Debug.Log($"[Refresh] Disabled NetworkSceneManager component.");

        _sessionListTask = new TaskCompletionSource<bool>(); // Reset task

        try
        {
            // Configure AppSettings for joining the lobby, potentially in a specific region
            var appSettings = new AppSettings
            {
                AppIdFusion = Photon.Realtime.PhotonAppSettings.Instance.AppIdFusion,
                AppVersion = Application.version,
                FixedRegion = regionCode ?? string.Empty // Use specified region or let Photon pick best/use default
            };
            Debug.Log($"[Refresh] Joining lobby with FixedRegion: '{appSettings.FixedRegion}'");

            // Start game in Client mode to join the lobby
            var args = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = null, // JOIN LOBBY
                SceneManager = null, // DO NOT manage scenes when joining lobby
                AppSettings = appSettings // Pass region preference
            };

            Debug.Log($"[Refresh] Calling StartGame to join lobby...");
            var result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Refresh] FAILED to start client to join lobby: {result.ShutdownReason}");
                if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable on failure
                _sessionListTask.TrySetResult(false);
                await ResetNetworkRunner();
                return false;
            }

            Debug.Log($"[Refresh] Client started for lobby join. Waiting for session list (30s timeout)...");
            // Wait for OnSessionListUpdated OR timeout
            var timeoutTask = Task.Delay(30000);
            var completedTask = await Task.WhenAny(_sessionListTask.Task, timeoutTask);

            if (completedTask == _sessionListTask.Task && _sessionListTask.Task.Result)
            {
                Debug.Log($"[Refresh] Session list received successfully.");
                // Don't shutdown lobby runner immediately, it might be reused or shutdown by join methods
                return true;
            }
            else
            {
                Debug.LogWarning($"[Refresh] Timed out or failed waiting for session list update.");
                _sessionListTask.TrySetCanceled();
                // Consider shutting down the lobby runner if timeout occurred
                // await ResetNetworkRunner(); // Or just await _runner.Shutdown();
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Refresh] RefreshSessionList EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            if (_sceneManager != null) _sceneManager.enabled = true;
            _sessionListTask.TrySetException(ex);
            await ResetNetworkRunner();
            return false;
        }
        // finally // Consider re-enabling scene manager in finally if lobby runner isn't immediately shut down
        // {
        //     if (_sceneManager != null) _sceneManager.enabled = true;
        // }
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
                await _runner.Shutdown(destroyGameObject: false);
                await Task.Delay(200);
            }

            if (_runner != null && !_runner.IsUnityNull())
            {
                Debug.Log($"[Reset] Destroying runner component.");
                Destroy(_runner);
                await Task.Yield();
            }
            _runner = null;
        }

        Debug.Log($"[Reset] Adding new runner component and callbacks.");
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);

        // Re-enable Scene Manager after reset
        if (_sceneManager != null)
        {
            _sceneManager.enabled = true;
            Debug.Log($"[Reset] Ensured SceneManager component is enabled.");
        }
        else
        {
            // Try to get it again if it was null
            _sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (_sceneManager != null) _sceneManager.enabled = true;
            else Debug.LogWarning("[Reset] SceneManager component is still null after reset!");
        }

        // Clear local session state
        SessionDisplayName = string.Empty; SessionUniqueID = string.Empty; SessionHash = string.Empty; SessionStartTime = 0;
        _availableSessions.Clear();
        _isJoining = false;
        Debug.Log("[Reset] Cleared session info and reset joining flag.");
    }

    // --- Helper to setup session codes ---
    private void SetupSessionCode(string sessionNameBase)
    {
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>(); // Consider using Singleton pattern: SessionCodeManager.Instance
        if (scm == null)
        {
            Debug.LogError("SessionCodeManager not found! Cannot generate proper codes.");
            // Fallback (less ideal)
            SessionHash = ComputeSessionHash(Guid.NewGuid().ToString()).Substring(0, 6).ToUpper();
            SessionUniqueID = Guid.NewGuid().ToString();
        }
        else
        {
            // Use your SessionCodeManager logic
            SessionHash = scm.GenerateNewSessionCode();     // Generates "AdjectiveNoun" or similar
            SessionUniqueID = scm.GetInternalId(SessionHash); // Gets the underlying Guid string
        }
        SessionDisplayName = string.IsNullOrEmpty(sessionNameBase) ? $"Session {SessionHash}" : sessionNameBase;
        SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Debug.Log($"SetupSessionCode: Hash='{SessionHash}', UniqueID='{SessionUniqueID}', DisplayName='{SessionDisplayName}'");
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
        // Use SceneUtility.GetBuildIndexByScenePath with the correct path format
        string scenePath = $"Assets/Project/Scenes/{sceneName}.unity";
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);

        if (buildIndex < 0)
        {
            Debug.LogError($"Scene '{sceneName}' (Path: {scenePath}) not found in build settings!");
            return;
        }

        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log($"Runner loading scene: {sceneName} (Index: {buildIndex})");
            // Ensure Scene Manager is enabled before loading scenes via runner
            if (_sceneManager != null) _sceneManager.enabled = true;
            else { Debug.LogWarning("SceneManager is null, scene loading might fail."); }

            // Use the build index for LoadScene
            await _runner.LoadScene(SceneRef.FromIndex(buildIndex));
        }
        else
        {
            Debug.LogWarning($"Runner not active, cannot load scene '{sceneName}' via runner.");
            // Fallback: Load scene directly if not networked? Usually not desired.
            // SceneManager.LoadScene(sceneName);
        }
    }

    // Shutdown the current game/runner
    public async Task ShutdownGame()
    {
        Debug.Log("ShutdownGame called.");
        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log("Shutting down active runner...");
            await _runner.Shutdown(destroyGameObject: false); // Let OnShutdown handle scene change etc.
        }
        else
        {
            Debug.Log("No active runner to shut down.");
            // Force return to main menu if needed
            if (SceneManager.GetActiveScene().name != _mainMenuSceneName)
            {
                Debug.Log($"No runner, directly loading {_mainMenuSceneName}.");
                SceneManager.LoadScene(_mainMenuSceneName);
            }
            // Clear local state manually if no runner shutdown callback occurs
            SessionDisplayName = string.Empty; SessionUniqueID = string.Empty; SessionHash = string.Empty; SessionStartTime = 0; _isJoining = false;
        }
    }


    // --- INetworkRunnerCallbacks Implementation ---

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined.");
        if (runner.IsServer)
        {
            Debug.Log($"Spawning character for player {player}");
            Transform spawnPoint = GetSpawnPoint();
            Vector3 spawnPos = spawnPoint ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

            try
            {
                NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPos, spawnRot, player);
                if (networkPlayerObject != null)
                {
                    _spawnedCharacters.Add(player, networkPlayerObject);
                    Debug.Log($"Successfully spawned character for player {player}");
                }
                else { Debug.LogError($"Failed to spawn character for player {player}"); }

                // Spawn Game State Manager if needed
                if (FindFirstObjectByType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid)
                {
                    Debug.Log("Spawning GameStateManager...");
                    runner.Spawn(_gameStateManagerPrefab, Vector3.zero, Quaternion.identity);
                }
            }
            catch (Exception ex) { Debug.LogError($"Exception during player spawn: {ex.Message}\n{ex.StackTrace}"); }
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
        else { Debug.LogWarning($"Player {player} left, but no spawned character found."); }
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"--- OnSessionListUpdated --- Received {sessionList.Count} raw sessions.");
        _availableSessions.Clear();
        foreach (var session in sessionList)
        {
            // More robust filtering
            bool isValid = session.IsVisible && session.IsOpen && !string.IsNullOrEmpty(session.Name) && !session.Name.StartsWith("TempDiscoverySession_");
            // Check for Hash property existence if required for display/joining
            bool hasHash = session.Properties.ContainsKey("Hash");

            if (isValid && hasHash)
            {
                _availableSessions.Add(session);
                string hash = session.Properties["Hash"]?.PropertyValue?.ToString() ?? "N/A";
                string dispName = session.Properties.TryGetValue("DisplayName", out var nameProp) ? nameProp?.PropertyValue?.ToString() ?? session.Name : session.Name;
                string lang = session.Properties.TryGetValue(SESSION_LANGUAGE_KEY, out var langProp) ? langProp?.PropertyValue?.ToString() ?? "N/A" : "N/A";
                Debug.Log($"  > Valid Session: Name='{session.Name}', Display='{dispName}', Hash='{hash}', Region='{session.Region}', Lang='{lang}', Players={session.PlayerCount}/{session.MaxPlayers}");
            }
            else
            {
                // Debug.Log($"  > Ignoring Session: Name='{session.Name}', Visible={session.IsVisible}, Open={session.IsOpen}, HasHash={hasHash}");
            }
        }
        Debug.Log($"Filtered list contains {_availableSessions.Count} sessions.");

        // Signal completion
        _sessionListTask?.TrySetResult(true);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"--- OnShutdown --- Reason: {shutdownReason}");

        // Cleanup session code if host
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>(); // Or SessionCodeManager.Instance
        if (runner.IsServer && !string.IsNullOrEmpty(SessionHash) && scm != null)
        {
            Debug.Log($"Ending session '{SessionHash}' via SessionCodeManager.");
            scm.EndSession(SessionHash);
        }


        // Clear local state
        _spawnedCharacters.Clear();
        _isJoining = false;

        Debug.Log($"Current Scene: {SceneManager.GetActiveScene().name}");
        // Return to Main Menu if not already there
        // Check prevents unnecessary scene reload if shutdown occurs IN main menu
        if (SceneManager.GetActiveScene().name != _mainMenuSceneName)
        {
            Debug.Log($"Loading {_mainMenuSceneName} scene after shutdown.");
            SceneManager.LoadScene(_mainMenuSceneName);
        }
        else
        {
            Debug.Log("Already in MainMenu scene. Resetting UI potentially.");
            // Reset UI elements if needed
            MainMenuUI mainMenu = FindFirstObjectByType<MainMenuUI>();
            mainMenu?.ShowPanel(mainMenu.transform.Find("MainPanel")?.gameObject); // Example reset
        }

        // Ensure scene manager is enabled for next time
        if (_sceneManager != null) _sceneManager.enabled = true;

        // Reset local session variables as runner is gone
        SessionDisplayName = string.Empty; SessionUniqueID = string.Empty; SessionHash = string.Empty; SessionStartTime = 0;
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[OnSceneLoadDone] Callback triggered. IsServer: {runner.IsServer}. Loaded Scene: '{currentSceneName}'.");

        if (runner.IsServer)
        {
            // Spawn Vehicle
            if (currentSceneName == _lobbySceneName || currentSceneName == _gameSceneName)
            {
                Debug.Log($"Server loaded scene '{currentSceneName}'. Checking for vehicle...");
                // Check using a specific script component on the vehicle prefab is more reliable
                if (FindFirstObjectByType<NetworkedPrometeoCar>() == null)
                {
                    Debug.Log("No vehicle found. Spawning...");
                    if (_vehiclePrefab != null && _vehiclePrefab.IsValid)
                    {
                        Transform spawnPoint = GetSpawnPoint();
                        Vector3 vehicleSpawnPos = spawnPoint ? spawnPoint.position + spawnPoint.forward * 2f + Vector3.up * 0.5f : new Vector3(0, 0.5f, 5);
                        Debug.Log($"Spawning vehicle prefab '{_vehiclePrefab}' at {vehicleSpawnPos}");
                        runner.Spawn(_vehiclePrefab, vehicleSpawnPos, Quaternion.identity);
                    }
                    else { Debug.LogError("Vehicle Prefab is invalid or not assigned!"); }
                }
                else { Debug.Log("Vehicle already exists."); }

                // Spawn Game State Manager (if not already done in OnPlayerJoined)
                if (FindFirstObjectByType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid)
                {
                    Debug.Log("Spawning GameStateManager in OnSceneLoadDone...");
                    runner.Spawn(_gameStateManagerPrefab);
                }
            }
        }
        // Client-side actions after scene load (e.g., initializing UI specific to the scene)
        if (runner.IsClient)
        {
            // Example: Find and setup UI specific to the loaded scene
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"--- OnConnectedToServer --- Region: '{runner.SessionInfo?.Region ?? "N/A"}'");
        // UpdateLocalSessionInfoFromRunner(); // Might be too early, SessionInfo props might not be synced yet. Better in OnPlayerJoined or first Spawned().
    }

    // --- Helper to find spawn points ---
    private Transform GetSpawnPoint()
    {
        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints.Length > 0)
        {
            // Simple random selection
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform;
            // Could add logic to find unused spawn points based on player positions if needed
        }
        Debug.LogWarning("No SpawnPoint objects found! Defaulting to Vector3.zero.");
        return null; // Return null explicitly if none found
    }

    // --- Other INetworkRunnerCallbacks ---
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Gather input here if needed for player control
        // Example: Find local player object and call a method on its input script
        // if (PlayerInput.Local != null) {
        //     input.Set(PlayerInput.Local.GetNetworkInput());
        // }
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.LogError($"Connect failed: {reason}"); _isJoining = false; /* Maybe show error UI */ }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { Debug.Log("[OnSceneLoadStart] Scene load initiated by runner."); }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"Disconnected from server: {reason}"); _isJoining = false; /* May need scene change or UI update */ }

}