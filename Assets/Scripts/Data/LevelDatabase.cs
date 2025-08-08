// LevelDatabase.cs
using UnityEngine;

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "StickIt/LevelDatabase")]
public class LevelDatabase : ScriptableObject
{
    public LevelData[] levels;
    public int Count => levels != null ? levels.Length : 0;

    public LevelData Get(int levelIndex) // 1-based
    {
        int i = levelIndex - 1;
        return (levels != null && i >= 0 && i < levels.Length) ? levels[i] : null;
    }
}
