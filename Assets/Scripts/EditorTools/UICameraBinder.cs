// UICameraBinder.cs
using UnityEngine;

[DefaultExecutionOrder(-1000)] // 되도록 빨리 실행
public class UICameraBinder : MonoBehaviour
{
    [Header("References")]
    public Canvas canvas;          // PlayUIOnly의 Canvas
    public Camera localUiCamera;   // PlayUIOnly의 UICamera

    [Header("Optional")]
    public string mainCameraTag = "MainCamera"; // 기본 "MainCamera"

    void Awake()
    {
        if (!canvas) canvas = FindObjectOfType<Canvas>();
        if (!localUiCamera) localUiCamera = GetComponentInChildren<Camera>(true);

        // 안전장치: Canvas는 반드시 Screen Space - Camera
        if (canvas) canvas.renderMode = RenderMode.ScreenSpaceCamera;

        // 메인 카메라 탐색
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam != localUiCamera)
        {
            // ▶ 합쳐서 로드된 경우: 메인 카메라 사용, UICamera 비활성
            if (canvas) canvas.worldCamera = mainCam;
            if (localUiCamera) localUiCamera.enabled = false;
        }
        else
        {
            // ▶ PlayUIOnly 단독 미리보기: 로컬 UICamera 사용
            if (localUiCamera)
            {
                if (canvas) canvas.worldCamera = localUiCamera;
                localUiCamera.enabled = true;

                // 권장값(안전)
                localUiCamera.clearFlags = CameraClearFlags.Depth;
                localUiCamera.cullingMask = LayerMask.GetMask("UI");
                localUiCamera.depth = 100;
                var al = localUiCamera.GetComponent<AudioListener>();
                if (al) al.enabled = false;
            }
            else
            {
                // 최후 보루: 카메라가 하나도 없으면 Overlay로 전환
                if (canvas) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
