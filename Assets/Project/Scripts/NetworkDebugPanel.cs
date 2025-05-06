using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using System;
using System.Text;
using UnityEngine.SceneManagement;

/// <summary>
/// Add this script to your JoinGamePanel to get a comprehensive debugging overlay that persists across scenes
/// </summary>
public class NetworkDebugPanel : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private Color _backgroundColor = new Color(0, 0, 0, 0.8f);
    [SerializeField] private Color _headerColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color _normalTextColor = new Color(0.9f, 0.9f, 0.9f);
    [SerializeField] private Color _warningTextColor = Color.yellow;
    [SerializeField] private Color _errorTextColor = new Color(1f, 0.5f, 0.5f);
    [SerializeField] private Color _successTextColor = new Color(0.5f, 1f, 0.5f);

    [Header("Hotkeys")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F4;
    [SerializeField] private KeyCode _copyKey = KeyCode.C;
    [SerializeField] private KeyCode _mainMenuKey = KeyCode.Escape;

    [Header("Scene Navigation")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    // Static instance for persistence across scenes
    private static NetworkDebugPanel _instance;

    // References
    private Canvas _canvas;
    private TextMeshProUGUI _debugText;
    private NetworkRunnerHandler _networkHandler;
    private RoomBrowserUI _roomBrowser;

    // Runtime data
    private StringBuilder _debugInfo = new StringBuilder();
    private float _lastUpdateTime;
    private bool _isCopying = false;

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

        // Create debug UI
        CreateDebugUI();
    }

    void Start()
    {
        // Find required components
        FindReferences();

        // Initial update
        UpdateDebugInfo();

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        // Clean up
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_instance == this)
            _instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find references again after scene change
        FindReferences();
        UpdateDebugInfo();
    }

    private void FindReferences()
    {
        _networkHandler = FindObjectOfType<NetworkRunnerHandler>();
        _roomBrowser = FindObjectOfType<RoomBrowserUI>();
    }

    void Update()
    {
        // Toggle visibility with F4
        if (Input.GetKeyDown(_toggleKey))
        {
            _canvas.gameObject.SetActive(!_canvas.gameObject.activeSelf);
            if (_canvas.gameObject.activeSelf)
                UpdateDebugInfo();
        }

        // Copy to clipboard with C key (when panel is visible)
        if (_canvas.gameObject.activeSelf && Input.GetKeyDown(_copyKey) && !_isCopying)
        {
            StartCoroutine(CopyToClipboard());
        }

        // Return to main menu with Escape key
        if (Input.GetKeyDown(_mainMenuKey) && SceneManager.GetActiveScene().name != _mainMenuSceneName)
        {
            ReturnToMainMenu();
        }

        // Update every 0.5 seconds when visible
        if (_canvas.gameObject.activeSelf && Time.time - _lastUpdateTime > 0.5f)
        {
            UpdateDebugInfo();
            _lastUpdateTime = Time.time;
        }
    }

    private void CreateDebugUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("DebugOverlayCanvas");
        canvasObj.transform.SetParent(transform);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767; // Make sure it's on top

        // Add required components for UI
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create panel background
        GameObject panelObj = new GameObject("DebugPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = _backgroundColor;

        // Position panel in top-right
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.7f, 0.6f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.offsetMin = new Vector2(5, 5);
        panelRect.offsetMax = new Vector2(-5, -5);

        // Add header
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(panelObj.transform, false);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = $"NETWORK DEBUG ({_toggleKey}=Toggle, {_copyKey}=Copy, {_mainMenuKey}=Menu)";
        headerText.fontSize = 14;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = _headerColor;
        headerText.alignment = TextAlignmentOptions.Center;

        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.sizeDelta = new Vector2(0, 20);
        headerRect.anchoredPosition = new Vector2(0, -10);

        // Add text area
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(panelObj.transform, false);

        _debugText = textObj.AddComponent<TextMeshProUGUI>();
        _debugText.fontSize = 12;
        _debugText.color = _normalTextColor;
        _debugText.alignment = TextAlignmentOptions.TopLeft;
        _debugText.enableWordWrapping = true;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -25);

        // Add copy button
        GameObject btnObj = new GameObject("CopyButton");
        btnObj.transform.SetParent(panelObj.transform, false);

        Button copyBtn = btnObj.AddComponent<Button>();
        UnityEngine.UI.Image btnImage = btnObj.AddComponent<UnityEngine.UI.Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.3f, 0);
        btnRect.anchorMax = new Vector2(0.7f, 0);
        btnRect.sizeDelta = new Vector2(0, 25);
        btnRect.anchoredPosition = new Vector2(0, 15);

        // Button text
        GameObject btnTextObj = new GameObject("ButtonText");
        btnTextObj.transform.SetParent(btnObj.transform, false);

        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "Copy (C)";
        btnText.fontSize = 12;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        // Set button click action
        copyBtn.onClick.AddListener(() => {
            StartCoroutine(CopyToClipboard());
        });

        // Add main menu button
        GameObject menuBtnObj = new GameObject("MenuButton");
        menuBtnObj.transform.SetParent(panelObj.transform, false);

        Button menuBtn = menuBtnObj.AddComponent<Button>();
        UnityEngine.UI.Image menuBtnImage = menuBtnObj.AddComponent<UnityEngine.UI.Image>();
        menuBtnImage.color = new Color(0.7f, 0.3f, 0.3f, 1);

        RectTransform menuBtnRect = menuBtnObj.GetComponent<RectTransform>();
        menuBtnRect.anchorMin = new Vector2(0.75f, 0);
        menuBtnRect.anchorMax = new Vector2(0.95f, 0);
        menuBtnRect.sizeDelta = new Vector2(0, 25);
        menuBtnRect.anchoredPosition = new Vector2(0, 15);

        // Button text
        GameObject menuBtnTextObj = new GameObject("MenuButtonText");
        menuBtnTextObj.transform.SetParent(menuBtnObj.transform, false);

        TextMeshProUGUI menuBtnText = menuBtnTextObj.AddComponent<TextMeshProUGUI>();
        menuBtnText.text = "Main Menu";
        menuBtnText.fontSize = 12;
        menuBtnText.color = Color.white;
        menuBtnText.alignment = TextAlignmentOptions.Center;

        RectTransform menuBtnTextRect = menuBtnTextObj.GetComponent<RectTransform>();
        menuBtnTextRect.anchorMin = Vector2.zero;
        menuBtnTextRect.anchorMax = Vector2.one;
        menuBtnTextRect.offsetMin = Vector2.zero;
        menuBtnTextRect.offsetMax = Vector2.zero;

        // Set button click action
        menuBtn.onClick.AddListener(ReturnToMainMenu);

        // Initially hide the panel
        _canvas.gameObject.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        // First check if we need to clean up network stuff
        if (_networkHandler != null && _networkHandler.IsSessionActive)
        {
            Debug.Log("Shutting down network session before returning to main menu");
            StartCoroutine(DisconnectAndReturnToMenu());
        }
        else
        {
            // No active session, just load the menu
            Debug.Log("Returning to main menu");
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }

    private IEnumerator DisconnectAndReturnToMenu()
    {
        // Start disconnection process
        if (_networkHandler != null)
        {
            var disconnectTask = _networkHandler.ShutdownGame();

            // Wait for disconnect to complete (or timeout after 3 seconds)
            float startTime = Time.time;
            while (!disconnectTask.IsCompleted && Time.time - startTime < 3.0f)
            {
                yield return null;
            }
        }

        // Now load the menu
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void UpdateDebugInfo()
    {
        _debugInfo.Clear();
        _debugInfo.AppendLine($"Time: {DateTime.Now.ToString("HH:mm:ss")}");
        _debugInfo.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        _debugInfo.AppendLine();

        // NetworkRunnerHandler state
        if (_networkHandler != null)
        {
            AppendWithHeader("NETWORK RUNNER STATE:");
            _debugInfo.AppendLine($"Runner: {(_networkHandler.Runner != null ? "Valid" : "NULL")}");

            if (_networkHandler.Runner != null)
            {
                var runner = _networkHandler.Runner;
                _debugInfo.AppendLine($"IsRunning: {runner.IsRunning}");
                _debugInfo.AppendLine($"GameMode: {runner.GameMode}");
                _debugInfo.AppendLine($"IsServer: {runner.IsServer}");
                _debugInfo.AppendLine($"IsClient: {runner.IsClient}");
                _debugInfo.AppendLine($"IsConnectedToServer: {runner.IsConnectedToServer}");
                _debugInfo.AppendLine($"IsShutdown: {runner.IsShutdown}");

                // Session information
                if (runner.SessionInfo != null)
                {
                    AppendWithHeader("CURRENT SESSION:");
                    _debugInfo.AppendLine($"Name: {runner.SessionInfo.Name}");
                    _debugInfo.AppendLine($"IsVisible: {runner.SessionInfo.IsVisible}");
                    _debugInfo.AppendLine($"IsOpen: {runner.SessionInfo.IsOpen}");
                    _debugInfo.AppendLine($"PlayerCount: {runner.SessionInfo.PlayerCount}");
                    _debugInfo.AppendLine($"Region: {runner.SessionInfo.Region}");
                }
                else
                {
                    AppendWithColor("Not connected to any session", _warningTextColor);
                }
            }

            // Handler session info
            AppendWithHeader("SESSION INFO:");
            _debugInfo.AppendLine($"SessionDisplayName: {_networkHandler.SessionDisplayName}");
            _debugInfo.AppendLine($"SessionHash: {_networkHandler.SessionHash}");
            _debugInfo.AppendLine($"SessionUniqueID: {_networkHandler.SessionUniqueID}");

            // Available sessions
            AppendWithHeader("AVAILABLE SESSIONS:");
            var sessions = _networkHandler.GetAvailableSessions();

            if (sessions.Count > 0)
            {
                foreach (var session in sessions)
                {
                    string displayName = session.Name;
                    string hash = "Unknown";

                    if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                        displayName = nameObj.PropertyValue.ToString();

                    if (session.Properties.TryGetValue("Hash", out var hashObj))
                        hash = hashObj.PropertyValue.ToString();

                    _debugInfo.AppendLine($"- {displayName} | Hash: {hash} | Players: {session.PlayerCount}/{session.MaxPlayers} | Region: {session.Region}");
                }
            }
            else
            {
                AppendWithColor("No available sessions found", _warningTextColor);
            }
        }
        else
        {
            AppendWithColor("NetworkRunnerHandler is NULL", _errorTextColor);
        }

        // RoomBrowserUI state
        if (_roomBrowser != null)
        {
            AppendWithHeader("ROOM BROWSER STATE:");
            // Get private fields via reflection (for debugging)
            var privateInfoType = _roomBrowser.GetType();

            // Try to get _joinGamePanel
            var joinPanelField = privateInfoType.GetField("_joinGamePanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (joinPanelField != null)
            {
                var joinPanel = joinPanelField.GetValue(_roomBrowser) as GameObject;
                _debugInfo.AppendLine($"JoinGamePanel: {(joinPanel != null ? (joinPanel.activeSelf ? "Active" : "Inactive") : "NULL")}");
            }

            // Try to get _roomListContent
            var roomListField = privateInfoType.GetField("_roomListContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (roomListField != null)
            {
                var roomList = roomListField.GetValue(_roomBrowser) as Transform;
                _debugInfo.AppendLine($"RoomListContent: {(roomList != null ? (roomList.gameObject.activeSelf ? "Active" : "Inactive") : "NULL")}");

                // Count children
                if (roomList != null)
                {
                    _debugInfo.AppendLine($"Room entries count: {roomList.childCount}");
                }
            }
        }
        else
        {
            AppendWithColor("RoomBrowserUI is NULL", _warningTextColor);
        }

        // Display debug info
        if (_debugText != null)
        {
            _debugText.text = _debugInfo.ToString();
        }
    }

    private void AppendWithHeader(string header)
    {
        _debugInfo.AppendLine();
        _debugInfo.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(_headerColor)}>{header}</color>");
    }

    private void AppendWithColor(string text, Color color)
    {
        _debugInfo.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>");
    }

    private IEnumerator CopyToClipboard()
    {
        _isCopying = true;

        // Update before copying to ensure latest info
        UpdateDebugInfo();

        // Copy to clipboard
        GUIUtility.systemCopyBuffer = _debugInfo.ToString().Replace("<color=#", "[color:").Replace("</color>", "[/color]");

        // Show feedback
        string originalText = _debugText.text;
        _debugText.text = "COPIED TO CLIPBOARD!\n\n" + originalText;

        yield return new WaitForSeconds(1.0f);

        // Restore text
        _debugText.text = originalText;
        _isCopying = false;
    }
}