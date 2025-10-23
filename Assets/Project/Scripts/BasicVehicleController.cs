using Fusion;
using UnityEngine;

// Removed [RequireComponent(typeof(NetworkRigidbody))]
[RequireComponent(typeof(Rigidbody))]
public class BasicVehicleController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private WheelCollider _frontLeftWheel;
    [SerializeField] private WheelCollider _frontRightWheel;
    [SerializeField] private WheelCollider _rearLeftWheel;
    [SerializeField] private WheelCollider _rearRightWheel;
    [SerializeField] private Transform _driverSeatPosition;
    [SerializeField] private GameObject _visualsRoot;

    [Header("Vehicle Settings")]
    [SerializeField] private float _motorTorque = 1500f;
    [SerializeField] private float _brakeTorque = 2000f;
    [SerializeField] private float _maxSteerAngle = 30f;
    [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0, -0.5f, 0);

    [Header("Interaction")]
    [SerializeField] private float _interactionRadius = 3f;

    [Networked] public PlayerRef Driver { get; private set; } = PlayerRef.None;
    [Networked] private NetworkBool IsOccupied { get; set; }

    private Rigidbody _rb;
    private NetworkObject _driverNetworkObject = null;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.centerOfMass += _centerOfMassOffset;
    }

    public override void FixedUpdateNetwork()
    {
        // Only the State Authority (Host) processes driving input
        if (GetInput(out NetworkInputData data) && Driver != PlayerRef.None && Object.HasStateAuthority)
        {
            ApplyDriving(data.VehicleSteer, data.VehicleThrottle);
        }

        // Apply visual updates locally based on networked state
        UpdateVisuals(IsOccupied, Driver);
    }

    private void ApplyDriving(float steerInput, float throttleInput)
    {
        // Steering
        float currentSteerAngle = _maxSteerAngle * steerInput;
        _frontLeftWheel.steerAngle = currentSteerAngle;
        _frontRightWheel.steerAngle = currentSteerAngle;

        // Throttle/Brake
        float currentMotorTorque = _motorTorque * throttleInput;
        float currentBrakeTorque = 0f;

        // FIX: Use linearVelocity (resolves obsolete warning)
        float forwardVelocity = transform.InverseTransformDirection(_rb.linearVelocity).z;

        if ((throttleInput < 0 && forwardVelocity > 0.1f) || (throttleInput > 0 && forwardVelocity < -0.1f))
        {
            currentBrakeTorque = _brakeTorque * Mathf.Abs(throttleInput);
            currentMotorTorque = 0f;
        }
        else if (Mathf.Abs(throttleInput) < 0.1f)
        {
            currentBrakeTorque = 100f;
        }

        _rearLeftWheel.motorTorque = currentMotorTorque;
        _rearRightWheel.motorTorque = currentMotorTorque;

        _frontLeftWheel.brakeTorque = currentBrakeTorque;
        _frontRightWheel.brakeTorque = currentBrakeTorque;
        _rearLeftWheel.brakeTorque = currentBrakeTorque;
        _rearRightWheel.brakeTorque = currentBrakeTorque;
    }

    // Called by Player via RPC to request entering the vehicle
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEnterVehicle(PlayerRef requestingPlayer)
    {
        if (!IsOccupied && Object.HasStateAuthority)
        {
            Debug.Log($"Player {requestingPlayer} entering vehicle {Object.Id}");
            Driver = requestingPlayer;
            IsOccupied = true;

            // FIX: Use Runner.TryGetPlayerObject to find the player object from PlayerRef
            if (Runner.TryGetPlayerObject(requestingPlayer, out var playerObj))
            {
                _driverNetworkObject = playerObj;

                if (_driverNetworkObject.TryGetComponent<PlayerController>(out var playerController))
                {
                    // FIX: Pass the VEHICLE'S NetworkId (this.Object.Id)
                    playerController.SetInVehicle(true, this.Object.Id);
                }
            }
        }
    }

    // Called by Player via RPC to request exiting
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestExitVehicle(PlayerRef requestingPlayer)
    {
        if (IsOccupied && requestingPlayer == Driver && Object.HasStateAuthority)
        {
            Debug.Log($"Player {requestingPlayer} exiting vehicle {Object.Id}");

            // Find a safe exit position 
            Vector3 exitPosition = transform.position + transform.right * 2.0f + Vector3.up * 0.5f;

            // Re-enable the player controller
            if (_driverNetworkObject != null && _driverNetworkObject.TryGetComponent<PlayerController>(out var playerController))
            {
                // FIX: Pass NetworkId.Invalid (default) instead of null (resolves conversion error)
                playerController.SetInVehicle(false, default, exitPosition);
            }

            // Clear driver state
            Driver = PlayerRef.None;
            IsOccupied = false;
            _driverNetworkObject = null;
        }
    }

    private void UpdateVisuals(bool occupied, PlayerRef driverRef)
    {
        // Custom visual synchronization logic would go here
    }

    public float GetInteractionRadius()
    {
        return _interactionRadius;
    }
}