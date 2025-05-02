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
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Settings")]
    [SerializeField] private string _defaultSessionName = "GameSession";

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

        // Hide loading panel initially
        if (_loadingPanel != null)
        {
            _loadingPanel.SetActive(false);
        }
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

        // Start host
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

        // Join game
        await _networkRunnerHandler.StartClientGame(sessionName);
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