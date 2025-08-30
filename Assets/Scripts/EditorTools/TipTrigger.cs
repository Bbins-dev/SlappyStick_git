using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[AddComponentMenu("StickIt/TipTrigger")]
[DisallowMultipleComponent]
public class TipTrigger : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string targetTag = "Target";

    [Header("Notify Game Manager (Auto)")]
    [Tooltip("If true, tries to find GameManager and call a method by name on clear.")]
    [SerializeField] private bool autoNotifyGameManager = true;
    [Tooltip("Method name to call on GameManager when stage is cleared.")]
    [SerializeField] private string gameManagerMethodName = "StageClear";
    [Tooltip("Optional override: If set, this specific object will receive the message.")]
    [SerializeField] private GameObject gameManagerOverride;

    [Header("UnityEvent (Optional)")]
    public UnityEvent onStageCleared;

    [Header("Push-In Effect")]
    [SerializeField] private float pushInDistance = 0.08f;
    [SerializeField] private float pushInDuration = 0.20f; // 0.2s

    [Header("Dangling Wobble")]
    [SerializeField] private float wobbleAmplitude = 8f;
    [SerializeField] private float wobbleFrequency = 6f;
    [SerializeField] private float wobbleDamping = 2f;
    [SerializeField] private float wobbleDuration = 1.0f;

    [Header("References (Auto if empty)")]
    [SerializeField] private Transform stickRoot;
    [SerializeField] private Rigidbody2D stickRb;

    private bool triggered;
    private RigidbodyType2D originalBodyType;
    private float originalGravityScale;
    public bool HasTriggered => triggered;

    private static Dictionary<Rigidbody2D, bool> _wobbleOriginalKinematic = new Dictionary<Rigidbody2D, bool>();
    private static bool _wobbleActive = false;

    private static void SetOtherRigidbodiesKinematic(bool kinematic, Transform stickRoot)
    {
        var all = Object.FindObjectsOfType<Rigidbody2D>();
        foreach (var rb in all)
        {
            if (rb == null || rb.transform == stickRoot) continue;
            if (kinematic)
            {
                if (!_wobbleOriginalKinematic.ContainsKey(rb))
                    _wobbleOriginalKinematic[rb] = rb.isKinematic;
                rb.isKinematic = true;
            }
            else
            {
                if (_wobbleOriginalKinematic.TryGetValue(rb, out var orig))
                    rb.isKinematic = orig;
            }
        }
        if (!kinematic) _wobbleOriginalKinematic.Clear();
        _wobbleActive = kinematic;
    }

    public static void ForceWobbleEndCleanup()
    {
        if (_wobbleActive)
            SetOtherRigidbodiesKinematic(false, null);
    }

    private void Awake()
    {
        if (stickRoot == null)
            stickRoot = GetComponentInParent<Rigidbody2D>() ? GetComponentInParent<Rigidbody2D>().transform : transform.root;

        if (stickRb == null)
            stickRb = stickRoot.GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;

        triggered = true;

        // 1) Notify/save (safe if no GM)
        TryNotifyGameManager();
        onStageCleared?.Invoke();

        // 2) Visual sequence
        StartCoroutine(DoStuckSequence(other));
    }

    private void TryNotifyGameManager()
    {
        if (!autoNotifyGameManager) return;

        GameObject gm = gameManagerOverride;

        // Prefer Tag = "GameManager"
        if (gm == null)
        {
            var byTag = GameObject.FindGameObjectWithTag("GameManager");
            if (byTag != null) gm = byTag;
        }

        // Fallback: name contains
        if (gm == null)
        {
            var roots = gameObject.scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r.name.Contains("GameManager"))
                {
                    gm = r;
                    break;
                }
            }
        }

        // Last resort: any component named GameManager
        if (gm == null)
        {
            var monos = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m != null && m.GetType().Name == "GameManager")
                {
                    gm = m.gameObject;
                    break;
                }
            }
        }

        if (gm != null && !string.IsNullOrEmpty(gameManagerMethodName))
        {
            gm.SendMessage(gameManagerMethodName, SendMessageOptions.DontRequireReceiver);
        }
    }

    private System.Collections.IEnumerator DoStuckSequence(Collider2D targetCol)
    {
        // Wobble 시작: 다른 Rigidbody2D를 Kinematic으로
        SetOtherRigidbodiesKinematic(true, stickRoot);
        try
        {
            if (stickRb != null)
            {
                originalBodyType = stickRb.bodyType;
                originalGravityScale = stickRb.gravityScale;
                stickRb.velocity = Vector2.zero;
                stickRb.angularVelocity = 0f;
                stickRb.bodyType = RigidbodyType2D.Kinematic;
                stickRb.gravityScale = 0f;
            }

            Vector3 startPos = stickRoot.position;
            Vector3 tipPos = transform.position;
            Vector3 closest = targetCol ? (Vector3)targetCol.ClosestPoint(tipPos) : tipPos + stickRoot.up * 0.01f;
            Vector3 dir = closest - tipPos;
            if (dir.sqrMagnitude < 1e-6f) dir = stickRoot.up;
            dir.Normalize();
            Vector3 endPos = startPos + dir * pushInDistance;

            // Push-in ease-out (≈0.2s)
            float t = 0f;
            while (t < pushInDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / pushInDuration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                stickRoot.position = Vector3.LerpUnclamped(startPos, endPos, eased);
                yield return null;
            }

            // Wobble around tip pivot
            Vector3 pivot = transform.position;
            float time = 0f;
            float prevAngle = 0f;

            while (time < wobbleDuration)
            {
                time += Time.deltaTime;
                float amp = wobbleAmplitude * Mathf.Exp(-wobbleDamping * time);
                float angle = amp * Mathf.Sin(2f * Mathf.PI * wobbleFrequency * time);
                float delta = angle - prevAngle;
                prevAngle = angle;
                stickRoot.RotateAround(pivot, Vector3.forward, delta);
                yield return null;
            }

            if (Mathf.Abs(prevAngle) > 0.001f)
                stickRoot.RotateAround(pivot, Vector3.forward, -prevAngle);
        }
        finally
        {
            // Wobble 종료: 다른 Rigidbody2D 원래대로 복원
            SetOtherRigidbodiesKinematic(false, stickRoot);
        }
    }

    [ContextMenu("Debug Reset Triggered")]
    private void DebugResetTriggered()
    {
        triggered = false;
        if (stickRb != null)
        {
            stickRb.bodyType = originalBodyType;
            stickRb.gravityScale = originalGravityScale;
        }
    }
}
