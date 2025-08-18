// UIOverlayKeeper.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-950)] // runs early (after your -1000 binders)
public class UIOverlayKeeper : MonoBehaviour
{
    private Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        Apply();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Apply();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => Apply();
    void OnActiveSceneChanged(Scene a, Scene b)   => Apply();

    private void Apply()
    {
        if (!_canvas) return;

        // Force Overlay no matter what changed it
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.worldCamera = null;     // overlay doesn't need a camera
        _canvas.sortingOrder = 0;       // adjust if you add more overlay canvases
        _canvas.targetDisplay = 0;      // Display 1

        // Safety: ensure the GO is enabled
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        if (!_canvas.enabled) _canvas.enabled = true;
    }
}
