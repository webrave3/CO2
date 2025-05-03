using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;

public class SimpleKCCNetworkSync : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SimpleKCC _kcc;

    // Store networked values for rotation
    [Networked]
    private Vector2 NetworkedLookRotation { get; set; }

    private Vector2 _lastSentRotation;
    private const float ROTATION_SYNC_THRESHOLD = 0.1f;
    private const bool DEBUG_ROTATION = true;

    private void Awake()
    {
        if (_kcc == null)
            _kcc = GetComponent<SimpleKCC>();
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            Debug.Log($"KCC Network Sync - I have input authority (Player {Object.InputAuthority})");
        }
        else if (Object.HasStateAuthority)
        {
            Debug.Log($"KCC Network Sync - I have state authority (Server)");
        }
        else
        {
            Debug.Log($"KCC Network Sync - I am a proxy");
        }

        // Initialize network rotation on spawn
        if (Object.HasStateAuthority)
        {
            NetworkedLookRotation = Vector2.zero;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasInputAuthority)
        {
            // Get current rotation from KCC
            Vector2 currentRotation = _kcc.GetLookRotation(true, true);

            // Check if rotation changed significantly
            if (Vector2.Distance(currentRotation, _lastSentRotation) > ROTATION_SYNC_THRESHOLD)
            {
                NetworkedLookRotation = currentRotation;
                _lastSentRotation = currentRotation;

                if (DEBUG_ROTATION)
                {
                    Debug.Log($"[Client] Sending rotation to network: {currentRotation}");
                }
            }
        }
        else if (Object.HasStateAuthority)
        {
            // Server just stays updated with latest networked value
            if (DEBUG_ROTATION)
            {
                Debug.Log($"[Server] Current networked rotation: {NetworkedLookRotation}");
            }
        }
        else
        {
            // Proxies (non-input, non-state authority) just read the networked value
            if (DEBUG_ROTATION)
            {
                Debug.Log($"[Proxy] Applying networked rotation: {NetworkedLookRotation}");
            }
        }
    }

    public override void Render()
    {
        // Apply networked rotation to non-input authority objects
        if (!Object.HasInputAuthority)
        {
            _kcc.SetLookRotation(NetworkedLookRotation);

            if (DEBUG_ROTATION)
            {
                Debug.Log($"Render - Setting KCC rotation to: {NetworkedLookRotation}");
            }
        }
    }
}