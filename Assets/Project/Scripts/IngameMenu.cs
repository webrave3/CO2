using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement; // Use full namespace

public class InGameMenu : MonoBehaviour
{
    [Header("Menu References")]
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _mainMenuButton;

    [Header("Settings")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private string _lobbySceneName = "Lobby"; // Assign your lobby scene name
    [SerializeField] private string _gameSceneName = "Game";   // Assign your game scene name

    private NetworkRunnerHandler _networkHandler;
    private bool _isMenuVisible = false;

    private void Start()
    {
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_menuPanel != null)
            _menuPanel.SetActive(false); // Ensure hidden at start

        // Setup button listeners
        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveAllListeners();
            _resumeButton.onClick.AddListener(ToggleMenu);
        }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.onClick.RemoveAllListeners();
            _mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    // --- MODIFICATION: Added scene check ---
    private void Update()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        // Only check for ESC key if we are in the Lobby or Game scene
        if (currentScene == _lobbySceneName || currentScene == _gameSceneName)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleMenu();
            }
        }
    }

    // Called by button or key press
    public void ToggleMenu()
    {
        _isMenuVisible = !_isMenuVisible;

        if (_menuPanel != null)
        {
            _menuPanel.SetActive(_isMenuVisible);
        }

        Cursor.lockState = _isMenuVisible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isMenuVisible;
    }

    public void ReturnToMainMenu()
    {
        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        if (_networkHandler != null && _networkHandler.Runner != null && _networkHandler.Runner.IsRunning)
        {
            StartCoroutine(DisconnectAndReturnToMenu());
        }
        else
        {
            SceneManager.LoadScene(_mainMenuSceneName);
        }
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
}