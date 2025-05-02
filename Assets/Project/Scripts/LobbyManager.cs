using UnityEngine;
using UnityEngine.UI;
using Fusion;
using System.Collections.Generic;
using TMPro;

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

    // Add debug info
    [SerializeField] private TextMeshProUGUI _debugText;

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
        RepositionUI();
        if (_startGameButton != null)
        {
            // Only show the start button for the host
            _startGameButton.gameObject.SetActive(Runner.IsServer);
        }

        if (_waitingText != null)
        {
            _waitingText.gameObject.SetActive(!Runner.IsServer);
        }

        // Set up player ready list
        RefreshPlayerReadyList();

        if (_debugText != null)
        {
            _debugText.text = $"Player ID: {Runner.LocalPlayer.PlayerId}\nIs Server: {Runner.IsServer}";
        }
    }

    private void OnStartGameClicked()
    {
        if (Runner.IsServer)
        {
            // Tell GameStateManager to start the game
            GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
            if (gameStateManager != null)
            {
                // Call RPC directly
                gameStateManager.RPC_StartGame();

                // Load the game scene - this should be done after game state change
                if (_networkRunnerHandler != null)
                {
                    _networkRunnerHandler.LoadScene(_gameSceneName);
                }
            }
        }
    }

    private void Update()
    {
        // Only attempt to toggle ready if we have a runner and we're in a session
        if (Runner != null && Runner.IsRunning)
        {
            // Check for ready button press
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("Space pressed - toggling ready state");
                ToggleReady();
            }
        }
    }

    private void ToggleReady()
    {
        GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
        if (gameStateManager != null)
        {
            // Get current ready status
            bool currentStatus = false;
            if (gameStateManager.PlayersReady.TryGet(Runner.LocalPlayer, out NetworkBool readyStatus))
            {
                currentStatus = readyStatus;
            }

            Debug.Log($"Toggling ready state from {currentStatus} to {!currentStatus}");

            // Call RPC directly
            gameStateManager.RPC_SetPlayerReady(Runner.LocalPlayer, !currentStatus);

            // Update UI immediately for responsiveness (will be overwritten by network state)
            UpdateReadyUI(Runner.LocalPlayer, !currentStatus);
        }
    }

    // Add method to update UI immediately
    private void UpdateReadyUI(PlayerRef player, bool isReady)
    {
        if (_playerReadyEntries.TryGetValue(player, out GameObject entryGO))
        {
            UnityEngine.UI.Image readyImage = entryGO.GetComponentInChildren<UnityEngine.UI.Image>();
            if (readyImage != null)
            {
                readyImage.color = isReady ? Color.green : Color.red;
            }
        }
    }

    // Add this method to your LobbyManager class
    private void RepositionUI()
    {
        // Find the Canvas
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            // Make sure it's Screen Space - Overlay
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Find the main panel (assuming first child of canvas)
            RectTransform panel = canvas.transform.GetChild(0) as RectTransform;
            if (panel != null)
            {
                // Set anchor to right side
                panel.anchorMin = new Vector2(0.7f, 0);
                panel.anchorMax = new Vector2(1, 1);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;

                // Adjust size to fit
                panel.sizeDelta = Vector2.zero;
            }
        }
    }

    public void RefreshPlayerReadyList()
    {
        if (_playerReadyListParent == null || _playerReadyEntryPrefab == null)
            return;

        // Clear existing entries
        foreach (var entry in _playerReadyEntries.Values)
        {
            Destroy(entry);
        }
        _playerReadyEntries.Clear();

        // Get the GameStateManager
        GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
        if (gameStateManager == null)
            return;

        // Add entry for each player
        foreach (var playerRef in Runner.ActivePlayers)
        {
            GameObject entryGO = Instantiate(_playerReadyEntryPrefab, _playerReadyListParent);
            _playerReadyEntries.Add(playerRef, entryGO);

            // Set player name
            TextMeshProUGUI nameText = entryGO.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = $"Player {playerRef.PlayerId}";
            }

            // Fix ambiguous reference by fully qualifying the type
            UnityEngine.UI.Image readyImage = entryGO.GetComponentInChildren<UnityEngine.UI.Image>();
            if (readyImage != null)
            {
                bool isReady = false;
                if (gameStateManager.PlayersReady.TryGet(playerRef, out NetworkBool readyStatus))
                {
                    isReady = readyStatus;
                }
                readyImage.color = isReady ? Color.green : Color.red;
            }
        }
    }
}