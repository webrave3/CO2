using UnityEngine;
using System.Collections.Generic;
using Fusion;

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
        Debug.Log("GameStateManager initialized");
    }

    public override void Spawned()
    {
        Debug.Log($"GameStateManager spawned - HasStateAuthority: {Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            // Start directly in playing state
            State = GameState.Playing;
            Debug.Log("Game started in Playing state");
        }

        // Initialize the dictionary if needed
        if (PlayersReady.Count == 0 && Object.HasStateAuthority)
        {
            PlayersReady.Clear(); // Ensure it's empty
            Debug.Log("Initialized PlayersReady dictionary");
        }

        // Broadcast initial state
        OnGameStateChanged();
    }

    // Manual state change notification method
    public void OnGameStateChanged()
    {
        Debug.Log($"Game state changed to: {State}");

        // Handle state transition logic here
        switch (State)
        {
            case GameState.Playing:
                Debug.Log("Game is now in Playing state");
                break;
            case GameState.GameOver:
                Debug.Log("Game Over");
                break;
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
            Debug.Log("All players ready");
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

    // Test function - can be called from anywhere to verify state
    public void PrintCurrentState()
    {
        Debug.LogWarning($"CURRENT GAME STATE: {State}, Instance ID: {GetInstanceID()}");

        // Print all player ready states - check Count instead of null
        Debug.Log($"Players ready status ({PlayersReady.Count} players):");
        if (PlayersReady.Count > 0)
        {
            foreach (var kvp in PlayersReady)
            {
                Debug.Log($"  Player {kvp.Key}: {(kvp.Value ? "READY" : "NOT READY")}");
            }
        }
        else
        {
            Debug.Log("  No players have registered ready status yet");
        }
    }

    // Direct force change to Playing state - for testing only
    public void ForcePlayingState()
    {
        if (Object.HasStateAuthority)
        {
            Debug.LogWarning("FORCING GAME STATE TO PLAYING");
            State = GameState.Playing;
            OnGameStateChanged();
        }
        else
        {
            Debug.LogError("Cannot force state change - not state authority");
        }
    }
}