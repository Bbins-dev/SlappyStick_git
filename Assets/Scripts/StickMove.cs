using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization; // For FormerlySerializedAs

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

    [Header("Auto Reset On Surface Idle")]
    [FormerlySerializedAs("idleOnObstacleSeconds")]
    [Tooltip("Seconds to wait while idle on specified tags before resetting")]
    public float idleOnSurfaceSeconds = 1f;

    [Tooltip("Linear speed under which the stick is considered idle (m/s)")]
    public float idleSpeedThreshold = 0.05f;

    [Tooltip("Angular speed under which the stick is considered idle (deg/s)")]
    public float idleAngularSpeedThreshold = 5f;

    [Tooltip("Tags that trigger idle-reset when touching (e.g., Obstacle, Floor, Target)")]
    public string[] idleResetTags = new[] { "Obstacle", "Floor", "Target" };

    // ───────────────────────── NEW: fast-stuck heuristics ─────────────────────────
    [Header("Fast Reset (Stuck/Oscillation)")]
    [Tooltip("Hard cap: reset if a shot lasts longer than this (seconds)")]
    public float maxShotSeconds = 7f;

    [Tooltip("Look-back window to measure progress (seconds)")]
    public float progressWindowSeconds = 0.8f;

    [Tooltip("If moved less than this distance within the window while in contact, consider stuck")]
    public float minProgressDistance = 0.15f;

    [Tooltip("How long the 'no progress' condition must persist before reset (seconds)")]
    public float contactStuckSeconds = 0.5f;

    [Tooltip("Time window to count angular velocity sign flips (seconds)")]
    public float angularFlipWindowSeconds = 1.2f;

    [Tooltip("If sign flips exceed this within the window (and moving slowly), reset")]
    public int angularFlipCountToReset = 10;

    // ──────────────────────────────────────────────────────────────────────────────

    private Rigidbody2D rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private RigidbodyConstraints2D originalConstraints;

    private bool isPositioning = true;
    private bool isHolding = false;
    private bool hasLaunched = false;
    private float holdTime = 0f;
    private CameraFollow cameraFollow;

    private int surfaceContacts = 0;
    private float surfaceIdleTimer = 0f;

    private TipTrigger tipTrigger;

    // NEW: timers/buffers for fast-stuck
    private float shotTimer = 0f;
    private float stuckTimer = 0f;

    private Vector2[] posBuffer;
    private float[]   timeBuffer;
    private int bufSize;
    private int bufIndex = 0;

    private int angularFlipCount = 0;
    private float angularFlipTimer = 0f;
    private float lastAngularVel = 0f;
    private void OnDisable()  { ReplayManager.Instance?.EndRecording(false); }
    private void OnDestroy()  { ReplayManager.Instance?.EndRecording(false); }

    [HideInInspector] public bool IsPositioning => isPositioning;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        originalConstraints = rb.constraints;

        var tip = transform.Find("Tip");
        if (tip) tipTrigger = tip.GetComponent<TipTrigger>();

        TryBindUI();
        if (holdTimeTMP == null)
            StartCoroutine(WaitAndBindUI());

        // Positioning mode: freeze Y & rotation
        isPositioning = true;
        rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

        if (holdTimeTMP != null)
            holdTimeTMP.gameObject.SetActive(false);

        cameraFollow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
        HideMessage();

        // NEW: init ring buffer for progress window
        bufSize = Mathf.Clamp(Mathf.CeilToInt(progressWindowSeconds / Mathf.Max(Time.fixedDeltaTime, 0.005f)) + 6, 16, 240);
        posBuffer  = new Vector2[bufSize];
        timeBuffer = new float[bufSize];
        SeedProgressBuffer();
    }

    void Update()
    {
        if (holdTimeTMP == null) TryBindUI();

        if (isPositioning)
        {
            if (cameraFollow == null || !cameraFollow.IsPositionCamReady)
            {
                ShowMessage("Input not available");
                return;
            }

            ShowMessage("Drag to position");
            HandlePositioning();
            return;
        }

        if (!hasLaunched)
        {
            if (!isHolding)
                ShowMessage("Hold to launch");

            HandleHoldAndLaunch();
            return;
        }

        HideMessage();
    }

    void FixedUpdate()
    {
        if (hasLaunched && !isPositioning)
        {
            shotTimer += Time.fixedDeltaTime;
            UpdateProgressBuffer();

            CheckIdleOnSurface();      // 기존 느린-아이들 체크
            CheckFastStuckHeuristics(); // NEW 빠른-스턱 체크
        }
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

            // reset shot timers just in case
            shotTimer = 0f;
            stuckTimer = 0f;
            angularFlipCount = 0;
            angularFlipTimer = 0f;
            lastAngularVel = rb.angularVelocity;
            SeedProgressBuffer();
        }
    }

    private void HandleHoldAndLaunch()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isHolding = true;
            
            ReplayManager.Instance?.TryDeleteCache(); // ★ 캐시 있을 시 제거
    
            // ★★★ 리플레이 시작 (발사 커밋 시점)
            ReplayManager.Instance?.BeginRecording(transform);

            holdTime = 0f;
            ShowMessage("");
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

            // NEW: start shot window fresh
            shotTimer = 0f;
            stuckTimer = 0f;
            angularFlipCount = 0;
            angularFlipTimer = 0f;
            lastAngularVel = 0f;
            SeedProgressBuffer();
        }
    }

    private void ShowMessage(string msg)
    {
        if (holdTimeTMP == null) TryBindUI();
        if (holdTimeTMP == null) return;
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

    // ---------------------- Contacts ----------------------
    private bool IsIdleResetTag(string tag)
    {
        if (idleResetTags == null) return false;
        for (int i = 0; i < idleResetTags.Length; i++)
        {
            if (!string.IsNullOrEmpty(idleResetTags[i]) && tag == idleResetTags[i])
                return true;
        }
        return false;
    }

    private void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag("Respawn"))
            ResetStick();

        if (IsIdleResetTag(c.tag))
            surfaceContacts++;
    }

    private void OnTriggerExit2D(Collider2D c)
    {
        if (IsIdleResetTag(c.tag))
            surfaceContacts = Mathf.Max(0, surfaceContacts - 1);
    }

    private void OnCollisionEnter2D(Collision2D c)
    {
        if (c.collider.CompareTag("Respawn"))
            ResetStick();

        if (IsIdleResetTag(c.collider.tag))
            surfaceContacts++;
    }

    private void OnCollisionExit2D(Collision2D c)
    {
        if (IsIdleResetTag(c.collider.tag))
            surfaceContacts = Mathf.Max(0, surfaceContacts - 1);
    }

    // ---------------------- Idle (existing) ----------------------
    private void CheckIdleOnSurface()
    {
        if (tipTrigger != null && tipTrigger.HasTriggered) return;

        bool touching = surfaceContacts > 0;
        bool linearIdle  = rb.velocity.sqrMagnitude <= (idleSpeedThreshold * idleSpeedThreshold);
        bool angularIdle = Mathf.Abs(rb.angularVelocity) <= idleAngularSpeedThreshold;

        if (touching && linearIdle && angularIdle)
        {
            surfaceIdleTimer += Time.fixedDeltaTime;
            if (surfaceIdleTimer >= idleOnSurfaceSeconds)
            {
                surfaceIdleTimer = 0f;
                ResetStick();
            }
        }
        else
        {
            surfaceIdleTimer = 0f;
        }
    }

    // ---------------------- NEW: fast-stuck ----------------------
    private void SeedProgressBuffer()
    {
        if (posBuffer == null || timeBuffer == null) return;
        bufIndex = 0;
        var p = (Vector2)transform.position;
        for (int i = 0; i < bufSize; i++)
        {
            posBuffer[i] = p;
            timeBuffer[i] = Time.time;
        }
    }

    private void UpdateProgressBuffer()
    {
        if (posBuffer == null) return;
        posBuffer[bufIndex]  = rb.position;
        timeBuffer[bufIndex] = Time.time;
        bufIndex = (bufIndex + 1) % bufSize;
    }

    private float DistanceSinceWindow(float seconds)
    {
        float cutoff = Time.time - seconds;
        // find oldest sample within window
        int idx = bufIndex;
        for (int i = 0; i < bufSize; i++)
        {
            int j = (bufIndex - 1 - i + bufSize) % bufSize;
            if (timeBuffer[j] <= cutoff || i == bufSize - 1)
            {
                idx = j;
                break;
            }
        }
        return Vector2.Distance(rb.position, posBuffer[idx]);
    }

    private void CheckFastStuckHeuristics()
    {
        if (tipTrigger != null && tipTrigger.HasTriggered) return;

        // (A) hard cap by time
        if (shotTimer >= maxShotSeconds)
        {
            ResetStick();
            return;
        }

        // (B) low progress while touching
        if (surfaceContacts > 0)
        {
            float moved = DistanceSinceWindow(progressWindowSeconds);
            if (moved < minProgressDistance)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= contactStuckSeconds)
                {
                    ResetStick();
                    return;
                }
            }
            else stuckTimer = 0f;
        }
        else stuckTimer = 0f;

        // (C) oscillation (angular velocity sign flips)
        float av = rb.angularVelocity;
        bool flipped =
            Mathf.Sign(av) != Mathf.Sign(lastAngularVel) &&
            Mathf.Abs(av) > 1f && Mathf.Abs(lastAngularVel) > 1f;

        if (flipped) angularFlipCount++;
        lastAngularVel = av;

        angularFlipTimer += Time.fixedDeltaTime;
        if (angularFlipTimer >= angularFlipWindowSeconds)
        {
            angularFlipTimer = 0f;
            angularFlipCount = 0;
        }

        bool slowLinear = rb.velocity.magnitude < 0.3f;
        if (surfaceContacts > 0 && slowLinear && angularFlipCount >= angularFlipCountToReset)
        {
            ResetStick();
        }
    }

    // ---------------------- Reset ----------------------
    private void ResetStick()
    {
        ReplayManager.Instance?.EndRecording(false); //녹화 종료

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.position = startPosition;
        transform.rotation = startRotation;

        isPositioning = true;
        rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

        hasLaunched = false;

        ResetBus.Raise();
        Debug.Log("[StickMove] ResetBus.Raise()");

        FindObjectOfType<LevelManager>()?.ResetObstacles();

        surfaceContacts = 0;
        surfaceIdleTimer = 0f;

        // NEW: clear fast-stuck state
        shotTimer = 0f;
        stuckTimer = 0f;
        angularFlipCount = 0;
        angularFlipTimer = 0f;
        lastAngularVel = 0f;
        SeedProgressBuffer();
    }

    // ---------------------- UI bind helpers ----------------------
    private void TryBindUI()
    {
        if (holdTimeTMP != null) return;
        if (UIRoot.Instance != null && UIRoot.Instance.holdTimeText != null)
            holdTimeTMP = UIRoot.Instance.holdTimeText;
    }

    private void SetHoldText(string s)
    {
        if (holdTimeTMP == null) TryBindUI();
        if (holdTimeTMP == null) return;
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
            yield return null;
        }
    }
}
