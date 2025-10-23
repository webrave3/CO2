using UnityEngine;
using Fusion;

// Attach this to any object that needs to react to game state changes
public class GameStateObserver : MonoBehaviour
{
    private GameStateManager _gameStateManager;
    private GameState _lastObservedState = GameState.MainMenu; // Initialize to a default

    private void Start()
    {
        // Find the GameStateManager
        _gameStateManager = FindObjectOfType<GameStateManager>();

        if (_gameStateManager != null)
        {
            // Store initial state
            _lastObservedState = _gameStateManager.State;
            OnGameStateChanged(GameState.MainMenu, _lastObservedState); // Trigger initial state logic
        }
    }

    private void Update()
    {
        if (_gameStateManager != null && _lastObservedState != _gameStateManager.State)
        {
            // State has changed
            GameState previousState = _lastObservedState;
            _lastObservedState = _gameStateManager.State;
            OnGameStateChanged(previousState, _lastObservedState);
        }
        else if (_gameStateManager == null)
        {
            // Optionally try to find it again if it wasn't available at Start
            _gameStateManager = FindObjectOfType<GameStateManager>();
        }
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        // Add custom state transition logic here
        // This is useful for non-networked objects that need to respond
        // Example: Enable/disable UI, change music, etc.

        if (newState == GameState.Playing)
        {
            // Actions to take when game starts playing
        }
        else if (newState == GameState.GameOver)
        {
            // Actions for game over
        }
    }
}