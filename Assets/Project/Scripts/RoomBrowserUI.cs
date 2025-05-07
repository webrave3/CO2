using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using System;
using System.Threading.Tasks;

public class RoomBrowserUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _joinGamePanel;
    [SerializeField] private Transform _roomListContent;
    [SerializeField] private GameObject _roomEntryPrefab;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private Button _backButton;

    [Header("Direct Join")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _directJoinButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private float _statusMessageDuration = 3f;
    [SerializeField] private GameObject _joiningIndicator;

    [Header("Settings")]
    [SerializeField] private bool _showPlaceholderWhenEmpty = true;

    [Header("Debug")]
    [SerializeField] private Button _debugButton;
    [SerializeField] private TextMeshProUGUI _debugStatusText;
    [SerializeField] private bool _showDebugInfo = false;

    private NetworkRunnerHandler _networkRunnerHandler;
    private List<GameObject> _roomEntries = new List<GameObject>();
    private bool _isJoining = false;
    private Coroutine _statusMessageCoroutine;

    void Start()
    {
        // Get reference to NetworkRunnerHandler
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("NetworkRunnerHandler not found! Room browser will not function.");
        }

        // Initialize UI elements
        if (_statusText != null)
            _statusText.gameObject.SetActive(false);

        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(false);

        if (_debugStatusText != null)
            _debugStatusText.gameObject.SetActive(_showDebugInfo);

        // Set up button listeners
        if (_refreshButton != null)
            _refreshButton.onClick.AddListener(RefreshRoomList);

        if (_backButton != null)
            _backButton.onClick.AddListener(() => {
                if (_joinGamePanel != null)
                    _joinGamePanel.SetActive(false);

                // Find MainMenuUI and show main panel
                MainMenuUI mainMenu = FindObjectOfType<MainMenuUI>();
                if (mainMenu != null)
                {
                    mainMenu.ShowMainPanel();
                }
            });

        if (_directJoinButton != null)
            _directJoinButton.onClick.AddListener(OnDirectJoinClicked);

        // Set up debug button if present
        if (_debugButton != null)
        {
            _debugButton.onClick.AddListener(DebugRoomDiscovery);
        }
    }

    // This is the method to call from MainMenuUI
    public void ShowRoomBrowser()
    {
        Debug.Log("[UI] ShowRoomBrowser called");

        if (_joinGamePanel == null)
        {
            Debug.LogError("[UI] Join Game Panel reference is missing!");
            return;
        }

        // Step 1: Show the panel first
        _joinGamePanel.SetActive(true);

        // Step 2: Reset UI state
        if (_statusText != null)
        {
            _statusText.text = "Loading room list...";
            _statusText.color = Color.white;
            _statusText.gameObject.SetActive(true);
        }

        if (_roomCodeInput != null)
        {
            _roomCodeInput.text = "";
        }

        // Step 3: Make sure room list content is visible
        if (_roomListContent != null)
        {
            _roomListContent.gameObject.SetActive(true);
            // Ensure it's visible in hierarchy
            Transform parent = _roomListContent.parent;
            while (parent != null)
            {
                parent.gameObject.SetActive(true);
                parent = parent.parent;
            }
        }
        else
        {
            Debug.LogError("[UI] Room list content reference is missing!");
        }

        // Step 4: Clear existing entries before refreshing
        ClearRoomEntries();

        // Add placeholder rooms first for better user experience
        if (_showPlaceholderWhenEmpty)
        {
            AddPlaceholderRooms();
        }

        // Step 5: Start refreshing rooms
        if (_networkRunnerHandler != null)
        {
            RefreshRoomList();
            Debug.Log("[UI] Started room list refresh");
        }
        else
        {
            Debug.LogError("[UI] NetworkRunnerHandler is null!");
            ShowStatusMessage("Network system not available", Color.red);
        }
    }

    private async void OnDirectJoinClicked()
    {
        // Prevent multiple join attempts
        if (_isJoining)
            return;

        if (_networkRunnerHandler == null)
        {
            ShowStatusMessage("Network system not initialized", Color.red);
            return;
        }

        string roomCode = _roomCodeInput.text.Trim();
        if (string.IsNullOrEmpty(roomCode))
        {
            ShowStatusMessage("Please enter a room code", Color.red);
            return;
        }

        // Start joining process
        _isJoining = true;

        // Show joining indicator
        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(true);

        // Disable join button during attempt
        if (_directJoinButton != null)
            _directJoinButton.interactable = false;

        ShowStatusMessage($"Joining room: {roomCode}...", Color.white);

        try
        {
            // Try to join with the room code
            await _networkRunnerHandler.StartClientGameByHash(roomCode);

            // If we get here and we're still not connected after a second,
            // show failure message
            StartCoroutine(CheckJoinResult());
        }
        catch (Exception ex)
        {
            // Show error and reset UI
            ShowStatusMessage($"Error joining: {ex.Message}", Color.red);
            ResetJoiningState();
        }
    }

    private IEnumerator CheckJoinResult()
    {
        yield return new WaitForSeconds(1.5f);

        // If we're still on this screen, join likely failed
        if (_networkRunnerHandler.Runner == null ||
            !_networkRunnerHandler.Runner.IsRunning ||
            string.IsNullOrEmpty(_networkRunnerHandler.SessionUniqueID))
        {
            ShowStatusMessage("Failed to find game with that code", Color.red);
            ResetJoiningState();
        }
    }

    private void ResetJoiningState()
    {
        _isJoining = false;

        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(false);

        if (_directJoinButton != null)
            _directJoinButton.interactable = true;
    }

    public async void RefreshRoomList()
    {
        Debug.Log("[UI] RefreshRoomList called");

        // CRITICAL FIX: Try different ways to find NetworkRunnerHandler if null
        if (_networkRunnerHandler == null)
        {
            // First try regular FindObjectOfType
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

            // If still null, try the BootstrapManager
            if (_networkRunnerHandler == null && BootstrapManager.Instance != null)
            {
                _networkRunnerHandler = BootstrapManager.Instance.GetNetworkRunnerHandler();
                Debug.Log("[UI] Found NetworkRunnerHandler via BootstrapManager");
            }

            // Last resort: get the static instance
            if (_networkRunnerHandler == null)
            {
                _networkRunnerHandler = NetworkRunnerHandler.GetInstance();
                Debug.Log("[UI] Created new NetworkRunnerHandler via GetInstance");
            }

            if (_networkRunnerHandler == null)
            {
                ShowStatusMessage("Network system not initialized", Color.red);
                Debug.LogError("[UI] All attempts to find NetworkRunnerHandler failed!");
                return;
            }
        }

        try
        {
            // Show refresh indicator
            ShowStatusMessage("Refreshing room list...", Color.white);

            // Clear existing entries
            ClearRoomEntries();

            // Make sure list is visible
            if (_roomListContent != null && !_roomListContent.gameObject.activeSelf)
            {
                _roomListContent.gameObject.SetActive(true);
                Debug.Log("[UI] Activated room list content");
            }

            // Add placeholder rooms for user experience while refreshing
            if (_showPlaceholderWhenEmpty)
            {
                AddPlaceholderRooms();
            }

            // Force network refresh
            await _networkRunnerHandler.ForceActiveSessionRefresh();

            // Delay slightly to ensure data is ready
            await Task.Delay(500);

            // Update the UI with received sessions
            PopulateRoomList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UI] Error during room refresh: {ex.Message}\n{ex.StackTrace}");
            ShowStatusMessage($"Error: {ex.Message}", Color.red);

            // Try to recover
            _networkRunnerHandler?.RecoverNetworkState();
        }
    }

    // Add this helper method
    private void ClearRoomEntries()
    {
        foreach (var entry in _roomEntries)
        {
            Destroy(entry);
        }
        _roomEntries.Clear();
        Debug.Log("Cleared existing room entries");
    }

    // Add this helper method to separate UI updates from data fetching
    private void PopulateRoomList()
    {
        // Clear previous entries first
        ClearRoomEntries();

        // Get available sessions from the NetworkRunnerHandler
        List<SessionInfo> availableSessions = _networkRunnerHandler.GetAvailableSessions();

        Debug.Log($"[UI] PopulateRoomList with {availableSessions.Count} sessions");

        bool anyRealRoomsShown = false;

        // Add real sessions if they exist
        if (availableSessions.Count > 0)
        {
            foreach (var session in availableSessions)
            {
                // Skip session if it's our own temporary browser session
                if (session.Name.StartsWith("BROWSER_"))
                    continue;

                GameObject entryGO = Instantiate(_roomEntryPrefab, _roomListContent);
                _roomEntries.Add(entryGO);
                anyRealRoomsShown = true;

                // Setup entry UI
                TextMeshProUGUI roomNameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
                if (roomNameText != null)
                {
                    string sessionHash = "Unknown";
                    string displayName = session.Name;
                    string regionText = "Unknown";

                    if (session.Properties.TryGetValue("Hash", out var hashObj))
                        sessionHash = hashObj.PropertyValue.ToString();

                    if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                        displayName = nameObj.PropertyValue.ToString();

                    if (session.Properties.TryGetValue("Region", out var regionObj))
                        regionText = regionObj.PropertyValue.ToString();

                    roomNameText.text = $"{displayName}\nPlayers: {session.PlayerCount}/{session.MaxPlayers}\nRegion: {regionText}\nCode: {sessionHash}";
                }

                // Configure join button
                Button joinButton = entryGO.GetComponentInChildren<Button>();
                if (joinButton != null)
                {
                    SessionInfo sessionToJoin = session;
                    joinButton.onClick.AddListener(() => {
                        JoinSession(sessionToJoin);
                    });
                }
            }

            // Show success message if we found rooms
            if (anyRealRoomsShown)
            {
                ShowStatusMessage($"Found {availableSessions.Count} games", Color.green, 2f);
            }
        }

        // If no real rooms were shown, conditionally show placeholders
        if (!anyRealRoomsShown)
        {
            if (_showPlaceholderWhenEmpty)
            {
                // We already cleared entries so need to add again
                AddPlaceholderRooms();
                ShowStatusMessage("No active games found, showing examples", Color.yellow);
            }
            else
            {
                ShowStatusMessage("No active games found", Color.yellow);
            }
        }
    }

    // Debug method for room discovery
    public void DebugRoomDiscovery()
    {
        if (_networkRunnerHandler == null)
        {
            ShowStatusMessage("Network system not initialized", Color.red);
            return;
        }

        Debug.Log("Triggering room discovery debug from browser");
        _networkRunnerHandler.DebugRoomDiscovery();

        // Attempt repair as well
        StartCoroutine(RepairRoomDiscovery());

        // Show debug status
        if (_debugStatusText != null)
        {
            _debugStatusText.text = "Debug info printed to console.\nAttempting repair...";
            _debugStatusText.gameObject.SetActive(true);
        }
    }

    private IEnumerator RepairRoomDiscovery()
    {
        if (_networkRunnerHandler == null)
            yield break;

        yield return new WaitForSeconds(1.0f);

        // Update debug text
        if (_debugStatusText != null)
        {
            _debugStatusText.text = "Refreshing rooms...";
        }

        // Refresh after delay
        yield return new WaitForSeconds(2.0f);

        // Try refresh but don't yield inside try/catch
        try
        {
            RefreshRoomList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during room refresh: {ex.Message}");
            if (_debugStatusText != null)
            {
                _debugStatusText.text = $"Refresh failed: {ex.Message}";
            }
            yield break; // Exit if error occurs
        }

        // Final update - done outside try/catch
        yield return new WaitForSeconds(1.0f);

        if (_debugStatusText != null)
        {
            _debugStatusText.text = "Room refresh complete.\nCheck logs for details.";
        }

        // Auto-hide after delay
        yield return new WaitForSeconds(3.0f);

        if (_debugStatusText != null)
        {
            _debugStatusText.gameObject.SetActive(_showDebugInfo);
        }
    }

    private IEnumerator RefreshAfterDelay(float delay)
    {
        // Wait for network to update
        yield return new WaitForSeconds(delay);

        // Clear existing entries
        foreach (var entry in _roomEntries)
        {
            Destroy(entry);
        }
        _roomEntries.Clear();

        // Get available sessions from the NetworkRunnerHandler
        List<SessionInfo> availableSessions = _networkRunnerHandler.GetAvailableSessions();

        Debug.Log($"Room browser found {availableSessions.Count} sessions");

        // Show placeholder rooms if needed
        if (_showPlaceholderWhenEmpty || availableSessions.Count == 0)
        {
            AddPlaceholderRooms();
        }

        // Add real sessions if they exist
        if (availableSessions.Count > 0)
        {
            ShowStatusMessage($"Found {availableSessions.Count} games", Color.green, 2f);

            // Create entries for each available session
            foreach (var session in availableSessions)
            {
                GameObject entryGO = Instantiate(_roomEntryPrefab, _roomListContent);
                _roomEntries.Add(entryGO);

                // Set session information
                TextMeshProUGUI roomNameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
                if (roomNameText != null)
                {
                    string sessionHash = "Unknown";
                    string displayName = session.Name;
                    string regionText = "Unknown";

                    // Try to get hash and display name from properties
                    if (session.Properties.TryGetValue("Hash", out var hashObj))
                        sessionHash = hashObj.PropertyValue.ToString();

                    if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                        displayName = nameObj.PropertyValue.ToString();

                    if (session.Properties.TryGetValue("Region", out var regionObj))
                        regionText = regionObj.PropertyValue.ToString();

                    roomNameText.text = $"{displayName}\nPlayers: {session.PlayerCount}/{session.MaxPlayers}\nRegion: {regionText}\nCode: {sessionHash}";
                }

                // Configure join button
                Button joinButton = entryGO.GetComponentInChildren<Button>();
                if (joinButton != null)
                {
                    // Store session in a local variable to avoid closure issues
                    SessionInfo sessionToJoin = session;
                    joinButton.onClick.AddListener(() => {
                        JoinSession(sessionToJoin);
                    });
                }
            }
        }
        else if (availableSessions.Count == 0)
        {
            // Only show "no sessions" message if we're not showing placeholders
            if (!_showPlaceholderWhenEmpty)
            {
                ShowStatusMessage("No active games found", Color.yellow);
            }
        }
    }

    private async void JoinSession(SessionInfo sessionInfo)
    {
        if (_networkRunnerHandler == null)
            return;

        // Show joining indicator
        _isJoining = true;
        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(true);

        // Get the session hash if available
        string sessionHash = "Unknown";
        if (sessionInfo.Properties.TryGetValue("Hash", out var hashObj))
            sessionHash = hashObj.PropertyValue.ToString();

        ShowStatusMessage($"Joining {sessionHash}...", Color.white);

        try
        {
            await _networkRunnerHandler.StartClientGameBySessionInfo(sessionInfo);

            // Hide the panel - successful join will transition to the game scene
            if (_joinGamePanel != null)
            {
                _joinGamePanel.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Failed to join: {ex.Message}", Color.red);
            ResetJoiningState();
        }
    }

    // Modified to add 5 placeholder rooms
    private void AddPlaceholderRooms()
    {
        // Create 5 placeholder entries
        AddPlaceholderRoomEntry("Corporate Override", "3/6", "RedFox293", "(Example Room)");
        AddPlaceholderRoomEntry("Research Lab", "2/6", "BlueBird478", "(Example Room)");
        AddPlaceholderRoomEntry("Maintenance Level", "4/6", "GreenWolf102", "(Example Room)");
        AddPlaceholderRoomEntry("Security Division", "1/6", "GoldStar845", "(Example Room)");
        AddPlaceholderRoomEntry("Core Access", "5/6", "SilverMoon367", "(Example Room)");
    }

    private void AddPlaceholderRoomEntry(string name, string players, string code, string suffix)
    {
        GameObject entryGO = Instantiate(_roomEntryPrefab, _roomListContent);
        _roomEntries.Add(entryGO);

        // Set room information
        TextMeshProUGUI roomNameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
        if (roomNameText != null)
        {
            roomNameText.text = $"{name} {suffix}\nPlayers: {players}\nRegion: Example\nCode: {code}";
        }

        // Configure join button - display but disable since this is a placeholder
        Button joinButton = entryGO.GetComponentInChildren<Button>();
        if (joinButton != null)
        {
            joinButton.interactable = false;

            // Add a tooltip or explanation text if your button has one
            TextMeshProUGUI buttonText = joinButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Demo";
            }
        }
    }

    private void ShowStatusMessage(string message, Color color, float duration = 0)
    {
        if (_statusText == null)
            return;

        // Cancel any existing coroutine
        if (_statusMessageCoroutine != null)
            StopCoroutine(_statusMessageCoroutine);

        // Show the message
        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);

        // If duration is specified, hide after that time
        if (duration > 0 || _statusMessageDuration > 0)
        {
            float actualDuration = duration > 0 ? duration : _statusMessageDuration;
            _statusMessageCoroutine = StartCoroutine(HideStatusAfterDelay(actualDuration));
        }
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_statusText != null)
            _statusText.gameObject.SetActive(false);

        _statusMessageCoroutine = null;
    }

    private void OnDestroy()
    {
        // Clean up coroutines
        if (_statusMessageCoroutine != null)
            StopCoroutine(_statusMessageCoroutine);
    }
}