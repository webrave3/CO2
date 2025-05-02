using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using System;

public class CameraAngleLimiter : NetworkBehaviour
{
    [SerializeField] private SimpleKCC _kcc;
    [SerializeField] private float _maxUpAngle = 85f;  // Default vertical limit (up)
    [SerializeField] private float _maxDownAngle = 85f; // Default vertical limit (down)

    private void Awake()
    {
        if (_kcc == null) _kcc = GetComponent<SimpleKCC>();
    }

    public override void FixedUpdateNetwork()
    {
        if (_kcc != null && Object.HasInputAuthority)
        {
            // Get current pitch (x) rotation
            Vector2 lookRotation = _kcc.GetLookRotation(true, false);

            // Clamp the pitch within limits
            float clampedPitch = Mathf.Clamp(lookRotation.x, -_maxDownAngle, _maxUpAngle);

            // If the pitch was clamped, update it
            if (clampedPitch != lookRotation.x)
            {
                Vector2 newRotation = new Vector2(clampedPitch, lookRotation.y);
                _kcc.SetLookRotation(newRotation);
            }
        }
    }
}