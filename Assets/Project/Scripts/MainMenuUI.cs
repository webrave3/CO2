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
    [SerializeField] private TextMeshProUGUI _statusText; // Still useful for loading/error messages

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

    [Header("Navigation Buttons")] // Keep if used for panel switching
    [SerializeField] private Button _hostGameNavButton;
    [SerializeField] private Button _joinGameNavButton;
    [SerializeField] private Button _settingsNavButton;

    [Header("Region Selection")]
    [SerializeField] private TMP_Dropdown _regionDropdown;
    [SerializeField] private bool _useAllRegions = true;
    // Removed: [SerializeField] private Button _debugButton;

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
        // --- ADDED DEBUG LOG ---
        Debug.Log("MainMenuUI Start: Starting...");
        // --- END ADDED DEBUG LOG ---

        // Get NetworkRunnerHandler - first try from BootstrapManager
        if (BootstrapManager.Instance != null)
        {
            _networkRunnerHandler = BootstrapManager.Instance.GetNetworkRunnerHandler();
        }

        // If still null, try direct lookup
        if (_networkRunnerHandler == null)
        {
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        }

        // --- ADDED NULL CHECK LOGS ---
        if (_networkRunnerHandler == null)
        {
            Debug.LogError("MainMenuUI Start: NetworkRunnerHandler NOT FOUND after checking BootstrapManager and FindObjectOfType!");
            // Optionally disable network-dependent UI elements here
            ShowStatusMessage("Network Error: Handler not found.", true); // Show persistent error
            if (_hostButton != null) _hostButton.interactable = false;
            if (_joinButton != null) _joinButton.interactable = false;
            if (_showBrowserButton != null) _showBrowserButton.interactable = false;
            if (_directJoinButton != null) _directJoinButton.interactable = false;
        }
        else
        {
            Debug.Log("MainMenuUI Start: NetworkRunnerHandler found successfully.");
        }
        // --- END ADDED NULL CHECK LOGS ---

        // Initialize region dropdown
        InitializeRegionDropdown();

        // Set default session name
        if (_sessionNameInput != null && string.IsNullOrEmpty(_sessionNameInput.text))
        {
            _sessionNameInput.text = _defaultSessionName;
        }

        // Set default player name if not set, load saved name if available
        if (_playerNameInput != null)
        {
            if (PlayerPrefs.HasKey("PlayerName"))
            {
                _playerNameInput.text = PlayerPrefs.GetString("PlayerName");
            }
            else if (string.IsNullOrEmpty(_playerNameInput.text))
            {
                _playerNameInput.text = "Player" + UnityEngine.Random.Range(1000, 9999);
            }
        }

        // Set up UI callbacks - Clear all listeners first to avoid duplicates
        SetupButton(_hostButton, () => ShowPanel(_hostPanel));
        SetupButton(_joinButton, () => {
            ShowPanel(_joinGamePanel);
            if (_roomBrowserUI != null) _roomBrowserUI.ShowRoomBrowser();
        });
        SetupButton(_settingsButton, () => ShowPanel(_settingsPanel));
        SetupButton(_quitButton, OnQuitButtonClicked);

        if (_showBrowserButton != null) // Keep separate if it has unique logic
        {
            _showBrowserButton.onClick.RemoveAllListeners();
            _showBrowserButton.onClick.AddListener(() => {
                ShowPanel(_joinGamePanel);
                if (_roomBrowserUI != null) _roomBrowserUI.ShowRoomBrowser();
            });
        }

        if (_directJoinButton != null)
        {
            _directJoinButton.onClick.RemoveAllListeners();
            _directJoinButton.onClick.AddListener(OnDirectJoinButtonClicked);
        }

        // Host Panel-specific buttons
        if (_hostPanel != null)
        {
            Button hostStartButton = _hostPanel.transform.Find("StartHostButton")?.GetComponent<Button>(); // Example name
            if (hostStartButton == null) hostStartButton = _hostPanel.GetComponentInChildren<Button>(); // Fallback

            if (hostStartButton != null)
            {
                hostStartButton.onClick.RemoveAllListeners();
                hostStartButton.onClick.AddListener(OnHostGameStartClicked);
            }
        }

        // Ensure main panel is active at start
        ShowPanel(_mainPanel);

        // Set up back buttons
        SetupBackButtons();
    }

    private void InitializeRegionDropdown()
    {
        if (_regionDropdown != null)
        {
            _regionDropdown.ClearOptions();
            _regionDropdown.AddOptions(_regionCodes.Keys.ToList());
            _regionDropdown.value = 0; // Default to "Auto (Best)"
        }
    }

    // Public method if needed by other scripts
    public void ShowHostGamePanel()
    {
        ShowPanel(_hostPanel);
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    public void ShowMainPanel()
    {
        ShowPanel(_mainPanel);
    }

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

    private async void OnHostGameStartClicked()
    {
        if (_networkRunnerHandler == null)
        {
            ShowStatusMessage("Network Error. Cannot host.", true);
            return; // Don't proceed if network handler isn't ready
        }

        SavePlayerName();
        ShowLoadingUI("Starting host...");

        string sessionName = (_sessionNameInput != null && !string.IsNullOrEmpty(_sessionNameInput.text))
            ? _sessionNameInput.text : _defaultSessionName;

        string selectedRegion = "best"; // Default
        if (_regionDropdown != null && _regionDropdown.value >= 0 && _regionDropdown.value < _regionCodes.Count)
        {
            selectedRegion = _regionCodes.ElementAt(_regionDropdown.value).Value;
        }

        // Start host using the updated method with region
        await _networkRunnerHandler.StartHostGame(sessionName, selectedRegion, _useAllRegions);

        // If StartHostGame fails, the runner might shut down, triggering scene change back here.
        // We might need a check here if the runner is still running after await.
        if (_networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning)
        {
            ShowStatusMessage("Failed to start host.", true); // Show persistent error
            await Task.Delay(2000); // Give user time to see error
            ShowPanel(_mainPanel); // Return to main panel
        }
    }

    private async void OnDirectJoinButtonClicked()
    {
        // --- ADDED DEBUG LOGS ---
        Debug.Log($"MainMenuUI OnDirectJoinButtonClicked: Trying to join. _networkRunnerHandler is null? {_networkRunnerHandler == null}");

        if (_networkRunnerHandler == null)
        {
            Debug.LogError("Cannot Join: _networkRunnerHandler is null right before trying to join!");
            ShowStatusMessage("Network Error. Cannot join.", true);
            // Optionally, try finding it again just in case, though this indicates an underlying initialization order problem
            // _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
            // if (_networkRunnerHandler == null)
            // {
            //     Debug.LogError("Still NULL after trying FindObjectOfType again!");
            //     return; // Still not found, exit.
            // }
            // Debug.LogWarning("Found NetworkRunnerHandler dynamically inside OnDirectJoinButtonClicked. Check initialization order!");
            return; // Exit if null after initial check
        }
        // --- END ADDED DEBUG LOGS ---

        SavePlayerName();
        ShowLoadingUI("Joining game by code...");

        string roomCode = (_roomCodeInput != null && !string.IsNullOrEmpty(_roomCodeInput.text))
            ? _roomCodeInput.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(roomCode))
        {
            ShowStatusMessage("Invalid room code. Please try again.", false); // Show temporary error
            await Task.Delay(2000);
            ShowPanel(_joinGamePanel); // Return to join panel
            return;
        }

        // Join game by hash
        await _networkRunnerHandler.StartClientGameByHash(roomCode);

        // Similar to host, check if join failed
        // Note: Runner might become null if Shutdown occurs within StartClientGameByHash on failure
        if (_networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning)
        {
            ShowStatusMessage("Failed to join game with that code.", false); // Show temporary error
            await Task.Delay(2000);
            ShowPanel(_joinGamePanel); // Return to join panel
        }
    }

    private void OnQuitButtonClicked()
    {
        Application.Quit();
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
        ShowStatusMessage(statusMessage, true); // Keep loading message visible
    }

    private void ShowStatusMessage(string message, bool persistent)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.gameObject.SetActive(true);
            StopAllCoroutines(); // Stop previous hide coroutine if running
            if (!persistent)
            {
                StartCoroutine(HideStatusAfterDelay(3f)); // Hide after 3 seconds if not persistent
            }
        }
    }

    private void SetupBackButtons()
    {
        SetupBackButton(_hostPanel);
        SetupBackButton(_settingsPanel);
        // JoinGamePanel back button might be part of RoomBrowserUI
        Button joinBack = _joinGamePanel?.transform.Find("Back")?.GetComponent<Button>();
        if (joinBack != null && (_roomBrowserUI == null || _roomBrowserUI.enabled == false)) // Only set up if RoomBrowser doesn't handle it
        {
            SetupButton(joinBack, () => ShowPanel(_mainPanel));
        }
    }

    private void SetupBackButton(GameObject panel)
    {
        if (panel == null) return;
        Button backButton = panel.transform.Find("Back")?.GetComponent<Button>(); // Assuming name is "Back"
        if (backButton != null)
        {
            SetupButton(backButton, () => ShowPanel(_mainPanel));
        }
    }

    // Removed DebugRoomDiscovery method

    private System.Collections.IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null)
        {
            _statusText.gameObject.SetActive(false);
        }
    }
}