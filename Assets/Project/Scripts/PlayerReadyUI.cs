using UnityEngine;
using TMPro;
using Fusion;

public class PlayerReadyUI : NetworkBehaviour
{
    [SerializeField] private UnityEngine.UI.Image _readyIndicator;
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private Color _readyColor = Color.green;
    [SerializeField] private Color _notReadyColor = Color.red;

    [Networked]
    public NetworkBool IsReady { get; set; }

    [Networked]
    public NetworkString<_16> PlayerName { get; set; }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            // Set player name from PlayerPrefs
            string playerName = PlayerPrefs.GetString("PlayerName", $"Player{Runner.LocalPlayer.PlayerId}");
            RPC_SetPlayerName(playerName);
        }

        // Initialize UI
        UpdateReadyUI();
        UpdatePlayerNameUI();
    }

    private void Update()
    {
        // Check for space key to toggle ready status for local player
        if (Object.HasInputAuthority && Input.GetKeyDown(KeyCode.Space))
        {
            RPC_ToggleReady();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ToggleReady()
    {
        IsReady = !IsReady;
        UpdateReadyUI();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerName(string name)
    {
        PlayerName = name;
        UpdatePlayerNameUI();
    }

    // Manual property change callbacks
    public void UpdateReadyUI()
    {
        if (_readyIndicator != null)
        {
            _readyIndicator.color = IsReady ? _readyColor : _notReadyColor;
        }
    }

    public void UpdatePlayerNameUI()
    {
        if (_playerNameText != null)
        {
            _playerNameText.text = PlayerName.ToString();
        }
    }
}