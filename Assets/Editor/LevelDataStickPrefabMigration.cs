// Assets/Editor/LevelDataStickPrefabMigration.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public class LevelDataStickPrefabMigration
{
    [MenuItem("Tools/Migrate Stick to Prefab Reference")]
    public static void MigrateAllLevels()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/ScriptableObjects/Levels" });
        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) continue;

            // Skip if already migrated
            if (!string.IsNullOrEmpty(level.stickSpawn.prefabName)) continue;

            level.stickSpawn.prefabName = "Stick"; // 원하는 프리팹명으로 일괄 지정
            level.stickSpawn.position = level.stick.position;
            level.stickSpawn.rotationZ = level.stick.rotationZ;
            level.stickSpawn.scale = level.stick.scale;

            EditorUtility.SetDirty(level);
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelDataStickPrefabMigration] Migrated {count} LevelData assets.");
    }
}