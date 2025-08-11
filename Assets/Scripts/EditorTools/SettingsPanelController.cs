using UnityEngine;
using UnityEngine.UI;
using TMPro;

[AddComponentMenu("StickIt/Settings Panel Controller")]
public class SettingsPanelController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;           // SettingsPanel CanvasGroup
    [SerializeField] private RectTransform dim;                 // SettingsPanel/Dim
    [SerializeField] private RectTransform window;              // SettingsPanel/Window
    [SerializeField] private Selectable firstSelected;          // Optional first focus

    [Header("Behavior")]
    [SerializeField] private bool pauseOnShow = true;

    private bool isOpen;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        dim = transform.Find("Dim") as RectTransform;
        window = transform.Find("Window") as RectTransform;
    }

    private void Awake()
    {
        ApplyVisible(false, true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) Close(); else Open();
        }
    }

    public void Open()
    {
        transform.SetAsLastSibling();             // bring panel to top
        if (dim) dim.SetSiblingIndex(0);          // Dim under Window
        if (window) window.SetSiblingIndex(1);

        AutoFixButtons(window ? window.gameObject : gameObject);
        ApplyVisible(true, false);

        if (firstSelected) firstSelected.Select();
        isOpen = true;
    }

    public void Close()
    {
        ApplyVisible(false, false);
        isOpen = false;
    }

    public void Toggle() { if (isOpen) Close(); else Open(); }

    private void ApplyVisible(bool visible, bool instant)
    {
        if (!canvasGroup) canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        if (pauseOnShow) Time.timeScale = visible ? 0f : 1f;
    }

    private void AutoFixButtons(GameObject root)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            // ensure Image as targetGraphic
            var img = btn.targetGraphic as Image ?? btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            btn.targetGraphic = img;
            if (btn.transition == Selectable.Transition.None)
                btn.transition = Selectable.Transition.ColorTint;

            // label should not intercept raycasts
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label) label.raycastTarget = false;

            // ensure height via LayoutElement (if layout drives children)
            var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            if (le.preferredHeight < 1f) le.preferredHeight = 120f;
        }
    }
}
