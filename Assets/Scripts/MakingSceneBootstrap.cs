// MakingSceneBootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class MakingSceneBootstrap : MonoBehaviour
{
    [Tooltip("Name of the UI-only scene to load additively (must be in Build Settings).")]
    public string uiSceneName = "PlayUIOnly";

    private void Start()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(uiSceneName))
        {
            Debug.LogError("[MakingSceneBootstrap] uiSceneName is empty.");
            return;
        }
        if (!IsSceneLoaded(uiSceneName))
            SceneManager.LoadSceneAsync(uiSceneName, LoadSceneMode.Additive);
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (IsSceneLoaded(uiSceneName))
            SceneManager.UnloadSceneAsync(uiSceneName);
#endif
    }

    private bool IsSceneLoaded(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == name) return true;
        return false;
    }
}
