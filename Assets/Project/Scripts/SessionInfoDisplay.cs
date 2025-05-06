using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System;
using System.Linq;
using System.Collections;

public class SessionInfoDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private TextMeshProUGUI _infoText;
    [SerializeField] private Color _textColor = new Color(0.8f, 0.8f, 1f, 0.9f);
    [SerializeField] private int _fontSize = 16;
    [SerializeField] private Vector2 _offset = new Vector2(20, 20);
    [SerializeField] private bool _showBackground = true;
    [SerializeField] private Color _backgroundColor = new Color(0, 0, 0, 0.7f);

    [Header("Display Options")]
    [SerializeField] private bool _showSessionName = true;
    [SerializeField] private bool _showSessionHash = true;
    [SerializeField] private bool _showSessionTime = true;
    [SerializeField] private bool _showPlayerCount = true;
    [SerializeField] private bool _showVisibilityStatus = true;

    [Header("Debug Features")]
    [SerializeField] private Button _debugButton;
    [SerializeField] private TextMeshProUGUI _debugStatusText;
    [SerializeField] private bool _showDebugPanelInMainMenu = true;

    private GameStateManager _gameStateManager;
    private NetworkRunner _runner;
    private Canvas _canvas;
    private RectTransform _rectTransform;
    private RectTransform _backgroundRect;

    private void Awake()
    {
        // Find NetworkRunner in scene
        _runner = FindObjectOfType<NetworkRunner>();

        if (_infoText == null)
        {
            // Create canvas for camera overlay
            GameObject canvasObj = new GameObject("SessionInfoCanvas");
            canvasObj.transform.SetParent(transform);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Add canvas scaler for better UI scaling
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Add a background panel if enabled
            if (_showBackground)
            {
                GameObject bgObj = new GameObject("InfoBackground");
                bgObj.transform.SetParent(canvasObj.transform);

                // Add image component for background
                UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
                bgImage.color = _backgroundColor;

                // Set up RectTransform - positioned in top-left with margin
                _backgroundRect = bgImage.rectTransform;
                _backgroundRect.anchorMin = new Vector2(0, 1);
                _backgroundRect.anchorMax = new Vector2(0, 1);
                _backgroundRect.pivot = new Vector2(0, 1);
                _backgroundRect.anchoredPosition = new Vector2(_offset.x, -_offset.y);
                _backgroundRect.sizeDelta = new Vector2(350, 180);
            }

            // Create text object
            GameObject textObj = new GameObject("SessionInfoText");
            textObj.transform.SetParent(_backgroundRect != null ? _backgroundRect.transform : canvasObj.transform);

            _infoText = textObj.AddComponent<TextMeshProUGUI>();
            _infoText.fontSize = _fontSize;
            _infoText.color = _textColor;
            _infoText.alignment = TextAlignmentOptions.TopLeft;
            _infoText.enableWordWrapping = true;

            // Position text within background
            _rectTransform = _infoText.GetComponent<RectTransform>();
            _rectTransform.anchorMin = new Vector2(0, 0);
            _rectTransform.anchorMax = new Vector2(1, 1);
            _rectTransform.offsetMin = new Vector2(10, 10);
            _rectTransform.offsetMax = new Vector2(-10, -10);
        }

        Debug.Log("SessionInfoDisplay initialized");
    }

    private void Start()
    {
        // Get reference to GameStateManager
        _gameStateManager = FindObjectOfType<GameStateManager>();

        if (_gameStateManager == null)
        {
            Debug.LogWarning("SessionInfoDisplay could not find GameStateManager. Session info may not display correctly.");
        }

        // Set up debug button
        SetupDebugButton();

        // Initial update
        UpdateSessionInfo();
    }

    private void Update()
    {
        // Update every second to reduce overhead
        if (Time.frameCount % 60 == 0)
        {
            UpdateSessionInfo();
        }
    }

    private void UpdateSessionInfo()
    {
        if (_infoText == null)
            return;

        // Initialize text
        string infoText = "<color=#ffd700><b>SESSION INFO</b></color>\n";

        if (_gameStateManager != null)
        {
            // Add session display name
            if (_showSessionName)
            {
                string sessionName = _gameStateManager.SessionDisplayName.ToString();
                if (!string.IsNullOrEmpty(sessionName))
                {
                    infoText += $"<color=#add8e6>Game:</color> {sessionName}\n";
                }
            }

            // Add session hash
            if (_showSessionHash)
            {
                string sessionHash = _gameStateManager.SessionHash.ToString();
                if (!string.IsNullOrEmpty(sessionHash))
                {
                    infoText += $"<color=#add8e6>Hash:</color> {sessionHash}\n";
                }
            }

            // Add session time
            if (_showSessionTime)
            {
                int startTime = _gameStateManager.SessionStartTime;
                if (startTime > 0)
                {
                    int currentTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    int elapsedSeconds = currentTime - startTime;
                    string timeString = FormatTimespan(elapsedSeconds);
                    infoText += $"<color=#add8e6>Time:</color> {timeString}\n";
                }
            }
        }

        // Add player count
        if (_showPlayerCount && _runner != null && _runner.IsRunning)
        {
            int playerCount = _runner.ActivePlayers.Count();
            int maxPlayers = _runner.SessionInfo?.MaxPlayers ?? 0;

            if (maxPlayers > 0)
            {
                infoText += $"<color=#add8e6>Players:</color> {playerCount}/{maxPlayers}\n";
            }
            else
            {
                infoText += $"<color=#add8e6>Players:</color> {playerCount}\n";
            }
        }

        // Add visibility status - clear indicator of room visibility
        if (_showVisibilityStatus && _runner != null && _runner.IsRunning && _runner.SessionInfo != null)
        {
            bool isVisible = _runner.SessionInfo.IsVisible;
            string visibilityText;
            string iconColor;

            if (isVisible)
            {
                visibilityText = "VISIBLE ✓";
                iconColor = "#00FF00"; // Bright green
            }
            else
            {
                visibilityText = "HIDDEN ✗";
                iconColor = "#FF6666"; // Light red
            }

            infoText += $"<color=#add8e6>Room List Status:</color> <color={iconColor}><b>{visibilityText}</b></color>\n";

            // Add explanation
            if (isVisible)
            {
                infoText += "<size=12><color=#AAAAAA>This room should appear in room lists</color></size>\n";
            }
            else
            {
                infoText += "<size=12><color=#AAAAAA>This room will NOT appear in room lists</color></size>\n";
            }
        }

        // Update UI text
        _infoText.text = infoText;

        // Adjust background size to fit text content
        if (_backgroundRect != null)
        {
            // Calculate height based on number of lines (approximate)
            int lineCount = infoText.Split('\n').Length;
            float newHeight = lineCount * (_fontSize + 4);

            if (_debugButton != null)
                newHeight += 50; // Add space for debug button

            // Update background size
            _backgroundRect.sizeDelta = new Vector2(350, Mathf.Max(180, newHeight + 20));
        }
    }

    public void SetupDebugButton()
    {
        if (_debugButton == null && _showDebugPanelInMainMenu)
        {
            // First ensure background rect exists
            if (_backgroundRect == null)
            {
                Debug.LogError("Cannot add debug button - background panel not found");
                return;
            }

            // Get current background size to position button
            float currentHeight = _backgroundRect.sizeDelta.y;

            // Create a debug button inside the main panel
            GameObject buttonObj = new GameObject("DebugButton");
            buttonObj.transform.SetParent(_backgroundRect.transform);

            // Add button component
            _debugButton = buttonObj.AddComponent<Button>();
            Image btnImage = buttonObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.4f, 0.8f, 0.8f); // Blue

            // Position button at bottom of panel
            RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0);
            btnRect.anchorMax = new Vector2(0.5f, 0);
            btnRect.pivot = new Vector2(0.5f, 0);
            btnRect.anchoredPosition = new Vector2(0, 10); // 10px from bottom
            btnRect.sizeDelta = new Vector2(220, 30); // Width, height

            // Add text to button
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);

            TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "REFRESH ROOM LIST";
            btnText.fontSize = 14;
            btnText.fontStyle = FontStyles.Bold;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Add status text
            GameObject statusObj = new GameObject("DebugStatus");
            statusObj.transform.SetParent(_backgroundRect.transform);

            _debugStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            _debugStatusText.fontSize = 12;
            _debugStatusText.color = Color.yellow;
            _debugStatusText.alignment = TextAlignmentOptions.Center;
            _debugStatusText.text = "";

            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.anchoredPosition = new Vector2(0, 45); // Above button
            statusRect.sizeDelta = new Vector2(0, 30);

            // Hide status initially
            _debugStatusText.gameObject.SetActive(false);

            // Add click event
            NetworkRunnerHandler networkHandler = FindObjectOfType<NetworkRunnerHandler>();
            if (networkHandler != null)
            {
                _debugButton.onClick.AddListener(() => {
                    networkHandler.ForceRefreshSessions();
                    StartCoroutine(ShowDebugStatus("Refreshing room list..."));
                });
            }

            // Update panel size to accommodate button
            UpdateSessionInfo();
        }
    }

    private IEnumerator ShowDebugStatus(string message)
    {
        if (_debugStatusText != null)
        {
            _debugStatusText.text = message;
            _debugStatusText.gameObject.SetActive(true);

            yield return new WaitForSeconds(3.0f);

            _debugStatusText.gameObject.SetActive(false);
        }
    }

    private string FormatTimespan(int seconds)
    {
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        int secs = seconds % 60;

        return $"{hours:00}:{minutes:00}:{secs:00}";
    }
}