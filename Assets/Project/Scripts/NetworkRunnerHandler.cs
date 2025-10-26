// Filename: NetworkRunnerHandler.cs
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network References")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private NetworkPrefabRef _gameStateManagerPrefab;
    [SerializeField] private NetworkPrefabRef _vehiclePrefab; // Vehicle prefab reference

    [Header("Scene Settings")]
    [SerializeField] private string _lobbySceneName = "Lobby";
    [SerializeField] private string _gameSceneName = "Game";

    [Header("Network Settings")]
    [SerializeField] private int _maxPlayers = 6;
    // We no longer need a region string here, it's set in PhotonAppSettings

    // Session information
    public string SessionDisplayName { get; private set; }
    public string SessionUniqueID { get; private set; }
    public string SessionHash { get; private set; }
    public long SessionStartTime { get; private set; }

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // TaskCompletionSource to properly wait for the session list
    private TaskCompletionSource<bool> _sessionListTask;

    // Store available sessions locally
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;


    public List<SessionInfo> GetAvailableSessions() => new List<SessionInfo>(_availableSessions);

    private void Awake()
    {
        InGameDebug.Log("NetworkRunnerHandler Awake.");
        Debug.Log("NetworkRunnerHandler Awake: Instance is being set.");

        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
        {
            Debug.Log("NetworkRunnerHandler Awake: Runner component not found, adding it.");
            _runner = gameObject.AddComponent<NetworkRunner>();
        }
        else
        {
            Debug.Log("NetworkRunnerHandler Awake: Runner component found.");
        }

        _runner.AddCallbacks(this);

        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null)
        {
            Debug.Log("NetworkRunnerHandler Awake: SceneManager component not found, adding it.");
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
        else
        {
            Debug.Log("NetworkRunnerHandler Awake: SceneManager component found.");
        }
    }

    private void Start()
    {
        if (FindObjectsByType<NetworkRunnerHandler>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    public async Task StartHostGame(string sessionName, string region = "auto", bool allowAllRegions = true)
    {
        try
        {
            InGameDebug.Log($"--- [HOST] StartHostGame ---");
            InGameDebug.Log($"SessionName base: '{sessionName}'");

            await ResetNetworkRunner();
            _runner.ProvideInput = true;
            SetupSessionCode(sessionName);

            InGameDebug.Log($"[HOST] SessionCode setup. Hash: '{SessionHash}', UniqueID: '{SessionUniqueID}'");

            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>
            { { "DisplayName", SessionDisplayName }, { "Hash", SessionHash }, { "StartTime", (int)SessionStartTime } };

            // Region is now handled by PhotonAppSettings

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = SessionUniqueID,
                SceneManager = _sceneManager,
                SessionProperties = sessionProps, // We will log these next
                PlayerCount = _maxPlayers,
                IsVisible = true,
                IsOpen = true
            };

            // --- 🟢 NEW LOG: Verify Session Properties Being Sent 🟢 ---
            InGameDebug.Log($"[HOST] StartGame Args - Properties to send:");
            foreach (var prop in startGameArgs.SessionProperties)
            {
                InGameDebug.Log($"  > Key: '{prop.Key}', Value: '{prop.Value}'");
            }
            // --- 🟢 END NEW LOG 🟢 ---

            InGameDebug.Log($"[HOST] Calling StartGame. Mode: '{startGameArgs.GameMode}', SessionName: '{startGameArgs.SessionName}'");

            var result = await _runner.StartGame(startGameArgs);
            if (result.Ok)
            {
                InGameDebug.Log($"[HOST] StartGame OK. Loading lobby scene...");
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID);
                PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName);
                PlayerPrefs.Save();
                await LoadScene(_lobbySceneName);
            }
            else
            {
                InGameDebug.Log($"[HOST] StartGame FAILED: {result.ShutdownReason}");
            }
        }
        catch (Exception ex)
        {
            InGameDebug.Log($"[HOST] StartGame EXCEPTION: {ex.Message}");
        }
    }

    public async Task StartClientGameByHash(string roomCode)
    {
        InGameDebug.Log($"--- [CP 2] StartClientGameByHash ---");
        InGameDebug.Log($"Received roomCode: '{roomCode}' (Target Hash)"); // Clearly label target

        try
        {
            if (string.IsNullOrEmpty(roomCode))
            {
                InGameDebug.Log($"[CP 2] Room code is empty. Aborting.");
                return;
            }

            InGameDebug.Log($"[CP 2] Calling RefreshSessionList...");
            await RefreshSessionList(); // Ensure list is fresh using the correct method
            InGameDebug.Log($"[CP 2] RefreshSessionList returned. Available sessions: {_availableSessions.Count}");

            // --- 🟢 NEW LOG: Prepare for Search 🟢 ---
            InGameDebug.Log($"[CP 4 PRE-SEARCH] Searching for Room Code (Hash): '{roomCode}'");
            InGameDebug.Log($"[CP 4 PRE-SEARCH] Available sessions in _availableSessions list ({_availableSessions.Count}):");
            for (int i = 0; i < _availableSessions.Count; i++)
            {
                var session = _availableSessions[i];
                string currentHash = "ERROR: NO HASH PROP";
                if (session.Properties != null && session.Properties.TryGetValue("Hash", out var hashProperty))
                {
                    currentHash = hashProperty.PropertyValue?.ToString() ?? "NULL PROP VALUE";
                }
                else if (session.Properties == null)
                {
                    currentHash = "PROPS NULL";
                }
                InGameDebug.Log($"  > Session [{i}]: Name='{session.Name}', Hash Property Value='{currentHash}'");
            }
            // --- 🟢 END NEW LOG 🟢 ---

            // The actual search - case-insensitive comparison
            SessionInfo targetSession = _availableSessions.FirstOrDefault(session =>
                 session.Properties != null && // Safety check
                 session.Properties.TryGetValue("Hash", out var hashProperty) &&
                 hashProperty.PropertyValue != null && // Safety check
                 hashProperty.PropertyValue.ToString().Equals(roomCode, StringComparison.OrdinalIgnoreCase)); // Case-insensitive compare

            InGameDebug.Log($"--- [CP 4] Session Search Result ---");
            if (targetSession != null)
            {
                InGameDebug.Log($"[CP 4] SUCCESS: Found session with matching hash.");
                InGameDebug.Log($"[CP 4] Session Name (Internal ID): '{targetSession.Name}'");
                InGameDebug.Log($"[CP 4] Calling StartClientGameBySessionInfo...");
                await StartClientGameBySessionInfo(targetSession);
            }
            else
            {
                InGameDebug.Log($"[CP 4] FAILED: No session found with code '{roomCode}'. Check logs above for available hashes.");
                Debug.LogWarning($"StartClientGameByHash: No session found with code {roomCode} after refresh.");
            }
        }
        catch (Exception ex)
        {
            InGameDebug.Log($"[CP 2] StartClientGameByHash EXCEPTION: {ex.Message}\nStackTrace: {ex.StackTrace}"); // Added StackTrace
            Debug.LogError($"StartClientGameByHash: Exception: {ex.Message}");
        }
    }


    public async Task RefreshSessionList()
    {
        InGameDebug.Log($"--- RefreshSessionList ---");
        Debug.Log("RefreshSessionList (Lobby Approach): Starting...");

        if (_runner != null)
        {
            if (!_runner.IsUnityNull())
            {
                if (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown)
                {
                    InGameDebug.Log($"[Refresh] Runner is active. Shutting down...");
                    await _runner.Shutdown();
                    await Task.Delay(200);
                }
                InGameDebug.Log($"[Refresh] Destroying existing runner component.");
                Destroy(_runner);
                await Task.Yield();
                _runner = null;
            }
            else
            {
                InGameDebug.Log($"[Refresh] _runner was already null or destroyed.");
                _runner = null;
            }
        }

        if (_runner == null)
        {
            InGameDebug.Log($"[Refresh] Adding new runner component for discovery.");
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this);
        }
        else
        {
            InGameDebug.Log($"[Refresh] ERROR: _runner was NOT null!");
            return;
        }

        InGameDebug.Log($"[Refresh] Disabling NetworkSceneManager component.");
        if (_sceneManager != null) _sceneManager.enabled = false;

        var args = new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = null,
            SceneManager = null
            // CloudRegion property removed - THIS FIXES THE ERROR
        };

        _sessionListTask = new TaskCompletionSource<bool>();

        try
        {
            InGameDebug.Log($"--- [CP 3] RefreshSessionList: Calling StartGame ---");
            InGameDebug.Log($"[CP 3] Mode: '{args.GameMode}', SessionName: 'NULL', SceneManager: 'NULL'");
            InGameDebug.Log($"[CP 3] This call should now only join the lobby.");

            var result = await _runner.StartGame(args);

            InGameDebug.Log($"[CP 3] Lobby Join StartGame result: Ok={result.Ok}, Reason={result.ShutdownReason}");

            if (!result.Ok)
            {
                InGameDebug.Log($"[CP 3] FAILED to start client to join lobby: {result.ShutdownReason}");
                if (_runner != null && !_runner.IsUnityNull() && !_runner.IsShutdown) await _runner.Shutdown();
                if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable on failure
                _sessionListTask.TrySetResult(false); // Manually fail the task
                return;
            }

            InGameDebug.Log($"[CP 3] Client started for lobby join. Waiting for session list (10s timeout)...");

            var timeoutTask = Task.Delay(30000); // 30 second timeout
            var completedTask = await Task.WhenAny(_sessionListTask.Task, timeoutTask);

            // --- 🟢 NEW LOG: Check Task Status 🟢 ---
            if (_sessionListTask.Task.IsCompletedSuccessfully)
            {
                InGameDebug.Log($"[CP 3 Post-Wait] SessionList Task Completed Successfully.");
            }
            else if (_sessionListTask.Task.IsCanceled)
            {
                InGameDebug.Log($"[CP 3 Post-Wait] SessionList Task Was Canceled (Likely Timeout).");
            }
            else if (_sessionListTask.Task.IsFaulted)
            {
                InGameDebug.Log($"[CP 3 Post-Wait] SessionList Task Faulted: {_sessionListTask.Task.Exception?.Message}");
            }
            else
            {
                InGameDebug.Log($"[CP 3 Post-Wait] SessionList Task Status Unknown (Shouldn't happen).");
            }
            // --- 🟢 END NEW LOG 🟢 ---

            if (completedTask == _sessionListTask.Task && _sessionListTask.Task.Result == true)
            {
                InGameDebug.Log($"[CP 3] OnSessionListUpdated callback received! List populated.");
            }
            else
            {
                InGameDebug.Log($"[CP 3] Timed out waiting for session list update after 10s.");
                _sessionListTask.TrySetCanceled();
            }

            InGameDebug.Log($"[CP 3] Finished waiting.");

        }
        catch (Exception ex)
        {
            InGameDebug.Log($"[CP 3] RefreshSessionList EXCEPTION: {ex.Message}");
            if (_runner != null && !_runner.IsUnityNull() && !_runner.IsShutdown)
            {
                await _runner.Shutdown();
            }
            if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable on exception
            _sessionListTask.TrySetException(ex); // Fail the task
        }
    }

    public async Task ResetNetworkRunner()
    {
        InGameDebug.Log($"ResetNetworkRunner called.");
        Debug.Log("ResetNetworkRunner: Called.");

        if (_runner != null)
        {
            if (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown)
            {
                InGameDebug.Log($"[Reset] Shutting down main runner...");
                await _runner.Shutdown();
                await Task.Delay(200);
            }

            InGameDebug.Log($"[Reset] Destroying main runner component.");
            Destroy(_runner);
            await Task.Yield();
        }

        InGameDebug.Log($"[Reset] Adding new main runner component and callbacks.");
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);

        if (_sceneManager != null)
        {
            InGameDebug.Log($"[Reset] Ensuring SceneManager component is enabled.");
            _sceneManager.enabled = true;
        }

        InGameDebug.Log($"[Reset] Clearing session info.");
        SessionDisplayName = string.Empty;
        SessionUniqueID = string.Empty;
        SessionHash = string.Empty;
        SessionStartTime = 0;
        _availableSessions.Clear();
    }

    public async Task StartClientGameBySessionInfo(SessionInfo sessionInfo)
    {
        InGameDebug.Log($"--- [CP 5] StartClientGameBySessionInfo ---");
        InGameDebug.Log($"Attempting to join session: '{sessionInfo.Name}'");

        try
        {
            if (_runner == null)
            {
                InGameDebug.Log($"[CP 5] Runner is NULL. This is bad. Re-adding component.");
                _runner = gameObject.AddComponent<NetworkRunner>();
                _runner.AddCallbacks(this);
            }
            else
            {
                InGameDebug.Log($"[CP 5] Runner was not null.");
            }

            InGameDebug.Log($"[CP 5] Shutting down lobby-only runner before joining.");
            await _runner.Shutdown();
            await Task.Delay(100);

            _runner.ProvideInput = true; _isJoining = true;

            if (sessionInfo.Properties.TryGetValue("DisplayName", out var displayNameObj)) SessionDisplayName = displayNameObj.PropertyValue.ToString(); else SessionDisplayName = sessionInfo.Name;
            if (sessionInfo.Properties.TryGetValue("Hash", out var hashObj)) SessionHash = hashObj.PropertyValue.ToString(); else SessionHash = ComputeSessionHash(sessionInfo.Name);

            InGameDebug.Log($"[CP 5] Re-enabling NetworkSceneManager component.");
            if (_sceneManager != null) _sceneManager.enabled = true;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionInfo.Name,
                SceneManager = _sceneManager
                // CloudRegion property removed - THIS FIXES THE ERROR
            };

            InGameDebug.Log($"[CP 5] Calling StartGame. Mode: '{startGameArgs.GameMode}', SessionName: '{startGameArgs.SessionName}', SceneManager: 'EXISTS'");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok && _runner.SessionInfo != null)
            {
                InGameDebug.Log($"[CP 5] StartGame OK. SessionUniqueID: '{_runner.SessionInfo.Name}'");
                SessionUniqueID = _runner.SessionInfo.Name;
            }
            else
            {
                InGameDebug.Log($"[CP 5] StartGame FAILED: {result.ShutdownReason}");
            }
            _isJoining = false;
        }
        catch (Exception ex)
        {
            InGameDebug.Log($"[CP 5] StartGame EXCEPTION: {ex.Message}");
            _isJoining = false;
            if (_sceneManager != null) _sceneManager.enabled = true; // Re-enable on exception
        }
    }

    private void SetupSessionCode(string sessionNameBase)
    {
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (scm == null)
        {
            SessionHash = "NOHASH"; // Fallback
            SessionUniqueID = Guid.NewGuid().ToString();
        }
        else
        {
            SessionHash = scm.GenerateNewSessionCode();
            SessionUniqueID = scm.GetInternalId(SessionHash);
        }
        SessionDisplayName = string.IsNullOrEmpty(sessionNameBase) ? $"Session {SessionHash.Substring(0, 4)}" : sessionNameBase;
        SessionStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private string ComputeSessionHash(string input) // Simple hash for fallback
    {
        if (string.IsNullOrEmpty(input)) return "NOHASH";
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        { byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input); byte[] hashBytes = md5.ComputeHash(inputBytes); return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8); }
    }

    public async Task LoadScene(string sceneName)
    {
        if (_runner != null && _runner.IsRunning) { await _runner.LoadScene(sceneName); }
    }

    public async Task ShutdownGame()
    {
        if (_runner != null && _runner.IsRunning) { await _runner.Shutdown(); }
    }

    public void ForceRefreshSessions() // Public method to trigger refresh
    {
        _ = RefreshSessionList(); // Fire-and-forget
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if ((currentScene == _lobbySceneName || currentScene == _gameSceneName) && runner.IsServer)
        {
            Transform spawnPoint = GetSpawnPoint();
            Vector3 spawnPos = spawnPoint ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

            try
            {
                NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPos, spawnRot, player);
                if (networkPlayerObject != null) _spawnedCharacters.Add(player, networkPlayerObject);
            }
            catch (Exception) { /* Handle spawn errors */ }

            if (FindFirstObjectByType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid)
            {
                runner.Spawn(_gameStateManagerPrefab);
            }

            if (_vehiclePrefab != null && _vehiclePrefab.IsValid && FindFirstObjectByType<BasicVehicleController>() == null)
            {
                Vector3 vehicleSpawnPos = spawnPoint ? spawnPoint.position + spawnPoint.forward * 5f + Vector3.up * 0.5f : new
Vector3(0, 0.5f, 5);
                runner.Spawn(_vehiclePrefab, vehicleSpawnPos, Quaternion.identity);
            }
        }
    }

    private Transform GetSpawnPoint()
    {
        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        return spawnPoints.Length > 0 ? spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform : null;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            if (networkObject != null) runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        // --- 🟢 NEW LOG: Confirm Entry 🟢 ---
        InGameDebug.Log($"--- OnSessionListUpdated --- <<< EXECUTING CALLBACK NOW >>>");
        // --- 🟢 END NEW LOG 🟢 ---
        InGameDebug.Log($"--- OnSessionListUpdated ---");
        InGameDebug.Log($"Received {sessionList.Count} raw sessions from Photon.");

        // --- 🟢 NEW LOG: Dump Raw Session Data 🟢 ---
        for (int i = 0; i < sessionList.Count; i++)
        {
            var session = sessionList[i];
            InGameDebug.Log($"  Raw Session [{i}]: Name='{session.Name}', IsVisible={session.IsVisible}, IsOpen={session.IsOpen}, PlayerCount={session.PlayerCount}");
            if (session.Properties != null)
            {
                foreach (var prop in session.Properties)
                {
                    InGameDebug.Log($"    > Prop Key='{prop.Key}', Value='{prop.Value}'");
                }
            }
            else
            {
                InGameDebug.Log($"    > Properties collection is null!");
            }
        }
        // --- 🟢 END NEW LOG 🟢 ---


        List<SessionInfo> updatedList = new List<SessionInfo>(sessionList);
        // Add self-hosted session if running as host and it's not in the list from the cloud (Shouldn't be needed anymore with visible sessions)
        // if (_runner != null && _runner.IsRunning && _runner.IsServer && _runner.SessionInfo != null)
        // {
        //     if (!sessionList.Any(session => session.Name == _runner.SessionInfo.Name))
        //     {
        //         updatedList.Add(_runner.SessionInfo);
        //     }
        // }

        _availableSessions = updatedList.Where(s => !s.Name.StartsWith("BROWSER_") && !s.Name.StartsWith("TempDiscoverySession_")).ToList();

        InGameDebug.Log($"Filtered list to {_availableSessions.Count} sessions.");
        foreach (var session in _availableSessions)
        {
            string hash = "NO HASH";
            if (session.Properties != null && session.Properties.TryGetValue("Hash", out var hashProp))
            {
                hash = hashProp.PropertyValue?.ToString() ?? "NULL PROP VALUE";
            }
            else if (session.Properties == null)
            {
                hash = "PROPS NULL";
            }
            InGameDebug.Log($"  > Filtered Session: Name='{session.Name}', Hash='{hash}'");
        }

        if (_sessionListTask != null && !_sessionListTask.Task.IsCompleted)
        {
            InGameDebug.Log($"[OnSessionListUpdated] Setting session list task to complete (true).");
            _sessionListTask.TrySetResult(true);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        InGameDebug.Log($"--- OnShutdown ---");
        InGameDebug.Log($"Reason: {shutdownReason}");

        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (!string.IsNullOrEmpty(SessionHash) && scm != null) scm.EndSession(SessionHash);
        _spawnedCharacters.Clear();

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }

        if (_sceneManager != null)
        {
            InGameDebug.Log($"[OnShutdown] Ensuring SceneManager component is re-enabled.");
            _sceneManager.enabled = true;
        }
    }

    public void RecoverNetworkState()
    {
        if (_runner != null && _runner.IsShutdown) { Destroy(_runner); _runner = gameObject.AddComponent<NetworkRunner>(); }
        if (_runner == null) { _runner = gameObject.AddComponent<NetworkRunner>(); }
    }


    public static NetworkRunnerHandler GetInstance()
    {
        NetworkRunnerHandler instance = FindFirstObjectByType<NetworkRunnerHandler>();
        if (instance == null)
        {
            GameObject go = new GameObject("NetworkRunnerHandler");
            instance = go.AddComponent<NetworkRunnerHandler>();
            if (instance.GetComponent<NetworkRunner>() == null) instance.AddComponent<NetworkRunner>();
            if (instance.GetComponent<NetworkSceneManagerDefault>() == null) instance.AddComponent<NetworkSceneManagerDefault>();
            DontDestroyOnLoad(go);
        }
        return instance;
    }


    // --- Empty Callbacks (Required by Interface) ---
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    // --- SYNTAX ERROR FIX ---
    // The previous partial script had an error here. This is the correct, empty callback.
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    // --- END FIX ---

    // --- NEW LOG: This will confirm our region ---
    // --- CORRECTED LOG: This will confirm our region ---
    public void OnConnectedToServer(NetworkRunner runner)
    {
        InGameDebug.Log($"--- OnConnectedToServer ---");
        // We get the region from the SessionInfo AFTER connecting
        if (runner.SessionInfo != null)
        {
            InGameDebug.Log($"Successfully connected to region: '{runner.SessionInfo.Region}'");
        }
        else
        {
            InGameDebug.Log($"Connected to server, but SessionInfo is unexpectedly null.");
        }
    }
    // --- END CORRECTION ---

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
}