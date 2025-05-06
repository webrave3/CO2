using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using System;

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
    }

    // This is the method to call from MainMenuUI
    public void ShowRoomBrowser()
    {
        if (_joinGamePanel != null)
        {
            _joinGamePanel.SetActive(true);

            // Clear any previous status messages
            if (_statusText != null)
                _statusText.gameObject.SetActive(false);

            // Clear room code input field
            if (_roomCodeInput != null)
                _roomCodeInput.text = "";

            RefreshRoomList();
        }
        else
        {
            Debug.LogError("Join Game Panel reference is missing in RoomBrowserUI");
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

    public void RefreshRoomList()
    {
        if (_networkRunnerHandler == null)
        {
            ShowStatusMessage("Network system not initialized", Color.red);
            return;
        }

        if (_roomListContent == null || _roomEntryPrefab == null)
        {
            Debug.LogError("Room list references missing");
            return;
        }

        // Force refresh sessions from network
        _networkRunnerHandler.ForceRefreshSessions();

        // Show a temporary refreshing message
        ShowStatusMessage("Refreshing room list...", Color.white, 1.5f);

        // Give network some time to update
        StartCoroutine(RefreshAfterDelay(0.5f));
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

                    // Try to get hash and display name from properties
                    if (session.Properties.TryGetValue("Hash", out var hashObj))
                        sessionHash = hashObj.PropertyValue.ToString();

                    if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                        displayName = nameObj.PropertyValue.ToString();

                    roomNameText.text = $"{displayName}\nPlayers: {session.PlayerCount}/{session.MaxPlayers}\nCode: {sessionHash}";
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
            roomNameText.text = $"{name} {suffix}\nPlayers: {players}\nCode: {code}";
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