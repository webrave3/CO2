using UnityEngine;
using UnityEngine.UI;
using System;

public class StartGameUI : MonoBehaviour
{
    // Events to notify other systems (like MainMenuUI) about button clicks
    public static event Action OnCreateGameRequest; // Renamed for clarity
    public static event Action OnJoinGameRequest;   // Renamed for clarity

    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private GameObject _menuPanel; // Panel containing these buttons

    private void Awake()
    {
        // Add listeners
        if (_createButton != null)
            _createButton.onClick.AddListener(HandleCreateClick);
        if (_joinButton != null)
            _joinButton.onClick.AddListener(HandleJoinClick);
    }

    private void HandleCreateClick()
    {
        OnCreateGameRequest?.Invoke(); // Trigger the event
        if (_menuPanel != null)
            _menuPanel.SetActive(false); // Hide this panel after clicking
    }

    private void HandleJoinClick()
    {
        OnJoinGameRequest?.Invoke(); // Trigger the event
        if (_menuPanel != null)
            _menuPanel.SetActive(false); // Hide this panel after clicking
    }

    private void OnDestroy()
    {
        // Clean up listeners
        if (_createButton != null)
            _createButton.onClick.RemoveListener(HandleCreateClick);
        if (_joinButton != null)
            _joinButton.onClick.RemoveListener(HandleJoinClick);
    }

    // Optional: Methods to show/hide the panel containing these buttons
    public void Show()
    {
        if (_menuPanel != null) _menuPanel.SetActive(true);
    }

    public void Hide()
    {
        if (_menuPanel != null) _menuPanel.SetActive(false);
    }
}