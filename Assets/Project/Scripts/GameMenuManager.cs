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
    public bool IsMenuActive { get; private set; } = false;
    private bool _isJoining = false;
    private CursorLockMode _previousCursorLockState;
    private bool _previousCursorVisible;

    private void Awake()
    {
        // Setup singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Find network handler
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Instantiate menu prefab if assigned
        if (_menuPrefab != null)
        {
            InstantiateAndSetupMenu();
        }

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void InstantiateAndSetupMenu()
    {
        // Instantiate the menu prefab as a child of this GameObject
        _menuInstance = Instantiate(_menuPrefab, transform);

        if (_menuInstance == null)
        {
            return;
        }

        // Find all required components by name/type
        FindMenuComponents();

        // Set up button listeners
        SetupButtonListeners();

        // Initially hide the menu
        HideAllMenus();
    }

    private void FindMenuComponents()
    {
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
            Button[] dialogButtons = _confirmationDialog.GetComponentsInChildren<Button>();
            if (dialogButtons.Length >= 2)
            {
                _confirmButton = dialogButtons[0]; // Assume first is confirm
                _cancelButton = dialogButtons[1]; // Assume second is cancel
            }
            else if (dialogButtons.Length == 1)
            {
                _confirmButton = dialogButtons[0]; // Only confirm found
            }
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
    }

    private GameObject FindChildByName(GameObject parent, params string[] possibleNames)
    {
        if (parent == null) return null;

        foreach (string name in possibleNames)
        {
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;

            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true)) // Include inactive
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
            Button[] buttons = _menuInstance.GetComponentsInChildren<Button>(true); // Include inactive
            foreach (Button button in buttons)
            {
                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (button.name.Contains(name) || (buttonText != null && buttonText.text.Contains(name)))
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
            Button[] buttons = parent.GetComponentsInChildren<Button>(true); // Include inactive
            foreach (Button button in buttons)
            {
                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (button.name.Contains(name) || (buttonText != null && buttonText.text.Contains(name)))
                {
                    return button;
                }
            }
        }
        return null;
    }

    private void SetupButtonListeners()
    {
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
            _mainMenuButton.onClick.AddListener(() => ShowConfirmation("Return to main menu? This will disconnect your current game.", ReturnToMainMenuAction));
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
        // Find network handler in the new scene if needed
        if (_networkHandler == null)
            _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Hide menu on scene change
        HideAllMenus();
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

        // Toggle menu with specified key
        if (Input.GetKeyDown(_menuToggleKey))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        IsMenuActive = !IsMenuActive;

        if (IsMenuActive)
        {
            _previousCursorLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(true);
            }
            else
            {
                // Try to find it again if null
                _mainMenuPanel = FindChildByName(_menuInstance, "MainPanel");
                if (_mainMenuPanel != null)
                {
                    _mainMenuPanel.SetActive(true);
                }
            }
            SetCursorState(CursorLockMode.None, true);
        }
        else
        {
            HideAllMenus();
            SetCursorState(_previousCursorLockState, _previousCursorVisible);
        }
    }

    public void ShowJoinGamePanel()
    {
        if (_joinGamePanel == null) return;
        HideAllMenus();
        _joinGamePanel.SetActive(true);

        if (_statusText != null) _statusText.gameObject.SetActive(false);
        if (_joiningIndicator != null) _joiningIndicator.SetActive(false);
        if (_roomCodeInput != null) _roomCodeInput.text = "";
    }

    public void ShowSettingsPanel()
    {
        if (_settingsPanel == null) return;
        HideAllMenus();
        _settingsPanel.SetActive(true);
    }

    private void ShowConfirmation(string message, UnityEngine.Events.UnityAction onConfirm)
    {
        if (_confirmationDialog == null)
        {
            _confirmationDialog = FindChildByName(_menuInstance, "ConfirmationDialog");
            if (_confirmationDialog == null)
            {
                // Fallback: execute immediately if dialog not found
                onConfirm.Invoke();
                return;
            }
        }

        // Find components if they weren't found initially
        if (_confirmationText == null) _confirmationText = _confirmationDialog.GetComponentInChildren<TextMeshProUGUI>();
        if (_confirmButton == null || _cancelButton == null)
        {
            Button[] buttons = _confirmationDialog.GetComponentsInChildren<Button>();
            // Simple assignment assuming order or specific names if needed
            if (buttons.Length >= 2) { _confirmButton = buttons[0]; _cancelButton = buttons[1]; }
            else if (buttons.Length == 1) { _confirmButton = buttons[0]; }
        }


        if (_confirmationText != null) _confirmationText.text = message;

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() => {
                onConfirm.Invoke();
                HideConfirmation();
            });
        }
        else
        {
            // Fallback if confirm button still not found
            onConfirm.Invoke();
            return;
        }


        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(HideConfirmation);
        }

        _confirmationDialog.SetActive(true);
    }

    private void HideConfirmation()
    {
        if (_confirmationDialog != null)
        {
            _confirmationDialog.SetActive(false);
        }
    }

    private void HideAllMenus()
    {
        if (_mainMenuPanel != null) _mainMenuPanel.SetActive(false);
        if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_confirmationDialog != null) _confirmationDialog.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        ShowConfirmation("Are you sure you want to return to the main menu?\nThis will disconnect you from the current game.", ReturnToMainMenuAction);
    }

    private void ReturnToMainMenuAction()
    {
        HideAllMenus();
        IsMenuActive = false;

        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            StartCoroutine(DisconnectAndReturnToMenu());
        }
        else
        {
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }

    private async void JoinGameByCode()
    {
        if (_isJoining || _networkHandler == null) return;

        string roomCode = _roomCodeInput != null ? _roomCodeInput.text.Trim() : "";

        if (string.IsNullOrEmpty(roomCode))
        {
            ShowStatusMessage("Please enter a room code", Color.red);
            return;
        }

        _isJoining = true;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(true);
        if (_directJoinButton != null) _directJoinButton.interactable = false;
        ShowStatusMessage($"Joining room: {roomCode}...", Color.white);

        try
        {
            await _networkHandler.StartClientGameByHash(roomCode);

            if (_networkHandler.Runner != null && _networkHandler.Runner.IsRunning && !string.IsNullOrEmpty(_networkHandler.SessionUniqueID))
            {
                HideAllMenus();
                IsMenuActive = false;
                SetCursorState(CursorLockMode.Locked, false);
            }
            else
            {
                ShowStatusMessage("Failed to find game with that code", Color.red);
                ResetJoiningState();
            }
        }
        catch (System.Exception ex)
        {
            ShowStatusMessage($"Error joining: {ex.Message}", Color.red);
            ResetJoiningState();
        }
    }

    private void ResetJoiningState()
    {
        _isJoining = false;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(false);
        if (_directJoinButton != null) _directJoinButton.interactable = true;
    }

    private void ShowStatusMessage(string message, Color color)
    {
        if (_statusText == null) return;
        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);
    }

    private IEnumerator DisconnectAndReturnToMenu()
    {
        if (_networkHandler != null)
        {
            var disconnectTask = _networkHandler.ShutdownGame();
            float startTime = Time.time;
            while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
            {
                yield return null;
            }
        }
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void QuitGame()
    {
        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            _networkHandler.ShutdownGame(); // Fire and forget is okay for quitting
        }
        Application.Quit();
    }

    private void SetCursorState(CursorLockMode lockMode, bool visible)
    {
        Cursor.lockState = lockMode;
        Cursor.visible = visible;
    }
}