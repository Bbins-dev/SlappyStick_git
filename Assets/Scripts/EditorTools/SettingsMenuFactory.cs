// Assets/Editor/SettingsMenuFactory.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public static class SettingsMenuFactory
{
    [MenuItem("StickIt/Rebuild In-Game Settings Menu")]
    public static void Rebuild()
    {
        // 0) Canvas 확보 (Screen Space - Overlay)
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // 1) EventSystem 보장
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 2) 기존 SettingsPanel 제거(있다면)
        var old = GameObject.Find("SettingsPanel");
        if (old) Undo.DestroyObjectImmediate(old);

        // 3) SettingsPanel 생성
        var panel = CreateUIObject("SettingsPanel", canvas.transform);
        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        // 3-1) Dim
        var dim = CreateUIObject("Dim", panel.transform);
        var dimRT = dim.GetComponent<RectTransform>();
        StretchFull(dimRT);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.65f);
        dimImg.raycastTarget = true;

        // 3-2) Window
        var window = CreateUIObject("Window", panel.transform);
        var winRT = window.GetComponent<RectTransform>();
        CenterBox(winRT, new Vector2(600, 720));
        var winImg = window.AddComponent<Image>();
        winImg.color = new Color(1f, 1f, 1f, 0.2f);

        // Title
        var title = CreateUIObject("Title", window.transform);
        var titleTMP = title.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "Menu";
        titleTMP.alignment = TextAlignmentOptions.TopLeft;
        titleTMP.fontSize = 64f;
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(0, 1);
        titleRT.pivot = new Vector2(0, 1);
        titleRT.anchoredPosition = new Vector2(32, -32);
        titleRT.sizeDelta = new Vector2(400, 120);

        // Buttoncolumn
        var column = CreateUIObject("Buttoncolumn", window.transform);
        var colRT = column.GetComponent<RectTransform>();
        colRT.anchorMin = new Vector2(0, 0);
        colRT.anchorMax = new Vector2(1, 1);
        colRT.offsetMin = new Vector2(32, 32 + 120); // 아래 + 제목 높이만큼 띄움
        colRT.offsetMax = new Vector2(-32, -32);

        var vlg = column.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 16f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 버튼 팩토리
        CreateMenuButton(column.transform, "ResumeButton", "Resume");
        CreateMenuButton(column.transform, "LevelSelectButton", "Level Select");
        CreateMenuButton(column.transform, "MainMenuButton", "Main Menu");
        CreateMenuButton(column.transform, "RestartButton", "Restart");

        // 4) SettingsPanelController 붙이고 참조 세팅
        var controller = panel.AddComponent<SettingsPanelController>();
        controller.pauseOnShow = true;

        controller.GetType().GetField("dim", 
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)
            ?.SetValue(controller, dim.GetComponent<RectTransform>());
        controller.GetType().GetField("window", 
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)
            ?.SetValue(controller, winRT);
        controller.GetType().GetField("canvasGroup", 
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)
            ?.SetValue(controller, cg);

        // 5) 상단 오른쪽 “Menu” 버튼(토글)
        var topBtn = CreateUIObject("SettingsButton", canvas.transform);
        var tbrt = topBtn.GetComponent<RectTransform>();
        tbrt.anchorMin = new Vector2(1, 1);
        tbrt.anchorMax = new Vector2(1, 1);
        tbrt.pivot = new Vector2(1, 1);
        tbrt.anchoredPosition = new Vector2(-24, -24);
        tbrt.sizeDelta = new Vector2(160, 56);

        var tbImg = topBtn.AddComponent<Image>(); tbImg.color = Color.white;
        var tbBtn = topBtn.AddComponent<Button>();
        tbBtn.targetGraphic = tbImg;

        var tbLabel = CreateUIObject("Text", topBtn.transform).AddComponent<TextMeshProUGUI>();
        tbLabel.text = "Menu";
        tbLabel.alignment = TextAlignmentOptions.Center;
        tbLabel.enableAutoSizing = true;
        tbLabel.rectTransform.anchorMin = Vector2.zero;
        tbLabel.rectTransform.anchorMax = Vector2.one;
        tbLabel.rectTransform.offsetMin = Vector2.zero;
        tbLabel.rectTransform.offsetMax = Vector2.zero;
        tbLabel.raycastTarget = false;

        // OnClick: SettingsPanelController.Toggle
        var method = new UnityEngine.Events.UnityAction(controller.Toggle);
        tbBtn.onClick.AddListener(method);

        // 6) 형제 순서: Dim이 0, Window가 1
        dim.transform.SetSiblingIndex(0);
        window.transform.SetSiblingIndex(1);

        Selection.activeGameObject = panel;
        Debug.Log("[SettingsMenuFactory] Rebuilt SettingsPanel under Canvas.");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CenterBox(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    private static void CreateMenuButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.sizeDelta = new Vector2(0, 0);

        var img = go.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 110f;

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.raycastTarget = false;
    }
}
