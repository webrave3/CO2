using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    // Keep Gizmo drawing as it's useful for level design in the editor
    [SerializeField] private bool _drawGizmo = true;
    [SerializeField] private Color _gizmoColor = Color.green;
    [SerializeField] private float _gizmoRadius = 0.5f; // Use radius for sphere
    [SerializeField] private float _gizmoArrowLength = 0.75f;

    // OnDrawGizmos is editor-only and doesn't affect builds
    private void OnDrawGizmos()
    {
        if (_drawGizmo)
        {
            Color previousColor = Gizmos.color; // Store previous color
            Gizmos.color = _gizmoColor;

            // Draw a sphere at the position
            Gizmos.DrawWireSphere(transform.position, _gizmoRadius);

            // Draw an arrow indicating the forward direction
            Vector3 forwardEnd = transform.position + transform.forward * _gizmoArrowLength;
            Gizmos.DrawLine(transform.position, forwardEnd);

            // Arrowhead lines (simple version)
            Quaternion arrowRotation = Quaternion.LookRotation(transform.forward);
            float arrowHeadAngle = 20.0f;
            float arrowHeadLength = 0.25f;
            Vector3 right = arrowRotation * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
            Vector3 left = arrowRotation * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
            Gizmos.DrawLine(forwardEnd, forwardEnd + right * arrowHeadLength);
            Gizmos.DrawLine(forwardEnd, forwardEnd + left * arrowHeadLength);


            Gizmos.color = previousColor; // Restore previous color
        }
    }
}