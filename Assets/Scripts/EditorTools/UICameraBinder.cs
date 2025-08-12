// UICameraBinder.cs
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class UICameraBinder : MonoBehaviour
{
    [Header("References")]
    public Canvas canvas;
    public Camera localUiCamera;

    [Header("Behavior")]
    [Tooltip("When a Main Camera exists (normal gameplay), use Overlay so world cannot cover UI.")]
    public bool preferOverlayWhenMainCamera = true;

    [Tooltip("When using Screen Space - Camera, place the canvas plane just in front of the camera near clip.")]
    public float planeDistanceOffset = 0.05f;

    [Header("Local Preview (PlayUIOnly only)")]
    [Tooltip("Clear flags used only when previewing PlayUIOnly without a Main Camera.")]
    public CameraClearFlags localPreviewClearFlags = CameraClearFlags.Skybox; // <- Skybox by default
    [Tooltip("Background color when using SolidColor for local preview.")]
    public Color localPreviewBackground = new Color(0.07f, 0.10f, 0.14f, 1f);
    [Tooltip("Add a Skybox component to the local UI camera if missing (uses RenderSettings.skybox).")]
    public bool addSkyboxComponentIfMissing = true;

    [Header("Optional")]
    public string mainCameraTag = "MainCamera";

    private void Awake()
    {
        if (!canvas) canvas = GetComponentInChildren<Canvas>(true) ?? FindObjectOfType<Canvas>();
        if (!localUiCamera) localUiCamera = GetComponentInChildren<Camera>(true);

        var mainCam = Camera.main;

        // Normal gameplay (Main Camera exists) → Overlay (safest)
        if (preferOverlayWhenMainCamera && mainCam != null)
        {
            if (canvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            if (localUiCamera) localUiCamera.enabled = false;
            return;
        }

        // PlayUIOnly preview (no Main Camera) → use local UI camera with chosen clear
        if (localUiCamera != null)
        {
            if (canvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = localUiCamera;

                float near = localUiCamera.nearClipPlane;
                canvas.planeDistance = Mathf.Max(near + planeDistanceOffset, 0.01f);
            }

            localUiCamera.enabled = true;
            localUiCamera.depth = 100;
            localUiCamera.cullingMask = LayerMask.GetMask("UI");

            // Clear flags just for local preview
            localUiCamera.clearFlags = localPreviewClearFlags;
            if (localPreviewClearFlags == CameraClearFlags.SolidColor)
            {
                localUiCamera.backgroundColor = localPreviewBackground;
            }
            else if (localPreviewClearFlags == CameraClearFlags.Skybox && addSkyboxComponentIfMissing)
            {
                // ensure there's a Skybox component (uses RenderSettings.skybox)
                if (!localUiCamera.GetComponent<Skybox>())
                    localUiCamera.gameObject.AddComponent<Skybox>();
            }

            // Avoid double AudioListeners
            var al = localUiCamera.GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }
        else
        {
            // Fallback: no camera at all → Overlay
            if (canvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }
    }
}
