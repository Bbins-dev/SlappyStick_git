using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class StickItCamera : MonoBehaviour
{
    [Header("Initial Focus")]
    public Transform initialTarget;
    public float initialFocusDuration = 1.2f;
    public float initialFocusLerp = 5f;

    // Zoom Presets (Ortho) 아래 아무데나
    [Header("Zoom Smoothing")]
    [Tooltip("초기 타겟 연출을 끝내고 포지션캠으로 이동을 시작할 때, travelZoomOutSize까지 부드럽게 확대되는 시간(초).")]
    public float travelZoomSmoothTime = 0.5f;   // 취향껏 0.2~0.5
    private float _travelZoomVel = 0f;          // SmoothDamp용 내부 속도

    [Header("Positioning Camera")]
    public Vector2 positioningCamXY = new Vector2(0f, 3.56f);
    [Tooltip("Linear speed to reach PositioningCam (units/sec).")]
    public float positioningMoveSpeed = 18f;
    [SerializeField, HideInInspector] private float positioningMoveLerp = 0f; // legacy -> speed
    public float positioningArriveThreshold = 0.5f;

    [Header("Zoom Presets (Ortho)")]
    public float initialZoomSize = 8f;
    [Tooltip("포지션캠으로 이동하는 동안 보여줄 큰 줌아웃 사이즈(이동 중 고정).")]
    public float travelZoomOutSize = 11f;
    [Tooltip("포지셔닝 단계에서 클로즈업 사이즈(도착 후 부드럽게 줌인).")]
    public float positioningZoomInSize = 4f;
    [Tooltip("런치/플레이 중 유지 기본 사이즈.")]
    public float playZoomSize = 8f;
    public float zoomLerp = 6f;

    [Header("Follow")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float followLerp = 8f;

    private Camera cam;
    private float initialTimer;
    private bool movingToPositionCam;
    private bool hasReachedPositionCam;
    private bool lastPositioningState;
    private Vector3 positionCamWorld;

    private StickMove stickMove;
    private bool replayOverrideActive;

    public bool IsPositionCamReady => hasReachedPositionCam;

    void Awake()
    {
        cam = GetComponent<Camera>() ?? Camera.main ?? gameObject.AddComponent<Camera>();
        cam.orthographic = true;
        if (Mathf.Abs(offset.z) < 0.001f) offset.z = -10f;

        // legacy 값 자동 이관
        if (positioningMoveSpeed <= 0f && positioningMoveLerp > 0f)
            positioningMoveSpeed = positioningMoveLerp;

        positionCamWorld = new Vector3(positioningCamXY.x, positioningCamXY.y, transform.position.z);
    }

    void Start()
    {
        if (target) stickMove = target.GetComponent<StickMove>();
        if (!initialTarget) initialTarget = target;

        initialTimer = Mathf.Max(0f, initialFocusDuration);
        hasReachedPositionCam = false;
        movingToPositionCam = false;

        // 초기 사이즈(ApplyInitial에서 덮을 수 있음)
        cam.orthographicSize = initialZoomSize;
    }

    void LateUpdate()
    {
        if (replayOverrideActive) return;

        if (target == null)
        {
            var sm = FindObjectOfType<StickMove>();
            if (sm) SetFollowTarget(sm.transform, resetTimers:false);
        }

        // 1) 초기 포커스
        if (initialTimer > 0f && initialTarget != null)
        {
            initialTimer -= Time.deltaTime;

            Vector3 d = new Vector3(initialTarget.position.x + offset.x,
                                    initialTarget.position.y + offset.y,
                                    offset.z);
            transform.position = Vector3.Lerp(transform.position, d, initialFocusLerp * Time.deltaTime);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, initialZoomSize, zoomLerp * Time.deltaTime);

            if (initialTimer <= 0f)
            {
                // 이동 시작: 즉시 큰 줌아웃 고정
                movingToPositionCam = true;
                hasReachedPositionCam = false;
                // cam.orthographicSize = travelZoomOutSize;
                cam.orthographicSize = Mathf.SmoothDamp(
                    cam.orthographicSize, 
                    travelZoomOutSize, 
                    ref _travelZoomVel, 
                    travelZoomSmoothTime
                );
                // 거의 다 왔으면 딱 맞춰 고정(미세 떨림 방지)
                if (Mathf.Abs(cam.orthographicSize - travelZoomOutSize) < 0.01f)
                    cam.orthographicSize = travelZoomOutSize;
            }
            return;
        }

        // 2) 포지션캠으로 '등속' 이동 (이동 중엔 travelZoomOutSize 유지)
        if (movingToPositionCam && !hasReachedPositionCam)
        {
            positionCamWorld = new Vector3(positioningCamXY.x, positioningCamXY.y, offset.z);
            float step = Mathf.Max(0.01f, positioningMoveSpeed) * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, positionCamWorld, step);

            // 이동 중엔 크게 줌아웃 고정(맵 확인 용이)
            cam.orthographicSize = travelZoomOutSize;

            if (Vector3.SqrMagnitude(transform.position - positionCamWorld) <= positioningArriveThreshold * positioningArriveThreshold)
            {
                transform.position = positionCamWorld;
                hasReachedPositionCam = true;
                movingToPositionCam = false;
                // 도착 직후부터 클로즈업으로 '이제' 부드럽게 줌인
            }
            return;
        }

        bool isPositioning = (stickMove != null && stickMove.IsPositioning);

        // 3) 포지셔닝 단계: 위치 고정 + 도착 후 클로즈업으로 서서히 줌인
        if (isPositioning)
        {
            positionCamWorld = new Vector3(positioningCamXY.x, positioningCamXY.y, offset.z);
            transform.position = positionCamWorld;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, positioningZoomInSize, zoomLerp * Time.deltaTime);
        }
        else
        {
            // 포지셔닝 → 플레이 전환 순간: 목표 사이즈를 플레이 사이즈로 설정(빠르게 나가고 싶으면 zoomLerp 올리면 됨)
            if (lastPositioningState && !isPositioning)
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, playZoomSize, zoomLerp * Time.deltaTime);

            // 팔로우
            if (target != null)
            {
                var followPos = new Vector3(target.position.x + offset.x,
                                            target.position.y + offset.y,
                                            offset.z);
                transform.position = Vector3.Lerp(transform.position, followPos, followLerp * Time.deltaTime);
            }

            // 플레이 줌 유지
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, playZoomSize, zoomLerp * Time.deltaTime);
        }

        lastPositioningState = isPositioning;
    }

    // ───────── 공개 API ─────────
    public void ConfigureTargets(Transform initial, Transform follow, bool resetTimers = true)
    {
        initialTarget = initial ? initial : follow;
        SetFollowTarget(follow, resetTimers);
    }

    public void ApplyInitial(LevelData.CameraInitData init)
    {
        transform.position = new Vector3(init.position.x, init.position.y, offset.z);
        transform.rotation = Quaternion.Euler(0, 0, init.rotationZ);

        if (cam.orthographic && init.orthographicSize > 0f)
            cam.orthographicSize = init.orthographicSize;
        else if (!cam.orthographic && init.fieldOfView > 0f)
            cam.fieldOfView = init.fieldOfView;
    }

    public void SetReplayOverride(bool enabled, Transform followOverride = null)
    {
        replayOverrideActive = enabled;

        if (enabled)
        {
            initialTimer = 0f;
            hasReachedPositionCam = true;
            movingToPositionCam = false;

            if (followOverride != null)
            {
                target = followOverride;
                stickMove = target.GetComponent<StickMove>();
            }
        }
        else
        {
            // 리플레이 끝나면 플레이 값 유지
            cam.orthographicSize = playZoomSize;
        }
    }

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
        }
    }
}
