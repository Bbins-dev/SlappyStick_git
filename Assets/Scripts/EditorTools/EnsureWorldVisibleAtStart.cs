using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class EnsureWorldVisibleAtStart : MonoBehaviour
{
    [Tooltip("시작 몇 프레임 동안 강제 보정")]
    public int frames = 3;

    [Tooltip("UI 레이어 이름(있으면 제외)")]
    public string uiLayerName = "UI";

    int _left;
    int _uiMask;

    void Awake()
    {
        _left = Mathf.Max(1, frames);
        _uiMask = LayerMask.NameToLayer(uiLayerName) >= 0
            ? 1 << LayerMask.NameToLayer(uiLayerName)
            : 0;
    }

    void LateUpdate()
    {
        if (_left <= 0) return;

        var cam = GetComponent<Camera>();
        if (!cam) return;

        // 1) 메인 디스플레이/활성 보장
        cam.enabled = true;
        cam.targetDisplay = 0;

        // 2) 월드 레이어는 켜고, UI만 카메라에서 꺼도 Overlay UI는 그대로 보임
        if (_uiMask != 0)
            cam.cullingMask = cam.cullingMask | ~_uiMask;   // UI 제외 외의 모든 레이어 on
        else
            cam.cullingMask = ~0;                            // 전부 on

        // 3) 투영/크기 보정(2D 기본)
        cam.orthographic = true;
        if (cam.orthographicSize <= 0.01f) cam.orthographicSize = 5f;

        _left--;
    }
}
