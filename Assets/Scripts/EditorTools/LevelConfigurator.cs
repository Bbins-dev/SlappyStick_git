using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Editor-time tool to save/load LevelData from the current scene layout.
/// - Save To LevelData: capture scene objects into LevelData asset
/// - Load From LevelData (Edit): spawn editable preview objects from LevelData
/// - Clear Preview (Generated Only): remove previously generated preview objects
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
    [Tooltip("All preview groups are created under this container")]
    public Transform previewContainer;
    [Tooltip("Last loaded group; Save uses this first")]
    public Transform currentGroup;


#if UNITY_EDITOR
    // ---------- Context Menus ----------
    [ContextMenu("Save To LevelData")]
    public void SaveToLevelData()
    {
        if (levelData == null)
        {
            Debug.LogError("[LevelConfigurator] LevelData asset is not assigned.");
            return;
        }

        // group 우선 부모 잡기
        Transform gStick = null, gTargets = null, gObstacles = null, gFulcrums = null;
        if (currentGroup != null)
        {
            gStick     = currentGroup.Find("__Preview_Stick");
            gTargets   = currentGroup.Find("__Preview_Targets");
            gObstacles = currentGroup.Find("__Preview_Obstacles");
            gFulcrums  = currentGroup.Find("__Preview_Fulcrums");
        }

        // Stick (single)
        GameObject stickGo = null;
        if (gStick != null && gStick.childCount > 0)
            stickGo = gStick.GetChild(0).gameObject;              // >>> group 우선
        else
            stickGo = ResolveStickForSave();                       // 기존 폴백
        levelData.stick = stickGo != null ? CaptureEntity(stickGo, isStick: true) : default;

        // Groups (group 우선 → 없으면 기존 root/marker)
        levelData.targets   = CaptureGroupForSave(gTargets   != null ? gTargets   : targetsRoot,   SpawnMarker.SpawnType.Target);
        levelData.obstacles = CaptureGroupForSave(gObstacles != null ? gObstacles : obstaclesRoot, SpawnMarker.SpawnType.Obstacle);
        levelData.fulcrums  = CaptureGroupForSave(gFulcrums  != null ? gFulcrums  : fulcrumsRoot,  SpawnMarker.SpawnType.Fulcrum);

        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelConfigurator] Saved from {(currentGroup ? currentGroup.name : "scene roots")} to LevelData: {levelData.name}");
    }

    [ContextMenu("Load From LevelData (Edit)")]
    public void LoadFromLevelData()
    {
        
    #if UNITY_EDITOR
        // ensure we are NOT selecting a preview object that will be destroyed
        Selection.activeObject = this.gameObject;
    #endif
    
        if (levelData == null)
        {
            Debug.LogError("[LevelConfigurator] LevelData asset is not assigned.");
            return;
        }

        // (A) 그룹 컨테이너 확보
        var container = EnsureContainer();

        // (B) Clear 단계: None이면 스킵, 그 외엔 컨테이너에서 수행
        if (clearModeOnLoad != ClearMode.None)
            ClearByMode(container, clearModeOnLoad);

        // (C) 새 그룹 생성
        string groupName = $"__PreviewGroup_{levelData.name}";
        var groupGO = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(groupGO, "Create Preview Group");
        groupGO.transform.SetParent(container, false);
        currentGroup = groupGO.transform; // Save에서 사용할 그룹

        // (D) 그룹 하위에 타입별 루트 생성
        var stickParent      = EnsurePreviewRoot(currentGroup, "__Preview_Stick");
        var targetsParent    = EnsurePreviewRoot(currentGroup, "__Preview_Targets");
        var obstaclesParent  = EnsurePreviewRoot(currentGroup, "__Preview_Obstacles");
        var fulcrumsParent   = EnsurePreviewRoot(currentGroup, "__Preview_Fulcrums");

        // (E) 스폰
        if (levelData.stick.sprite != null || levelData.stick.colliders != null)
            BuildEntityEditor(levelData.stick, SpawnMarker.SpawnType.Stick, stickParent, isStick:true);

        if (levelData.targets != null)
            foreach (var e in levelData.targets)
                BuildEntityEditor(e, SpawnMarker.SpawnType.Target, targetsParent, isStick:false);

        if (levelData.obstacles != null)
            foreach (var e in levelData.obstacles)
                BuildEntityEditor(e, SpawnMarker.SpawnType.Obstacle, obstaclesParent, isStick:false);

        if (levelData.fulcrums != null)
            foreach (var e in levelData.fulcrums)
                BuildEntityEditor(e, SpawnMarker.SpawnType.Fulcrum, fulcrumsParent, isStick:false);

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[LevelConfigurator] Loaded LevelData into new group: {groupName}");
    }


    [ContextMenu("Clear Preview (Generated Only)")]
    public void ClearPreview()
    {
        ClearGeneratedChildren(EnsurePreviewRoot("__Preview_Stick",    stickRoot));
        ClearGeneratedChildren(EnsurePreviewRoot("__Preview_Targets",  targetsRoot));
        ClearGeneratedChildren(EnsurePreviewRoot("__Preview_Obstacles",obstaclesRoot));
        ClearGeneratedChildren(EnsurePreviewRoot("__Preview_Fulcrums", fulcrumsRoot));
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif // UNITY_EDITOR

#if UNITY_EDITOR
    private void DeselectIfUnder(Transform root)
    {
        if (!root) return;
        foreach (var o in Selection.objects)
        {
            var go = o as GameObject;
            if (!go) continue;
            var t = go.transform;
            if (t == root || t.IsChildOf(root))
            {
                Selection.objects = new Object[0];
                break;
            }
        }
    }

    private void SafeUndoDestroy(Object obj)
    {
        if (obj != null) Undo.DestroyObjectImmediate(obj);
    }
#endif

#if UNITY_EDITOR
    // === GROUP HELPERS ===
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
        return previewContainer;
    }

    private Transform EnsurePreviewRoot(Transform parent, string name) // overload
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
#endif


    // =====================================================================
    // =========================== SAVE HELPERS =============================
    // =====================================================================
