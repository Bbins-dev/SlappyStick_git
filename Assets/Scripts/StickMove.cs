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

    private Rigidbody2D rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private RigidbodyConstraints2D originalConstraints;

    private bool isPositioning = true;
    private bool isHolding = false;
    private bool hasLaunched = false;
    private float holdTime = 0f;
    private CameraFollow cameraFollow;

    // 👇 Unified contact counter for idle-reset surfaces
    private int surfaceContacts = 0;
    private float surfaceIdleTimer = 0f;

    private TipTrigger tipTrigger;

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
    }

    void Update()
    {
        if (holdTimeTMP == null) TryBindUI();

        // 1) Initial Camera Sequence: input blocked
        if (isPositioning)
        {
            if (cameraFollow == null || !cameraFollow.IsPositionCamReady)
            {
                ShowMessage("Input not available");
                return;
            }

            // 2) Positioning phase
            ShowMessage("Drag to position");
            HandlePositioning();
            return;
        }

        // 3) After positioning, before launch
        if (!hasLaunched)
        {
            if (!isHolding)
                ShowMessage("Hold to launch");

            HandleHoldAndLaunch();
            return;
        }

        // After launch
        HideMessage();
    }

    void FixedUpdate()
    {
        // Check idle only after launch & after positioning
        if (hasLaunched && !isPositioning)
            CheckIdleOnSurface();
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
            ShowMessage(""); // prepare numeric output
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

    // ----------------------
    // Contact bookkeeping
    // ----------------------
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

    // ----------------------
    // Idle check (unified)
    // ----------------------
    private void CheckIdleOnSurface()
    {

        if (tipTrigger != null && tipTrigger.HasTriggered)
        return; // stuck/clear 연출 중에는 idle 리셋 중단
        
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

    // ----------------------
    // Reset
    // ----------------------
    private void ResetStick()
    {
        // position/rotation/velocity reset
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.position = startPosition;
        transform.rotation = startRotation;

        // back to positioning mode
        isPositioning = true;
        rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

        // launch state reset
        hasLaunched = false;

        // broadcast obstacle reset
        ResetBus.Raise();
        Debug.Log("[StickMove] ResetBus.Raise()");

        // force reset via LevelManager (if exists)
        FindObjectOfType<LevelManager>()?.ResetObstacles();

        // clear counters/timers
        surfaceContacts = 0;
        surfaceIdleTimer = 0f;
    }

    // ----------------------
    // UI bind helpers
    // ----------------------
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
