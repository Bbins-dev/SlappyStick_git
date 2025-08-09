// PreviewRuntimeIsolator.cs
using UnityEngine;

[DefaultExecutionOrder(-1000)] // LevelManager보다 먼저 실행되게
public class PreviewRuntimeIsolator : MonoBehaviour
{
    [Tooltip("Optional explicit configurator. If null, the first in scene is used.")]
    public LevelConfigurator configurator;

    [Tooltip("Legacy preview roots to disable at play")]
    public string[] legacyRootNames = {
        "__Preview_Stick", "__Preview_Targets", "__Preview_Obstacles", "__Preview_Fulcrums"
    };

    void Awake()
    {
        if (Application.isPlaying)
            IsolatePreviews(true);   // ▶ 플레이 시작 시 프리뷰 비활성화
    }

    public void IsolatePreviews(bool on)
    {
        var lc = configurator != null ? configurator : FindObjectOfType<LevelConfigurator>();
        if (lc == null) return;

        // 새 그룹 컨테이너
        var container = lc.transform.Find("__PreviewContainer");
        if (container) container.gameObject.SetActive(!on ? true : false);

        // 레거시 루트도 함께
        foreach (var n in legacyRootNames)
        {
            var t = lc.transform.Find(n);
            if (t) t.gameObject.SetActive(!on ? true : false);
        }
    }
}
