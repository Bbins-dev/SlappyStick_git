using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Initial Focus Settings")]
    [Tooltip("Camera will focus on this Transform first")]
    public Transform initialTarget;
    [Tooltip("Seconds to focus on initialTarget before switching")]
    public float initialFocusDuration = 2f;

    [Header("Transition Settings")]
    [Tooltip("Speed when moving from initialTarget to followTarget")]
    public float transitionSpeed = 5f;

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
    private float targetStartY;
    private float initialTimer;
    private bool hasSwitchedToFollow = false;
    public bool IsSwitchedToFollow => hasSwitchedToFollow;

    void Start()
    {
        cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = baseOrthographicSize;

        initialTimer = initialFocusDuration;
    }

    void LateUpdate()
    {
        // 1) Initial focus phase
        if (initialTimer > 0f)
        {
            if (initialTarget != null)
            {
                Vector3 desiredPos = new Vector3(
                    initialTarget.position.x + offset.x,
                    initialTarget.position.y + offset.y,
                    transform.position.z
                );
                transform.position = Vector3.Lerp(transform.position, desiredPos, followSmoothSpeed * Time.deltaTime);
            }

            initialTimer -= Time.deltaTime;
            return;
        }

        // 2) Transition to follow target
        if (!hasSwitchedToFollow)
        {
            if (target != null)
            {
                Vector3 desiredPos = new Vector3(
                    target.position.x + offset.x,
                    target.position.y + offset.y,
                    transform.position.z
                );
                transform.position = Vector3.Lerp(transform.position, desiredPos, transitionSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, desiredPos) < 0.5f)
                {
                    hasSwitchedToFollow = true;
                    targetStartY = target.position.y;
                }
            }
            return;
        }

        // 3) Regular follow + zoom
        if (target == null) return;

        // Follow position
        Vector3 followPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );
        transform.position = Vector3.Lerp(transform.position, followPos, followSmoothSpeed * Time.deltaTime);

        // Zoom based on height
        float heightDelta = target.position.y - targetStartY;
        float desiredSize = baseOrthographicSize + Mathf.Max(0f, heightDelta) * zoomFactor;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredSize, zoomSmoothSpeed * Time.deltaTime);
    }
}
