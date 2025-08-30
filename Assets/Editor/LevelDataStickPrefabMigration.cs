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

    [MenuItem("Tools/Migrate All Entities to Prefab Reference")]
    public static void MigrateAllEntities()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/ScriptableObjects/Levels" });
        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) continue;

            // Stick
            if (string.IsNullOrEmpty(level.stickSpawn.prefabName))
            {
                level.stickSpawn.prefabName = "St_A_01";
                level.stickSpawn.position = level.stick.position;
                level.stickSpawn.rotationZ = level.stick.rotationZ;
                level.stickSpawn.scale = level.stick.scale;
            }

            // Obstacles
            if ((level.obstacleSpawns == null || level.obstacleSpawns.Length == 0) && level.obstacles != null)
            {
                level.obstacleSpawns = new LevelData.EntitySpawnData[level.obstacles.Length];
                for (int i = 0; i < level.obstacles.Length; i++)
                {
                    level.obstacleSpawns[i].prefabName = "Ob_A_01";
                    level.obstacleSpawns[i].position = level.obstacles[i].position;
                    level.obstacleSpawns[i].rotationZ = level.obstacles[i].rotationZ;
                    level.obstacleSpawns[i].scale = level.obstacles[i].scale;
                }
            }

            // Targets
            if ((level.targetSpawns == null || level.targetSpawns.Length == 0) && level.targets != null)
            {
                level.targetSpawns = new LevelData.EntitySpawnData[level.targets.Length];
                for (int i = 0; i < level.targets.Length; i++)
                {
                    level.targetSpawns[i].prefabName = "Ta_A_01";
                    level.targetSpawns[i].position = level.targets[i].position;
                    level.targetSpawns[i].rotationZ = level.targets[i].rotationZ;
                    level.targetSpawns[i].scale = level.targets[i].scale;
                }
            }

            // Fulcrums
            if ((level.fulcrumSpawns == null || level.fulcrumSpawns.Length == 0) && level.fulcrums != null)
            {
                level.fulcrumSpawns = new LevelData.EntitySpawnData[level.fulcrums.Length];
                for (int i = 0; i < level.fulcrums.Length; i++)
                {
                    level.fulcrumSpawns[i].prefabName = "Fu_A_01";
                    level.fulcrumSpawns[i].position = level.fulcrums[i].position;
                    level.fulcrumSpawns[i].rotationZ = level.fulcrums[i].rotationZ;
                    
                    // 프리팹의 원본 스케일을 고려하여 상대적 스케일 계산
                    // Fu_A_01 프리팹의 원본 스케일은 (1, 1)이므로, 저장된 스케일을 그대로 사용
                    // 만약 프리팹이 다른 스케일을 가지고 있다면 여기서 나누기 연산을 해야 함
                    // 기존 fulcrums의 scale이 (0.32, 0.32)인 경우, 프리팹 기반에서는 (1, 1)로 설정
                    if (Mathf.Approximately(level.fulcrums[i].scale.x, 0.32f) && Mathf.Approximately(level.fulcrums[i].scale.y, 0.32f))
                    {
                        level.fulcrumSpawns[i].scale = new Vector2(1f, 1f);
                    }
                    else
                    {
                        level.fulcrumSpawns[i].scale = level.fulcrums[i].scale;
                    }
                }
            }

            EditorUtility.SetDirty(level);
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelDataStickPrefabMigration] Migrated {count} LevelData assets (all entities).\nStick: St_A_01, Obstacle: Ob_A_01, Target: Ta_A_01, Fulcrum: Fu_A_01");
    }
}