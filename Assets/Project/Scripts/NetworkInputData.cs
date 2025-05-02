using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public float HorizontalInput;
    public float VerticalInput;
    public Vector2 MouseDelta;
    public NetworkBool Jump;
}