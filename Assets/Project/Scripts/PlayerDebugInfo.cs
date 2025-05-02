using UnityEngine;
using TMPro;
using Fusion;
using Fusion.Addons.SimpleKCC;

public class PlayerDebugInfo : MonoBehaviour
{
    [SerializeField] private TextMeshPro _debugText;
    [SerializeField] private Vector3 _offset = new Vector3(0, 2f, 0);

    private PlayerController _playerController;
    private NetworkObject _networkObject;
    private SimpleKCC _kcc;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _networkObject = GetComponent<NetworkObject>();
        _kcc = GetComponent<SimpleKCC>();

        // Create text component if not assigned
        if (_debugText == null)
        {
            GameObject textObj = new GameObject("DebugText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = _offset;

            _debugText = textObj.AddComponent<TextMeshPro>();
            _debugText.fontSize = 3;
            _debugText.alignment = TextAlignmentOptions.Center;
            _debugText.rectTransform.sizeDelta = new Vector2(5, 2);
        }
    }

    private void Update()
    {
        if (_debugText != null)
        {
            string info = $"Player {(_networkObject?.InputAuthority.PlayerId.ToString() ?? "Unknown")}\n";
            info += $"Position: {transform.position.ToString("F1")}\n";
            info += $"HasInputAuth: {(_networkObject?.HasInputAuthority.ToString() ?? "Unknown")}\n";
            info += $"IsInLobby: {(_playerController?.IsInLobby.ToString() ?? "Unknown")}\n";

            // Instead of accessing Velocity directly, calculate recent movement
            Vector3 currentPos = transform.position;
            info += $"Movement: {(_playerController != null ? "Check Input" : "No Controller")}\n";

            _debugText.text = info;

            // Make the text face the camera
            if (Camera.main != null)
            {
                _debugText.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}