using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    public Button levelSelectButton;
    public GameObject levelSelectPanel;
    public Button[] levelButtons;
    public Text levelSelectDescription;
    public Button levelBackButton;
    public int selectedLevel = 1;
    public bool isLoadingGame;
    public int fallbackClickFrame = -1;

    void Awake()
    {
        Application.runInBackground = true;
        EnsureInputRouting();
        if (startButton == null) startButton = FindButton("StartButton");
        if (settingsButton == null) settingsButton = FindButton("SettingsButton");
        if (codexButton == null) codexButton = FindButton("CodexButton");
        if (achievementsButton == null) achievementsButton = FindButton("AchievementsButton");
        if (quitButton == null) quitButton = FindButton("QuitButton");
        if (statusText == null) statusText = FindText("StatusText");
        selectedLevel = Mathf.Clamp(PlayerPrefs.GetInt("VesselGuard.SelectedLevel", 1), 1, 3);

        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        if (displayFont != null)
        {
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = displayFont;
        }
        EnsureLevelSelectView();
        Text codexLabel = codexButton == null ? null : codexButton.GetComponentInChildren<Text>();
        if (codexLabel != null) codexLabel.text = "血管图鉴";

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsPlaceholder);
        if (codexButton != null) codexButton.onClick.AddListener(OpenCodexPlaceholder);
        if (achievementsButton != null) achievementsButton.onClick.AddListener(OpenAchievementsPlaceholder);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    public void EnsureInputRouting()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem = eventObject.GetComponent<EventSystem>();
        }
        eventSystem.enabled = true;
        StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (inputModule == null) inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        inputModule.enabled = true;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.MouseUp || Event.current.button != 0) return;
        if (fallbackClickFrame == Time.frameCount) return;
        Vector2 guiPosition = Event.current.mousePosition;
        Button hit = FindTopmostFallbackButton(guiPosition);
        if (hit == null) return;
        fallbackClickFrame = Time.frameCount;
        Event.current.Use();
        hit.onClick.Invoke();
    }

    public Button FindTopmostFallbackButton(Vector2 guiPosition)
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;
        Button best = null;
        int bestScore = -1;
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (!IsFallbackHit(button, guiPosition)) continue;
            int score = 0;
            Transform current = button.transform;
            while (current != null && current != canvas.transform)
            {
                score = score * 100 + current.GetSiblingIndex() + 1;
                current = current.parent;
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = button;
            }
        }
        return best;
    }
    public bool IsFallbackHit(Button button, Vector2 guiPosition)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) return false;
        RectTransform rectTransform = button.transform as RectTransform;
        if (rectTransform == null) return false;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
        Vector2 size = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);
        Rect rect = new Rect(screenPosition.x - size.x * 0.5f, Screen.height - screenPosition.y - size.y * 0.5f, size.x, size.y);
        return rect.Contains(guiPosition);
    }
    void Update()
    {
        if (levelSelectPanel != null && levelSelectPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseLevelSelect();
    }

    public void StartGame()
    {
        if (isLoadingGame) return;
        isLoadingGame = true;
        PlayerPrefs.SetInt("VesselGuard.SelectedLevel", selectedLevel);
        PlayerPrefs.Save();
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (!string.IsNullOrEmpty(gameSceneName)) SceneManager.LoadScene(gameSceneName);
    }

    public void OpenLevelSelect()
    {
        if (levelSelectPanel == null) EnsureLevelSelectView();
        if (levelSelectPanel == null) return;
        levelSelectPanel.SetActive(true);
        levelSelectPanel.transform.SetAsLastSibling();
        RefreshLevelChoices();
    }

    public void CloseLevelSelect()
    {
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
    }

    public void SelectLevel(int level)
    {
        selectedLevel = Mathf.Clamp(level, 1, 3);
        PlayerPrefs.SetInt("VesselGuard.SelectedLevel", selectedLevel);
        PlayerPrefs.Save();
        RefreshLevelChoices();
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

    public void EnsureLevelSelectView()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (levelSelectButton == null)
        {
            levelSelectButton = MakeButton(canvas.transform, "LevelSelectButton", "选择关卡",
                new Vector2(0f, -145f), new Vector2(250f, 56f), new Color(0.18f, 0.34f, 0.48f, 1f));
            levelSelectButton.onClick.AddListener(OpenLevelSelect);
        }

        if (levelSelectPanel != null)
        {
            RefreshLevelChoices();
            return;
        }

        levelSelectPanel = new GameObject("LevelSelectPanel", typeof(RectTransform), typeof(Image));
        levelSelectPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = levelSelectPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        levelSelectPanel.GetComponent<Image>().color = new Color(0.008f, 0.018f, 0.04f, 0.97f);

        GameObject content = new GameObject("LevelSelectContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(levelSelectPanel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.sizeDelta = new Vector2(1080f, 620f);
        contentRect.anchoredPosition = Vector2.zero;
        content.GetComponent<Image>().color = new Color(0.025f, 0.075f, 0.12f, 0.98f);

        Text title = MakeText(content.transform, "LevelSelectTitle", "选择关卡", 34, TextAnchor.MiddleCenter,
            new Vector2(0f, 245f), new Vector2(800f, 56f));
        title.color = new Color(0.7f, 0.94f, 1f, 1f);
        levelSelectDescription = MakeText(content.transform, "LevelSelectDescription", "", 22, TextAnchor.MiddleCenter,
            new Vector2(0f, -105f), new Vector2(900f, 100f));
        levelSelectDescription.color = new Color(0.78f, 0.86f, 0.94f, 1f);

        levelButtons = new Button[3];
        string[] captions = { "第一关\n血管起点", "第二关\n斑块挑战", "第三关\n健康冲刺" };
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int level = i + 1;
            levelButtons[i] = MakeButton(content.transform, "Level" + level + "Button", captions[i],
                new Vector2(-340f + i * 340f, 80f), new Vector2(290f, 130f),
                new Color(0.12f, 0.22f, 0.32f, 1f));
            levelButtons[i].onClick.AddListener(() => SelectLevel(level));
        }

        levelBackButton = MakeButton(content.transform, "LevelBackButton", "返回主界面",
            new Vector2(0f, -220f), new Vector2(250f, 58f), new Color(0.28f, 0.12f, 0.18f, 1f));
        levelBackButton.onClick.AddListener(CloseLevelSelect);
        levelSelectPanel.SetActive(false);
        RefreshLevelChoices();
    }

    public void RefreshLevelChoices()
    {
        if (levelButtons == null) return;
        string[] descriptions =
        {
            "标准节奏：熟悉移动、自动攻击和血管图鉴。",
            "压力提升：敌人出现更频繁，适合进阶挑战。",
            "健康冲刺：最高强度，检验你的守护策略。"
        };
        if (levelSelectDescription != null) levelSelectDescription.text = descriptions[selectedLevel - 1];
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;
            Image image = levelButtons[i].GetComponent<Image>();
            bool chosen = i + 1 == selectedLevel;
            if (image != null) image.color = chosen
                ? new Color(0.12f, 0.58f, 0.72f, 1f)
                : new Color(0.12f, 0.22f, 0.32f, 1f);
        }
    }

    public Button MakeButton(Transform parent, string objectName, string caption, Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(Mathf.Min(1f, color.r + 0.14f), Mathf.Min(1f, color.g + 0.14f), Mathf.Min(1f, color.b + 0.14f), 1f);
        colors.pressedColor = new Color(Mathf.Max(0f, color.r - 0.08f), Mathf.Max(0f, color.g - 0.08f), Mathf.Max(0f, color.b - 0.08f), 1f);
        button.colors = colors;
        SetRect(obj.GetComponent<RectTransform>(), position, size);
        Text label = MakeText(obj.transform, "Label", caption, 22, TextAnchor.MiddleCenter, Vector2.zero, size - Vector2.one * 12f);
        label.color = Color.white;
        return button;
    }

    public Text MakeText(Transform parent, string objectName, string value, int fontSize, TextAnchor alignment, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = displayFont == null ? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") : displayFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(text.rectTransform, position, size);
        return text;
    }

    public void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
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