#if UNITY_EDITOR
    private GameObject ResolveStickForSave()
    {
        if (stickRoot != null)
            return stickRoot.childCount > 0 ? stickRoot.GetChild(0).gameObject : stickRoot.gameObject;

        // Fallback: find first marker of type Stick
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
                list.Add(CaptureEntity(root.GetChild(i).gameObject, isStick:false));
        }
        else
        {
            var markers = FindObjectsOfType<SpawnMarker>(true);
            foreach (var m in markers)
                if (m != null && m.type == type)
                    list.Add(CaptureEntity(m.gameObject, isStick:false));
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
            position  = tr.position,
            rotationZ = tr.eulerAngles.z,
            scale     = tr.localScale,
            sprite    = null,
            color     = Color.white,
            colliders = CaptureAllColliders(go),
            tip       = default
        };

        var rb = go.GetComponent<Rigidbody2D>();
        data.hasRigidbody2D = rb != null;
        data.rbGravityScale = rb != null ? rb.gravityScale : 0f;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            data.sprite = sr.sprite;
            data.color = sr.color;
            data.sortingLayerName = sr.sortingLayerName;
            data.sortingOrder     = sr.sortingOrder;
        }

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
                        localPosition = tipTr.localPosition,
                        localRotationZ = tipTr.localEulerAngles.z,
                        collider = CaptureCollider(tipCol)
                    };
                }
            }
        }

        return data;
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
#endif // UNITY_EDITOR

    // =====================================================================
    // =========================== LOAD HELPERS =============================
    // =====================================================================
