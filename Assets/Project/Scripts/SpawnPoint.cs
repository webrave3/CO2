using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private bool _drawGizmo = true;
    [SerializeField] private Color _gizmoColor = Color.green;
    [SerializeField] private float _gizmoSize = 1f;

    private void OnDrawGizmos()
    {
        if (_drawGizmo)
        {
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireSphere(transform.position, _gizmoSize);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * _gizmoSize);
        }
    }
}