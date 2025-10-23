using Fusion;
using UnityEngine;

public class VehicleInteraction : NetworkBehaviour
{
    [SerializeField] private float _interactionCheckRadius = 1.5f;
    [SerializeField] private LayerMask _vehicleLayer;

    private BasicVehicleController _currentVehicle = null; // Stores the vehicle we are currently in

    public override void FixedUpdateNetwork()
    {
        // Only process input if we have authority and input is available
        if (GetInput(out NetworkInputData data) && HasInputAuthority)
        {
            // If E is pressed
            if (data.Use)
            {
                // --- DEBUG: Log E press detection ---
                Debug.Log($"'Use' input detected (E key pressed). Current Vehicle: {(_currentVehicle == null ? "None" : _currentVehicle.Id.ToString())}");
                // --- End DEBUG ---

                if (_currentVehicle != null)
                {
                    // --- Request Exit ---
                    // --- DEBUG: Log exit attempt ---
                    Debug.Log($"Attempting to exit vehicle: {_currentVehicle.Id}");
                    // --- End DEBUG ---
                    _currentVehicle.RPC_RequestExitVehicle(Runner.LocalPlayer);
                }
                else
                {
                    // --- Try to Find and Enter Vehicle ---
                    // --- DEBUG: Log enter attempt ---
                    Debug.Log("Not in a vehicle. Attempting to find nearby vehicle...");
                    // --- End DEBUG ---
                    BasicVehicleController nearestVehicle = FindNearestVehicle();

                    if (nearestVehicle != null)
                    {
                        // --- DEBUG: Log vehicle found and RPC call ---
                        Debug.Log($"Found nearest vehicle: {nearestVehicle.Id}. Requesting Enter Vehicle RPC...");
                        // --- End DEBUG ---
                        nearestVehicle.RPC_RequestEnterVehicle(Runner.LocalPlayer);
                    }
                    else
                    {
                        // --- DEBUG: Log no vehicle found ---
                        Debug.Log("No vehicle found in range to enter.");
                        // --- End DEBUG ---
                    }
                }
            }
        }
    }

    private BasicVehicleController FindNearestVehicle()
    {
        // --- DEBUG: Log overlap sphere check ---
        Debug.Log($"Checking for vehicles within {_interactionCheckRadius} units on layer {_vehicleLayer.value}");
        // --- End DEBUG ---

        Collider[] colliders = Physics.OverlapSphere(transform.position, _interactionCheckRadius, _vehicleLayer);
        BasicVehicleController closestVehicle = null;
        float minDistance = float.MaxValue;

        // --- DEBUG: Log how many colliders found ---
        if (colliders.Length > 0)
        {
            Debug.Log($"Found {colliders.Length} colliders on the vehicle layer.");
        }
        else
        {
            Debug.Log("Found 0 colliders on the vehicle layer.");
        }
        // --- End DEBUG ---


        foreach (var col in colliders)
        {
            // --- DEBUG: Log each collider being checked ---
            Debug.Log($"Checking collider: {col.gameObject.name}");
            // --- End DEBUG ---

            if (col.TryGetComponent<BasicVehicleController>(out var vehicle))
            {
                float distance = Vector3.Distance(transform.position, vehicle.transform.position);

                // --- DEBUG: Log distance and interaction radius check ---
                Debug.Log($"... Found BasicVehicleController on {vehicle.gameObject.name}. Distance: {distance:F2}. Vehicle Interaction Radius: {vehicle.GetInteractionRadius():F2}");
                // --- End DEBUG ---

                if (distance < minDistance && distance <= vehicle.GetInteractionRadius())
                {
                    // --- DEBUG: Log successful candidate ---
                    Debug.Log($"... {vehicle.gameObject.name} is a valid candidate. Updating closest vehicle.");
                    // --- End DEBUG ---
                    minDistance = distance;
                    closestVehicle = vehicle;
                }
                else if (distance > vehicle.GetInteractionRadius())
                {
                    Debug.Log($"... {vehicle.gameObject.name} is too far based on its own interaction radius ({distance:F2} > {vehicle.GetInteractionRadius():F2}).");
                }
            }
            else
            {
                Debug.Log($"... {col.gameObject.name} does NOT have a BasicVehicleController component.");
            }
        }

        // --- DEBUG: Log the final result of the search ---
        if (closestVehicle != null)
        {
            Debug.Log($"FindNearestVehicle returning: {closestVehicle.Id}");
        }
        else
        {
            Debug.Log("FindNearestVehicle returning: null");
        }
        // --- End DEBUG ---
        return closestVehicle;
    }

    // Called by PlayerController when the networked IsInVehicle state changes
    public void SetCurrentVehicle(BasicVehicleController vehicle)
    {
        _currentVehicle = vehicle;
        // Optional log: Debug.Log(vehicle == null ? "Player Exited Vehicle" : $"Player Entered Vehicle {vehicle.Id}");
    }

    // Draws the interaction radius in the editor scene view when the player is selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionCheckRadius);
    }
}