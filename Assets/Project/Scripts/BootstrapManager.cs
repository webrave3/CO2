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
            Debug.LogWarning("[BootstrapManager] Multiple instances detected - destroying duplicate");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[BootstrapManager] Bootstrap initialized as singleton");

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
            Debug.Log("[BootstrapManager] Found existing NetworkRunnerHandler in scene");
            DontDestroyOnLoad(_networkRunnerHandler.gameObject);
            return;
        }

        // If not found, instantiate from prefab
        if (_networkRunnerPrefab != null)
        {
            Debug.Log("[BootstrapManager] Creating NetworkRunnerHandler from prefab");
            GameObject networkRunnerObject = Instantiate(_networkRunnerPrefab);

            // Verify the prefab has the component
            _networkRunnerHandler = networkRunnerObject.GetComponent<NetworkRunnerHandler>();

            if (_networkRunnerHandler != null)
            {
                Debug.Log("[BootstrapManager] NetworkRunnerHandler instantiated successfully");
                DontDestroyOnLoad(networkRunnerObject);
            }
            else
            {
                Debug.LogError("[BootstrapManager] NetworkRunnerPrefab does not have NetworkRunnerHandler component!");
            }
        }
        else
        {
            // Last resort: create a new GameObject with the component
            Debug.LogWarning("[BootstrapManager] NetworkRunnerPrefab not assigned, creating basic NetworkRunnerHandler");
            GameObject networkRunnerObject = new GameObject("NetworkRunnerHandler");
            _networkRunnerHandler = networkRunnerObject.AddComponent<NetworkRunnerHandler>();

            // Add NetworkRunner component if needed
            if (networkRunnerObject.GetComponent<NetworkRunner>() == null)
                networkRunnerObject.AddComponent<NetworkRunner>();

            DontDestroyOnLoad(networkRunnerObject);
            Debug.Log("[BootstrapManager] Basic NetworkRunnerHandler created");
        }
    }

    public NetworkRunnerHandler GetNetworkRunnerHandler()
    {
        if (_networkRunnerHandler == null)
        {
            Debug.LogWarning("[BootstrapManager] GetNetworkRunnerHandler called but handler is null!");

            // Try to find it again as a fallback
            _networkRunnerHandler = FindObjectOfType<NetworkRunnerHandler>();

            if (_networkRunnerHandler == null)
            {
                Debug.LogError("[BootstrapManager] Critical error - NetworkRunnerHandler not found in any scene!");
                // Re-initialize as last resort
                InitializeNetworkRunner();
            }
        }

        return _networkRunnerHandler;
    }
}