// GameManager.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [ContextMenu("Reset Progress (Clear PlayerPrefs)")]
    public void ResetProgress()
    {
        // 개별 클리어 기록 삭제
        for (int i = 1; i <= TotalLevels; i++)
            PlayerPrefs.DeleteKey($"LevelCleared_{i}");

        // 최고 해금 레벨 삭제
        PlayerPrefs.DeleteKey("HighestUnlocked");

        // 메모리 상태도 초기화
        highestUnlockedLevel = 1;
        CurrentLevel = 1;

        PlayerPrefs.Save();
        Debug.Log("[GameManager] Progress reset.");
    }
}
