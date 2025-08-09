using UnityEngine;
using TMPro;

public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("References")]
    public GameObject hudRoot;        // ← HUD 오브젝트 드래그
    public TMP_Text holdTimeText;     // ← HoldTimeText 드래그

    void Awake() { Instance = this; }

    // 안전: 이름으로도 한 번 더 찾아보기(드래그 빠졌을 때 대비)
    void OnEnable()
    {
        if (holdTimeText == null)
            holdTimeText = transform.Find("HUD/HoldTimeText")?.GetComponent<TMP_Text>();
        if (hudRoot == null)
            hudRoot = transform.Find("HUD")?.gameObject;
    }

    public void EnsureHUDVisible()
    {
        if (hudRoot && !hudRoot.activeSelf) hudRoot.SetActive(true);
        if (holdTimeText && !holdTimeText.gameObject.activeSelf) holdTimeText.gameObject.SetActive(true);
    }
}
