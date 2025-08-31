using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Scene ↔ LevelData 동기화 툴(에디터 전용)
/// - Save To LevelData: 현재(또는 currentGroup)의 배치를 LevelData로 저장
/// - Load From LevelData (Edit): LevelData를 프리뷰 오브젝트로 생성
/// - Clear Preview (Generated Only): 생성된 프리뷰만 제거
/// </summary>
public class LevelConfigurator : MonoBehaviour
{
    public enum ClearMode { GeneratedOnly, AllChildren, None }

    [Header("Load Settings")]
    public ClearMode clearModeOnLoad = ClearMode.GeneratedOnly;

    [Header("Target Asset")]
    public LevelData levelData;

    [Header("Optional Scene Roots (children will be used on Load)")]
    public Transform stickRoot;
    public Transform targetsRoot;
    public Transform obstaclesRoot;
    public Transform fulcrumsRoot;

    [Header("Preview Groups (Editor)")]
    [Tooltip("모든 프리뷰 그룹이 생성될 컨테이너")]
    public Transform previewContainer;
    [Tooltip("최근 로드된 그룹(있으면 Save가 이쪽을 우선으로 읽음)")]
    public Transform currentGroup;

    [Header("Camera Initial (Save/Load)")]
    [Tooltip("비워두면 Camera.main을 사용하여 카메라 초기 포즈를 저장/적용")]
    public Camera initialCamera;

#if UNITY_EDITOR
    // ─────────────────────────────────────────────────────────────────────
    // Save
    // ─────────────────────────────────────────────────────────────────────
    // [ContextMenu("Save To LevelData")] // 기존 방식 ContextMenu 제거
    public void SaveToLevelData()
    {
        if (levelData == null)
        {
            Debug.LogError("[LevelConfigurator] LevelData asset is not assigned.");
            return;
        }

        // 1) 그룹 우선으로 참조(있으면 그룹 하위에서만 읽음)
        Transform gStick = null, gTargets = null, gObstacles = null, gFulcrums = null;
        if (currentGroup != null)
        {
            gStick     = currentGroup.Find("__Preview_Stick");
            gTargets   = currentGroup.Find("__Preview_Targets");
            gObstacles = currentGroup.Find("__Preview_Obstacles");
            gFulcrums  = currentGroup.Find("__Preview_Fulcrums");
        }

        // 2) Stick (단일)
        GameObject stickGo = null;
        if (gStick != null && gStick.childCount > 0)
            stickGo = gStick.GetChild(0).gameObject;   // 그룹 우선
        else
            stickGo = ResolveStickForSave();            // 폴백(씬에서 찾기)

        levelData.stick = stickGo != null ? CaptureEntity(stickGo, isStick: true) : default;

        // 3) 그룹(타겟/장애물/지지대)
        levelData.targets   = CaptureGroupForSave(gTargets   != null ? gTargets   : targetsRoot,   SpawnMarker.SpawnType.Target);
        levelData.obstacles = CaptureGroupForSave(gObstacles != null ? gObstacles : obstaclesRoot, SpawnMarker.SpawnType.Obstacle);
        levelData.fulcrums  = CaptureGroupForSave(gFulcrums  != null ? gFulcrums  : fulcrumsRoot,  SpawnMarker.SpawnType.Fulcrum);

        // 4) 카메라 초기 포즈
        CaptureCameraInitial();

        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelConfigurator] Saved → LevelData: {levelData.name} (source: {(currentGroup ? currentGroup.name : "scene roots/markers")})");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Save (Prefab Spawns)
    // ─────────────────────────────────────────────────────────────────────
    [ContextMenu("Save To LevelData (Prefab Spawns)")]
    public void SaveToLevelDataPrefabSpawns()
    {
        if (levelData == null)
        {
            Debug.LogError("[LevelConfigurator] LevelData asset is not assigned.");
            return;
        }

        // 1. PreviewGroup 찾기
        Transform previewGroup = currentGroup;
        if (previewGroup == null)
        {
            // currentGroup이 없으면 씬에서 찾기
            var allGroups = FindObjectsOfType<Transform>();
            foreach (var group in allGroups)
            {
                if (group.name == $"__PreviewGroup_{levelData.name}")
                {
                    previewGroup = group;
                    break;
                }
            }
        }

        if (previewGroup == null)
        {
            Debug.LogError($"[LevelConfigurator] PreviewGroup({levelData.name})을(를) 찾을 수 없습니다.");
            return;
        }

        Debug.Log($"[LevelConfigurator] PreviewGroup 찾음: {previewGroup.name}, 자식 수: {previewGroup.childCount}");

        // 2. PreviewGroup 하위 모든 자식 오브젝트 수집 및 분류
        var stickList = new List<LevelData.EntitySpawnData>();
        var obstacleList = new List<LevelData.EntitySpawnData>();
        var targetList = new List<LevelData.EntitySpawnData>();
        var fulcrumList = new List<LevelData.EntitySpawnData>();
        var backgroundList = new List<LevelData.EntitySpawnData>();

        for (int i = 0; i < previewGroup.childCount; i++)
        {
            var child = previewGroup.GetChild(i);
            var go = child.gameObject;
            string tag = go.tag;
            string name = go.name;

            Debug.Log($"[LevelConfigurator] 검사 중: {name} (태그: {tag})");

            // Stick
            if (tag == "Stick" && name.StartsWith("St_"))
            {
                stickList.Add(CaptureEntitySpawnData(go));
                Debug.Log($"[LevelConfigurator] Stick 추가: {name}");
            }
            // Obstacle
            else if (tag == "Obstacle" && name.StartsWith("Ob_"))
            {
                obstacleList.Add(CaptureEntitySpawnData(go));
                Debug.Log($"[LevelConfigurator] Obstacle 추가: {name}");
            }
            // Target
            else if (tag == "Target" && name.StartsWith("Ta_"))
            {
                targetList.Add(CaptureEntitySpawnData(go));
                Debug.Log($"[LevelConfigurator] Target 추가: {name}");
            }
            // Fulcrum
            else if (tag == "Fulcrum" && name.StartsWith("Fu_"))
            {
                fulcrumList.Add(CaptureEntitySpawnData(go));
                Debug.Log($"[LevelConfigurator] Fulcrum 추가: {name}");
            }
            // Background
            else if (tag == "Background" && name.StartsWith("Bg_"))
            {
                backgroundList.Add(CaptureEntitySpawnData(go));
                Debug.Log($"[LevelConfigurator] Background 추가: {name}");
            }
            // 임시/기타 오브젝트는 무시
            else
            {
                Debug.Log($"[LevelConfigurator] 무시됨: {name} (태그: {tag}) - 규칙 불일치");
            }
        }

        // 3. Stick은 1개만 허용, 여러 개면 경고
        if (stickList.Count > 1)
            Debug.LogWarning($"[LevelConfigurator] Stick(프리팹)이 2개 이상입니다. 첫 번째만 저장합니다.");
        levelData.stickSpawn = stickList.Count > 0 ? stickList[0] : default;

        // 4. 나머지는 모두 리스트로 저장
        levelData.obstacleSpawns = obstacleList.ToArray();
        levelData.targetSpawns = targetList.ToArray();
        levelData.fulcrumSpawns = fulcrumList.ToArray();

        // Background는 1개만 허용, 여러 개면 경고
        if (backgroundList.Count > 1)
            Debug.LogWarning($"[LevelConfigurator] Background(프리팹)이 2개 이상입니다. 첫 번째만 저장합니다.");
        levelData.backgroundSpawn = backgroundList.Count > 0 ? backgroundList[0] : default;

        Debug.Log($"[LevelConfigurator] 저장 결과 - Stick: {stickList.Count}, Obstacle: {obstacleList.Count}, Target: {targetList.Count}, Fulcrum: {fulcrumList.Count}, Background: {backgroundList.Count}");

        // 5. 기존 EntityData 자동 비움
        levelData.obstacles = new LevelData.EntityData[0];
        levelData.targets = new LevelData.EntityData[0];
        levelData.fulcrums = new LevelData.EntityData[0];

        // 6. 카메라 초기 포즈
        CaptureCameraInitial();
        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelConfigurator] Saved prefab spawns → LevelData: {levelData.name}");

