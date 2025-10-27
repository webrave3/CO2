// Filename: JoinGameUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Keep for StringComparison etc.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
// *** REMOVED Photon.Realtime ***
using System.Threading.Tasks;

public class JoinGameUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _joinGamePanel;
    [SerializeField] private Button _backButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private GameObject _joiningIndicator;

    [Header("Direct Join")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private TMP_InputField _passwordInput; // Keep this
    [SerializeField] private Button _directJoinButton;

    [Header("Matchmaking")]
    [SerializeField] private Button _matchmakingButton;
    [SerializeField] private TMP_Dropdown _languageFilterDropdown;
    [SerializeField] private TMP_Dropdown _regionFilterDropdown;

    [Header("Settings")]
    [SerializeField] private float _statusMessageDuration = 3f;

    private NetworkRunnerHandler _networkRunnerHandler;
    private bool _isJoining = false;
    private Coroutine _statusMessageCoroutine;
    private List<string> _languageOptions = new List<string> { "Any", "English", "Spanish", "French", "German", "Chinese", "Russian" };

    void Start()
    {
         _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
         if (_networkRunnerHandler == null) { /* ... Error handling ... */ Debug.LogError("NetworkRunnerHandler not found!"); enabled = false; return; }

         if (_statusText != null) _statusText.gameObject.SetActive(false);
         if (_joiningIndicator != null) _joiningIndicator.SetActive(false);

         if (_backButton != null) {
             _backButton.onClick.AddListener(() => {
                 if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
                 FindObjectOfType<MainMenuUI>()?.ShowMainPanel();
             });
         }

         if (_directJoinButton != null) _directJoinButton.onClick.AddListener(OnDirectJoinClicked);
         if (_matchmakingButton != null) _matchmakingButton.onClick.AddListener(OnMatchmakingClicked);

         // Initialize Dropdowns using static helpers from NetworkRunnerHandler
         InitializeDropdown(_languageFilterDropdown, _languageOptions);
         List<string> regionNames = NetworkRunnerHandler.GetRegionNames();
         // Ensure "Any" is first, remove "Best"
          if (!regionNames.Contains("Any", StringComparer.OrdinalIgnoreCase)) {
             regionNames.RemoveAll(r => r.Equals("Best", StringComparison.OrdinalIgnoreCase));
             regionNames.Insert(0, "Any");
         } else {
              regionNames.RemoveAll(r => r.Equals("Best", StringComparison.OrdinalIgnoreCase));
              if (!regionNames[0].Equals("Any", StringComparison.OrdinalIgnoreCase)) {
                   regionNames.RemoveAll(r => r.Equals("Any", StringComparison.OrdinalIgnoreCase));
                   regionNames.Insert(0, "Any");
              }
         }
         InitializeDropdown(_regionFilterDropdown, regionNames);

         if(_passwordInput != null) _passwordInput.text = ""; // Clear password field initially
    }

    private void InitializeDropdown(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown != null) { dropdown.ClearOptions(); if (options != null && options.Count > 0) { dropdown.AddOptions(options); dropdown.value = 0; } }
    }

    public void ShowJoinPanel()
    {
        if (_joinGamePanel == null) return;
        _joinGamePanel.SetActive(true);
        if (_statusText != null) _statusText.gameObject.SetActive(false);
        if (_roomCodeInput != null) _roomCodeInput.text = "";
        if (_passwordInput != null) _passwordInput.text = ""; // Clear password on show
        SetJoiningState(false);
    }

    private async void OnDirectJoinClicked()
    {
        if (_isJoining || _networkRunnerHandler == null) return;

        string roomCode = _roomCodeInput?.text.Trim();
        if (string.IsNullOrEmpty(roomCode)) { ShowStatusMessage("Please enter a room code", Color.yellow); return; }

        string password = _passwordInput?.text; // Get password (can be empty/null)

        SetJoiningState(true, $"Joining room: {roomCode}...");
        bool joinStarted = false;
        try
        {
            // Call handler - the StartClientGameByHash overload takes password string
            joinStarted = await _networkRunnerHandler.StartClientGameByHash(roomCode, password);

            if (!joinStarted) {
                 if (this != null && gameObject != null && gameObject.activeInHierarchy) { // Check validity after await
                     // Check if a specific error was already shown by OnConnectFailed
                     if (_statusText == null || !_statusText.gameObject.activeSelf || _statusText.color != Color.red) {
                        ShowStatusMessage("Failed to join. Check room code/password or console.", Color.red); // More informative message
                     }
                     SetJoiningState(false);
                 }
             }
             // On success, scene loading is handled by NetworkRunnerHandler/Fusion
        }
        catch (Exception ex) { // Catch potential exceptions (like if StartClientGameByHash throws)
             Debug.LogError($"Error during direct join: {ex.Message}\n{ex.StackTrace}");
             if (this != null && gameObject != null && gameObject.activeInHierarchy) { ShowStatusMessage($"Error: {ex.Message}", Color.red); SetJoiningState(false); }
        }
    }

    private async void OnMatchmakingClicked()
    {
         if (_isJoining || _networkRunnerHandler == null) return;
         SetJoiningState(true, "Searching for a public game...");

         Dictionary<string, SessionProperty> filters = new Dictionary<string, SessionProperty>();
         // Language Filter (if not "Any")
         if (_languageFilterDropdown != null && _languageFilterDropdown.value > 0) {
             filters[NetworkRunnerHandler.SESSION_LANGUAGE_KEY] = _languageFilterDropdown.options[_languageFilterDropdown.value].text;
             Debug.Log($"Filtering matchmaking by Language: {filters[NetworkRunnerHandler.SESSION_LANGUAGE_KEY]}");
         }
         // Region Filter (if not "Any")
         if (_regionFilterDropdown != null && _regionFilterDropdown.value > 0) {
              string regionText = _regionFilterDropdown.options[_regionFilterDropdown.value].text;
              // Pass the user-friendly text as the filter preference
             filters[NetworkRunnerHandler.SESSION_REGION_KEY] = regionText;
              Debug.Log($"Filtering matchmaking by Region Preference: {regionText}");
         } else {
              Debug.Log("No specific region preference set for matchmaking.");
         }


         bool matchmakingJoinStarted = false;
         try {
             matchmakingJoinStarted = await _networkRunnerHandler.FindAndJoinPublicGame(filters);
             if (!matchmakingJoinStarted) {
                  if (this != null && gameObject != null && gameObject.activeInHierarchy) { ShowStatusMessage("No suitable public games found.", Color.yellow); SetJoiningState(false); }
             }
             // On success, scene loading handled by Handler/Fusion
         } catch (Exception ex) {
              Debug.LogError($"Error during matchmaking: {ex.Message}\n{ex.StackTrace}");
              if (this != null && gameObject != null && gameObject.activeInHierarchy) { ShowStatusMessage($"Error: {ex.Message}", Color.red); SetJoiningState(false); }
         }
    }


    private void SetJoiningState(bool isJoining, string statusMessage = "")
    {
        _isJoining = isJoining;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(isJoining);
        // Set interactable state for UI elements
        if (_directJoinButton != null) _directJoinButton.interactable = !isJoining;
        if (_matchmakingButton != null) _matchmakingButton.interactable = !isJoining;
        if (_backButton != null) _backButton.interactable = !isJoining;
        if (_roomCodeInput != null) _roomCodeInput.interactable = !isJoining;
        if (_passwordInput != null) _passwordInput.interactable = !isJoining;
        if (_languageFilterDropdown != null) _languageFilterDropdown.interactable = !isJoining;
        if (_regionFilterDropdown != null) _regionFilterDropdown.interactable = !isJoining;

        // Handle persistent "Joining..." message
        if (isJoining && !string.IsNullOrEmpty(statusMessage)) {
            ShowStatusMessage(statusMessage, Color.white, 0); // Persistent
        } else if (!isJoining && _statusText != null && _statusText.gameObject.activeSelf && _statusText.color == Color.white && _statusMessageCoroutine == null) {
            _statusText.gameObject.SetActive(false); // Hide persistent message if no longer joining AND no timed message is active
        }
    }

    // Public method to allow NetworkRunnerHandler (e.g., in OnConnectFailed) to show status messages
    public void ShowStatusMessage(string message, Color color, float duration = -1f)
    {
        if (_statusText == null) return;
        if (_statusMessageCoroutine != null) { StopCoroutine(_statusMessageCoroutine); _statusMessageCoroutine = null; }

        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);

        float actualDuration = (duration == 0) ? 0 : (duration < 0 ? _statusMessageDuration : duration);

        if (actualDuration > 0) { _statusMessageCoroutine = StartCoroutine(HideStatusAfterDelay(actualDuration)); }
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null) _statusText.gameObject.SetActive(false);
        _statusMessageCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_statusMessageCoroutine != null) StopCoroutine(_statusMessageCoroutine);
    }
}