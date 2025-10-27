using UnityEngine;
using UnityEngine.UI; // Keep for Image
using TMPro;
using Fusion;
using System;
using System.Linq; // Keep for player count
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
    [SerializeField] private bool _showInternalSessionID = false;
    [SerializeField] private bool _showIsHostStatus = true;
    [SerializeField] private bool _showLocalPlayerRef = false;

    // Removed GameStateManager reference as it wasn't used
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

    private void SetupUI()
    {
        // Find runner early, might exist already if this object is instantiated later
        if (_runner == null) _runner = FindObjectOfType<NetworkRunner>();

        if (_infoText == null)
        {
            // --- Auto-create UI logic ---
            _canvas = FindObjectOfType<Canvas>();
            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning("SessionInfoDisplay: Creating fallback Canvas.");
                GameObject canvasObj = new GameObject("SessionInfoCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                // Consider DontDestroyOnLoad(canvasObj); if needed across scenes without Bootstrap
            }


            GameObject displayRoot = new GameObject("SessionInfoDisplayRoot");
            displayRoot.transform.SetParent(_canvas.transform, false);
            RectTransform rootRect = displayRoot.AddComponent<RectTransform>();
            // Anchor top-left
            rootRect.anchorMin = new Vector2(0, 1);
            rootRect.anchorMax = new Vector2(0, 1);
            rootRect.pivot = new Vector2(0, 1);
            rootRect.anchoredPosition = _offset; // Apply offset from top-left
            rootRect.sizeDelta = new Vector2(350, 150); // Initial size, will adapt

            if (_showBackground)
            {
                _backgroundImage = displayRoot.AddComponent<Image>();
                _backgroundImage.color = _backgroundColor;
                _backgroundRect = rootRect; // Background uses the root rect transform
                                            // Ensure Image is not raycast target unless needed
                _backgroundImage.raycastTarget = false;
            }

            GameObject textObj = new GameObject("SessionInfoText");
            textObj.transform.SetParent(rootRect, false); // Child of the background/root
            _infoText = textObj.AddComponent<TextMeshProUGUI>();
            _infoText.fontSize = _fontSize;
            _infoText.color = _textColor;
            _infoText.alignment = TextAlignmentOptions.TopLeft;
            _infoText.enableWordWrapping = true;
            _infoText.raycastTarget = false; // Usually not needed for display text

            _textRectTransform = _infoText.rectTransform;
            // Stretch text within the background rect, applying padding
            _textRectTransform.anchorMin = Vector2.zero;
            _textRectTransform.anchorMax = Vector2.one;
            _textRectTransform.offsetMin = _backgroundPadding;        // Bottom-left padding
            _textRectTransform.offsetMax = -_backgroundPadding;       // Top-right padding (negative)

        }
        else
        {
            // --- Find existing elements if _infoText was assigned ---
            _textRectTransform = _infoText.rectTransform;
            if (_showBackground && _backgroundRect == null)
            {
                _backgroundRect = _infoText.transform.parent as RectTransform; // Assume parent holds background
                if (_backgroundRect != null) _backgroundImage = _backgroundRect.GetComponent<Image>();
            }
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        }
    }


    private void Start()
    {
        // Ensure runner is found if not already
        if (_runner == null) _runner = FindObjectOfType<NetworkRunner>();
        UpdateSessionInfo(); // Initial update
    }

    private void Update()
    {
        // Update less frequently for performance
        if (Time.frameCount % 30 == 0) // Roughly 2 times per second at 60fps
        {
            // Runner might disconnect or scene might change, so keep checking/finding
            if (_runner == null || !_runner.IsRunning)
            {
                _runner = FindObjectOfType<NetworkRunner>();
            }
            UpdateSessionInfo();
        }
    }

    private void UpdateSessionInfo()
    {
        if (_infoText == null) return;

        _infoBuilder.Clear();
        _infoBuilder.AppendLine("<color=#yellow><b>SESSION INFO</b></color>");

        // Check if runner exists and is actively running a session
        bool hasRunner = _runner != null && _runner.IsRunning && _runner.SessionInfo != null;

        if (!hasRunner)
        {
            _infoText.text = "Not Connected";
            AdjustBackgroundSize(); // Adjust size even when not connected
            return;
        }

        // --- Runner is valid, proceed ---
        NetworkRunnerHandler handler = _runner.GetComponent<NetworkRunnerHandler>(); // Can be null

        // --- Session Details ---
        if (_showSessionName)
        {
            string name = handler?.SessionDisplayName;
            if (string.IsNullOrEmpty(name))
            {
                if (!_runner.SessionInfo.Properties.TryGetValue("DisplayName", out var nameProp) || string.IsNullOrEmpty(name = nameProp?.PropertyValue as string))
                {
                    name = _runner.SessionInfo.Name ?? "N/A";
                }
            }
            _infoBuilder.AppendLine($"Game: {name}");
        }
        if (_showSessionHash)
        {
            string hash = handler?.SessionHash;
            if (string.IsNullOrEmpty(hash) || hash == "N/A")
            {
                if (_runner.SessionInfo.Properties.TryGetValue("Hash", out var hashProp))
                {
                    hash = hashProp?.PropertyValue?.ToString() ?? "N/A";
                }
                else { hash = "N/A"; }
            }
            _infoBuilder.AppendLine($"Code: {hash}");
        }
        if (_showInternalSessionID)
        {
            _infoBuilder.AppendLine($"<color=#grey>ID: {_runner.SessionInfo.Name}</color>");
        }
        if (_showSessionTime)
        {
            long startTime = handler?.SessionStartTime ?? 0;
            if (startTime == 0 && _runner.SessionInfo.Properties.TryGetValue("StartTime", out var timeProp) && timeProp.PropertyValue is int stVal)
            {
                startTime = stVal;
            }
            if (startTime > 0)
            {
                int elapsedSeconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startTime);
                _infoBuilder.AppendLine($"Time: {FormatTimespan(elapsedSeconds)}");
            }
            else { _infoBuilder.AppendLine("Time: N/A"); }
        }

        // --- Connection/Status Details ---
        if (_showPublicStatus)
        {
            string statusText = _runner.SessionInfo.IsVisible ? "<color=green>Public</color>" : "<color=orange>Private</color>";
            if (!_runner.SessionInfo.IsOpen) statusText += " <color=red>(Closed)</color>";
            _infoBuilder.AppendLine($"Status: {statusText}");
        }
        if (_showRunnerMode)
        {
            _infoBuilder.AppendLine($"Mode: {_runner.Mode}");
        }
        if (_showIsHostStatus)
        {
            // --- CORRECTED: Use IsServer for Host/Server status ---
            string hostStatus = _runner.IsServer ? "Yes (Server/Host)" : "No (Client)";
            _infoBuilder.AppendLine($"Is Server: {hostStatus}");
        }
        if (_showRegion)
        {
            string region = _runner.SessionInfo.Region ?? "N/A";
            _infoBuilder.AppendLine($"Region: {region.ToUpper()}");
        }
        if (_showPing)
        {
            // --- CORRECTED: Check player ref against None ---
            if (_runner.LocalPlayer != PlayerRef.None)
            {
                double rttSeconds = _runner.GetPlayerRtt(_runner.LocalPlayer);
                int pingMs = Mathf.RoundToInt((float)(rttSeconds * 1000));
                _infoBuilder.AppendLine($"Ping: {pingMs} ms");
            }
            else { _infoBuilder.AppendLine($"Ping: N/A"); }
        }

        // --- Player Details ---
        if (_showPlayerCount)
        {
            int playerCount = _runner.ActivePlayers.Count(); // Current players
            int maxPlayers = _runner.SessionInfo.MaxPlayers; // Max allowed
            _infoBuilder.AppendLine($"Players: {playerCount} / {maxPlayers}");
        }
        if (_showLocalPlayerRef)
        {
            _infoBuilder.AppendLine($"<color=#grey>Local Ref: {_runner.LocalPlayer}</color>");
        }


        _infoText.text = _infoBuilder.ToString();
        AdjustBackgroundSize();
    }

    private void AdjustBackgroundSize()
    {
        if (_backgroundRect == null || _infoText == null) return; // Need both elements

        // Ensure canvas is up-to-date for accurate preferred size calculation
        Canvas.ForceUpdateCanvases();

        float preferredWidth = _infoText.preferredWidth;
        float preferredHeight = _infoText.preferredHeight;

        // Calculate background size including padding
        float bgWidth = preferredWidth + _backgroundPadding.x * 2;
        float bgHeight = preferredHeight + _backgroundPadding.y * 2;

        // Optional: Enforce a minimum size for the background
        bgWidth = Mathf.Max(200, bgWidth); // Example minimum width
        bgHeight = Mathf.Max(50, bgHeight); // Example minimum height

        // Apply the calculated size to the background RectTransform
        _backgroundRect.sizeDelta = new Vector2(bgWidth, bgHeight);

        // If the text or background is part of a Layout Group, might need to update layout
        // LayoutRebuilder.ForceRebuildLayoutImmediate(_backgroundRect); // Uncomment if needed
    }


    private string FormatTimespan(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
        return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
    }
}