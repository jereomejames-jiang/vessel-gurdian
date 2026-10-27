using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance;
    public string gameSceneName = "SampleScene";
    public Button startButton;
    public Button settingsButton;
    public Button codexButton;
    public Button levelSelectButton;
    public MainMenuBackground background;
    public Font displayFont;
    public GameObject levelSelectPanel;
    public Button[] levelButtons;
    public Text levelSelectDescription;
    public Button levelBackButton;
    public int selectedLevel = 1;
    public GameObject characterSelectPanel;
    public GameObject settingsPanel;
    public Text settingsVolumeLabel;
    public Button[] characterButtons;
    public Text characterSelectDescription;
    public Button characterBackButton;
    public int selectedCharacter;
    public bool isLoadingGame;
    public bool playerReadyForCharacterSelect;

    void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        EnsureInputRouting();
        Canvas mainCanvas = FindAnyObjectByType<Canvas>();
        if (startButton == null) startButton = FindButton("StartButton");
        if (settingsButton == null) settingsButton = FindButton("SettingsButton");
        if (codexButton == null) codexButton = FindButton("CodexButton");
        if (levelSelectButton == null) levelSelectButton = FindButton("LevelSelectButton");
        if (background == null) background = FindAnyObjectByType<MainMenuBackground>();
        selectedLevel = Mathf.Clamp(PlayerPrefs.GetInt("VesselGuard.SelectedLevel", 1), 1, 3);
        selectedCharacter = Mathf.Clamp(PlayerPrefs.GetInt(CharacterCatalog.PreferenceKey, 0), 0, 2);
        AudioListener.volume = PlayerPrefs.GetFloat("VesselGuard.MasterVolume", 1f);

        displayFont = RuntimeFont.Get();
        if (displayFont != null)
        {
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = displayFont;
        }
        EnsureLevelSelectView();
        EnsureCharacterSelectView();
        EnsureForwardMotion(mainCanvas);
        ApplyRequestedMenuLayout(mainCanvas);
        Text codexLabel = codexButton == null ? null : codexButton.GetComponentInChildren<Text>();
        if (codexLabel != null) codexLabel.text = "血管图鉴";

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsPlaceholder);
        if (codexButton != null) codexButton.onClick.AddListener(OpenCodexPlaceholder);
        if (levelSelectButton != null) levelSelectButton.onClick.AddListener(OpenLevelSelect);
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

    void Update()
    {
        if (settingsPanel != null && settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            settingsPanel.SetActive(false);
        if (levelSelectPanel != null && levelSelectPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseLevelSelect();
        if (characterSelectPanel != null && characterSelectPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseCharacterSelect();
    }

    public void StartGame()
    {
        if (isLoadingGame || playerReadyForCharacterSelect) return;
        isLoadingGame = true;
        if (background != null)
            background.MoveToCenter();
        else
            OnPlayerArrivedAtCenter();
    }

    public void OnPlayerArrivedAtCenter()
    {
        playerReadyForCharacterSelect = true;
        OpenCharacterSelect();
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
        if (background != null) background.RefreshSelectedLevelTheme();
        PlaceButton(levelSelectButton, new Vector2(0f, -460f), new Vector2(260f, 60f), "选择关卡  ·  " + selectedLevel);
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
        if (playerReadyForCharacterSelect)
        {
            playerReadyForCharacterSelect = false;
            isLoadingGame = false;
            if (background != null) background.MoveToStart();
        }
    }

    public void SelectCharacter(int character)
    {
        selectedCharacter = Mathf.Clamp(character, 0, 2);
        PlayerPrefs.SetInt(CharacterCatalog.PreferenceKey, selectedCharacter);
        PlayerPrefs.Save();
        if (background != null && background.playerTransform != null)
            MonsterSurvivorArtLibrary.ApplyCharacter(background.playerTransform.gameObject, (CharacterRole)selectedCharacter);
        RefreshCharacterChoices();
        StartCoroutine(LoadGameAfterDelay());
    }

    System.Collections.IEnumerator LoadGameAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        PlayerPrefs.SetInt("VesselGuard.SelectedLevel", selectedLevel);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (!string.IsNullOrEmpty(gameSceneName)) SceneManager.LoadScene(gameSceneName);
    }

    public void EnsureCharacterSelectView()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        if (characterSelectPanel != null)
        {
            RefreshCharacterChoices();
            return;
        }

        characterSelectPanel = new GameObject("CharacterSelectPanel", typeof(RectTransform), typeof(Image));
        characterSelectPanel.transform.SetParent(canvas.transform, false);
        RectTransform panel = characterSelectPanel.GetComponent<RectTransform>();
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        characterSelectPanel.GetComponent<Image>().color = new Color(0.008f, 0.018f, 0.04f, 0.98f);

        GameObject content = new GameObject("CharacterSelectContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(characterSelectPanel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.sizeDelta = new Vector2(1180f, 700f);
        content.GetComponent<Image>().color = new Color(0.025f, 0.075f, 0.12f, 0.98f);

        Text title = MakeText(content.transform, "CharacterSelectTitle", "选择角色", 36, TextAnchor.MiddleCenter,
            new Vector2(0f, 282f), new Vector2(800f, 60f));
        title.color = new Color(0.7f, 0.94f, 1f, 1f);
        characterSelectDescription = MakeText(content.transform, "CharacterSelectDescription", "", 17, TextAnchor.UpperLeft,
            new Vector2(0f, -185f), new Vector2(1060f, 210f));
        characterSelectDescription.color = new Color(0.78f, 0.86f, 0.94f, 1f);

        characterButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int character = i;
            characterButtons[i] = MakeButton(content.transform, "Character" + i + "Button",
                CharacterCatalog.Names[i],
                new Vector2(-390f + i * 390f, 58f), new Vector2(340f, 230f),
                new Color(CharacterCatalog.Colors[i].r * 0.45f, CharacterCatalog.Colors[i].g * 0.45f,
                    CharacterCatalog.Colors[i].b * 0.45f, 1f));
            characterButtons[i].onClick.AddListener(() => SelectCharacter(character));
        }
        characterBackButton = MakeButton(content.transform, "CharacterBackButton", "返回主界面",
            new Vector2(0f, -275f), new Vector2(250f, 58f), new Color(0.28f, 0.12f, 0.18f, 1f));
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
            Image image = characterButtons[i].GetComponent<Image>();
            bool chosen = i == selectedCharacter;
            Color baseColor = CharacterCatalog.Colors[i];
            if (image != null) image.color = chosen
                ? new Color(baseColor.r * 0.75f, baseColor.g * 0.75f, baseColor.b * 0.75f, 1f)
                : new Color(baseColor.r * 0.35f, baseColor.g * 0.35f, baseColor.b * 0.35f, 1f);
        }
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
        if (settingsPanel == null) EnsureSettingsView();
        if (settingsPanel == null) return;
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
        RefreshSettingsView();
    }

    void EnsureSettingsView()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsPanel.transform.SetParent(canvas.transform, false);
        RectTransform full = settingsPanel.GetComponent<RectTransform>();
        full.anchorMin = Vector2.zero;
        full.anchorMax = Vector2.one;
        full.offsetMin = full.offsetMax = Vector2.zero;
        settingsPanel.GetComponent<Image>().color = new Color(0.005f, 0.018f, 0.035f, 0.88f);

        GameObject content = new GameObject("SettingsContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(settingsPanel.transform, false);
        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = new Vector2(580f, 390f);
        content.GetComponent<Image>().color = new Color(0.025f, 0.09f, 0.14f, 0.98f);

        Text title = MakeText(content.transform, "SettingsTitle", "设置", 36, TextAnchor.MiddleCenter,
            new Vector2(0f, 135f), new Vector2(460f, 58f));
        title.color = new Color(0.7f, 0.94f, 1f, 1f);
        Text volumeTitle = MakeText(content.transform, "MasterVolumeTitle", "总音量", 24, TextAnchor.MiddleCenter,
            new Vector2(0f, 55f), new Vector2(300f, 44f));
        volumeTitle.color = new Color(0.78f, 0.86f, 0.94f, 1f);
        settingsVolumeLabel = MakeText(content.transform, "MasterVolumeValue", "", 24, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(180f, 44f));
        Button quieter = MakeButton(content.transform, "VolumeDownButton", "−", new Vector2(-155f, 0f),
            new Vector2(70f, 56f), new Color(0.12f, 0.22f, 0.32f, 1f));
        Button louder = MakeButton(content.transform, "VolumeUpButton", "+", new Vector2(155f, 0f),
            new Vector2(70f, 56f), new Color(0.12f, 0.22f, 0.32f, 1f));
        quieter.onClick.AddListener(() => AdjustMasterVolume(-0.1f));
        louder.onClick.AddListener(() => AdjustMasterVolume(0.1f));
        Button close = MakeButton(content.transform, "SettingsCloseButton", "返回", new Vector2(0f, -125f),
            new Vector2(220f, 58f), new Color(0.08f, 0.46f, 0.58f, 1f));
        close.onClick.AddListener(() => settingsPanel.SetActive(false));
        settingsPanel.SetActive(false);
    }

    void AdjustMasterVolume(float delta)
    {
        AudioListener.volume = Mathf.Clamp01(AudioListener.volume + delta);
        PlayerPrefs.SetFloat("VesselGuard.MasterVolume", AudioListener.volume);
        PlayerPrefs.Save();
        RefreshSettingsView();
    }

    void RefreshSettingsView()
    {
        if (settingsVolumeLabel != null) settingsVolumeLabel.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
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
        if (child == null)
        {
            GameObject global = GameObject.Find(objectName);
            if (global != null) child = global.transform;
        }
        return child == null ? null : child.GetComponent<Button>();
    }

    void EnsureForwardMotion(Canvas canvas)
    {
        if (background == null)
        {
            GameObject motion = GameObject.Find("MainMenuBackgroundMotion");
            if (motion == null) motion = new GameObject("MainMenuBackgroundMotion");
            background = motion.GetComponent<MainMenuBackground>();
            if (background == null) background = motion.AddComponent<MainMenuBackground>();
        }

        GameObject hero = GameObject.Find("MainMenuPlayer");
        if (hero == null)
        {
            hero = new GameObject("MainMenuPlayer");
        }
        MonsterSurvivorArtLibrary.ApplyMenuLightPoint(hero);
        background.playerTransform = hero.transform;
        background.playerSprite = hero.GetComponent<SpriteRenderer>();
        background.initialPosition = new Vector3(0f, 1.45f, 0f);
        background.centerPosition = Vector3.zero;
    }

    void ApplyRequestedMenuLayout(Canvas canvas)
    {
        if (canvas == null) return;
        Image fullBackground = FindChild(canvas.transform, "Background")?.GetComponent<Image>();
        if (fullBackground != null)
        {
            fullBackground.color = new Color(0.012f, 0.025f, 0.05f, 0f);
            fullBackground.raycastTarget = false;
        }
        Image centerGlow = FindChild(canvas.transform, "CenterGlow")?.GetComponent<Image>();
        if (centerGlow != null)
        {
            centerGlow.gameObject.SetActive(false);
        }

        PlaceText(canvas.transform, "GameTitle", new Vector2(0f, -78f), new Vector2(900f, 86f), 62);
        PlaceText(canvas.transform, "GameSubtitle", new Vector2(0f, -150f), new Vector2(800f, 58f), 30);
        PlaceText(canvas.transform, "GameDescription", new Vector2(0f, -202f), new Vector2(800f, 38f), 18);

        PlaceButton(startButton, new Vector2(0f, -365f), new Vector2(360f, 84f), "开始游戏");
        if (levelSelectButton == null && canvas != null)
        {
            levelSelectButton = MakeButton(canvas.transform, "LevelSelectButton", "选择关卡", Vector2.zero,
                new Vector2(260f, 60f), new Color(0.12f, 0.22f, 0.32f, 0.96f));
        }
        PlaceButton(levelSelectButton, new Vector2(0f, -460f), new Vector2(260f, 60f), "选择关卡  ·  " + selectedLevel);
        PlaceButton(settingsButton, new Vector2(-740f, -10f), new Vector2(420f, 140f), "设置");
        PlaceButton(codexButton, new Vector2(740f, -10f), new Vector2(420f, 140f), "血管图鉴");

        HideMenuObject(canvas.transform, "SystemTitle");
        HideMenuObject(canvas.transform, "AchievementsButton");
        HideMenuObject(canvas.transform, "QuitButton");
        PlaceBottomText(canvas.transform, "StatusText", new Vector2(0f, 22f), new Vector2(900f, 34f), 15);
    }

    void PlaceText(Transform root, string objectName, Vector2 position, Vector2 size, int fontSize, bool topAnchored = true)
    {
        Transform target = FindChild(root, objectName);
        if (target == null) return;
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) return;
        if (topAnchored)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
        else rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text label = target.GetComponent<Text>();
        if (label != null) label.fontSize = fontSize;
    }

    void PlaceBottomText(Transform root, string objectName, Vector2 position, Vector2 size, int fontSize)
    {
        Transform target = FindChild(root, objectName);
        if (target == null) return;
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text label = target.GetComponent<Text>();
        if (label != null) label.fontSize = fontSize;
    }

    void PlaceButton(Button button, Vector2 position, Vector2 size, string caption)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = caption;
            label.fontSize = size.y < 70f ? 20 : (size.y >= 130f ? 34 : (size.y >= 100f ? 30 : 25));
        }
    }

    void HideMenuObject(Transform root, string objectName)
    {
        Transform target = FindChild(root, objectName);
        if (target != null) target.gameObject.SetActive(false);
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
        Instance = null;
    }
}









