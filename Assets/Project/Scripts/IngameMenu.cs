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
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Ensure menu is hidden at start
        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        // Setup button listeners
        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(ToggleMenu);

        if (_mainMenuButton != null)
            _mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        Debug.Log("InGameMenu initialized");
    }

    private void Update()
    {
        // Remove Escape key handling from here completely
        // GameMenuManager now handles this
    }

    public void ToggleMenu()
    {
        Debug.Log("ToggleMenu called in InGameMenu");

        // Check if GameMenuManager exists and let it handle the menu
        if (GameMenuManager.Instance != null)
        {
            Debug.Log("Delegating to GameMenuManager.ToggleMenu");
            GameMenuManager.Instance.ToggleMenu();
            return;
        }

        // Fallback to original behavior if GameMenuManager doesn't exist
        Debug.Log("No GameMenuManager found, using native toggle behavior");
        _isMenuVisible = !_isMenuVisible;

        if (_menuPanel != null)
        {
            _menuPanel.SetActive(_isMenuVisible);
        }

        // Lock/unlock cursor based on menu state
        Cursor.lockState = _isMenuVisible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isMenuVisible;
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("ReturnToMainMenu pressed in InGameMenu");

        // Check if GameMenuManager exists and let it handle the return to main menu
        if (GameMenuManager.Instance != null)
        {
            Debug.Log("Delegating to GameMenuManager.ReturnToMainMenu");
            GameMenuManager.Instance.ReturnToMainMenu();
            return;
        }

        Debug.Log("No GameMenuManager found, using native main menu return");

        // First hide menu
        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        // Properly shutdown network session
        if (_networkHandler != null && _networkHandler.Runner != null)
        {
            StartCoroutine(DisconnectAndReturnToMenu());
        }
        else
        {
            Debug.Log("No active network session, loading main menu directly");
            // No active session, just load the menu
            UnityEngine.SceneManagement.SceneManager.LoadScene(_mainMenuSceneName);
        }
    }

    private IEnumerator DisconnectAndReturnToMenu()
    {
        Debug.Log("Shutting down network session before returning to main menu");

        // Start disconnection process
        var disconnectTask = _networkHandler.ShutdownGame();

        // Wait for disconnect to complete (or timeout after 3 seconds)
        float startTime = Time.time;
        while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
        {
            yield return null;
        }

        // Now load the menu
        UnityEngine.SceneManagement.SceneManager.LoadScene(_mainMenuSceneName);
    }
}