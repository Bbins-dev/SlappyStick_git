// Assets/Scripts/Managers/LevelManager.cs
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Editor Override (MakingScene)")]
    [Tooltip("When playing in the editor, use this LevelData instead of GameManager/Database.")]
    public bool useEditorOverride = true;
    public LevelData editorOverrideLevel;

    [Header("Parent for spawned objects")]
    public Transform dynamicRoot;

    private void Start()
    {
        LevelData data = null;

#if UNITY_EDITOR
    if (useEditorOverride && editorOverrideLevel != null)
    {
        data = editorOverrideLevel;
        Debug.Log($"[LevelManager] Using editorOverrideLevel: {data.name}");
    }
    else
#endif
    {
        var gm = GameManager.Instance;
        var db = gm != null ? gm.Database : null;
        data = db != null ? db.Get(gm.CurrentLevel) : null;
        Debug.Log($"[LevelManager] Using GameManager/DB: data={(data ? data.name : "NULL")}");
    }

    if (data == null)
    {
        Debug.LogError("[LevelManager] LevelData not found. Assign editorOverrideLevel in MakingScene.");
        return;
    }

    // Clean parent
    if (dynamicRoot == null)
    {
        var go = new GameObject("DynamicRoot");
        dynamicRoot = go.transform;
    }
    else
    {
        for (int i = dynamicRoot.childCount - 1; i >= 0; i--)
            Destroy(dynamicRoot.GetChild(i).gameObject);
    }

    // Build
    var stickGo = BuildEntity(data.stick, isStick: true);
    if (data.targets != null) foreach (var t in data.targets)   BuildEntity(t, false);
    if (data.obstacles != null) foreach (var o in data.obstacles) BuildEntity(o, false);
    if (data.fulcrums != null) foreach (var f in data.fulcrums)  BuildEntity(f, false);

    Debug.Log("[LevelManager] Build complete.");
    }
    
    private GameObject BuildEntity(LevelData.EntityData e, bool isStick)
    {
        var go = new GameObject(string.IsNullOrEmpty(e.name) ? (isStick ? "Stick" : "Entity") : e.name);
        go.transform.SetParent(dynamicRoot, false);
        go.transform.position = e.position;
        go.transform.rotation = Quaternion.Euler(0, 0, e.rotationZ);
        go.transform.localScale = new Vector3(e.scale.x, e.scale.y, 1f);

        if (!string.IsNullOrEmpty(e.tag))
        {
            try { go.tag = e.tag; } catch { /* tag 미등록이면 무시 */ }
        }

        if (e.sprite != null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = e.sprite;
            sr.color = (e.color == default ? Color.white : e.color);
        }

        if (e.colliders != null)
        {
            foreach (var c in e.colliders)
                AddCollider(go, c); // ← 기존 AddCollider 메서드 그대로 사용
        }

        // Tip
        if (isStick && e.tip.collider.kind != LevelData.ColliderKind.None)
        {
            var tip = new GameObject("Tip");
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = e.tip.localPosition;
            tip.transform.localRotation = Quaternion.Euler(0, 0, e.tip.localRotationZ);
            AddCollider(tip, e.tip.collider);
        }

        // 👇 런타임용 최소 보정(Stick만)
        PostBuildRuntimeTuning(go, isStick);

        return go;
    }

    private void PostBuildRuntimeTuning(GameObject go, bool isStick)
    {
        if (!isStick) return;

        // Rigidbody2D 보장
        var rb = go.GetComponent<Rigidbody2D>() ?? go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // TipTrigger 보장 (Target 태그와 충돌 시 처리)
        var tip = go.transform.Find("Tip");
        if (tip != null && tip.GetComponent<TipTrigger>() == null)
            tip.gameObject.AddComponent<TipTrigger>();

        // StickMove 보장 (네 프로젝트의 이동 스크립트명에 맞춰 바꾸세요)
        if (go.GetComponent<StickMove>() == null)
            go.AddComponent<StickMove>(); // TODO: 필요 파라미터가 있으면 여기서 세팅
    }

    private void AddCollider(GameObject host, LevelData.Collider2DData d)
    {
        switch (d.kind)
        {
            case LevelData.ColliderKind.Box:
                {
                    var c = host.AddComponent<BoxCollider2D>();
                    c.isTrigger = d.isTrigger;
                    c.offset = d.offset;
                    c.size = d.size;
                    break;
                }
            case LevelData.ColliderKind.Circle:
                {
                    var c = host.AddComponent<CircleCollider2D>();
                    c.isTrigger = d.isTrigger;
                    c.offset = d.offset;
                    c.radius = d.radius;
                    break;
                }
            case LevelData.ColliderKind.Capsule:
                {
                    var c = host.AddComponent<CapsuleCollider2D>();
                    c.isTrigger = d.isTrigger;
                    c.offset = d.offset;
                    c.size = d.size;
                    c.direction = d.capsuleDirection;
                    break;
                }
            case LevelData.ColliderKind.Polygon:
                {
                    var c = host.AddComponent<PolygonCollider2D>();
                    c.isTrigger = d.isTrigger;
                    c.offset = d.offset;
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
                    var c = host.AddComponent<EdgeCollider2D>();
                    c.isTrigger = d.isTrigger;
                    c.offset = d.offset;
                    if (d.edgePoints != null && d.edgePoints.Length > 0)
                        c.points = d.edgePoints;
                    break;
                }
            case LevelData.ColliderKind.None:
            default:
                break;
        }
    }
}
