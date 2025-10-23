using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

public class SessionCodeManager : MonoBehaviour
{
    public static SessionCodeManager Instance { get; private set; }

    [Header("Word Lists")]
    [SerializeField] private TextAsset _adjectivesFile;
    [SerializeField] private TextAsset _nounsFile;

    [Header("Blacklist")]
    [SerializeField] private TextAsset _blacklistedCombinationsFile;

    [Header("Settings")]
    [SerializeField] private int _recycleTimeInMinutes = 30;
    // Removed: [SerializeField] private bool _debugMode = false;

    private List<string> _adjectives = new List<string>();
    private List<string> _nouns = new List<string>();
    private HashSet<string> _blacklistedCombinations = new HashSet<string>();

    private Dictionary<string, SessionInfo> _activeSessions = new Dictionary<string, SessionInfo>();
    private Dictionary<string, DateTime> _recentlyUsedCodes = new Dictionary<string, DateTime>();

    public class SessionInfo
    {
        public string RoomCode { get; set; }
        public string InternalId { get; set; } // Fusion uses this SessionName internally
        public DateTime CreationTime { get; set; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadWordLists();
        LoadBlacklist();

        // Optional log for initialization confirmation (can be removed)
        // Debug.Log($"SessionCodeManager initialized: {_adjectives.Count} adj, {_nouns.Count} nouns.");
    }

    private void Start()
    {
        // Start periodic cleanup
        InvokeRepeating(nameof(CleanupExpiredCooldowns), 5 * 60, 5 * 60); // Run every 5 minutes
    }


    private void LoadWordLists()
    {
        _adjectives = LoadListFromFile(_adjectivesFile, new[] { "Red", "Blue", "Green" });
        _nouns = LoadListFromFile(_nounsFile, new[] { "Wolf", "Tiger", "Eagle" });
    }

    private List<string> LoadListFromFile(TextAsset file, string[] defaults)
    {
        List<string> list = new List<string>();
        if (file != null)
        {
            string[] lines = file.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    // Ensure PascalCase
                    list.Add(char.ToUpper(trimmed[0]) + trimmed.Substring(1).ToLower());
                }
            }
        }

        if (list.Count == 0)
        {
            list.AddRange(defaults); // Use defaults if file loading failed or empty
        }
        return list;
    }


    private void LoadBlacklist()
    {
        _blacklistedCombinations.Clear();
        if (_blacklistedCombinationsFile != null)
        {
            string[] lines = _blacklistedCombinationsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    _blacklistedCombinations.Add(trimmed.ToLower()); // Store blacklist in lowercase
                }
            }
        }
    }

    public string GenerateNewSessionCode()
    {
        string internalId = System.Guid.NewGuid().ToString();
        string roomCode = GenerateUniqueWordCode();

        SessionInfo sessionInfo = new SessionInfo
        {
            RoomCode = roomCode,
            InternalId = internalId,
            CreationTime = DateTime.UtcNow
        };

        _activeSessions.Add(roomCode, sessionInfo);
        // Removed Debug Log
        return roomCode;
    }

    private string GenerateUniqueWordCode()
    {
        int maxAttempts = 100;
        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            string adjective = _adjectives[UnityEngine.Random.Range(0, _adjectives.Count)];
            string noun = _nouns[UnityEngine.Random.Range(0, _nouns.Count)];
            string roomCode = adjective + noun;

            if (IsValidCombination(roomCode))
            {
                return roomCode;
            }
        }

        // Fallback to GUID-based code if word generation fails
        return "Session" + DateTime.UtcNow.Ticks.ToString().Substring(10);
    }

    private bool IsValidCombination(string combinedCode)
    {
        // Check active sessions
        if (_activeSessions.ContainsKey(combinedCode)) return false;

        // Check recently used (cooldown)
        if (_recentlyUsedCodes.TryGetValue(combinedCode, out DateTime usageTime))
        {
            if (DateTime.UtcNow < usageTime.AddMinutes(_recycleTimeInMinutes)) return false;
            _recentlyUsedCodes.Remove(combinedCode); // Cooldown expired, remove entry
        }

        // Check blacklist (case insensitive)
        if (_blacklistedCombinations.Contains(combinedCode.ToLower())) return false;

        return true;
    }

    public string GetInternalId(string roomCode)
    {
        return _activeSessions.TryGetValue(roomCode, out SessionInfo sessionInfo) ? sessionInfo.InternalId : null;
    }

    public bool IsValidSessionCode(string roomCode)
    {
        return _activeSessions.ContainsKey(roomCode);
    }

    public void EndSession(string roomCode)
    {
        if (_activeSessions.Remove(roomCode)) // Remove returns true if successful
        {
            // Add to recently used for cooldown
            _recentlyUsedCodes[roomCode] = DateTime.UtcNow;
            // Removed Debug Log
        }
    }

    public void CleanupExpiredCooldowns()
    {
        List<string> expiredCodes = _recentlyUsedCodes
            .Where(kvp => DateTime.UtcNow >= kvp.Value.AddMinutes(_recycleTimeInMinutes))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string code in expiredCodes)
        {
            _recentlyUsedCodes.Remove(code);
        }
        // Removed Debug Log
    }

    // --- Optional Blacklist Management ---

    public bool AddToBlacklist(string combination)
    {
        string lowercase = combination.ToLower();
        if (_blacklistedCombinations.Contains(lowercase)) return false; // Already blacklisted

        _blacklistedCombinations.Add(lowercase);
        if (_activeSessions.ContainsKey(combination)) EndSession(combination); // End if active
        // Removed Debug Log
        // Consider saving the blacklist here if persistence is needed immediately
        // SaveBlacklist();
        return true;
    }

    public bool RemoveFromBlacklist(string combination)
    {
        bool removed = _blacklistedCombinations.Remove(combination.ToLower());
        // if (removed) Debug.Log($"Removed from blacklist: {combination}"); // Optional log
        // Consider saving blacklist changes
        // if (removed) SaveBlacklist();
        return removed;
    }

    public bool IsBlacklisted(string combination)
    {
        return _blacklistedCombinations.Contains(combination.ToLower());
    }

    // Example of saving blacklist (call when needed, e.g., OnApplicationQuit or after adding/removing)
    public void SaveBlacklist()
    {
        try
        {
            // Prefer persistentDataPath for runtime saving
            string path = Path.Combine(Application.persistentDataPath, "blacklist.txt");
            File.WriteAllLines(path, _blacklistedCombinations);
            // Debug.Log($"Saved blacklist to {path}"); // Optional log
        }
        catch (Exception ex)
        {
            // Handle file write error
        }
    }

    // --- Statistics (Optional) ---
    public class SessionStats { public int Active; public int Cooldown; public long Possible; public int Blacklisted; }
    public SessionStats GetSessionStats() => new SessionStats
    {
        Active = _activeSessions.Count,
        Cooldown = _recentlyUsedCodes.Count,
        Possible = (long)_adjectives.Count * _nouns.Count,
        Blacklisted = _blacklistedCombinations.Count
    };
}