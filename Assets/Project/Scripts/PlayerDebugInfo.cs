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

            // Get current game state if available
            if (GameStateManager.Instance != null)
            {
                info += $"GameState: {GameStateManager.Instance.State}\n";
            }

            // Get look rotation if KCC is available
            if (_kcc != null)
            {
                Vector2 lookRotation = _kcc.GetLookRotation(true, true);
                info += $"Look: P={lookRotation.x:F1}, Y={lookRotation.y:F1}\n";
            }

            _debugText.text = info;

            // Make the text face the camera
            if (Camera.main != null)
            {
                _debugText.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}