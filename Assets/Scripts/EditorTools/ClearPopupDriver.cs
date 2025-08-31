// ClearPopupDriver.cs (교체)
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[AddComponentMenu("StickIt/ClearPopup Driver (Robust)")]
public class ClearPopupDriver : MonoBehaviour
{
    [Tooltip("Delay before showing clear popup (seconds)")]
    public float popupDelay = 1.0f;

    [Header("Direct Reference (optional)")]
    public ClearPopupController popup; // 인스펙터에 ClearPopup 드래그 권장

    private bool subscribedUnity;
    private bool pendingShow;

    private void OnEnable()
    {
        // ✅ 정적 이벤트는 즉시 구독(Instance 필요 없음)
        GameManager.StageClearedStatic += OnStageClearedStatic;

        // ✅ UnityEvent도 있으면 같이 구독(중복 방지 로직 있음)
        if (GameManager.Instance != null && !subscribedUnity)
        {
            GameManager.Instance.onStageCleared.AddListener(OnStageClearedUnity);
            subscribedUnity = true;
        }

        // GM이 나중에 뜨는 케이스도 커버
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(EnsureUnityEventSubscription());
        EnsurePopupRef();
        Debug.Log("[ClearPopupDriver] Enabled & listening");
    }

    private void OnDisable()
    {
        GameManager.StageClearedStatic -= OnStageClearedStatic;
        if (subscribedUnity && GameManager.Instance != null)
            GameManager.Instance.onStageCleared.RemoveListener(OnStageClearedUnity);
        subscribedUnity = false;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Debug.Log("[ClearPopupDriver] Disabled");
    }

    private IEnumerator EnsureUnityEventSubscription()
    {
        // GM이 늦게 생기면 기다렸다가 UnityEvent 구독 추가
        float t = 0f, timeout = 5f;
        while (!subscribedUnity && t < timeout)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.onStageCleared.AddListener(OnStageClearedUnity);
                subscribedUnity = true;
                Debug.Log("[ClearPopupDriver] Subscribed to GameManager.onStageCleared");
                yield break;
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnStageClearedStatic() { TriggerShow("static"); }
    private void OnStageClearedUnity()  { TriggerShow("unity");  }

    private void TriggerShow(string source)
    {
        if (pendingShow) return;
        
        // ★ 리플레이 중이면 ClearPopup 표시하지 않음 (TipTrigger에서 Wobble 이펙트 후 자연스럽게 처리)
        bool isReplayMode = ReplayManager.Instance?.IsReplaying ?? false;
        if (isReplayMode)
        {
            Debug.Log($"[ClearPopupDriver] 리플레이 모드 - ClearPopup 표시 건너뜀 (source: {source})");
            return;
        }
        
        pendingShow = true;
        Debug.Log($"[ClearPopupDriver] onStageCleared received via {source} → will show");
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        // TipTrigger에서 이미 리플레이 모드 처리를 했으므로, 여기서는 일반적인 지연만 적용
        yield return new WaitForSecondsRealtime(popupDelay);

        // ★★★ 성공(클리어): 끝내고 캐시 유지 (TipTrigger에서 이미 처리되었을 수 있지만 안전하게 중복 호출)
        // ShowAfterDelay는 코루틴이므로 OnDestroy 상황이 아니어서 안전함
        ReplayManager.Instance?.EndRecording(keepFile: true);

        // 바로 있으면 표시, 아니면 끝까지 기다린다
        if (!EnsurePopupRef())
        {
            // 씬 추가 로드/활성화로 늦게 생기는 케이스 대비
            float t = 0f, timeout = 3f;
            while (!EnsurePopupRef() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (popup != null)
        {
            popup.Show();
            Debug.Log("[ClearPopupDriver] Popup.Show()");
        }
        else
        {
            Debug.LogWarning("[ClearPopupDriver] ClearPopupController not found.");
        }

        pendingShow = false;
    }

    private bool EnsurePopupRef()
    {
        if (popup != null) return true;

        // 활성/비활성 포함 탐색
        popup = FindObjectOfType<ClearPopupController>(true);
        if (popup != null) return true;

        // 에디터/특수 케이스 안전망
        var all = Resources.FindObjectsOfTypeAll<ClearPopupController>();
        if (all != null && all.Length > 0) { popup = all[0]; return true; }

        return false;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // 씬 로드 후 팝업을 다시 시도
        if (popup == null) EnsurePopupRef();

        // 클리어 직후 UI 씬이 늦게 붙는 케이스: pending이면 즉시 표시
        if (pendingShow && popup != null)
        {
            popup.Show();
            Debug.Log("[ClearPopupDriver] Popup.Show() on sceneLoaded");
            pendingShow = false;
        }
    }
}
