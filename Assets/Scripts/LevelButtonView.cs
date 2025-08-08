// LevelButtonView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelButtonView : MonoBehaviour
{
    public Button button;
    public TMP_Text label;
    public GameObject lockOverlay;

    public void Setup(int levelIndex, bool unlocked)
    {
        if (label != null) label.text = levelIndex.ToString();
        if (button != null) button.interactable = unlocked;
        if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
    }
}
