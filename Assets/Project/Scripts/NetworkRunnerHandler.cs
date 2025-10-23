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
    [SerializeField] private bool _enableDiscovery = true;

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

    // Store available sessions locally
    private List<SessionInfo> _availableSessions = new List<SessionInfo>();
    private bool _isJoining = false;

    public bool IsSessionActive => _runner != null && _runner.IsRunning;
    public NetworkRunner Runner => _runner;
    public bool IsDiscoveryRunning => _isDiscoveryRunning && _discoveryRunner != null && _discoveryRunner.IsRunning;

    public List<SessionInfo> GetAvailableSessions() => new List<SessionInfo>(_availableSessions);

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();
        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null) _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
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
            await RefreshSessionList(); // Ensure list is fresh
            SessionInfo targetSession = _availableSessions.FirstOrDefault(session =>
                 session.Properties.TryGetValue("Hash", out var hashProperty) &&
                 hashProperty.PropertyValue.ToString().Equals(roomCode, StringComparison.OrdinalIgnoreCase));

            if (targetSession != null)
            {
                await StartClientGameBySessionInfo(targetSession);
            }
        }
        catch (Exception ex) { /* Handle exceptions */ }
    }

    private async Task RefreshSessionList()
    {
        // Simplified: relies on OnSessionListUpdated being called by the runner
        // Start a temporary client if not already connected, purely for discovery
        if (_runner == null || !_runner.IsRunning || !_runner.IsCloudReady)
        {
            var tempArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = "TempDiscoverySession_" + Guid.NewGuid(), // Must be unique
                SceneManager = _sceneManager,
                IsVisible = false, // Don't show this temp session
                PlayerCount = 0 // Don't allow players
            };
            try
            {
                if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();
                var result = await _runner.StartGame(tempArgs);
                if (!result.Ok) return; // Failed to start temp session
                await Task.Delay(500); // Allow connection time
            }
            catch (Exception ex) { return; }
        }
        // Wait for Fusion to update the list via OnSessionListUpdated
        await Task.Delay(1500);
    }

    public async Task ForceActiveSessionRefresh()
    {
        try
        {
            if (_discoveryRunner == null) { _discoveryRunner = gameObject.AddComponent<NetworkRunner>(); }
            if ((DateTime.Now - _lastRefreshTime).TotalSeconds < 2) { return; } // Throttle refresh
            _lastRefreshTime = DateTime.Now;

            if (_isDiscoveryRunning && _discoveryRunner != null && !_discoveryRunner.IsShutdown)
            {
                await _discoveryRunner.Shutdown();
                await Task.Delay(200);
            }

            string sessionBrowserId = "BROWSER_" + DateTime.Now.Ticks;
            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = sessionBrowserId,
                SceneManager = _sceneManager,
                IsVisible = false // This runner is just for browsing
            };

            var result = await _discoveryRunner.StartGame(startGameArgs);
            if (!result.Ok)
            {
                _isDiscoveryRunning = false; return;
            }
            _isDiscoveryRunning = true;
            await Task.Delay(2000); // Wait for session list updates
        }
        catch (Exception ex) { _isDiscoveryRunning = false; }
    }

    public async Task ResetNetworkRunner()
    {
        if (Runner != null && Runner.IsRunning)
        {
            try { await Runner.Shutdown(); await Task.Delay(300); } catch (Exception) { /* Ignored */ }
        }
        SessionDisplayName = string.Empty; SessionUniqueID = string.Empty; SessionHash = string.Empty; SessionStartTime = 0;
        _availableSessions.Clear();
    }

    public async Task StartClientGameBySessionInfo(SessionInfo sessionInfo)
    {
        try
        {
            if (_runner == null) return;
            _runner.ProvideInput = true; _isJoining = true;

            if (sessionInfo.Properties.TryGetValue("DisplayName", out var displayNameObj)) SessionDisplayName = displayNameObj.PropertyValue.ToString(); else SessionDisplayName = sessionInfo.Name;
            if (sessionInfo.Properties.TryGetValue("Hash", out var hashObj)) SessionHash = hashObj.PropertyValue.ToString(); else SessionHash = ComputeSessionHash(sessionInfo.Name);

            var startGameArgs = new StartGameArgs() { GameMode = GameMode.Client, SessionName = sessionInfo.Name, SceneManager = _sceneManager };
            var result = await _runner.StartGame(startGameArgs);
            if (result.Ok && _runner.SessionInfo != null)
            {
                SessionUniqueID = _runner.SessionInfo.Name;
            }
            _isJoining = false;
        }
        catch (Exception ex) { _isJoining = false; }
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
        if (_runner == null || !_runner.IsRunning) return;
        _ = ForceActiveSessionRefresh(); // Fire-and-forget
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
        if (_discoveryRunner != null && _discoveryRunner.IsShutdown) { Destroy(_discoveryRunner); _discoveryRunner = null; _isDiscoveryRunning = false; }
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