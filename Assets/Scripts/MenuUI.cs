// MenuUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuUI : MonoBehaviour
{
    public void OnStartButtonClicked()
    {
        SceneManager.LoadScene("LevelSelectScene");
    }
}
