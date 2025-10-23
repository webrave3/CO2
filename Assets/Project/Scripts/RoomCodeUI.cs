using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
// Removed: using System.Diagnostics; // Not needed without Debug

public class RoomCodeUI : MonoBehaviour
{
    [Header("Host UI")]
    [SerializeField] private TextMeshProUGUI _roomCodeDisplay;
    [SerializeField] private Button _copyCodeButton;

    [Header("Join UI")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _joinButton;
    [SerializeField] private GameObject _joiningIndicator;
    [SerializeField] private TextMeshProUGUI _errorMessageText;

    [Header("Settings")]
    [SerializeField] private float _errorMessageDuration = 3f;

    private NetworkRunnerHandler _networkRunnerHandler;
    private bool _isJoining = false;
    private Coroutine _feedbackCoroutine;
    private Coroutine _errorCoroutine;

    private void Awake()
    {
        if (_errorMessageText != null) _errorMessageText.gameObject.SetActive(false);
        if (_joiningIndicator != null) _joiningIndicator.SetActive(false);
    }

    private void Start()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        // Add null check for safety
        if (_networkRunnerHandler == null)
        {
            // Disable UI or show an error if handler is critical
            if (_roomCodeDisplay != null) _roomCodeDisplay.text = "Network Error";
            if (_copyCodeButton != null) _copyCodeButton.interactable = false;
            if (_joinButton != null) _joinButton.interactable = false;
            if (_roomCodeInput != null) _roomCodeInput.interactable = false;
            return;
        }


        if (_copyCodeButton != null) _copyCodeButton.onClick.AddListener(CopyRoomCodeToClipboard);
        if (_joinButton != null) _joinButton.onClick.AddListener(JoinRoomByCode);

        // Initial update
        UpdateRoomCodeDisplay();
    }

    // Called periodically or on state change
    private void UpdateRoomCodeDisplay()
    {
        if (_roomCodeDisplay == null || _networkRunnerHandler == null) return;

        bool hasCode = _networkRunnerHandler.Runner != null &&
                       _networkRunnerHandler.Runner.IsRunning &&
                       !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash);

        _roomCodeDisplay.text = hasCode ? FormatRoomCodeForDisplay(_networkRunnerHandler.SessionHash) : "No Session";
        if (_copyCodeButton != null) _copyCodeButton.interactable = hasCode;
    }

    private string FormatRoomCodeForDisplay(string code)
    {
        // Example: Keep CamelCase like "RedWolf"
        return code;
        // Or add space: return System.Text.RegularExpressions.Regex.Replace(code, "([A-Z])", " $1").Trim();
    }

    private void CopyRoomCodeToClipboard()
    {
        if (_networkRunnerHandler != null && !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
        {
            GUIUtility.systemCopyBuffer = _networkRunnerHandler.SessionHash;
            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = StartCoroutine(ShowCopiedFeedback());
        }
    }

    private IEnumerator ShowCopiedFeedback()
    {
        if (_roomCodeDisplay == null) yield break;
        string originalText = _roomCodeDisplay.text;
        _roomCodeDisplay.text = "Copied!";
        yield return new WaitForSeconds(1.5f);
        // Restore only if text wasn't updated by something else
        if (_roomCodeDisplay.text == "Copied!") _roomCodeDisplay.text = originalText;
        _feedbackCoroutine = null;
    }

    private async void JoinRoomByCode()
    {
        if (_isJoining || _networkRunnerHandler == null || _roomCodeInput == null) return;

        string roomCode = _roomCodeInput.text.Trim();
        if (string.IsNullOrEmpty(roomCode))
        {
            ShowErrorMessage("Please enter a room code");
            return;
        }

        roomCode = FormatRoomCodeInput(roomCode); // Clean up user input

        _isJoining = true;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(true);
        if (_joinButton != null) _joinButton.interactable = false;
        if (_errorMessageText != null) _errorMessageText.gameObject.SetActive(false); // Hide previous errors

        try
        {
            await _networkRunnerHandler.StartClientGameByHash(roomCode);

            // Check if join succeeded after a short delay (scene change handles success)
            await System.Threading.Tasks.Task.Delay(1000); // Wait 1 second

            if (_isJoining && // Still in joining state?
                (_networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning || string.IsNullOrEmpty(_networkRunnerHandler.SessionUniqueID)))
            {
                ShowErrorMessage("Could not find or join game with that code");
                ResetJoiningUI();
            }
            else if (_isJoining)
            {
                // Connected or scene changed, just reset UI state silently
                ResetJoiningUI();
            }
        }
        catch (System.Exception ex) // Catch potential exceptions during StartClientGameByHash
        {
            ShowErrorMessage($"Error joining: {ex.Message}");
            ResetJoiningUI();
        }
    }

    private void ResetJoiningUI()
    {
        _isJoining = false;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(false);
        if (_joinButton != null) _joinButton.interactable = true;
    }

    private string FormatRoomCodeInput(string input)
    {
        // Basic cleanup: Remove spaces and ensure PascalCase (e.g., "red wolf" -> "RedWolf")
        string cleanInput = "";
        bool capitalizeNext = true;
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                cleanInput += capitalizeNext ? char.ToUpper(c) : c;
                capitalizeNext = false;
            }
            else if (char.IsWhiteSpace(c)) // Capitalize after space
            {
                capitalizeNext = true;
            }
        }
        return cleanInput;
    }

    private void ShowErrorMessage(string message)
    {
        if (_errorMessageText != null)
        {
            _errorMessageText.text = message;
            _errorMessageText.gameObject.SetActive(true);
            if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
            _errorCoroutine = StartCoroutine(HideErrorAfterDelay());
        }
    }

    private IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSeconds(_errorMessageDuration);
        if (_errorMessageText != null) _errorMessageText.gameObject.SetActive(false);
        _errorCoroutine = null;
    }

    // Update display if running as host
    private void Update()
    {
        if (_networkRunnerHandler != null &&
            _networkRunnerHandler.Runner != null &&
            _networkRunnerHandler.Runner.IsServer)
        {
            // Update display less frequently if needed
            if (Time.frameCount % 30 == 0) // Example: Update twice per second
                UpdateRoomCodeDisplay();
        }
    }

    private void OnDestroy()
    {
        if (_copyCodeButton != null) _copyCodeButton.onClick.RemoveListener(CopyRoomCodeToClipboard);
        if (_joinButton != null) _joinButton.onClick.RemoveListener(JoinRoomByCode);
        // Stop coroutines
        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
        if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
    }
}