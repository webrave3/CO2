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
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Host Game Settings")]
    [SerializeField] private TMP_InputField _hostSessionNameInput;
    [SerializeField] private Toggle _isPublicToggle;
    [SerializeField] private TMP_Dropdown _hostLanguageDropdown; // Make sure this is assigned in Inspector
    [SerializeField] private TMP_Dropdown _hostRegionDropdown; // Use this for region selection (MATCHES NetworkRunnerHandler GetPhotonRegionCode!)

    [Header("Join Game UI Reference")]
    [SerializeField] private JoinGameUI _joinGameUI;

    [Header("Settings")]
    [SerializeField] private string _defaultSessionName = "GameSession";

    [Header("Panels")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _hostPanel;
    [SerializeField] private GameObject _joinGamePanel;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _loadingPanel;

    // Removed Region Dictionary - Logic moved to NetworkRunnerHandler

    private NetworkRunnerHandler _networkRunnerHandler;

    void Start()
    {
        Debug.Log("MainMenuUI Start: Starting...");

        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>(); // Prefer direct find in scene

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("MainMenuUI Start: NetworkRunnerHandler NOT FOUND!");
            ShowStatusMessage("Network Error: Handler not found.", Color.red, true);
            if (_hostButton != null) _hostButton.interactable = false;
            if (_joinButton != null) _joinButton.interactable = false;
        }
        else
        {
            Debug.Log("MainMenuUI Start: NetworkRunnerHandler found successfully.");
        }

        // --- Initialize Host Dropdowns ---
        InitializeDropdown(_hostRegionDropdown, new List<string> { "NA East", "EU", "Asia" }); // Match NetworkRunnerHandler.GetPhotonRegionCode cases
        InitializeDropdown(_hostLanguageDropdown, new List<string> { "English", "Estonian" }); // Example languages

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
            ShowPanel(_joinGamePanel);
            if (_joinGameUI != null)
            {
                _joinGameUI.ShowJoinPanel(); // Call the specific show method on JoinGameUI
            }
            else { Debug.LogError("MainMenuUI: _joinGameUI reference is NULL!"); }
        });
        SetupButton(_settingsButton, () => ShowPanel(_settingsPanel));
        SetupButton(_quitButton, OnQuitButtonClicked);

        // Host Panel - Find and Setup Start Button
        if (_hostPanel != null)
        {
            Button hostStartButton = _hostPanel.transform.Find("StartHostButton")?.GetComponent<Button>();
            if (hostStartButton == null) hostStartButton = _hostPanel.GetComponentInChildren<Button>(true); // Fallback search

            if (hostStartButton != null)
            {
                SetupButton(hostStartButton, OnHostGameStartClicked);
                Debug.Log("Host Start Button listener attached.");
            }
            else { Debug.LogError("Could not find StartHostButton in the Host Panel!"); }
        }

        ShowPanel(_mainPanel); // Start on main panel
        SetupBackButtons();
        if (_statusText != null) _statusText.gameObject.SetActive(false); // Hide status initially
    }

    private void InitializeDropdown(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown != null)
        {
            dropdown.ClearOptions();
            if (options != null && options.Count > 0)
            {
                dropdown.AddOptions(options);
                dropdown.value = 0; // Default to first option
            }
        }
    }


    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    public void ShowMainPanel() => ShowPanel(_mainPanel);
    public void ShowHostGamePanel() => ShowPanel(_hostPanel);

    public void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow == null) return;

        // Disable all known panels
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_hostPanel != null) _hostPanel.SetActive(false);
        if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_loadingPanel != null) _loadingPanel.SetActive(false);

        // Enable the requested one
        panelToShow.SetActive(true);
    }

    private async void OnHostGameStartClicked()
    {
        if (_networkRunnerHandler == null)
        {
            ShowStatusMessage("Network Error. Cannot host.", Color.red, true);
            return;
        }

        SavePlayerName();
        ShowLoadingUI("Starting game...");

        string sessionName = (_hostSessionNameInput != null && !string.IsNullOrEmpty(_hostSessionNameInput.text))
            ? _hostSessionNameInput.text : _defaultSessionName;

        bool isVisible = _isPublicToggle != null ? _isPublicToggle.isOn : true;

        // --- Get Region and Language from Dropdowns ---
        string selectedRegionText = "best"; // Default if dropdown missing
        if (_hostRegionDropdown != null && _hostRegionDropdown.options.Count > 0)
        {
            selectedRegionText = _hostRegionDropdown.options[_hostRegionDropdown.value].text;
        }

        string selectedLanguage = "English"; // Default if dropdown missing
        if (_hostLanguageDropdown != null && _hostLanguageDropdown.options.Count > 0)
        {
            selectedLanguage = _hostLanguageDropdown.options[_hostLanguageDropdown.value].text;
        }

        // --- Prepare Custom Session Properties ---
        var customSessionProperties = new Dictionary<string, SessionProperty>();
        // Add any *other* custom properties here if needed (language is now a direct parameter)
        // customSessionProperties["difficulty"] = "Normal"; // Example

        // --- CORRECTED Call to StartHostGame ---
        // Pass all parameters explicitly matching the NetworkRunnerHandler method signature
        await _networkRunnerHandler.StartHostGame(
            sessionNameBase: sessionName,
            isVisible: isVisible,
            regionText: selectedRegionText, // Pass region text
            language: selectedLanguage,     // Pass language string
            customProps: customSessionProperties
        );

        // Check if starting failed (Runner might be null or not running after await)
        // Check this condition *after* the await completes
        if (_networkRunnerHandler == null || _networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning)
        {
            // Only update UI if this component is still active
            if (this != null && gameObject.activeInHierarchy)
            {
                ShowStatusMessage("Failed to start host.", Color.red, false); // Show error briefly
                // No Task.Delay needed here, HideStatusAfterDelay handles it
                ShowPanel(_mainPanel); // Return to main panel
            }
        }
        // Success case: NetworkRunnerHandler handles scene change
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
        ShowStatusMessage(statusMessage, Color.white, true); // Keep loading message visible
    }

    // Overload for persistent message
    private void ShowStatusMessage(string message, Color color, bool persistent)
    {
        ShowStatusMessage(message, color, persistent ? -1f : 3f); // Use negative duration for persistent
    }

    private void ShowStatusMessage(string message, Color color, float duration = 3f)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.color = color;
            _statusText.gameObject.SetActive(true);
            StopAllCoroutines(); // Stop previous hide coroutine
            if (duration > 0) // Only start hide coroutine if duration is positive
            {
                StartCoroutine(HideStatusAfterDelay(duration));
            }
        }
    }


    private void SetupBackButtons()
    {
        SetupBackButton(_hostPanel);
        SetupBackButton(_settingsPanel);
        // Join panel back button is handled by JoinGameUI
    }

    private void SetupBackButton(GameObject panel)
    {
        if (panel == null) return;
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