using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Fusion;
using System;
using UnityEngine.SceneManagement;

public class SessionDebugger : MonoBehaviour
{
    // Public key binding that can be changed in the inspector
    [Header("Debug Controls")]
    [SerializeField] private KeyCode _debugKey = KeyCode.F6; // Changed to F6 to avoid potential conflicts
    [SerializeField] private Color _panelColor = new Color(0, 0, 0, 0.85f);
    [SerializeField] private Color _headerColor = new Color(1f, 0.8f, 0f, 1f); // Bright yellow

    // Private references
    private GameObject _debugPanel;
    private TextMeshProUGUI _debugText;
    private NetworkRunnerHandler _networkRunnerHandler;
    private Canvas _canvas;
    private float _lastRefreshTime;

    // Test message to confirm visibility (will appear in the log tab)
    private bool _keyPressed = false;
    private float _visibilityTimer = 0f;

    private void Awake()
    {
        // Log key debug setup
        Debug.Log($"[SessionDebugger] Initialized with debug key: {_debugKey}. Press this key to show debug panel.");
        DontDestroyOnLoad(gameObject);

        // Check for duplicates and clean up if needed
        SessionDebugger[] debuggers = FindObjectsOfType<SessionDebugger>();
        if (debuggers.Length > 1)
        {
            Debug.Log($"[SessionDebugger] Found {debuggers.Length} debuggers - keeping only one");
            for (int i = 1; i < debuggers.Length; i++)
            {
                Destroy(debuggers[i].gameObject);
            }
        }

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void AddRefreshButton()
    {
        // Create debug button at bottom of panel
        GameObject refreshBtnObj = new GameObject("RefreshBtn");
        refreshBtnObj.transform.SetParent(_debugPanel.transform, false);

        UnityEngine.UI.Button refreshBtn = refreshBtnObj.AddComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image btnImage = refreshBtnObj.AddComponent<UnityEngine.UI.Image>();
        btnImage.color = new Color(0.2f, 0.5f, 0.2f, 1);

        RectTransform btnRect = refreshBtnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0);
        btnRect.anchorMax = new Vector2(0.5f, 0);
        btnRect.sizeDelta = new Vector2(120, 30);
        btnRect.anchoredPosition = new Vector2(0, 15);

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(refreshBtnObj.transform, false);

        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "Refresh Rooms";
        btnText.fontSize = 12;
        btnText.alignment = TextAlignmentOptions.Center;

        RectTransform textRect = btnTextObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        // Add click handler
        refreshBtn.onClick.AddListener(() => {
            if (_networkRunnerHandler != null)
            {
                _networkRunnerHandler.ForceRefreshSessions();
                UpdateDebugInfo(); // Refresh display right away
            }
        });
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SessionDebugger] Scene loaded: {scene.name}");

        // Force recreation of debug panel
        if (_debugPanel != null)
        {
            Destroy(_debugPanel);
            _debugPanel = null;
        }

        // Create new panel for this scene
        EnsureDebugPanelExists();

        // Make sure NetworkRunnerHandler reference is up to date
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        // Add refresh button
        AddRefreshButton();

