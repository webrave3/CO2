using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    // Static instance for singleton pattern
    public static GameMenuManager Instance { get; private set; }

    [Header("Menu Settings")]
    [SerializeField] private GameObject _menuPrefab; // Reference to the InGameMenu prefab
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode _menuToggleKey = KeyCode.Escape;

    // Menu panels - these will be found at runtime
    private GameObject _menuInstance;
    private GameObject _mainMenuPanel;
    private GameObject _joinGamePanel;
    private GameObject _settingsPanel;
    private GameObject _confirmationDialog;

    // Button references - these will be found at runtime
    private Button _resumeButton;
    private Button _joinGameButton;
    private Button _settingsButton;
    private Button _mainMenuButton;
    private Button _quitButton;
    private Button _confirmButton;
    private Button _cancelButton;
    private TextMeshProUGUI _confirmationText;

    // Join panel references
    private TMP_InputField _roomCodeInput;
    private Button _directJoinButton;
    private TextMeshProUGUI _statusText;
    private GameObject _joiningIndicator;

    // Private variables
    private NetworkRunnerHandler _networkHandler;
    // FIX: Converted private field to public property to be accessible by PlayerController
    public bool IsMenuActive { get; private set; } = false;
    private bool _isJoining = false;
    private CursorLockMode _previousCursorLockState;
    private bool _previousCursorVisible;

    private void Awake()
    {
        Debug.Log("[GameMenuManager] Awake called");

        // Setup singleton
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameMenuManager] Multiple instances detected - destroying duplicate");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[GameMenuManager] Initialized as singleton");
    }

    private void Start()
    {
        // Find network handler
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();
        if (_networkHandler == null)
            Debug.LogWarning("[GameMenuManager] NetworkRunnerHandler not found!");

        // Instantiate menu prefab if assigned
        if (_menuPrefab != null)
        {
            InstantiateAndSetupMenu();
        }
        else
        {
            Debug.LogError("[GameMenuManager] Menu prefab not assigned!");
        }

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void InstantiateAndSetupMenu()
    {
        Debug.Log("[GameMenuManager] Instantiating menu prefab");

        // Instantiate the menu prefab as a child of this GameObject
        _menuInstance = Instantiate(_menuPrefab, transform);

        if (_menuInstance == null)
        {
            Debug.LogError("[GameMenuManager] Failed to instantiate menu prefab!");
            return;
        }

        // Find all required components by name/type
        FindMenuComponents();

        // Set up button listeners
        SetupButtonListeners();

        // Initially hide the menu
        HideAllMenus();

        Debug.Log("[GameMenuManager] Menu setup completed successfully");

        // Log component state for debugging
        LogComponentState();
    }

    private void FindMenuComponents()
    {
        Debug.Log("[GameMenuManager] Finding menu components");

        // Find panels by name - adjust these paths to match your prefab structure
        _mainMenuPanel = FindChildByName(_menuInstance, "MainPanel");
        _joinGamePanel = FindChildByName(_menuInstance, "JoinGamePanel");
        _settingsPanel = FindChildByName(_menuInstance, "SettingsPanel");
        _confirmationDialog = FindChildByName(_menuInstance, "ConfirmationDialog");

        // Find buttons - adjust the search paths to match your prefab structure
        _resumeButton = FindButtonByName("ResumeGame", "Resume");
        _joinGameButton = FindButtonByName("JoinGame");
        _settingsButton = FindButtonByName("Settings");
        _mainMenuButton = FindButtonByName("MainMenu", "Exit");
        _quitButton = FindButtonByName("Quit");

        // Find confirmation dialog elements
        if (_confirmationDialog != null)
        {
            _confirmButton = _confirmationDialog.GetComponentInChildren<Button>();

            // If there are multiple buttons, find the correct ones
            Button[] dialogButtons = _confirmationDialog.GetComponentsInChildren<Button>();
            if (dialogButtons.Length >= 2)
            {
                _confirmButton = dialogButtons[0]; // Usually the first button
                _cancelButton = dialogButtons[1]; // Usually the second button
            }

            // Find confirmation text
            _confirmationText = _confirmationDialog.GetComponentInChildren<TextMeshProUGUI>();
        }

        // Find join panel elements if the panel exists
        if (_joinGamePanel != null)
        {
            _roomCodeInput = _joinGamePanel.GetComponentInChildren<TMP_InputField>();
            _directJoinButton = FindButtonInParent(_joinGamePanel, "DirectJoin", "Join");
            _statusText = _joinGamePanel.GetComponentInChildren<TextMeshProUGUI>();
            _joiningIndicator = FindChildByName(_joinGamePanel, "JoiningIndicator", "Loading");
        }

        // Log any missing critical components
        ValidateComponents();
    }

    private GameObject FindChildByName(GameObject parent, params string[] possibleNames)
    {
        if (parent == null) return null;

        foreach (string name in possibleNames)
        {
            // Try direct child first
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;

            // Try recursive search with partial name matching
            foreach (Transform t in parent.GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains(name))
                    return t.gameObject;
            }
        }

        return null;
    }

    private Button FindButtonByName(params string[] possibleNames)
    {
        if (_menuInstance == null) return null;

        foreach (string name in possibleNames)
        {
            Button[] buttons = _menuInstance.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name.Contains(name) ||
                    (button.GetComponentInChildren<TextMeshProUGUI>() != null &&
                     button.GetComponentInChildren<TextMeshProUGUI>().text.Contains(name)))
                {
                    return button;
                }
            }
        }

        return null;
    }

    private Button FindButtonInParent(GameObject parent, params string[] possibleNames)
    {
        if (parent == null) return null;

        foreach (string name in possibleNames)
        {
            Button[] buttons = parent.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name.Contains(name) ||
                    (button.GetComponentInChildren<TextMeshProUGUI>() != null &&
                     button.GetComponentInChildren<TextMeshProUGUI>().text.Contains(name)))
                {
                    return button;
                }
            }
        }

        return null;
    }

    private void ValidateComponents()
    {
        if (_mainMenuPanel == null)
            Debug.LogWarning("[GameMenuManager] MainMenuPanel not found!");

        if (_resumeButton == null)
            Debug.LogWarning("[GameMenuManager] ResumeButton not found!");

        if (_mainMenuButton == null)
            Debug.LogWarning("[GameMenuManager] MainMenuButton not found!");

        if (_confirmationDialog == null)
            Debug.LogWarning("[GameMenuManager] ConfirmationDialog not found!");

        if (_confirmButton == null && _confirmationDialog != null)
            Debug.LogWarning("[GameMenuManager] ConfirmButton not found in ConfirmationDialog!");
    }

    private void SetupButtonListeners()
    {
        Debug.Log("[GameMenuManager] Setting up button listeners");

        // Clear listeners first to avoid duplicates
        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveAllListeners();
            _resumeButton.onClick.AddListener(ToggleMenu);
        }

        if (_joinGameButton != null)
        {
            _joinGameButton.onClick.RemoveAllListeners();
            _joinGameButton.onClick.AddListener(ShowJoinGamePanel);
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveAllListeners();
            _settingsButton.onClick.AddListener(ShowSettingsPanel);
        }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.onClick.RemoveAllListeners();
            _mainMenuButton.onClick.AddListener(() => ShowConfirmation("Return to main menu? This will disconnect your current game.", ReturnToMainMenu));
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveAllListeners();
            _quitButton.onClick.AddListener(() => ShowConfirmation("Are you sure you want to quit the game?", QuitGame));
        }

        // Setup confirmation dialog buttons
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            // Action will be assigned dynamically in ShowConfirmation
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(HideConfirmation);
        }

        // Join game panel buttons
        if (_directJoinButton != null)
        {
            _directJoinButton.onClick.RemoveAllListeners();
            _directJoinButton.onClick.AddListener(JoinGameByCode);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameMenuManager] Scene loaded: {scene.name}");

        // Find network handler in the new scene if needed
        if (_networkHandler == null)
            _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Hide menu on scene change
        HideAllMenus();
        // FIX: Use the new public property
        IsMenuActive = false;

        // Handle cursor based on scene
        if (scene.name == _mainMenuSceneName)
        {
            SetCursorState(CursorLockMode.None, true);
        }
        else
        {
            SetCursorState(CursorLockMode.Locked, false);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        // Don't toggle menu in main menu scene
        if (SceneManager.GetActiveScene().name == _mainMenuSceneName)
            return;

        // Toggle menu with Escape key
        if (Input.GetKeyDown(_menuToggleKey))
        {
            Debug.Log($"[GameMenuManager] Menu toggle key {_menuToggleKey} pressed");
            ToggleMenu();
        }

        // CRITICAL FIX: Explicitly handle Escape to prevent disconnection
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[GameMenuManager] Escape key intercepted");
            // Show menu instead of disconnecting
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        // FIX: Use the new public property
        IsMenuActive = !IsMenuActive;
        Debug.Log($"[GameMenuManager] Toggling menu, active: {IsMenuActive}");

        if (IsMenuActive)
        {
            // Save current cursor state
            _previousCursorLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            // CRITICAL FIX: Make sure menu panel exists and is properly shown
            if (_mainMenuPanel != null)
            {
                Debug.Log("[GameMenuManager] Activating menu panel");
                _mainMenuPanel.SetActive(true);

                // Debug log menu panel hierarchy
                Transform current = _mainMenuPanel.transform;
                string path = current.name;
                while (current.parent != null)
                {
                    current = current.parent;
                    path = current.name + "/" + path;
                }
                Debug.Log($"Menu panel hierarchy: {path}");
            }
            else
            {
                Debug.LogError("[GameMenuManager] Can't show menu - MainMenuPanel is null!");

                // Try to find it if not already found
                _mainMenuPanel = FindChildByName(_menuInstance, "MainPanel");

                if (_mainMenuPanel != null)
                {
                    _mainMenuPanel.SetActive(true);
                    Debug.Log("[GameMenuManager] Found and activated MainMenuPanel");
                }
            }

            SetCursorState(CursorLockMode.None, true);
        }
        else
        {
            // Hide all menus
            HideAllMenus();

            // Restore previous cursor state
            SetCursorState(_previousCursorLockState, _previousCursorVisible);
        }
    }

    public void ShowJoinGamePanel()
    {
        if (_joinGamePanel == null)
        {
            Debug.LogError("[GameMenuManager] JoinGamePanel is null!");
            return;
        }

        HideAllMenus();
        _joinGamePanel.SetActive(true);

        // Clear previous status
        if (_statusText != null)
            _statusText.gameObject.SetActive(false);

        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(false);

        // Clear previous room code
        if (_roomCodeInput != null)
            _roomCodeInput.text = "";
    }

    public void ShowSettingsPanel()
    {
        if (_settingsPanel == null)
        {
            Debug.LogError("[GameMenuManager] SettingsPanel is null!");
            return;
        }

        HideAllMenus();
        _settingsPanel.SetActive(true);
    }

    private void ShowConfirmation(string message, UnityEngine.Events.UnityAction onConfirm)
    {
        Debug.Log($"[GameMenuManager] ShowConfirmation called with message: {message}");

        if (_confirmationDialog == null)
        {
            Debug.LogError("[GameMenuManager] ConfirmationDialog is null!");

            // Attempt to find it if not already found
            _confirmationDialog = FindChildByName(_menuInstance, "ConfirmationDialog");

            if (_confirmationDialog == null)
            {
                Debug.LogError("[GameMenuManager] Could not find ConfirmationDialog in menu hierarchy!");
                // As a fallback, just execute the action
                onConfirm.Invoke();
                return;
            }
        }

        // Find text component if not already assigned
        if (_confirmationText == null)
        {
            _confirmationText = _confirmationDialog.GetComponentInChildren<TextMeshProUGUI>();
            if (_confirmationText == null)
                Debug.LogWarning("[GameMenuManager] Could not find text component in confirmation dialog");
        }

        // Find buttons if not already assigned
        if (_confirmButton == null || _cancelButton == null)
        {
            // Try to find with both possible names
            Button[] buttons = _confirmationDialog.GetComponentsInChildren<Button>();

            foreach (Button button in buttons)
            {
                // Check for Confirm/Yes button
                if (button.name.Contains("Confirm") || button.name.Contains("Yes"))
                    _confirmButton = button;
                // Check for Cancel/No button
                else if (button.name.Contains("Cancel") || button.name.Contains("No"))
                    _cancelButton = button;
            }

            if (_confirmButton == null)
                Debug.LogError("[GameMenuManager] Could not find confirm button in dialog!");

            if (_cancelButton == null)
                Debug.LogError("[GameMenuManager] Could not find cancel button in dialog!");
        }

        // Set confirmation message
        if (_confirmationText != null)
            _confirmationText.text = message;

        // Setup confirmation button
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() => {
                Debug.Log("[GameMenuManager] Confirm button clicked, executing callback");
                onConfirm.Invoke();
                HideConfirmation();
            });
        }
        else
        {
            Debug.LogError("[GameMenuManager] Can't set up confirm button - it's null!");
            onConfirm.Invoke(); // Fallback
            return;
        }

        // Setup cancel button
        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(() => {
                Debug.Log("[GameMenuManager] Cancel button clicked");
                HideConfirmation();
            });
        }

        // Show dialog
        _confirmationDialog.SetActive(true);
        Debug.Log("[GameMenuManager] Confirmation dialog displayed");
    }

    private void HideConfirmation()
    {
        Debug.Log("[GameMenuManager] HideConfirmation called");

        if (_confirmationDialog != null)
        {
            _confirmationDialog.SetActive(false);
            Debug.Log("[GameMenuManager] Confirmation dialog hidden");
        }
        else
        {
            Debug.LogWarning("[GameMenuManager] Cannot hide confirmation dialog - it's null!");
        }
    }

    private void HideAllMenus()
    {
        // Hide all menu panels
        if (_mainMenuPanel != null)
            _mainMenuPanel.SetActive(false);

        if (_joinGamePanel != null)
            _joinGamePanel.SetActive(false);

        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        if (_confirmationDialog != null)
            _confirmationDialog.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("[GameMenuManager] ReturnToMainMenu called");

        // Show confirmation dialog
        ShowConfirmation("Are you sure you want to return to the main menu?\nThis will disconnect you from the current game.", () => {
            Debug.Log("[GameMenuManager] User confirmed return to main menu");

            // Hide all menus including confirmation dialog
            HideAllMenus();
            // FIX: Use the new public property
            IsMenuActive = false;

            // Properly shutdown network session
            if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
            {
                Debug.Log("[GameMenuManager] Starting network disconnection process");
                StartCoroutine(DisconnectAndReturnToMenu());
            }
            else
            {
                Debug.Log("[GameMenuManager] No active network session, loading main menu directly");
                // No active session, just load menu
                SceneManager.LoadScene(_mainMenuSceneName);
            }
        });
    }

    private async void JoinGameByCode()
    {
        if (_isJoining || _networkHandler == null)
            return;

        string roomCode = _roomCodeInput != null ? _roomCodeInput.text.Trim() : "";

        if (string.IsNullOrEmpty(roomCode))
        {
            ShowStatusMessage("Please enter a room code", Color.red);
            return;
        }

        _isJoining = true;

        // Show joining indicator
        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(true);

        if (_directJoinButton != null)
            _directJoinButton.interactable = false;

        ShowStatusMessage($"Joining room: {roomCode}...", Color.white);

        try
        {
            // Try to join with the room code
            await _networkHandler.StartClientGameByHash(roomCode);

            // If join was successful, hide menus
            if (_networkHandler.Runner != null && _networkHandler.Runner.IsRunning &&
                !string.IsNullOrEmpty(_networkHandler.SessionUniqueID))
            {
                // Hide all menu UI
                HideAllMenus();
                // FIX: Use the new public property
                IsMenuActive = false;

                // Lock cursor for gameplay
                SetCursorState(CursorLockMode.Locked, false);
            }
            else
            {
                // Show error and reset UI
                ShowStatusMessage("Failed to find game with that code", Color.red);
                ResetJoiningState();
            }
        }
        catch (System.Exception ex)
        {
            // Show error and reset UI
            ShowStatusMessage($"Error joining: {ex.Message}", Color.red);
            ResetJoiningState();
        }
    }

    private void ResetJoiningState()
    {
        _isJoining = false;

        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(false);

        if (_directJoinButton != null)
            _directJoinButton.interactable = true;
    }

    private void ShowStatusMessage(string message, Color color)
    {
        if (_statusText == null)
        {
            Debug.LogWarning("[GameMenuManager] StatusText is null!");
            return;
        }

        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);
    }

    private IEnumerator DisconnectAndReturnToMenu()
    {
        Debug.Log("[GameMenuManager] Disconnecting before returning to menu");

        // Start disconnection
        var disconnectTask = _networkHandler.ShutdownGame();

        // Wait for disconnect or timeout
        float startTime = Time.time;
        while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
        {
            yield return null;
        }

        Debug.Log("[GameMenuManager] Network disconnection complete (or timed out), loading main menu");
        // Load main menu
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void QuitGame()
    {
        Debug.Log("[GameMenuManager] Quitting game");

        // Make sure to disconnect first
        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            _networkHandler.ShutdownGame();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetCursorState(CursorLockMode lockMode, bool visible)
    {
        Cursor.lockState = lockMode;
        Cursor.visible = visible;
    }

    private void LogComponentState()
    {
        Debug.Log("===== GAME MENU MANAGER STATE =====");
        Debug.Log($"Instance valid: {Instance != null}");
        Debug.Log($"MenuPrefab assigned: {_menuPrefab != null}");
        Debug.Log($"Menu instance created: {_menuInstance != null}");
        Debug.Log($"NetworkHandler found: {_networkHandler != null}");
        Debug.Log($"MainMenuPanel found: {_mainMenuPanel != null}");
        Debug.Log($"JoinGamePanel found: {_joinGamePanel != null}");
        Debug.Log($"SettingsPanel found: {_settingsPanel != null}");
        Debug.Log($"ConfirmationDialog found: {_confirmationDialog != null}");
        Debug.Log($"ResumeButton found: {_resumeButton != null}");
        Debug.Log($"MainMenuButton found: {_mainMenuButton != null}");
        Debug.Log($"ConfirmButton found: {_confirmButton != null}");
        Debug.Log($"CancelButton found: {_cancelButton != null}");
        // FIX: Use the new public property
        Debug.Log($"IsMenuActive: {IsMenuActive}");
        Debug.Log("===================================");
    }
}