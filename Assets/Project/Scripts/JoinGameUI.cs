// Filename: JoinGameUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using System;
using System.Threading.Tasks; // Keep for async methods

public class JoinGameUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _joinGamePanel;
    [SerializeField] private Button _backButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private GameObject _joiningIndicator;

    [Header("Direct Join")]
    [SerializeField] private TMP_InputField _roomCodeInput;
    [SerializeField] private Button _directJoinButton;

    [Header("Matchmaking")]
    [SerializeField] private Button _matchmakingButton;
    [SerializeField] private TMP_Dropdown _languageFilterDropdown; // Assign Language Dropdown
    [SerializeField] private TMP_Dropdown _regionFilterDropdown;   // Assign Region Filter Dropdown

    [Header("Settings")]
    [SerializeField] private float _statusMessageDuration = 3f;

    private NetworkRunnerHandler _networkRunnerHandler;
    private bool _isJoining = false;
    private Coroutine _statusMessageCoroutine;

    void Start()
    {
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        if (_networkRunnerHandler == null)
        {
            Debug.LogError("NetworkRunnerHandler not found! Disabling Join Game UI.");
            if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
            enabled = false;
            return;
        }

        if (_statusText != null) _statusText.gameObject.SetActive(false);
        if (_joiningIndicator != null) _joiningIndicator.SetActive(false);

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(() => {
                if (_joinGamePanel != null) _joinGamePanel.SetActive(false);
                MainMenuUI mainMenu = FindObjectOfType<MainMenuUI>();
                if (mainMenu != null) mainMenu.ShowMainPanel(); // Ensure MainMenuUI has ShowMainPanel()
            });
        }

        if (_directJoinButton != null) _directJoinButton.onClick.AddListener(OnDirectJoinClicked);
        if (_matchmakingButton != null) _matchmakingButton.onClick.AddListener(OnMatchmakingClicked);

        // --- Initialize Dropdowns ---
        InitializeDropdown(_languageFilterDropdown, new List<string> { "Any", "English", "Estonian" }); // "Any" should be first
        InitializeDropdown(_regionFilterDropdown, new List<string> { "Any", "NA East", "EU", "Asia" }); // "Any" first, match host options
    }

    private void InitializeDropdown(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown != null)
        {
            dropdown.ClearOptions();
            if (options != null && options.Count > 0)
            {
                dropdown.AddOptions(options);
                dropdown.value = 0; // Default to "Any"
            }
        }
    }


    // This should be called by MainMenuUI when the Join button is clicked
    public void ShowJoinPanel()
    {
        if (_joinGamePanel == null) return;
        _joinGamePanel.SetActive(true);
        if (_statusText != null) _statusText.gameObject.SetActive(false);
        if (_roomCodeInput != null) _roomCodeInput.text = "";
        SetJoiningState(false); // Reset joining state
    }

    private async void OnDirectJoinClicked()
    {
        if (_isJoining || _networkRunnerHandler == null) return; // Removed IsSessionActive check, handler handles this

        string roomCode = _roomCodeInput.text.Trim(); // Don't force ToUpper if codes are case-sensitive (AdjectiveNoun)
        if (string.IsNullOrEmpty(roomCode))
        {
            ShowStatusMessage("Please enter a room code", Color.yellow);
            return;
        }

        SetJoiningState(true, $"Joining room: {roomCode}...");
        bool joinStarted = false;
        try
        {
            // Direct join uses the HASH code (the user-friendly one)
            joinStarted = await _networkRunnerHandler.StartClientGameByHash(roomCode);

            // Check result *after* await completes
            if (!joinStarted) // Handler's method returns false on failure to start
            {
                if (this != null && gameObject.activeInHierarchy) // Check if UI still exists
                {
                    ShowStatusMessage("Failed to find or connect to game.", Color.red);
                    SetJoiningState(false);
                }
            }
            // On success, NetworkRunnerHandler manages scene loading
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during direct join: {ex.Message}\n{ex.StackTrace}");
            if (this != null && gameObject.activeInHierarchy)
            {
                ShowStatusMessage($"Error: {ex.Message}", Color.red);
                SetJoiningState(false);
            }
        }
        // If joinStarted was true but runner isn't running (e.g., immediate disconnect), OnShutdown handles UI reset
    }

    private async void OnMatchmakingClicked()
    {
        if (_isJoining || _networkRunnerHandler == null) return; // Removed IsSessionActive check

        SetJoiningState(true, "Searching for a public game...");

        // --- Prepare Filters from UI ---
        Dictionary<string, SessionProperty> filters = new Dictionary<string, SessionProperty>();

        // Get language filter
        if (_languageFilterDropdown != null && _languageFilterDropdown.value > 0) // Index 0 is "Any"
        {
            string selectedLanguage = _languageFilterDropdown.options[_languageFilterDropdown.value].text;
            // Use the constant key defined in NetworkRunnerHandler
            filters[NetworkRunnerHandler.SESSION_LANGUAGE_KEY] = selectedLanguage;
            Debug.Log($"Filtering matchmaking by language: {selectedLanguage}");
        }

        // --- Get Region Preference ---
        string selectedRegionText = null; // Null means "Any" or "Best" for FindAndJoinPublicGame
        if (_regionFilterDropdown != null && _regionFilterDropdown.value > 0) // Index 0 is "Any"
        {
            selectedRegionText = _regionFilterDropdown.options[_regionFilterDropdown.value].text;
            Debug.Log($"Filtering matchmaking by region preference: {selectedRegionText}");
            // Note: Region filtering happens in FindAndJoinPublicGame based on this string,
            // no need to add it to the 'filters' dictionary unless your host *also* sets a custom region property.
        }

        // Add other filters similarly...
        // e.g., filters["difficulty"] = "Normal";

        // --- CORRECTED Call to FindAndJoinPublicGame ---
        bool matchmakingJoinStarted = false;
        try
        {
            // Pass region string first, then the filters dictionary
            matchmakingJoinStarted = await _networkRunnerHandler.FindAndJoinPublicGame(selectedRegionText, filters);

            if (!matchmakingJoinStarted)
            {
                if (this != null && gameObject.activeInHierarchy)
                {
                    ShowStatusMessage("No suitable public games found.", Color.yellow);
                    SetJoiningState(false);
                    // Optional: Add logic here to offer hosting a game instead
                }
            }
            // On success, NetworkRunnerHandler handles scene loading
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during matchmaking: {ex.Message}\n{ex.StackTrace}");
            if (this != null && gameObject.activeInHierarchy)
            {
                ShowStatusMessage($"Error: {ex.Message}", Color.red);
                SetJoiningState(false);
            }
        }
    }


    private void SetJoiningState(bool isJoining, string statusMessage = "")
    {
        _isJoining = isJoining;
        if (_joiningIndicator != null) _joiningIndicator.SetActive(isJoining);
        if (_directJoinButton != null) _directJoinButton.interactable = !isJoining;
        if (_matchmakingButton != null) _matchmakingButton.interactable = !isJoining;
        if (_backButton != null) _backButton.interactable = !isJoining;
        if (_roomCodeInput != null) _roomCodeInput.interactable = !isJoining;
        // Disable filter dropdowns while joining
        if (_languageFilterDropdown != null) _languageFilterDropdown.interactable = !isJoining;
        if (_regionFilterDropdown != null) _regionFilterDropdown.interactable = !isJoining;


        if (isJoining && !string.IsNullOrEmpty(statusMessage))
        {
            ShowStatusMessage(statusMessage, Color.white, 0); // Use duration 0 for persistent message
        }
        else if (!isJoining && _statusText != null && _statusText.gameObject.activeSelf && _statusText.color == Color.white)
        {
            // Hide persistent "Joining..." message if we are no longer joining
            _statusText.gameObject.SetActive(false);
        }
    }

    // Use duration 0 for persistent, negative for default duration, positive for specific duration
    private void ShowStatusMessage(string message, Color color, float duration = -1f)
    {
        if (_statusText == null) return;
        if (_statusMessageCoroutine != null) StopCoroutine(_statusMessageCoroutine);

        _statusText.text = message;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);

        float actualDuration = (duration == 0) ? 0 : (duration < 0 ? _statusMessageDuration : duration);

        if (actualDuration > 0)
        {
            _statusMessageCoroutine = StartCoroutine(HideStatusAfterDelay(actualDuration));
        }
        else // If duration is 0 (persistent) or invalid, ensure coroutine reference is null
        {
            _statusMessageCoroutine = null;
        }
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null) _statusText.gameObject.SetActive(false);
        _statusMessageCoroutine = null;
    }

    private void OnDestroy()
    {
        // Stop coroutine if object is destroyed to prevent errors
        if (_statusMessageCoroutine != null) StopCoroutine(_statusMessageCoroutine);
    }
}