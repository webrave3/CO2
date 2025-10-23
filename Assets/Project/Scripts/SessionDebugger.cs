using UnityEngine;
using TMPro; // Keep if using TextMeshPro for non-debug UI
using Fusion; // Keep if interacting with Fusion components
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Keep if using UI Buttons etc.
using System.Collections;
using System.Text; // Keep only if still needed (e.g., building strings for non-debug display)

// Consider renaming this class if it no longer primarily serves debugging purposes.
// For example, "NetworkUtilityPanel" or "SessionHelper".
public class SessionDebugger : MonoBehaviour // Or rename to NetworkUtilityPanel
{
    // Keep controls relevant to non-debug functions
    [Header("Controls")]
    [SerializeField] private KeyCode _togglePanelKey = KeyCode.F6; // Key to show/hide the utility panel
    // Removed color settings if debug panel UI is removed

    [Header("Network Recovery")] // Keep if recovery function remains
    [SerializeField] private Button _recoveryButton;
    [SerializeField] private TextMeshProUGUI _recoveryStatusText; // Keep if showing recovery status

    // Remove references to debug-specific UI elements
    // private GameObject _debugPanel;
    // private TextMeshProUGUI _debugText;
    private NetworkRunnerHandler _networkRunnerHandler;
    private Canvas _panelCanvas; // Reference to the canvas holding the utility UI
    private float _lastRefreshTime; // Could be used for throttling non-debug updates

    private Coroutine _recoveryStatusCoroutine;

    private static SessionDebugger _instance; // Keep singleton if needed for persistence

    private void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Find the canvas associated with this utility panel
        _panelCanvas = GetComponentInChildren<Canvas>(true); // Assuming canvas is a child

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        SetupRecoveryButton(); // Setup button if it exists
        if (_panelCanvas != null) _panelCanvas.gameObject.SetActive(false); // Start hidden
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
        // Clean up listeners
        if (_recoveryButton != null) _recoveryButton.onClick.RemoveAllListeners();
    }


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-find handler if necessary
        if (_networkRunnerHandler == null)
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Optionally hide/show panel based on scene
        // if (_panelCanvas != null) _panelCanvas.gameObject.SetActive(ShouldShowPanelInScene(scene.name));
    }

    private void SetupRecoveryButton()
    {
        // Find the recovery button if not assigned
        if (_recoveryButton == null)
        {
            // Attempt to find it by name or tag within the panel canvas
            if (_panelCanvas != null)
            {
                _recoveryButton = _panelCanvas.GetComponentInChildren<Button>(); // Simple example
                                                                                 // More robust: Find by specific name using FindDeepChild or similar utility
            }
        }


        if (_recoveryButton != null)
        {
            _recoveryButton.onClick.RemoveAllListeners(); // Clear previous listeners
            _recoveryButton.onClick.AddListener(AttemptNetworkRecovery);
        }

        // Find status text if needed
        if (_recoveryStatusText == null && _panelCanvas != null)
        {
            // Find TextMeshProUGUI, potentially by name
            var texts = _panelCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts) { if (t.name.Contains("Status")) { _recoveryStatusText = t; break; } } // Example find
        }

        if (_recoveryStatusText != null)
            _recoveryStatusText.gameObject.SetActive(false);
    }

    private void AttemptNetworkRecovery()
    {
        if (_networkRunnerHandler != null)
        {
            _networkRunnerHandler.RecoverNetworkState();
            // Optionally log state to console if needed for non-UI feedback
            // _networkRunnerHandler.LogNetworkState();
            ShowRecoveryStatus("Network recovery attempted.");
        }
        else
        {
            ShowRecoveryStatus("NetworkRunnerHandler not found!");
        }
    }


    private void Update()
    {
        // Toggle panel visibility
        if (Input.GetKeyDown(_togglePanelKey) && _panelCanvas != null)
        {
            bool isActive = !_panelCanvas.gameObject.activeSelf;
            _panelCanvas.gameObject.SetActive(isActive);
            // Optionally update content when shown
            // if (isActive) UpdatePanelContent();
        }

        // Try to find NetworkRunnerHandler if null
        if (_networkRunnerHandler == null)
        {
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
            // If found, re-setup the button listener in case it was missed
            if (_networkRunnerHandler != null && _recoveryButton != null && _recoveryButton.onClick.GetPersistentEventCount() == 0)
            {
                SetupRecoveryButton();
            }
        }

        // Optional: Update panel content periodically if needed and visible
        // if (_panelCanvas != null && _panelCanvas.gameObject.activeSelf) {
        //     if (Time.time - _lastRefreshTime >= 1.0f) { // Update every second
        //         UpdatePanelContent();
        //         _lastRefreshTime = Time.time;
        //     }
        // }
    }

    // Removed EnsureDebugPanelExists - assumes UI is pre-built or handled differently

    // Removed UpdateDebugInfo - replace with UpdatePanelContent if needed for non-debug info

    private void ShowRecoveryStatus(string message)
    {
        if (_recoveryStatusText != null)
        {
            _recoveryStatusText.text = message;
            _recoveryStatusText.gameObject.SetActive(true);
            if (_recoveryStatusCoroutine != null) StopCoroutine(_recoveryStatusCoroutine);
            _recoveryStatusCoroutine = StartCoroutine(HideRecoveryStatusAfterDelay(3.0f));
        }
    }

    private IEnumerator HideRecoveryStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_recoveryStatusText != null) _recoveryStatusText.gameObject.SetActive(false);
        _recoveryStatusCoroutine = null;
    }

    // Removed OnGUI - was only for debug key press feedback
}