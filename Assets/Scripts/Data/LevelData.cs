// Assets/Scripts/Data/LevelData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "StickIt/LevelData")]
public class LevelData : ScriptableObject
{
    public enum ColliderKind { None, Box, Circle, Capsule, Polygon, Edge }

    [System.Serializable]
    public struct Path2D
    {
        public Vector2[] points; // local points
    }

    [System.Serializable]
    public struct Collider2DData
    {
        [Header("Common")]
        public ColliderKind kind;
        public bool isTrigger;
        public Vector2 offset;

        [Header("Box/Capsule")]
        public Vector2 size;

        [Header("Circle")]
        public float radius;

        [Header("Capsule")]
        public CapsuleDirection2D capsuleDirection;

        [Header("Polygon")]
        public Path2D[] paths;   // multiple paths (holes) supported

        [Header("Edge")]
        public Vector2[] edgePoints;
    }

    [System.Serializable]
    public struct TipData
    {
        public Vector2 localPosition;
        public float localRotationZ;
        public Collider2DData collider; // tip is usually a trigger
    }

    [System.Serializable]
    public struct CameraInitData
    {
        public Vector3 position;      // initial camera world position
        public float rotationZ;       // initial camera z-rotation (2D)
        public float orthographicSize; // if using orthographic camera (2D)
        public float fieldOfView;     // if using perspective camera (fallback)
    }

    [Header("Camera (Initial)")]
    public CameraInitData cameraInitial;

    [System.Serializable]
    public struct EntityData
    {
        [Header("Identity")]
        public string name;          // optional label
        public string tag;           // optional; must exist in Tag Manager
        public int layer;

        [Header("Transform")]
        public Vector2 position;     // world pos for root entity
        public float rotationZ;
        public Vector2 scale;

        [Header("Visual")]
        public Sprite sprite;        // SpriteRenderer.sprite
        public Color color;          // SpriteRenderer.color
        public string sortingLayerName;
        public int sortingOrder;

        [Header("Physics")]
        public Collider2DData[] colliders; // all colliders on the root

        [Header("Rigidbody (optional)")]
        public bool hasRigidbody2D;
        public float rbGravityScale;

        [Header("Stick only")]
        public TipData tip;          // optional child tip collider
    }

    [Header("Stick (single)")]
    public EntityData stick;

    [Header("Targets")]
    public EntityData[] targets;

    [Header("Obstacles")]
    public EntityData[] obstacles;

    [Header("Fulcrums")]
    public EntityData[] fulcrums;
}
