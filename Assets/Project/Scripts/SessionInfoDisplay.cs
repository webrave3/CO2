using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System;
using System.Linq;

public class SessionInfoDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private TextMeshProUGUI _infoText;
    [SerializeField] private Color _textColor = new Color(0.8f, 0.8f, 1f, 0.9f);
    [SerializeField] private int _fontSize = 16;
    [SerializeField] private Vector2 _offset = new Vector2(10, 10);
    [SerializeField] private bool _showBackground = true;
    [SerializeField] private Color _backgroundColor = new Color(0, 0, 0, 0.5f);

    [Header("Display Options")]
    [SerializeField] private bool _showSessionName = true;
    [SerializeField] private bool _showSessionHash = true;
    [SerializeField] private bool _showSessionTime = true;
    [SerializeField] private bool _showPlayerCount = true;

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

                // Set up RectTransform
                _backgroundRect = bgImage.rectTransform;
                _backgroundRect.anchorMin = new Vector2(0, 1);
                _backgroundRect.anchorMax = new Vector2(0, 1);
                _backgroundRect.pivot = new Vector2(0, 1);
                _backgroundRect.anchoredPosition = new Vector2(_offset.x - 5, _offset.y - 40);
                _backgroundRect.sizeDelta = new Vector2(300, 100);
            }

            // Create text object
            GameObject textObj = new GameObject("SessionInfoText");
            textObj.transform.SetParent(canvasObj.transform);

            _infoText = textObj.AddComponent<TextMeshProUGUI>();
            _infoText.fontSize = _fontSize;
            _infoText.color = _textColor;
            _infoText.alignment = TextAlignmentOptions.TopLeft;
            _infoText.enableWordWrapping = false;

            // Position text in top-left corner
            _rectTransform = _infoText.GetComponent<RectTransform>();
            _rectTransform.anchorMin = new Vector2(0, 1);
            _rectTransform.anchorMax = new Vector2(0, 1);
            _rectTransform.pivot = new Vector2(0, 1);
            _rectTransform.anchoredPosition = new Vector2(_offset.x, _offset.y - 40);
            _rectTransform.sizeDelta = new Vector2(290, 100);
        }

        UnityEngine.Debug.Log("SessionInfoDisplay initialized");
    }

    private void Start()
    {
        // Get reference to GameStateManager
        _gameStateManager = FindObjectOfType<GameStateManager>();

        if (_gameStateManager == null)
        {
            UnityEngine.Debug.LogWarning("SessionInfoDisplay could not find GameStateManager. Session info may not display correctly.");
        }

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
                infoText += $"<color=#add8e6>Players:</color> {playerCount}/{maxPlayers}";
            }
            else
            {
                infoText += $"<color=#add8e6>Players:</color> {playerCount}";
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

            // Update background size
            _backgroundRect.sizeDelta = new Vector2(300, newHeight + 10);
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