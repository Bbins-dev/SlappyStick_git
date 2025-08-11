using UnityEngine;
using System.Collections;

[AddComponentMenu("StickIt/ClearPopup Driver")]
public class ClearPopupDriver : MonoBehaviour
{
    [Tooltip("Delay before showing clear popup (seconds)")]
    public float popupDelay = 1.0f;

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onStageCleared.AddListener(OnStageCleared);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onStageCleared.RemoveListener(OnStageCleared);
    }

    private void OnStageCleared() => StartCoroutine(ShowAfterDelay());

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(popupDelay);
        UIRoot.Instance?.ShowClearPopup();
    }
}
