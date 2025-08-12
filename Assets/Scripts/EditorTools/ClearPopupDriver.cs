// ClearPopupDriver.cs (교체)
using UnityEngine;
using System.Collections;

[AddComponentMenu("StickIt/ClearPopup Driver")]
public class ClearPopupDriver : MonoBehaviour
{
    [Tooltip("Delay before showing clear popup (seconds)")]
    public float popupDelay = 1.0f;

    private bool subscribed;

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private IEnumerator BindWhenReady()
    {
        // ✅ GameManager가 준비될 때까지 대기
        while (GameManager.Instance == null) yield return null;

        if (!subscribed)
        {
            GameManager.Instance.onStageCleared.AddListener(OnStageCleared);
            subscribed = true;
            Debug.Log("[ClearPopupDriver] Subscribed to GameManager.onStageCleared");
        }
    }

    private void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.onStageCleared.RemoveListener(OnStageCleared);
            subscribed = false;
            Debug.Log("[ClearPopupDriver] Unsubscribed");
        }
    }

    private void OnStageCleared()
    {
        Debug.Log("[ClearPopupDriver] onStageCleared received → will show popup");
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        // ✅ 시간 정지와 무관하게 동작
        yield return new WaitForSecondsRealtime(popupDelay);

        // ✅ UIRoot 경유가 있으면 우선
        if (UIRoot.Instance != null && UIRoot.Instance.clearPopup != null)
        {
            UIRoot.Instance.ShowClearPopup();
            Debug.Log("[ClearPopupDriver] Show via UIRoot");
        }
        else
        {
            // 폴백: 씬에서 직접 찾기
            var popup = FindObjectOfType<ClearPopupController>(true);
            if (popup != null)
            {
                popup.Show();
                Debug.Log("[ClearPopupDriver] Show via direct FindObjectOfType");
            }
            else
            {
                Debug.LogWarning("[ClearPopupDriver] ClearPopupController not found in scene.");
            }
        }
    }
}
