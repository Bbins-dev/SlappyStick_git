// SettingsUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("Panel Root")]
    public GameObject panelRoot;

    [Header("Buttons")]
    public Button resumeButton;
    public Button levelSelectButton;
    public Button mainMenuButton;
    public Button restartButton;

    [Header("Scene Names")]
    public string levelSelectSceneName = "LevelSelectScene";
    public string menuSceneName = "MenuScene";
    public string playSceneName = "PlayScene";

    bool isOpen;

    void Awake()
    {
        // Wire up buttons
        if (resumeButton)      resumeButton.onClick.AddListener(OnResume);
        if (levelSelectButton) levelSelectButton.onClick.AddListener(OnGoLevelSelect);
        if (mainMenuButton)    mainMenuButton.onClick.AddListener(OnGoMainMenu);
        if (restartButton)     restartButton.onClick.AddListener(OnRestart);

        CloseImmediate();
    }

    void Update()
    {
        // Toggle by ESC / Back
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) OnResume();
            else Open();
        }
    }

    public void Open()
    {
        isOpen = true;
        if (panelRoot) panelRoot.SetActive(true);
        Time.timeScale = 0f; // pause
        // Optionally lock input / audio, etc.
    }

    public void CloseImmediate()
    {
        isOpen = false;
        if (panelRoot) panelRoot.SetActive(false);
        Time.timeScale = 1f; // ensure not stuck when changing scenes
    }

    public void OnResume()
    {
        CloseImmediate();
        if (UIRoot.Instance != null)
        UIRoot.Instance.EnsureHUDVisible();
    }

    public void OnGoLevelSelect()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(levelSelectSceneName, LoadSceneMode.Single);
    }

    public void OnGoMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    public void OnRestart()
    {
        Time.timeScale = 1f;
        // Reload current play scene (LevelManager will rebuild from GameManager.CurrentLevel)
        SceneManager.LoadScene(playSceneName, LoadSceneMode.Single);
        // Alternatively: SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
