using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;
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
    [SerializeField] private Button _joinGameNavButton;
    [SerializeField] private Button _settingsNavButton;

    private NetworkRunnerHandler _networkRunnerHandler;

    private void Start()
    {
        Debug.Log("MainMenuUI Start method executing");

        // Get NetworkRunnerHandler
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("NetworkRunnerHandler not found");
        }
        else
        {
            Debug.Log("NetworkRunnerHandler found successfully");
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

        // Set up UI callbacks - Clear all listeners first to avoid duplicates
        SetupButton(_hostButton, () => {
            Debug.Log("Host button clicked");
            ShowPanel(_hostPanel);
        }, "Host Button");

        SetupButton(_joinButton, () => {
            Debug.Log("Join button clicked");
            ShowPanel(_joinGamePanel);

            // Also refresh room list if room browser UI exists
            if (_roomBrowserUI != null)
            {
                _roomBrowserUI.ShowRoomBrowser();
            }
        }, "Join Button");

        SetupButton(_settingsButton, () => {
            Debug.Log("Settings button clicked");
            ShowPanel(_settingsPanel);
        }, "Settings Button");

        SetupButton(_quitButton, OnQuitButtonClicked, "Quit Button");

        if (_showBrowserButton != null)
        {
            _showBrowserButton.onClick.RemoveAllListeners();
            _showBrowserButton.onClick.AddListener(() => {
                Debug.Log("Show Browser button clicked");
                ShowPanel(_joinGamePanel);

                if (_roomBrowserUI != null)
                {
                    _roomBrowserUI.ShowRoomBrowser();
                }
                else
                {
                    Debug.LogWarning("RoomBrowserUI reference is missing");
                }
            });
            Debug.Log("Show Browser button setup complete");
        }

        if (_directJoinButton != null)
        {
            _directJoinButton.onClick.RemoveAllListeners();
            _directJoinButton.onClick.AddListener(OnDirectJoinButtonClicked);
            Debug.Log("Direct Join button setup complete");
        }

        // Host Panel-specific buttons
        if (_hostPanel != null)
        {
            Button hostStartButton = _hostPanel.transform.Find("Host")?.GetComponent<Button>();
            if (hostStartButton != null)
            {
                hostStartButton.onClick.RemoveAllListeners();
                hostStartButton.onClick.AddListener(OnHostGameStartClicked);
                Debug.Log("Host Start button setup complete");
            }
        }

        // Ensure main panel is active at start
        ShowPanel(_mainPanel);

        // Set up back buttons
        SetupBackButtons();

        Debug.Log("MainMenuUI initialization complete");
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action, string buttonName)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            Debug.Log($"{buttonName} setup complete");
        }
        else
        {
            Debug.LogWarning($"{buttonName} reference is missing");
        }
    }

    // Public method to allow other scripts to show the main panel
    public void ShowMainPanel()
    {
        ShowPanel(_mainPanel);
    }

    public void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow == null)
        {
            Debug.LogError("ShowPanel called with NULL panel reference!");
            return;
        }

        Debug.Log($"Showing panel: {panelToShow.name}");

        // Disable all panels first
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_hostPanel != null) _hostPanel.SetActive(false);
        if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_loadingPanel != null) _loadingPanel.SetActive(false);

        // Then enable the requested panel
        panelToShow.SetActive(true);

        // Verify it worked
        Debug.Log($"Panel {panelToShow.name} active state: {panelToShow.activeSelf}");
    }

    private async void OnHostGameStartClicked()
    {
        Debug.Log("Host Game Start button clicked");

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
        if (_networkRunnerHandler != null)
        {
            await _networkRunnerHandler.StartHostGame(sessionName);
        }
        else
        {
            Debug.LogError("Cannot start host - NetworkRunnerHandler is null");
            // Return to main panel if failed
            ShowPanel(_mainPanel);
        }
    }

    private async void OnDirectJoinButtonClicked()
    {
        Debug.Log("Direct join button clicked");

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
            ShowPanel(_mainPanel);
            return;
        }

        // Join game by hash
        if (_networkRunnerHandler != null)
        {
            await _networkRunnerHandler.StartClientGameByHash(roomCode);
        }
        else
        {
            Debug.LogError("Cannot join by hash - NetworkRunnerHandler is null");
            // Return to main panel if failed
            ShowPanel(_mainPanel);
        }
    }

    private void OnQuitButtonClicked()
    {
        Debug.Log("Quit button clicked");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
        ShowPanel(_loadingPanel);

        if (_statusText != null)
        {
            _statusText.text = statusMessage;
        }
    }

    // Add this to set up all back buttons
    private void SetupBackButtons()
    {
        // Find and set up all back buttons
        SetupBackButton(_hostPanel, "HostPanel");
        SetupBackButton(_settingsPanel, "SettingsPanel");
        // Note: JoinGamePanel's back button is handled by RoomBrowserUI
    }

    private void SetupBackButton(GameObject panel, string panelName)
    {
        if (panel == null) return;

        // Find the back button within this panel
        Button backButton = panel.transform.Find("Back")?.GetComponent<Button>();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => OnBackButtonClicked(panelName));
            Debug.Log($"Back button for {panelName} set up");
        }
        else
        {
            Debug.LogWarning($"Back button for {panelName} not found");
        }
    }

    // Add this method to handle back button clicks from any panel
    public void OnBackButtonClicked(string fromPanel)
    {
        Debug.Log($"Back button clicked from {fromPanel}");
        ShowPanel(_mainPanel);  // Return to main panel
    }
}