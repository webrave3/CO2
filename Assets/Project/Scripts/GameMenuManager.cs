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
    [SerializeField] private GameObject _menuPrefab;
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode _menuToggleKey = KeyCode.Escape;

    // Menu Panels
    [Header("Menu Panels")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _joinGamePanel;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _confirmationDialog;

    // Button references
    [Header("Button References")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _joinGameButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _quitButton;

    // Join Game panel references
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _directJoinButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private GameObject _joiningIndicator;

    // Confirmation dialog references
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TextMeshProUGUI _confirmationText;

    // Private variables
    private GameObject _menuInstance;
    private NetworkRunnerHandler _networkHandler;
    private bool _isMenuActive = false;
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

        // Create menu if it doesn't exist
        if (_menuInstance == null && _menuPrefab != null)
        {
            _menuInstance = Instantiate(_menuPrefab);
            _menuInstance.transform.SetParent(transform);

            // Find references if not already assigned
            FindMenuReferences();
            SetupButtonListeners();

            // Initially hide menu
            HideAllMenus();
        }

        Debug.Log("GameMenuManager initialized");
    }

    private void Start()
    {
        // Find network handler
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find network handler in the new scene
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Hide menu on scene change
        HideAllMenus();
        _isMenuActive = false;

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

    private void Update()
    {
        // Don't toggle menu in main menu scene
        if (SceneManager.GetActiveScene().name == _mainMenuSceneName)
            return;

        // Toggle menu with Escape key
        if (Input.GetKeyDown(_menuToggleKey))
        {
            ToggleMenu();
        }
    }

    private void FindMenuReferences()
    {
        if (_menuInstance == null) return;

        // Find all menu panels if not already assigned
        if (_mainMenuPanel == null)
            _mainMenuPanel = FindChildObject(_menuInstance.transform, "MainMenuPanel");

        if (_joinGamePanel == null)
            _joinGamePanel = FindChildObject(_menuInstance.transform, "JoinGamePanel");

        if (_settingsPanel == null)
            _settingsPanel = FindChildObject(_menuInstance.transform, "SettingsPanel");

        if (_confirmationDialog == null)
            _confirmationDialog = FindChildObject(_menuInstance.transform, "ConfirmationDialog");

        // Find button references if not already assigned
        if (_resumeButton == null)
            _resumeButton = FindButtonInChildren(_menuInstance.transform, "ResumeButton");

        if (_joinGameButton == null)
            _joinGameButton = FindButtonInChildren(_menuInstance.transform, "JoinGameButton");

        if (_settingsButton == null)
            _settingsButton = FindButtonInChildren(_menuInstance.transform, "SettingsButton");

        if (_mainMenuButton == null)
            _mainMenuButton = FindButtonInChildren(_menuInstance.transform, "MainMenuButton");

        if (_quitButton == null)
            _quitButton = FindButtonInChildren(_menuInstance.transform, "QuitButton");
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
            // Action will be assigned dynamically
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

    private GameObject FindChildObject(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child.gameObject;

        // If not found directly, search recursively
        foreach (Transform t in parent)
        {
            GameObject found = FindChildObject(t, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Button FindButtonInChildren(Transform parent, string buttonName)
    {
        // First try direct lookup
        Transform buttonTransform = parent.Find(buttonName);
        if (buttonTransform != null)
            return buttonTransform.GetComponent<Button>();

        // Try recursive lookup with partial name match
        Button[] allButtons = parent.GetComponentsInChildren<Button>(true);
        foreach (Button button in allButtons)
        {
            if (button.name.Contains(buttonName))
                return button;
        }

        return null;
    }

    public void ToggleMenu()
    {
        _isMenuActive = !_isMenuActive;

        if (_isMenuActive)
        {
            // Save current cursor state
            _previousCursorLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            // Show main menu panel and unlock cursor
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);

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
        if (_joinGamePanel == null) return;

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
        if (_settingsPanel == null) return;

        HideAllMenus();
        _settingsPanel.SetActive(true);
    }

    private void ShowConfirmation(string message, UnityEngine.Events.UnityAction onConfirm)
    {
        if (_confirmationDialog == null) return;

        // Set confirmation message
        if (_confirmationText != null)
            _confirmationText.text = message;

        // Setup confirmation button
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() => {
                onConfirm.Invoke();
                HideConfirmation();
            });
        }

        // Show dialog
        _confirmationDialog.SetActive(true);
    }

    private void HideConfirmation()
    {
        if (_confirmationDialog != null)
            _confirmationDialog.SetActive(false);
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
        HideAllMenus();
        _isMenuActive = false;

        // Properly shutdown network session
        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            StartCoroutine(DisconnectAndReturnToMenu());
        }
        else
        {
            // No active session, just load menu
            SceneManager.LoadScene(_mainMenuSceneName);
        }
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
                _isMenuActive = false;

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
        if (_statusText == null) return;

        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);
    }

    private IEnumerator DisconnectAndReturnToMenu()
    {
        Debug.Log("Disconnecting before returning to menu");

        // Start disconnection
        var disconnectTask = _networkHandler.ShutdownGame();

        // Wait for disconnect or timeout
        float startTime = Time.time;
        while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
        {
            yield return null;
        }

        // Load main menu
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void QuitGame()
    {
        Debug.Log("Quitting game");

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
}