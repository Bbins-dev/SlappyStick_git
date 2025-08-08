using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Initial Focus Settings")]
    [Tooltip("Camera will focus on this Transform first")]
    public Transform initialTarget;
    [Tooltip("Seconds to focus on initialTarget before switching")]
    public float initialFocusDuration = 2f;

    [Header("Positioning Camera Settings")]
    [Tooltip("X position during stick positioning")]
    public float positioningCamX = 0f;
    [Tooltip("Y position during stick positioning")]
    public float positioningCamY = 3.56f;
    [Tooltip("Distance threshold to allow stick positioning")]
    public float positioningCamThreshold = 0.5f;
    [Tooltip("Speed when moving camera to positioning point")]
    public float positioningCamSpeed = 5f;

    [Header("Follow Settings")]
    [Tooltip("The Transform that the camera will follow afterwards")]
    public Transform target;
    [Tooltip("Offset from the target's position")]
    public Vector3 offset;
    [Tooltip("How quickly the camera moves when following")]
    public float followSmoothSpeed = 5f;

    [Header("Zoom Settings")]
    [Tooltip("Base orthographic size of the camera")]
    public float baseOrthographicSize = 5f;
    [Tooltip("Additional zoom per unit of height above start Y")]
    public float zoomFactor = 0.5f;
    [Tooltip("How quickly the camera zooms in/out")]
    public float zoomSmoothSpeed = 5f;

    private Camera cam;
    private float initialTimer;
    private bool hasReachedPositionCam = false;
    // 카메라가 positioningCam 위치에 도달했는지 외부에서 읽기 전용으로 공개
    [HideInInspector] public bool IsPositionCamReady => hasReachedPositionCam;
    private Vector3 repositionPos;
    private StickMove stickMove;
    private float targetStartY;


    void Start()
    {
        cam = Camera.main;
        if (cam == null) cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = baseOrthographicSize;

        if (target == null) TryAutoBindTargets();

        initialTimer = initialFocusDuration;
        repositionPos = new Vector3(positioningCamX, positioningCamY, transform.position.z);

        if (target != null)
        {
            stickMove = target.GetComponent<StickMove>();
            targetStartY = target.position.y;
        }
    }

    void LateUpdate()
    {
        if (target == null) TryAutoBindTargets();

        // 1) Initial Focus Phase
        if (initialTimer > 0f)
        {
            if (initialTarget != null)
            {
                Vector3 desired = new Vector3(
                    initialTarget.position.x + offset.x,
                    initialTarget.position.y + offset.y,
                    transform.position.z);
                transform.position = Vector3.Lerp(transform.position, desired, followSmoothSpeed * Time.deltaTime);
            }
            initialTimer -= Time.deltaTime;
            return;
        }

        // 2) Move to Positioning Camera Point
        if (!hasReachedPositionCam)
        {
            transform.position = Vector3.Lerp(transform.position, repositionPos, positioningCamSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, repositionPos) < positioningCamThreshold)
                hasReachedPositionCam = true;
            return;
        }

        // 3) While Stick Is Positioning, hold camera static
        if (stickMove != null && stickMove.IsPositioning)
        {
            transform.position = repositionPos;
            return;
        }

        // 4) Follow & Zoom after positioning
        if (target == null) return;

        // Follow
        Vector3 followPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);
        transform.position = Vector3.Lerp(transform.position, followPos, followSmoothSpeed * Time.deltaTime);

        // Zoom
        float heightDelta = target.position.y - targetStartY;
        float desiredSize = baseOrthographicSize + Mathf.Max(0f, heightDelta) * zoomFactor;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredSize, zoomSmoothSpeed * Time.deltaTime);
    }

    public void SetFollowTarget(Transform t, bool resetTimers = true)
    {
        target = t;
        stickMove = t ? t.GetComponent<StickMove>() : null;
        if (t) targetStartY = t.position.y;

        if (resetTimers)
        {
            hasReachedPositionCam = false;
            initialTimer = initialFocusDuration;
        }

        // 초기 포커스 대상이 비어있다면 Stick으로
        if (initialTarget == null) initialTarget = t;
    }

    private void TryAutoBindTargets()
    {
        if (target != null) return;
        var sm = FindObjectOfType<StickMove>();
        if (sm != null) SetFollowTarget(sm.transform, resetTimers: true);
    }
    
    public void ConfigureTargets(Transform initial, Transform follow, bool resetTimers = true)
    {
        initialTarget = initial;               // 1) 먼저 초기 포커스 대상 지정 (예: 첫 번째 Target)
        SetFollowTarget(follow, resetTimers);  // 2) 그 다음 스틱을 팔로우 대상으로
    }
}
