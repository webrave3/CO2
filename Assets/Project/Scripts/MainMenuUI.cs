// Filename: MainMenuUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Fusion; // Required for SessionProperty

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private TextMeshProUGUI _statusText; // Still useful for loading/error messages

    // --- Host Panel Specific ---
    [Header("Host Game Settings")]
    [SerializeField] private TMP_InputField _hostSessionNameInput; // Renamed from _sessionNameInput for clarity
    [SerializeField] private Toggle _isPublicToggle; // To determine if the game is visible for matchmaking
    // [SerializeField] private TMP_Dropdown _hostLanguageDropdown; // ADDED: For host to set language
    // Add other filter UI elements here (e.g., difficulty dropdown)

    [Header("Join Game UI Reference")] // Renamed from Room Browser
    [SerializeField] private JoinGameUI _joinGameUI; // Renamed from RoomBrowserUI

    [Header("Settings")]
    [SerializeField] private string _defaultSessionName = "GameSession";

    [Header("Panels")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _hostPanel;
    [SerializeField] private GameObject _joinGamePanel; // Reference to the panel managed by JoinGameUI
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _loadingPanel;

    [Header("Navigation Buttons")] // Keep if used for panel switching
    [SerializeField] private Button _hostGameNavButton; // Not used in provided code?
    [SerializeField] private Button _joinGameNavButton; // Not used in provided code?
    [SerializeField] private Button _settingsNavButton; // Not used in provided code?

    [Header("Region Selection (Optional - Fusion handles region automatically)")]
    [SerializeField] private TMP_Dropdown _regionDropdown;
    [SerializeField] private bool _useAllRegions = true; // Still used by StartHostGame? Review if needed.

    // Dictionary of region names to codes (might be redundant if using PhotonAppSettings)
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
        Debug.Log("MainMenuUI Start: Starting...");

        // Get NetworkRunnerHandler
        if (BootstrapManager.Instance != null) { _networkRunnerHandler = BootstrapManager.Instance.GetNetworkRunnerHandler(); }
        if (_networkRunnerHandler == null) { _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>(); }

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("MainMenuUI Start: NetworkRunnerHandler NOT FOUND!");
            ShowStatusMessage("Network Error: Handler not found.", true);
            if (_hostButton != null) _hostButton.interactable = false;
            if (_joinButton != null) _joinButton.interactable = false;
        }
        else
        {
            Debug.Log("MainMenuUI Start: NetworkRunnerHandler found successfully.");
        }

        InitializeRegionDropdown(); // Keep if you still want manual region selection override

        // Set default session name in the HOST panel input
        if (_hostSessionNameInput != null && string.IsNullOrEmpty(_hostSessionNameInput.text))
        {
            _hostSessionNameInput.text = _defaultSessionName;
        }

        // Player Name Handling
        if (_playerNameInput != null)
        {
            _playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
        }

        // Setup Main Panel Button Callbacks
        SetupButton(_hostButton, () => ShowPanel(_hostPanel));
        SetupButton(_joinButton, () => {
            Debug.Log("MainMenuUI: Join Button Clicked!"); // <-- ADD THIS
            ShowPanel(_joinGamePanel);
            if (_joinGameUI != null)
            {
                _joinGameUI.ShowJoinPanel();
            }
            else
            {
                Debug.LogError("MainMenuUI: _joinGameUI reference is NULL when Join Button clicked!"); // <-- ADD THIS CHECK
            }
        });
        SetupButton(_settingsButton, () => ShowPanel(_settingsPanel));
        SetupButton(_quitButton, OnQuitButtonClicked);

        // Host Panel - Find and Setup Start Button
        if (_hostPanel != null)
        {
            // Try finding button by specific name first, then fallback
            Button hostStartButton = _hostPanel.transform.Find("StartHostButton")?.GetComponent<Button>();
            if (hostStartButton == null) hostStartButton = _hostPanel.GetComponentInChildren<Button>(true); // Search inactive too

            if (hostStartButton != null)
            {
                SetupButton(hostStartButton, OnHostGameStartClicked);
                Debug.Log("Host Start Button listener attached.");
            }
            else
            {
                Debug.LogError("Could not find StartHostButton in the Host Panel!");
            }
        }

        ShowPanel(_mainPanel); // Ensure main panel is active at start
        SetupBackButtons(); // Setup back buttons for panels
    }

    private void InitializeRegionDropdown()
    {
        // Keep this if you want manual region override, otherwise remove/disable
        if (_regionDropdown != null)
        {
            _regionDropdown.ClearOptions();
            _regionDropdown.AddOptions(_regionCodes.Keys.ToList());
            _regionDropdown.value = 0; // Default to "Auto (Best)"
        }
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners(); // Prevent duplicate listeners
            button.onClick.AddListener(action);
        }
    }

    public void ShowMainPanel() => ShowPanel(_mainPanel);
    public void ShowHostGamePanel() => ShowPanel(_hostPanel);

    public void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow == null) return;

        // Disable all panels first
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_hostPanel != null) _hostPanel.SetActive(false);
        if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_loadingPanel != null) _loadingPanel.SetActive(false);

        // Then enable the requested panel
        panelToShow.SetActive(true);
    }

    // --- MODIFIED: Handles starting the host with Session Properties ---
    private async void OnHostGameStartClicked()
    {
        if (_networkRunnerHandler == null)
        {
            ShowStatusMessage("Network Error. Cannot host.", true);
            return;
        }

        SavePlayerName();
        ShowLoadingUI("Starting game...");

        string sessionName = (_hostSessionNameInput != null && !string.IsNullOrEmpty(_hostSessionNameInput.text))
            ? _hostSessionNameInput.text : _defaultSessionName;

        // Determine visibility based on toggle
        bool isVisible = _isPublicToggle != null ? _isPublicToggle.isOn : true; // Default to public if no toggle

        // --- Prepare Custom Session Properties (Filters) ---
        var customSessionProperties = new Dictionary<string, SessionProperty>();

        // Example: Add Language Filter (Uncomment and assign _hostLanguageDropdown in Inspector)
        // if (_hostLanguageDropdown != null) {
        //     string selectedLanguage = _hostLanguageDropdown.options[_hostLanguageDropdown.value].text;
        //     if (!string.IsNullOrEmpty(selectedLanguage) && selectedLanguage != "Any") { // Don't add if "Any"
        //         customSessionProperties["lang"] = selectedLanguage; // Key must match client filter key
        //         Debug.Log($"Hosting with Language property: {selectedLanguage}");
        //     }
        // }

        // Example: Add Difficulty Filter (Requires a UI element like _hostDifficultyDropdown)
        // string selectedDifficulty = "Normal"; // Get from UI
        // customSessionProperties["difficulty"] = selectedDifficulty;

        // Add other filters as needed...

        // Start host using the method that accepts session properties
        // Region selection logic might be redundant if using PhotonAppSettings, review NetworkRunnerHandler's StartHostGame
        // string selectedRegion = "best"; // Default (often handled by AppSettings now)
        // if (_regionDropdown != null && _regionDropdown.value >= 0 && _regionDropdown.value < _regionCodes.Count)
        // {
        //     selectedRegion = _regionCodes.ElementAt(_regionDropdown.value).Value;
        // }
        // await _networkRunnerHandler.StartHostGame(sessionName, selectedRegion, _useAllRegions, isVisible, customSessionProperties); // Pass properties

        // --- Simpler call assuming region is handled by AppSettings ---
        await _networkRunnerHandler.StartHostGame(sessionName, isVisible, customSessionProperties); // Pass properties

        // Check if starting failed (Runner might shut down automatically)
        if (_networkRunnerHandler != null && (_networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning))
        {
            ShowStatusMessage("Failed to start host.", true);
            await Task.Delay(2000); // Give user time to see error
            ShowPanel(_mainPanel); // Return to main panel
        }
        // On success, scene change should be handled by NetworkRunnerHandler
    }


    private void OnQuitButtonClicked() => Application.Quit();

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
        ShowStatusMessage(statusMessage, true); // Keep loading message visible
    }

    private void ShowStatusMessage(string message, bool persistent, float duration = 3f)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.gameObject.SetActive(true);
            StopAllCoroutines(); // Stop previous hide coroutine if running
            if (!persistent)
            {
                StartCoroutine(HideStatusAfterDelay(duration));
            }
        }
    }

    private void SetupBackButtons()
    {
        // Setup back button for Host Panel
        SetupBackButton(_hostPanel);
        // Setup back button for Settings Panel
        SetupBackButton(_settingsPanel);
        // Join panel back button is now handled by JoinGameUI
    }

    private void SetupBackButton(GameObject panel)
    {
        if (panel == null) return;
        // Find button named "BackButton" or "Back" (case-insensitive search might be better)
        Button backButton = panel.transform.Find("BackButton")?.GetComponent<Button>() ?? panel.transform.Find("Back")?.GetComponent<Button>();
        if (backButton != null)
        {
            SetupButton(backButton, () => ShowPanel(_mainPanel));
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