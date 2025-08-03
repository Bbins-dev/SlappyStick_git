using System.Collections;
using System.Collections.Generic;
using UnityEditor.VisionOS;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("The Transform that the camera will follow")]
    public Transform target;
    [Tooltip("Offset from the target's position")]
    public Vector3 offset;
    [Tooltip("How quickly the camera moves to the target")]
    public float smoothSpeed = 5f;

    [Header("Zoom Settings")]
    [Tooltip("Base orthographic size of the camera")]
    public float baseOrthographicSize = 5f;
    [Tooltip("Additional zoom per unit of height above start Y")]
    public float zoomFactor = 0.5f;
    [Tooltip("How quickly the camera zooms in/out")]
    public float zoomSmoothSpeed = 5f;

    private Camera cam;
    private float targetStartY;
    // Update is called once per frame

    void Start()
    {
        cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = baseOrthographicSize;

        if (target != null)
            targetStartY = target.position.y;
    }

     void Update()
    {
        if (target == null) return;

        // Smoothly follow target
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Adjust orthographic size based on height above start Y
        float heightDelta = target.position.y - targetStartY;
        float desiredSize = baseOrthographicSize + Mathf.Max(0f, heightDelta) * zoomFactor;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredSize, zoomSmoothSpeed * Time.deltaTime);
    }
}
