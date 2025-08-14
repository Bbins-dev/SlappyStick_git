using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor.SceneManagement; // EditorSceneManager
#endif

[AddComponentMenu("StickIt/ClearPopup Controller")]
public class ClearPopupController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Buttons")]
    [SerializeField] private Button btnSaveScreenshot;
    [SerializeField] private Button btnRestart;
    [SerializeField] private Button btnLevelSelect;
    [SerializeField] private Button btnNextLevel;
    [SerializeField] private TextMeshProUGUI nextLevelLabel;   // child label of Btn_NextLevel

    [Header("Show/Hide FX")]
    [SerializeField] private float tweenDuration = 0.25f;
    [SerializeField] private float showScale = 1.0f;
    [SerializeField] private float hideScale = 0.9f;
    [SerializeField] private Selectable firstSelected;

    [Header("Scenes")]
    [Tooltip("Scene name for the Level Select Scene screen (single load).")]
    public string levelSelectScene = "LevelSelectScene";

    [Header("Pause")]
    [SerializeField] private bool pauseOnShow = true;

    

    private Coroutine fxCo;
    private bool isShowing;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        AutoWireButtons();
        SetVisible(false, instant: true);
    }

    private void AutoWireButtons()
    {
        btnSaveScreenshot ??= transform.Find("Btn_SaveScreenshot")?.GetComponent<Button>();
        btnRestart       ??= transform.Find("Btn_Restart")?.GetComponent<Button>();
        btnLevelSelect   ??= transform.Find("Btn_LevelSelect")?.GetComponent<Button>();
        btnNextLevel     ??= transform.Find("Btn_NextLevel")?.GetComponent<Button>();
        if (btnNextLevel && !nextLevelLabel)
            nextLevelLabel = btnNextLevel.GetComponentInChildren<TextMeshProUGUI>(true);

        if (btnSaveScreenshot) { btnSaveScreenshot.onClick.RemoveAllListeners(); btnSaveScreenshot.onClick.AddListener(OnClick_SaveScreenshot); }
        if (btnRestart)       { btnRestart.onClick.RemoveAllListeners();       btnRestart.onClick.AddListener(OnClick_RestartLevel); }
        if (btnLevelSelect)   { btnLevelSelect.onClick.RemoveAllListeners();   btnLevelSelect.onClick.AddListener(OnClick_LevelSelect); }
        if (btnNextLevel)     { btnNextLevel.onClick.RemoveAllListeners();     btnNextLevel.onClick.AddListener(OnClick_NextLevel); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Show / Hide
    // ─────────────────────────────────────────────────────────────────────────
    public void Show()
    {
        if (isShowing) return;
        isShowing = true;

        if (pauseOnShow) Time.timeScale = 0f;

        RefreshButtons();
        SetVisible(true, instant: false);

        if (firstSelected)
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(firstSelected.gameObject);
    }

    public void Hide()
    {
        if (!isShowing) return;
        isShowing = false;

        if (pauseOnShow) Time.timeScale = 1f;
        SetVisible(false, instant: false);
    }

    private void SetVisible(bool visible, bool instant)
    {
        if (fxCo != null) StopCoroutine(fxCo);
        fxCo = StartCoroutine(DoFx(visible, instant));
    }

    private IEnumerator DoFx(bool visible, bool instant)
    {
        float dur = instant ? 0f : tweenDuration;
        float t = 0f;

        float startA = canvasGroup.alpha;
        float endA   = visible ? 1f : 0f;

        Vector3 startS = transform.localScale;
        Vector3 endS   = Vector3.one * (visible ? showScale : hideScale);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = dur <= 0f ? 1f : Mathf.Clamp01(t / dur);
            float eased = 1f - Mathf.Pow(1f - p, 3f); // easeOutCubic
            canvasGroup.alpha = Mathf.Lerp(startA, endA, eased);
            transform.localScale = Vector3.Lerp(startS, endS, eased);
            yield return null;
        }

        canvasGroup.alpha = endA;
        transform.localScale = endS;

        canvasGroup.interactable   = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI State (NextLevel lock)
    // ─────────────────────────────────────────────────────────────────────────
    private void RefreshButtons()
    {
        var gm = GameManager.Instance;
        bool canNext = false;

        if (gm != null)
        {
            int next = gm.CurrentLevel + 1;
            bool exists   = next <= Mathf.Max(1, gm.TotalLevels);
            bool unlocked = next <= Mathf.Max(1, gm.highestUnlockedLevel);
            canNext = exists && unlocked;
        }

        if (btnNextLevel) btnNextLevel.interactable = canNext;
        if (nextLevelLabel) nextLevelLabel.text = canNext ? "Next Level" : "Next Level (Locked)";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button handlers
    // ─────────────────────────────────────────────────────────────────────────
    public void OnClick_SaveScreenshot() { StartCoroutine(CaptureWorldOffscreenAndKeepPopup()); }

    public void OnClick_RestartLevel()   { StartCoroutine(RestartGameplaySceneKeepUI()); }

    public void OnClick_LevelSelect()
        {
            Time.timeScale = 1f;

        #if UNITY_EDITOR
            // 씬 이름이 빌드에 없으면 경로로 로드(에디터 플레이 전용)
            if (!IsSceneInBuild(levelSelectScene))
            {
                string path = FindScenePathByName(levelSelectScene);
                if (!string.IsNullOrEmpty(path))
                {
                    var p = new LoadSceneParameters(LoadSceneMode.Single);
                    UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, p);
                    return;
                }
                Debug.LogError($"[ClearPopup] Scene '{levelSelectScene}' not found in Build Settings or by path.");
            }
        #endif

            UnityEngine.SceneManagement.SceneManager.LoadScene(levelSelectScene, LoadSceneMode.Single);
        }

        #if UNITY_EDITOR
        private static bool IsSceneInBuild(string sceneName)
        {
            foreach (var s in UnityEditor.EditorBuildSettings.scenes)
                if (System.IO.Path.GetFileNameWithoutExtension(s.path) == sceneName)
                    return true;
            return false;
        }

        private static string FindScenePathByName(string sceneName)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneName}");
            foreach (var g in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == sceneName)
                    return p;
            }
            return null;
        }
        #endif

    public void OnClick_NextLevel()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int next = gm.CurrentLevel + 1;
        bool exists   = next <= Mathf.Max(1, gm.TotalLevels);
        bool unlocked = next <= Mathf.Max(1, gm.highestUnlockedLevel);

        if (exists && unlocked)
        {
            gm.SetCurrentLevel(next);
            StartCoroutine(RestartGameplaySceneKeepUI());
        }
        // else: locked → button is already disabled
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator CaptureWorldOffscreenAndKeepPopup()
    {
        // 팝업은 그대로 둔 채, 월드만 렌더텍스처로 캡처
        yield return new WaitForEndOfFrame(); // timeScale=0에서도 OK

        var cam = FindWorldCamera();
        if (cam == null)
        {
            Debug.LogWarning("[ClearPopup] No world camera found to capture.");
            yield break;
        }

        int width  = Screen.width;
        int height = Screen.height;

        // UI 레이어 제외(Overlay는 어차피 안 찍히지만, Camera 모드 캔버스 대비용)
        int uiLayer = LayerMask.NameToLayer("UI");
        int uiMask  = uiLayer >= 0 ? (1 << uiLayer) : 0;
        int oldMask = cam.cullingMask;
        var oldTarget = cam.targetTexture;

        RenderTexture rt = null;
        Texture2D tex = null;

        try
        {
            cam.cullingMask = oldMask & ~uiMask;

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            cam.targetTexture = rt;
            cam.Render(); // 월드만 RT에 렌더

            RenderTexture.active = rt;
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            string dir = Application.persistentDataPath;
            string path = System.IO.Path.Combine(dir, $"StickIt_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[ClearPopup] Screenshot saved: {path}");
        }
        finally
        {
            cam.cullingMask   = oldMask;
            cam.targetTexture = oldTarget;
            RenderTexture.active = null;
            if (rt)  rt.Release();
            if (rt)  Destroy(rt);
            if (tex) Destroy(tex);
        }

        // 안전망: 혹시 팝업이 닫혔다면 강제로 다시 열기
        if (!isShowing)
        {
            isShowing = false;   // guard 우회
            Show();
        }
        else
        {
            // 보이는 상태라면 인터랙션만 확실히 살려둠
            if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }

    // 월드 카메라 찾기(메인 우선, 없으면 UI만 안 그리는 첫 카메라)
    private Camera FindWorldCamera()
    {
        if (Camera.main != null) return Camera.main;

        var cams = Camera.allCameras;
        int uiLayer = LayerMask.NameToLayer("UI");
        int uiMask  = uiLayer >= 0 ? (1 << uiLayer) : 0;

        foreach (var c in cams)
        {
            if (!c || !c.enabled) continue;
            // UI만 그리는 카메라가 아니면 월드 카메라로 간주
            if (uiMask == 0 || (c.cullingMask & ~uiMask) != 0)
                return c;
        }
        return null;
    }


    /// <summary>
    /// Reload gameplay scene(s) while keeping UI scene alive.
    /// - Unloads ALL scenes that contain a LevelManager (handles duplicates)
    /// - Reloads one of them additively (Editor supports non-build scenes)
    /// </summary>
    private IEnumerator RestartGameplaySceneKeepUI()
    {
        Time.timeScale = 1f;

        // 1) 현재 로드된 모든 씬 중에서 LevelManager가 "실제로 존재"하는 씬만 수집
        var gameplayScenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.isLoaded) continue;

            var roots = s.GetRootGameObjects();
            bool hasLM = false;
            foreach (var r in roots)
            {
                if (!r) continue;
                if (r.GetComponentInChildren<LevelManager>(true) != null) { hasLM = true; break; }
            }
            if (hasLM) gameplayScenes.Add(s);
        }

        // 2) 가드: 못 찾았다면(아주 드문 케이스) 활성 씬을 Single로 리로드
        if (gameplayScenes.Count == 0)
        {
            var active = SceneManager.GetActiveScene();
            Debug.LogWarning("[ClearPopup] No LevelManager scenes found. Reloading active scene (Single) as fallback.");
            yield return SceneManager.LoadSceneAsync(active.name, LoadSceneMode.Single);
            yield break;
        }

        // 3) 다시 로드할 타겟 결정(가능하면 활성 씬, 아니면 목록 첫 번째)
        var activeScene = SceneManager.GetActiveScene();
        Scene target = gameplayScenes.Contains(activeScene) ? activeScene : gameplayScenes[0];

        string reloadName = target.name;
        string reloadPath = target.path;
    #if UNITY_EDITOR
        bool inBuild = SceneUtility.GetBuildIndexByScenePath(reloadPath) >= 0;
    #endif

        // 4) 게임플레이 씬들만 언로드(UI 씬은 남김)
        foreach (var sc in gameplayScenes)
            yield return SceneManager.UnloadSceneAsync(sc);

        // 5) 게임플레이 씬 하나만 애드티브 재로딩
    #if UNITY_EDITOR
        if (!inBuild && !string.IsNullOrEmpty(reloadPath))
        {
            var pars = new LoadSceneParameters(LoadSceneMode.Additive);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(reloadPath, pars);
        }
        else
        {
            yield return SceneManager.LoadSceneAsync(reloadName, LoadSceneMode.Additive);
        }
    #else
        yield return SceneManager.LoadSceneAsync(reloadName, LoadSceneMode.Additive);
    #endif

        // 6) 활성 씬 지정 + 팝업 닫기
        var reloaded = SceneManager.GetSceneByName(reloadName);
        if (reloaded.IsValid()) SceneManager.SetActiveScene(reloaded);
        Hide();
    }
}
