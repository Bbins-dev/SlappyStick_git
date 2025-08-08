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
        // 1) Stick 먼저 생성
        var stickGo = BuildEntity(data.stick, isStick: true);

        // 2) Targets 생성하면서 "첫 번째 타겟 Transform" 기억
        Transform firstTarget = null;
        if (data.targets != null)
        {
            foreach (var t in data.targets)
            {
                var go = BuildEntity(t, false);
                if (firstTarget == null) firstTarget = go.transform;
            }
        }

        // 3) Obstacles / Fulcrums
        if (data.obstacles != null) foreach (var o in data.obstacles) BuildEntity(o, false);
        if (data.fulcrums  != null) foreach (var f in data.fulcrums)  BuildEntity(f, false);

        // 4) 카메라에 "초기 포커스=첫 타겟, 팔로우=스틱" 전달
        var camFollow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camFollow != null && stickGo != null)
        {
            camFollow.ConfigureTargets(
                firstTarget != null ? firstTarget : stickGo.transform, // initialTarget
                stickGo.transform,                                     // follow target
                resetTimers: true
            );
        }

        Debug.Log("[LevelManager] Build complete.");
        }
        
        private GameObject BuildEntity(LevelData.EntityData e, bool isStick)
        {
            var go = new GameObject(string.IsNullOrEmpty(e.name) ? (isStick ? "Stick" : "Entity") : e.name);
            go.transform.SetParent(dynamicRoot, false);
            go.transform.position = e.position;
            go.transform.rotation = Quaternion.Euler(0, 0, e.rotationZ);
            go.transform.localScale = new Vector3(e.scale.x, e.scale.y, 1f);
            go.layer = e.layer;
            go.layer = Mathf.Clamp(e.layer, 0, 31);

            if (!string.IsNullOrEmpty(e.tag))
            {
                try { go.tag = e.tag; } catch { /* tag 미등록이면 무시 */ }
            }

            if (e.sprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = e.sprite;
                sr.color = (e.color == default ? Color.white : e.color);
                if (!string.IsNullOrEmpty(e.sortingLayerName))
                sr.sortingLayerName = e.sortingLayerName;
                sr.sortingOrder = e.sortingOrder;
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
                tip.layer = e.layer; // Tip도 같은 레이어 사용
                tip.transform.SetParent(go.transform, false);
                tip.transform.localPosition = e.tip.localPosition;
                tip.transform.localRotation = Quaternion.Euler(0, 0, e.tip.localRotationZ);
                tip.layer = go.layer;
                AddCollider(tip, e.tip.collider);
            }

        // 👇 런타임용 최소 보정(Stick만)
        PostBuildRuntimeTuning(go, isStick);

        return go;
    }

    private void PostBuildRuntimeTuning(GameObject go, bool isStick)
    {
        if (!isStick) return;

        // 1) Rigidbody2D 보장 (없으면 추가)
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // rb.gravityScale = 1f; // 필요하면 조절

        // 2) Tip 트리거/스테이지클리어 보장
        var tip = go.transform.Find("Tip");
        if (tip != null)
        {
            var tipCol = tip.GetComponent<Collider2D>();
            if (tipCol != null) tipCol.isTrigger = true;

            if (tip.GetComponent<TipTrigger>() == null)
                tip.gameObject.AddComponent<TipTrigger>();
        }

        // 3) StickMove 스크립트 보장 (네 스크립트명에 맞춰 사용)
        var move = go.GetComponent<StickMove>() ?? go.AddComponent<StickMove>();
        if (move.holdTimeTMP == null && UIRoot.Instance != null)
            move.holdTimeTMP = UIRoot.Instance.holdTimeText;
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
