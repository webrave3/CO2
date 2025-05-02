using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class BootstrapManager : MonoBehaviour
{
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    private NetworkRunnerHandler _networkRunnerHandler;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("Bootstrap initialized");

        // Create or find NetworkRunnerHandler
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();
        if (_networkRunnerHandler == null)
        {
            GameObject networkRunnerObject = new GameObject("NetworkRunner");
            _networkRunnerHandler = networkRunnerObject.AddComponent<NetworkRunnerHandler>();
            networkRunnerObject.AddComponent<NetworkRunner>();
            networkRunnerObject.AddComponent<NetworkSceneManagerDefault>();
            networkRunnerObject.AddComponent<PlayerInput>();
            DontDestroyOnLoad(networkRunnerObject);
        }

        // Set up any other core systems here

        // Load the main menu scene
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    public NetworkRunnerHandler GetNetworkRunnerHandler()
    {
        return _networkRunnerHandler;
    }
}