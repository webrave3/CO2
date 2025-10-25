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

    // Session information
    public string SessionDisplayName { get; private set; }
    public string SessionUniqueID { get; private set; }
    public string SessionHash { get; private set; }
    public long SessionStartTime { get; private set; }

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    // Discovery-specific fields
    
    private DateTime _lastRefreshTime = DateTime.MinValue;

    // Store available sessions locally
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;
    

    public List<SessionInfo> GetAvailableSessions() => new List<SessionInfo>(_availableSessions);

    private void Awake()
    {
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

        // --- Ensure Callbacks are added ---
        _runner.AddCallbacks(this);
        // --- End Ensure Callbacks ---

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
                IsVisible = true, // Make session visible in browser
                IsOpen = true
            };
            var result = await _runner.StartGame(startGameArgs);
            if (result.Ok)
            {
                PlayerPrefs.SetString("LastSessionID", SessionUniqueID);
                PlayerPrefs.SetString("LastSessionHash", SessionHash);
                PlayerPrefs.SetString("LastSessionName", SessionDisplayName);
                PlayerPrefs.Save();
                await LoadScene(_lobbySceneName);
            }
        }
        catch (Exception ex) { /* Handle exceptions appropriately */ }
    }

    public async Task StartClientGameByHash(string roomCode)
    {
        try
        {
            if (string.IsNullOrEmpty(roomCode)) return;

            // --- MODIFICATION ---
            await RefreshSessionList(); // Ensure list is fresh using the correct method
                                        // --- END MODIFICATION ---

            SessionInfo targetSession = _availableSessions.FirstOrDefault(session =>
                 session.Properties.TryGetValue("Hash", out var hashProperty) &&
                 hashProperty.PropertyValue.ToString().Equals(roomCode, StringComparison.OrdinalIgnoreCase));

            if (targetSession != null)
            {
                await StartClientGameBySessionInfo(targetSession);
            }
            else
            {
                Debug.LogWarning($"StartClientGameByHash: No session found with code {roomCode} after refresh.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"StartClientGameByHash: Exception: {ex.Message}");
        }
    }

    public async Task RefreshSessionList()
    {
        Debug.Log("RefreshSessionList (Lobby Approach): Starting...");

        // --- START NEW LOBBY APPROACH ---
        // Ensure runner component exists, but if it does, it MUST be shut down and destroyed
        if (_runner != null)
        {
            if(!_runner.IsUnityNull())
            {
                if (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown)
                {
                    Debug.Log($"RefreshSessionList: Runner is active. Shutting down before destroying...");
                    await _runner.Shutdown();
                    await Task.Delay(200); // Give time for shutdown
                }

                Debug.Log("RefreshSessionList: Destroying existing runner component.");
                Destroy(_runner);
                await Task.Yield(); // Wait one frame for Destroy
                _runner = null; // Explicitly set to null
            }
            else
            {
                 Debug.Log("RefreshSessionList: _runner was already null or destroyed.");
                 _runner = null; // Ensure it's null
            }
        }

        // Add a new runner component and callbacks
        if (_runner == null)
        {
            Debug.Log("RefreshSessionList: Adding new runner component for discovery.");
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this); // Add callbacks immediately
             Debug.Log("RefreshSessionList: Callbacks added.");
        }
        else
        {
             Debug.LogError("RefreshSessionList: _runner was NOT null before AddComponent! This shouldn't happen.");
             return; // Avoid potential issues
        }

        // Try connecting to the Name Server and joining the default lobby
        Debug.Log("RefreshSessionList: Starting game in Client mode to join lobby...");
        var args = new StartGameArgs()
        {
            GameMode = GameMode.Client,
            // SessionName = null, // Let Fusion handle temporary connection details
            // LobbyName = "", // Explicitly targeting the default lobby
            SceneManager = _sceneManager // Still might be needed by Fusion internally
        };

        try
        {
            Debug.Log($"RefreshSessionList: Attempting StartGame with mode: {args.GameMode} (to join lobby)");
            var result = await _runner.StartGame(args);

             Debug.Log($"RefreshSessionList: Lobby Join StartGame result: Ok={result.Ok}, Reason={result.ShutdownReason}");

            if (!result.Ok)
            {
                Debug.LogError($"RefreshSessionList: Failed to start client to join lobby: {result.ShutdownReason}");
                 if (_runner != null && !_runner.IsUnityNull() && !_runner.IsShutdown) await _runner.Shutdown();
                return;
            }

            Debug.Log("RefreshSessionList: Client started successfully for lobby join. Waiting for session list update...");

            // Instead of fixed delay, wait for the callback or a timeout
            float startTime = Time.time;
            bool listUpdated = false;
            while (Time.time < startTime + 5.0f) // 5 second timeout
            {
                 // Check if the callback updated the list (maybe add a flag in OnSessionListUpdated)
                 // For now, just rely on the delay, but ideally, you'd check a flag.
                 await Task.Delay(100); // Check every 100ms
                 if (_availableSessions.Count > 0) { // Simple check if list got populated
                      listUpdated = true;
                      Debug.Log("RefreshSessionList: Detected session list update.");
                      break;
                 }
            }

            if (!listUpdated) {
                 Debug.LogWarning("RefreshSessionList: Timed out waiting for session list update after joining lobby.");
            }

            Debug.Log("RefreshSessionList: Finished waiting/timeout.");

            
        }
        catch (Exception ex)
        {
            Debug.LogError($"RefreshSessionList: Exception during lobby join StartGame/Wait: {ex.Message} \nStack Trace: {ex.StackTrace}");
            if (_runner != null && !_runner.IsUnityNull() && !_runner.IsShutdown)
            {
                await _runner.Shutdown(); // Ensure cleanup on exception
            }
        }
        // --- END NEW LOBBY APPROACH ---
    }



    public async Task ResetNetworkRunner()
    {
        Debug.Log("ResetNetworkRunner: Called.");

        // Shut down and destroy the old runner if it exists
        if (_runner != null)
        {
            if (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown)
            {
                Debug.Log($"ResetNetworkRunner: Shutting down main runner...");
                await _runner.Shutdown();
                await Task.Delay(200); // Give shutdown time
            }

            Debug.Log("ResetNetworkRunner: Destroying main runner component.");
            Destroy(_runner);
            await Task.Yield(); // Wait a frame for Destroy
        }

        // Add a new runner component and re-add callbacks
        Debug.Log("ResetNetworkRunner: Adding new main runner component and adding callbacks.");
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);

        Debug.Log("ResetNetworkRunner: Clearing session info and available sessions.");
        SessionDisplayName = string.Empty;
        SessionUniqueID = string.Empty;
        SessionHash = string.Empty;
        SessionStartTime = 0;
        _availableSessions.Clear();
    }

    public async Task StartClientGameBySessionInfo(SessionInfo sessionInfo) // sessionInfo here is Photon's SessionInfo
    {
        Debug.Log($"StartClientGameBySessionInfo: Preparing to join session with Name (UniqueID): {sessionInfo.Name}");
        try
        {
            // --- START MODIFICATION: Explicitly Reset Runner ---
            // Shut down the runner used for lobby discovery first.
            if (_runner != null && !_runner.IsUnityNull() && (_runner.IsRunning || _runner.IsCloudReady || !_runner.IsShutdown))
            {
                Debug.Log("StartClientGameBySessionInfo: Shutting down existing (lobby) runner before joining specific session...");
                await _runner.Shutdown();
                await Task.Delay(200); // Allow time for shutdown
            }

            // Destroy the old component
            if (_runner != null && !_runner.IsUnityNull())
            {
                Debug.Log("StartClientGameBySessionInfo: Destroying old runner component.");
                Destroy(_runner);
                await Task.Yield(); // Wait a frame
                _runner = null;
            }

            // Add a fresh runner instance for the specific join attempt
            Debug.Log("StartClientGameBySessionInfo: Adding new runner component for specific session join.");
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this); // Re-add callbacks
                                        // --- END MODIFICATION ---


            _runner.ProvideInput = true; // Set input provision on the new runner
            _isJoining = true;

            // Log what we are trying to join
            Debug.Log($"StartClientGameBySessionInfo: Attempting StartGame to join session: {sessionInfo.Name}");

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionInfo.Name, // Use the Unique ID received from Photon
                SceneManager = _sceneManager
            };

            var result = await _runner.StartGame(startGameArgs);

            Debug.Log($"StartClientGameBySessionInfo: Join result: Ok={result.Ok}, Reason={result.ShutdownReason}");

            if (result.Ok && _runner.SessionInfo != null)
            {
                SessionUniqueID = _runner.SessionInfo.Name;
                // It's generally better practice to rely on properties received, but let's keep your original logic for now
                if (sessionInfo.Properties.TryGetValue("DisplayName", out var displayNameObj)) SessionDisplayName = displayNameObj.PropertyValue.ToString(); else SessionDisplayName = sessionInfo.Name;
                if (sessionInfo.Properties.TryGetValue("Hash", out var hashObj)) SessionHash = hashObj.PropertyValue.ToString(); else SessionHash = ComputeSessionHash(sessionInfo.Name);
                Debug.Log($"StartClientGameBySessionInfo: Successfully started/joined session: {SessionUniqueID}");
                // Scene change should be handled by Fusion's SceneManager upon successful connection
            }
            else
            {
                Debug.LogError($"StartClientGameBySessionInfo: Failed to join session {sessionInfo.Name}. Reason: {result.ShutdownReason}");
                // Consider resetting runner state here if needed upon failure
            }
            _isJoining = false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"StartClientGameBySessionInfo: Exception: {ex.Message} \nStack Trace: {ex.StackTrace}");
            _isJoining = false;
            // Ensure runner is cleaned up on exception too
            if (_runner != null && !_runner.IsUnityNull() && !_runner.IsShutdown)
            {
                await _runner.Shutdown();
                // Optionally destroy and nullify _runner here too
            }
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
        // We don't need the runner check, RefreshSessionList will handle it
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

            // Spawn GameStateManager if it doesn't exist
            if (FindFirstObjectByType<GameStateManager>() == null && _gameStateManagerPrefab != null && _gameStateManagerPrefab.IsValid)
            {
                runner.Spawn(_gameStateManagerPrefab);
            }

            // Spawn Vehicle if needed
            if (_vehiclePrefab != null && _vehiclePrefab.IsValid && FindFirstObjectByType<BasicVehicleController>() == null)
            {
                Vector3 vehicleSpawnPos = spawnPoint ? spawnPoint.position + spawnPoint.forward * 5f + Vector3.up * 0.5f : new Vector3(0, 0.5f, 5);
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
        List<SessionInfo> updatedList = new List<SessionInfo>(sessionList);
        // Add self-hosted session if running as host and it's not in the list from the cloud
        if (_runner != null && _runner.IsRunning && _runner.IsServer && _runner.SessionInfo != null)
        {
            if (!sessionList.Any(session => session.Name == _runner.SessionInfo.Name))
            {
                updatedList.Add(_runner.SessionInfo);
            }
        }
        _availableSessions = updatedList.Where(s => !s.Name.StartsWith("BROWSER_") && !s.Name.StartsWith("TempDiscoverySession_")).ToList(); // Filter out temp/browser sessions
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SessionCodeManager scm = FindFirstObjectByType<SessionCodeManager>();
        if (!string.IsNullOrEmpty(SessionHash) && scm != null) scm.EndSession(SessionHash);
        _spawnedCharacters.Clear();
        // Go back to main menu unless already there
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
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
            // Ensure necessary components are added
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
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
}