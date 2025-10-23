using UnityEngine;
using System.Collections.Generic;
using Fusion;
using System;

public enum GameState
{
    MainMenu,
    Playing,
    GameOver
}

public class GameStateManager : NetworkBehaviour
{
    [Networked]
    public GameState State { get; set; } = GameState.Playing;

    // Singleton pattern
    public static GameStateManager Instance { get; private set; }

    // Session information
    [Networked] public NetworkString<_32> SessionDisplayName { get; set; }
    [Networked] public NetworkString<_64> SessionID { get; set; }
    [Networked] public NetworkString<_16> SessionHash { get; set; }
    [Networked] public int SessionStartTime { get; set; }

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
            NetworkRunnerHandler runnerHandler = FindObjectOfType<NetworkRunnerHandler>();
            if (runnerHandler != null)
            {
                SessionDisplayName = runnerHandler.SessionDisplayName;
                SessionID = runnerHandler.SessionUniqueID;
                SessionHash = runnerHandler.SessionHash;
                SessionStartTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            // Start directly in playing state
            State = GameState.Playing;

            // Initialize the dictionary if needed (Ensure called only once)
            if (PlayersReady.Count == 0)
            {
                PlayersReady.Clear();
            }
        }

        // Broadcast initial state change effects if necessary
        HandleGameStateChangeEffects();
    }

    // This method can be called manually or hooked into a change detector if needed
    private void HandleGameStateChangeEffects()
    {
        // Example: Enable/disable certain UI, start/stop timers, etc.
        switch (State)
        {
            case GameState.Playing:
                // Logic for when the game enters Playing state
                break;
            case GameState.GameOver:
                // Logic for Game Over
                break;
        }
    }

    // Callback for when the State property changes (Fusion automatically calls this)
    public override void Render()
    {
        // If you need to react *immediately* visually when the state changes on clients,
        // you might check for changes here. However, for most logic, reacting in Spawned
        // or using change detection might be better.
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerReady(PlayerRef player, bool isReady)
    {
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
        if (!Object.HasStateAuthority) return;

        bool allReady = true;
        foreach (var kvp in PlayersReady)
        {
            if (!kvp.Value)
            {
                allReady = false;
                break;
            }
        }

        // If we have at least 1 player and all are ready, trigger game start logic
        if (allReady && PlayersReady.Count > 0)
        {
            // Optional: Auto-start game logic or enable start button for host
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartGame()
    {
        if (!Object.HasStateAuthority) return;

        State = GameState.Playing;
        // The change detector or Render method should handle the effects
        // Or call HandleGameStateChangeEffects() explicitly if needed immediately
    }
}