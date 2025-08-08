// PreviewRuntimeIsolator.cs
using UnityEngine;

public class PreviewRuntimeIsolator : MonoBehaviour
{
    [Tooltip("Preview roots to deactivate on play (e.g., __Preview_* parents).")]
    public Transform[] previewRoots;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        foreach (var r in previewRoots)
            if (r) r.gameObject.SetActive(false); // disable previews during play
    }
}
