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

    // Internal class to hold session details managed by this script
    public class SessionInfo
    {
        public string RoomCode { get; set; }   // The user-friendly code (e.g., AdjectiveNoun)
        public string InternalId { get; set; } // The unique Guid used as Fusion's SessionName
        public DateTime CreationTime { get; set; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate SessionCodeManager instance found. Destroying self.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Keep this manager persistent

        LoadWordLists();
        LoadBlacklist();

        Debug.Log($"SessionCodeManager initialized: {_adjectives.Count} adjectives, {_nouns.Count} nouns, {_blacklistedCombinations.Count} blacklisted.");
    }

    private void Start()
    {
        // Start periodic cleanup of the cooldown dictionary
        InvokeRepeating(nameof(CleanupExpiredCooldowns), 5 * 60, 5 * 60); // Check every 5 minutes
    }


    private void LoadWordLists()
    {
        _adjectives = LoadListFromFile(_adjectivesFile, new[] { "Red", "Blue", "Green", "Fast", "Shiny", "Brave" }); // Added more defaults
        _nouns = LoadListFromFile(_nounsFile, new[] { "Wolf", "Tiger", "Eagle", "Fox", "Lion", "Bear" }); // Added more defaults
        Debug.Log($"Loaded {_adjectives.Count} adjectives, {_nouns.Count} nouns.");
    }

    private List<string> LoadListFromFile(TextAsset file, string[] defaults)
    {
        List<string> list = new List<string>();
        if (file != null && !string.IsNullOrEmpty(file.text))
        {
            try
            {
                string[] lines = file.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed.All(char.IsLetter)) // Basic validation
                    {
                        // Ensure PascalCase (Capitalize first letter, rest lower)
                        list.Add(char.ToUpper(trimmed[0]) + trimmed.Substring(1).ToLower());
                    }
                    else
                    {
                        // Debug.LogWarning($"Skipping invalid line in word list: '{line}'");
                    }
                }
                Debug.Log($"Loaded {list.Count} words from {file.name}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error reading word list {file.name}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Word list TextAsset '{file?.name ?? "NULL"}' is empty or missing. Using defaults.");
        }


        if (list.Count == 0)
        {
            Debug.LogWarning("Word list count is zero after loading attempt. Using default words.");
            list.AddRange(defaults); // Use defaults if file loading failed or empty
        }
        return list;
    }


    private void LoadBlacklist()
    {
        _blacklistedCombinations.Clear();
        if (_blacklistedCombinationsFile != null && !string.IsNullOrEmpty(_blacklistedCombinationsFile.text))
        {
            try
            {
                string[] lines = _blacklistedCombinationsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        _blacklistedCombinations.Add(trimmed.ToLowerInvariant()); // Store blacklist in lowercase invariant
                    }
                }
                Debug.Log($"Loaded {_blacklistedCombinations.Count} blacklisted combinations.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error reading blacklist file {_blacklistedCombinationsFile.name}: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Blacklist file is empty or missing. No combinations blacklisted initially.");
        }
    }

    /// <summary>
    /// Generates a new unique session code (e.g., "AdjectiveNoun") and associated internal ID.
    /// </summary>
    /// <returns>The user-friendly room code.</returns>
    public string GenerateNewSessionCode()
    {
        string internalId = System.Guid.NewGuid().ToString(); // Unique ID for Fusion's SessionName
        string roomCode = GenerateUniqueWordCode();           // User-friendly code

        SessionInfo sessionInfo = new SessionInfo
        {
            RoomCode = roomCode,
            InternalId = internalId,
            CreationTime = DateTime.UtcNow
        };

        _activeSessions.Add(roomCode, sessionInfo); // Track the active session by its room code
        Debug.Log($"Generated new session: Code='{roomCode}', InternalID='{internalId}'");
        return roomCode;
    }

    private string GenerateUniqueWordCode()
    {
        if (_adjectives.Count == 0 || _nouns.Count == 0)
        {
            Debug.LogError("Word lists are empty! Cannot generate word code. Falling back to simple code.");
            return "S" + UnityEngine.Random.Range(10000, 99999); // Simple fallback
        }

        int maxAttempts = _adjectives.Count * _nouns.Count; // Try at most all combinations
        maxAttempts = Math.Min(maxAttempts, 1000); // Limit attempts to prevent infinite loop in dense scenarios

        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            string adjective = _adjectives[UnityEngine.Random.Range(0, _adjectives.Count)];
            string noun = _nouns[UnityEngine.Random.Range(0, _nouns.Count)];
            string roomCode = adjective + noun; // Combine them

            if (IsValidCombination(roomCode)) // Check uniqueness and blacklist
            {
                return roomCode;
            }
            // Optional: Log if many attempts are needed
            // if (attempts > 50) Debug.LogWarning($"High attempt count ({attempts}) generating unique code.");
        }

        Debug.LogError($"Failed to generate a unique word code after {maxAttempts} attempts! Falling back to GUID-based code.");
        // Fallback to a truncated GUID if word generation fails repeatedly
        return "G" + Guid.NewGuid().ToString().Substring(0, 7).ToUpper();
    }

    private bool IsValidCombination(string combinedCode)
    {
        // 1. Check active sessions (case sensitive, as keys are stored as generated)
        if (_activeSessions.ContainsKey(combinedCode)) return false;

        // 2. Check recently used (cooldown, case sensitive for dictionary lookup)
        if (_recentlyUsedCodes.TryGetValue(combinedCode, out DateTime usageTime))
        {
            if (DateTime.UtcNow < usageTime.AddMinutes(_recycleTimeInMinutes)) return false; // Still in cooldown
            _recentlyUsedCodes.Remove(combinedCode); // Cooldown expired, okay to reuse, remove entry
        }

        // 3. Check blacklist (case insensitive)
        if (_blacklistedCombinations.Contains(combinedCode.ToLowerInvariant())) return false;

        return true; // Passed all checks
    }

    /// <summary>
    /// Retrieves the internal unique ID (Fusion SessionName) associated with a user-friendly room code.
    /// </summary>
    /// <param name="roomCode">The user-friendly code (e.g., "AdjectiveNoun").</param>
    /// <returns>The internal ID (Guid string) or null if the code is not active.</returns>
    public string GetInternalId(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode)) return null;
        return _activeSessions.TryGetValue(roomCode, out SessionInfo sessionInfo) ? sessionInfo.InternalId : null;
    }

    /// <summary>
    /// Checks if a given room code corresponds to an active session.
    /// </summary>
    /// <param name="roomCode">The user-friendly code to check.</param>
    /// <returns>True if the session code is currently active, false otherwise.</returns>
    public bool IsValidSessionCode(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode)) return false;
        return _activeSessions.ContainsKey(roomCode);
    }

    /// <summary>
    /// Marks a session as ended, removing it from active sessions and adding the code to the cooldown list.
    /// </summary>
    /// <param name="roomCode">The user-friendly code of the session to end.</param>
    public void EndSession(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode)) return;

        if (_activeSessions.Remove(roomCode)) // Remove returns true if the key existed
        {
            // Add to recently used for cooldown
            _recentlyUsedCodes[roomCode] = DateTime.UtcNow;
            Debug.Log($"Ended session '{roomCode}'. Added to cooldown.");
        }
        else
        {
            Debug.LogWarning($"Attempted to end session '{roomCode}', but it was not found in the active list.");
        }
    }

    // Called periodically by InvokeRepeating
    private void CleanupExpiredCooldowns()
    {
        // Find codes where the cooldown period has passed
        List<string> expiredCodes = _recentlyUsedCodes
            .Where(kvp => DateTime.UtcNow >= kvp.Value.AddMinutes(_recycleTimeInMinutes))
            .Select(kvp => kvp.Key)
            .ToList(); // ToList prevents modification during iteration

        if (expiredCodes.Count > 0)
        {
            foreach (string code in expiredCodes)
            {
                _recentlyUsedCodes.Remove(code);
            }
            Debug.Log($"Cleaned up {expiredCodes.Count} expired cooldown codes.");
        }
        // else Debug.Log("No expired cooldown codes found during cleanup.");
    }

    // --- Optional Blacklist Management Methods ---

    public bool AddToBlacklist(string combination)
    {
        if (string.IsNullOrEmpty(combination)) return false;

        string lowercase = combination.ToLowerInvariant();
        if (_blacklistedCombinations.Contains(lowercase))
        {
            // Debug.Log($"'{combination}' is already blacklisted.");
            return false; // Already blacklisted
        }


        _blacklistedCombinations.Add(lowercase);
        Debug.Log($"Added '{combination}' (as '{lowercase}') to blacklist.");

        // If the code was active, end the session immediately
        // Need to check original casing if activeSessions uses it
        if (_activeSessions.ContainsKey(combination))
        {
            Debug.LogWarning($"Blacklisted combination '{combination}' was active. Ending session.");
            EndSession(combination);
        }

        // Consider saving the blacklist here if persistence is needed immediately
        // SaveBlacklist();
        return true;
    }

    public bool RemoveFromBlacklist(string combination)
    {
        if (string.IsNullOrEmpty(combination)) return false;
        bool removed = _blacklistedCombinations.Remove(combination.ToLowerInvariant());
        if (removed)
        {
            Debug.Log($"Removed '{combination}' from blacklist.");
            // Consider saving blacklist changes
            // SaveBlacklist();
        }
        return removed;
    }

    public bool IsBlacklisted(string combination)
    {
        if (string.IsNullOrEmpty(combination)) return false;
        return _blacklistedCombinations.Contains(combination.ToLowerInvariant());
    }

    // Example of saving blacklist (call when needed, e.g., OnApplicationQuit or after changes)
    public void SaveBlacklist()
    {
        // Ensure path exists if needed, handle potential exceptions
        try
        {
            // Application.persistentDataPath is generally safer for runtime saves than Resources
            string path = Path.Combine(Application.persistentDataPath, "blacklist_saved.txt");
            // Convert HashSet to sorted list for consistent saving order (optional)
            var sortedBlacklist = _blacklistedCombinations.ToList();
            sortedBlacklist.Sort();
            File.WriteAllLines(path, sortedBlacklist);
            Debug.Log($"Saved blacklist ({_blacklistedCombinations.Count} items) to {path}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"Failed to save blacklist: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.LogError($"Permission error saving blacklist: {ex.Message}");
        }
        catch (Exception ex) // Catch other potential errors
        {
            Debug.LogError($"An unexpected error occurred saving blacklist: {ex.Message}");
        }
    }
    // Optional: Load saved blacklist on start (after initial resource load)
    // private void LoadSavedBlacklist() { ... File.ReadAllLines... AddRange to _blacklistedCombinations ... }


    // --- Statistics (Optional) ---
    public class SessionStats { public int Active; public int Cooldown; public long PossibleCombinations; public int Blacklisted; }
    public SessionStats GetSessionStats() => new SessionStats
    {
        Active = _activeSessions.Count,
        Cooldown = _recentlyUsedCodes.Count,
        PossibleCombinations = (long)_adjectives.Count * _nouns.Count, // Use long for large numbers
        Blacklisted = _blacklistedCombinations.Count
    };

    // Optional: Add a method to list active sessions for debugging
    public List<string> GetActiveSessionCodes()
    {
        return _activeSessions.Keys.ToList();
    }
}