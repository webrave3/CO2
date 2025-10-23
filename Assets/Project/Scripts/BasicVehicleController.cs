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
    [SerializeField] private Transform _driverSeatPosition;
    [SerializeField] private GameObject _visualsRoot;
    [SerializeField] private Camera _vehicleCamera; // NEW: Dedicated camera for driving

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

        if (_vehicleCamera != null)
        {
            _vehicleCamera.gameObject.SetActive(false); // Ensure camera starts disabled
        }
    }

    // Unity's Update is used for visual effects (wheel mesh rotation) 
    // as it doesn't need to be perfectly deterministic.
    private void Update()
    {
        // Call the visual synchronization method every frame
        SyncWheelVisuals(_frontLeftWheel);
        SyncWheelVisuals(_frontRightWheel);
        SyncWheelVisuals(_rearLeftWheel);
        SyncWheelVisuals(_rearRightWheel);
    }

    private void SyncWheelVisuals(WheelCollider wheel)
    {
        // Simple function to make the wheel mesh match the collider's position/rotation
        if (wheel.transform.childCount > 0)
        {
            Transform mesh = wheel.transform.GetChild(0);
            Vector3 position;
            Quaternion rotation;

            wheel.GetWorldPose(out position, out rotation);

            mesh.position = position;
            mesh.rotation = rotation;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data) && Driver != PlayerRef.None && Object.HasStateAuthority)
        {
            ApplyDriving(data.VehicleSteer, data.VehicleThrottle);
        }
    }

    private void ApplyDriving(float steerInput, float throttleInput)
    {
        float currentSteerAngle = _maxSteerAngle * steerInput;
        _frontLeftWheel.steerAngle = currentSteerAngle;
        _frontRightWheel.steerAngle = currentSteerAngle;

        float currentMotorTorque = _motorTorque * throttleInput;
        float currentBrakeTorque = 0f;

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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEnterVehicle(PlayerRef requestingPlayer)
    {
        if (!IsOccupied && Object.HasStateAuthority)
        {
            Driver = requestingPlayer;
            IsOccupied = true;

            // FIX: Use Runner.TryGetPlayerObject to find the player object
            if (Runner.TryGetPlayerObject(requestingPlayer, out var playerObj))
            {
                _driverNetworkObject = playerObj;

                if (_driverNetworkObject.TryGetComponent<PlayerController>(out var playerController))
                {
                    playerController.SetInVehicle(true, this.Object.Id);
                    RPC_ToggleVehicleCamera(true); // Toggle camera via RPC after entering
                }
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestExitVehicle(PlayerRef requestingPlayer)
    {
        if (IsOccupied && requestingPlayer == Driver && Object.HasStateAuthority)
        {
            Vector3 exitPosition = transform.position + transform.right * 2.0f + Vector3.up * 0.5f;

            if (_driverNetworkObject != null && _driverNetworkObject.TryGetComponent<PlayerController>(out var playerController))
            {
                // FIX: Passed NetworkId.Invalid (default) instead of null
                playerController.SetInVehicle(false, default, exitPosition);
                RPC_ToggleVehicleCamera(false); // Toggle camera via RPC before exiting
            }

            Driver = PlayerRef.None;
            IsOccupied = false;
            _driverNetworkObject = null;
        }
    }

    // NEW RPC: Toggles the vehicle camera on all clients (called by host/server)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ToggleVehicleCamera(bool enable)
    {
        if (_vehicleCamera != null)
        {
            _vehicleCamera.gameObject.SetActive(enable);

            // This is just for demonstration. You'd typically use a more sophisticated method 
            // to enable/disable camera controls and input specific to the vehicle camera.
        }
    }


    public float GetInteractionRadius()
    {
        return _interactionRadius;
    }
}
