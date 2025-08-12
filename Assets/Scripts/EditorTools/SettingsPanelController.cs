using UnityEngine;
using UnityEngine.UI;
using TMPro;

[AddComponentMenu("StickIt/Settings Panel Controller")]
public class SettingsPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform dim;
    [SerializeField] private RectTransform window;

    [Header("Behavior")]
    public bool pauseOnShow = true;

    private bool isOpen;

    private void Awake()
    {
        ApplyVisible(false, true);
    }

    private void Update()
    {
    
    }

    public void Toggle() { if (isOpen) Close(); else Open(); }

    public void Open()
    {
        transform.SetAsLastSibling();
        if (dim) dim.SetSiblingIndex(0);
        if (window) window.SetSiblingIndex(1);
        ApplyVisible(true, false);
        isOpen = true;
    }

    public void Close()
    {
        ApplyVisible(false, false);
        isOpen = false;
    }

    private void ApplyVisible(bool visible, bool instant)
    {
        if (!canvasGroup) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        if (pauseOnShow) Time.timeScale = visible ? 0f : 1f;
    }
}
