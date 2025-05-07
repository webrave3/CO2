using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

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

    [Header("Region Selection")]
    [SerializeField] private TMP_Dropdown _regionDropdown;
    [SerializeField] private bool _useAllRegions = true;
    [SerializeField] private Button _debugButton;

    // Dictionary of region names to codes
    private Dictionary<string, string> _regionCodes = new Dictionary<string, string>()
    {
        { "Auto (Best)", "best" },
        { "Europe", "eu" },
        { "US East", "us" },
        { "US West", "usw" },
        { "Asia", "asia" },
        { "Japan", "jp" },
        { "Australia", "au" },
        { "South America", "sa" },
        { "South Africa", "za" }
    };

    private NetworkRunnerHandler _networkRunnerHandler;

    void Start()
    {
        Debug.Log("[MainMenuUI] Start method executing");

        // Get NetworkRunnerHandler - first try from BootstrapManager
        if (BootstrapManager.Instance != null)
        {
            _networkRunnerHandler = BootstrapManager.Instance.GetNetworkRunnerHandler();
            Debug.Log("[MainMenuUI] Attempted to get NetworkRunnerHandler from BootstrapManager");
        }

        // If still null, try direct lookup
        if (_networkRunnerHandler == null)
        {
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
            Debug.Log("[MainMenuUI] Attempted direct lookup for NetworkRunnerHandler");
        }

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("[MainMenuUI] NetworkRunnerHandler not found - network features will be disabled");
            // Optionally disable network-dependent UI elements here
            if (_hostButton != null) _hostButton.interactable = false;
            if (_joinButton != null) _joinButton.interactable = false;
            if (_showBrowserButton != null) _showBrowserButton.interactable = false;
        }
        else
        {
            Debug.Log("[MainMenuUI] NetworkRunnerHandler found successfully");
        }

        // Initialize region dropdown
        InitializeRegionDropdown();

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

        // Set up debug button
        if (_debugButton != null)
        {
            _debugButton.onClick.RemoveAllListeners();
            _debugButton.onClick.AddListener(DebugRoomDiscovery);
            Debug.Log("Debug button setup complete");
        }

        // Host Panel-specific buttons
        if (_hostPanel != null)
        {
            Button hostStartButton = _hostPanel.GetComponentInChildren<Button>();
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

        // Check for auto-actions from in-game menu
        if (PlayerPrefs.HasKey("AutoHost"))
        {
            Debug.Log("Auto-Host flag detected, showing host panel");
            PlayerPrefs.DeleteKey("AutoHost");
            PlayerPrefs.Save();
            ShowHostGamePanel();
        }
        else if (PlayerPrefs.HasKey("AutoJoin"))
        {
            Debug.Log("Auto-Join flag detected, showing join panel");
            PlayerPrefs.DeleteKey("AutoJoin");
            PlayerPrefs.Save();
            if (_roomBrowserUI != null)
                _roomBrowserUI.ShowRoomBrowser();
            else
                ShowPanel(_joinGamePanel);
        }
    }

    private void InitializeRegionDropdown()
    {
        if (_regionDropdown != null)
        {
            _regionDropdown.ClearOptions();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

            foreach (var region in _regionCodes.Keys)
            {
                options.Add(new TMP_Dropdown.OptionData(region));
            }

            _regionDropdown.AddOptions(options);
            _regionDropdown.value = 0; // Default to "Auto (Best)"

            Debug.Log("Region dropdown initialized with " + options.Count + " regions");
        }
    }

    // Add this method to your MainMenuUI.cs
    public void ShowHostGamePanel()
    {
        ShowPanel(_hostPanel);
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

        // Get selected region
        string selectedRegion = "best";
        if (_regionDropdown != null && _regionDropdown.value >= 0)
        {
            selectedRegion = _regionCodes.ElementAt(_regionDropdown.value).Value;
        }

        // Start host using the updated method with region
        if (_networkRunnerHandler != null)
        {
            await _networkRunnerHandler.StartHostGame(sessionName, selectedRegion, _useAllRegions);
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

    // Debug function for room discovery
    public void DebugRoomDiscovery()
    {
        if (_networkRunnerHandler == null)
        {
            Debug.LogError("Cannot debug - NetworkRunnerHandler is null");
            return;
        }

        Debug.Log("Triggering room discovery debug");
        _networkRunnerHandler.DebugRoomDiscovery();

        // Show feedback to user
        if (_statusText != null)
        {
            string currentPanel = _mainPanel.activeSelf ? "main menu" : "current panel";
            _statusText.text = "Debug info printed to console.\nCheck logs for details.";
            _statusText.gameObject.SetActive(true);
            StartCoroutine(HideStatusAfterDelay(3f));
        }
    }

    private System.Collections.IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null)
        {
            _statusText.gameObject.SetActive(false);
        }
    }
}