using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections.Generic;
using TMPro;

// Ensure this script inherits from NetworkBehaviour
public class LobbyManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button _startGameButton;
    [SerializeField] private TextMeshProUGUI _waitingText;
    [SerializeField] private Transform _playerReadyListParent;
    [SerializeField] private GameObject _playerReadyEntryPrefab;

    [Header("Scene Settings")]
    [SerializeField] private string _gameSceneName = "Game";

    private Dictionary<PlayerRef, GameObject> _playerReadyEntries = new Dictionary<PlayerRef, GameObject>();
    private NetworkRunnerHandler _networkRunnerHandler;

    private void Awake()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_startGameButton != null)
        {
            _startGameButton.onClick.AddListener(OnStartGameClicked);
        }
    }

    public override void Spawned()
    {
        // Basic UI setup based on host/client
        if (_startGameButton != null)
        {
            // Only host can start the game
            _startGameButton.gameObject.SetActive(Runner.IsServer);
        }
        if (_waitingText != null)
        {
            // Show waiting text for clients
            _waitingText.gameObject.SetActive(!Runner.IsServer);
        }

        RefreshPlayerReadyList(); // Initial population
    }

    private void OnStartGameClicked()
    {
        // Only the host (server) can start the game
        if (Runner != null && Runner.IsServer)
        {
            GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
            if (gameStateManager != null)
            {
                // Tell GameStateManager (on the server) to change state
                gameStateManager.RPC_StartGame();

                // Host loads the game scene for everyone
                if (_networkRunnerHandler != null)
                {
                    _networkRunnerHandler.LoadScene(_gameSceneName);
                }
            }
        }
    }

    private void Update()
    {
        // Example: Allow toggling ready state with Space key
        if (Runner != null && Runner.IsRunning && Input.GetKeyDown(KeyCode.Space))
        {
            ToggleReady();
        }
    }

    private void ToggleReady()
    {
        GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
        // Ensure Runner and GameStateManager are valid before proceeding
        // ** CORRECTED PlayerRef Check **
        if (gameStateManager != null && Runner != null && Runner.LocalPlayer != PlayerRef.None)
        {
            bool currentStatus = false;
            // Safely check the dictionary using TryGet
            if (gameStateManager.PlayersReady.TryGet(Runner.LocalPlayer, out NetworkBool readyStatus))
            {
                currentStatus = readyStatus;
            }

            // Send RPC to server to update ready state
            gameStateManager.RPC_SetPlayerReady(Runner.LocalPlayer, !currentStatus);

            // Update local UI immediately for responsiveness
            UpdateReadyUI(Runner.LocalPlayer, !currentStatus);
        }
    }

    // Updates the local player's UI indicator (visual feedback)
    private void UpdateReadyUI(PlayerRef player, bool isReady)
    {
        if (_playerReadyEntries.TryGetValue(player, out GameObject entryGO))
        {
            // Find the Image component used as the ready indicator
            Image readyImage = entryGO.GetComponentInChildren<Image>(); // Adjust if the Image is not a direct child
            if (readyImage != null)
            {
                readyImage.color = isReady ? Color.green : Color.red;
            }
        }
    }

    // Call this when the player list or ready states might have changed
    public void RefreshPlayerReadyList()
    {
        if (_playerReadyListParent == null || _playerReadyEntryPrefab == null) return;

        // Clear existing UI entries
        foreach (var entry in _playerReadyEntries.Values)
        {
            if (entry != null) Destroy(entry);
        }
        _playerReadyEntries.Clear();

        GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
        // Ensure Runner and GameStateManager are available
        if (gameStateManager == null || Runner == null || !Runner.IsRunning) return;

        // Add a UI entry for each currently active player
        foreach (var playerRef in Runner.ActivePlayers)
        {
            // ** CORRECTED PlayerRef Check **
            // Make sure the playerRef is valid before proceeding
            if (playerRef == PlayerRef.None) continue;

            GameObject entryGO = Instantiate(_playerReadyEntryPrefab, _playerReadyListParent);
            _playerReadyEntries.Add(playerRef, entryGO);

            // Set player name/ID in the UI entry
            TextMeshProUGUI nameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                // You might want a more sophisticated way to get player names later
                nameText.text = $"Player {playerRef.PlayerId}";
            }

            // Set ready status indicator in the UI entry
            Image readyImage = entryGO.GetComponentInChildren<Image>(); // Adjust selector if needed
            if (readyImage != null)
            {
                bool isReady = false;
                // Use TryGet for safety when accessing the networked dictionary
                if (gameStateManager.PlayersReady.TryGet(playerRef, out NetworkBool readyStatus))
                {
                    isReady = readyStatus;
                }
                readyImage.color = isReady ? Color.green : Color.red;
            }
        }
    }

    // ** REMOVED 'override' KEYWORD FROM CALLBACKS BELOW **
    // These methods are called automatically by Fusion when players join or leave

    // Called by Fusion when a player joins the session (on host and clients)
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Refresh the list when a new player joins
        // Delay slightly might help ensure player data is ready
        Invoke(nameof(RefreshPlayerReadyList), 0.1f);
    }

    // Called by Fusion when a player leaves the session (on host and clients)
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Remove the UI entry for the player who left
        if (_playerReadyEntries.TryGetValue(player, out GameObject entryGO))
        {
            if (entryGO != null) Destroy(entryGO);
            _playerReadyEntries.Remove(player);
        }
    }
}