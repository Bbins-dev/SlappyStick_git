// GameManager.cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Databases")]
    public LevelDatabase Database;

    [Header("Progress")]
    public int highestUnlockedLevel = 1;
    public int CurrentLevel { get; set; }

    public int TotalLevels => Database != null ? Database.Count : 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
        }
        else Destroy(gameObject);
    }

    public void StageClear()
    {
        if (highestUnlockedLevel < CurrentLevel + 1)
            highestUnlockedLevel = CurrentLevel + 1;

        PlayerPrefs.SetInt($"LevelCleared_{CurrentLevel}", 1);
        PlayerPrefs.SetInt("HighestUnlocked", highestUnlockedLevel);
        PlayerPrefs.Save();
    }

    public bool IsLevelCleared(int level) =>
        PlayerPrefs.GetInt($"LevelCleared_{level}", 0) == 1;

    private void LoadProgress()
    {
        highestUnlockedLevel = PlayerPrefs.GetInt("HighestUnlocked", 1);
    }
}
