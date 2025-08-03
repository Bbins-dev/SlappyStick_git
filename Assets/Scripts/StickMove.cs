using System.Collections;
using UnityEngine;
using TMPro;                  // ← TextMeshPro 네임스페이스

[RequireComponent(typeof(Rigidbody2D))]
public class StickMove : MonoBehaviour
{
    [Header("Hold Settings")]
    [Tooltip("마우스 최대 홀드 시간 (초)")]
    public float maxHoldTime = 2f; // 최대 홀드 시간 (public으로 조절 가능)

    [Header("Launch Settings")]
    [Tooltip("우상향으로 발사할 때의 최대 힘")]
    public float maxLaunchForce = 5f; // 최대 힘
    [Tooltip("발사 시 적용되는 지연 시간 (초)")]
    public float launchDelay = 0.125f; // 발사 지연 시간

    [Header("Launch Direction")]
    [Tooltip("발사 방향의 X축 비율")]
    public float launchX = 1.5f; // X 방향 계수
    [Tooltip("발사 방향의 Y축 비율")]
    public float launchY = 1f;   // Y 방향 계수

    [Header("Rotation Settings")]
    [Tooltip("홀드 100%일 때 적용되는 최대 회전력")]
    public float maxTorque = 200f; // 회전력 최대값
    [Tooltip("홀드 시간 대비 회전력 조절 계수 (빠르게/느리게 회전 조절)")]
    public float torqueMultiplier = 1f; // 회전 속도 조절 계수

    [Header("UI Settings")]
    [Tooltip("홀드 시간 표시용 TMP Text")]
    public TMP_Text holdTimeTMP;   // 인스펙터에 드래그로 할당

    private Rigidbody2D rb;
    private bool isHolding = false;
    private float holdTime = 0f;

    // 최초 위치 및 회전 저장
    private Vector3 startPosition;
    private Quaternion startRotation;
    private CameraFollow cameraFollow;
    private bool hasLaunched = false;  // block input after launch until respawn

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
        startRotation = transform.rotation;

        cameraFollow = Camera.main.GetComponent<CameraFollow>();

        if (holdTimeTMP == null)
        {
            Debug.LogError("⚠ holdTimeTMP가 연결되지 않았습니다!");
        }
        else
        {
            holdTimeTMP.gameObject.SetActive(true);
            holdTimeTMP.text = "UI Test :  OK";
        }
    }

    void Update()
    {

        // 카메라가 전환 완료 전까지 입력 무시
        if (cameraFollow == null || !cameraFollow.IsSwitchedToFollow || hasLaunched)
            return;

        // 마우스 버튼 누르기 시작
        if (Input.GetMouseButtonDown(0))
        {
            isHolding = true;
            holdTime = 0f;
            if (holdTimeTMP != null) holdTimeTMP.gameObject.SetActive(true);
        }

        // 마우스를 누르고 있는 동안 홀드 시간 증가
        if (isHolding)
        {
            holdTime += Time.deltaTime;
            holdTime = Mathf.Clamp(holdTime, 0f, maxHoldTime); // 최대 홀드 시간 제한

            if (holdTimeTMP != null)
                holdTimeTMP.text = $"{holdTime:F2} / {maxHoldTime:F2} sec";
        }

        // 마우스 버튼 뗄 때 발사 처리 시작
        if (Input.GetMouseButtonUp(0) && isHolding)
        {
            isHolding = false;
            float holdPercent = holdTime / maxHoldTime; // 홀드 비율 (0~1)
            StartCoroutine(LaunchAfterDelay(holdPercent));
            hasLaunched = true; // 발사 후 입력 차단

            if (holdTimeTMP != null)
            {
                holdTimeTMP.text = "";
                holdTimeTMP.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator LaunchAfterDelay(float holdPercent)
    {
        yield return new WaitForSeconds(launchDelay);

        // 발사 방향 (x 비중을 높여서 더 오른쪽으로)
        Vector2 launchDirection = new Vector2(launchX, launchY).normalized;

        // 힘 적용
        rb.AddForce(launchDirection * maxLaunchForce * holdPercent, ForceMode2D.Impulse);

        // 회전력 적용
        float torque = maxTorque * holdPercent * torqueMultiplier;
        rb.AddTorque(-torque, ForceMode2D.Impulse); // 시계 방향 회전
    }

    // Respawn 태그 닿으면 초기화
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Respawn"))
        {
            ResetStick();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Respawn"))
        {
            ResetStick();
        }
    }

    private void ResetStick()
    {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.position = startPosition;
        transform.rotation = startRotation;

        hasLaunched = false;  // Respawn 후 다시 입력 허용
    }
}
