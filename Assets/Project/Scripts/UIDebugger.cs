using UnityEngine;
using UnityEngine.UI;

public class SimpleUIDebugger : MonoBehaviour
{
    [SerializeField] private Button _testButton;

    private void Start()
    {
        Debug.Log("==== SIMPLE UI DEBUGGER STARTED ====");

        // Create a test button to verify if ANY button works
        CreateTestButton();

        // Check for EventSystem
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogError("NO EVENT SYSTEM FOUND! UI buttons won't work without it");
        }

        // Check all main buttons
        CheckButton("Host Game", "HostGame");
        CheckButton("Join Game", "JoinGame");
        CheckButton("Settings", "Settings");
    }

    private void CheckButton(string buttonName, string buttonId)
    {
        // Find buttons by common names/paths
        Button button = null;

        // Try different methods to find the button
        button = GameObject.Find(buttonId)?.GetComponent<Button>();
        if (button == null) button = GameObject.Find(buttonName)?.GetComponent<Button>();
        if (button == null) button = GameObject.Find($"{buttonId}Button")?.GetComponent<Button>();

        string status = button != null ? "FOUND" : "NOT FOUND";
        Debug.Log($"Button '{buttonName}': {status}");

        if (button != null)
        {
            // Check if it has onClick listeners
            int listeners = button.onClick.GetPersistentEventCount();
            Debug.Log($"  - Has {listeners} listeners");
            Debug.Log($"  - Is active: {button.gameObject.activeInHierarchy}");
            Debug.Log($"  - Is interactable: {button.interactable}");
        }
    }

    private void CreateTestButton()
    {
        // Create a visible test button in top-right corner
        GameObject btnObj = new GameObject("TestButton");
        btnObj.transform.SetParent(transform);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -20);
        rect.sizeDelta = new Vector2(100, 50);

        Button btn = btnObj.AddComponent<Button>();
        UnityEngine.UI.Image img = btnObj.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.green;

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = "TEST";
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.black;

        // Add simple click listener that just logs
        btn.onClick.AddListener(() => {
            Debug.Log("TEST BUTTON CLICKED - If you see this, buttons CAN work!");
        });

        _testButton = btn;
    }
}