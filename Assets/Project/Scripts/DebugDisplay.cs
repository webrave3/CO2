// Filename: DebugDisplay.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Controls the Debug UI, makes it persistent (DontDestroyOnLoad),
/// and handles the 'T' key hotkey for copying.
/// </summary>
public class DebugDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private KeyCode copyHotkey = KeyCode.T;

    // Make this a persistent singleton
    public static DebugDisplay Instance { get; private set; }

    void Awake()
    {
        // --- Singleton and Persistence Logic ---
        // This answers your question about the panel carrying over.
        // This code will make it carry over.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            // If a duplicate exists (e.g., from reloading the MainMenu scene),
            // destroy the new duplicate.
            Destroy(gameObject);
            return;
        }
        // --- End of Persistence Logic ---

        // Optional: Hide the panel on start if you want
        // gameObject.SetActive(false); 
    }

    void Update()
    {
        // --- Hotkey Logic ---
        // This answers your request for a copy hotkey.
        if (Input.GetKeyDown(copyHotkey))
        {
            InGameDebug.CopyToClipboard();
        }

        // Optional: Toggle visibility with a different key
        // if (Input.GetKeyDown(KeyCode.BackQuote))
        // {
        //     gameObject.SetActive(!gameObject.activeSelf);
        // }
        // --- End of Hotkey Logic ---


        // Only update the UI text if there are new messages.
        // This is much more efficient than updating every frame.
        if (InGameDebug.HasNewMessages())
        {
            logText.text = InGameDebug.GetLog();

            // Force the scroll view to the bottom
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}