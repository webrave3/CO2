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
    [Networked]
    public NetworkString<_32> SessionDisplayName { get; set; }

    [Networked]
    public NetworkString<_64> SessionID { get; set; }

    [Networked]
    public NetworkString<_16> SessionHash { get; set; }

    [Networked]
    public int SessionStartTime { get; set; }  // Changed from NetworkInt to int

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
        UnityEngine.Debug.Log("GameStateManager initialized");
    }

    public override void Spawned()
    {
        UnityEngine.Debug.Log($"GameStateManager spawned - HasStateAuthority: {Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            // Get session info from NetworkRunnerHandler
            NetworkRunnerHandler runnerHandler = FindObjectOfType<NetworkRunnerHandler>();
            if (runnerHandler != null)
            {
                SessionDisplayName = runnerHandler.SessionDisplayName;
                SessionID = runnerHandler.SessionUniqueID;
                SessionHash = runnerHandler.SessionHash;
                SessionStartTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                UnityEngine.Debug.Log($"GameStateManager: Session info set - {SessionDisplayName} | {SessionID} | {SessionHash}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("GameStateManager: Could not find NetworkRunnerHandler for session info");
            }

            // Start directly in playing state
            State = GameState.Playing;
            UnityEngine.Debug.Log("Game started in Playing state");
        }

        // Initialize the dictionary if needed
        if (PlayersReady.Count == 0 && Object.HasStateAuthority)
        {
            PlayersReady.Clear(); // Ensure it's empty
            UnityEngine.Debug.Log("Initialized PlayersReady dictionary");
        }

        // Broadcast initial state
        OnGameStateChanged();
    }

    // Manual state change notification method
    public void OnGameStateChanged()
    {
        UnityEngine.Debug.Log($"Game state changed to: {State}");

        // Handle state transition logic here
        switch (State)
        {
            case GameState.Playing:
                UnityEngine.Debug.Log("Game is now in Playing state");
                break;
            case GameState.GameOver:
                UnityEngine.Debug.Log("Game Over");
                break;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerReady(PlayerRef player, bool isReady)
    {
        UnityEngine.Debug.Log($"RPC_SetPlayerReady called for player {player}, ready: {isReady}");

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
            UnityEngine.Debug.Log("All players ready");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartGame()
    {
        UnityEngine.Debug.Log("RPC_StartGame called");

        // Make sure this only executes on the State Authority (server)
        if (!Object.HasStateAuthority) return;

        State = GameState.Playing;
        OnGameStateChanged();
    }

    // Test function - can be called from anywhere to verify state
    public void PrintCurrentState()
    {
        UnityEngine.Debug.LogWarning($"CURRENT GAME STATE: {State}, Instance ID: {GetInstanceID()}");
        UnityEngine.Debug.LogWarning($"SESSION INFO: Name={SessionDisplayName}, ID={SessionID}, Hash={SessionHash}");

        // Print all player ready states - check Count instead of null
        UnityEngine.Debug.Log($"Players ready status ({PlayersReady.Count} players):");
        if (PlayersReady.Count > 0)
        {
            foreach (var kvp in PlayersReady)
            {
                UnityEngine.Debug.Log($"  Player {kvp.Key}: {(kvp.Value ? "READY" : "NOT READY")}");
            }
        }
        else
        {
            UnityEngine.Debug.Log("  No players have registered ready status yet");
        }
    }

    // Direct force change to Playing state - for testing only
    public void ForcePlayingState()
    {
        if (Object.HasStateAuthority)
        {
            UnityEngine.Debug.LogWarning("FORCING GAME STATE TO PLAYING");
            State = GameState.Playing;
            OnGameStateChanged();
        }
        else
        {
            UnityEngine.Debug.LogError("Cannot force state change - not state authority");
        }
    }
}