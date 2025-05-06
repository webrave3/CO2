using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

public class SessionCodeManager : MonoBehaviour
{
    // Singleton pattern
    public static SessionCodeManager Instance { get; private set; }

    [Header("Word Lists")]
    [SerializeField] private TextAsset _adjectivesFile;
    [SerializeField] private TextAsset _nounsFile;

    [Header("Blacklist")]
    [SerializeField] private TextAsset _blacklistedCombinationsFile;

    [Header("Settings")]
    [SerializeField] private int _recycleTimeInMinutes = 30;
    [SerializeField] private bool _debugMode = false;

    // Runtime lists
    private List<string> _adjectives = new List<string>();
    private List<string> _nouns = new List<string>();
    private HashSet<string> _blacklistedCombinations = new HashSet<string>();

    // Track active and recently used codes
    private Dictionary<string, SessionInfo> _activeSessions = new Dictionary<string, SessionInfo>();
    private Dictionary<string, DateTime> _recentlyUsedCodes = new Dictionary<string, DateTime>();

    // Session info class to track internal data
    public class SessionInfo
    {
        public string RoomCode { get; set; }
        public string InternalId { get; set; }
        public DateTime CreationTime { get; set; }
    }

    private void Awake()
    {
        // Implement singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load word lists
        LoadWordLists();

        // Load blacklist
        LoadBlacklist();

        Debug.Log($"SessionCodeManager initialized with {_adjectives.Count} adjectives and {_nouns.Count} nouns");
        Debug.Log($"Total possible combinations: {_adjectives.Count * _nouns.Count}");
    }

