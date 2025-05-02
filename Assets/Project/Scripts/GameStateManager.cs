using UnityEngine;
using System.Collections.Generic;
using Fusion;

public enum GameState
{
    MainMenu,
    Lobby,
    Playing,
    GameOver
}

public class GameStateManager : NetworkBehaviour
{
    [Networked]
    public GameState State { get; set; } = GameState.Lobby;

    // Singleton pattern 
    public static GameStateManager Instance { get; private set; }

    // Player ready status
    [Networked]
    public NetworkDictionary<PlayerRef, NetworkBool> PlayersReady => default;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            State = GameState.Lobby;
        }

        // Initialize the dictionary if needed
        if (PlayersReady.Count == 0 && Object.HasStateAuthority)
        {
            PlayersReady.Clear(); // Ensure it's empty
        }
    }

    // Manual state change notification method
    public void OnGameStateChanged()
    {
        Debug.Log($"Game state changed to: {State}");

        // Handle state transition logic here
        switch (State)
        {
            case GameState.Lobby:
                // Setup lobby state
                break;
            case GameState.Playing:
                // Begin gameplay
                NotifyPlayersOfGameStart();
                break;
            case GameState.GameOver:
                // End game
                break;
        }
    }

    private void NotifyPlayersOfGameStart()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            player.OnGameStart();
        }
    }

    // Fix RPC by removing any additional attributes
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerReady(PlayerRef player, bool isReady)
    {
        Debug.Log($"RPC_SetPlayerReady called for player {player}, ready: {isReady}");

        // Make sure this only executes on the State Authority (server)
        if (!Object.HasStateAuthority) return;

        if (PlayersReady.ContainsKey(player))
        {
            PlayersReady.Set(player, isReady);
        }
        else
        {
            PlayersReady.Add(player, isReady);
        }

        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (!Object.HasStateAuthority)
            return;

        bool allReady = true;
        foreach (var kvp in PlayersReady)
        {
            if (!kvp.Value)
            {
                allReady = false;
                break;
            }
        }

        // If we have at least 1 player and all are ready, we could auto-start
        if (allReady && PlayersReady.Count > 0)
        {
            Debug.Log("All players ready, game could start now");
        }
    }

    // Fix RPC by removing any additional attributes
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartGame()
    {
        Debug.Log("RPC_StartGame called");

        // Make sure this only executes on the State Authority (server)
        if (!Object.HasStateAuthority) return;

        State = GameState.Playing;
        OnGameStateChanged();
    }
}