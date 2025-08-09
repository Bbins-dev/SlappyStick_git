// PlayBootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayBootstrap : MonoBehaviour
{
    [Tooltip("UI-only scene name (must be in Build Settings)")]
    public string uiSceneName = "PlayUIOnly";

    void Start()
    {
        if (!IsLoaded(uiSceneName))
            SceneManager.LoadSceneAsync(uiSceneName, LoadSceneMode.Additive);
    }

    void OnDestroy()
    {
        if (IsLoaded(uiSceneName))
            SceneManager.UnloadSceneAsync(uiSceneName);
    }

    bool IsLoaded(string n)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == n) return true;
        return false;
    }
}
