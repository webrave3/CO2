using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class RoomCodeDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _roomCodeText;
    [SerializeField] private Button _copyButton;

    private NetworkRunnerHandler _networkRunnerHandler;
    private Coroutine _feedbackCoroutine;

    private void Start()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_copyButton != null)
            _copyButton.onClick.AddListener(CopyRoomCode);

        // Update display periodically or when network state changes
        // Using InvokeRepeating is simple, but event-driven updates are better if possible
        InvokeRepeating(nameof(UpdateRoomCodeDisplay), 0.5f, 1.0f);
    }

    private void UpdateRoomCodeDisplay()
    {
        if (_roomCodeText == null || _networkRunnerHandler == null)
            return;

        // Display code only if Runner exists, is running, and hash is available
        if (_networkRunnerHandler.Runner != null &&
            _networkRunnerHandler.Runner.IsRunning &&
            !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
        {
            _roomCodeText.text = $"Code: {_networkRunnerHandler.SessionHash}";
            if (_copyButton != null) _copyButton.interactable = true;
        }
        else
        {
            _roomCodeText.text = "No Room Code";
            if (_copyButton != null) _copyButton.interactable = false;
        }
    }

    private void CopyRoomCode()
    {
        if (_networkRunnerHandler != null && !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
        {
            GUIUtility.systemCopyBuffer = _networkRunnerHandler.SessionHash;

            // Stop previous feedback if running
            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = StartCoroutine(ShowCopiedFeedback());
        }
    }

    private IEnumerator ShowCopiedFeedback()
    {
        if (_roomCodeText == null) yield break; // Need text element for feedback

        string originalText = _roomCodeText.text; // Store current text
        Color originalColor = _roomCodeText.color;
        _roomCodeText.text = "Copied!";
        _roomCodeText.color = Color.green; // Example feedback color

        yield return new WaitForSeconds(1.5f);

        // Restore only if the code hasn't changed in the meantime
        if (_roomCodeText.text == "Copied!")
        {
            UpdateRoomCodeDisplay(); // Refresh to current code
            _roomCodeText.color = originalColor;
        }
        _feedbackCoroutine = null;
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(UpdateRoomCodeDisplay));
        if (_copyButton != null) _copyButton.onClick.RemoveListener(CopyRoomCode);
    }
}