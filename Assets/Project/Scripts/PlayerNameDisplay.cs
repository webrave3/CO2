using UnityEngine;
using TMPro;
using Fusion;

public class PlayerNameDisplay : NetworkBehaviour
{
    [SerializeField] private TextMeshPro _nameText;
    [SerializeField] private Transform _visualModel;
    [SerializeField] private Vector3 _offset = new Vector3(0, 1.5f, 0);
    [SerializeField] private Color _localPlayerColor = Color.green;
    [SerializeField] private Color _otherPlayerColor = Color.white;

    [Networked]
    public NetworkString<_32> PlayerName { get; set; }

    private string _lastDisplayedName = "";

    private void Awake()
    {
        // Create text component if not assigned
        if (_nameText == null)
        {
            GameObject textObj = new GameObject("PlayerNameText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = _offset;
            textObj.transform.localRotation = Quaternion.identity;

            _nameText = textObj.AddComponent<TextMeshPro>();
            _nameText.fontSize = 5;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.rectTransform.sizeDelta = new Vector2(5, 1);
        }

        // Find visual model if not assigned
        if (_visualModel == null)
        {
            _visualModel = transform.Find("PlayerVisualModel");
        }
    }

    public override void Spawned()
    {
        // Set default player name if not set yet
        if (string.IsNullOrEmpty(PlayerName.ToString()))
        {
            SetPlayerNameRpc($"Player {Object.InputAuthority.PlayerId}");
        }

        // Update name text
        UpdateNameText();

        // Make sure model is visible
        EnsureModelIsVisible();
    }

    public override void FixedUpdateNetwork()
    {
        // Check if name has changed
        string currentName = PlayerName.ToString();
        if (currentName != _lastDisplayedName)
        {
            UpdateNameText();
            _lastDisplayedName = currentName;
        }
    }

    private void Update()
    {
        // Make the text face the camera
        if (_nameText != null && Camera.main != null)
        {
            _nameText.transform.rotation = Camera.main.transform.rotation;
        }
    }

    // Fixed: Added "Rpc" suffix to method name
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void SetPlayerNameRpc(string name)
    {
        PlayerName = name;
        UpdateNameText();
    }

    private void UpdateNameText()
    {
        if (_nameText != null)
        {
            string name = PlayerName.ToString();
            if (string.IsNullOrEmpty(name))
            {
                name = $"Player {Object.InputAuthority.PlayerId}";
            }

            // Include player ID and local/remote status
            bool isLocal = HasInputAuthority;
            string statusText = isLocal ? "YOU" : "OTHER";
            _nameText.text = $"{name}\nID: {Object.InputAuthority.PlayerId}\n{statusText}";
            _nameText.color = isLocal ? _localPlayerColor : _otherPlayerColor;
        }
    }

    private void EnsureModelIsVisible()
    {
        if (_visualModel != null)
        {
            // Get Renderer component
            Renderer renderer = _visualModel.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Check if material is assigned
                if (renderer.material == null || renderer.material.name.Contains("Default"))
                {
                    // Create a new material with a bright color
                    Material newMaterial = new Material(Shader.Find("Standard"));

                    // Set material color based on player authority
                    if (HasInputAuthority)
                    {
                        newMaterial.color = new Color(0.2f, 0.8f, 0.2f); // Green for local player
                    }
                    else
                    {
                        newMaterial.color = new Color(0.8f, 0.2f, 0.2f); // Red for other players
                    }

                    // Apply the material
                    renderer.material = newMaterial;
                }
            }
        }
    }
}