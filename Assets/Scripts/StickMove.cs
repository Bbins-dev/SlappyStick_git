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

    [Header("Auto Reset On Obstacle Idle")]
    [Tooltip("Seconds to wait on obstacle before resetting")]
    public float idleOnObstacleSeconds = 1f;          // ← 퍼블릭으로 조절 가능
    [Tooltip("Linear speed under which the stick is considered idle (m/s)")]
    public float idleSpeedThreshold = 0.05f;
    [Tooltip("Angular speed under which the stick is considered idle (deg/s)")]
    public float idleAngularSpeedThreshold = 5f;


    private Rigidbody2D rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private RigidbodyConstraints2D originalConstraints;

    private bool isPositioning = true;
    private bool isHolding = false;
    private bool hasLaunched = false;
    private float holdTime = 0f;
    private CameraFollow cameraFollow;
    private int obstacleContacts = 0; // 현재 Obstacle과의 접촉 수
    private float obstacleIdleTimer = 0f;

    [HideInInspector] public bool IsPositioning => isPositioning;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        originalConstraints = rb.constraints;

        // 🔽 Add: try bind UI right away; if not ready yet, wait a bit.
        TryBindUI();
        if (holdTimeTMP == null)
            StartCoroutine(WaitAndBindUI());

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

        if (holdTimeTMP == null) TryBindUI();

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

    void FixedUpdate()
    {
        // 발사 후 & 포지셔닝이 끝난 상태에서만 체크
        if (hasLaunched && !isPositioning)
            CheckIdleOnObstacle();
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
            SetHoldText($"{holdTime:F2} / {maxHoldTime:F2}");
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
        if (holdTimeTMP == null) TryBindUI();
        if (holdTimeTMP == null) return;
        // HUD가 꺼져 있으면 켠다
        if (UIRoot.Instance != null) UIRoot.Instance.EnsureHUDVisible();
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

        if (c.CompareTag("Obstacle"))
        obstacleContacts++;
    }

    private void OnCollisionEnter2D(Collision2D c)
    {
        if (c.collider.CompareTag("Respawn"))
            ResetStick();

        if (c.collider.CompareTag("Obstacle"))
        obstacleContacts++;
    }

    
    private void OnCollisionExit2D(Collision2D c)
    {
        if (c.collider.CompareTag("Obstacle"))
            obstacleContacts = Mathf.Max(0, obstacleContacts - 1);
    }

    private void OnTriggerExit2D(Collider2D c)
    {
        if (c.CompareTag("Obstacle"))
            obstacleContacts = Mathf.Max(0, obstacleContacts - 1);
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

        // 장애물 리셋 브로드캐스트
        ResetBus.Raise();
        Debug.Log("[StickMove] ResetBus.Raise()");

        // ★ 추가: 매니저를 통한 강제 리셋
        FindObjectOfType<LevelManager>()?.ResetObstacles();
    }

    private void TryBindUI()
    {
        if (holdTimeTMP != null) return;
        if (UIRoot.Instance != null && UIRoot.Instance.holdTimeText != null)
            holdTimeTMP = UIRoot.Instance.holdTimeText;
    }

    private void SetHoldText(string s)
    {
        // lazy bind
        if (holdTimeTMP == null) TryBindUI();
        if (holdTimeTMP == null) return; // 여전히 없으면 그냥 패스

        holdTimeTMP.gameObject.SetActive(true);
        holdTimeTMP.text = s;
    }

    private IEnumerator WaitAndBindUI()
    {
        float t = 0f, timeout = 2f;
        while (holdTimeTMP == null && t < timeout)
        {
            TryBindUI();
            if (holdTimeTMP != null) yield break;
            t += Time.unscaledDeltaTime;
            yield return null; // wait next frame until UI scene finishes loading
        }
    }

    private void CheckIdleOnObstacle()
    {
        // Obstacle과 맞닿아 있고 속도가 매우 낮으면 타이머 증가
        bool touchingObstacle = obstacleContacts > 0;
        bool linearIdle  = rb.velocity.sqrMagnitude <= (idleSpeedThreshold * idleSpeedThreshold);
        bool angularIdle = Mathf.Abs(rb.angularVelocity) <= idleAngularSpeedThreshold;

        if (touchingObstacle && linearIdle && angularIdle)
        {
            obstacleIdleTimer += Time.fixedDeltaTime;
            if (obstacleIdleTimer >= idleOnObstacleSeconds)
            {
                // 리셋 실행
                obstacleIdleTimer = 0f;
                ResetStick(); // ← 네가 이미 쓰는 리셋 로직 (ResetBus, LM.ResetObstacles 포함)
            }
        }
        else
        {
            obstacleIdleTimer = 0f;
        }
    }
}
