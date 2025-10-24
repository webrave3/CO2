using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BasicVehicleController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private WheelCollider _frontLeftWheel;
    [SerializeField] private WheelCollider _frontRightWheel;
    [SerializeField] private WheelCollider _rearLeftWheel;
    [SerializeField] private WheelCollider _rearRightWheel;
    [SerializeField] private Transform _driverSeatPosition; // Optional: For placing the player visually if needed later
    [SerializeField] private GameObject _visualsRoot; // Optional: Parent of meshes if you want to hide/show them
    [SerializeField] private Camera _vehicleCamera; // Dedicated camera for driving

    [Header("Vehicle Settings")]
    [SerializeField] private float _motorTorque = 1500f;
    [SerializeField] private float _brakeTorque = 2000f;
    [SerializeField] private float _maxSteerAngle = 30f;
    [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0, -0.5f, 0);

    [Header("Interaction")]
    // Make this public or provide a getter if VehicleInteraction needs it
    [SerializeField] public float _interactionRadius = 3f; // Interaction radius specific to the vehicle

    [Networked] public PlayerRef Driver { get; private set; } = PlayerRef.None;
    [Networked] private NetworkBool IsOccupied { get; set; }

    private Rigidbody _rb;
    private NetworkObject _driverNetworkObject = null; // Store reference to the driver's NetworkObject

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.centerOfMass += _centerOfMassOffset; // Adjust center of mass

        // Ensure vehicle camera starts disabled
        if (_vehicleCamera != null)
        {
            _vehicleCamera.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError($"Vehicle Camera is not assigned on {gameObject.name}!");
        }
    }

    // Update is fine for visual-only things like wheel rotation
    private void Update()
    {
        // Simple wheel mesh synchronization
        SyncWheelVisuals(_frontLeftWheel);
        SyncWheelVisuals(_frontRightWheel);
        SyncWheelVisuals(_rearLeftWheel);
        SyncWheelVisuals(_rearRightWheel);
    }

    private void SyncWheelVisuals(WheelCollider wheel)
    {
        if (wheel == null || wheel.transform.childCount == 0) return; // Guard clause

        Transform mesh = wheel.transform.GetChild(0); // Assumes mesh is the first child
        Vector3 position;
        Quaternion rotation;
        wheel.GetWorldPose(out position, out rotation);
        mesh.position = position;
        mesh.rotation = rotation;
    }

    public override void FixedUpdateNetwork()
    {
        // Driving logic runs on State Authority using input from the Driver
        if (Object.HasStateAuthority)
        {
            if (Driver != PlayerRef.None && Runner.TryGetInputForPlayer(Driver, out NetworkInputData input))
            {
                ApplyDriving(input.VehicleSteer, input.VehicleThrottle);
            }
            else
            {
                // Apply brakes or zero torque if no driver or input missing
                ApplyDriving(0, 0); // Ensure vehicle stops if driver leaves or loses input
            }
        }
    }

    private void ApplyDriving(float steerInput, float throttleInput)
    {
        // Steering
        float currentSteerAngle = _maxSteerAngle * steerInput;
        if (_frontLeftWheel != null) _frontLeftWheel.steerAngle = currentSteerAngle;
        if (_frontRightWheel != null) _frontRightWheel.steerAngle = currentSteerAngle;


        // Throttle/Brake
        float currentMotorTorque = 0f;
        float currentBrakeTorque = 0f;

        // Simplified braking logic: Apply brake if throttle is opposite to current velocity direction, or if throttle is near zero
        float forwardVelocity = transform.InverseTransformDirection(_rb.linearVelocity).z;
        bool isBraking = (throttleInput < -0.1f && forwardVelocity > 0.1f) ||
                         (throttleInput > 0.1f && forwardVelocity < -0.1f) ||
                         (Mathf.Abs(throttleInput) < 0.1f);

        if (isBraking)
        {
            currentMotorTorque = 0f;
            currentBrakeTorque = _brakeTorque * Mathf.Abs(throttleInput); // Brake harder with more reverse input
            if (Mathf.Abs(throttleInput) < 0.1f) currentBrakeTorque = 100f; // Apply gentle brake when idle
        }
        else
        {
            currentMotorTorque = _motorTorque * throttleInput;
            currentBrakeTorque = 0f;
        }

        // Apply torque to rear wheels (check for null)
        if (_rearLeftWheel != null) _rearLeftWheel.motorTorque = currentMotorTorque;
        if (_rearRightWheel != null) _rearRightWheel.motorTorque = currentMotorTorque;

        // Apply brake torque to all wheels (check for null)
        if (_frontLeftWheel != null) _frontLeftWheel.brakeTorque = currentBrakeTorque;
        if (_frontRightWheel != null) _frontRightWheel.brakeTorque = currentBrakeTorque;
        if (_rearLeftWheel != null) _rearLeftWheel.brakeTorque = currentBrakeTorque;
        if (_rearRightWheel != null) _rearRightWheel.brakeTorque = currentBrakeTorque;
    }

    // --- RPC MODIFICATION #1: Added "NetworkObject playerNetworkObject" parameter ---
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEnterVehicle(PlayerRef requestingPlayer, NetworkObject playerNetworkObject)
    {
        // --- DEBUG: Log RPC reception ---
        Debug.Log($"[Server] RPC_RequestEnterVehicle received from Player {requestingPlayer}. HasStateAuthority: {Object.HasStateAuthority}. IsOccupied: {IsOccupied}");
        // --- End DEBUG ---

        // Check if the RPC is running on the State Authority and the vehicle is not already occupied
        if (!IsOccupied && Object.HasStateAuthority)
        {
            // --- DEBUG: Log conditions met ---
            Debug.Log($"[Server] Conditions met. Setting Driver to {requestingPlayer} and IsOccupied to true.");
            // --- End DEBUG ---

            Driver = requestingPlayer;
            IsOccupied = true;

            // --- RPC MODIFICATION #2: Replaced the failing TryGetPlayerObject block ---
            // Use the NetworkObject passed directly into the RPC
            if (playerNetworkObject != null)
            {
                // --- DEBUG: Log player object received ---
                Debug.Log($"[Server] Received NetworkObject for Player {requestingPlayer}. Object ID: {playerNetworkObject.Id}");
                // --- End DEBUG ---
                _driverNetworkObject = playerNetworkObject; // Store reference

                // Try to get the PlayerController component from the player's NetworkObject
                if (_driverNetworkObject.TryGetComponent<PlayerController>(out var playerController))
                {
                    // --- DEBUG: Log PlayerController found and calling SetInVehicle ---
                    Debug.Log($"[Server] Found PlayerController. Calling SetInVehicle(true, {this.Object.Id}) on Player {requestingPlayer}'s object.");
                    // --- End DEBUG ---

                    // Update the player's state across the network via the PlayerController
                    playerController.SetInVehicle(true, this.Object.Id);

                    // --- DEBUG: Log calling RPC_ToggleVehicleCamera ---
                    Debug.Log($"[Server] Calling RPC_ToggleVehicleCamera(true).");
                    // --- End DEBUG ---

                    // Activate the vehicle's camera for all clients via another RPC
                    RPC_ToggleVehicleCamera(true);
                }
                else
                {
                    // --- DEBUG: Log PlayerController NOT found ---
                    Debug.LogError($"[Server] Failed to get PlayerController component from Player {requestingPlayer}'s NetworkObject!");
                    // --- End DEBUG ---
                }
            }
            else
            {
                // --- DEBUG: Log Player Object was NULL ---
                Debug.LogError($"[Server] Received NULL NetworkObject for Player {requestingPlayer} from RPC!");
                // --- End DEBUG ---
            }
            // --- END OF MODIFICATION #2 ---
        }
        else if (IsOccupied)
        {
            // --- DEBUG: Log vehicle already occupied ---
            Debug.LogWarning($"[Server] Vehicle {Object.Id} already occupied by Driver {Driver}. Ignoring request from {requestingPlayer}.");
            // --- End DEBUG ---
        }
        else if (!Object.HasStateAuthority)
        {
            // --- DEBUG: Log not state authority ---
            Debug.LogError($"[Server] RPC_RequestEnterVehicle called on an object without State Authority! This shouldn't happen.");
            // --- End DEBUG ---
        }
    }


    // RPC called by the driver (Player with InputAuthority relative to their player object) to request exiting
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestExitVehicle(PlayerRef requestingPlayer)
    {
        // --- DEBUG: Log RPC reception ---
        Debug.Log($"[Server] RPC_RequestExitVehicle received from Player {requestingPlayer}. Current Driver: {Driver}. IsOccupied: {IsOccupied}. HasStateAuthority: {Object.HasStateAuthority}.");
        // --- End DEBUG ---

        // Only the State Authority processes the exit, and only if the requester is the current driver
        if (IsOccupied && requestingPlayer == Driver && Object.HasStateAuthority)
        {
            // --- DEBUG: Log exit conditions met ---
            Debug.Log($"[Server] Exit conditions met for Player {requestingPlayer}.");
            // --- End DEBUG ---

            // Calculate a safe exit position (e.g., beside the vehicle)
            Vector3 exitPosition = transform.position + transform.right * 2.0f + Vector3.up * 0.5f;

            // Use the stored reference to the driver's NetworkObject
            if (_driverNetworkObject != null && _driverNetworkObject.TryGetComponent<PlayerController>(out var playerController))
            {
                // --- DEBUG: Log calling SetInVehicle(false) ---
                Debug.Log($"[Server] Calling SetInVehicle(false, default, exitPosition:{exitPosition}) on Player {requestingPlayer}'s object.");
                // --- End DEBUG ---

                // Tell the player controller to revert state
                playerController.SetInVehicle(false, default, exitPosition); // NetworkId.Invalid is passed via default

                // --- DEBUG: Log calling RPC_ToggleVehicleCamera(false) ---
                Debug.Log($"[Server] Calling RPC_ToggleVehicleCamera(false).");
                // --- End DEBUG ---

                // Turn off the vehicle camera for everyone
                RPC_ToggleVehicleCamera(false);
            }
            else
            {
                // --- DEBUG: Log missing driver reference or PlayerController ---
                Debug.LogError($"[Server] Failed to get PlayerController for exiting driver {requestingPlayer}. _driverNetworkObject is {(_driverNetworkObject == null ? "NULL" : "Valid")}");
                // --- End DEBUG ---
            }

            // --- DEBUG: Log resetting driver state ---
            Debug.Log($"[Server] Resetting Driver to None, IsOccupied to false.");
            // --- End DEBUG ---

            // Clear driver state
            Driver = PlayerRef.None;
            IsOccupied = false;
            _driverNetworkObject = null; // Clear the reference

            // Reset wheel torque/brake when exiting to prevent runaway vehicle
            ApplyDriving(0, 0);
        }
        else if (!Object.HasStateAuthority)
        {
            Debug.LogError($"[Client Error] RPC_RequestExitVehicle called on client without State Authority. Sending to Server.");
            // This case is handled by Fusion routing the RPC to StateAuthority, but good to note.
        }
        else if (requestingPlayer != Driver)
        {
            Debug.LogWarning($"[Server] Player {requestingPlayer} tried to exit vehicle but is not the driver ({Driver}). Ignoring.");
        }
        else if (!IsOccupied)
        {
            Debug.LogWarning($"[Server] Player {requestingPlayer} tried to exit vehicle but it's not occupied. Ignoring.");
        }
    }

    // RPC called by the State Authority to enable/disable the vehicle camera on ALL clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ToggleVehicleCamera(bool enable)
    {
        // --- DEBUG: Log camera toggle RPC execution ---
        // Determine if this client is the one actually driving
        // *** CORRECTED: Changed Driver.IsValid to Driver != PlayerRef.None ***
        bool isDriverMe = (Runner != null && Driver != PlayerRef.None && Driver == Runner.LocalPlayer);
        // *** END CORRECTION ***

        Debug.Log($"[Client/Server {Runner?.LocalPlayer}] RPC_ToggleVehicleCamera({enable}) called. Is this player the driver? {isDriverMe}. Vehicle Camera is {(_vehicleCamera != null ? "assigned" : "NULL")}");
        // --- End DEBUG ---

        if (_vehicleCamera != null)
        {
            // Only enable the camera if this specific client is the driver
            // Always disable it if enable is false
            _vehicleCamera.gameObject.SetActive(enable && isDriverMe);

            // You might add logic here to enable/disable specific vehicle camera controls if needed
        }
        else
        {
            Debug.LogWarning($"Vehicle Camera is not assigned on {gameObject.name}. Cannot toggle camera state.");
        }
    }


    // Public getter for the interaction radius defined on the vehicle
    public float GetInteractionRadius()
    {
        return _interactionRadius;
    }
}