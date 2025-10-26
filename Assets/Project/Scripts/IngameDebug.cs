// Filename: InGameDebug.cs
using UnityEngine;
using System.Text;

/// <summary>
/// A simple, static, non-spammy logger. Other scripts call InGameDebug.Log("message").
/// </summary>
public static class InGameDebug
{
    private static StringBuilder logMessages = new StringBuilder();
    private static bool hasNewMessages = false;

    // This is how other scripts will add logs
    public static void Log(string message)
    {
        // Add a timestamp
        logMessages.AppendLine($"[{Time.time:F2}s] {message}");
        hasNewMessages = true;
    }

    // This is how the UI will get the text
    public static string GetLog()
    {
        hasNewMessages = false;
        return logMessages.ToString();
    }

    // Check if the log has been updated
    public static bool HasNewMessages()
    {
        return hasNewMessages;
    }

    // This is for the "Copy" button/hotkey
    public static void CopyToClipboard()
    {
        GUIUtility.systemCopyBuffer = logMessages.ToString();
        Log("--- Log Copied to Clipboard ---");
    }

    public static void Clear()
    {
        logMessages.Clear();
        hasNewMessages = true; // Need to update the display to be empty
    }
}