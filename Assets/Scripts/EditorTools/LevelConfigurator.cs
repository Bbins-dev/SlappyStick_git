using System.Collections.Generic;
using UnityEngine;
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
        // 그룹 우선 참조
        Transform gStick = null, gTargets = null, gObstacles = null, gFulcrums = null;
        if (currentGroup != null)
        {
            gStick     = currentGroup.Find("__Preview_Stick");
            gTargets   = currentGroup.Find("__Preview_Targets");
            gObstacles = currentGroup.Find("__Preview_Obstacles");
            gFulcrums  = currentGroup.Find("__Preview_Fulcrums");
        }
        // Stick
        GameObject stickGo = null;
        if (gStick != null && gStick.childCount > 0)
            stickGo = gStick.GetChild(0).gameObject;
        else
            stickGo = ResolveStickForSave();
        if (stickGo != null)
            levelData.stickSpawn = CaptureEntitySpawnData(stickGo);
        // Targets
        var targetList = new List<LevelData.EntitySpawnData>();
        Transform tRoot = gTargets != null ? gTargets : targetsRoot;
        if (tRoot != null && tRoot.childCount > 0)
            for (int i = 0; i < tRoot.childCount; i++)
                targetList.Add(CaptureEntitySpawnData(tRoot.GetChild(i).gameObject));
        levelData.targetSpawns = targetList.ToArray();
        // Obstacles
        var obsList = new List<LevelData.EntitySpawnData>();
        Transform oRoot = gObstacles != null ? gObstacles : obstaclesRoot;
        if (oRoot != null && oRoot.childCount > 0)
            for (int i = 0; i < oRoot.childCount; i++)
                obsList.Add(CaptureEntitySpawnData(oRoot.GetChild(i).gameObject));
        levelData.obstacleSpawns = obsList.ToArray();
        // Fulcrums
        var fulcList = new List<LevelData.EntitySpawnData>();
        Transform fRoot = gFulcrums != null ? gFulcrums : fulcrumsRoot;
        if (fRoot != null && fRoot.childCount > 0)
            for (int i = 0; i < fRoot.childCount; i++)
                fulcList.Add(CaptureEntitySpawnData(fRoot.GetChild(i).gameObject));
        levelData.fulcrumSpawns = fulcList.ToArray();
        // 카메라 초기 포즈
        CaptureCameraInitial();
        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelConfigurator] Saved prefab spawns → LevelData: {levelData.name}");
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

        // C) 새 그룹
        string groupName = $"__PreviewGroup_{levelData.name}";
        var groupGO = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(groupGO, "Create Preview Group");
        groupGO.transform.SetParent(container, false);
        currentGroup = groupGO.transform;

        // D) 타입별 루트
        var stickParent     = EnsurePreviewRoot(currentGroup, "__Preview_Stick");
        var targetsParent   = EnsurePreviewRoot(currentGroup, "__Preview_Targets");
        var obstaclesParent = EnsurePreviewRoot(currentGroup, "__Preview_Obstacles");
        var fulcrumsParent  = EnsurePreviewRoot(currentGroup, "__Preview_Fulcrums");

        // E) 스폰
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

        // F) 카메라 초기값 적용
        ApplyCameraInitialOnLoad();

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[LevelConfigurator] Loaded LevelData → {groupName}");
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
#if UNITY_EDITOR
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (prefab != null)
            prefabName = prefab.name;
#endif
        return new LevelData.EntitySpawnData
        {
            prefabName = prefabName,
            position = tr.position,
            rotationZ = tr.eulerAngles.z,
            scale = new Vector2(tr.localScale.x, tr.localScale.y)
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
