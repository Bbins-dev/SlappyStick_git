// GameManager.cs
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("StickIt/GameManager")]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static event System.Action StageClearedStatic;
    public static GameManager Instance { get; private set; }

    [Header("Databases")]
    public LevelDatabase Database;

    [Header("Progress")]
    [Tooltip("Highest level index that is currently unlocked (1-based).")]
    public int highestUnlockedLevel = 1;

    [Tooltip("Current playing level index (1-based). Set this when loading a level.")]
    public int CurrentLevel { get; set; } = 1;

    [Tooltip("Only unlock the next level if the player just cleared the highest currently unlocked level.")]
    public bool requireSequentialUnlock = true;

    [Header("Events")]
    [Tooltip("Raised right after a level is recorded as cleared.")]
    public UnityEvent onStageCleared;

    public int TotalLevels => Database != null ? Database.Count : 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();

            // Optional but recommended: ensure this object is tagged for auto-find
            // Set the tag to "GameManager" in the Inspector (create the tag if needed).

            // ✅ 보장: UnityEvent null 방지
            if (onStageCleared == null) onStageCleared = new UnityEvent();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called when the current level is cleared.
    /// </summary>
    public void StageClear()
    {
        // Persist per-level clear flag
        PlayerPrefs.SetInt($"LevelCleared_{CurrentLevel}", 1);

        // Guard: clamp baseline
        if (highestUnlockedLevel < 1) highestUnlockedLevel = 1;
        int maxIndex = Mathf.Max(1, TotalLevels);

        if (requireSequentialUnlock)
        {
            // Unlock next ONLY if we just cleared the highest currently unlocked level
            if (CurrentLevel == highestUnlockedLevel && highestUnlockedLevel < maxIndex)
            {
                highestUnlockedLevel = Mathf.Min(highestUnlockedLevel + 1, maxIndex);
            }
        }
        else
        {
            // Fallback behavior (your original logic, but clamped)
            if (highestUnlockedLevel < CurrentLevel + 1)
            {
                highestUnlockedLevel = Mathf.Min(CurrentLevel + 1, maxIndex);
            }
        }

        PlayerPrefs.SetInt("HighestUnlocked", highestUnlockedLevel);
        PlayerPrefs.Save();

        // ✅ 진짜 이벤트가 쏴지는지 확인 로그
        Debug.Log($"[GameManager] StageClear fired. CurrentLevel={CurrentLevel}, HighestUnlocked={highestUnlockedLevel}");
        onStageCleared?.Invoke();
        StageClearedStatic?.Invoke();      // static event (robust)
    }

    // Alias for tools/scripts expecting this name (e.g., earlier TipTrigger samples).
    // If you set TipTrigger's method name to "StageClear", you may remove this.
    public void SaveStageClear() => StageClear();

    public bool IsLevelCleared(int level) =>
        PlayerPrefs.GetInt($"LevelCleared_{level}", 0) == 1;

    public bool IsUnlocked(int level) =>
        level <= Mathf.Max(1, highestUnlockedLevel);

    public void SetCurrentLevel(int level)
    {
        CurrentLevel = Mathf.Clamp(level, 1, Mathf.Max(1, TotalLevels));
    }

    private void LoadProgress()
    {
        highestUnlockedLevel = Mathf.Max(1, PlayerPrefs.GetInt("HighestUnlocked", 1));
        // Clamp to DB size if available
        if (TotalLevels > 0)
            highestUnlockedLevel = Mathf.Min(highestUnlockedLevel, TotalLevels);
        if (CurrentLevel < 1) CurrentLevel = 1;
    }

    [ContextMenu("Reset Progress (Clear PlayerPrefs)")]
    public void ResetProgress()
    {
        for (int i = 1; i <= TotalLevels; i++)
            PlayerPrefs.DeleteKey($"LevelCleared_{i}");

        PlayerPrefs.DeleteKey("HighestUnlocked");

        highestUnlockedLevel = 1;
        CurrentLevel = 1;

        PlayerPrefs.Save();
        Debug.Log("[GameManager] Progress reset.");
    }
}
