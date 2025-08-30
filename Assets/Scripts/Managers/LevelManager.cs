// Assets/Scripts/Managers/LevelManager.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{

    [Header("Editor Override (MakingScene)")]
    [Tooltip("When playing in the editor, use this LevelData instead of GameManager/Database.")]
    public bool useEditorOverride = true;
    public LevelData editorOverrideLevel;

    [Header("Parent for spawned objects")]
    public Transform dynamicRoot;

    private readonly List<Resettable2D> allResets = new List<Resettable2D>();
    private LevelData currentLevelData;

    private void Start()
    {
#if UNITY_EDITOR
        DisableEditorPreviewsIfAny();   // 🔹 가장 먼저 프리뷰 비활성화
#endif

        EnsureDynamicRoot();
        // LevelData 결정
#if UNITY_EDITOR
        if (useEditorOverride && editorOverrideLevel != null)
            currentLevelData = editorOverrideLevel;
        else
#endif
        {
            var gm = GameManager.Instance;
            var db = gm != null ? gm.Database : null;
            currentLevelData = db != null ? db.Get(gm.CurrentLevel) : null;
        }
        Build(currentLevelData);
    }

    private void Build(LevelData data)
    {
        allResets.Clear();
        if (data == null)
        {
            Debug.LogError("[LevelManager] LevelData not found. Assign editorOverrideLevel in MakingScene.");
            return;
        }

        // Build
        // 1) Stick 먼저 생성 (prefab 기반 우선)
        GameObject stickGo = null;
        if (!string.IsNullOrEmpty(data.stickSpawn.prefabName))
        {
            var prefab = Resources.Load<GameObject>($"Sticks/{data.stickSpawn.prefabName}");
            if (prefab == null)
            {
                Debug.LogError($"[LevelManager] Stick prefab not found: {data.stickSpawn.prefabName}");
            }
            else
            {
                stickGo = Instantiate(prefab, data.stickSpawn.position, Quaternion.Euler(0, 0, data.stickSpawn.rotationZ), dynamicRoot);
                stickGo.transform.localScale = new Vector3(data.stickSpawn.scale.x, data.stickSpawn.scale.y, 1f);
                stickGo.name = data.stickSpawn.prefabName;
                // Resettable2D, TipTrigger 보장
                if (stickGo.GetComponent<Resettable2D>() == null) stickGo.AddComponent<Resettable2D>();
                var tip = stickGo.transform.Find("Tip");
                if (tip != null && tip.GetComponent<TipTrigger>() == null) tip.gameObject.AddComponent<TipTrigger>();
                allResets.Add(stickGo.GetComponent<Resettable2D>());
            }
        }
        // fallback: 기존 방식
        if (stickGo == null)
            stickGo = BuildEntity(data.stick, isStick: true);

        // 2) Targets (Prefab 기반 우선)
        Transform firstTarget = null;
        if (data.targetSpawns != null && data.targetSpawns.Length > 0)
        {
            foreach (var t in data.targetSpawns)
            {
                var prefab = Resources.Load<GameObject>($"Targets/{t.prefabName}");
                if (prefab == null)
                {
                    Debug.LogError($"[LevelManager] Target prefab not found: {t.prefabName}");
                    continue;
                }
                var go = Instantiate(prefab, t.position, Quaternion.Euler(0, 0, t.rotationZ), dynamicRoot);
                go.transform.localScale = new Vector3(t.scale.x, t.scale.y, 1f);
                go.name = t.prefabName;
                if (go.GetComponent<Resettable2D>() == null) go.AddComponent<Resettable2D>();
                allResets.Add(go.GetComponent<Resettable2D>());
                if (firstTarget == null) firstTarget = go.transform;
            }
        }
        else if (data.targets != null)
        {
            foreach (var t in data.targets)
            {
                var go = BuildEntity(t, isStick: false, isObstacle: false);
                if (firstTarget == null) firstTarget = go.transform;
            }
        }

        // 3) Obstacles (Prefab 기반 우선)
        if (data.obstacleSpawns != null && data.obstacleSpawns.Length > 0)
        {
            foreach (var o in data.obstacleSpawns)
            {
                var prefab = Resources.Load<GameObject>($"Obstacles/{o.prefabName}");
                if (prefab == null)
                {
                    Debug.LogError($"[LevelManager] Obstacle prefab not found: {o.prefabName}");
                    continue;
                }
                var go = Instantiate(prefab, o.position, Quaternion.Euler(0, 0, o.rotationZ), dynamicRoot);
                go.transform.localScale = new Vector3(o.scale.x, o.scale.y, 1f);
                go.name = o.prefabName;
                if (go.GetComponent<Resettable2D>() == null) go.AddComponent<Resettable2D>();
                allResets.Add(go.GetComponent<Resettable2D>());
            }
        }
        else if (data.obstacles != null)
        {
            foreach (var o in data.obstacles)
                BuildEntity(o, isStick: false, isObstacle: true);
        }

        // 4) Fulcrums (Prefab 기반 우선)
        if (data.fulcrumSpawns != null && data.fulcrumSpawns.Length > 0)
        {
            foreach (var f in data.fulcrumSpawns)
            {
                var prefab = Resources.Load<GameObject>($"Fulcrums/{f.prefabName}");
                if (prefab == null)
                {
                    Debug.LogError($"[LevelManager] Fulcrum prefab not found: {f.prefabName}");
                    continue;
                }
                var go = Instantiate(prefab, f.position, Quaternion.Euler(0, 0, f.rotationZ), dynamicRoot);
                go.transform.localScale = new Vector3(f.scale.x, f.scale.y, 1f);
                go.name = f.prefabName;
                if (go.GetComponent<Resettable2D>() == null) go.AddComponent<Resettable2D>();
                allResets.Add(go.GetComponent<Resettable2D>());
            }
        }
        else if (data.fulcrums != null)
        {
            foreach (var f in data.fulcrums)
                BuildEntity(f, isStick: false, isObstacle: false);
        }

        var cam = FindWorldCamera();
        var camFollow = cam ? cam.GetComponent<StickItCamera>() : null;
        if (camFollow != null && stickGo != null)
        {
            camFollow.ApplyInitial(data.cameraInitial);
            camFollow.ConfigureTargets(
                firstTarget != null ? firstTarget : stickGo.transform,
                stickGo.transform,
                resetTimers: true
            );
        }

        Debug.Log("[LevelManager] Build complete.");
    }

    private GameObject BuildEntity(LevelData.EntityData e, bool isStick, bool isObstacle = false)
    {
        var go = new GameObject(string.IsNullOrEmpty(e.name) ? (isStick ? "Stick" : "Entity") : e.name);
        go.transform.SetParent(dynamicRoot, false);
        go.transform.position = e.position;
        go.transform.rotation = Quaternion.Euler(0, 0, e.rotationZ);
        go.transform.localScale = new Vector3(e.scale.x, e.scale.y, 1f);
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

        // --- Obstacle handling (Rigidbody + Reset registration) ---
        if (isObstacle)
        {
            // 1) Decide whether this obstacle should have a Rigidbody2D
            // bool wantRb = e.hasRigidbody2D;
            // 변경: 저장값과 무관하게 Obstacle은 항상 RB 부여
            bool wantRb = true;

            // (선택) 레이어로 강제 Dynamic 처리하고 싶다면: obstacleDynamicLayer 사용
            // wantRb |= (obstacleDynamicLayer >= 0 && e.layer == obstacleDynamicLayer);

            // 2) Get-or-Add Rigidbody2D (Dynamic / Continuous)
            if (wantRb)
            {
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb == null) rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                // 저장값이 있으면 그대로, 없으면 기본값(예: 1f) 지정
                rb.gravityScale = (e.hasRigidbody2D ? e.rbGravityScale :
                                (Mathf.Approximately(e.rbGravityScale, 0f) ? 1f : e.rbGravityScale));
            }

            // 3) Get-or-Add Resettable2D (초기 상태 캡처 → 이벤트/강제 리셋 대응)
            var reset = go.GetComponent<Resettable2D>();
            if (reset == null) reset = go.AddComponent<Resettable2D>();
            allResets.Add(reset);
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

    // ─────────────────────────────────────────────────────────────
    // DynamicRoot를 반드시 LevelManager의 자식 + 같은 씬으로 보장
    // ─────────────────────────────────────────────────────────────
    private void EnsureDynamicRoot()
    {
        // 1) 없으면 만들고 부모를 LevelManager로
        if (dynamicRoot == null)
        {
            var go = new GameObject("DynamicRoot");
            go.transform.SetParent(this.transform, false);      // ← 부모를 LvlMgr로!
            dynamicRoot = go.transform;
        }
        else
        {
            // 있으면 부모 재설정(혹시 루트에 떠 있으면)
            dynamicRoot.SetParent(this.transform, false);
        }

        // 2) 씬 일치 보장(다른 씬에 있으면 강제 이동)
        var myScene = gameObject.scene;
        if (dynamicRoot.gameObject.scene != myScene)
            SceneManager.MoveGameObjectToScene(dynamicRoot.gameObject, myScene);

        // 3) 자식 정리(런타임 스폰물 초기화)
        for (int i = dynamicRoot.childCount - 1; i >= 0; i--)
            Destroy(dynamicRoot.GetChild(i).gameObject);

        // (선택) 같은 씬 루트에 떠 있는 ‘다른’ DynamicRoot가 있으면 정리
        var roots = myScene.GetRootGameObjects();
        foreach (var r in roots)
        {
            if (r != dynamicRoot.gameObject && r.name == "DynamicRoot")
                Destroy(r); // 같은 씬의 떠다니는 잔재 제거
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Disable any editor preview containers/groups so they don't collide with runtime spawns.
    /// Looks for names used by LevelConfigurator: "__PreviewContainer", "__Preview_*", "__PreviewGroup_*".
    /// </summary>
    private void DisableEditorPreviewsIfAny()
    {
        var scene = gameObject.scene;
        var roots = scene.GetRootGameObjects();
        int disabled = 0;

        foreach (var go in roots)
            disabled += DisableRecursive(go.transform);

        if (disabled > 0)
            Debug.Log($"[LevelManager] Disabled {disabled} editor preview object(s).");

        int DisableRecursive(Transform t)
        {
            int count = 0;
            string n = t.name;

            bool isPreview =
                n == "__PreviewContainer" ||
                n.StartsWith("__Preview_") ||
                n.StartsWith("__PreviewGroup_");

            if (isPreview)
            {
                if (t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    count++;
                }
                return count; // 이 밑의 자식들은 굳이 확인할 필요 없이 꺼짐
            }

            for (int i = 0; i < t.childCount; i++)
                count += DisableRecursive(t.GetChild(i));
            return count;
        }
    }
#endif

    public void ResetAllEntities()
    {
        int count = 0;
        foreach (var r in allResets)
        {
            if (r == null) continue;
            r.ResetNow();
            count++;
        }
        Debug.Log($"[LevelManager] ResetAllEntities invoked: {count} items.");
    }

    public void RestartLevel()
    {
        // 1. DynamicRoot 하위 게임 오브젝트(Stick, Target, Obstacle, Fulcrum 등)만 삭제
        if (dynamicRoot != null)
        {
            var toDestroy = new List<GameObject>();
            foreach (Transform child in dynamicRoot)
            {
                // UI가 아닌 오브젝트만 삭제 (UI는 별도 계층에 있어야 함)
                toDestroy.Add(child.gameObject);
            }
            foreach (var go in toDestroy)
            {
                Destroy(go);
            }
        }
        // 2. allResets 리스트 Clear
        allResets.Clear();
        // 3. 레벨 데이터로 다시 빌드
        Build(currentLevelData);
        // 4. 필요시 UI/팝업 등은 별도 관리 (ClearPopup 등은 외부에서 닫기)
        Debug.Log("[LevelManager] RestartLevel: 오브젝트/이벤트/리스트 초기화 및 재생성 완료");
    }

    private Camera FindWorldCamera()
    {
        var cams = GameObject.FindObjectsOfType<Camera>(true);
        foreach (var c in cams)
        {
            if (!c.enabled) continue;
            // UI 카메라(마스크가 UI만인 카메라)는 제외
            if (c.cullingMask == LayerMask.GetMask("UI")) continue;
            return c;
        }
        return Camera.main; // 최후 보루
    }


}
