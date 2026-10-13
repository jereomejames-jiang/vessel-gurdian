using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public string gameSceneName = "SampleScene";
    public Button startButton;
    public Button settingsButton;
    public Button codexButton;
    public Button achievementsButton;
    public Button quitButton;
    public Text statusText;
    public Font displayFont;

    void Awake()
    {
        if (startButton == null) startButton = FindButton("StartButton");
        if (settingsButton == null) settingsButton = FindButton("SettingsButton");
        if (codexButton == null) codexButton = FindButton("CodexButton");
        if (achievementsButton == null) achievementsButton = FindButton("AchievementsButton");
        if (quitButton == null) quitButton = FindButton("QuitButton");
        if (statusText == null) statusText = FindText("StatusText");

        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        if (displayFont != null)
        {
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = displayFont;
        }

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsPlaceholder);
        if (codexButton != null) codexButton.onClick.AddListener(OpenCodexPlaceholder);
        if (achievementsButton != null) achievementsButton.onClick.AddListener(OpenAchievementsPlaceholder);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    public void StartGame()
    {
        if (!string.IsNullOrEmpty(gameSceneName)) SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettingsPlaceholder()
    {
        ShowPlaceholder("设置系统预留入口");
    }

    public void OpenCodexPlaceholder()
    {
        SkillCodexManager codex = FindAnyObjectByType<SkillCodexManager>();
        if (codex == null) codex = gameObject.AddComponent<SkillCodexManager>();
        codex.isGameplay = false;
        codex.canvas = FindAnyObjectByType<Canvas>();
        codex.EnsureCards();
        codex.EnsureView();
        codex.SetPanel(true);
    }

    public void OpenAchievementsPlaceholder()
    {
        ShowPlaceholder("成就系统预留入口");
    }

    public void ShowPlaceholder(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public Button FindButton(string objectName)
    {
        Transform child = FindChild(transform, objectName);
        return child == null ? null : child.GetComponent<Button>();
    }

    public Text FindText(string objectName)
    {
        Transform child = FindChild(transform, objectName);
        return child == null ? null : child.GetComponent<Text>();
    }

    public Transform FindChild(Transform parent, string objectName)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName) return child;
            Transform nested = FindChild(child, objectName);
            if (nested != null) return nested;
        }
        return null;
    }

    void OnDestroy()
    {
        if (displayFont != null) Destroy(displayFont);
    }
}
// codex menu wired