    private void LoadWordLists()
    {
        // Load adjectives
        if (_adjectivesFile != null)
        {
            string[] lines = _adjectivesFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    // Capitalize first letter
                    _adjectives.Add(char.ToUpper(trimmed[0]) + trimmed.Substring(1).ToLower());
                }
            }
        }
        else
        {
            Debug.LogError("Adjectives file not assigned!");
            // Add some defaults for testing
            _adjectives.AddRange(new[] { "Red", "Blue", "Green", "Fast", "Slow", "Big", "Small" });
        }

        // Load nouns
        if (_nounsFile != null)
        {
            string[] lines = _nounsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    // Capitalize first letter
                    _nouns.Add(char.ToUpper(trimmed[0]) + trimmed.Substring(1).ToLower());
                }
            }
        }
        else
        {
            Debug.LogError("Nouns file not assigned!");
            // Add some defaults for testing
            _nouns.AddRange(new[] { "Wolf", "Tiger", "Eagle", "Mountain", "River", "Tree", "Stone" });
        }
    }

    private void LoadBlacklist()
    {
        if (_blacklistedCombinationsFile != null)
        {
            string[] lines = _blacklistedCombinationsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    _blacklistedCombinations.Add(trimmed.ToLower());
                }
            }
        }
    }

    public string GenerateNewSessionCode()
    {
        // Create a new unique internal ID (GUID)
        string internalId = System.Guid.NewGuid().ToString();

        // Generate a word-based room code
        string roomCode = GenerateUniqueWordCode();

        // Store the mapping between room code and internal ID
        SessionInfo sessionInfo = new SessionInfo
        {
            RoomCode = roomCode,
            InternalId = internalId,
            CreationTime = DateTime.UtcNow
        };

        // Add to active sessions
        _activeSessions.Add(roomCode, sessionInfo);

        Debug.Log($"Generated new session code: {roomCode} (Internal ID: {internalId})");

        return roomCode;
    }

    private string GenerateUniqueWordCode()
    {
        int maxAttempts = 100; // Safety limit
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            // Get random words
            string adjective = GetRandomWord(_adjectives);
            string noun = GetRandomWord(_nouns);

            // Combine into room code
            string roomCode = adjective + noun;

            // Check if this combination is valid
            if (IsValidCombination(adjective, noun, roomCode))
            {
                return roomCode;
            }

            attempts++;
        }

        // If we couldn't find a valid combination after max attempts,
        // fall back to a guaranteed unique code
        string fallbackCode = "Random" + DateTime.UtcNow.Ticks.ToString().Substring(0, 6);
        Debug.LogWarning($"Failed to generate unique word code after {maxAttempts} attempts. Using fallback: {fallbackCode}");
        return fallbackCode;
    }

    private string GetRandomWord(List<string> wordList)
    {
        int index = UnityEngine.Random.Range(0, wordList.Count);
        return wordList[index];
    }

    private bool IsValidCombination(string adjective, string noun, string combined)
    {
        // Check if this code is already active
        if (_activeSessions.ContainsKey(combined))
        {
            if (_debugMode) Debug.Log($"Code '{combined}' is already active");
            return false;
        }

        // Check if this code was recently used and is in cooldown
        if (_recentlyUsedCodes.ContainsKey(combined))
        {
            DateTime recycleTime = _recentlyUsedCodes[combined].AddMinutes(_recycleTimeInMinutes);
            if (DateTime.UtcNow < recycleTime)
            {
                if (_debugMode) Debug.Log($"Code '{combined}' is in cooldown until {recycleTime}");
                return false;
            }

            // If cooldown period is over, remove from recently used
            _recentlyUsedCodes.Remove(combined);
            if (_debugMode) Debug.Log($"Code '{combined}' removed from cooldown");
        }

        // Check against blacklist (case insensitive)
        if (_blacklistedCombinations.Contains(combined.ToLower()))
        {
            if (_debugMode) Debug.Log($"Code '{combined}' is blacklisted");
            return false;
        }

        // All checks passed, this is a valid combination
        return true;
    }

    public string GetInternalId(string roomCode)
    {
        if (_activeSessions.TryGetValue(roomCode, out SessionInfo sessionInfo))
        {
            return sessionInfo.InternalId;
        }
        return null;
    }

    public bool IsValidSessionCode(string roomCode)
    {
        return _activeSessions.ContainsKey(roomCode);
    }

    public void EndSession(string roomCode)
    {
        if (_activeSessions.TryGetValue(roomCode, out SessionInfo sessionInfo))
        {
            // Move from active to recently used for cooldown
            _recentlyUsedCodes[roomCode] = DateTime.UtcNow;

            // Remove from active sessions
            _activeSessions.Remove(roomCode);

            Debug.Log($"Ended session: {roomCode} (Internal ID: {sessionInfo.InternalId})");
        }
    }

    // Call this periodically to clean up expired cooldowns
    public void CleanupExpiredCooldowns()
    {
        List<string> expiredCodes = new List<string>();

        foreach (var kvp in _recentlyUsedCodes)
        {
            DateTime recycleTime = kvp.Value.AddMinutes(_recycleTimeInMinutes);
            if (DateTime.UtcNow >= recycleTime)
            {
                expiredCodes.Add(kvp.Key);
            }
        }

        foreach (string code in expiredCodes)
        {
            _recentlyUsedCodes.Remove(code);
        }

        if (expiredCodes.Count > 0 && _debugMode)
        {
            Debug.Log($"Cleaned up {expiredCodes.Count} expired cooldowns");
        }
    }

    // Add this in Update or call on a timer
    private void Update()
    {
        // Clean up expired cooldowns every 5 minutes
        if (Time.frameCount % (300 * 60) == 0) // Roughly every 5 minutes at 60fps
        {
            CleanupExpiredCooldowns();
        }
    }

    public bool AddToBlacklist(string combination)
    {
        string lowercase = combination.ToLower();

        if (_blacklistedCombinations.Contains(lowercase))
            return false;

        _blacklistedCombinations.Add(lowercase);

        // If this combination is currently active, end the session
        if (_activeSessions.ContainsKey(combination))
        {
            EndSession(combination);
        }

        Debug.Log($"Added combination to blacklist: {combination}");
        return true;
    }

    public bool RemoveFromBlacklist(string combination)
    {
        string lowercase = combination.ToLower();

        if (!_blacklistedCombinations.Contains(lowercase))
            return false;

        _blacklistedCombinations.Remove(lowercase);
        Debug.Log($"Removed combination from blacklist: {combination}");
        return true;
    }

    public bool IsBlacklisted(string combination)
    {
        return _blacklistedCombinations.Contains(combination.ToLower());
    }

    // This will save the blacklist back to a file if needed
    public void SaveBlacklist()
    {
        string path = Application.persistentDataPath + "/blacklist.txt";
        File.WriteAllLines(path, _blacklistedCombinations);
        Debug.Log($"Saved blacklist to {path}");
    }

    // Get session statistics for monitoring/debugging
    public SessionStats GetSessionStats()
    {
        return new SessionStats
        {
            ActiveSessionCount = _activeSessions.Count,
            CooldownSessionCount = _recentlyUsedCodes.Count,
            TotalWordCombinations = _adjectives.Count * _nouns.Count,
            BlacklistedCombinations = _blacklistedCombinations.Count
        };
    }

    // Statistics class
    public class SessionStats
    {
        public int ActiveSessionCount { get; set; }
        public int CooldownSessionCount { get; set; }
        public int TotalWordCombinations { get; set; }
        public int BlacklistedCombinations { get; set; }
    }
}