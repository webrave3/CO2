using UnityEngine;
using UnityEngine.UI;
using System;

public class StartGameUI : MonoBehaviour
{
    public static event Action OnCreateGame;
    public static event Action OnJoinGame;

    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private GameObject _menuPanel;

    private void Awake()
    {
        Debug.Log("StartGameUI Awake");

        if (_createButton == null)
        {
            Debug.LogError("Create Button not assigned in StartGameUI");
        }

        if (_joinButton == null)
        {
            Debug.LogError("Join Button not assigned in StartGameUI");
        }

        if (_menuPanel == null)
        {
            Debug.LogError("Menu Panel not assigned in StartGameUI");
        }

        _createButton.onClick.AddListener(() => {
            Debug.Log("Create Game button clicked");
            OnCreateGame?.Invoke();
            _menuPanel.SetActive(false);
        });

        _joinButton.onClick.AddListener(() => {
            Debug.Log("Join Game button clicked");
            OnJoinGame?.Invoke();
            _menuPanel.SetActive(false);
        });
    }

    private void OnDestroy()
    {
        if (_createButton != null)
            _createButton.onClick.RemoveAllListeners();

        if (_joinButton != null)
            _joinButton.onClick.RemoveAllListeners();
    }
}