#if UNITY_EDITOR
    private Transform EnsurePreviewRoot(string autoName, Transform explicitRoot)
    {
        if (explicitRoot != null) return explicitRoot;

        var t = transform.Find(autoName);
        if (t == null)
        {
            var go = new GameObject(autoName);
            Undo.RegisterCreatedObjectUndo(go, "Create Preview Root");
            t = go.transform;
            t.SetParent(transform, false);
        }
        return t;
    }

    private void ClearByMode(Transform parent, ClearMode mode)
    {
        if (parent == null) return;
        if (mode == ClearMode.AllChildren)
            ClearAllChildren(parent);
        else
            ClearGeneratedChildren(parent); // 기존 함수 사용
    }

    private void ClearAllChildren(Transform parent)
    {
    #if UNITY_EDITOR
        for (int i = parent.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    #else
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyImmediate(parent.GetChild(i).gameObject);
    #endif
    }

    private void ClearGeneratedChildren(Transform parent)
    {
        if (parent == null) return;
        var flags = parent.GetComponentsInChildren<GeneratedFlag>(true);
    #if UNITY_EDITOR
        for (int i = flags.Length - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(flags[i].gameObject);
    #else
        for (int i = flags.Length - 1; i >= 0; i--)
            DestroyImmediate(flags[i].gameObject);
    #endif
    }

    private void BuildEntityEditor(LevelData.EntityData e, SpawnMarker.SpawnType type, Transform parent, bool isStick)
    {
        var root = new GameObject(string.IsNullOrEmpty(e.name) ? "Entity" : e.name);
        Undo.RegisterCreatedObjectUndo(root, "Load Entity");
        root.transform.SetParent(parent, false);
        root.transform.position = e.position;
        root.transform.rotation = Quaternion.Euler(0, 0, e.rotationZ);
        root.transform.localScale = new Vector3(e.scale.x, e.scale.y, 1f);
        root.layer = e.layer;

        // Tag (safe assign)
        if (!string.IsNullOrEmpty(e.tag))
        {
            try { root.tag = e.tag; } catch { /* ignore if tag not defined */ }
        }

        // Marker for saving & for cleanup
        var marker = Undo.AddComponent<SpawnMarker>(root);
        marker.type = type;
        Undo.AddComponent<GeneratedFlag>(root); // mark as generated

        // Visual
        if (e.sprite != null)
        {
            var sr = Undo.AddComponent<SpriteRenderer>(root);
            sr.sprite = e.sprite;
            sr.color = (e.color == default ? Color.white : e.color);
            sr.sortingLayerName = e.sortingLayerName;
            sr.sortingOrder = e.sortingOrder;
        }

        // Colliders
        if (e.colliders != null)
            foreach (var cd in e.colliders)
                AddColliderEditor(root, cd);

        // Tip (stick only)
        if (isStick && e.tip.collider.kind != LevelData.ColliderKind.None)
        {
            var tip = new GameObject("Tip");
            Undo.RegisterCreatedObjectUndo(tip, "Load Tip");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = e.tip.localPosition;
            tip.transform.localRotation = Quaternion.Euler(0, 0, e.tip.localRotationZ);
            tip.layer = root.layer; // Tip도 같은 레이어 사용
            AddColliderEditor(tip, e.tip.collider);
            Undo.AddComponent<GeneratedFlag>(tip);
        }
        
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
            {
                var c = Undo.AddComponent<BoxCollider2D>(host);
                c.isTrigger = d.isTrigger;
                c.offset    = d.offset;
                c.size      = d.size;
                break;
            }
            case LevelData.ColliderKind.Circle:
            {
                var c = Undo.AddComponent<CircleCollider2D>(host);
                c.isTrigger = d.isTrigger;
                c.offset    = d.offset;
                c.radius    = d.radius;
                break;
            }
            case LevelData.ColliderKind.Capsule:
            {
                var c = Undo.AddComponent<CapsuleCollider2D>(host);
                c.isTrigger = d.isTrigger;
                c.offset    = d.offset;
                c.size      = d.size;
                c.direction = d.capsuleDirection;
                break;
            }
            case LevelData.ColliderKind.Polygon:
            {
                var c = Undo.AddComponent<PolygonCollider2D>(host);
                c.isTrigger = d.isTrigger;
                c.offset    = d.offset;
                if (d.paths != null && d.paths.Length > 0)
                {
                    c.pathCount = d.paths.Length;
                    for (int i = 0; i < d.paths.Length; i++)
                        c.SetPath(i, d.paths[i].points);
                }
                break;
            }
            case LevelData.ColliderKind.Edge:
            {
                var c = Undo.AddComponent<EdgeCollider2D>(host);
                c.isTrigger = d.isTrigger;
                c.offset    = d.offset;
                if (d.edgePoints != null && d.edgePoints.Length > 0)
                    c.points = d.edgePoints;
                break;
            }
            case LevelData.ColliderKind.None:
            default:
                break;
        }
    }

    // Marker component used to identify generated preview objects
    private class GeneratedFlag : MonoBehaviour { }
#endif // UNITY_EDITOR
}
