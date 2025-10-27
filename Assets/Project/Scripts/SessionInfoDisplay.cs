using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System;
using System.Linq; // Keep for player count if needed
using System.Text; // Added for StringBuilder

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
    [SerializeField] private bool _showPublicStatus = true;   // Renamed from _showVisibilityStatus
    [SerializeField] private bool _showPing = true;
    [SerializeField] private bool _showRegion = true;
    [SerializeField] private bool _showRunnerMode = true;
    [SerializeField] private bool _showInternalSessionID = false; // <-- ADDED: Photon's internal session name/ID
    [SerializeField] private bool _showIsHostStatus = true;     // <-- ADDED: Indicate if local player is host/server
    [SerializeField] private bool _showLocalPlayerRef = false;    // <-- ADDED: Show local PlayerRef

    private GameStateManager _gameStateManager; // Still useful for potentially more persistent data if needed
    private NetworkRunner _runner;
    private Canvas _canvas;
    private RectTransform _textRectTransform;
    private RectTransform _backgroundRect;
    private Image _backgroundImage;

    private readonly StringBuilder _infoBuilder = new StringBuilder(); // Reuse StringBuilder

    private void Awake()
    {
        SetupUI();
    }

    // --- SetupUI() method remains the same as the previous version ---
    private void SetupUI()
    {
        _runner = FindObjectOfType<NetworkRunner>(); // Find runner early

        if (_infoText == null)
        {
            // --- Auto-create UI logic (kept from previous version) ---
            _canvas = FindObjectOfType<Canvas>();
            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                GameObject canvasObj = new GameObject("SessionInfoCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                // Optionally: DontDestroyOnLoad(canvasObj);
            }

            GameObject displayRoot = new GameObject("SessionInfoDisplayRoot");
            displayRoot.transform.SetParent(_canvas.transform, false);
            RectTransform rootRect = displayRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0, 1);
            rootRect.anchorMax = new Vector2(0, 1);
            rootRect.pivot = new Vector2(0, 1);
            rootRect.anchoredPosition = _offset;
            rootRect.sizeDelta = new Vector2(350, 150); // Initial size, will adapt

            if (_showBackground)
            {
                _backgroundImage = displayRoot.AddComponent<Image>();
                _backgroundImage.color = _backgroundColor;
                _backgroundRect = rootRect;
            }

            GameObject textObj = new GameObject("SessionInfoText");
            textObj.transform.SetParent(rootRect, false);
            _infoText = textObj.AddComponent<TextMeshProUGUI>();
            _infoText.fontSize = _fontSize;
            _infoText.color = _textColor;
            _infoText.alignment = TextAlignmentOptions.TopLeft;
            _infoText.enableWordWrapping = true;
            _infoText.raycastTarget = false;

            _textRectTransform = _infoText.rectTransform;
            _textRectTransform.anchorMin = Vector2.zero;
            _textRectTransform.anchorMax = Vector2.one;
            _textRectTransform.offsetMin = _backgroundPadding;
            _textRectTransform.offsetMax = -_backgroundPadding;
        }
        else
        {
            // --- Find existing elements logic (kept from previous version) ---
            _textRectTransform = _infoText.rectTransform;
            if (_showBackground && _backgroundRect == null)
            {
                _backgroundRect = _infoText.transform.parent as RectTransform;
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
        // Update less frequently
        if (Time.frameCount % 30 == 0) // ~2 times per second
        {
            if (_runner == null) _runner = FindObjectOfType<NetworkRunner>();
            // GameStateManager might still be useful, keep the check
            if (_gameStateManager == null) _gameStateManager = FindObjectOfType<GameStateManager>();
            UpdateSessionInfo();
        }
    }

    private void UpdateSessionInfo()
    {
        if (_infoText == null) return;

        _infoBuilder.Clear();
        _infoBuilder.AppendLine("<color=#yellow><b>SESSION INFO</b></color>");

        bool hasRunner = _runner != null && _runner.IsRunning;

        if (!hasRunner)
        {
            _infoText.text = "Not Connected";
            AdjustBackgroundSize();
            return;
        }

        // Get handler for potentially stored info (like original hash/name)
        NetworkRunnerHandler handler = _runner.GetComponent<NetworkRunnerHandler>();

        // --- Session Details ---
        if (_showSessionName)
        {
            string name = handler?.SessionDisplayName ?? _runner.SessionInfo?.Name ?? "N/A";
            _infoBuilder.AppendLine($"Game: {name}");
        }
        if (_showSessionHash)
        {
            string hash = handler?.SessionHash ?? "N/A";
            if (hash == "N/A" && _runner.SessionInfo != null && _runner.SessionInfo.Properties.TryGetValue("Hash", out var hashProp))
            {
                hash = hashProp?.PropertyValue?.ToString() ?? "N/A";
            }
            _infoBuilder.AppendLine($"Code: {hash}");
        }
        if (_showInternalSessionID && _runner.SessionInfo != null) // <-- ADDED
        {
            _infoBuilder.AppendLine($"<color=#grey>ID: {_runner.SessionInfo.Name}</color>"); // Show Photon internal ID
        }
        if (_showSessionTime)
        {
            long startTime = handler?.SessionStartTime ?? 0;
            if (startTime == 0 && _runner.SessionInfo != null && _runner.SessionInfo.Properties.TryGetValue("StartTime", out var timeProp) && timeProp.PropertyValue is int stVal)
            {
                startTime = stVal;
            }
            if (startTime > 0)
            {
                int elapsedSeconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startTime);
                _infoBuilder.AppendLine($"Time: {FormatTimespan(elapsedSeconds)}");
            }
            else
            {
                _infoBuilder.AppendLine("Time: N/A");
            }
        }

        // --- Connection/Status Details ---
        if (_showPublicStatus && _runner.SessionInfo != null) // Renamed field, logic is the same
        {
            string statusText = _runner.SessionInfo.IsVisible ? "<color=green>Public</color>" : "<color=orange>Private</color>";
            if (!_runner.SessionInfo.IsOpen) statusText += " <color=red>(Closed)</color>"; // Also show if closed
            _infoBuilder.AppendLine($"Status: {statusText}");
        }
        if (_showRunnerMode)
        {
            _infoBuilder.AppendLine($"Mode: {_runner.Mode}");
        }
        if (_showIsHostStatus) // <-- ADDED
        {
            string hostStatus = _runner.IsServer ? "Yes" : "No";
            _infoBuilder.AppendLine($"Is Host: {hostStatus}");
        }
        if (_showRegion && _runner.SessionInfo != null)
        {
            string region = _runner.SessionInfo.Region ?? "N/A";
            _infoBuilder.AppendLine($"Region: {region.ToUpper()}");
        }
        if (_showPing)
        {
            double rttSeconds = _runner.GetPlayerRtt(_runner.LocalPlayer);
            int pingMs = (int)(rttSeconds * 1000);
            _infoBuilder.AppendLine($"Ping: {pingMs} ms");
        }

        // --- Player Details ---
        if (_showPlayerCount)
        {
            int playerCount = _runner.ActivePlayers.Count();
            int maxPlayers = _runner.SessionInfo?.MaxPlayers ?? _runner.Config.Simulation.PlayerCount;
            _infoBuilder.AppendLine($"Players: {playerCount} / {maxPlayers}");
        }
        if (_showLocalPlayerRef) // <-- ADDED
        {
            _infoBuilder.AppendLine($"<color=#grey>Local Ref: {_runner.LocalPlayer}</color>");
        }


        _infoText.text = _infoBuilder.ToString();
        AdjustBackgroundSize();
    }

    // --- AdjustBackgroundSize() method remains the same ---
    private void AdjustBackgroundSize()
    {
        if (_backgroundRect != null && _infoText != null)
        {
            float preferredWidth = _infoText.preferredWidth;
            float preferredHeight = _infoText.preferredHeight;
            float bgWidth = preferredWidth + _backgroundPadding.x * 2;
            float bgHeight = preferredHeight + _backgroundPadding.y * 2;
            bgWidth = Mathf.Max(200, bgWidth);
            bgHeight = Mathf.Max(50, bgHeight);
            _backgroundRect.sizeDelta = new Vector2(bgWidth, bgHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_textRectTransform);
        }
    }


    // --- FormatTimespan() method remains the same ---
    private string FormatTimespan(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
        return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
    }
}