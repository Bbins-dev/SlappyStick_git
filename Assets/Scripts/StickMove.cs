using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class StickMove : MonoBehaviour
{
    [Header("Positioning Settings")]
    [Tooltip("Max horizontal drag range from start position")]
    public float positionRange = 0.5f;

    [Header("Hold Settings")]
    [Tooltip("Maximum mouse hold time (seconds)")]
    public float maxHoldTime = 0.5f;

    [Header("Launch Settings")]
    [Tooltip("Maximum launch force at 100% hold")]
    public float maxLaunchForce = 3f;
    [Tooltip("Delay before launch after mouse release (seconds)")]
    public float launchDelay = 0.125f;

    [Header("Launch Direction")]
    [Tooltip("X component of the launch direction")]
    public float launchX = 1.5f;
    [Tooltip("Y component of the launch direction")]
    public float launchY = 1f;

    [Header("Rotation Settings")]
    [Tooltip("Maximum torque at 100% hold")]
    public float maxTorque = 25f;
    [Tooltip("Multiplier to adjust rotation speed")]
    public float torqueMultiplier = 1f;

    [Header("UI Settings")]
    [Tooltip("TMP Text for displaying hold time & messages")]
    public TMP_Text holdTimeTMP;

    private Rigidbody2D rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private RigidbodyConstraints2D originalConstraints;

    private bool isPositioning = true;
    private bool isHolding = false;
    private bool hasLaunched = false;
    private float holdTime = 0f;
    private CameraFollow cameraFollow;
    [HideInInspector]
    public bool IsPositioning => isPositioning;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        originalConstraints = rb.constraints;

        // positioning 모드 진입: Y이동·회전 동결
        isPositioning = true;
        rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

        if (holdTimeTMP != null)
            holdTimeTMP.gameObject.SetActive(false);

        cameraFollow = Camera.main.GetComponent<CameraFollow>();
        // 메시지 숨기기
        HideMessage();
    }

    void Update()
    {
        // 1) Initial Camera Sequence: 입력 불가
        if (isPositioning)
        {
            if (cameraFollow == null || !cameraFollow.IsPositionCamReady)
            {
                ShowMessage("Input not available");
                return;
            }

            // 2) Positioning Phase: 드래그 가능
            ShowMessage("Drag to position");
            HandlePositioning();
            return;
        }

        // 3) Positioning 끝난 후, 발사 전까지
        if (!hasLaunched)
        {
            if (!isHolding)
                ShowMessage("Hold to launch");

            HandleHoldAndLaunch();
            return;
        }

        // 발사 후
        HideMessage();
    }

    private void HandlePositioning()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float clampedX = Mathf.Clamp(world.x,
                startPosition.x - positionRange,
                startPosition.x + positionRange);
            transform.position = new Vector3(clampedX, startPosition.y, startPosition.z);
        }
        if (Input.GetMouseButtonUp(0))
        {
            isPositioning = false;
            rb.constraints = originalConstraints;
        }
    }

    private void HandleHoldAndLaunch()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isHolding = true;
            holdTime = 0f;
            ShowMessage(""); // 숫자 출력 준비
        }
        if (isHolding)
        {
            holdTime += Time.deltaTime;
            holdTime = Mathf.Clamp(holdTime, 0f, maxHoldTime);
            holdTimeTMP.text = $"{holdTime:F2} / {maxHoldTime:F2}";
        }
        if (Input.GetMouseButtonUp(0) && isHolding)
        {
            isHolding = false;
            float p = holdTime / maxHoldTime;
            StartCoroutine(LaunchAfterDelay(p));
            hasLaunched = true;
        }
    }

    private void ShowMessage(string msg)
    {
        if (holdTimeTMP == null) return;
        holdTimeTMP.gameObject.SetActive(true);
        holdTimeTMP.text = msg;
    }

    private void HideMessage()
    {
        if (holdTimeTMP == null) return;
        holdTimeTMP.gameObject.SetActive(false);
    }

    private IEnumerator LaunchAfterDelay(float holdPercent)
    {
        yield return new WaitForSeconds(launchDelay);
        Vector2 dir = new Vector2(launchX, launchY).normalized;
        rb.AddForce(dir * maxLaunchForce * holdPercent, ForceMode2D.Impulse);
        float torque = maxTorque * holdPercent * torqueMultiplier;
        rb.AddTorque(-torque, ForceMode2D.Impulse);
    }

    private void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag("Respawn"))
            ResetStick();
    }

    private void OnCollisionEnter2D(Collision2D c)
    {
        if (c.collider.CompareTag("Respawn"))
            ResetStick();
    }

    private void ResetStick()
    {
        // 위치·회전·속도 리셋
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.position = startPosition;
        transform.rotation = startRotation;

        // positioning 모드 재진입
        isPositioning = true;
        rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

        // 발사 상태 초기화
        hasLaunched = false;
    }
}
