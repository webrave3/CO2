using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Diagnostics;

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

    private void Awake()
    {
        // Hide error message and joining indicator initially
        if (_errorMessageText != null)
            _errorMessageText.gameObject.SetActive(false);

        if (_joiningIndicator != null)
            _joiningIndicator.SetActive(false);
    }

    private void Start()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_copyCodeButton != null)
        {
            _copyCodeButton.onClick.AddListener(CopyRoomCodeToClipboard);
        }

        if (_joinButton != null)
        {
            _joinButton.onClick.AddListener(JoinRoomByCode);
        }

        // Update room code display if hosting
        UpdateRoomCodeDisplay();
    }

    private void UpdateRoomCodeDisplay()
    {
        if (_roomCodeDisplay != null && _networkRunnerHandler != null)
        {
            if (!string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
            {
                _roomCodeDisplay.text = FormatRoomCodeForDisplay(_networkRunnerHandler.SessionHash);
            }
            else
            {
                _roomCodeDisplay.text = "No active session";
            }
        }
    }

    private string FormatRoomCodeForDisplay(string code)
    {
        // Format room code for better readability
        // This can be customized based on your preference

        // Option 1: Keep as is for CamelCase (e.g., "RedWolf")
        return code;

        // Option 2: Add a space between words
        // Find the position where second word begins (capital letter)
        /*
        for (int i = 1; i < code.Length; i++)
        {
            if (char.IsUpper(code[i]))
            {
                return code.Substring(0, i) + " " + code.Substring(i);
            }
        }
        return code;
        */
    }

    private void CopyRoomCodeToClipboard()
    {
        if (_networkRunnerHandler != null && !string.IsNullOrEmpty(_networkRunnerHandler.SessionHash))
        {
            GUIUtility.systemCopyBuffer = _networkRunnerHandler.SessionHash;
            UnityEngine.Debug.Log("Room code copied to clipboard: " + _networkRunnerHandler.SessionHash);

            // Show feedback to user
            StartCoroutine(ShowCopiedFeedback());
        }
    }

    private IEnumerator ShowCopiedFeedback()
    {
        // This is a simple example - expand with proper UI feedback
        string originalText = _roomCodeDisplay.text;
        _roomCodeDisplay.text = "Copied!";

        yield return new WaitForSeconds(1.0f);

        _roomCodeDisplay.text = originalText;
    }

    private async void JoinRoomByCode()
    {
        if (_isJoining) return;

        if (_networkRunnerHandler != null && _roomCodeInput != null)
        {
            string roomCode = _roomCodeInput.text.Trim();

            if (string.IsNullOrEmpty(roomCode))
            {
                ShowErrorMessage("Please enter a room code");
                return;
            }

            // Format the code properly
            roomCode = FormatRoomCode(roomCode);

            // Show joining indicator
            _isJoining = true;
            if (_joiningIndicator != null)
                _joiningIndicator.SetActive(true);

            if (_joinButton != null)
                _joinButton.interactable = false;

            // Try to join
            UnityEngine.Debug.Log($"Attempting to join room with code: {roomCode}");
            await _networkRunnerHandler.StartClientGameByHash(roomCode);

            // If we get here and the runner is not in a game, the join failed
            if (_networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning ||
                string.IsNullOrEmpty(_networkRunnerHandler.SessionUniqueID))
            {
                ShowErrorMessage("Could not find a game with that code");

                // Hide joining indicator
                if (_joiningIndicator != null)
                    _joiningIndicator.SetActive(false);

                if (_joinButton != null)
                    _joinButton.interactable = true;
            }

            _isJoining = false;
        }
    }

    private string FormatRoomCode(string input)
    {
        // Remove all spaces and special characters
        string cleanInput = "";
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
                cleanInput += c;
        }

        // Check if we need to fix capitalization
        // If the user typed something like "redwolf", convert to "RedWolf"
        if (cleanInput.Length > 0)
        {
            // First, make sure the first letter is capitalized
            cleanInput = char.ToUpper(cleanInput[0]) + cleanInput.Substring(1);

            // Now try to find where the second word might begin
            bool foundSecondWord = false;
            for (int i = 1; i < cleanInput.Length; i++)
            {
                if (char.IsUpper(cleanInput[i]))
                {
                    foundSecondWord = true;
                    break;
                }
            }

            // If we didn't find a capital letter, try to guess where the second word begins
            // This is a simple heuristic and might need to be refined based on your word lists
            if (!foundSecondWord && cleanInput.Length > 3)
            {
                // Look for common adjective endings
                string[] commonEndings = new[] { "ed", "en", "er", "le", "ow", "py", "ry", "ty" };
                foreach (string ending in commonEndings)
                {
                    int pos = cleanInput.IndexOf(ending);
                    if (pos > 0 && pos + ending.Length < cleanInput.Length)
                    {
                        // Capitalize the letter after the ending
                        int newWordPos = pos + ending.Length;
                        cleanInput = cleanInput.Substring(0, newWordPos) +
                                    char.ToUpper(cleanInput[newWordPos]) +
                                    cleanInput.Substring(newWordPos + 1);
                        break;
                    }
                }
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

            // Hide the error message after a delay
            StartCoroutine(HideErrorAfterDelay());
        }
    }

    private IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSeconds(_errorMessageDuration);

        if (_errorMessageText != null)
            _errorMessageText.gameObject.SetActive(false);
    }

    // Called on each frame to update the UI based on the network state
    private void Update()
    {
        // If we're on the host's side, update the room code display
        if (_networkRunnerHandler != null &&
            _networkRunnerHandler.Runner != null &&
            _networkRunnerHandler.Runner.IsServer &&
            _roomCodeDisplay != null)
        {
            UpdateRoomCodeDisplay();
        }
    }
}