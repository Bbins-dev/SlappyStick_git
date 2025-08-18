using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public class MainCameraSanity : MonoBehaviour
{
    [Header("Masks")]
    [Tooltip("Play 시작 시 카메라를 'UI만 제외한 모든 레이어'로 강제")]
    public bool forceEverythingExceptUI = true;

    [Tooltip("위 옵션을 끄면, 이 목록의 레이어들은 최소 포함되도록 보정")]
    public string[] mustIncludeLayers = new[] { "Default","Stick","Target","Obstacle","Fulcrum" };

    void Awake()  { FixNow(); }
    void OnEnable()
    {
        SceneManager.sceneLoaded        += (_,__) => FixNow();
        SceneManager.activeSceneChanged += (_,__) => FixNow();
        StartCoroutine(RebindWhenMainAppears());
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded        -= (_,__) => FixNow();
        SceneManager.activeSceneChanged -= (_,__) => FixNow();
    }
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
        Camera worldCam = null, uiCam = null;

        foreach (var c in all)
        {
            bool looksUI = c.cullingMask == LayerMask.GetMask("UI") || c.name.Contains("UICamera");
            if (looksUI) uiCam = c; else worldCam ??= c;
        }
        if (!worldCam) return;

        if (uiCam != null && uiCam.CompareTag("MainCamera")) uiCam.tag = "Untagged";
        if (!worldCam.CompareTag("MainCamera")) worldCam.tag = "MainCamera";
        worldCam.enabled = true;

        // ✅ Z 보정(깜빡임 방지)
        if (Mathf.Abs(worldCam.transform.position.z) < 0.001f)
            worldCam.transform.position += new Vector3(0,0,-10);

        // ✅ 마스크 보정
        int uiLayer = LayerMask.NameToLayer("UI");
        int uiMask  = uiLayer >= 0 ? (1 << uiLayer) : 0;

        if (forceEverythingExceptUI)
        {
            worldCam.cullingMask = ~uiMask; // UI만 제외
        }
        else
        {
            int mask = worldCam.cullingMask;
            // 핵심 레이어 최소 포함
            foreach (var n in mustIncludeLayers)
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) mask |= (1 << l);
            }
            // UI 제외
            mask &= ~uiMask;
            worldCam.cullingMask = mask;
        }

        Debug.Log($"[MainCameraSanity] Mask fixed: {LayerMask.LayerToName(uiLayer)} excluded, cam={worldCam.name}");
    }
}
