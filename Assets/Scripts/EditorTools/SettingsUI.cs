// SettingsUI.cs (drop-in replacement)
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class SettingsUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Assign SettingsPanelController on SettingsPanel GameObject")]
    public SettingsPanelController panel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button levelSelectButton;
    public Button mainMenuButton;
    public Button restartButton;

    [Header("Scene Names")]
    public string levelSelectSceneName = "LevelSelectScene";
    public string menuSceneName = "MenuScene";
    public string playSceneName = "PlayScene";

#if ENABLE_INPUT_SYSTEM
    private InputAction _cancelAction; // new input system cancel (ESC/Back)
#endif

    private void Awake()
    {
        if (!panel) panel = FindObjectOfType<SettingsPanelController>(true);

        if (resumeButton)      resumeButton.onClick.AddListener(OnResume);
        if (levelSelectButton) levelSelectButton.onClick.AddListener(OnGoLevelSelect);
        if (mainMenuButton)    mainMenuButton.onClick.AddListener(OnGoMainMenu);
        if (restartButton)     restartButton.onClick.AddListener(OnRestart);

        // Start hidden via controller (CanvasGroup only)
        if (panel) panel.Close();

        // IMPORTANT: ensure the SettingsPanel GameObject stays active (we don't SetActive(false))
        if (panel && !panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        // Hook EventSystem cancel action (works in Input System only projects)
        var es = EventSystem.current;
        var ui = es ? es.GetComponent<InputSystemUIInputModule>() : null;
        if (ui != null && ui.cancel != null)
        {
            _cancelAction = ui.cancel.action;
            if (_cancelAction != null)
                _cancelAction.performed += OnCancelPerformed;
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_cancelAction != null)
        {
            _cancelAction.performed -= OnCancelPerformed;
            _cancelAction = null;
        }
#endif
    }

    private void Update()
    {
        // Old Input System / Both: read ESC directly
        // (Works even when Time.timeScale == 0)
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePanel();
    }

#if ENABLE_INPUT_SYSTEM
    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        // New Input System: UI "Cancel" (ESC / gamepad B) triggers here
        TogglePanel();
    }
#endif

    // Called by HUD SettingsButton OnClick, or ESC above
    public void TogglePanel()
    {
        if (!panel) return;
        panel.Toggle(); // single source of truth
    }

    public void OnResume()
    {
        if (panel) panel.Close();
        UIRoot.Instance?.EnsureHUDVisible();
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
        SceneManager.LoadScene(playSceneName, LoadSceneMode.Single);
    }
}
