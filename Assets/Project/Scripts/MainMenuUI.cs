// Filename: MainMenuUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Keep for StringComparison
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Fusion;
// *** REMOVED Photon.Realtime ***

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
    [SerializeField] private TMP_InputField _hostPasswordInput; // Keep this
    [SerializeField] private TMP_Dropdown _hostLanguageDropdown;
    [SerializeField] private TMP_Dropdown _hostRegionDropdown;

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

    private NetworkRunnerHandler _networkRunnerHandler;
    private List<string> _languageOptions = new List<string> { "Any", "English", "Spanish", "French", "German", "Chinese", "Russian" };


    void Start()
    {
        Debug.Log("MainMenuUI Start: Starting...");
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        if (_networkRunnerHandler == null)
        {
            Debug.LogError("NetworkRunnerHandler NOT FOUND!");
            ShowStatusMessage("Network Error: Handler not found.", Color.red, true);
            if (_hostButton != null) _hostButton.interactable = false;
            if (_joinButton != null) _joinButton.interactable = false;
            return;
        }

        // Initialize Dropdowns using static helpers from NetworkRunnerHandler
        List<string> regionNames = NetworkRunnerHandler.GetRegionNames();
        // Ensure "Any" is first, replacing "Best" if necessary for UI clarity
        if (!regionNames.Contains("Any", StringComparer.OrdinalIgnoreCase))
        {
            regionNames.RemoveAll(r => r.Equals("Best", StringComparison.OrdinalIgnoreCase));
            regionNames.Insert(0, "Any");
        }
        else
        {
            // If "Any" exists, make sure "Best" isn't also shown if it means the same thing
            regionNames.RemoveAll(r => r.Equals("Best", StringComparison.OrdinalIgnoreCase));
            if (!regionNames[0].Equals("Any", StringComparison.OrdinalIgnoreCase))
            { // Ensure Any is still first
                regionNames.RemoveAll(r => r.Equals("Any", StringComparison.OrdinalIgnoreCase));
                regionNames.Insert(0, "Any");
            }
        }

        InitializeDropdown(_hostRegionDropdown, regionNames);
        InitializeDropdown(_hostLanguageDropdown, _languageOptions);

        if (_hostSessionNameInput != null && string.IsNullOrEmpty(_hostSessionNameInput.text)) _hostSessionNameInput.text = _defaultSessionName;
        if (_playerNameInput != null) _playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));

        SetupButton(_hostButton, () => ShowPanel(_hostPanel));
        SetupButton(_joinButton, () => { ShowPanel(_joinGamePanel); _joinGameUI?.ShowJoinPanel(); });
        SetupButton(_settingsButton, () => ShowPanel(_settingsPanel));
        SetupButton(_quitButton, OnQuitButtonClicked);

        if (_hostPanel != null)
        {
            Button hostStartButton = _hostPanel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name.Contains("StartHostButton"));
            if (hostStartButton != null) SetupButton(hostStartButton, OnHostGameStartClicked); else Debug.LogError("Host Start Button not found!");
            // Password visibility toggle setup
            if (_isPublicToggle != null && _hostPasswordInput != null)
            {
                _isPublicToggle.onValueChanged.AddListener(UpdatePasswordVisibility);
                UpdatePasswordVisibility(_isPublicToggle.isOn); // Initial state
            }
        }
        ShowPanel(_mainPanel);
        SetupBackButtons();
        if (_statusText != null) _statusText.gameObject.SetActive(false);
    }

    private void UpdatePasswordVisibility(bool isPublic)
    {
        if (_hostPasswordInput != null)
        {
            _hostPasswordInput.gameObject.SetActive(!isPublic);
            if (isPublic) _hostPasswordInput.text = ""; // Clear password if public
        }
    }

    private void InitializeDropdown(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown != null)
        {
            dropdown.ClearOptions();
            if (options != null && options.Count > 0)
            {
                dropdown.AddOptions(options);
                dropdown.value = 0; // Default to first option ("Any")
            }
        }
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
    }

    public void ShowMainPanel() => ShowPanel(_mainPanel);
    public void ShowHostGamePanel() => ShowPanel(_hostPanel);

    public void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow == null) return;
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_hostPanel != null) _hostPanel.SetActive(false);
        if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_loadingPanel != null) _loadingPanel.SetActive(false);
        panelToShow.SetActive(true);
    }

    private async void OnHostGameStartClicked()
    {
        if (_networkRunnerHandler == null) { ShowStatusMessage("Network Error.", Color.red, true); return; }

        SavePlayerName();
        ShowLoadingUI("Starting game...");

        string sessionName = _hostSessionNameInput?.text;
        if (string.IsNullOrWhiteSpace(sessionName)) sessionName = _defaultSessionName;

        bool isVisible = _isPublicToggle?.isOn ?? true;

        // Get Region Selection ("Any" maps to "Best" for the property)
        string selectedRegionText = "Best";
        if (_hostRegionDropdown != null && _hostRegionDropdown.options.Count > 0)
        {
            selectedRegionText = _hostRegionDropdown.options[_hostRegionDropdown.value].text;
            if (selectedRegionText.Equals("Any", StringComparison.OrdinalIgnoreCase)) selectedRegionText = "Best";
        }

        // Get Language (null if "Any")
        string selectedLanguage = null;
        if (_hostLanguageDropdown != null && _hostLanguageDropdown.value > 0) // Index 0 is "Any"
        {
            selectedLanguage = _hostLanguageDropdown.options[_hostLanguageDropdown.value].text;
        }

        // Get Password (only if private)
        string password = null;
        if (!isVisible && _hostPasswordInput != null && !string.IsNullOrEmpty(_hostPasswordInput.text))
        {
            password = _hostPasswordInput.text;
        }

        // Prepare Custom Session Properties Dictionary
        var customSessionProperties = new Dictionary<string, SessionProperty>();

        // Add Language property ONLY if a specific language was selected
        if (!string.IsNullOrEmpty(selectedLanguage))
        {
            customSessionProperties[NetworkRunnerHandler.SESSION_LANGUAGE_KEY] = selectedLanguage;
            Debug.Log($"[HOST] Adding Language Property: {selectedLanguage}");
        }

        // Add Region property (using the user-friendly name or "Best")
        customSessionProperties[NetworkRunnerHandler.SESSION_REGION_KEY] = selectedRegionText;
        Debug.Log($"[HOST] Adding Region Property: {selectedRegionText}");


        // Add password to custom properties IF private and password exists
        if (!isVisible && !string.IsNullOrEmpty(password))
        {
            customSessionProperties[NetworkRunnerHandler.SESSION_PASSWORD_KEY] = password;
            Debug.Log($"[HOST] Adding Password Property.");
        }

        // Call StartHostGame - Handler will read password from customProps now
        await _networkRunnerHandler.StartHostGame(
            sessionNameBase: sessionName,
            isVisible: isVisible,
            customProps: customSessionProperties // Pass the dictionary
        );

        // Check result (Handler changes scene on success)
        await Task.Delay(100); // Small delay to allow runner state update
        if (_networkRunnerHandler == null || _networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning)
        {
            // Check if component is still valid after await
            if (this != null && gameObject != null && gameObject.activeInHierarchy)
            {
                ShowStatusMessage("Failed to start host.", Color.red); ShowPanel(_mainPanel);
            }
        }
    }


    private void OnQuitButtonClicked() => Application.Quit();

    private void SavePlayerName()
    {
        if (_playerNameInput != null && !string.IsNullOrEmpty(_playerNameInput.text))
        {
            PlayerPrefs.SetString("PlayerName", _playerNameInput.text); PlayerPrefs.Save();
        }
    }

    private void ShowLoadingUI(string msg) { ShowPanel(_loadingPanel); ShowStatusMessage(msg, Color.white, true); }
    private void ShowStatusMessage(string msg, Color color, bool persistent) { ShowStatusMessage(msg, color, persistent ? -1f : 3f); }
    private void ShowStatusMessage(string msg, Color color, float duration = 3f) { if (_statusText != null) { _statusText.text = msg; _statusText.color = color; _statusText.gameObject.SetActive(true); StopAllCoroutines(); if (duration > 0) { StartCoroutine(HideStatusAfterDelay(duration)); } } }
    private void SetupBackButtons() { SetupBackButton(_hostPanel); SetupBackButton(_settingsPanel); }
    private void SetupBackButton(GameObject panel) { if (panel == null) return; Button backButton = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name.Contains("Back")); if (backButton != null) SetupButton(backButton, () => ShowPanel(_mainPanel)); }
    private System.Collections.IEnumerator HideStatusAfterDelay(float delay) { yield return new WaitForSeconds(delay); if (_statusText != null) _statusText.gameObject.SetActive(false); }
    void OnDestroy() { if (_isPublicToggle != null) _isPublicToggle.onValueChanged.RemoveListener(UpdatePasswordVisibility); }
}