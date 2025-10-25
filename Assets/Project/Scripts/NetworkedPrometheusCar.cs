using Fusion;
using UnityEngine;
using Fusion.Addons.SimpleKCC; // Added for KCC teleport
using Fusion.Addons.Physics; // <--- *** ADDED THIS LINE ***

// Ensure the necessary components are present on the GameObject
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(NetworkRigidbody3D))] // Use NetworkRigidbody3D

public class NetworkedPrometeoCar : NetworkBehaviour
{
    [Header("Vehicle Setup")]
    [SerializeField] private Transform _exitPoint; // Assign a child GameObject's transform for where the player exits
    [SerializeField] private float _interactionRadius = 2.5f; // How close player needs to be to enter

    [Header("Vehicle Components")] // Add a new header
    [SerializeField] private Camera _vehicleCamera;
    [SerializeField] private AudioListener _vehicleAudioListener; // Add reference for listener too

    // --- Networked State ---
    [Networked] public PlayerRef DriverRef { get; private set; } = PlayerRef.None;
    [Networked] public NetworkBool IsOccupied { get; private set; } = false;

    // --- Local References ---
    private PrometeoCarController _prometeoController;
    private NetworkRigidbody3D _networkRigidbody; // Use NetworkRigidbody3D

    // --- Input Storage ---
    // Store input values locally to be applied in Prometeo's Update/FixedUpdate
    private float _currentThrottleInput = 0f;
    private float _currentSteeringInput = 0f;
    private bool _currentHandbrakeInput = false;

    // --- Initialization ---
    private void Awake()
    {
        // Get references to components on this GameObject
        _prometeoController = GetComponent<PrometeoCarController>();
        _networkRigidbody = GetComponent<NetworkRigidbody3D>(); // Use NetworkRigidbody3D

        // Initial sanity checks
        if (_exitPoint == null)
        {
            Debug.LogWarning($"Vehicle {gameObject.name} needs an Exit Point Transform assigned.", this);
        }
        if (_prometeoController == null)
        {
            Debug.LogError($"PrometeoCarController not found on {gameObject.name}.", this);
        }
        if (_networkRigidbody == null)
        {
            Debug.LogError($"NetworkRigidbody3D not found on {gameObject.name}.", this);
        }
        if (_vehicleCamera == null)
        {
            // Try to find it automatically if not assigned
            _vehicleCamera = GetComponentInChildren<Camera>();
            if (_vehicleCamera == null)
            {
                Debug.LogError($"Vehicle Camera not found or assigned on {gameObject.name}.", this);
            }
            else
            {
                // Ensure it's disabled initially
                _vehicleCamera.enabled = false;
            }
        }
        if (_vehicleAudioListener == null && _vehicleCamera != null)
        {
            _vehicleAudioListener = _vehicleCamera.GetComponent<AudioListener>();
            if (_vehicleAudioListener == null)
            {
                Debug.LogWarning($"Vehicle Audio Listener not found on vehicle camera for {gameObject.name}. Adding one.", this);
                _vehicleAudioListener = _vehicleCamera.gameObject.AddComponent<AudioListener>();
            }
            _vehicleAudioListener.enabled = false; // Ensure disabled initially
        }
        else if (_vehicleCamera == null)
        {
            Debug.LogError($"Cannot find/add Audio Listener because Vehicle Camera is missing on {gameObject.name}.", this);
        }
    }
    

    public override void Spawned()
    {
        Debug.Log($"NetworkedPrometeoCar {Object.Id} Spawned. IsOccupied: {IsOccupied}");
        // We will enable/disable PrometeoController based on Input Authority later if needed.
    }

    // --- Core Network Update ---
    public override void FixedUpdateNetwork()
    {
        // Get the input from the network. If HasInputAuthority, it's the driver's input.
        if (GetInput(out NetworkInputData data))
        {
            // Only the driver (who has Input Authority) should provide input
            if (HasInputAuthority && IsOccupied)
            {
                // Store the inputs. We will apply them in Update/FixedUpdate inside PrometeoCarController
                _currentThrottleInput = data.VehicleThrottle;
                _currentSteeringInput = data.VehicleSteer;
                _currentHandbrakeInput = data.Jump; // Assuming Jump is handbrake for now

                // Directly pass inputs to PrometeoController
                _prometeoController.SetInputs(_currentThrottleInput, _currentSteeringInput, _currentHandbrakeInput);
            }
        }
        else if (HasInputAuthority && IsOccupied)
        {
            // If input is not available for the driver for some reason, ensure inputs are zeroed
            _currentThrottleInput = 0f;
            _currentSteeringInput = 0f;
            _currentHandbrakeInput = false;
            _prometeoController.SetInputs(_currentThrottleInput, _currentSteeringInput, _currentHandbrakeInput);
        }

        // --- Important Physics Consideration ---
        // The actual physics application (torque, steer angle) happens inside PrometeoCarController.
        // For Host Mode, Fusion's NetworkRigidbody synchronizes the stateAUTHORITY's simulation.
        // PrometeoCarController's physics logic should ideally only execute meaningful calculations
        // on the State Authority (Host) or maybe Input Authority. We modified it in Step 3.
    }

