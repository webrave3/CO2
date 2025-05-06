using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using System.Collections;

public class RoomBrowserUI : MonoBehaviour
{
    [SerializeField] private GameObject _joinGamePanel;
    [SerializeField] private GameObject _roomEntryPrefab;
    [SerializeField] private Transform _roomListContent;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private bool _showPlaceholderWhenEmpty = true;

    // Direct Join elements
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _directJoinButton;

    private NetworkRunnerHandler _networkRunnerHandler;
    private List<GameObject> _roomEntries = new List<GameObject>();

    void Start()
    {
        // Get reference to NetworkRunnerHandler
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("NetworkRunnerHandler not found! Room browser will not function.");
        }

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
            RefreshRoomList();
        }
        else
        {
            Debug.LogError("Join Game Panel reference is missing in RoomBrowserUI");
        }
    }

    private async void OnDirectJoinClicked()
    {
        if (_networkRunnerHandler == null)
        {
            Debug.LogError("Cannot join room - NetworkRunnerHandler is null");
            return;
        }

        if (_roomCodeInput == null)
        {
            Debug.LogError("Room Code Input field is missing");
            return;
        }

        string roomCode = _roomCodeInput.text.Trim();
        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning("Room code is empty");
            return;
        }

        Debug.Log($"Attempting to join room with code: {roomCode}");
        await _networkRunnerHandler.StartClientGameByHash(roomCode);
    }

    public void RefreshRoomList()
    {
        // Add this null check at the beginning
        if (_networkRunnerHandler == null)
        {
            Debug.LogError("Cannot refresh room list - NetworkRunnerHandler is null");
            return;
        }

        if (_roomListContent == null)
        {
            Debug.LogError("Room List Content transform is missing");
            return;
        }

        if (_roomEntryPrefab == null)
        {
            Debug.LogError("Room Entry Prefab is missing");
            return;
        }

        // First force refresh sessions from network
        _networkRunnerHandler.ForceRefreshSessions();

        // Give network some time to update - you might want to show a loading indicator here
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

        // ALWAYS add placeholders for testing (modified based on request)
        AddPlaceholderRooms();

        // Then add real sessions if they exist
        if (availableSessions.Count > 0)
        {
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
                        Debug.Log($"Joining session: {sessionToJoin.Name}");
                        if (_networkRunnerHandler != null)
                        {
                            _networkRunnerHandler.StartClientGameBySessionInfo(sessionToJoin);
                        }

                        if (_joinGamePanel != null)
                        {
                            _joinGamePanel.SetActive(false);
                        }
                    });
                }
            }
        }
        else
        {
            Debug.Log("No real sessions found, showing only placeholders");
        }
    }

    // Modified to add 5 placeholder rooms
    private void AddPlaceholderRooms()
    {
        // Create 5 placeholder entries
        AddPlaceholderRoomEntry("Corporate Override", "3/6", "A1B2C3D4", "(Example Room)");
        AddPlaceholderRoomEntry("Research Lab", "2/6", "E5F6G7H8", "(Example Room)");
        AddPlaceholderRoomEntry("Maintenance Level", "4/6", "I9J0K1L2", "(Example Room)");
        AddPlaceholderRoomEntry("Security Division", "1/6", "M3N4O5P6", "(Example Room)");
        AddPlaceholderRoomEntry("Core Access", "5/6", "Q7R8S9T0", "(Example Room)");
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
}