using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class BootstrapManager : MonoBehaviour
{
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private GameObject _networkRunnerPrefab; // Reference to the prefab

    private NetworkRunnerHandler _networkRunnerHandler;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("Bootstrap initialized");

        // Instantiate the NetworkRunner prefab instead of creating components
        GameObject networkRunnerObject = Instantiate(_networkRunnerPrefab);
        DontDestroyOnLoad(networkRunnerObject);

        _networkRunnerHandler = networkRunnerObject.GetComponent<NetworkRunnerHandler>();

        // Load the main menu scene
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    public NetworkRunnerHandler GetNetworkRunnerHandler()
    {
        return _networkRunnerHandler;
    }
}