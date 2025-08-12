using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;

[AddComponentMenu("StickIt/ClearPopup Controller")]
public class ClearPopupController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Show/Hide FX")]
    [SerializeField] private float tweenDuration = 0.25f;
    [SerializeField] private float showScale = 1.0f;
    [SerializeField] private float hideScale = 0.9f;

    [Header("Scenes")]
    [Tooltip("Scene name for the Level Select screen (single load).")]
    public string levelSelectScene = "LevelSelect";

    // ClearPopupController.cs 안에 추가
    [ContextMenu("Debug/Show Now")]
    private void CtxShow() => Show();

    [ContextMenu("Debug/Hide Now")]
    private void CtxHide() => Hide();

    [Header("Pause")]
    [SerializeField] private bool pauseOnShow = true; // 팝업 뜰 때 일시정지 여부

    private Coroutine fxCo;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (!canvasGroup)
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        SetVisible(false, instant:true);
    }

    public void Show()
    {
        if (pauseOnShow) Time.timeScale = 0f;   // 팝업 표시와 동시에 게임 일시정지
        SetVisible(true, instant:false);
    }
    public void Hide()
    {
        if (pauseOnShow) Time.timeScale = 1f;   // 닫을 때 게임 재개
        SetVisible(false, instant:false);
    }

    private void SetVisible(bool visible, bool instant)
    {
        if (fxCo != null) StopCoroutine(fxCo);
        fxCo = StartCoroutine(DoFx(visible, instant));
    }

    private IEnumerator DoFx(bool visible, bool instant)
    {
        float t = 0f;
        float dur = instant ? 0f : tweenDuration;

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

    // --------------------------------------------------------------------
    // Buttons
    // --------------------------------------------------------------------
    public void OnClick_SaveScreenshot()
    {
        StartCoroutine(CaptureWorldWithoutUI());
    }

    public void OnClick_RestartLevel()
    {
        StartCoroutine(RestartGameplaySceneKeepUI());
    }

    public void OnClick_LevelSelect()
    {
        // Leave to a dedicated scene (single load).
        if (!string.IsNullOrEmpty(levelSelectScene))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(levelSelectScene, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[ClearPopup] LevelSelect scene name is empty.");
        }
    }

    public void OnClick_NextLevel()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[ClearPopup] GameManager not found.");
            return;
        }

        int next = gm.CurrentLevel + 1;
        bool unlocked = next <= gm.highestUnlockedLevel;
        bool exists   = next <= Mathf.Max(1, gm.TotalLevels);

        if (unlocked && exists)
        {
            gm.SetCurrentLevel(next);
            StartCoroutine(RestartGameplaySceneKeepUI()); // reload gameplay scene; LevelManager will build next level
        }
        else
        {
            Debug.Log("[ClearPopup] Next level is locked or out of range.");
        }
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------

    /// <summary>
    /// Temporarily disables all Canvas to capture a UI-free screenshot.
    /// Saves to Application.persistentDataPath.
    /// </summary>
    private IEnumerator CaptureWorldWithoutUI()
    {
        // 1) Gather and disable all canvases
        var canvases = GameObject.FindObjectsOfType<Canvas>(true);
        var prev = new bool[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            prev[i] = canvases[i].enabled;
            canvases[i].enabled = false;
        }

        // 2) Wait end of frame to ensure UI is not rendered
        yield return new WaitForEndOfFrame();

        // 3) Capture
        string dir = Application.persistentDataPath;
        string name = $"StickIt_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(dir, name);

        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[ClearPopup] Screenshot saved: {path}");

        // 4) Give the OS a moment to flush
        yield return new WaitForSecondsRealtime(0.2f);

        // 5) Restore canvases
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null) canvases[i].enabled = prev[i];
        }
    }

    /// <summary>
    /// Reloads the gameplay scene (the one that contains LevelManager)
    /// while keeping the UI scene (this popup lives in) loaded.
    /// </summary>
    private IEnumerator RestartGameplaySceneKeepUI()
    {
        Time.timeScale = 1f;

        // Find the scene that has a LevelManager
        LevelManager lm = FindObjectOfType<LevelManager>();
        if (lm == null)
        {
            // Fallback: reload active scene single (UI will be reloaded by your bootstrap if any)
            var active = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(active.name, LoadSceneMode.Single);
            yield break;
        }

        var gameplayScene = lm.gameObject.scene;
        var uiScene = gameObject.scene; // this popup lives in UI scene

        // Additively reload only the gameplay scene so that UI stays
        yield return SceneManager.UnloadSceneAsync(gameplayScene);
        yield return SceneManager.LoadSceneAsync(gameplayScene.name, LoadSceneMode.Additive);

        // Set the newly loaded gameplay scene active (optional)
        var reloaded = SceneManager.GetSceneByName(gameplayScene.name);
        if (reloaded.IsValid()) SceneManager.SetActiveScene(reloaded);

        // Hide popup after restart
        Hide();
    }
}
