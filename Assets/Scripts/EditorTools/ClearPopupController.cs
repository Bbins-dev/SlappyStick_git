using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Reflection;

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
    [SerializeField] private Button btnPlayReplay;
    [SerializeField] private Button btnSaveReplay;

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
    private int levelAtPopup = 0;


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
        btnRestart ??= transform.Find("Btn_Restart")?.GetComponent<Button>();
        btnLevelSelect ??= transform.Find("Btn_LevelSelect")?.GetComponent<Button>();
        btnNextLevel ??= transform.Find("Btn_NextLevel")?.GetComponent<Button>();
        if (btnNextLevel && !nextLevelLabel)
            nextLevelLabel = btnNextLevel.GetComponentInChildren<TextMeshProUGUI>(true);

        if (btnSaveScreenshot) { btnSaveScreenshot.onClick.RemoveAllListeners(); btnSaveScreenshot.onClick.AddListener(OnClick_SaveScreenshot); }
        if (btnRestart) { btnRestart.onClick.RemoveAllListeners(); btnRestart.onClick.AddListener(OnClick_RestartLevel); }
        if (btnLevelSelect) { btnLevelSelect.onClick.RemoveAllListeners(); btnLevelSelect.onClick.AddListener(OnClick_LevelSelect); }
        if (btnNextLevel) { btnNextLevel.onClick.RemoveAllListeners(); btnNextLevel.onClick.AddListener(OnClick_NextLevel); }
        if (btnPlayReplay) { btnPlayReplay.onClick.RemoveAllListeners(); btnPlayReplay.onClick.AddListener(OnClick_PlayReplay); }
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

        var gm = GameManager.Instance;
        levelAtPopup = gm != null ? gm.CurrentLevel : 1;

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
        float endA = visible ? 1f : 0f;

        Vector3 startS = transform.localScale;
        Vector3 endS = Vector3.one * (visible ? showScale : hideScale);

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

        canvasGroup.interactable = visible;
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
            bool exists = next <= Mathf.Max(1, gm.TotalLevels);
            bool unlocked = next <= Mathf.Max(1, gm.highestUnlockedLevel);
            canNext = exists && unlocked;
        }

        if (btnNextLevel) btnNextLevel.interactable = canNext;
        if (nextLevelLabel) nextLevelLabel.text = canNext ? "Next Level" : "Next Level (Locked)";

        // ★ 리플레이 캐시 유무로 PlayReplay 버튼 상태 갱신 (메이킹 씬에서는 특별 처리)
        bool hasReplay = ReplayManager.Instance && ReplayManager.Instance.HasCache;
        bool isMakingScene = IsMakingScene();
        
        if (btnPlayReplay) 
        {
            if (isMakingScene)
            {
                // 메이킹 씬에서는 리플레이 상태와 관계없이 활성화 (테스트 목적)
                btnPlayReplay.interactable = true;
                Debug.Log($"[ClearPopup] Making 씬에서 PlayReplay 버튼 강제 활성화 (hasReplay: {hasReplay})");
            }
            else
            {
                btnPlayReplay.interactable = hasReplay;
            }
        }

        // Making 씬에서는 LevelSelect와 NextLevel 버튼 비활성화
        if (btnLevelSelect)
        {
            btnLevelSelect.interactable = !isMakingScene;
            if (isMakingScene)
            {
                Debug.Log("[ClearPopup] Making 씬에서 LevelSelect 버튼 비활성화");
            }
        }
        if (btnNextLevel && isMakingScene)
        {
            btnNextLevel.interactable = false;
            Debug.Log("[ClearPopup] Making 씬에서 NextLevel 버튼 비활성화");
        }
    }

    /// <summary>
    /// 현재 씬이 Making 씬인지 확인
    /// </summary>
    private bool IsMakingScene()
    {
        // 1. MakingSceneBootstrap 컴포넌트가 있으면 Making 씬
        if (FindObjectOfType<MakingSceneBootstrap>() != null)
            return true;

        // 2. 씬 이름으로 확인
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return activeScene.name.Contains("Making") || activeScene.name.Contains("making");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button handlers
    // ─────────────────────────────────────────────────────────────────────────
    public void OnClick_SaveScreenshot()
    {
        StartCoroutine(CaptureWorldOffscreenAndKeepPopup());
    }

    public void OnClick_RestartLevel()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.PinLevelForNextReload(gm.CurrentLevel);

        ReplayManager.Instance?.TryDeleteCache(); // ★ 리플레이 캐시 있을 시 제거
        StartCoroutine(RestartGameplaySceneKeepUI());
    }

    public void OnClick_LevelSelect()
    {
        ReplayManager.Instance?.TryDeleteCache(); // ★ 리플레이 캐시 있을 시 제거

        // 팝업에서 나갈 때는 일시정지 해제
        Time.timeScale = 1f;

        // 1) 현재 프로젝트에서 이미 정상 동작하는 SettingsUI 경로 재사용
        var settings = FindObjectOfType<SettingsUI>(true);
        if (settings != null)
        {
            settings.OnGoLevelSelect(); // 내부에서 SceneManager.LoadScene(...) 호출
            return;
        }

        // 2) SettingsUI가 없으면, 필드 값으로 직접 로드 (빌드에서도 동작)
        if (!string.IsNullOrEmpty(levelSelectScene))
        {
#if UNITY_EDITOR
            // 에디터에서 빌드목록에 없을 때도 경로로 로드 (편의)
            if (!IsSceneInBuild(levelSelectScene))
            {
                string path = FindScenePathByName(levelSelectScene);
                if (!string.IsNullOrEmpty(path))
                {
                    var p = new UnityEngine.SceneManagement.LoadSceneParameters(LoadSceneMode.Single);
                    UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, p);
                    return;
                }
            }
#endif
            UnityEngine.SceneManagement.SceneManager.LoadScene(levelSelectScene, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[ClearPopup] LevelSelect scene name is empty.");
        }
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

        // ✅ “클리어 당시 레벨” 우선, 없으면 팝업이 떴을 때 스냅샷, 그것도 없으면 현재값
        int baseLevel =
            (gm.LastClearedLevel > 0) ? gm.LastClearedLevel :
            (levelAtPopup > 0 ? levelAtPopup : gm.CurrentLevel);

        int next = baseLevel + 1;
        bool exists   = next <= Mathf.Max(1, gm.TotalLevels);
        bool unlocked = next <= Mathf.Max(1, gm.highestUnlockedLevel);

        if (exists && unlocked)
        {
            // 🔒 리로드 사이에 다른 스크립트가 값을 건드려도 확정되도록 핀으로 고정
            gm.PinLevelForNextReload(next);
            StartCoroutine(RestartGameplaySceneKeepUI());
        }
    }


    public void OnClick_PlayReplay()
    {
        // ★ 리플레이 캐시가 없으면 경고 후 종료 (메이킹 씬에서는 예외)
        bool isMakingScene = IsMakingScene();
        bool hasCache = ReplayManager.Instance && ReplayManager.Instance.HasCache;
        
        if (!hasCache)
        {
            if (isMakingScene)
            {
                Debug.LogWarning("[Replay] Making 씬에서 캐시가 없음 - 빈 리플레이 재생 시도");
                // 메이킹 씬에서는 캐시가 없어도 계속 진행 (테스트/디버그 목적)
            }
            else
            {
                Debug.LogWarning("[Replay] No cached replay to play.");
                return;
            }
        }

        // 팝업을 잠깐 닫아 시야/입력/타임스케일 정상화
        Hide(); // pauseOnShow=true면 여기서 Time.timeScale=1로 복귀

        var player = FindObjectOfType<ReplayPlayer>(true);
        if (player == null)
        {
            var go = new GameObject("ReplayPlayer");
            player = go.AddComponent<ReplayPlayer>();
            DontDestroyOnLoad(go);
        }

        // 재생 끝나면 팝업 다시 열어서 일시정지 복원 + 버튼들 접근 가능하게
        player.PlayCached(() =>
        {
            Show(); // 다시 Pause 상태로 팝업 복귀
        });
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

        int width = Screen.width;
        int height = Screen.height;

        // UI 레이어 제외(Overlay는 어차피 안 찍히지만, Camera 모드 캔버스 대비용)
        int uiLayer = LayerMask.NameToLayer("UI");
        int uiMask = uiLayer >= 0 ? (1 << uiLayer) : 0;
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
            cam.cullingMask = oldMask;
            cam.targetTexture = oldTarget;
            RenderTexture.active = null;
            if (rt) rt.Release();
            if (rt) Destroy(rt);
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
        int uiMask = uiLayer >= 0 ? (1 << uiLayer) : 0;

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
    // ClearPopupController.cs 안에 추가/교체
    private IEnumerator RestartGameplaySceneKeepUI()
    {
        Time.timeScale = 1f;

        // 1) 임시 홀드 카메라 생성 → 카메라 공백 프레임 방지
        var tempCam = SpawnTempHoldCamera();

        // 2) 재시작할 "게임플레이 씬"을 확실하게 잡는다 (LevelManager가 들어있는 씬)
        Scene gameplayScene = default;
        var lm = FindObjectOfType<LevelManager>();
        if (lm != null)
            gameplayScene = lm.gameObject.scene;

        bool hasGameplay = gameplayScene.IsValid() && gameplayScene.isLoaded;

        // 폴백(거의 안 탐): 기존 스캔 로직
        if (!hasGameplay)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || !s.isLoaded) continue;
                foreach (var r in s.GetRootGameObjects())
                {
                    if (r && r.GetComponentInChildren<LevelManager>(true) != null)
                    {
                        gameplayScene = s;
                        hasGameplay = true;
                        break;
                    }
                }
                if (hasGameplay) break;
            }
        }

        // 그래도 못 찾으면: 활성 씬을 Single로 재로드(최후) — 하지만 임시 카메라가 있으니 깜빡임 없음
        if (!hasGameplay)
        {
            var active = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(active.name, LoadSceneMode.Single);
            CleanupTempHoldCamera(tempCam);
            yield break;
        }

        string reloadName = gameplayScene.name;
    #if UNITY_EDITOR
        string reloadPath = gameplayScene.path;
        bool inBuild = SceneUtility.GetBuildIndexByScenePath(reloadPath) >= 0;
    #endif

        // 3) 해당 게임플레이 씬만 언로드 (UI 씬은 남겨둠)
        yield return SceneManager.UnloadSceneAsync(gameplayScene);

        // 4) 게임플레이 씬 재로드 (애드티브)
    #if UNITY_EDITOR
        if (!inBuild && !string.IsNullOrEmpty(reloadPath))
        {
            var pars = new LoadSceneParameters(LoadSceneMode.Additive);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(reloadPath, pars);
        }
        else
        {
            yield return SceneManager.LoadSceneAsync(reloadName, LoadSceneMode.Additive);
        }
    #else
        yield return SceneManager.LoadSceneAsync(reloadName, LoadSceneMode.Additive);
    #endif

        // 5) 활성 씬 지정
        var reloaded = SceneManager.GetSceneByName(reloadName);
        if (reloaded.IsValid()) SceneManager.SetActiveScene(reloaded);

        // 6) 메인카메라가 살아났는지 한두 프레임 기다렸다가 임시 카메라 제거
        for (int i = 0; i < 3; i++)
        {
            if (Camera.main != null && Camera.main.enabled) break;
            yield return null;
        }
        CleanupTempHoldCamera(tempCam);

        // 7) 팝업 닫기
        Hide();
    }

    // ----- 임시 카메라 유틸 -----
    private Camera SpawnTempHoldCamera()
    {
        var go = new GameObject("~TempHoldCamera");
        DontDestroyOnLoad(go);

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;        // 아무 것도 렌더링하지 않지만, '카메라 있음' 상태가 되어 경고/깜빡임 방지
        cam.depth = -10000;
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cam.targetDisplay = 0;      // Display 1
        go.tag = "Untagged";
        go.hideFlags = HideFlags.DontSave;
        return cam;
    }

    private void CleanupTempHoldCamera(Camera c)
    {
        if (c != null && c.gameObject != null)
            Destroy(c.gameObject);
    }
}
