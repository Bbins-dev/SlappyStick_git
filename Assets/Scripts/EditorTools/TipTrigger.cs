using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TipTrigger : MonoBehaviour
{
    [Tooltip("Tag on a valid target")]
    public string targetTag = "Target";

    private void Reset()
    {
        // Ensure this collider is a trigger
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(targetTag)) return;

        Debug.Log($"[TipTrigger] Hit Target: {other.name}");

        // Stage clear
        if (GameManager.Instance != null)
            GameManager.Instance.StageClear();
        else
            Debug.LogWarning("[TipTrigger] GameManager.Instance is null (editor test?).");
    }
}
