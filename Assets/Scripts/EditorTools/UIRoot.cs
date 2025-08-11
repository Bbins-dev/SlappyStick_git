using UnityEngine;
using TMPro;

public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("References")]
    public GameObject hudRoot;
    public TMP_Text holdTimeText;

    [Header("Popups")]
    public ClearPopupController clearPopup;

    private void Awake()
    {
        // singleton guard
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (holdTimeText == null)
            holdTimeText = transform.Find("HUD/HoldTimeText")?.GetComponent<TMP_Text>();
        if (hudRoot == null)
            hudRoot = transform.Find("HUD")?.gameObject;

        // lazy find for popup
        if (clearPopup == null)
            clearPopup = transform.Find("ClearPopup")?.GetComponent<ClearPopupController>();
    }

    public void EnsureHUDVisible()
    {
        if (hudRoot && !hudRoot.activeSelf) hudRoot.SetActive(true);
        if (holdTimeText && !holdTimeText.gameObject.activeSelf) holdTimeText.gameObject.SetActive(true);
    }

    public void ShowClearPopup() { if (clearPopup) clearPopup.Show(); }
    public void HideClearPopup() { if (clearPopup) clearPopup.Hide(); }
}
