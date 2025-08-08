using UnityEngine;

[DisallowMultipleComponent]
public class SpawnMarker : MonoBehaviour
{
    // Nested enum to avoid name clashes across files.
    public enum SpawnType { Stick, Target, Obstacle, Fulcrum }

    [Header("Marker")]
    [Tooltip("What this scene object represents for Level saving.")]
    public SpawnType type = SpawnType.Target;

    [Header("Capture Options")]
    [Tooltip("Save world rotation instead of 0.")]
    public bool useWorldRotation = true;

    [Tooltip("Save world scale instead of (1,1).")]
    public bool useWorldScale = true;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color c = type switch
        {
            SpawnType.Stick    => new Color(0f, 1f, 1f, 0.75f),  // cyan
            SpawnType.Target   => new Color(0f, 1f, 0f, 0.75f),  // green
            SpawnType.Obstacle => new Color(1f, 0f, 0f, 0.75f),  // red
            SpawnType.Fulcrum  => new Color(1f, 1f, 0f, 0.75f),  // yellow
            _ => new Color(1f, 1f, 1f, 0.5f)
        };
        Gizmos.color = c;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
#endif
}
