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
    public Button characterSelectButton;
    public GameObject characterSelectPanel;
    public Button[] characterButtons;
    public Text characterSelectDescription;
    public Button characterBackButton;
    public int selectedCharacter;
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
        selectedCharacter = Mathf.Clamp(PlayerPrefs.GetInt(CharacterCatalog.PreferenceKey, 0), 0, 2);

        // WebGL 无系统字体，统一使用随包分发的 simhei
        displayFont = RuntimeFont.Get();
        if (displayFont != null)
        {
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = displayFont;
        }
        EnsureLevelSelectView();
        EnsureCharacterSelectView();
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
        if (characterSelectPanel != null && characterSelectPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseCharacterSelect();
    }

    public void StartGame()
    {
        if (isLoadingGame) return;
        isLoadingGame = true;
        PlayerPrefs.SetInt("VesselGuard.SelectedLevel", selectedLevel);
        PlayerPrefs.SetInt(CharacterCatalog.PreferenceKey, selectedCharacter);
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

    public void OpenCharacterSelect()
    {
        if (characterSelectPanel == null) EnsureCharacterSelectView();
        if (characterSelectPanel == null) return;
        characterSelectPanel.SetActive(true);
        characterSelectPanel.transform.SetAsLastSibling();
        RefreshCharacterChoices();
    }

    public void CloseCharacterSelect()
    {
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
    }

    public void SelectCharacter(int character)
    {
        selectedCharacter = Mathf.Clamp(character, 0, 2);
        PlayerPrefs.SetInt(CharacterCatalog.PreferenceKey, selectedCharacter);
        PlayerPrefs.Save();
        RefreshCharacterChoices();
    }

    public void EnsureCharacterSelectView()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        if (characterSelectButton == null)
        {
            characterSelectButton = MakeButton(canvas.transform, "CharacterSelectButton", "选择角色",
                new Vector2(0f, -215f), new Vector2(250f, 56f), new Color(0.24f, 0.32f, 0.46f, 1f));
            characterSelectButton.onClick.AddListener(OpenCharacterSelect);
        }
        if (characterSelectPanel != null)
        {
            RefreshCharacterChoices();
            return;
        }

        Image panel = MenuSkin.FullScreen(canvas.transform, "CharacterSelectPanel", MenuSkin.Scrim);
        characterSelectPanel = panel.gameObject;

        Image panelCard = MenuSkin.Card(panel.transform, "CharacterSelectCard", new Vector2(1180f, 700f), Vector2.zero);
        MenuSkin.Label(panelCard.transform, "CharacterSelectTitle", "选择角色", 44, MenuSkin.Ink,
            TextAnchor.MiddleCenter, new Vector2(0f, 300f), new Vector2(800f, 60f));
        characterSelectDescription = MenuSkin.Label(panelCard.transform, "CharacterSelectDescription", "", 17,
            MenuSkin.Muted, TextAnchor.UpperLeft, new Vector2(0f, -140f), new Vector2(1080f, 190f));

        characterButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int character = i;
            Color baseColor = CharacterCatalog.Colors[i];
            characterButtons[i] = MenuSkin.Button(panelCard.transform, "Character" + i + "Button",
                CharacterCatalog.Names[i], baseColor, Darken(baseColor, 0.5f), 20,
                new Vector2(-380f + i * 380f, 80f), new Vector2(320f, 220f), 32, MenuSkin.White);
            characterButtons[i].onClick.AddListener(() => SelectCharacter(character));
        }
        characterBackButton = MenuSkin.Button(panelCard.transform, "CharacterBackButton", "返回主界面",
            MenuSkin.Grey, MenuSkin.GreyDepth, 16, new Vector2(0f, -310f), new Vector2(220f, 64f), 24, MenuSkin.Ink);
        characterBackButton.onClick.AddListener(CloseCharacterSelect);
        characterSelectPanel.SetActive(false);
        RefreshCharacterChoices();
    }

    public void RefreshCharacterChoices()
    {
        if (characterButtons == null) return;
        if (characterSelectDescription != null)
            characterSelectDescription.text = BuildCharacterRouteSummary((CharacterRole)selectedCharacter);
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] == null) continue;
            bool chosen = i == selectedCharacter;
            Color baseColor = CharacterCatalog.Colors[i];
            MenuSkin.Recolor(characterButtons[i],
                chosen ? baseColor : Color.Lerp(baseColor, MenuSkin.White, 0.62f),
                chosen ? Darken(baseColor, 0.5f) : Color.Lerp(baseColor, MenuSkin.White, 0.35f),
                chosen ? MenuSkin.White : MenuSkin.Ink);
        }
    }

    static Color Darken(Color c, float amount)
    {
        return Color.Lerp(c, Color.black, amount);
    }

    public string BuildCharacterRouteSummary(CharacterRole role)
    {
        string summary = CharacterCatalog.Names[(int)role] + "技能路线（每升 3 级进化）\n";
        for (int rank = 0; rank <= CharacterCatalog.MaxRank; rank++)
        {
            int level = rank == 0 ? 1 : rank * 3;
            summary += "Lv." + level + "：" + CharacterCatalog.Route(role, rank);
            if (rank < CharacterCatalog.MaxRank) summary += "\n";
        }
        return summary;
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

        Image panel = MenuSkin.FullScreen(canvas.transform, "LevelSelectPanel", MenuSkin.Scrim);
        levelSelectPanel = panel.gameObject;

        Image panelCard = MenuSkin.Card(panel.transform, "LevelSelectCard", new Vector2(1000f, 640f), Vector2.zero);
        MenuSkin.Label(panelCard.transform, "LevelSelectTitle", "选择关卡", 44, MenuSkin.Ink,
            TextAnchor.MiddleCenter, new Vector2(0f, 250f), new Vector2(800f, 60f));
        levelSelectDescription = MenuSkin.Label(panelCard.transform, "LevelSelectDescription", "", 22,
            MenuSkin.Muted, TextAnchor.MiddleCenter, new Vector2(0f, -110f), new Vector2(880f, 90f));

        levelButtons = new Button[3];
        string[] captions = { "第一关\n血管起点", "第二关\n斑块挑战", "第三关\n健康冲刺" };
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int level = i + 1;
            levelButtons[i] = MenuSkin.Button(panelCard.transform, "Level" + level + "Button", captions[i],
                MenuSkin.Blue, MenuSkin.BlueDepth, 20, new Vector2(-320f + i * 320f, 60f),
                new Vector2(280f, 150f), 26, MenuSkin.White);
            levelButtons[i].onClick.AddListener(() => SelectLevel(level));
        }

        levelBackButton = MenuSkin.Button(panelCard.transform, "LevelBackButton", "返回主界面",
            MenuSkin.Grey, MenuSkin.GreyDepth, 16, new Vector2(0f, -250f), new Vector2(220f, 64f), 24, MenuSkin.Ink);
        levelBackButton.onClick.AddListener(CloseLevelSelect);
        levelSelectPanel.SetActive(false);
        RefreshLevelChoices();
    }

    public void RefreshLevelChoices()
    {
        if (levelButtons == null) return;
        string[] levelDescriptions =
        {
            "标准节奏：熟悉移动、自动攻击和血管图鉴。",
            "压力提升：敌人出现更频繁，适合进阶挑战。",
            "健康冲刺：最高强度，检验你的守护策略。"
        };
        if (levelSelectDescription != null) levelSelectDescription.text = levelDescriptions[selectedLevel - 1];
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;
            bool chosen = i + 1 == selectedLevel;
            MenuSkin.Recolor(levelButtons[i],
                chosen ? MenuSkin.Blue : MenuSkin.CardIdle,
                chosen ? MenuSkin.BlueDepth : MenuSkin.CardIdleDepth,
                chosen ? MenuSkin.White : MenuSkin.Ink);
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
        text.font = displayFont == null ? RuntimeFont.Get() : displayFont;
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
        // displayFont 是共享资源（RuntimeFont），不能销毁
    }
}
// codex menu wired









