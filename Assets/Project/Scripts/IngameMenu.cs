using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InGameMenu : MonoBehaviour
{
    [Header("Menu References")]
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _mainMenuButton;

    [Header("Settings")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    private NetworkRunnerHandler _networkHandler;
    private bool _isMenuVisible = false;

    private void Start()
    {
        Debug.Log("[InGameMenu] Initializing...");
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_networkHandler == null)
            Debug.LogWarning("[InGameMenu] NetworkRunnerHandler not found!");
        else
            Debug.Log("[InGameMenu] NetworkRunnerHandler found successfully");

        // Ensure menu is hidden at start
        if (_menuPanel != null)
            _menuPanel.SetActive(false);
        else
            Debug.LogError("[InGameMenu] Menu panel reference is missing!");

        // Setup button listeners
        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveAllListeners();
            _resumeButton.onClick.AddListener(ToggleMenu);
            Debug.Log("[InGameMenu] Resume button listener set up");
        }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.onClick.RemoveAllListeners();
            _mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            Debug.Log("[InGameMenu] Main menu button listener set up");
        }

        Debug.Log("[InGameMenu] Initialization complete");
    }

    public void ToggleMenu()
    {
        Debug.Log("[InGameMenu] ToggleMenu called");

        // Check if GameMenuManager exists and let it handle the menu
        if (GameMenuManager.Instance != null)
        {
            Debug.Log("[InGameMenu] Delegating to GameMenuManager.ToggleMenu");
            GameMenuManager.Instance.ToggleMenu();
            return;
        }

        Debug.Log("[InGameMenu] No GameMenuManager found, using native toggle behavior");
        _isMenuVisible = !_isMenuVisible;

        if (_menuPanel != null)
        {
            _menuPanel.SetActive(_isMenuVisible);
            Debug.Log($"[InGameMenu] Menu panel visibility set to: {_isMenuVisible}");
        }
        else
        {
            Debug.LogError("[InGameMenu] Menu panel is null!");
        }

        // Lock/unlock cursor based on menu state
        Cursor.lockState = _isMenuVisible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isMenuVisible;
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("[InGameMenu] ReturnToMainMenu called");

        // Proper check for GameMenuManager
        if (GameMenuManager.Instance != null)
        {
            Debug.Log("[InGameMenu] GameMenuManager found, delegating ReturnToMainMenu");
            GameMenuManager.Instance.ReturnToMainMenu();
            return;
        }

        Debug.Log("[InGameMenu] GameMenuManager not found, using fallback behavior");

        // Fallback behavior
        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        // Properly shutdown network session
        if (_networkHandler != null && _networkHandler.Runner != null)
        {
            Debug.Log("[InGameMenu] Starting network disconnection via coroutine");
            StartCoroutine(DisconnectAndReturnToMenu());
        }
        else
        {
            Debug.Log("[InGameMenu] No active network session, loading main menu directly");
            UnityEngine.SceneManagement.SceneManager.LoadScene(_mainMenuSceneName);
        }
    }

    private IEnumerator DisconnectAndReturnToMenu()
    {
        Debug.Log("[InGameMenu] Shutting down network session before returning to main menu");

        // Start disconnection process
        var disconnectTask = _networkHandler.ShutdownGame();

        // Wait for disconnect to complete (or timeout after 3 seconds)
        float startTime = Time.time;
        while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
        {
            yield return null;
        }

        Debug.Log("[InGameMenu] Network disconnect complete, loading main menu");
        // Now load the menu
        UnityEngine.SceneManagement.SceneManager.LoadScene(_mainMenuSceneName);
    }
}