        // 7. 저장 후 prefabSpawns가 비어 있으면 경고 출력
        if ((levelData.obstacleSpawns == null || levelData.obstacleSpawns.Length == 0) &&
            (levelData.targetSpawns == null || levelData.targetSpawns.Length == 0) &&
            (levelData.fulcrumSpawns == null || levelData.fulcrumSpawns.Length == 0) &&
            string.IsNullOrEmpty(levelData.backgroundSpawn.prefabName))
        {
            Debug.LogWarning($"[LevelConfigurator] prefabSpawns가 비어 있습니다. PreviewGroup 하위에 프리팹 인스턴스가 맞는지, 이름/태그 규칙이 맞는지 확인하세요.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Load (Editor Preview)
    // ─────────────────────────────────────────────────────────────────────
    [ContextMenu("Load From LevelData (Edit)")]
    public void LoadFromLevelData()
    {
        Selection.activeObject = this.gameObject;

        if (levelData == null)
        {
            Debug.LogError("[LevelConfigurator] LevelData asset is not assigned.");
            return;
        }

        // A) 컨테이너
        var container = EnsureContainer();

        // B) 정리
        if (clearModeOnLoad != ClearMode.None)
            ClearByMode(container, clearModeOnLoad);

        // C) 새 그룹 (폴더 구조 없이 직접)
        string groupName = $"__PreviewGroup_{levelData.name}";
        var groupGO = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(groupGO, "Create Preview Group");
        groupGO.transform.SetParent(container, false);
        currentGroup = groupGO.transform;

        Debug.Log($"[LevelConfigurator] PreviewGroup 생성: {groupName}");

        // D) 프리팹 기반 스폰 (prefabSpawns 우선)
        Debug.Log($"[LevelConfigurator] LevelData 상태 확인:");
        Debug.Log($"[LevelConfigurator] - stickSpawn.prefabName: {levelData.stickSpawn.prefabName}");
        Debug.Log($"[LevelConfigurator] - targetSpawns: {(levelData.targetSpawns != null ? levelData.targetSpawns.Length : 0)}개");
        Debug.Log($"[LevelConfigurator] - obstacleSpawns: {(levelData.obstacleSpawns != null ? levelData.obstacleSpawns.Length : 0)}개");
        Debug.Log($"[LevelConfigurator] - fulcrumSpawns: {(levelData.fulcrumSpawns != null ? levelData.fulcrumSpawns.Length : 0)}개");
        Debug.Log($"[LevelConfigurator] - backgroundSpawn.prefabName: {levelData.backgroundSpawn.prefabName}");
        
        int spawnCount = 0;

        // Stick
        if (!string.IsNullOrEmpty(levelData.stickSpawn.prefabName))
        {
            string stickPath = $"Sticks/{levelData.stickSpawn.prefabName}";
            Debug.Log($"[LevelConfigurator] Stick 프리팹 로드 시도: {stickPath}");
            
            // Resources 폴더 내 모든 프리팹 확인
            var allSticks = Resources.LoadAll<GameObject>("Sticks");
            Debug.Log($"[LevelConfigurator] Resources/Sticks 폴더 내 프리팹들: {string.Join(", ", allSticks.Select(p => p.name))}");
            
#if UNITY_EDITOR
            var stickPrefab = Resources.Load<GameObject>(stickPath);
            if (stickPrefab != null)
            {
                var stickGo = (GameObject)PrefabUtility.InstantiatePrefab(stickPrefab, currentGroup);
                stickGo.transform.position = levelData.stickSpawn.position;
                stickGo.transform.rotation = Quaternion.Euler(0, 0, levelData.stickSpawn.rotationZ);
                stickGo.transform.localScale = new Vector3(levelData.stickSpawn.scale.x, levelData.stickSpawn.scale.y, 1f);
                stickGo.name = levelData.stickSpawn.prefabName;
                Undo.RegisterCreatedObjectUndo(stickGo, "Load Stick");
                Debug.Log($"[LevelConfigurator] Stick 프리팹 인스턴스(파란색)로 로드: {levelData.stickSpawn.prefabName}, 스케일: {levelData.stickSpawn.scale}");
                spawnCount++;
            }
            else
            {
                Debug.LogError($"[LevelConfigurator] Stick 프리팹을 찾을 수 없습니다: {stickPath}");
                Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {levelData.stickSpawn.prefabName}");
            }
#else
            // 기존 Instantiate 방식 (런타임)
            var stickPrefab = Resources.Load<GameObject>(stickPath);
            if (stickPrefab != null)
            {
                var stickGo = Instantiate(stickPrefab, levelData.stickSpawn.position, Quaternion.Euler(0, 0, levelData.stickSpawn.rotationZ), currentGroup);
                stickGo.transform.localScale = new Vector3(levelData.stickSpawn.scale.x, levelData.stickSpawn.scale.y, 1f);
                stickGo.name = levelData.stickSpawn.prefabName;
                Undo.RegisterCreatedObjectUndo(stickGo, "Load Stick");
                Debug.Log($"[LevelConfigurator] Stick 로드 성공: {levelData.stickSpawn.prefabName}, 스케일: {levelData.stickSpawn.scale}");
                spawnCount++;
            }
            else
            {
                Debug.LogError($"[LevelConfigurator] Stick 프리팹을 찾을 수 없습니다: {stickPath}");
                Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {levelData.stickSpawn.prefabName}");
            }
#endif
        }

        // Targets
        if (levelData.targetSpawns != null)
        {
            foreach (var targetSpawn in levelData.targetSpawns)
            {
                string targetPath = $"Targets/{targetSpawn.prefabName}";
                Debug.Log($"[LevelConfigurator] Target 프리팹 로드 시도: {targetPath}");
                
                // Resources 폴더 내 모든 프리팹 확인
                var allTargets = Resources.LoadAll<GameObject>("Targets");
                Debug.Log($"[LevelConfigurator] Resources/Targets 폴더 내 프리팹들: {string.Join(", ", allTargets.Select(p => p.name))}");
                
#if UNITY_EDITOR
                var targetPrefab = Resources.Load<GameObject>(targetPath);
                if (targetPrefab != null)
                {
                    var targetGo = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, currentGroup);
                    targetGo.transform.position = targetSpawn.position;
                    targetGo.transform.rotation = Quaternion.Euler(0, 0, targetSpawn.rotationZ);
                    targetGo.transform.localScale = new Vector3(targetSpawn.scale.x, targetSpawn.scale.y, 1f);
                    targetGo.name = targetSpawn.prefabName;
                    Undo.RegisterCreatedObjectUndo(targetGo, "Load Target");
                    Debug.Log($"[LevelConfigurator] Target 프리팹 인스턴스(파란색)로 로드: {targetSpawn.prefabName}, 스케일: {targetSpawn.scale}");
                    spawnCount++;
                }
                else
                {
                    Debug.LogError($"[LevelConfigurator] Target 프리팹을 찾을 수 없습니다: {targetPath}");
                    Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {targetSpawn.prefabName}");
                }
#else
                var targetPrefab = Resources.Load<GameObject>(targetPath);
                if (targetPrefab != null)
                {
                    var targetGo = Instantiate(targetPrefab, targetSpawn.position, 
                        Quaternion.Euler(0, 0, targetSpawn.rotationZ), currentGroup);
                    targetGo.transform.localScale = new Vector3(targetSpawn.scale.x, targetSpawn.scale.y, 1f);
                    targetGo.name = targetSpawn.prefabName;
                    Undo.RegisterCreatedObjectUndo(targetGo, "Load Target");
                    Debug.Log($"[LevelConfigurator] Target 로드 성공: {targetSpawn.prefabName}, 스케일: {targetSpawn.scale}");
                    spawnCount++;
                }
                else
                {
                    Debug.LogError($"[LevelConfigurator] Target 프리팹을 찾을 수 없습니다: {targetPath}");
                    Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {targetSpawn.prefabName}");
                }
#endif
            }
        }

        // Obstacles
        if (levelData.obstacleSpawns != null)
        {
            foreach (var obstacleSpawn in levelData.obstacleSpawns)
            {
                string obstaclePath = $"Obstacles/{obstacleSpawn.prefabName}";
                Debug.Log($"[LevelConfigurator] Obstacle 프리팹 로드 시도: {obstaclePath}");
                
                // Resources 폴더 내 모든 프리팹 확인
                var allObstacles = Resources.LoadAll<GameObject>("Obstacles");
                Debug.Log($"[LevelConfigurator] Resources/Obstacles 폴더 내 프리팹들: {string.Join(", ", allObstacles.Select(p => p.name))}");
                
#if UNITY_EDITOR
                var obstaclePrefab = Resources.Load<GameObject>(obstaclePath);
                if (obstaclePrefab != null)
                {
                    var obstacleGo = (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab, currentGroup);
                    obstacleGo.transform.position = obstacleSpawn.position;
                    obstacleGo.transform.rotation = Quaternion.Euler(0, 0, obstacleSpawn.rotationZ);
                    obstacleGo.transform.localScale = new Vector3(obstacleSpawn.scale.x, obstacleSpawn.scale.y, 1f);
                    obstacleGo.name = obstacleSpawn.prefabName;
                    Undo.RegisterCreatedObjectUndo(obstacleGo, "Load Obstacle");
                    Debug.Log($"[LevelConfigurator] Obstacle 프리팹 인스턴스(파란색)로 로드: {obstacleSpawn.prefabName}, 스케일: {obstacleSpawn.scale}");
                    spawnCount++;
                }
                else
                {
                    Debug.LogError($"[LevelConfigurator] Obstacle 프리팹을 찾을 수 없습니다: {obstaclePath}");
                    Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {obstacleSpawn.prefabName}");
                }
#else
                var obstaclePrefab = Resources.Load<GameObject>(obstaclePath);
                if (obstaclePrefab != null)
                {
                    var obstacleGo = Instantiate(obstaclePrefab, obstacleSpawn.position, 
                        Quaternion.Euler(0, 0, obstacleSpawn.rotationZ), currentGroup);
                    obstacleGo.transform.localScale = new Vector3(obstacleSpawn.scale.x, obstacleSpawn.scale.y, 1f);
                    obstacleGo.name = obstacleSpawn.prefabName;
                    Undo.RegisterCreatedObjectUndo(obstacleGo, "Load Obstacle");
                    Debug.Log($"[LevelConfigurator] Obstacle 로드 성공: {obstacleSpawn.prefabName}, 스케일: {obstacleSpawn.scale}");
                    spawnCount++;
                }
                else
                {
                    Debug.LogError($"[LevelConfigurator] Obstacle 프리팹을 찾을 수 없습니다: {obstaclePath}");
                    Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {obstacleSpawn.prefabName}");
                }
#endif
            }
        }

        // Fulcrums
        if (levelData.fulcrumSpawns != null)
        {
            foreach (var fulcrumSpawn in levelData.fulcrumSpawns)
            {
                string fulcrumPath = $"Fulcrums/{fulcrumSpawn.prefabName}";
                Debug.Log($"[LevelConfigurator] Fulcrum 프리팹 로드 시도: {fulcrumPath}");
                
                // Resources 폴더 내 모든 프리팹 확인
                var allFulcrums = Resources.LoadAll<GameObject>("Fulcrums");
                Debug.Log($"[LevelConfigurator] Resources/Fulcrums 폴더 내 프리팹들: {string.Join(", ", allFulcrums.Select(p => p.name))}");
                
#if UNITY_EDITOR
                var fulcrumPrefab = Resources.Load<GameObject>(fulcrumPath);
                if (fulcrumPrefab != null)
                {
                    var fulcrumGo = (GameObject)PrefabUtility.InstantiatePrefab(fulcrumPrefab, currentGroup);
                    fulcrumGo.transform.position = fulcrumSpawn.position;
                    fulcrumGo.transform.rotation = Quaternion.Euler(0, 0, fulcrumSpawn.rotationZ);
                    fulcrumGo.transform.localScale = new Vector3(fulcrumSpawn.scale.x, fulcrumSpawn.scale.y, 1f);
                    fulcrumGo.name = fulcrumSpawn.prefabName;
                    Undo.RegisterCreatedObjectUndo(fulcrumGo, "Load Fulcrum");
                    Debug.Log($"[LevelConfigurator] Fulcrum 프리팹 인스턴스(파란색)로 로드: {fulcrumSpawn.prefabName}, 스케일: {fulcrumSpawn.scale}");
                    spawnCount++;
                }
                else
                {
                    Debug.LogError($"[LevelConfigurator] Fulcrum 프리팹을 찾을 수 없습니다: {fulcrumPath}");
                    Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {fulcrumSpawn.prefabName}");
                }
#else
                var fulcrumPrefab = Resources.Load<GameObject>(fulcrumPath);
                if (fulcrumPrefab != null)
                {
                    var fulcrumGo = Instantiate(fulcrumPrefab, fulcrumSpawn.position, 
                        Quaternion.Euler(0, 0, fulcrumSpawn.rotationZ), currentGroup);
                    fulcrumGo.transform.localScale = new Vector3(fulcrumSpawn.scale.x, fulcrumSpawn.scale.y, 1f);
                    fulcrumGo.name = fulcrumSpawn.prefabName;
                    Undo.RegisterCreatedObjectUndo(fulcrumGo, "Load Fulcrum");
                    Debug.Log($"[LevelConfigurator] Fulcrum 로드 성공: {fulcrumSpawn.prefabName}, 스케일: {fulcrumSpawn.scale}");
                    spawnCount++;
                }
                else
                {
                    Debug.LogError($"[LevelConfigurator] Fulcrum 프리팹을 찾을 수 없습니다: {fulcrumPath}");
                    Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {fulcrumSpawn.prefabName}");
                }
#endif
            }
        }

        // Background
        if (!string.IsNullOrEmpty(levelData.backgroundSpawn.prefabName))
        {
            string backgroundPath = $"Backgrounds/{levelData.backgroundSpawn.prefabName}";
            Debug.Log($"[LevelConfigurator] Background 프리팹 로드 시도: {backgroundPath}");
            
            // Resources 폴더 내 모든 프리팹 확인
            var allBackgrounds = Resources.LoadAll<GameObject>("Backgrounds");
            Debug.Log($"[LevelConfigurator] Resources/Backgrounds 폴더 내 프리팹들: {string.Join(", ", allBackgrounds.Select(p => p.name))}");
            
#if UNITY_EDITOR
            var backgroundPrefab = Resources.Load<GameObject>(backgroundPath);
            if (backgroundPrefab != null)
            {
                var backgroundGo = (GameObject)PrefabUtility.InstantiatePrefab(backgroundPrefab, currentGroup);
                backgroundGo.transform.position = levelData.backgroundSpawn.position;
                backgroundGo.transform.rotation = Quaternion.Euler(0, 0, levelData.backgroundSpawn.rotationZ);
                backgroundGo.transform.localScale = new Vector3(levelData.backgroundSpawn.scale.x, levelData.backgroundSpawn.scale.y, 1f);
                backgroundGo.name = levelData.backgroundSpawn.prefabName;
                Undo.RegisterCreatedObjectUndo(backgroundGo, "Load Background");
                Debug.Log($"[LevelConfigurator] Background 프리팹 인스턴스(파란색)로 로드: {levelData.backgroundSpawn.prefabName}, 스케일: {levelData.backgroundSpawn.scale}");
                spawnCount++;
            }
            else
            {
                Debug.LogError($"[LevelConfigurator] Background 프리팹을 찾을 수 없습니다: {backgroundPath}");
                Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {levelData.backgroundSpawn.prefabName}");
            }
#else
            // 기존 Instantiate 방식 (런타임)
            var backgroundPrefab = Resources.Load<GameObject>(backgroundPath);
            if (backgroundPrefab != null)
            {
                var backgroundGo = Instantiate(backgroundPrefab, levelData.backgroundSpawn.position, Quaternion.Euler(0, 0, levelData.backgroundSpawn.rotationZ), currentGroup);
                backgroundGo.transform.localScale = new Vector3(levelData.backgroundSpawn.scale.x, levelData.backgroundSpawn.scale.y, 1f);
                backgroundGo.name = levelData.backgroundSpawn.prefabName;
                Undo.RegisterCreatedObjectUndo(backgroundGo, "Load Background");
                Debug.Log($"[LevelConfigurator] Background 로드 성공: {levelData.backgroundSpawn.prefabName}, 스케일: {levelData.backgroundSpawn.scale}");
                spawnCount++;
            }
            else
            {
                Debug.LogError($"[LevelConfigurator] Background 프리팹을 찾을 수 없습니다: {backgroundPath}");
                Debug.LogError($"[LevelConfigurator] 요청된 프리팹 이름: {levelData.backgroundSpawn.prefabName}");
            }
#endif
        }

        // E) 기존 EntityData 방식 (fallback)
        if (spawnCount == 0)
        {
            Debug.LogWarning($"[LevelConfigurator] prefabSpawns가 비어 있어서 기존 EntityData 방식으로 로드합니다.");
            
            // 타입별 루트 생성 (기존 방식)
            var stickParent     = EnsurePreviewRoot(currentGroup, "__Preview_Stick");
            var targetsParent   = EnsurePreviewRoot(currentGroup, "__Preview_Targets");
            var obstaclesParent = EnsurePreviewRoot(currentGroup, "__Preview_Obstacles");
            var fulcrumsParent  = EnsurePreviewRoot(currentGroup, "__Preview_Fulcrums");

            if (HasValidEntity(levelData.stick))
                BuildEntityEditor(levelData.stick, SpawnMarker.SpawnType.Stick, stickParent, isStick: true);

            if (levelData.targets != null)
                foreach (var e in levelData.targets)
                    BuildEntityEditor(e, SpawnMarker.SpawnType.Target, targetsParent, isStick: false);

            if (levelData.obstacles != null)
                foreach (var e in levelData.obstacles)
                    BuildEntityEditor(e, SpawnMarker.SpawnType.Obstacle, obstaclesParent, isStick: false);

            if (levelData.fulcrums != null)
                foreach (var e in levelData.fulcrums)
                    BuildEntityEditor(e, SpawnMarker.SpawnType.Fulcrum, fulcrumsParent, isStick: false);
        }

        // F) 카메라 초기값 적용
        ApplyCameraInitialOnLoad();

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[LevelConfigurator] Loaded LevelData → {groupName} (총 {spawnCount}개 오브젝트 로드)");
    }

    [ContextMenu("Clear Preview (Generated Only)")]
    public void ClearPreview()
    {
        // 컨테이너 기준으로 생성된 것만 지움
        var container = EnsureContainer();
        ClearGeneratedChildren(container);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 내부 유틸
    // ─────────────────────────────────────────────────────────────────────

    private Transform EnsureContainer()
    {
        if (previewContainer != null) return previewContainer;
        var t = transform.Find("__PreviewContainer");
        if (t == null)
        {
            var go = new GameObject("__PreviewContainer");
            Undo.RegisterCreatedObjectUndo(go, "Create Preview Container");
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        previewContainer = t;
        return t;
    }

    private Transform EnsurePreviewRoot(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Preview Root");
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        return t;
    }

    private bool HasValidEntity(LevelData.EntityData e)
    {
        // 스프라이트/콜라이더 하나도 없고 이름도 비어있으면 무효 처리
        bool hasVisual = e.sprite != null || (e.colliders != null && e.colliders.Length > 0);
        return hasVisual || !string.IsNullOrEmpty(e.name);
    }

    private GameObject ResolveStickForSave()
    {
        if (stickRoot != null)
            return stickRoot.childCount > 0 ? stickRoot.GetChild(0).gameObject : stickRoot.gameObject;

        // 폴백: SpawnMarker 중 Stick 타입
        var markers = FindObjectsOfType<SpawnMarker>(true);
        foreach (var m in markers)
            if (m != null && m.type == SpawnMarker.SpawnType.Stick)
                return m.gameObject;
        return null;
    }

    private LevelData.EntityData[] CaptureGroupForSave(Transform root, SpawnMarker.SpawnType type)
    {
        var list = new List<LevelData.EntityData>();

        if (root != null && root.childCount > 0)
        {
            for (int i = 0; i < root.childCount; i++)
                list.Add(CaptureEntity(root.GetChild(i).gameObject, isStick: false));
        }
        else
        {
            var markers = FindObjectsOfType<SpawnMarker>(true);
            foreach (var m in markers)
                if (m != null && m.type == type)
                    list.Add(CaptureEntity(m.gameObject, isStick: false));
        }

        return list.ToArray();
    }

    private LevelData.EntityData CaptureEntity(GameObject go, bool isStick)
    {
        var tr = go.transform;

        var data = new LevelData.EntityData
        {
            name      = go.name,
            tag       = go.tag,
            layer     = go.layer,
            position  = tr.position,                   // ← Z 포함
            rotationZ = tr.eulerAngles.z,
            scale = new Vector2(tr.localScale.x, tr.localScale.y),                 // ← Z 스케일까지 유지
            sprite    = null,
            color     = Color.white,
            colliders = CaptureAllColliders(go),
            tip       = default,

            // Rigidbody2D
            hasRigidbody2D = false,
            rbGravityScale = 0f,

            // Sorting
            sortingLayerName = string.Empty,
            sortingOrder     = 0
        };

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            data.hasRigidbody2D = true;
            data.rbGravityScale = rb.gravityScale;
        }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            data.sprite = sr.sprite;
            data.color  = sr.color;
            data.sortingLayerName = sr.sortingLayerName;
            data.sortingOrder     = sr.sortingOrder;
        }

        // Stick의 Tip 저장
        if (isStick)
        {
            var tipTr = tr.Find("Tip");
            if (tipTr != null)
            {
                var tipCol = tipTr.GetComponent<Collider2D>();
                if (tipCol != null)
                {
                    data.tip = new LevelData.TipData
                    {
                        localPosition  = tipTr.localPosition,
                        localRotationZ = tipTr.localEulerAngles.z,
                        collider       = CaptureCollider(tipCol)
                    };
                }
            }
        }

        return data;
    }

    // EntitySpawnData로 프리팹 참조 저장 (Stick, Obstacle, Target, Fulcrum 공통)
    private LevelData.EntitySpawnData CaptureEntitySpawnData(GameObject go)
    {
        var tr = go.transform;
        string prefabName = go.name;
        Vector2 scale = new Vector2(tr.localScale.x, tr.localScale.y);
        string tag = go.tag;

#if UNITY_EDITOR
        var prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (prefab != null)
        {
            prefabName = prefab.name;
            // 프리팹의 원본 스케일이 (1,1)이므로 현재 스케일을 그대로 저장
            // 만약 프리팹이 다른 스케일을 가지고 있다면 여기서 나누기 연산을 해야 함
            scale = new Vector2(tr.localScale.x, tr.localScale.y);
            Debug.Log($"[LevelConfigurator] 프리팹 인스턴스 발견: {prefabName}, 스케일: {scale}");
        }
        else
        {
            // 프리팹 인스턴스가 아니더라도 이름 규칙+태그가 맞으면 prefabName 강제 지정
            if ((tag == "Stick" && prefabName.StartsWith("St_")) ||
                (tag == "Obstacle" && prefabName.StartsWith("Ob_")) ||
                (tag == "Fulcrum" && prefabName.StartsWith("Fu_")) ||
                (tag == "Target" && prefabName.StartsWith("Ta_")) ||
                (tag == "Background" && prefabName.StartsWith("Bg_")))
            {
                // prefabName 그대로 사용
                Debug.Log($"[LevelConfigurator] 이름/태그 규칙에 맞는 오브젝트: {prefabName}, 스케일: {scale}");
            }
            else
            {
                Debug.LogWarning($"[LevelConfigurator] {go.name}은(는) 프리팹 인스턴스가 아니고, 이름/태그 규칙도 맞지 않습니다. prefabName이 올바르지 않을 수 있습니다.");
            }
        }
#endif
        return new LevelData.EntitySpawnData
        {
            prefabName = prefabName,
            position = tr.position,
            rotationZ = tr.eulerAngles.z,
            scale = scale
        };
    }

    private LevelData.Collider2DData[] CaptureAllColliders(GameObject go)
    {
        var cols = go.GetComponents<Collider2D>();
        var list = new List<LevelData.Collider2DData>(cols.Length);
        foreach (var c in cols)
            list.Add(CaptureCollider(c));
        return list.ToArray();
    }

    private LevelData.Collider2DData CaptureCollider(Collider2D c)
    {
        var d = new LevelData.Collider2DData
        {
            isTrigger = c.isTrigger,
            offset    = c.offset,
            kind      = LevelData.ColliderKind.None,
            size      = Vector2.zero,
            radius    = 0f,
            capsuleDirection = CapsuleDirection2D.Vertical,
            paths     = null,
            edgePoints= null
        };

        if (c is BoxCollider2D b)
        {
            d.kind = LevelData.ColliderKind.Box;
            d.size = b.size;
        }
        else if (c is CircleCollider2D cc)
        {
            d.kind = LevelData.ColliderKind.Circle;
            d.radius = cc.radius;
        }
        else if (c is CapsuleCollider2D cap)
        {
            d.kind = LevelData.ColliderKind.Capsule;
            d.size = cap.size;
            d.capsuleDirection = cap.direction;
        }
        else if (c is PolygonCollider2D poly)
        {
            d.kind = LevelData.ColliderKind.Polygon;
            var paths = new List<LevelData.Path2D>();
            for (int i = 0; i < poly.pathCount; i++)
                paths.Add(new LevelData.Path2D { points = poly.GetPath(i) });
            d.paths = paths.ToArray();
        }
        else if (c is EdgeCollider2D edge)
        {
            d.kind = LevelData.ColliderKind.Edge;
            d.edgePoints = edge.points;
        }

        return d;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Clear helpers
    // ─────────────────────────────────────────────────────────────────────
    private void ClearByMode(Transform parent, ClearMode mode)
    {
        if (parent == null) return;
        if (mode == ClearMode.AllChildren)
            ClearAllChildren(parent);
        else
            ClearGeneratedChildren(parent);
    }

    private void ClearAllChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }

    private void ClearGeneratedChildren(Transform parent)
    {
        if (parent == null) return;
        var flags = parent.GetComponentsInChildren<PreviewGeneratedFlag>(true);
        for (int i = flags.Length - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(flags[i].gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Build (Editor Preview)
    // ─────────────────────────────────────────────────────────────────────
    private void BuildEntityEditor(LevelData.EntityData e, SpawnMarker.SpawnType type, Transform parent, bool isStick)
    {
        var root = new GameObject(string.IsNullOrEmpty(e.name) ? "Entity" : e.name);
        Undo.RegisterCreatedObjectUndo(root, "Load Entity");
        root.transform.SetParent(parent, false);

        // 포즈 복원: Z 포함
        root.transform.position = e.position;
        root.transform.rotation = Quaternion.Euler(0, 0, e.rotationZ);
        root.transform.localScale = new Vector3(e.scale.x, e.scale.y, 1f);           // ← Z 스케일은 복원 X

        root.layer = e.layer;
        if (!string.IsNullOrEmpty(e.tag))
        {
            try { root.tag = e.tag; } catch { /* 태그 미정의 시 무시 */ }
        }

        // 마커 & 정리 플래그
        var marker = Undo.AddComponent<SpawnMarker>(root);
        marker.type = type;
        Undo.AddComponent<PreviewGeneratedFlag>(root);

        // 비주얼
        if (e.sprite != null)
        {
            var sr = Undo.AddComponent<SpriteRenderer>(root);
            sr.sprite = e.sprite;
            sr.color  = (e.color == default ? Color.white : e.color);
            sr.sortingLayerName = e.sortingLayerName;
            sr.sortingOrder     = e.sortingOrder;
        }

        // 콜라이더
        if (e.colliders != null)
            foreach (var cd in e.colliders)
                AddColliderEditor(root, cd);

        // Tip (Stick 전용)
        if (isStick && e.tip.collider.kind != LevelData.ColliderKind.None)
        {
            var tip = new GameObject("Tip");
            Undo.RegisterCreatedObjectUndo(tip, "Load Tip");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = e.tip.localPosition;
            tip.transform.localRotation = Quaternion.Euler(0, 0, e.tip.localRotationZ);
            tip.layer = root.layer;
            AddColliderEditor(tip, e.tip.collider);
            Undo.AddComponent<PreviewGeneratedFlag>(tip);
        }

        // Obstacle에 Rigidbody2D 필요 시 복원
        if (type == SpawnMarker.SpawnType.Obstacle && e.hasRigidbody2D)
        {
            var rb = Undo.AddComponent<Rigidbody2D>(root);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.gravityScale = e.rbGravityScale;
        }
    }

    private void AddColliderEditor(GameObject host, LevelData.Collider2DData d)
    {
        switch (d.kind)
        {
            case LevelData.ColliderKind.Box:
                { var c = Undo.AddComponent<BoxCollider2D>(host); c.isTrigger = d.isTrigger; c.offset = d.offset; c.size = d.size; break; }
            case LevelData.ColliderKind.Circle:
                { var c = Undo.AddComponent<CircleCollider2D>(host); c.isTrigger = d.isTrigger; c.offset = d.offset; c.radius = d.radius; break; }
            case LevelData.ColliderKind.Capsule:
                { var c = Undo.AddComponent<CapsuleCollider2D>(host); c.isTrigger = d.isTrigger; c.offset = d.offset; c.size = d.size; c.direction = d.capsuleDirection; break; }
            case LevelData.ColliderKind.Polygon:
                {
                    var c = Undo.AddComponent<PolygonCollider2D>(host);
                    c.isTrigger = d.isTrigger; c.offset = d.offset;
                    if (d.paths != null && d.paths.Length > 0)
                    {
                        c.pathCount = d.paths.Length;
                        for (int i = 0; i < d.paths.Length; i++) c.SetPath(i, d.paths[i].points);
                    }
                    break;
                }
            case LevelData.ColliderKind.Edge:
                { var c = Undo.AddComponent<EdgeCollider2D>(host); c.isTrigger = d.isTrigger; c.offset = d.offset; if (d.edgePoints != null) c.points = d.edgePoints; break; }
            case LevelData.ColliderKind.None:
            default: break;
        }
    }

    // 카메라 초기값 저장/적용
    private void CaptureCameraInitial()
    {
        var cam = initialCamera ? initialCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[LevelConfigurator] No camera to capture.");
            levelData.cameraInitial = default;
            return;
        }

        levelData.cameraInitial = new LevelData.CameraInitData
        {
            position = cam.transform.position,         // ← Z 포함
            rotationZ = cam.transform.eulerAngles.z,
            orthographicSize = cam.orthographic ? cam.orthographicSize : 0f,
            fieldOfView      = cam.orthographic ? 0f : cam.fieldOfView
        };

        EditorUtility.SetDirty(levelData);
    }

    private void ApplyCameraInitialOnLoad()
    {
        var cam = initialCamera ? initialCamera : Camera.main;
        if (!cam) { Debug.LogWarning("[LevelConfigurator] No Camera found to apply initial."); return; }

        var init = levelData.cameraInitial;

        Undo.RecordObject(cam.transform, "Apply Camera Initial");
        Undo.RecordObject(cam, "Apply Camera Initial");

        cam.transform.position = init.position;               // ← Z 포함
        cam.transform.rotation = Quaternion.Euler(0, 0, init.rotationZ);

        if (cam.orthographic)
        {
            if (init.orthographicSize > 0f) cam.orthographicSize = init.orthographicSize;
        }
        else
        {
            if (init.fieldOfView > 0f) cam.fieldOfView = init.fieldOfView;
        }
    }



    // 생성물 식별 플래그
    // private class GeneratedFlag : MonoBehaviour { }
#endif // UNITY_EDITOR
}