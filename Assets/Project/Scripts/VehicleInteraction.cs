using Fusion;
using UnityEngine;

public class VehicleInteraction : NetworkBehaviour
{
    [SerializeField] private float _interactionCheckRadius = 1.5f;
    [SerializeField] private LayerMask _vehicleLayer;

    private BasicVehicleController _currentVehicle = null;

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data) && HasInputAuthority)
        {
            // If E is pressed
            if (data.Use)
            {
                if (_currentVehicle != null)
                {
                    // --- Request Exit ---
                    Debug.Log("Requesting Exit Vehicle RPC");
                    _currentVehicle.RPC_RequestExitVehicle(Runner.LocalPlayer);
                }
                else
                {
                    // --- Try to Find and Enter Vehicle ---
                    BasicVehicleController nearestVehicle = FindNearestVehicle();
                    if (nearestVehicle != null)
                    {
                        Debug.Log("Requesting Enter Vehicle RPC for vehicle: " + nearestVehicle.Id);
                        nearestVehicle.RPC_RequestEnterVehicle(Runner.LocalPlayer);
                    }
                    else
                    {
                        Debug.Log("No vehicle found in range to enter.");
                    }
                }
            }
        }
    }

    private BasicVehicleController FindNearestVehicle()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _interactionCheckRadius, _vehicleLayer);
        BasicVehicleController closestVehicle = null;
        float minDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            if (col.TryGetComponent<BasicVehicleController>(out var vehicle))
            {
                float distance = Vector3.Distance(transform.position, vehicle.transform.position);
                if (distance < minDistance && distance <= vehicle.GetInteractionRadius())
                {
                    minDistance = distance;
                    closestVehicle = vehicle;
                }
            }
        }
        return closestVehicle;
    }

    // Called by PlayerController when the networked IsInVehicle state changes
    public void SetCurrentVehicle(BasicVehicleController vehicle)
    {
        _currentVehicle = vehicle;
        Debug.Log(vehicle == null ? "Player Exited Vehicle" : $"Player Entered Vehicle {vehicle.Id}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionCheckRadius);
    }
}