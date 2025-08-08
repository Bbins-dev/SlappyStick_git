// UIRoot.cs
using UnityEngine;
using TMPro;

public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("References")]
    public TMP_Text holdTimeText;

    private void Awake()
    {
        Instance = this;
    }
}
