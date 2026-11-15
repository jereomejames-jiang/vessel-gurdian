using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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
        EnsureMenuGrain(mainCanvas);
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
        SyncFocusRing();
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
        characterSelectPanel.GetComponent<Image>().color = UITheme.Scrim;

        GameObject content = new GameObject("CharacterSelectContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(characterSelectPanel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.sizeDelta = new Vector2(1180f, 700f);
        StylePanelBody(content, 1180f, 700f);

        Text title = MakeText(content.transform, "CharacterSelectTitle", "选择角色", UITheme.Title, TextAnchor.MiddleCenter,
            new Vector2(0f, 282f), new Vector2(800f, 60f));
        title.font = RuntimeFont.GetTitle();
        title.color = UITheme.TextPrimary;
        characterSelectDescription = MakeText(content.transform, "CharacterSelectDescription", "", UITheme.Body, TextAnchor.UpperLeft,
            new Vector2(-180f, -185f), new Vector2(700f, 210f));
        characterSelectDescription.color = UITheme.TextMuted;
        characterSelectDescription.lineSpacing = 1.3f;
        characterSelectDescription.supportRichText = true;

        characterButtons = new Button[3];
        string[] portraitNames = { "character_doctor", "character_nurse", "character_pharmacist" };
        for (int i = 0; i < 3; i++)
        {
            int character = i;
            characterButtons[i] = MakeButton(content.transform, "Character" + i + "Button",
                CharacterCatalog.Names[i],
                new Vector2(-390f + i * 390f, 58f), new Vector2(340f, 230f), UITheme.Surface);
            StyleTile(characterButtons[i], 340f, 230f);
            Image band = EnsureChildImage(characterButtons[i].transform, "RoleBand");
            band.sprite = null; band.type = Image.Type.Simple; band.raycastTarget = false;
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 0f); bandRect.anchorMax = new Vector2(0f, 1f);
            bandRect.pivot = new Vector2(0f, 0.5f);
            bandRect.offsetMin = new Vector2(0f, 10f); bandRect.offsetMax = new Vector2(4f, -10f);
            Image portrait = EnsureChildImage(characterButtons[i].transform, "Portrait");
            portrait.sprite = MonsterSurvivorArtLibrary.FirstFrame(portraitNames[i]);
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 1f);
            portraitRect.sizeDelta = new Vector2(110f, 110f);
            portraitRect.anchoredPosition = new Vector2(0f, -16f);
            Text nameLabel = characterButtons[i].GetComponentInChildren<Text>();
            if (nameLabel != null)
            {
                nameLabel.font = RuntimeFont.GetTitle();
                nameLabel.fontSize = 24;
                nameLabel.color = UITheme.TextPrimary;
                SetRect(nameLabel.rectTransform, new Vector2(0f, -78f), new Vector2(300f, 40f));
                nameLabel.transform.SetAsLastSibling();
            }
            characterButtons[i].onClick.AddListener(() => SelectCharacter(character));
        }
        WireRowNav(characterButtons);
        characterBackButton = MakeButton(content.transform, "CharacterBackButton", "返回主界面",
            new Vector2(400f, -275f), new Vector2(250f, 58f), UITheme.Surface);
        StyleMenuButton(characterBackButton, 20, UITheme.TextMuted, UITheme.Surface, 10f);
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
            ColorBlock cb = characterButtons[i].colors;
            cb.normalColor = chosen ? UITheme.SurfaceRaised : UITheme.Surface;
            cb.selectedColor = cb.normalColor;
            characterButtons[i].colors = cb;
            Transform frameT = characterButtons[i].transform.Find("TileFrame");
            if (frameT != null)
            {
                Image frame = frameT.GetComponent<Image>();
                frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(340, 230, 12f, chosen ? 2f : 1f);
                TintSelect(characterButtons[i], frameT, FrameTarget(characterButtons[i], chosen));
            }
            Transform bandT = characterButtons[i].transform.Find("RoleBand");
            if (bandT != null)
            {
                Color roleColor = UITheme.Role[i];
                roleColor.a = chosen ? 1f : 0.5f;
                bandT.GetComponent<Image>().color = roleColor;
            }
        }
    }

    public string BuildCharacterRouteSummary(CharacterRole role)
    {
        // R2-10：首行标题暖强调；rank 前缀 14 Clinical 与正文 18 分层（文案逐字不变）。
        string summary = UITheme.Wrap(CharacterCatalog.Names[(int)role] + "技能路线（每升 3 级进化）", UITheme.WarmSoft) + "\n";
        for (int rank = 0; rank <= CharacterCatalog.MaxRank; rank++)
        {
            int level = rank == 0 ? 1 : rank * 3;
            summary += UITheme.Wrap("Lv." + level + "：", UITheme.Clinical, 14) + CharacterCatalog.Route(role, rank);
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
        settingsPanel.GetComponent<Image>().color = UITheme.Scrim;

        GameObject content = new GameObject("SettingsContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(settingsPanel.transform, false);
        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = new Vector2(580f, 390f);
        StylePanelBody(content, 580f, 390f);

        Text title = MakeText(content.transform, "SettingsTitle", "设置", UITheme.Title, TextAnchor.MiddleCenter,
            new Vector2(0f, 135f), new Vector2(460f, 58f));
        title.font = RuntimeFont.GetTitle();
        title.color = UITheme.TextPrimary;
        Text volumeTitle = MakeText(content.transform, "MasterVolumeTitle", "总音量", UITheme.Subtitle, TextAnchor.MiddleCenter,
            new Vector2(0f, 55f), new Vector2(300f, 44f));
        volumeTitle.color = UITheme.TextMuted;
        settingsVolumeLabel = MakeText(content.transform, "MasterVolumeValue", "", 24, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(180f, 44f));
        settingsVolumeLabel.font = RuntimeFont.GetTitle();
        settingsVolumeLabel.color = UITheme.TextPrimary;
        Button quieter = MakeButton(content.transform, "VolumeDownButton", "−", new Vector2(-155f, 0f),
            new Vector2(70f, 56f), UITheme.Surface);
        Button louder = MakeButton(content.transform, "VolumeUpButton", "+", new Vector2(155f, 0f),
            new Vector2(70f, 56f), UITheme.Surface);
        StyleMenuButton(quieter, 24, UITheme.TextPrimary, UITheme.Surface, 10f);
        StyleMenuButton(louder, 24, UITheme.TextPrimary, UITheme.Surface, 10f);
        quieter.onClick.AddListener(() => AdjustMasterVolume(-0.1f));
        louder.onClick.AddListener(() => AdjustMasterVolume(0.1f));
        Button close = MakeButton(content.transform, "SettingsCloseButton", "返回", new Vector2(0f, -125f),
            new Vector2(220f, 58f), UITheme.Surface);
        StyleMenuButton(close, 20, UITheme.TextMuted, UITheme.Surface, 10f);
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
        levelSelectPanel.GetComponent<Image>().color = UITheme.Scrim;

        GameObject content = new GameObject("LevelSelectContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(levelSelectPanel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.sizeDelta = new Vector2(1080f, 560f);
        contentRect.anchoredPosition = Vector2.zero;
        StylePanelBody(content, 1080f, 560f);

        Text title = MakeText(content.transform, "LevelSelectTitle", "选择关卡", UITheme.Title, TextAnchor.MiddleCenter,
            new Vector2(0f, 215f), new Vector2(800f, 56f));
        title.font = RuntimeFont.GetTitle();
        title.color = UITheme.TextPrimary;
        levelSelectDescription = MakeText(content.transform, "LevelSelectDescription", "", UITheme.Body, TextAnchor.MiddleCenter,
            new Vector2(0f, -60f), new Vector2(900f, 64f));
        levelSelectDescription.color = UITheme.TextMuted;
        levelSelectDescription.lineSpacing = 1.2f;

        levelButtons = new Button[3];
        // R2-09：tile 双行层级——首行 20 Medium 暖纸白，关名 16 暖沙灰（文案逐字不变）。
        string[] captions =
        {
            "第一关\n" + UITheme.Wrap("血管起点", UITheme.TextMuted, 16),
            "第二关\n" + UITheme.Wrap("斑块挑战", UITheme.TextMuted, 16),
            "第三关\n" + UITheme.Wrap("健康冲刺", UITheme.TextMuted, 16)
        };
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int level = i + 1;
            levelButtons[i] = MakeButton(content.transform, "Level" + level + "Button", captions[i],
                new Vector2(-340f + i * 340f, 60f), new Vector2(290f, 130f), UITheme.Surface);
            StyleTile(levelButtons[i], 290f, 130f);
            Text tileLabel = levelButtons[i].GetComponentInChildren<Text>();
            if (tileLabel != null)
            {
                tileLabel.font = RuntimeFont.GetTitle();
                tileLabel.fontSize = 20;
                tileLabel.color = UITheme.TextPrimary;
                tileLabel.lineSpacing = 1.15f;
            }
            Image badge = EnsureChildImage(levelButtons[i].transform, "LevelBadge");
            badge.sprite = null; badge.type = Image.Type.Simple; badge.raycastTarget = false;
            RectTransform badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(40f, 20f);
            badgeRect.anchoredPosition = new Vector2(10f, -10f);
            Text badgeText = MakeText(badge.transform, "LevelBadgeText", "0" + level, UITheme.Tag, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(40f, 20f));
            badgeText.font = RuntimeFont.GetTitle();
            badgeText.color = UITheme.InkOnWarm;
            if (tileLabel != null) tileLabel.transform.SetAsLastSibling();
            levelButtons[i].onClick.AddListener(() => SelectLevel(level));
        }
        WireRowNav(levelButtons);

        levelBackButton = MakeButton(content.transform, "LevelBackButton", "返回主界面",
            new Vector2(0f, -190f), new Vector2(250f, 58f), UITheme.Surface);
        StyleMenuButton(levelBackButton, 20, UITheme.TextMuted, UITheme.Surface, 10f);
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
        if (levelSelectDescription != null)
        {
            // R2-09：首个"："前缀临床青，其余暖沙灰；文案逐字不变。
            string desc = descriptions[selectedLevel - 1];
            int cut = desc.IndexOf('：');
            levelSelectDescription.text = cut > 0
                ? UITheme.Wrap(desc.Substring(0, cut), UITheme.Clinical) + "：" + UITheme.Wrap(desc.Substring(cut + 1), UITheme.TextMuted)
                : desc;
        }
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;
            bool chosen = i + 1 == selectedLevel;
            ColorBlock cb = levelButtons[i].colors;
            cb.normalColor = chosen ? UITheme.SurfaceRaised : UITheme.Surface;
            cb.selectedColor = cb.normalColor;
            levelButtons[i].colors = cb;
            Transform frameT = levelButtons[i].transform.Find("TileFrame");
            if (frameT != null)
            {
                Image frame = frameT.GetComponent<Image>();
                frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(290, 130, 12f, chosen ? 2f : 1f);
                TintSelect(levelButtons[i], frameT, FrameTarget(levelButtons[i], chosen));
            }
            Transform badgeT = levelButtons[i].transform.Find("LevelBadge");
            if (badgeT != null)
            {
                // R2-09：未选中牌降为琥珀 a0.16 底 + TextMuted a0.9 字（复合底对比 ≈7:1），数字可读。
                badgeT.GetComponent<Image>().color = chosen ? UITheme.WarmAccent : UITheme.HexA("#D9A05B", 0.16f);
                Transform badgeTextT = badgeT.Find("LevelBadgeText");
                if (badgeTextT != null)
                {
                    Color ink = chosen ? UITheme.InkOnWarm : UITheme.TextMuted;
                    if (!chosen) ink.a = 0.9f;
                    badgeTextT.GetComponent<Text>().color = ink;
                }
            }
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
        // R2-07：hover/press 走令牌，删除族外 ±0.14 派生色（normal 保持传入 body 色）。
        colors.highlightedColor = UITheme.SurfaceRaised;
        colors.pressedColor = UITheme.Hex("#241D17");
        colors.fadeDuration = 0.12f;
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
        text.supportRichText = true;
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
        // 标题两侧的青色霓虹横线降为 muted 临床青：首视图不再留高饱和青。
        Image topAccent = FindChild(canvas.transform, "TopAccent")?.GetComponent<Image>();
        if (topAccent != null)
        {
            topAccent.color = UITheme.HexA("#7FB5AD", 0.55f);
            topAccent.raycastTarget = false;
        }

        PlaceText(canvas.transform, "GameTitle", new Vector2(0f, -78f), new Vector2(900f, 86f), UITheme.Display);
        PlaceText(canvas.transform, "GameSubtitle", new Vector2(0f, -150f), new Vector2(800f, 58f), UITheme.Title);
        PlaceText(canvas.transform, "GameDescription", new Vector2(0f, -202f), new Vector2(800f, 38f), UITheme.Body);
        StyleMenuText(canvas.transform, "GameTitle", UITheme.Display, UITheme.TextPrimary, true);
        StyleMenuText(canvas.transform, "GameSubtitle", UITheme.Title, UITheme.WarmSoft, true);
        StyleMenuText(canvas.transform, "GameDescription", UITheme.Body, UITheme.TextMuted, false);

        PlaceButton(startButton, new Vector2(0f, -365f), new Vector2(360f, 84f), "开始游戏");
        UIManager.StyleWarmButton(startButton, 28);
        if (startButton != null)
        {
            Image startImage = startButton.GetComponent<Image>();
            if (startImage != null) startImage.sprite = MonsterSurvivorArtLibrary.ChamferFill(360, 84, 12f);
        }
        if (levelSelectButton == null && canvas != null)
        {
            levelSelectButton = MakeButton(canvas.transform, "LevelSelectButton", "选择关卡", Vector2.zero,
                new Vector2(260f, 60f), UITheme.SurfaceRaised);
        }
        PlaceButton(levelSelectButton, new Vector2(0f, -460f), new Vector2(260f, 60f), "选择关卡  ·  " + selectedLevel);
        StyleMenuButton(levelSelectButton, 20, UITheme.Clinical, UITheme.SurfaceRaised, 10f);
        PlaceButton(settingsButton, new Vector2(-700f, -10f), new Vector2(320f, 100f), "设置");
        PlaceButton(codexButton, new Vector2(700f, -10f), new Vector2(320f, 100f), "血管图鉴");
        StyleMenuButton(settingsButton, 24, UITheme.TextPrimary, UITheme.Surface, 12f);
        StyleMenuButton(codexButton, 24, UITheme.TextPrimary, UITheme.Surface, 12f);
        // R2-08：侧按钮升 500 字重，首视图不再读作 disabled；levelSelect 保持 400 Clinical。
        Text settingsLabel = settingsButton == null ? null : settingsButton.GetComponentInChildren<Text>();
        if (settingsLabel != null) settingsLabel.font = RuntimeFont.GetTitle();
        Text codexMenuLabel = codexButton == null ? null : codexButton.GetComponentInChildren<Text>();
        if (codexMenuLabel != null) codexMenuLabel.font = RuntimeFont.GetTitle();

        HideMenuObject(canvas.transform, "SystemTitle");
        HideMenuObject(canvas.transform, "AchievementsButton");
        HideMenuObject(canvas.transform, "QuitButton");
        PlaceBottomText(canvas.transform, "StatusText", new Vector2(0f, 22f), new Vector2(900f, 34f), 16);
        Transform statusT = FindChild(canvas.transform, "StatusText");
        if (statusT != null)
        {
            Text status = statusT.GetComponent<Text>();
            if (status != null)
            {
                status.font = RuntimeFont.Get();
                Color muted = UITheme.TextMuted; muted.a = 0.85f;
                status.color = muted;
            }
            Outline statusOutline = statusT.GetComponent<Outline>();
            if (statusOutline != null) statusOutline.enabled = false;
        }
        // R2-08：footer hairline 托住版本行（900×1，冷钢 a0.3）。
        Transform ruleT = FindChild(canvas.transform, "MenuFooterRule");
        GameObject ruleObj = ruleT == null ? new GameObject("MenuFooterRule", typeof(RectTransform), typeof(Image)) : ruleT.gameObject;
        ruleObj.transform.SetParent(canvas.transform, false);
        Image rule = ruleObj.GetComponent<Image>();
        rule.sprite = null; rule.type = Image.Type.Simple;
        rule.color = UITheme.HexA("#7A8894", 0.3f);
        rule.raycastTarget = false;
        footerRule = ruleObj.GetComponent<RectTransform>();
        footerRule.anchorMin = footerRule.anchorMax = new Vector2(0.5f, 0f);
        footerRule.pivot = new Vector2(0.5f, 0f);
        footerRule.sizeDelta = new Vector2(900f, 1f);
        footerRule.anchoredPosition = new Vector2(0f, 48f);
    }

    // ── R2-08：主菜单 staged 入场（纯 rise+fade，总时长 0.76s，不阻塞 StartGame 点击）──
    RectTransform footerRule;
    Coroutine menuEntrance;

    void Start()
    {
        if (menuEntrance != null) StopCoroutine(menuEntrance);
        menuEntrance = StartCoroutine(MenuEntrance());
    }

    System.Collections.IEnumerator MenuEntrance()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) yield break;
        Transform root = canvas.transform;
        Transform[] seq =
        {
            FindChild(root, "GameTitle"), FindChild(root, "TopAccent"),
            FindChild(root, "GameSubtitle"), FindChild(root, "GameDescription"),
            startButton == null ? null : startButton.transform,
            levelSelectButton == null ? null : levelSelectButton.transform,
            settingsButton == null ? null : settingsButton.transform,
            codexButton == null ? null : codexButton.transform,
            FindChild(root, "StatusText"), footerRule
        };
        float[] delays = { 0f, 0f, 0.08f, 0.16f, 0.24f, 0.32f, 0.40f, 0.40f, 0.48f, 0.48f };
        RectTransform[] rects = new RectTransform[seq.Length];
        CanvasGroup[] groups = new CanvasGroup[seq.Length];
        for (int i = 0; i < seq.Length; i++)
        {
            if (seq[i] == null) continue;
            rects[i] = seq[i] as RectTransform;
            groups[i] = UIMotion.EnsureGroup(seq[i].gameObject);
            groups[i].alpha = 0f;
        }
        float waited = 0f;
        for (int i = 0; i < seq.Length; i++)
        {
            if (rects[i] == null) continue;
            float wait = delays[i] - waited;
            if (wait > 0f)
            {
                yield return new WaitForSecondsRealtime(wait);
                waited = delays[i];
            }
            // Button 不依赖 alpha，入场期间点击 CTA 仍立即响应。
            StartCoroutine(UIMotion.ModalIn(rects[i], groups[i], 0.28f, 12f, 1f));
        }
        menuEntrance = null;
    }

    // ── R1 令牌化助手：面板体 / 切角框 / 网格托底 / tile / 菜单按钮 ──
    // T_Select：选中框 0.15s 插值；重入先 Stop 再启动，防协程叠加。
    readonly Dictionary<Button, Coroutine> selectMotions = new Dictionary<Button, Coroutine>();
    GameObject focusedTile;

    void TintSelect(Button button, Transform frameT, Color target)
    {
        if (button == null || frameT == null) return;
        Image frame = frameT.GetComponent<Image>();
        if (frame == null) return;
        if (frame.color == target) return;
        Coroutine old;
        if (selectMotions.TryGetValue(button, out old) && old != null) StopCoroutine(old);
        selectMotions[button] = StartCoroutine(UIMotion.Tint(frame, frame.color, target, UIMotion.SelectDur));
    }

    // R3-03：键盘焦点环临床青，与选中琥珀区分；选中优先。
    Color FrameTarget(Button button, bool chosen)
    {
        if (chosen) return UITheme.WarmAccent;
        return focusedTile == button.gameObject ? UITheme.HexA("#7FB5AD", 0.7f) : UITheme.LineCool;
    }

    void SyncFocusRing()
    {
        GameObject sel = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (sel == focusedTile) return;
        focusedTile = sel;
        if (levelButtons != null)
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] == null) continue;
                Transform frameT = levelButtons[i].transform.Find("TileFrame");
                if (frameT != null) TintSelect(levelButtons[i], frameT, FrameTarget(levelButtons[i], i + 1 == selectedLevel));
            }
        if (characterButtons != null)
            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] == null) continue;
                Transform frameT = characterButtons[i].transform.Find("TileFrame");
                if (frameT != null) TintSelect(characterButtons[i], frameT, FrameTarget(characterButtons[i], i == selectedCharacter));
            }
    }

    static void WireRowNav(Button[] tiles)
    {
        if (tiles == null) return;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = i > 0 ? tiles[i - 1] : null;
            nav.selectOnRight = i + 1 < tiles.Length ? tiles[i + 1] : null;
            tiles[i].navigation = nav;
        }
    }

    // R3-01：菜单背景颗粒层（等效 alpha≈0.02），垫在所有菜单 UI 之下、只叠砖墙。
    void EnsureMenuGrain(Canvas canvas)
    {
        if (canvas == null) return;
        Transform existing = canvas.transform.Find("MenuGrain");
        GameObject obj = existing == null ? new GameObject("MenuGrain", typeof(RectTransform), typeof(Image)) : existing.gameObject;
        obj.transform.SetParent(canvas.transform, false);
        obj.transform.SetAsFirstSibling();
        Image grain = obj.GetComponent<Image>();
        grain.sprite = MonsterSurvivorArtLibrary.FrostedNoise();
        grain.type = Image.Type.Tiled;
        grain.color = new Color(1f, 1f, 1f, 0.36f);
        grain.raycastTarget = false;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static Image EnsureChildImage(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject obj = existing == null ? new GameObject(objectName, typeof(RectTransform), typeof(Image)) : existing.gameObject;
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<Image>();
    }

    static void StylePanelBody(GameObject content, float width, float height)
    {
        int w = Mathf.RoundToInt(width), h = Mathf.RoundToInt(height);
        Image body = content.GetComponent<Image>();
        body.sprite = MonsterSurvivorArtLibrary.ChamferFill(w, h, 16f);
        body.type = Image.Type.Simple;
        body.color = UITheme.Overlay;
        Image grid = EnsureChildImage(content.transform, "ContentGrid");
        grid.sprite = MonsterSurvivorArtLibrary.GridField();
        grid.type = Image.Type.Tiled;
        grid.color = new Color(1f, 1f, 1f, 0.03f);
        grid.raycastTarget = false;
        RectTransform gridRect = grid.rectTransform;
        gridRect.anchorMin = Vector2.zero; gridRect.anchorMax = Vector2.one;
        gridRect.offsetMin = new Vector2(16f, 16f); gridRect.offsetMax = new Vector2(-16f, -16f);
        Image frame = EnsureChildImage(content.transform, "ContentFrame");
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(w, h, 16f, 1f);
        frame.type = Image.Type.Simple;
        frame.color = UITheme.LineCool;
        frame.raycastTarget = false;
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
    }

    static void StyleTile(Button button, float width, float height)
    {
        int w = Mathf.RoundToInt(width), h = Mathf.RoundToInt(height);
        Image img = button.GetComponent<Image>();
        img.sprite = MonsterSurvivorArtLibrary.ChamferFill(w, h, 12f);
        img.type = Image.Type.Simple;
        img.color = Color.white;
        button.targetGraphic = img;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor = UITheme.Surface;
        cb.highlightedColor = UITheme.SurfaceRaised;
        cb.pressedColor = UITheme.Hex("#241D17");
        cb.selectedColor = UITheme.Surface;
        cb.fadeDuration = 0.12f;
        button.colors = cb;
        Image frame = EnsureChildImage(button.transform, "TileFrame");
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(w, h, 12f, 1f);
        frame.type = Image.Type.Simple;
        frame.color = UITheme.LineCool;
        frame.raycastTarget = false;
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
    }

    static void StyleMenuButton(Button button, int labelSize, Color labelColor, Color bodyColor, float chamfer)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        int w = Mathf.RoundToInt(rect.sizeDelta.x), h = Mathf.RoundToInt(rect.sizeDelta.y);
        Image img = button.GetComponent<Image>();
        img.sprite = MonsterSurvivorArtLibrary.ChamferFill(w, h, chamfer);
        img.type = Image.Type.Simple;
        img.color = Color.white;
        button.targetGraphic = img;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor = bodyColor;
        cb.highlightedColor = bodyColor == UITheme.SurfaceRaised ? UITheme.Hex("#3A3128") : UITheme.SurfaceRaised;
        cb.pressedColor = UITheme.Hex("#241D17");
        cb.selectedColor = bodyColor;
        cb.fadeDuration = 0.12f;
        button.colors = cb;
        Image frame = EnsureChildImage(button.transform, "MenuFrame");
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(w, h, chamfer, 1f);
        frame.type = Image.Type.Simple;
        frame.color = UITheme.LineCool;
        frame.raycastTarget = false;
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.font = RuntimeFont.Get();
            label.fontSize = labelSize;
            label.color = labelColor;
            label.supportRichText = true;
        }
        frame.transform.SetAsLastSibling();
    }

    void StyleMenuText(Transform root, string objectName, int fontSize, Color color, bool medium)
    {
        Transform target = FindChild(root, objectName);
        if (target == null) return;
        Text label = target.GetComponent<Text>();
        if (label != null)
        {
            label.font = medium ? RuntimeFont.GetTitle() : RuntimeFont.Get();
            label.fontSize = fontSize;
            label.color = color;
            label.supportRichText = true;
        }
        Outline outline = target.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
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









