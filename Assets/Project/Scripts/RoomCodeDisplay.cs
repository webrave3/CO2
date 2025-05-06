// Create a new script called RoomCodeDisplay.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class RoomCodeDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _roomCodeText;
    [SerializeField] private Button _copyButton;

    private NetworkRunnerHandler _networkRunnerHandler;

    private void Start()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_copyButton != null)
            _copyButton.onClick.AddListener(CopyRoomCode);

        // Start periodic updates
        InvokeRepeating("UpdateRoomCodeDisplay", 0.5f, 1.0f);
    }

    private void UpdateRoomCodeDisplay()
    {
        if (_roomCodeText == null || _networkRunnerHandler == null)
            return;

        if (_networkRunnerHandler.Runner != null && _networkRunnerHandler.Runner.IsRunning &&
            !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
        {
            _roomCodeText.text = $"Room Code: {_networkRunnerHandler.SessionHash}";
        }
        else
        {
            _roomCodeText.text = "Waiting for room code...";
        }
    }

    private void CopyRoomCode()
    {
        if (_networkRunnerHandler != null && !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
        {
            GUIUtility.systemCopyBuffer = _networkRunnerHandler.SessionHash;

            StartCoroutine(ShowCopiedFeedback());
        }
    }

    private IEnumerator ShowCopiedFeedback()
    {
        string originalText = _roomCodeText.text;
        _roomCodeText.text = "Copied to clipboard!";

        yield return new WaitForSeconds(1.0f);

        _roomCodeText.text = originalText;
    }
}