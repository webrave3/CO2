using UnityEngine;
using UnityEngine.UI;
using Fusion;
using Fusion.Addons.SimpleKCC;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class MouseInputDebugger : NetworkBehaviour
{
    [Header("Debug Display")]
    [SerializeField] private GameObject _debugPanelPrefab;
    [SerializeField] private TextMeshProUGUI _debugText;
    [SerializeField] private Vector2 _debugPanelPosition = new Vector2(10, 10);

    private SimpleKCC _kcc;
    private NetworkObject _networkObject;
    private Camera _camera;
    private PlayerController _controller;

    private List<string> _logHistory = new List<string>();
    private int _maxLogCount = 20;
    private float _logStartTime;

    // Debug data structures
    private Vector2 _lastMouseDelta;
    private Vector2 _lastLookRotation;
    private Vector2 _lastCameraRotation;
    private int _mouseMoveCount = 0;
    private int _mouseResetCount = 0;
    private float _lastMoveTime;
    private string _lastResetReason = "";

    private void Awake()
    {
        _kcc = GetComponent<SimpleKCC>();
        _networkObject = GetComponent<NetworkObject>();
        _camera = GetComponentInChildren<Camera>();
        _controller = GetComponent<PlayerController>();
        _logStartTime = Time.time;

        CreateDebugPanel();
    }

    private void CreateDebugPanel()
    {
        if (_debugPanelPrefab == null)
        {
            GameObject canvasGO = new GameObject("DebugCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();

            GameObject panelGO = new GameObject("DebugPanel");
            panelGO.transform.SetParent(canvasGO.transform);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(600, 400);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = _debugPanelPosition;

            UnityEngine.UI.Image panelImage = panelGO.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            GameObject textGO = new GameObject("DebugText");
            textGO.transform.SetParent(panelGO.transform);

            _debugText = textGO.AddComponent<TextMeshProUGUI>();
            _debugText.rectTransform.anchorMin = Vector2.zero;
            _debugText.rectTransform.anchorMax = Vector2.one;
            _debugText.rectTransform.sizeDelta = Vector2.zero;
            _debugText.rectTransform.offsetMin = new Vector2(10, 10);
            _debugText.rectTransform.offsetMax = new Vector2(-10, -10);
            _debugText.fontSize = 16;
            _debugText.color = Color.yellow;
            _debugText.alignment = TextAlignmentOptions.TopLeft;
            _debugText.enableWordWrapping = true;

            DontDestroyOnLoad(canvasGO);
        }
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            AddLog($"Local player spawned - Authority: {HasInputAuthority}");
        }
        else
        {
            // Destroy debug panel on remote players
            if (_debugText != null)
                Destroy(_debugText.transform.parent.parent.gameObject);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;

        if (GetInput(out NetworkInputData data))
        {
            if (data.MouseDelta.magnitude > 0.01f)
            {
                _lastMouseDelta = data.MouseDelta;
                _lastMoveTime = Time.time;
                _mouseMoveCount++;

                AddLog($"MOUSE: Delta={data.MouseDelta.ToString("F3")} Time={_lastMoveTime:F2}");
            }
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority) return;

        if (_kcc != null)
        {
            Vector2 currentRotation = _kcc.GetLookRotation(true, true);
            Vector2 rotationChange = currentRotation - _lastLookRotation;

            if (rotationChange.magnitude > 0.01f && _lastLookRotation != Vector2.zero)
            {
                AddLog($"KCC ROT: {currentRotation.ToString("F2")} Change={rotationChange.ToString("F2")}");
            }

            // Check for unexpected resets
            if (rotationChange.magnitude > 90f || (Time.time - _lastMoveTime > 0.1f && rotationChange.magnitude > 1f))
            {
                _mouseResetCount++;
                _lastResetReason = $"Large change: {rotationChange.magnitude:F1}° at time {Time.time:F2}";
                AddLog($"RESET DETECTED: {_lastResetReason}");
            }

            _lastLookRotation = currentRotation;
        }

        if (_camera != null)
        {
            Vector3 camEuler = _camera.transform.localEulerAngles;
            _lastCameraRotation = new Vector2(camEuler.x, camEuler.y);
        }

        UpdateDebugDisplay();
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        // Press C to copy debug info to clipboard
        if (Input.GetKeyDown(KeyCode.C))
        {
            CopyDebugToClipboard();
        }

        // Press L to clear log
        if (Input.GetKeyDown(KeyCode.L))
        {
            _logHistory.Clear();
            _mouseMoveCount = 0;
            _mouseResetCount = 0;
            _lastResetReason = "";
        }
    }

    private void UpdateDebugDisplay()
    {
        if (_debugText == null) return;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("===== MOUSE INPUT DEBUG =====");
        sb.AppendLine($"Time: {Time.time:F2} | Session: {Time.time - _logStartTime:F2}s");
        sb.AppendLine("");

        // Network Status
        sb.AppendLine("NETWORK STATUS:");
        sb.AppendLine($"HasInputAuth: {HasInputAuthority}");
        sb.AppendLine($"InputAuthority: {(_networkObject ? _networkObject.InputAuthority : "null")}");
        sb.AppendLine($"LocalPlayer: {(Runner != null ? Runner.LocalPlayer : "null")}");
        sb.AppendLine($"Runner.IsClient: {(Runner != null ? Runner.IsClient : false)}");
        sb.AppendLine("");

        // Mouse Input Stats
        sb.AppendLine("MOUSE STATS:");
        sb.AppendLine($"Move Count: {_mouseMoveCount}");
        sb.AppendLine($"Reset Count: {_mouseResetCount}");
        sb.AppendLine($"Last Delta: {_lastMouseDelta.ToString("F3")}");
        sb.AppendLine($"Last Reset: {_lastResetReason}");
        sb.AppendLine("");

        // SimpleKCC Status
        if (_kcc != null)
        {
            sb.AppendLine("SIMPLEKCC STATUS:");
            Vector2 rotation = _kcc.GetLookRotation(true, true);
            sb.AppendLine($"Current Rot: {rotation.ToString("F2")}");
            sb.AppendLine($"IsGrounded: {_kcc.IsGrounded}");
        }

        // Camera Status
        if (_camera != null)
        {
            sb.AppendLine("CAMERA STATUS:");
            sb.AppendLine($"Active: {_camera.gameObject.activeSelf}");
            sb.AppendLine($"Local Rot: {_lastCameraRotation.ToString("F2")}");
        }

        sb.AppendLine("");
        sb.AppendLine("RECENT LOGS:");
        int startIdx = Mathf.Max(0, _logHistory.Count - 10);
        for (int i = startIdx; i < _logHistory.Count; i++)
        {
            sb.AppendLine(_logHistory[i]);
        }

        sb.AppendLine("");
        sb.AppendLine("Press C to copy to clipboard");
        sb.AppendLine("Press L to clear logs");

        _debugText.text = sb.ToString();
    }

    private void AddLog(string message)
    {
        string timeStamp = $"[{Time.time:F2}]";
        string fullMessage = $"{timeStamp} {message}";
        _logHistory.Add(fullMessage);

        if (_logHistory.Count > _maxLogCount)
        {
            _logHistory.RemoveAt(0);
        }
    }

    private void CopyDebugToClipboard()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("===== COMPLETE DEBUG REPORT =====");
        sb.AppendLine($"Timestamp: {System.DateTime.Now}");
        sb.AppendLine($"Unity Version: {UnityEngine.Application.unityVersion}");
        sb.AppendLine("");

        // System Info
        sb.AppendLine("SYSTEM INFO:");
        sb.AppendLine($"Platform: {UnityEngine.Application.platform}");
        sb.AppendLine($"FPS: {1f / Time.deltaTime:F1}");
        sb.AppendLine("");

        // Network Info
        sb.AppendLine("NETWORK INFO:");
        sb.AppendLine($"Session Time: {Time.time - _logStartTime:F2}s");
        sb.AppendLine($"HasInputAuthority: {HasInputAuthority}");
        sb.AppendLine($"InputAuthority: {(_networkObject ? _networkObject.InputAuthority : "null")}");
        sb.AppendLine($"HasStateAuthority: {(_networkObject ? _networkObject.HasStateAuthority : false)}");
        sb.AppendLine($"Runner.LocalPlayer: {(Runner != null ? Runner.LocalPlayer : "null")}");
        sb.AppendLine($"Runner.IsClient: {(Runner != null ? Runner.IsClient : false)}");
        sb.AppendLine($"Runner.IsServer: {(Runner != null ? Runner.IsServer : false)}");
        sb.AppendLine("");

        // Mouse Stats
        sb.AppendLine("MOUSE STATISTICS:");
        sb.AppendLine($"Total Mouse Moves: {_mouseMoveCount}");
        sb.AppendLine($"Total Resets: {_mouseResetCount}");
        sb.AppendLine($"Reset Ratio: {(_mouseMoveCount > 0 ? (_mouseResetCount / (float)_mouseMoveCount * 100) : 0):F1}%");
        sb.AppendLine($"Last Mouse Delta: {_lastMouseDelta}");
        sb.AppendLine($"Last Reset Reason: {_lastResetReason}");
        sb.AppendLine("");

        // SimpleKCC Debug
        if (_kcc != null)
        {
            sb.AppendLine("SIMPLEKCC DEBUG:");
            sb.AppendLine($"Current Look Rotation: {_kcc.GetLookRotation(true, true)}");
            sb.AppendLine($"Transform Rotation: {_kcc.Transform.rotation.eulerAngles}");
            sb.AppendLine($"IsGrounded: {_kcc.IsGrounded}");
            sb.AppendLine("");
        }

        // Camera Debug
        if (_camera != null)
        {
            sb.AppendLine("CAMERA DEBUG:");
            sb.AppendLine($"Active: {_camera.gameObject.activeSelf}");
            sb.AppendLine($"Local Rotation: {_camera.transform.localRotation.eulerAngles}");
            sb.AppendLine($"World Rotation: {_camera.transform.rotation.eulerAngles}");
            sb.AppendLine($"Position: {_camera.transform.position}");
            sb.AppendLine("");
        }

        // Full Log History
        sb.AppendLine("LOG HISTORY:");
        foreach (string log in _logHistory)
        {
            sb.AppendLine(log);
        }

        string report = sb.ToString();
        GUIUtility.systemCopyBuffer = report;

        Debug.Log("Debug report copied to clipboard!");
        AddLog("Debug report copied to clipboard");
    }
}