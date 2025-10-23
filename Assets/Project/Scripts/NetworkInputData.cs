using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    // Existing Player Movement Input
    public float HorizontalInput;
    public float VerticalInput;
    public Vector2 MouseDelta;
    public NetworkBool Jump;

    // Vehicle Input
    public float VehicleSteer;
    public float VehicleThrottle;
    public NetworkBool Use;
}