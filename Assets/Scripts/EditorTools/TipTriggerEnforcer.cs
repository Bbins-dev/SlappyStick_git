using System.Collections;
using UnityEngine;

[AddComponentMenu("StickIt/TipTrigger Enforcer")]
public class TipTriggerEnforcer : MonoBehaviour
{
    [Header("Search")]
    [Tooltip("Name of the Tip child under the stick.")]
    [SerializeField] private string tipObjectName = "Tip";
    [Tooltip("Tag of the Target collider the tip should hit.")]
    [SerializeField] private string targetTag = "Target";

    [Header("Timing")]
    [Tooltip("Frames to wait before searching (allow level build).")]
    [SerializeField] private int waitFramesBeforeSearch = 2;

    [Header("Defaults")]
    [SerializeField] private float pushInDistance = 0.08f;
    [SerializeField] private float pushInDuration = 0.20f;
    [SerializeField] private float wobbleAmplitude = 8f;
    [SerializeField] private float wobbleFrequency = 6f;
    [SerializeField] private float wobbleDamping = 2f;
    [SerializeField] private float wobbleDuration = 1.0f;
    [SerializeField] private string gameManagerMethodName = "StageClear";

    private void OnEnable()
    {
        StartCoroutine(EnsureTipTrigger());
    }

    private IEnumerator EnsureTipTrigger()
    {
        // Wait a couple frames for level objects to spawn/bind.
        for (int i = 0; i < waitFramesBeforeSearch; i++)
            yield return null;

        // Find the Tip by name anywhere in the scene (first match).
        Transform tip = FindTipTransform();
        if (tip == null)
        {
            Debug.LogWarning("[TipTriggerEnforcer] Could not find Tip transform. Make sure the child is named 'Tip'.");
            yield break;
        }

        // Ensure trigger collider
        var col = tip.GetComponent<Collider2D>();
        if (col == null) col = tip.gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;

        // Add or configure TipTrigger
        var trigger = tip.GetComponent<TipTrigger>();
        if (trigger == null) trigger = tip.gameObject.AddComponent<TipTrigger>();

        // Configure defaults via reflection (fields are serialized-private)
        SetPrivate(trigger, "targetTag", targetTag);
        SetPrivate(trigger, "pushInDistance", pushInDistance);
        SetPrivate(trigger, "pushInDuration", pushInDuration);
        SetPrivate(trigger, "wobbleAmplitude", wobbleAmplitude);
        SetPrivate(trigger, "wobbleFrequency", wobbleFrequency);
        SetPrivate(trigger, "wobbleDamping", wobbleDamping);
        SetPrivate(trigger, "wobbleDuration", wobbleDuration);
        SetPrivate(trigger, "autoNotifyGameManager", true);
        SetPrivate(trigger, "gameManagerMethodName", gameManagerMethodName);
    }

    private Transform FindTipTransform()
    {
        var all = GameObject.FindObjectsOfType<Transform>(true);
        foreach (var t in all)
            if (t.name == tipObjectName) return t;
        return null;
    }

    private void SetPrivate(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (f != null && value != null && f.FieldType.IsAssignableFrom(value.GetType()))
            f.SetValue(obj, value);
    }
}
