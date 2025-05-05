using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private TMP_InputField _sessionNameInput;
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Room Browser")]
    [SerializeField] private Button _showBrowserButton;
    [SerializeField] private RoomBrowserUI _roomBrowserUI;

    [Header("Direct Join")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _directJoinButton;

    [Header("Settings")]
    [SerializeField] private string _defaultSessionName = "GameSession";

    [Header("Panels")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _hostPanel;
    [SerializeField] private GameObject _joinGamePanel; // Combined Join/Browse panel
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _loadingPanel;

    [Header("Navigation Buttons")]
    [SerializeField] private Button _hostGameNavButton;
    [SerializeField] private Button _joinGameNavButton; // Points to the combined panel
    [SerializeField] private Button _settingsNavButton;

    private NetworkRunnerHandler _networkRunnerHandler;

    private void Start()
    {
        UnityEngine.Debug.Log("MainMenuUI Start");

        // Get NetworkRunnerHandler
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_networkRunnerHandler == null)
        {
            UnityEngine.Debug.LogError("NetworkRunnerHandler not found.");
            return;
        }

        // Set default session name
        if (_sessionNameInput != null && string.IsNullOrEmpty(_sessionNameInput.text))
        {
            _sessionNameInput.text = _defaultSessionName;
        }

        // Set default player name if not set
        if (_playerNameInput != null && string.IsNullOrEmpty(_playerNameInput.text))
        {
            _playerNameInput.text = "Player" + UnityEngine.Random.Range(1000, 9999);
        }

        // Set up UI callbacks
        if (_hostButton != null)
        {
            _hostButton.onClick.AddListener(OnHostButtonClicked);
        }

        if (_joinButton != null)
        {
            _joinButton.onClick.AddListener(OnJoinButtonClicked);
        }

        if (_showBrowserButton != null)
        {
            _showBrowserButton.onClick.AddListener(() => {
                if (_roomBrowserUI != null)
                    _roomBrowserUI.ShowRoomBrowser();
            });
        }

        if (_directJoinButton != null)
        {
            _directJoinButton.onClick.AddListener(OnDirectJoinButtonClicked);
        }

        // Hide loading panel initially
        if (_loadingPanel != null)
        {
            _loadingPanel.SetActive(false);
        }
        if (_showBrowserButton != null)
        {
            _showBrowserButton.onClick.AddListener(() => {
                // First hide the main panel and show join panel
                ShowPanel(_joinGamePanel);

                // Then refresh the room list
                if (_roomBrowserUI != null)
                    _roomBrowserUI.ShowRoomBrowser();
            });
        }
    }

    private void ShowPanel(GameObject panelToShow)
    {
        // Hide all panels
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_hostPanel != null) _hostPanel.SetActive(false);
        if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_loadingPanel != null) _loadingPanel.SetActive(false);

        // Show the selected panel
        if (panelToShow != null)
            panelToShow.SetActive(true);
    }
    private async void OnHostButtonClicked()
    {
        UnityEngine.Debug.Log("Host button clicked");

        // Save player name
        SavePlayerName();

        // Show loading UI
        ShowLoadingUI("Starting host...");

        // Get session name
        string sessionName = _defaultSessionName;
        if (_sessionNameInput != null && !string.IsNullOrEmpty(_sessionNameInput.text))
        {
            sessionName = _sessionNameInput.text;
        }

        // Start host using the updated method
        await _networkRunnerHandler.StartHostGame(sessionName);
    }

    private async void OnJoinButtonClicked()
    {
        UnityEngine.Debug.Log("Join button clicked");

        // Save player name
        SavePlayerName();

        // Show loading UI
        ShowLoadingUI("Joining game...");

        // Get session name
        string sessionName = _defaultSessionName;
        if (_sessionNameInput != null && !string.IsNullOrEmpty(_sessionNameInput.text))
        {
            sessionName = _sessionNameInput.text;
        }

        // Join game using the updated method
        await _networkRunnerHandler.StartClientGame(sessionName);
    }

    private async void OnDirectJoinButtonClicked()
    {
        UnityEngine.Debug.Log("Direct join button clicked");

        // Save player name
        SavePlayerName();

        // Show loading UI
        ShowLoadingUI("Joining game by code...");

        // Get room code (hash)
        string roomCode = string.Empty;
        if (_roomCodeInput != null && !string.IsNullOrEmpty(_roomCodeInput.text))
        {
            roomCode = _roomCodeInput.text.Trim();
        }

        if (string.IsNullOrEmpty(roomCode))
        {
            // Show error and return to main menu
            ShowLoadingUI("Invalid room code. Please try again.");
            await Task.Delay(2000); // Wait 2 seconds to show error
            _loadingPanel.SetActive(false);
            _mainPanel.SetActive(true);
            return;
        }

        // Join game by hash
        await _networkRunnerHandler.StartClientGameByHash(roomCode);
    }

    private void SavePlayerName()
    {
        if (_playerNameInput != null && !string.IsNullOrEmpty(_playerNameInput.text))
        {
            PlayerPrefs.SetString("PlayerName", _playerNameInput.text);
            PlayerPrefs.Save();
        }
    }

    private void ShowLoadingUI(string statusMessage)
    {
        if (_mainPanel != null)
        {
            _mainPanel.SetActive(false);
        }

        if (_loadingPanel != null)
        {
            _loadingPanel.SetActive(true);
        }

        if (_statusText != null)
        {
            _statusText.text = statusMessage;
        }
    }
}