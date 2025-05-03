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
        Debug.Log("GameStateManager initialized");
    }

    public override void Spawned()
    {
        Debug.Log($"GameStateManager spawned - HasStateAuthority: {Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene.Contains("Lobby"))
            {
                State = GameState.Lobby;
                Debug.Log("Setting initial state to Lobby based on scene name");
            }
            else if (currentScene.Contains("Game"))
            {
                State = GameState.Playing;
                Debug.Log("Setting initial state to Playing based on scene name");
            }
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
        Debug.Log("Notifying all players of game start");

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            Debug.Log($"Notifying player {player.gameObject.name} of game start");
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