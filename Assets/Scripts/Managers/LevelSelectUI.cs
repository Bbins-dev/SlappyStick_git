// LevelSelectUI.cs (핵심만 교체)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelSelectUI : MonoBehaviour
{
    public Transform content;
    public GameObject levelButtonPrefab;
    public Button backToMenuButton;

    private void Awake()
    {
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
    }

    private void OnEnable()
    {
        ClearGrid();
        BuildGrid();
    }

    private void BuildGrid()
    {
        if (!content || !levelButtonPrefab)
        {
            Debug.LogError("[LevelSelectUI] Missing content or prefab.");
            return;
        }

        var gm = GameManager.Instance;
        var db = gm != null ? gm.Database : null;
        if (db == null || db.levels == null)
        {
            Debug.LogError("[LevelSelectUI] Database is null.");
            return;
        }

        int display = 1; // 화면에 표시할 번호(1,2,3…)
        for (int dbIndex = 0; dbIndex < db.levels.Length; dbIndex++)
        {
            var data = db.levels[dbIndex];
            if (data == null) continue; // null 슬롯은 건너뛰기

            var go = Instantiate(levelButtonPrefab, content);
            go.name = $"LevelButton_{display}";

            // Label 설정 (TMP 우선, 없으면 Legacy Text)
            var view = go.GetComponent<LevelButtonView>();
            var btn  = go.GetComponent<Button>();
            TMP_Text tmp = null;
            Text legacy = null;

            if (view != null)
            {
                // view가 있으면 그걸 사용
                view.Setup(display, /*unlocked*/ (gm.highestUnlockedLevel >= (dbIndex + 1)));
                btn = view.button; // view 안의 Button 참조
            }
            else
            {
                tmp = go.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = display.ToString();
                else
                {
                    legacy = go.GetComponentInChildren<Text>(true);
                    if (legacy != null) legacy.text = display.ToString();
                }

                // 잠금/해금
                bool unlocked = gm.highestUnlockedLevel >= (dbIndex + 1);
                if (btn != null) btn.interactable = unlocked;
            }

            int levelIndex1Based = dbIndex + 1; // DB 실제 인덱스(1-based)
            if (btn != null)
                btn.onClick.AddListener(() => OnLevelClicked(levelIndex1Based));

            display++;
        }
    }

    private void ClearGrid()
    {
        if (!content) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private void OnLevelClicked(int levelIndex1Based)
    {
        GameManager.Instance.CurrentLevel = levelIndex1Based;
        SceneManager.LoadScene("PlayScene");
    }

    public void OnBackToMenuClicked()
    {
        SceneManager.LoadScene("MenuScene");
    }
}
