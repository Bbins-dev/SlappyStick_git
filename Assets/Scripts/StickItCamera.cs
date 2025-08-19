using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class StickItCamera : MonoBehaviour
{
    // ───────────────────────────────── Initial Focus ─────────────────────────────────
    [Header("Initial Focus")]
    [Tooltip("처음 잠깐 비춰줄 대상(없으면 follow 대상 사용)")]
    public Transform initialTarget;
    [Tooltip("초기 타겟을 비추는 시간(초)")]
    public float initialFocusDuration = 1.2f;
    [Tooltip("초기 타겟을 향해 이동 속도(Lerp 계수)")]
    public float initialFocusLerp = 5f;

    // ─────────────────────────────── Positioning Cam (좌표/이동) ───────────────────────
    [Header("Positioning Camera")]
    [Tooltip("포지셔닝 때 카메라가 머무는 XY (Z는 offset.z 사용)")]
    public Vector2 positioningCamXY = new Vector2(0f, 3.56f);
    [Tooltip("포지션캠으로 이동 속도(단위/초) – 리니어 MoveTowards")]
    public float positioningMoveSpeed = 18f;
    [Tooltip("도착 판정 반경")]
    public float positioningArriveThreshold = 0.5f;

    // ──────────────────────────────── Zoom Presets (Ortho) ───────────────────────────
    [Header("Zoom Presets (Ortho)")]
    [Tooltip("초기 연출에 사용될 기본 사이즈(ApplyInitial에서 덮일 수 있음)")]
    public float initialZoomSize = 8f;
    [Tooltip("타겟 연출 직후, 포지션캠으로 이동하는 동안 보여줄 멀리 보기 사이즈")]
    public float travelZoomOutSize = 11f;
    [Tooltip("포지셔닝 단계에서의 클로즈업 사이즈")]
    public float positioningZoomInSize = 4f;
    [Tooltip("줌 보간 계수(클수록 빠름)")]
    public float zoomLerp = 6f;

    // ────────────── 타겟 연출 → 포지션캠으로 ‘부드럽게’ 줌아웃하기 위한 파라미터 ─────────────
    [Header("Travel Zoom Smoothing (after Target focus)")]
    [Tooltip("여행 줌아웃에 걸리는 시간(초)")]
    public float travelZoomDuration = 0.45f;
    [Tooltip("0→1 구간 이징 곡선")]
    public AnimationCurve travelZoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // 내부(여행줌)
    float _travelZoomFrom;  // 시작 사이즈(스냅샷)
    float _travelZoomT;     // 0→1 진행도

    // ───────────────────────────────── Follow & Zoom ────────────────────────────────
    [Header("Follow")]
    [Tooltip("연출 이후 따라갈 대상(Stick)")]
    public Transform target;
    [Tooltip("타겟 기준 오프셋. Z는 -10 권장")]
    public Vector3 offset = new Vector3(0, 0, -10);
    [Tooltip("팔로우 이동 보간 계수")]
    public float followLerp = 8f;

    [Header("Follow Zoom (after launch)")]
    [Tooltip("런치/플레이 중 유지 기본 사이즈")]
    public float followBaseSize = 8f;
    [Tooltip("타겟이 시작 Y보다 높아질 때 1유닛당 추가 확대량")]
    public float followZoomFactor = 0.5f;
    [Tooltip("런치 후 동적줌 반응 속도")]
    public float followZoomLerp = 8f;

    // ───────────────────────────────── State ────────────────────────────────────────
    private Camera cam;
    private float initialTimer;
    private bool movingToPositionCam;
    private bool hasReachedPositionCam;
    private bool lastPositioningState;
    private Vector3 positionCamWorld;

    private StickMove stickMove;
    private bool replayOverrideActive;

    private float _followStartY;              // 포지셔닝 종료 시점의 스틱 Y
    public bool IsPositionCamReady => hasReachedPositionCam;

    // ─────────────────────────────── Unity lifecycle ────────────────────────────────
    void Awake()
    {
        cam = GetComponent<Camera>() ?? Camera.main ?? gameObject.AddComponent<Camera>();
        cam.orthographic = true;

        if (Mathf.Abs(offset.z) < 0.001f) offset.z = -10f;          // 안전 Z
        positionCamWorld = new Vector3(positioningCamXY.x, positioningCamXY.y, offset.z);
    }

    void Start()
    {
        if (target) stickMove = target.GetComponent<StickMove>();
        if (!initialTarget) initialTarget = target;

        initialTimer = Mathf.Max(0f, initialFocusDuration);
        hasReachedPositionCam = false;
        movingToPositionCam = false;

        // 초기 사이즈(ApplyInitial에서 이미 설정됐으면 그 값을 유지)
        if (cam.orthographicSize <= 0f) cam.orthographicSize = initialZoomSize;
        else initialZoomSize = cam.orthographicSize; // 씬/데이터 값 반영
    }

    void LateUpdate()
    {
        if (replayOverrideActive) return;

        // 타겟 자동 바인딩(안전망)
        if (target == null)
        {
            var sm = FindObjectOfType<StickMove>();
            if (sm) SetFollowTarget(sm.transform, resetTimers: false);
        }

        // ── 1) 초기 타겟 연출 ─────────────────────────────────────────────
        if (initialTimer > 0f && initialTarget != null)
        {
            initialTimer -= Time.deltaTime;

            // 위치/줌: 타겟으로 점점
            Vector3 desired = new Vector3(
                initialTarget.position.x + offset.x,
                initialTarget.position.y + offset.y,
                offset.z
            );
            transform.position = Vector3.Lerp(transform.position, desired, initialFocusLerp * Time.deltaTime);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, initialZoomSize, zoomLerp * Time.deltaTime);

            // 연출 끝 → 여행 시작 준비(팝 방지: 현 사이즈 스냅샷)
            if (initialTimer <= 0f)
            {
                movingToPositionCam = true;
                hasReachedPositionCam = false;

                _travelZoomFrom = cam.orthographicSize; // ← 지금 사이즈 캡처
                _travelZoomT = 0f;                      // ← 시간 0에서 시작
            }
            return;
        }

        // ── 2) 포지션캠으로 ‘등속’ 이동 + 여행 중 부드러운 줌아웃 ─────────────
        if (movingToPositionCam && !hasReachedPositionCam)
        {
            positionCamWorld = new Vector3(positioningCamXY.x, positioningCamXY.y, offset.z);

            // 선형 이동
            float step = Mathf.Max(0.01f, positioningMoveSpeed) * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, positionCamWorld, step);

            // 부드러운 여행 줌아웃(곡선 기반)
            _travelZoomT += Time.deltaTime / Mathf.Max(0.0001f, travelZoomDuration);
            float t = Mathf.Clamp01(_travelZoomT);
            float eased = travelZoomCurve.Evaluate(t);
            cam.orthographicSize = Mathf.Lerp(_travelZoomFrom, travelZoomOutSize, eased);

            // 도착 판정
            if ((transform.position - positionCamWorld).sqrMagnitude <= positioningArriveThreshold * positioningArriveThreshold)
            {
                transform.position = positionCamWorld;
                hasReachedPositionCam = true;
                movingToPositionCam = false;
            }
            return;
        }

        // 현재 스틱 포지셔닝 중인지?
        bool isPositioning = (stickMove != null && stickMove.IsPositioning);

        // ── 3) 포지셔닝 단계: 위치 고정 + 서서히 클로즈업 ─────────────────────
        if (isPositioning)
        {
            positionCamWorld = new Vector3(positioningCamXY.x, positioningCamXY.y, offset.z);
            transform.position = positionCamWorld;

            // 도착 후부터 클로즈업으로 부드럽게
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, positioningZoomInSize, zoomLerp * Time.deltaTime);
        }
        else
        {
            // 포지셔닝 → 플레이 전환하는 순간: 동적줌 기준 Y 기록
            if (lastPositioningState && !isPositioning && target != null)
                _followStartY = target.position.y;

            // 팔로우 이동
            if (target != null)
            {
                var followPos = new Vector3(
                    target.position.x + offset.x,
                    target.position.y + offset.y,
                    offset.z
                );
                transform.position = Vector3.Lerp(transform.position, followPos, followLerp * Time.deltaTime);

                // 런치 후 동적 줌(위로 갈수록 확대, 내려오면 복귀)
                float heightDelta = target.position.y - _followStartY;
                float desiredSize = followBaseSize + Mathf.Max(0f, heightDelta) * followZoomFactor;

                cam.orthographicSize = Mathf.Lerp(
                    cam.orthographicSize,
                    desiredSize,
                    Time.deltaTime * followZoomLerp
                );
            }
            else
            {
                // 타겟이 일시적으로 없으면 플레이 기본 사이즈로 유지
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, followBaseSize, Time.deltaTime * zoomLerp);
            }
        }

        lastPositioningState = isPositioning;
    }

    // ─────────────────────────────── Public API ───────────────────────────────
    public void ConfigureTargets(Transform initial, Transform follow, bool resetTimers = true)
    {
        initialTarget = initial ? initial : follow;
        SetFollowTarget(follow, resetTimers);
    }

    public void ApplyInitial(LevelData.CameraInitData init)
    {
        // 저장된 카메라 포즈 적용(항상 Z는 offset.z로 보정)
        transform.position = new Vector3(init.position.x, init.position.y, offset.z);
        transform.rotation = Quaternion.Euler(0, 0, init.rotationZ);

        if (cam.orthographic && init.orthographicSize > 0f)
            cam.orthographicSize = init.orthographicSize;
        else if (!cam.orthographic && init.fieldOfView > 0f)
            cam.fieldOfView = init.fieldOfView;

        // 초기값 갱신(팝 방지용)
        initialZoomSize = cam.orthographic ? cam.orthographicSize : initialZoomSize;
    }

    public void SetReplayOverride(bool enabled, Transform followOverride = null)
    {
        replayOverrideActive = enabled;

        if (enabled)
        {
            // 연출/이동 시퀀스 비활성화
            initialTimer = 0f;
            hasReachedPositionCam = true;
            movingToPositionCam = false;

            if (followOverride != null)
            {
                target = followOverride;
                stickMove = target.GetComponent<StickMove>();
                _followStartY = target.position.y; // 동적 줌 기준
            }
        }
        else
        {
            // 리플레이 종료 후엔 현재 상태 유지
        }
    }

    // 내부 공용
    private void SetFollowTarget(Transform t, bool resetTimers)
    {
        target = t;
        stickMove = t ? t.GetComponent<StickMove>() : null;

        if (resetTimers)
        {
            initialTimer = Mathf.Max(0f, initialFocusDuration);
            hasReachedPositionCam = false;
            movingToPositionCam = false;
            lastPositioningState = false;
            _travelZoomT = 0f;

            if (target) _followStartY = target.position.y;
        }
    }
}
