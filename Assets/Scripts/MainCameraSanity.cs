using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public class MainCameraSanity : MonoBehaviour
{
    public string uiCameraNameContains = "UICamera";
    public bool autoRebind = true;

    void Awake()  { FixNow(); }
    void OnEnable()
    {
        if (!autoRebind) return;
        SceneManager.sceneLoaded        += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        StartCoroutine(RebindWhenMainAppears());
    }
    void OnDisable()
    {
        if (!autoRebind) return;
        SceneManager.sceneLoaded        -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)    => FixNow();
    void OnActiveSceneChanged(Scene prev, Scene cur)=> FixNow();

    IEnumerator RebindWhenMainAppears()
    {
        float t = 0f;
        while (t < 3f)
        {
            if (Camera.main != null) { FixNow(); yield break; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void FixNow()
    {
        var all = GameObject.FindObjectsOfType<Camera>(true);
        Camera uiCam = null, worldCam = null;

        foreach (var c in all)
        {
            bool looksUI = c.cullingMask == LayerMask.GetMask("UI") ||
                           (!string.IsNullOrEmpty(uiCameraNameContains) && c.name.Contains(uiCameraNameContains));
            if (looksUI) { uiCam = c; continue; }
            worldCam ??= c;
        }

        if (worldCam == null)
        {
            Debug.LogWarning("[MainCameraSanity] No non-UI camera found.");
            return;
        }

        if (uiCam != null && uiCam.CompareTag("MainCamera")) uiCam.tag = "Untagged";
        if (!worldCam.CompareTag("MainCamera")) worldCam.tag = "MainCamera";

        worldCam.enabled = true;

        // ✅ Camera.targetDisplay는 0이 Display 1 임
        if (worldCam.targetDisplay != 0)
            worldCam.targetDisplay = 0; // Display 1

        // 마스크가 비었거나 UI만이면 Everything으로 (UI만 제외하려면 필요에 맞게 수정)
        if (worldCam.cullingMask == 0 || worldCam.cullingMask == LayerMask.GetMask("UI"))
            worldCam.cullingMask = ~LayerMask.GetMask("UI");

        Debug.Log($"[MainCameraSanity] MainCamera = {worldCam.name}, targetDisplay(index)={worldCam.targetDisplay}, mask={worldCam.cullingMask}");
    }
}
