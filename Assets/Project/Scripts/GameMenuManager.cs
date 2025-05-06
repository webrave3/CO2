using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    // Static instance
    public static GameMenuManager Instance { get; private set; }

    [Header("Menu Settings")]
    [SerializeField] private GameObject _menuPrefab;
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    // Private variables
    private GameObject _menuInstance;
    private bool _isMenuActive = false;
    private NetworkRunnerHandler _networkHandler;

    // Button references
    private Button _resumeButton;
    private Button _hostGameButton;
    private Button _joinGameButton;
    private Button _mainMenuButton;

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

        // Find network handler
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Create menu initially
        CreateMenu();

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
        if (_menuInstance != null)
        {
            _menuInstance.SetActive(false);
            _isMenuActive = false;
        }

        // Handle cursor
        if (scene.name == _mainMenuSceneName)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        // Don't toggle menu in main menu scene
        if (SceneManager.GetActiveScene().name == _mainMenuSceneName)
            return;

        // Toggle menu with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    private void CreateMenu()
    {
        if (_menuPrefab != null && _menuInstance == null)
        {
            // Instantiate menu
            _menuInstance = Instantiate(_menuPrefab);
            _menuInstance.transform.SetParent(transform);

            // Find buttons - using FindInChildren to be more flexible with hierarchy
            _resumeButton = FindButtonInChildren(_menuInstance.transform, "ResumeButton");
            _hostGameButton = FindButtonInChildren(_menuInstance.transform, "HostGameButton");
            _joinGameButton = FindButtonInChildren(_menuInstance.transform, "JoinGameButton");
            _mainMenuButton = FindButtonInChildren(_menuInstance.transform, "MainMenuButton");

            // Setup button listeners
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(ToggleMenu);

            if (_hostGameButton != null)
                _hostGameButton.onClick.AddListener(HostNewGame);

            if (_joinGameButton != null)
                _joinGameButton.onClick.AddListener(JoinGamePanel);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            // Hide initially
            _menuInstance.SetActive(false);
            _isMenuActive = false;
        }
    }

    // Helper to find buttons in children with partial name matching
    private Button FindButtonInChildren(Transform parent, string buttonName)
    {
        // First try exact name match
        Transform buttonTransform = parent.Find(buttonName);

        // If not found, search recursively in children
        if (buttonTransform == null)
        {
            Button[] allButtons = parent.GetComponentsInChildren<Button>(true);
            foreach (Button button in allButtons)
            {
                if (button.name.Contains(buttonName))
                    return button;
            }
            return null;
        }

        return buttonTransform.GetComponent<Button>();
    }

    public void ToggleMenu()
    {
        _isMenuActive = !_isMenuActive;

        if (_menuInstance != null)
        {
            _menuInstance.SetActive(_isMenuActive);
        }

        // Control cursor
        Cursor.lockState = _isMenuActive ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isMenuActive;
    }

    // New game functions
    public void HostNewGame()
    {
        // First disconnect from current session
        StartCoroutine(DisconnectAndHostNew());
    }

    public void JoinGamePanel()
    {
        // First disconnect from current session
        StartCoroutine(DisconnectAndJoinNew());
    }

    public void ReturnToMainMenu()
    {
        // Close menu
        if (_menuInstance != null)
            _menuInstance.SetActive(false);

        _isMenuActive = false;

        // If we have a network connection, close it
        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            StartCoroutine(DisconnectAndLoadMenu());
        }
        else
        {
            // No connection, just load menu
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }

    private IEnumerator DisconnectAndLoadMenu()
    {
        Debug.Log("Disconnecting before returning to menu");

        // Show some loading UI here if needed

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

    private IEnumerator DisconnectAndHostNew()
    {
        Debug.Log("Disconnecting before hosting new game");

        // Hide menu
        if (_menuInstance != null)
            _menuInstance.SetActive(false);

        _isMenuActive = false;

        // Disconnect if connected
        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            var disconnectTask = _networkHandler.ShutdownGame();

            // Wait for disconnect or timeout
            float startTime = Time.time;
            while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
            {
                yield return null;
            }
        }

        // Go to main menu then immediately host
        SceneManager.LoadScene(_mainMenuSceneName);

        // Set a flag to automatically host when loaded
        PlayerPrefs.SetInt("AutoHost", 1);
        PlayerPrefs.Save();
    }

    private IEnumerator DisconnectAndJoinNew()
    {
        Debug.Log("Disconnecting before joining new game");

        // Hide menu
        if (_menuInstance != null)
            _menuInstance.SetActive(false);

        _isMenuActive = false;

        // Disconnect if connected
        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            var disconnectTask = _networkHandler.ShutdownGame();

            // Wait for disconnect or timeout
            float startTime = Time.time;
            while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
            {
                yield return null;
            }
        }

        // Go to main menu then immediately show join panel
        SceneManager.LoadScene(_mainMenuSceneName);

        // Set a flag to automatically open join panel when loaded
        PlayerPrefs.SetInt("AutoJoin", 1);
        PlayerPrefs.Save();
    }
}