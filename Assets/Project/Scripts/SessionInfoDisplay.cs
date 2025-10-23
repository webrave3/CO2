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
    [SerializeField] private Vector2 _offset = new Vector2(20, 20); // Position offset
    [SerializeField] private bool _showBackground = true;
    [SerializeField] private Color _backgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private Vector2 _backgroundPadding = new Vector2(10, 10); // Padding inside background

    [Header("Display Options")]
    [SerializeField] private bool _showSessionName = true;
    [SerializeField] private bool _showSessionHash = true;
    [SerializeField] private bool _showSessionTime = true;
    [SerializeField] private bool _showPlayerCount = true;
    [SerializeField] private bool _showVisibilityStatus = true; // Useful info, not just debug

    private GameStateManager _gameStateManager;
    private NetworkRunner _runner;
    private Canvas _canvas;
    private RectTransform _textRectTransform;
    private RectTransform _backgroundRect;
    private Image _backgroundImage; // Added reference for background

    private void Awake()
    {
        // Find or create UI elements
        SetupUI();
    }

    private void SetupUI()
    {
        _runner = FindObjectOfType<NetworkRunner>(); // Find runner early

        if (_infoText == null)
        {
            // Ensure a Canvas exists in the scene or create one
            _canvas = FindObjectOfType<Canvas>();
            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                GameObject canvasObj = new GameObject("SessionInfoCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>(); // Add scaler for responsiveness
                canvasObj.AddComponent<GraphicRaycaster>();
                // Optionally make it persistent if needed across scenes: DontDestroyOnLoad(canvasObj);
            }


            GameObject displayRoot = new GameObject("SessionInfoDisplayRoot");
            displayRoot.transform.SetParent(_canvas.transform, false); // Parent to canvas
            RectTransform rootRect = displayRoot.AddComponent<RectTransform>();

            // Configure root RectTransform for top-left positioning
            rootRect.anchorMin = new Vector2(0, 1);
            rootRect.anchorMax = new Vector2(0, 1);
            rootRect.pivot = new Vector2(0, 1);
            rootRect.anchoredPosition = _offset;
            rootRect.sizeDelta = new Vector2(350, 150); // Initial size, will adapt


            if (_showBackground)
            {
                _backgroundImage = displayRoot.AddComponent<Image>();
                _backgroundImage.color = _backgroundColor;
                _backgroundRect = rootRect; // Background is the root object
            }

            // Create Text object as child of the root/background
            GameObject textObj = new GameObject("SessionInfoText");
            textObj.transform.SetParent(rootRect, false); // Parent to root/background

            _infoText = textObj.AddComponent<TextMeshProUGUI>();
            _infoText.fontSize = _fontSize;
            _infoText.color = _textColor;
            _infoText.alignment = TextAlignmentOptions.TopLeft;
            _infoText.enableWordWrapping = true;
            _infoText.raycastTarget = false; // Prevent blocking UI behind it


            _textRectTransform = _infoText.rectTransform;
            _textRectTransform.anchorMin = Vector2.zero; // Stretch to fill parent
            _textRectTransform.anchorMax = Vector2.one;
            _textRectTransform.offsetMin = _backgroundPadding; // Apply padding
            _textRectTransform.offsetMax = -_backgroundPadding;
        }
        else
        {
            // If _infoText is assigned, try to find its parent elements
            _textRectTransform = _infoText.rectTransform;
            if (_showBackground && _backgroundRect == null)
            {
                _backgroundRect = _infoText.transform.parent as RectTransform; // Assume parent is background
                if (_backgroundRect != null) _backgroundImage = _backgroundRect.GetComponent<Image>();
            }
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        }
    }


    private void Start()
    {
        // Get GameStateManager reference (might be null initially)
        _gameStateManager = FindObjectOfType<GameStateManager>();
        UpdateSessionInfo(); // Initial update
    }

    private void Update()
    {
        // Update less frequently to save performance
        if (Time.frameCount % 30 == 0) // Update ~2 times per second
        {
            // Ensure references are valid (especially after scene loads)
            if (_runner == null) _runner = FindObjectOfType<NetworkRunner>();
            if (_gameStateManager == null) _gameStateManager = FindObjectOfType<GameStateManager>();

            UpdateSessionInfo();
        }
    }

    private void UpdateSessionInfo()
    {
        if (_infoText == null) return;

        System.Text.StringBuilder infoBuilder = new System.Text.StringBuilder();
        infoBuilder.AppendLine("<color=#yellow><b>SESSION INFO</b></color>"); // Example header

        bool hasRunner = _runner != null && _runner.IsRunning;
        bool hasGameState = _gameStateManager != null;

        if (!hasRunner && !hasGameState)
        {
            _infoText.text = "Not Connected";
            AdjustBackgroundSize();
            return;
        }


        // Use GameStateManager for persistent info if available
        if (hasGameState)
        {
            if (_showSessionName && !string.IsNullOrEmpty(_gameStateManager.SessionDisplayName.ToString()))
                infoBuilder.AppendLine($"Game: {_gameStateManager.SessionDisplayName}");
            if (_showSessionHash && !string.IsNullOrEmpty(_gameStateManager.SessionHash.ToString()))
                infoBuilder.AppendLine($"Code: {_gameStateManager.SessionHash}");
            if (_showSessionTime && _gameStateManager.SessionStartTime > 0)
            {
                int elapsedSeconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _gameStateManager.SessionStartTime;
                infoBuilder.AppendLine($"Time: {FormatTimespan(elapsedSeconds)}");
            }
        }
        // Use Runner for dynamic info
        else if (hasRunner) // Fallback if GameStateManager isn't ready/available
        {
            NetworkRunnerHandler handler = FindObjectOfType<NetworkRunnerHandler>(); // Temp find
            if (handler != null)
            {
                if (_showSessionName && !string.IsNullOrEmpty(handler.SessionDisplayName)) infoBuilder.AppendLine($"Game: {handler.SessionDisplayName}");
                if (_showSessionHash && !string.IsNullOrEmpty(handler.SessionHash)) infoBuilder.AppendLine($"Code: {handler.SessionHash}");
            }
        }


        if (hasRunner)
        {
            if (_showPlayerCount)
            {
                int playerCount = _runner.ActivePlayers.Count();
                int maxPlayers = _runner.SessionInfo?.MaxPlayers ?? _runner.Config.Simulation.PlayerCount; // Use PlayerCount from config
                infoBuilder.AppendLine($"Players: {playerCount} / {maxPlayers}");
            }

            if (_showVisibilityStatus && _runner.SessionInfo != null)
            {
                string visibilityText = _runner.SessionInfo.IsVisible ? "<color=green>VISIBLE</color>" : "<color=red>HIDDEN</color>";
                infoBuilder.AppendLine($"Listing: {visibilityText}");
            }
        }


        _infoText.text = infoBuilder.ToString();
        AdjustBackgroundSize();
    }

    private void AdjustBackgroundSize()
    {
        if (_backgroundRect != null && _infoText != null)
        {
            // Force update preferred height
            LayoutRebuilder.ForceRebuildLayoutImmediate(_textRectTransform);
            float preferredHeight = _infoText.preferredHeight;

            // Calculate required background size
            float bgHeight = preferredHeight + _backgroundPadding.y * 2;
            float bgWidth = _backgroundRect.sizeDelta.x; // Keep existing width or set dynamically

            _backgroundRect.sizeDelta = new Vector2(bgWidth, Mathf.Max(50, bgHeight)); // Ensure minimum size
        }
    }


    private string FormatTimespan(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
        // Format as HH:MM:SS or MM:SS depending on duration
        return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
    }
}