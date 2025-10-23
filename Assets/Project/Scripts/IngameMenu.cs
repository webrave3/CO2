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

    private NetworkRunnerHandler _networkHandler;
    private bool _isMenuVisible = false;

    private void Start()
    {
        // If GameMenuManager exists, this script is redundant
        if (GameMenuManager.Instance != null)
        {
            this.enabled = false; // Disable this component
            if (_menuPanel != null) _menuPanel.SetActive(false); // Ensure panel is hidden
            Destroy(gameObject); // Optional: Destroy this GameObject
            return;
        }

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
        // Settings button listener would go here if needed
    }

    // Called by button or key press
    public void ToggleMenu()
    {
        // If GameMenuManager now exists (e.g., loaded later), delegate to it
        if (GameMenuManager.Instance != null)
        {
            GameMenuManager.Instance.ToggleMenu();
            this.enabled = false; // Disable self
            return;
        }

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
        // If GameMenuManager exists, delegate
        if (GameMenuManager.Instance != null)
        {
            GameMenuManager.Instance.ReturnToMainMenu();
            return;
        }

        // Fallback behavior
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
            // Wait for disconnect or timeout
            while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
            {
                yield return null;
            }
        }
        SceneManager.LoadScene(_mainMenuSceneName);
    }
}