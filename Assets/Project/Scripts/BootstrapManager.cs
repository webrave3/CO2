using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class BootstrapManager : MonoBehaviour
{
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private GameObject _networkRunnerPrefab; // Reference to the prefab

    private NetworkRunnerHandler _networkRunnerHandler;
    private static BootstrapManager _instance;

    public static BootstrapManager Instance { get { return _instance; } }

    private void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeNetworkRunner();

        // Load the main menu scene
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void InitializeNetworkRunner()
    {
        // First check if NetworkRunnerHandler already exists in the scene
        _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

        if (_networkRunnerHandler != null)
        {
            DontDestroyOnLoad(_networkRunnerHandler.gameObject);
            return;
        }

        // If not found, instantiate from prefab
        if (_networkRunnerPrefab != null)
        {
            GameObject networkRunnerObject = Instantiate(_networkRunnerPrefab);

            // Verify the prefab has the component
            _networkRunnerHandler = networkRunnerObject.GetComponent<NetworkRunnerHandler>();

            if (_networkRunnerHandler != null)
            {
                DontDestroyOnLoad(networkRunnerObject);
            }
            else
            {
                // Fallback if component is missing on prefab
                GameObject.Destroy(networkRunnerObject); // Destroy the incorrect instance
                CreateBasicNetworkRunnerHandler();
            }
        }
        else
        {
            CreateBasicNetworkRunnerHandler();
        }
    }

    private void CreateBasicNetworkRunnerHandler()
    {
        // Create a new GameObject with the component
        GameObject networkRunnerObject = new GameObject("NetworkRunnerHandler");
        _networkRunnerHandler = networkRunnerObject.AddComponent<NetworkRunnerHandler>();

        // Add NetworkRunner component if needed
        if (networkRunnerObject.GetComponent<NetworkRunner>() == null)
            networkRunnerObject.AddComponent<NetworkRunner>();

        DontDestroyOnLoad(networkRunnerObject);
    }


    public NetworkRunnerHandler GetNetworkRunnerHandler()
    {
        if (_networkRunnerHandler == null)
        {
            // Try to find it again as a fallback
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

            if (_networkRunnerHandler == null)
            {
                // Re-initialize as last resort
                InitializeNetworkRunner();
            }
        }

        return _networkRunnerHandler;
    }
}