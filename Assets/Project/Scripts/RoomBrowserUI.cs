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
    [SerializeField] private bool _showPlaceholderWhenEmpty = true; // Still useful for UX

    private NetworkRunnerHandler _networkRunnerHandler;
    private List<GameObject> _roomEntries = new List<GameObject>();
    private bool _isJoining = false;
    private Coroutine _statusMessageCoroutine;

    void Start()
    {
        // Get reference, handling potential absence
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        if (_networkRunnerHandler == null)
        {
            // Log error only once, maybe disable functionality
            enabled = false; // Disable update loop etc.
            return;
        }


        if (_statusText != null) _statusText.gameObject.SetActive(false);
        if (_joiningIndicator != null) _joiningIndicator.SetActive(false);

        if (_refreshButton != null) _refreshButton.onClick.AddListener(RefreshRoomList);

        if (_backButton != null)
            _backButton.onClick.AddListener(() => {
                if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
                MainMenuUI mainMenu = FindObjectOfType<MainMenuUI>();
                if (mainMenu != null) mainMenu.ShowMainPanel();
            });

        if (_directJoinButton != null) _directJoinButton.onClick.AddListener(OnDirectJoinClicked);
    }

    public void ShowRoomBrowser()
    {
        if (_joinGamePanel == null) return;
        _joinGamePanel.SetActive(true);

        if (_statusText != null)
        {
            _statusText.text = "Loading room list...";
            _statusText.color = Color.white;
            _statusText.gameObject.SetActive(true);
        }
        if (_roomCodeInput != null) _roomCodeInput.text = "";
        if (_roomListContent != null) _roomListContent.gameObject.SetActive(true);

        ClearRoomEntries();
        if (_showPlaceholderWhenEmpty) AddPlaceholderRooms();

        if (_networkRunnerHandler != null) RefreshRoomList();
        else ShowStatusMessage("Network system not available", Color.red);
    }

    // --- MODIFICATION: This function is now much simpler and more reliable ---
    private async void OnDirectJoinClicked()
    {
        if (_isJoining) return;
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

        // 1. Set UI to "joining" state
        SetJoiningState(true, $"Joining room: {roomCode}...");

        try
        {
            // 2. Await the join attempt and get the true/false result
            bool joinSucceeded = await _networkRunnerHandler.StartClientGameByHash(roomCode);

            // 3. Check the result
            if (joinSucceeded)
            {
                // Success! The NetworkRunnerHandler will change the scene.
                // We can just reset the UI in case the user returns.
                SetJoiningState(false);
            }
            else
            {
                // Failure! Show an error and reset the UI so the user can try again.
                ShowStatusMessage("Failed to find or connect to game with that code", Color.red);
                SetJoiningState(false);
            }
        }
        catch (Exception ex)
        {
            // Catch any other unexpected errors
            ShowStatusMessage($"Error joining: {ex.Message}", Color.red);
            SetJoiningState(false);
        }
    }

    // --- REMOVED: The CheckJoinResult() coroutine is no longer needed. ---

    // --- RENAMED: from ResetJoiningState to SetJoiningState ---
    private void SetJoiningState(bool isJoining, string statusMessage = "")
    {
        _isJoining = isJoining;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(isJoining);
        if (_directJoinButton != null) _directJoinButton.interactable = !isJoining;

        if (isJoining && !string.IsNullOrEmpty(statusMessage))
        {
            ShowStatusMessage(statusMessage, Color.white, 0); // Show persistent status
        }
    }

    public async void RefreshRoomList()
    {
        if (_networkRunnerHandler == null)
        {
            _networkRunnerHandler = NetworkRunnerHandler.GetInstance(); // Try to get instance
            if (_networkRunnerHandler == null)
            {
                ShowStatusMessage("Network system not initialized", Color.red);
                return;
            }
        }

        try
        {
            ShowStatusMessage("Refreshing room list...", Color.white);
            ClearRoomEntries();
            if (_roomListContent != null) _roomListContent.gameObject.SetActive(true);
            if (_showPlaceholderWhenEmpty) AddPlaceholderRooms();

            await _networkRunnerHandler.RefreshSessionList();
            await Task.Delay(500); // Allow time for list update
            PopulateRoomList();
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Error refreshing: {ex.Message}", Color.red);
            _networkRunnerHandler?.RecoverNetworkState(); // Attempt recovery
        }
    }

    private void ClearRoomEntries()
    {
        foreach (var entry in _roomEntries) Destroy(entry);
        _roomEntries.Clear();
    }

    private void PopulateRoomList()
    {
        ClearRoomEntries();
        if (_networkRunnerHandler == null) return;

        List<SessionInfo> availableSessions = _networkRunnerHandler.GetAvailableSessions();
        bool anyRealRoomsShown = false;

        if (availableSessions.Count > 0)
        {
            foreach (var session in availableSessions)
            {
                // Skip internal/browser sessions
                if (session.Name.StartsWith("BROWSER_") || session.Name.StartsWith("TempDiscoverySession_")) continue;

                GameObject entryGO = Instantiate(_roomEntryPrefab, _roomListContent);
                _roomEntries.Add(entryGO);
                anyRealRoomsShown = true;

                TextMeshProUGUI roomNameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
                if (roomNameText != null)
                {
                    string sessionHash = session.Properties.TryGetValue("Hash", out var hashObj) ? hashObj.PropertyValue.ToString() : "N/A";
                    string displayName = session.Properties.TryGetValue("DisplayName", out var nameObj) ? nameObj.PropertyValue.ToString() : session.Name;
                    string regionText = session.Properties.TryGetValue("Region", out var regionObj) ? regionObj.PropertyValue.ToString() : session.Region;

                    roomNameText.text = $"{displayName}\nPlayers: {session.PlayerCount}/{session.MaxPlayers}\nRegion: {regionText}\nCode: {sessionHash}";
                }

                Button joinButton = entryGO.GetComponentInChildren<Button>();
                if (joinButton != null)
                {
                    SessionInfo sessionToJoin = session; // Local copy for closure
                    joinButton.onClick.AddListener(() => JoinSession(sessionToJoin));
                }
            }
        }

        if (anyRealRoomsShown)
        {
            if (availableSessions.Count > _roomEntries.Count) // Check if we filtered some sessions
                ShowStatusMessage($"Found {_roomEntries.Count} public games", Color.green, 2f);
            else
                ShowStatusMessage($"Found {availableSessions.Count} games", Color.green, 2f);
        }
        else if (_showPlaceholderWhenEmpty)
        {
            AddPlaceholderRooms();
            ShowStatusMessage("No active games found, showing examples", Color.yellow);
        }
        else
        {
            ShowStatusMessage("No active games found", Color.yellow);
        }
    }

    // --- MODIFICATION: This function is now much simpler and more reliable ---
    private async void JoinSession(SessionInfo sessionInfo)
    {
        if (_networkRunnerHandler == null || _isJoining) return;

        string sessionHash = sessionInfo.Properties.TryGetValue("Hash", out var hashObj) ? hashObj.PropertyValue.ToString() : "N/A";
        SetJoiningState(true, $"Joining {sessionHash}...");

        try
        {
            // 1. Await the join attempt
            bool joinSucceeded = await _networkRunnerHandler.StartClientGameBySessionInfo(sessionInfo);

            // 2. Check the result
            if (joinSucceeded)
            {
                // Success! Scene will change.
                SetJoiningState(false);
            }
            else
            {
                // Failure!
                ShowStatusMessage("Failed to join the selected game.", Color.red);
                SetJoiningState(false);
            }
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Failed to join: {ex.Message}", Color.red);
            SetJoiningState(false);
        }
    }

    private void AddPlaceholderRooms()
    {
        // Example placeholders
        AddPlaceholderRoomEntry("Corporate Override", "3/6", "RedFox293", "(Example)");
        AddPlaceholderRoomEntry("Research Lab", "2/6", "BlueBird478", "(Example)");
    }

    private void AddPlaceholderRoomEntry(string name, string players, string code, string suffix)
    {
        if (_roomEntryPrefab == null || _roomListContent == null) return;
        GameObject entryGO = Instantiate(_roomEntryPrefab, _roomListContent);
        _roomEntries.Add(entryGO);

        TextMeshProUGUI roomNameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
        if (roomNameText != null)
        {
            roomNameText.text = $"{name} {suffix}\nPlayers: {players}\nRegion: Example\nCode: {code}";
        }
        Button joinButton = entryGO.GetComponentInChildren<Button>();
        if (joinButton != null)
        {
            joinButton.interactable = false;
            TextMeshProUGUI buttonText = joinButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = "Demo";
        }
    }

    private void ShowStatusMessage(string message, Color color, float duration = 0)
    {
        if (_statusText == null) return;
        if (_statusMessageCoroutine != null) StopCoroutine(_statusMessageCoroutine);

        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);

        float actualDuration = duration > 0 ? duration : _statusMessageDuration;
        if (actualDuration > 0)
        {
            _statusMessageCoroutine = StartCoroutine(HideStatusAfterDelay(actualDuration));
        }
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null) _statusText.gameObject.SetActive(false);
        _statusMessageCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_statusMessageCoroutine != null) StopCoroutine(_statusMessageCoroutine);
    }
}