        // Initially hide panel
        _debugPanel.SetActive(false);
    }

    private void Start()
    {
        Debug.Log("[SessionDebugger] Start called");

        // Ensure we have all necessary components
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        EnsureDebugPanelExists();

        // Visual confirmation in the console
        Debug.Log($"[SessionDebugger] Press {_debugKey} key to toggle network debug panel");
    }

    private void Update()
    {
        // Create a flashing indication in the console when key is pressed
        if (Input.GetKeyDown(_debugKey))
        {
            _keyPressed = true;
            _visibilityTimer = 2f; // Show message for 2 seconds

            Debug.Log($"[SessionDebugger] {_debugKey} KEY PRESSED!");

            // Toggle panel
            EnsureDebugPanelExists();
            if (_debugPanel != null)
            {
                _debugPanel.SetActive(!_debugPanel.activeSelf);
                Debug.Log($"[SessionDebugger] Panel visibility: {_debugPanel.activeSelf}");
            }
        }

        // Update key press indication
        if (_keyPressed)
        {
            _visibilityTimer -= Time.deltaTime;
            if (_visibilityTimer <= 0)
            {
                _keyPressed = false;
            }
        }

        // Update debug info if panel is active
        if (_debugPanel != null && _debugPanel.activeSelf)
        {
            if (Time.time - _lastRefreshTime >= 0.5f)
            {
                UpdateDebugInfo();
                _lastRefreshTime = Time.time;
            }
        }

        // Try to find NetworkRunnerHandler if null
        if (_networkRunnerHandler == null)
        {
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        }
    }

    private void EnsureDebugPanelExists()
    {
        if (_debugPanel != null)
            return;

        Debug.Log("[SessionDebugger] Creating debug panel");

        // Create a dedicated canvas for our debug UI
        GameObject canvasObj = new GameObject("DebugCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767; // Maximum sorting order to ensure it's on top

        // Add canvas components
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Create event system if needed
        EnsureEventSystemExists();

        // Make canvas persist
        DontDestroyOnLoad(canvasObj);

        // Create debug panel
        _debugPanel = new GameObject("DebugPanel");
        _debugPanel.transform.SetParent(_canvas.transform, false);

        // Add panel background
        UnityEngine.UI.Image panelImage = _debugPanel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = _panelColor;

        // Position panel in upper right
        RectTransform panelRect = _debugPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.7f, 0.65f);
        panelRect.anchorMax = new Vector2(0.99f, 0.99f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add header text
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(_debugPanel.transform, false);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = $"NETWORK DEBUG (TOGGLE: {_debugKey})";
        headerText.fontSize = 16;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = _headerColor;
        headerText.alignment = TextAlignmentOptions.Center;

        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.sizeDelta = new Vector2(0, 25);
        headerRect.anchoredPosition = new Vector2(0, -12.5f);

        // Create scrollable content
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(_debugPanel.transform, false);

        UnityEngine.UI.ScrollRect scrollRect = scrollObj.AddComponent<UnityEngine.UI.ScrollRect>();

        RectTransform scrollRectTransform = scrollObj.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0);
        scrollRectTransform.anchorMax = new Vector2(1, 1);
        scrollRectTransform.offsetMin = new Vector2(10, 10);
        scrollRectTransform.offsetMax = new Vector2(-10, -30);

        // Create viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);

        UnityEngine.UI.Image viewportImage = viewportObj.AddComponent<UnityEngine.UI.Image>();
        viewportImage.color = new Color(0, 0, 0, 0.2f);

        UnityEngine.UI.Mask viewportMask = viewportObj.AddComponent<UnityEngine.UI.Mask>();
        viewportMask.showMaskGraphic = true;

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;

        // Create content container
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);

        _debugText = contentObj.AddComponent<TextMeshProUGUI>();
        _debugText.fontSize = 14;
        _debugText.color = Color.white;
        _debugText.alignment = TextAlignmentOptions.TopLeft;
        _debugText.enableWordWrapping = true;
        _debugText.raycastTarget = false; // Prevent blocking clicks

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.sizeDelta = new Vector2(0, 600);
        contentRect.pivot = new Vector2(0.5f, 1);

        // Configure scroll view
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 10;

        // Close button
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(_debugPanel.transform, false);

        UnityEngine.UI.Button closeButton = closeObj.AddComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image closeImage = closeObj.AddComponent<UnityEngine.UI.Image>();
        closeImage.color = new Color(0.7f, 0.2f, 0.2f, 1);

        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.sizeDelta = new Vector2(20, 20);
        closeRect.anchoredPosition = new Vector2(-5, -5);

        // Add X text to button
        GameObject xObj = new GameObject("X");
        xObj.transform.SetParent(closeObj.transform, false);

        TextMeshProUGUI xText = xObj.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 14;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Color.white;

        RectTransform xRect = xObj.GetComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;

        // Add close functionality
        closeButton.onClick.AddListener(() => {
            _debugPanel.SetActive(false);
        });

        // Initially hide panel
        _debugPanel.SetActive(false);

        // Add initial info
        UpdateDebugInfo();

        Debug.Log("[SessionDebugger] Debug panel created successfully");
    }

    private void EnsureEventSystemExists()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            Debug.Log("[SessionDebugger] Creating EventSystem");
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(eventSystem);
        }
    }

    private void UpdateDebugInfo()
    {
        if (_debugText == null)
        {
            Debug.LogError("[SessionDebugger] DebugText is null!");
            return;
        }

        string info = $"<color=#FFFF00>NETWORK DEBUG [{DateTime.Now.ToString("HH:mm:ss")}]</color>\n\n";

        // Always show scene info
        info += $"<color=#00FFFF>SCENE:</color> {SceneManager.GetActiveScene().name}\n";
        info += $"<color=#00FFFF>FPS:</color> {(int)(1.0f / Time.deltaTime)}\n\n";

        // NetworkRunnerHandler info
        if (_networkRunnerHandler == null)
        {
            info += "<color=#FF0000>NetworkRunnerHandler not found</color>\n";
            info += "Please make sure NetworkRunnerHandler exists in the scene\n";
            _debugText.text = info;
            return;
        }

        // Network info
        info += "<color=#00FFFF>NETWORK INFO:</color>\n";
        info += $"Session Active: {(_networkRunnerHandler.IsSessionActive ? "YES" : "NO")}\n";

        // Runner info
        if (_networkRunnerHandler.Runner != null)
        {
            NetworkRunner runner = _networkRunnerHandler.Runner;
            info += $"Game Mode: {runner.GameMode}\n";
            info += $"Is Server: {runner.IsServer}\n";
            info += $"Is Client: {runner.IsClient}\n";
            info += $"Local Player: {runner.LocalPlayer.PlayerId}\n\n";
        }
        else
        {
            info += "Runner is null\n\n";
        }

        // Session info
        info += "<color=#00FFFF>SESSION INFO:</color>\n";
        info += $"Name: {_networkRunnerHandler.SessionDisplayName}\n";
        info += $"Hash: {_networkRunnerHandler.SessionHash}\n";
        info += $"ID: {_networkRunnerHandler.SessionUniqueID}\n\n";

        // Available sessions
        info += "<color=#00FFFF>AVAILABLE SESSIONS:</color>\n";
        List<SessionInfo> sessions = _networkRunnerHandler.GetAvailableSessions();

        if (sessions.Count > 0)
        {
            info += $"Found {sessions.Count} sessions:\n";
            int index = 1;

            foreach (var session in sessions)
            {
                string displayName = session.Name;
                string hash = "Unknown";

                if (session.Properties.TryGetValue("DisplayName", out var nameObj))
                    displayName = nameObj.PropertyValue.ToString();
                if (session.Properties.TryGetValue("Hash", out var hashObj))
                    hash = hashObj.PropertyValue.ToString();

                info += $"{index}. <color=#AAFFAA>{displayName}</color> [{hash}] - {session.PlayerCount}/{session.MaxPlayers}\n";
                index++;
            }
        }
        else
        {
            info += "No sessions available\n";
        }

        // Update text
        _debugText.text = info;

        // Adjust content size
        RectTransform contentRect = _debugText.transform.parent.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x,
                Mathf.Max(300, _debugText.preferredHeight + 20));
        }
    }

    private void OnDestroy()
    {
        // Cleanup 
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Debug.Log("[SessionDebugger] Destroyed");
    }

    private void OnGUI()
    {
        // Add a very visible indicator when key is pressed - helps debug key detection issues
        if (_keyPressed)
        {
            GUI.backgroundColor = Color.yellow;
            GUI.contentColor = Color.black;
            GUI.Box(new Rect(Screen.width / 2 - 150, 10, 300, 30),
                $"{_debugKey} KEY PRESSED! Debug panel toggled.");
        }
    }
}