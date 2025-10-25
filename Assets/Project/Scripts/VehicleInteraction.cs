using Fusion;
using UnityEngine;

public class VehicleInteraction : NetworkBehaviour
{
    [SerializeField] private float _interactionCheckRadius = 1.5f;
    [SerializeField] private LayerMask _vehicleLayer; // Make sure your NetworkedPrometheusCar prefab is on this layer

    private NetworkedPrometeoCar _currentVehicle = null;

    public override void FixedUpdateNetwork()
    {
        // Only process input if we have authority and input is available
        if (GetInput(out NetworkInputData data) && HasInputAuthority)
        {
            // If E is pressed
            if (data.Use)
            {
                // --- Refined Debug ---
                Debug.Log($"[VehicleInteraction] 'Use' input detected (E key pressed) on Tick: {Runner.Tick}. Current Vehicle: {(_currentVehicle == null ? "None" : _currentVehicle.Id.ToString())}");
                // --- End Refined Debug ---

                if (_currentVehicle != null)
                {
                    // --- Request Exit ---
                    Debug.Log($"[VehicleInteraction] Attempting RPC_RequestExitVehicle for: {_currentVehicle.Id}");
                    _currentVehicle.RPC_RequestExitVehicle(Runner.LocalPlayer);
                }
                else
                {
                    // --- Try to Find and Enter Vehicle ---
                    Debug.Log("[VehicleInteraction] Not in a vehicle. Attempting FindNearestVehicle...");
                    NetworkedPrometeoCar nearestVehicle = FindNearestVehicle();

                    if (nearestVehicle != null)
                    {
                        Debug.Log($"[VehicleInteraction] Found nearest vehicle: {nearestVehicle.Id}. Attempting RPC_RequestEnterVehicle...");
                        nearestVehicle.RPC_RequestEnterVehicle(Runner.LocalPlayer, this.Object);
                    }
                    else
                    {
                        Debug.Log("[VehicleInteraction] No vehicle found in range to enter.");
                    }
                }
            }
        }
    }

    private NetworkedPrometeoCar FindNearestVehicle()
    {
        // Debug.Log($"[VehicleInteraction] Checking OverlapSphere. Position: {transform.position}, Radius: {_interactionCheckRadius}, Layer: {_vehicleLayer.value}");

        Collider[] colliders = Physics.OverlapSphere(transform.position, _interactionCheckRadius, _vehicleLayer);
        NetworkedPrometeoCar closestVehicle = null;
        float minDistance = float.MaxValue;

        if (colliders.Length == 0)
        {
            // Debug.Log("[VehicleInteraction] OverlapSphere found 0 colliders on vehicle layer."); // Less spammy
            return null;
        }

        Debug.Log($"[VehicleInteraction] OverlapSphere found {colliders.Length} colliders on vehicle layer. Checking them...");

        foreach (var col in colliders)
        {
            Debug.Log($"[VehicleInteraction] Checking collider: '{col.gameObject.name}' on GameObject '{col.transform.root.name}' (Tag: {col.gameObject.tag}, Layer: {LayerMask.LayerToName(col.gameObject.layer)})");

            // --- *** USE GetComponentInParent *** ---
            // Search upwards from the collider's transform for the NetworkedPrometeoCar script
            NetworkedPrometeoCar vehicle = col.GetComponentInParent<NetworkedPrometeoCar>();
            // --- *** END MODIFICATION *** ---

            if (vehicle != null) // Check if the script was found in parents
            {
                Debug.Log($"... Found NetworkedPrometeoCar component on '{vehicle.gameObject.name}' by searching parents of '{col.gameObject.name}'.");

                // Calculate distance from player to the object *with the script* (the root car object)
                float distance = Vector3.Distance(transform.position, vehicle.transform.position);
                float vehicleInteractionRadius = vehicle.GetInteractionRadius();

                Debug.Log($"... Player Distance to '{vehicle.gameObject.name}': {distance:F2}. Vehicle Interaction Radius: {vehicleInteractionRadius:F2}");

                bool isInRange = distance <= vehicleInteractionRadius;
                Debug.Log($"... Is within vehicle's radius? {isInRange}");

                // Check if this vehicle is closer than the current best candidate AND within its radius
                if (distance < minDistance && isInRange)
                {
                    Debug.Log($"... >>> '{vehicle.gameObject.name}' (ID: {vehicle.Id}) is the new closest valid candidate. <<<");
                    minDistance = distance;
                    closestVehicle = vehicle;
                }
                else if (!isInRange)
                {
                    Debug.Log($"... Rejected '{vehicle.gameObject.name}': Outside its own interaction radius.");
                }
                else
                {
                    Debug.Log($"... Rejected '{vehicle.gameObject.name}': Not closer than current candidate (Dist: {distance:F2}, MinDist: {minDistance:F2})");
                }
            }
            else
            {
                // This log might still appear for other colliders on the layer that aren't part of the car, which is fine.
                Debug.Log($"... Collider '{col.gameObject.name}' does NOT have a NetworkedPrometeoCar component in its hierarchy.");
            }
        }

        if (closestVehicle != null) Debug.Log($"[VehicleInteraction] FindNearestVehicle returning: {closestVehicle.Id}");
        else Debug.Log("[VehicleInteraction] FindNearestVehicle returning: null (No VALID vehicle found in range/with script)");

        return closestVehicle;
    }

    public void SetCurrentVehicle(NetworkedPrometeoCar vehicle)
    {
        _currentVehicle = vehicle;
        // Optional log: Debug.Log(vehicle == null ? "[VehicleInteraction] SetCurrentVehicle(null)" : $"[VehicleInteraction] SetCurrentVehicle({vehicle.Id})");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionCheckRadius);
    }
}