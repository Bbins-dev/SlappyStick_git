// Assets/Scripts/CameraFollow.cs
using UnityEngine;

[DefaultExecutionOrder(50)] // LevelManager(Start) 이후
public class CameraFollow : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Replay override (리플레이 때는 초기연출/포지셔닝 OFF, 팔로우 전용)
    // ───────────────────────────────────────────────────────────
    [Header("Replay Override")]
    [SerializeField] private bool replayOverride = false;
    private Transform savedInitialTarget;
    private Transform savedFollowTarget;
    private float    savedInitialTimer;
    private bool     savedHasReachedPositionCam;

    // ───────────────────────────────────────────────────────────
    // Initial Focus
    // ───────────────────────────────────────────────────────────
    [Header("Initial Focus Settings")]
    [Tooltip("처음 잠깐 바라볼 대상(없으면 자동 검색 가능)")]
    public Transform initialTarget;
    [Tooltip("초기 타겟을 바라보는 시간(초)")]
    public float initialFocusDuration = 2f;

    // ───────────────────────────────────────────────────────────
    // Positioning Camera
    // ───────────────────────────────────────────────────────────
    [Header("Positioning Camera Settings")]
    public float positioningCamX = 0f;
    public float positioningCamY = 3.56f;
    public float positioningCamThreshold = 0.5f;
    public float positioningCamSpeed = 5f;

    // ───────────────────────────────────────────────────────────
    // Initial Target Auto Binding
    // ───────────────────────────────────────────────────────────
    [Header("Initial Target Auto Binding")]
    [Tooltip("초기 타겟이 비어있으면 태그로 자동 탐색")]
    public bool autoFindInitialByTag = true;
    [Tooltip("초기 타겟으로 찾을 태그")]
    public string initialTargetTag = "Target";
    [Tooltip("여러 개면 스틱(팔로우 타겟)과 가장 가까운 것을 선택")]
    public bool pickClosestTaggedToStick = true;

    // ───────────────────────────────────────────────────────────
    // Follow & Zoom
    // ───────────────────────────────────────────────────────────
    [Header("Follow Settings")]
    [Tooltip("팔로우할 대상(보통 Stick)")]
    public Transform target;
    public Vector3 offset;
    public float followSmoothSpeed = 5f;

    [Header("Zoom Settings")]
    public float baseOrthographicSize = 5f;
    public float zoomFactor = 0.5f;
    public float zoomSmoothSpeed = 5f;

    // ───────────────────────────────────────────────────────────
    // Internals
    // ───────────────────────────────────────────────────────────
    private Camera cam;
    private float  initialTimer;
    private bool   hasReachedPositionCam = false;
    [HideInInspector] public bool IsPositionCamReady => replayOverride || hasReachedPositionCam;

    private Vector3   repositionPos;
    private StickMove stickMove;
    private float     targetStartY;
    private bool      triedAutoBindOnce = false;

    // ───────────────────────────────────────────────────────────

    void Start()
    {
        cam = Camera.main ? Camera.main : GetComponent<Camera>();
        if (cam) { cam.orthographic = true; cam.orthographicSize = baseOrthographicSize; }

        if (target == null) TryAutoBindFollowTarget(); // Stick 자동 바인드

        initialTimer  = initialFocusDuration;
        repositionPos = new Vector3(positioningCamX, positioningCamY, transform.position.z);

        if (target != null)
        {
            stickMove    = target.GetComponent<StickMove>();
            targetStartY = target.position.y;
        }
    }

    void LateUpdate()
    {
        if (target == null) TryAutoBindFollowTarget();

        // ── 리플레이 중엔 팔로우/줌만 ───────────────────────────
        if (replayOverride)
        {
            FollowAndZoom();
            return;
        }

        // ── 1) Initial Focus ───────────────────────────────────
        if (initialTimer > 0f)
        {
            if (initialTarget == null && autoFindInitialByTag && !triedAutoBindOnce)
            {
                initialTarget    = TryFindInitialByTag();
                triedAutoBindOnce = true;
            }

            if (initialTarget == null)
            {
                // 빈 화면으로 시간 끌지 말고 스킵
                initialTimer = 0f;
            }
            else
            {
                Vector3 desired = new Vector3(
                    initialTarget.position.x + offset.x,
                    initialTarget.position.y + offset.y,
                    transform.position.z);

                transform.position = Vector3.Lerp(
                    transform.position, desired, followSmoothSpeed * Time.deltaTime);

                initialTimer -= Time.deltaTime;
                return; // 초기 포커스 중에는 여기서 종료
            }
        }

        // ── 2) Positioning Cam 이동 ────────────────────────────
        if (!hasReachedPositionCam)
        {
            transform.position = Vector3.Lerp(
                transform.position, repositionPos, positioningCamSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, repositionPos) < positioningCamThreshold)
                hasReachedPositionCam = true;

            return;
        }

        // ── 3) Stick 포지셔닝 중엔 고정 ─────────────────────────
        if (stickMove != null && stickMove.IsPositioning)
        {
            transform.position = repositionPos;
            return;
        }

        // ── 4) Follow & Zoom ───────────────────────────────────
        FollowAndZoom();
    }

    // ───────────────────────────────────────────────────────────
    // Public API
    // ───────────────────────────────────────────────────────────

    public void SetFollowTarget(Transform t, bool resetTimers = true)
    {
        target    = t;
        stickMove = t ? t.GetComponent<StickMove>() : null;
        if (t) targetStartY = t.position.y;

        if (resetTimers)
        {
            hasReachedPositionCam = false;
            initialTimer          = initialFocusDuration;
        }

        if (initialTarget == null) initialTarget = t; // 초기타겟 미지정 시 폴백
    }

    public void ConfigureTargets(Transform initial, Transform follow, bool resetTimers = true)
    {
        initialTarget = initial;
        SetFollowTarget(follow, resetTimers);
    }

    public void ApplyInitial(LevelData.CameraInitData init)
    {
        var cam = GetComponent<Camera>();

        // 1) 위치 적용 (z가 0으로 저장돼온 경우 안전 보정)
        Vector3 p = init.position;
        if (Mathf.Approximately(p.z, 0f))
            p.z = (Mathf.Approximately(transform.position.z, 0f) ? -10f : transform.position.z);
        transform.position = p;

        // 2) 회전
        transform.rotation = Quaternion.Euler(0f, 0f, init.rotationZ);

        // 3) 사이즈/FOV (해당 값이 유효할 때만)
        if (cam != null)
        {
            if (cam.orthographic && init.orthographicSize > 0f)
                cam.orthographicSize = init.orthographicSize;
            else if (!cam.orthographic && init.fieldOfView > 0f)
                cam.fieldOfView = init.fieldOfView;
        }
    }

    // 리플레이 진입/해제
    public void EnterReplayOverride(Transform followTarget)
    {
        if (followTarget == null) return;

        // 저장
        savedInitialTarget        = initialTarget;
        savedFollowTarget         = target;
        savedInitialTimer         = initialTimer;
        savedHasReachedPositionCam= hasReachedPositionCam;

        replayOverride            = true;

        // 초기연출/포지셔닝 시퀀스 비활성
        initialTimer              = 0f;
        hasReachedPositionCam     = true;

        // 즉시 팔로우(타이머 리셋 없음)
        SetFollowTarget(followTarget, resetTimers: false);
    }

    public void ExitReplayOverride(Transform restoreInitial, Transform restoreFollow)
    {
        replayOverride = false;

        var init = restoreInitial != null ? restoreInitial : savedInitialTarget;
        var foll = restoreFollow  != null ? restoreFollow  : savedFollowTarget;

        if (foll != null)
            ConfigureTargets(init != null ? init : foll, foll, resetTimers: true);
    }

    public void SetReplayOverride(bool enabled, Transform followOverride = null)
    {
        replayOverride = enabled;
        if (enabled && followOverride != null)
            SetFollowTarget(followOverride, resetTimers: false);
    }

    // ───────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────

    // Stick 자동 바인드 (없을 때 한 번만)
    private void TryAutoBindFollowTarget()
    {
        if (target != null) return;

        var sm = FindObjectOfType<StickMove>();
        if (sm != null) SetFollowTarget(sm.transform, resetTimers: true);
    }

    // Tag=Target 에서 '진짜' 초기 타겟 찾기 (프리뷰/컨피그 제외)
    private Transform TryFindInitialByTag()
    {
        GameObject[] tagged;
        try { tagged = GameObject.FindGameObjectsWithTag(initialTargetTag); }
        catch { return null; } // 태그 미등록 등 예외

        Transform best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < tagged.Length; i++)
        {
            var go = tagged[i];
            if (!go || !go.activeInHierarchy) continue;

            var t = go.transform;
            if (IsEditorPreview(t)) continue;              // 프리뷰/설정용 제외
            if (!HasWorldContent(t)) continue;             // 보일 게 없는 건 제외

            float score;
            if (pickClosestTaggedToStick && target != null)
            {
                score = (t.position - target.position).sqrMagnitude;
            }
            else
            {
                score = (t.position - transform.position).sqrMagnitude;
            }

            if (score < bestScore) { bestScore = score; best = t; }
        }

        // 못 찾으면 null
        return best;
    }

    // 프리뷰/설정 트리 배제 규칙
    private bool IsEditorPreview(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (n.StartsWith("_Preview") || n.Contains("LevelConfigurator") || n.Contains("PreviewGroup"))
                return true;
            t = t.parent;
        }
        return false;
    }

    // 렌더러/콜라이더가 있으면 ‘보일’ 가능성이 높다고 판단
    private bool HasWorldContent(Transform t)
    {
        if (!t) return false;
        if (t.GetComponentInChildren<Renderer>(true)   != null) return true;
        if (t.GetComponentInChildren<Collider2D>(true) != null) return true;
        return false;
    }

    private void FollowAndZoom()
    {
        if (target == null) return;

        // Follow
        Vector3 followPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);

        transform.position = Vector3.Lerp(
            transform.position, followPos, followSmoothSpeed * Time.deltaTime);

        // Zoom
        if (cam != null)
        {
            float heightDelta = target.position.y - targetStartY;
            float desiredSize = baseOrthographicSize + Mathf.Max(0f, heightDelta) * zoomFactor;
            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize, desiredSize, zoomSmoothSpeed * Time.deltaTime);
        }
    }
}
