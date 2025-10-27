// Filename: JoinGameUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using System;
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
    [SerializeField] private Button _directJoinButton;

    [Header("Matchmaking")]
    [SerializeField] private Button _matchmakingButton;
    // --- ADDED: UI elements for client-side filters ---
    [SerializeField] private TMP_Dropdown _languageFilterDropdown; // Assign Language Dropdown here
    [SerializeField] private TMP_Dropdown _regionFilterDropdown;   // Assign Region Filter Dropdown here (Optional)
    // Add other filter UI dropdowns/toggles as needed (e.g., Difficulty)

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
            Debug.LogError("NetworkRunnerHandler not found in scene. Disabling Join Game UI.");
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
                if (mainMenu != null) mainMenu.ShowMainPanel();
            });
        }

        if (_directJoinButton != null) _directJoinButton.onClick.AddListener(OnDirectJoinClicked);
        if (_matchmakingButton != null) _matchmakingButton.onClick.AddListener(OnMatchmakingClicked);

        // --- Optional: Initialize Dropdowns ---
        // Populate dropdowns here if they aren't set up in the Inspector
        // Example:
        // if (_languageFilterDropdown != null) {
        //     _languageFilterDropdown.ClearOptions();
        //     _languageFilterDropdown.AddOptions(new List<string> { "Any", "English", "Estonian" }); // Match host options + "Any"
        // }
        // if (_regionFilterDropdown != null) {
        //     _regionFilterDropdown.ClearOptions();
        //     _regionFilterDropdown.AddOptions(new List<string> { "Any", "Europe", "US East" }); // Example sub-regions
        // }
    }

    public void ShowJoinPanel()
    {
        Debug.Log("JoinGameUI: ShowJoinPanel() method executed!"); // <-- ADD THIS
        if (_joinGamePanel == null)
        {
            Debug.LogError("JoinGameUI: _joinGamePanel reference is NULL!"); // <-- ADD check
            return;
        }
        _joinGamePanel.SetActive(true);
        if (_joinGamePanel == null) return;
        _joinGamePanel.SetActive(true);
        if (_statusText != null) _statusText.gameObject.SetActive(false);
        if (_roomCodeInput != null) _roomCodeInput.text = "";
        SetJoiningState(false);
    }

    private async void OnDirectJoinClicked()
    {
        if (_isJoining || _networkRunnerHandler == null || _networkRunnerHandler.IsSessionActive) return;

        string roomCode = _roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(roomCode))
        {
            ShowStatusMessage("Please enter a room code", Color.yellow);
            return;
        }

        SetJoiningState(true, $"Joining room: {roomCode}...");
        try
        {
            bool joinStarted = await _networkRunnerHandler.StartClientGameByHash(roomCode);
            if (!joinStarted || (_networkRunnerHandler.Runner == null || !_networkRunnerHandler.Runner.IsRunning))
            {
                if (this != null && gameObject.activeInHierarchy)
                {
                    ShowStatusMessage("Failed to find or connect to game.", Color.red);
                    SetJoiningState(false);
                }
            }
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
    }

    private async void OnMatchmakingClicked()
    {
        if (_isJoining || _networkRunnerHandler == null || _networkRunnerHandler.IsSessionActive) return;

        SetJoiningState(true, "Searching for a public game...");

        // --- Prepare Filters from UI ---
        Dictionary<string, SessionProperty> filters = new Dictionary<string, SessionProperty>();

        // Get language filter
        if (_languageFilterDropdown != null && _languageFilterDropdown.value > 0) // Assuming index 0 is "Any"
        {
            string selectedLanguage = _languageFilterDropdown.options[_languageFilterDropdown.value].text;
            filters["lang"] = selectedLanguage; // Key must match host-side key ("lang")
            Debug.Log($"Filtering matchmaking by language: {selectedLanguage}");
        }

        // Get region filter (Optional: Assumes host sets a custom "RegionFilter" property)
        if (_regionFilterDropdown != null && _regionFilterDropdown.value > 0) // Assuming index 0 is "Any"
        {
            string selectedRegionFilter = _regionFilterDropdown.options[_regionFilterDropdown.value].text;
            // IMPORTANT: The key "RegionFilter" must match EXACTLY what the host sets in their custom properties
            filters["RegionFilter"] = selectedRegionFilter;
            Debug.Log($"Filtering matchmaking by custom region filter: {selectedRegionFilter}");
        }

        // Add other filters similarly...
        // e.g., if (_difficultyFilterDropdown != null && _difficultyFilterDropdown.value > 0) { filters["difficulty"] = _difficultyFilterDropdown.options[_difficultyFilterDropdown.value].text; }


        // --- Call Matchmaking Logic ---
        try
        {
            bool matchmakingJoinStarted = await _networkRunnerHandler.FindAndJoinPublicGame(filters);

            if (!matchmakingJoinStarted)
            {
                if (this != null && gameObject.activeInHierarchy)
                {
                    ShowStatusMessage("No suitable public games found.", Color.yellow);
                    SetJoiningState(false);
                    // Optional: Auto-host logic could go here
                }
            }
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
        // Disable filter dropdowns while joining
        if (_languageFilterDropdown != null) _languageFilterDropdown.interactable = !isJoining;
        if (_regionFilterDropdown != null) _regionFilterDropdown.interactable = !isJoining;


        if (isJoining && !string.IsNullOrEmpty(statusMessage))
        {
            ShowStatusMessage(statusMessage, Color.white, 0);
        }
    }

    private void ShowStatusMessage(string message, Color color, float duration = 0)
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