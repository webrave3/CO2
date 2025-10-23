using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text; // Keep for clipboard functionality if desired
using UnityEngine.SceneManagement;

/// <summary>
/// A persistent utility panel, originally for debugging, now potentially for other functions.
/// </summary>
public class NetworkDebugPanel : MonoBehaviour
{
    // Hotkeys might still be useful for non-debug actions
    [Header("Hotkeys")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F4; // Example: Toggle visibility
    [SerializeField] private KeyCode _copyKey = KeyCode.C;    // Example: Copy session info?
    [SerializeField] private KeyCode _mainMenuKey = KeyCode.Escape;

    [Header("Scene Navigation")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    // Static instance for persistence across scenes
    private static NetworkDebugPanel _instance;

    // References - Keep references needed for non-debug functionality
    private Canvas _canvas; // Might control visibility of a non-debug panel now
    private NetworkRunnerHandler _networkHandler;
    // Remove references to debug text elements:
    // private TextMeshProUGUI _debugText;
    // private RoomBrowserUI _roomBrowser; // Remove if only used for debug display

    // Runtime data
    private bool _isCopying = false; // Keep if copy functionality remains

    void Awake()
    {
        // Singleton pattern for persistence
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Create a minimal UI if needed, or assume UI exists elsewhere
        // CreateBaseUI(); // Modify or remove this call
    }

    void Start()
    {
        FindReferences();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindReferences();
        // Potentially hide/show panel based on scene
        if (_canvas != null)
        {
            // Example: Hide panel in main menu
            _canvas.gameObject.SetActive(scene.name != _mainMenuSceneName);
        }
    }

    private void FindReferences()
    {
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();
        // Find other necessary non-debug components
    }

    void Update()
    {
        // Toggle visibility (if panel still exists)
        if (Input.GetKeyDown(_toggleKey) && _canvas != null)
        {
            _canvas.gameObject.SetActive(!_canvas.gameObject.activeSelf);
        }

        // Copy functionality (adapt to copy relevant non-debug info)
        if (_canvas != null && _canvas.gameObject.activeSelf && Input.GetKeyDown(_copyKey) && !_isCopying)
        {
            StartCoroutine(CopyToClipboard()); // Adapt this coroutine
        }

        // Return to main menu
        if (Input.GetKeyDown(_mainMenuKey) && SceneManager.GetActiveScene().name != _mainMenuSceneName)
        {
            ReturnToMainMenu();
        }
    }

    // Optional: Create a very basic panel or remove UI creation entirely
    private void CreateBaseUI()
    {
        // If you still need a panel controlled by this script, create it here.
        // Otherwise, remove this method. Example:
        GameObject canvasObj = new GameObject("UtilityCanvas");
        canvasObj.transform.SetParent(transform);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32766; // High, but maybe below debug panels if they exist
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        // Add minimal panel elements if needed

        _canvas.gameObject.SetActive(false); // Initially hidden
    }


    public void ReturnToMainMenu()
    {
        if (_networkHandler != null && _networkHandler.IsSessionActive)
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

    // Adapt this to copy relevant info (e.g., Session Hash)
    private IEnumerator CopyToClipboard()
    {
        _isCopying = true;
        string infoToCopy = "No info available";

        if (_networkHandler != null && !string.IsNullOrEmpty(_networkHandler.SessionHash))
        {
            infoToCopy = _networkHandler.SessionHash;
        }

        GUIUtility.systemCopyBuffer = infoToCopy;

        // Optional: Show feedback (requires a text element)
        // string originalText = _feedbackText.text;
        // _feedbackText.text = "COPIED!";
        yield return new WaitForSeconds(1.0f);
        // _feedbackText.text = originalText;

        _isCopying = false;
    }
}