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
    }

    private void OnStartGameClicked()
    {
        if (Runner.IsServer)
        {
            // Tell GameStateManager to start the game
            GameStateManager gameStateManager = FindObjectOfType<GameStateManager>();
            if (gameStateManager != null)
            {
                gameStateManager.RPC_StartGame();
            }

            // Load the game scene
            _networkRunnerHandler.LoadScene(_gameSceneName);
        }
    }

    private void Update()
    {
        // Check for ready button press
        if (Runner != null && Runner.IsRunning && Input.GetKeyDown(KeyCode.Space))
        {
            // Toggle ready status
            ToggleReady();
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

            // Toggle and send to server
            gameStateManager.RPC_SetPlayerReady(Runner.LocalPlayer, !currentStatus);
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