using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.IO;

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
    [Tooltip("Scene name for the Level Select screen (single load).")]
    public string levelSelectScene = "LevelSelect";

    [Header("Pause")]
    [SerializeField] private bool pauseOnShow = true;

    private Coroutine fxCo;
    private bool isShowing;

    private void Reset() { canvasGroup = GetComponent<CanvasGroup>(); }

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

    public void Show()
    {
        if (isShowing) return;
        isShowing = true;

        if (pauseOnShow) Time.timeScale = 0f;

        RefreshButtons(); // ← NextLevel 상태 갱신
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
    // UI State (NextLevel 잠금/활성)
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
    // Buttons
    // ─────────────────────────────────────────────────────────────────────────
    public void OnClick_SaveScreenshot() { StartCoroutine(CaptureWorldWithoutUI()); }

    public void OnClick_RestartLevel()   { StartCoroutine(RestartGameplaySceneKeepUI()); }

    public void OnClick_LevelSelect()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(levelSelectScene))
            SceneManager.LoadScene(levelSelectScene, LoadSceneMode.Single);
        else
            Debug.LogWarning("[ClearPopup] LevelSelect scene name is empty.");
    }

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
        else
        {
            // Locked: 버튼이 이미 비활성화되어 있음. (원하면 사운드/토스트 추가)
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator CaptureWorldWithoutUI()
    {
        // Disable all canvases (works while paused)
        var canvases = GameObject.FindObjectsOfType<Canvas>(true);
        var prev = new bool[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            prev[i] = canvases[i].enabled;
            canvases[i].enabled = false;
        }

        yield return new WaitForEndOfFrame();

        string dir = Application.persistentDataPath;
        string path = Path.Combine(dir, $"StickIt_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[ClearPopup] Screenshot saved: {path}");

        yield return new WaitForSecondsRealtime(0.2f);

        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null) canvases[i].enabled = prev[i];
    }

    private IEnumerator RestartGameplaySceneKeepUI()
    {
        Time.timeScale = 1f;

        // Find gameplay scene via LevelManager
        var lm = FindObjectOfType<LevelManager>();
        if (lm == null)
        {
            // Fallback: reload active scene single
            var active = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(active.name, LoadSceneMode.Single);
            yield break;
        }

        var gameplayScene = lm.gameObject.scene;

        // Reload gameplay scene additively so UI stays
        yield return SceneManager.UnloadSceneAsync(gameplayScene);
        yield return SceneManager.LoadSceneAsync(gameplayScene.name, LoadSceneMode.Additive);

        var reloaded = SceneManager.GetSceneByName(gameplayScene.name);
        if (reloaded.IsValid()) SceneManager.SetActiveScene(reloaded);

        Hide();
    }
}
