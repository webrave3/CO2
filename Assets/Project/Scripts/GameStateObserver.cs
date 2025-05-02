using UnityEngine;
using Fusion;

// Attach this to any object that needs to react to game state changes
public class GameStateObserver : MonoBehaviour
{
    private GameStateManager _gameStateManager;
    private GameState _lastObservedState;

    private void Start()
    {
        // Find the GameStateManager
        _gameStateManager = FindObjectOfType<GameStateManager>();

        if (_gameStateManager != null)
        {
            // Store initial state
            _lastObservedState = _gameStateManager.State;
        }
        else
        {
            Debug.LogWarning("GameStateObserver could not find GameStateManager");
        }
    }

    private void Update()
    {
        if (_gameStateManager != null && _lastObservedState != _gameStateManager.State)
        {
            // State has changed
            OnGameStateChanged(_lastObservedState, _gameStateManager.State);
            _lastObservedState = _gameStateManager.State;
        }
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        Debug.Log($"GameStateObserver detected state change: {oldState} -> {newState}");

        // Add custom state transition logic as needed
        // This is useful for non-networked objects that need to respond to game state changes
    }
}