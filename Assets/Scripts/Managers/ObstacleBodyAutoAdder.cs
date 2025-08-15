// Assets/Scripts/Installers/ObstacleBodyAutoAdder.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("StickIt/Obstacle Body Auto Adder")]
[DefaultExecutionOrder(-400)] // LevelManager보다 빨리
public class ObstacleBodyAutoAdder : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only objects with this tag will be processed.")]
    public string obstacleTag = "Obstacle";
    [Tooltip("Process inactive objects as well (editor play)")]
    public bool includeInactive = false;
    [Tooltip("Require at least one Collider2D on the object to add a Rigidbody2D")]
    public bool requireCollider2D = true;

    [Header("Rigidbody2D Defaults (when added)")]
    public RigidbodyType2D bodyType = RigidbodyType2D.Dynamic;
    public float gravityScale = 1f;
    public RigidbodyInterpolation2D interpolation = RigidbodyInterpolation2D.Interpolate;
    public CollisionDetectionMode2D collisionDetection = CollisionDetectionMode2D.Continuous;

    [Header("Extras")]
    [Tooltip("Also add Resettable2D if missing (for your reset flow).")]
    public bool addResettable2D = true;

    [Header("When to Apply")]
    public bool applyOnAwake = true;
    public bool applyOnSceneLoaded = true;

    void Awake()
    {
        if (applyOnAwake) ApplyOnce();
        if (applyOnSceneLoaded)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (applyOnSceneLoaded)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyOnce();
    }

    [ContextMenu("Apply Now")]
    public void ApplyOnce()
    {
        GameObject[] candidates;
#if UNITY_2023_1_OR_NEWER
        candidates = GameObject.FindGameObjectsWithTag(obstacleTag);
#else
        // FindGameObjectsWithTag은 비활성은 못 찾기 때문에 필요하면 전체 스캔
        if (includeInactive)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var go in all)
            {
                if (!go) continue;
                // 씬에 실제 존재하는 오브젝트만
                if (go.scene.IsValid() && go.CompareTag(obstacleTag))
                    list.Add(go);
            }
            candidates = list.ToArray();
        }
        else
        {
            candidates = GameObject.FindGameObjectsWithTag(obstacleTag);
        }
#endif

        foreach (var go in candidates)
        {
            if (!go) continue;
            if (!go.scene.IsValid()) continue;
            if (!includeInactive && !go.activeInHierarchy) continue;

            // 콜라이더 요구 조건
            if (requireCollider2D)
            {
                var hasCol = go.GetComponent<Collider2D>() != null;
                if (!hasCol) continue;
            }

            // 이미 Rigidbody2D가 있으면 건너뜀(기존 세팅 존중)
            var rb = go.GetComponent<Rigidbody2D>();
            if (!rb)
            {
                rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = bodyType;
                rb.gravityScale = gravityScale;
                rb.interpolation = interpolation;
                rb.collisionDetectionMode = collisionDetection;
                // Debug.Log($"[ObstacleBodyAutoAdder] Added Rigidbody2D to {go.name}");
            }

            if (addResettable2D && !go.GetComponent<Resettable2D>())
            {
                go.AddComponent<Resettable2D>();
            }
        }
    }
}