    // --- Enter/Exit RPCs ---

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEnterVehicle(PlayerRef player, NetworkId requestingPlayerObjectId)
    {
        Debug.Log($"RPC_RequestEnterVehicle received from Player {player}. Current Occupied: {IsOccupied}");

        if (!IsOccupied)
        {
            // Assign driver and authority
            IsOccupied = true;
            DriverRef = player;
            Object.AssignInputAuthority(player); // Give control to the player

            Debug.Log($"Vehicle {Object.Id} Assigning Input Authority to Player {player}.");

            // Find the PlayerController using the NetworkId passed in the RPC
            if (Runner.TryFindObject(requestingPlayerObjectId, out var playerNetworkObject))
            {
                if (playerNetworkObject.TryGetComponent<PlayerController>(out var playerController))
                {
                    // Tell the PlayerController it's now in this vehicle
                    playerController.SetInVehicle(true, Object.Id);
                    Debug.Log($"Told PlayerController {requestingPlayerObjectId} they are in vehicle {Object.Id}");
                }
                else { Debug.LogError($"Player Object {requestingPlayerObjectId} does not have a PlayerController component."); }
            }
            else { Debug.LogError($"Could not find Player Object with ID {requestingPlayerObjectId} to notify about entering vehicle."); }
        }
        else
        {
            Debug.Log($"Vehicle {Object.Id} entry requested by {player}, but already occupied by {DriverRef}.");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestExitVehicle(PlayerRef player)
    {
        Debug.Log($"RPC_RequestExitVehicle received from Player {player}. Current Driver: {DriverRef}");

        if (player == DriverRef) // Only the current driver can exit
        {
            Vector3 exitPosition = _exitPoint != null ? _exitPoint.position : transform.position + transform.forward * 2f + Vector3.up; // Default exit pos if none assigned
            Quaternion exitRotation = _exitPoint != null ? _exitPoint.rotation : transform.rotation; // Default exit rot if none assigned


            // Find the PlayerController using the PlayerRef
            PlayerController playerController = FindPlayerController(player);

            if (playerController != null)
            {
                // Teleport KCC directly BEFORE removing authority
                SimpleKCC kcc = playerController.GetComponent<SimpleKCC>();
                if (kcc != null)
                {
                    kcc.SetPosition(exitPosition);
                    // Optionally set rotation too, although KCC might handle its own look direction
                    // kcc.SetRotation(exitRotation);
                    Debug.Log($"Teleported Player {player} KCC to {exitPosition}");
                }
                else { Debug.LogError($"Could not find SimpleKCC on Player Object for Player {player}."); }


                // Tell the PlayerController it's no longer in a vehicle
                playerController.SetInVehicle(false, default, exitPosition); // Send exit pos again just in case
                Debug.Log($"Told PlayerController for Player {player} they have exited vehicle {Object.Id}");
            }
            else { Debug.LogError($"Could not find PlayerController for Player {player} to notify about exiting vehicle."); }

            // Clear driver and remove authority
            IsOccupied = false;
            DriverRef = PlayerRef.None;
            Object.RemoveInputAuthority(); // Remove control from the player
            Debug.Log($"Vehicle {Object.Id} Input Authority removed.");

            // Reset car inputs immediately on the Host
            _currentThrottleInput = 0f;
            _currentSteeringInput = 0f;
            _currentHandbrakeInput = false;
            _prometeoController.SetInputs(0f, 0f, false); // Reset inputs via the method
            _prometeoController.ResetSteeringAngle();
            _prometeoController.ThrottleOff();

            // Optionally reset physics state more forcefully if needed (use carefully)
            // _networkRigidbody.Rigidbody.velocity = Vector3.zero;
            // _networkRigidbody.Rigidbody.angularVelocity = Vector3.zero;
        }
        else
        {
            Debug.LogWarning($"Vehicle {Object.Id} exit requested by {player}, but current driver is {DriverRef}. Ignoring.");
        }
    }

    // Helper to find the PlayerController NetworkObject associated with a PlayerRef
    private PlayerController FindPlayerController(PlayerRef player)
    {
        // Runner.TryGetPlayerObject is a more direct way if available and reliable
        if (Runner.TryGetPlayerObject(player, out var playerNetworkObject))
        {
            if (playerNetworkObject.TryGetComponent<PlayerController>(out var pc))
            {
                return pc;
            }
        }

        // Fallback: Iterate if the above doesn't work (less efficient)
        Debug.LogWarning($"Could not find player object directly for PlayerRef {player}. Iterating...");
        foreach (var obj in Runner.GetAllBehaviours<PlayerController>())
        {
            if (obj.Object != null && obj.Object.InputAuthority == player)
            {
                return obj;
            }
        }
        return null; // Player object not found
    }

    public override void Render()
    {
        // Runs on all clients, every visual frame
        bool isDriver = Object.HasInputAuthority && IsOccupied;

        if (_vehicleCamera != null)
        {
            _vehicleCamera.enabled = isDriver;
        }
        if (_vehicleAudioListener != null)
        {
            _vehicleAudioListener.enabled = isDriver;
        }
    }

    // --- Interaction ---
    public float GetInteractionRadius()
    {
        return _interactionRadius;
    }

    // --- Gizmos ---
    private void OnDrawGizmosSelected()
    {
        // Draw interaction radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _interactionRadius);

        // Draw exit point
        if (_exitPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_exitPoint.position, 0.25f);
            Gizmos.DrawLine(_exitPoint.position, _exitPoint.position + _exitPoint.forward); // Show exit direction
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position + transform.forward * 2f + Vector3.up, 0.25f); // Show default exit pos
        }
    }
}