using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public Canvas canvas;
    public Slider XPSlider;
    public Slider HPSlider;
    public Text LevelText;
    public Text TimerText;
    public GameObject LevelUpPanel;
    public Button[] choiceButtons;

    public PlayerStats playerStats;
    public LevelUpManager levelUpManager;
    public GameManager gameManager;
    public GameObject GameOverPanel;
    public Text ResultText;
    public Button RestartButton;
    public Font displayFont;
    public Button pauseButton;
    public GameObject PausePanel;
    public Text PauseTitle;
    public Button ResumeButton;
    public Button ExitButton;
    public string mainMenuSceneName = "MainMenu";
    public bool isPausedByMenu;
    public float previousTimeScale = 1f;
    public Button speedTestButton;
    public Button invincibleTestButton;
    public Text speedTestLabel;
    public Text invincibleTestLabel;
    public bool speedSixX;
    public float testSpeedMultiplier = 6f;
    public CharacterCombat characterCombat;
    public PlayerSkillCollection playerSkillCollection;
    public RectTransform RunHudRoot;
    public Text RunStatsText;
    public GameObject VictoryPanel;
    public Text VictoryTitle;
    public Text VictorySubtitle;
    public Text VictoryStats;
    public Button VictoryRestartButton;
    public Button VictoryExitButton;

    void Awake()
    {
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        XPSlider = FindChild<Slider>(canvas.transform, "XPSlider");
        HPSlider = FindChild<Slider>(canvas.transform, "HPSlider");
        LevelText = FindChild<Text>(canvas.transform, "LevelText");
        TimerText = FindChild<Text>(canvas.transform, "TimerText");
        LevelUpPanel = FindChild<GameObject>(canvas.transform, "LevelUpPanel");

        GameOverPanel = FindChild<GameObject>(canvas.transform, "GameOverPanel");
        ResultText = FindChild<Text>(canvas.transform, "ResultText");
        RestartButton = FindChild<Button>(canvas.transform, "RestartButton");
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
        if (RestartButton != null) RestartButton.onClick.AddListener(() => { if (gameManager != null) gameManager.RestartGame(); });

        // The Windows prototype uses an installed CJK font for Chinese labels.
        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.font = displayFont;
        EnsureVictoryView();
        EnsureSurvivorHud();
        EnsurePauseView();
        EnsureTestTools();
        ApplyProgressSkin(XPSlider, new Color(0.28f, 0.72f, 0.92f, 1f));
        ApplyProgressSkin(HPSlider, new Color(0.92f, 0.32f, 0.42f, 1f));

        choiceButtons = new Button[3];
        choiceButtons[0] = FindChild<Button>(canvas.transform, "Choice1");
        choiceButtons[1] = FindChild<Button>(canvas.transform, "Choice2");
        choiceButtons[2] = FindChild<Button>(canvas.transform, "Choice3");

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.AddListener(() => Choose(index));
            }
        }

        if (LevelUpPanel != null) LevelUpPanel.SetActive(false);
    }

    void OnEnable()
    {
        playerStats = FindAnyObjectByType<PlayerStats>();
        levelUpManager = FindAnyObjectByType<LevelUpManager>();
        gameManager = GameManager.Instance;
        if (gameManager != null) gameManager.OnGameEnded += ShowGameOver;

        if (playerStats != null)
        {
            playerStats.OnHPChanged += UpdateHP;
            playerStats.OnXPChanged += UpdateXP;
        }

        if (levelUpManager != null)
        {
            levelUpManager.OnShowChoices += ShowChoices;
            levelUpManager.OnChoicesClosed += HideChoices;
        }
    }

    void Start()
    {
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (levelUpManager == null) levelUpManager = FindAnyObjectByType<LevelUpManager>();
        if (characterCombat == null) characterCombat = FindAnyObjectByType<CharacterCombat>();
        if (playerSkillCollection == null) playerSkillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        // GameManager.Awake may run after this object's OnEnable; retry the hookup here.
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
            if (gameManager != null) gameManager.OnGameEnded += ShowGameOver;
        }
        if (playerStats != null)
        {
            UpdateHP(playerStats.currentHP, playerStats.maxHP);
            UpdateXP(playerStats.currentXP, playerStats.GetXPToNextLevel(), playerStats.level);
        }
    }

    void Update()
    {
        if (characterCombat == null) characterCombat = FindAnyObjectByType<CharacterCombat>();
        if (LevelText != null && playerStats != null)
        {
            LevelText.text = "Lv." + playerStats.level;
            if (characterCombat != null)
                LevelText.text += "  " + CharacterCatalog.Name(characterCombat.role) + "·" + characterCombat.rank;
        }

        if (TimerText != null && GameManager.Instance != null)
        {
            TimerText.text = GameManager.Instance.selectedLevelName + "  " + FormatTime(GameManager.Instance.GetRemaining());
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !isPausedByMenu)
            TogglePauseMenu();
        if (Input.GetKeyDown(KeyCode.F6)) ToggleSixXSpeed();
        if (Input.GetKeyDown(KeyCode.F7)) ToggleInvincibility();
        if (VictoryPanel != null && VictoryPanel.activeSelf &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) RestartGameFromResult();
        UpdateRunHud();
        UpdateTestLabels();
    }

    void OnDisable()
    {
        if (gameManager != null) gameManager.OnGameEnded -= ShowGameOver;
        if (playerStats != null)
        {
            playerStats.OnHPChanged -= UpdateHP;
            playerStats.OnXPChanged -= UpdateXP;
        }

        if (levelUpManager != null)
        {
            levelUpManager.OnShowChoices -= ShowChoices;
            levelUpManager.OnChoicesClosed -= HideChoices;
        }
    }

    public T FindChild<T>(Transform parent, string childName) where T : class
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                if (typeof(T) == typeof(GameObject)) return child.gameObject as T;
                return child.GetComponent(typeof(T)) as T;
            }

            T nested = FindChild<T>(child, childName);
            if (nested != null) return nested;
        }

        return null;
    }

    public void UpdateHP(int current, int max)
    {
        if (HPSlider == null) return;
        HPSlider.maxValue = Mathf.Max(1, max);
        HPSlider.value = current;
    }

    public void UpdateXP(int current, int needed, int level)
    {
        if (XPSlider == null) return;
        XPSlider.maxValue = Mathf.Max(1, needed);
        XPSlider.value = current;
        if (LevelText != null) LevelText.text = "Lv." + level;
    }

    public void ShowChoices(UpgradeOption[] choices)
    {
        if (LevelUpPanel == null) return;

        LevelUpPanel.SetActive(true);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        SetTestToolsVisible(false);
        if (SkillCodexManager.Instance != null)
        {
            SkillCodexManager.Instance.SetPanel(false);
            if (SkillCodexManager.Instance.codexButton != null) SkillCodexManager.Instance.codexButton.gameObject.SetActive(false);
        }
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null) continue;

            bool active = choices != null && i < choices.Length && choices[i] != null;
            button.gameObject.SetActive(active);
            if (!active) continue;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(300f, 380f);
                buttonRect.anchoredPosition = new Vector2((i - 1) * 340f, 0f);
            }

            UpgradeOption option = choices[i];
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = FormatUpgradeCardText(option);
                label.fontSize = 19;
                label.lineSpacing = 1.35f;
                label.alignment = TextAnchor.UpperCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.supportRichText = true;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.offsetMin = new Vector2(14f, 16f);
                labelRect.offsetMax = new Vector2(-14f, 168f);
                // Subtle outline for anti-aliasing and readability.
                Outline labelOutline = label.GetComponent<Outline>();
                if (labelOutline == null) labelOutline = label.gameObject.AddComponent<Outline>();
                labelOutline.effectColor = new Color(0f, 0f, 0f, 0.35f);
                labelOutline.effectDistance = new Vector2(0.5f, 0.5f);
                labelOutline.useGraphicAlpha = false;
            }
            Image cardImage = button.GetComponent<Image>();
            if (cardImage != null)
            {
                Color accent = option.skillCard == null ? new Color(0.22f, 0.48f, 0.62f, 1f) : option.skillCard.cardColor;
                // Darker, more transparent background so icons show through.
                MonsterSurvivorArtLibrary.ApplySliced(cardImage, "ui_button_blue",
                    Color.Lerp(new Color(0.02f, 0.05f, 0.09f, 0.92f), accent, 0.25f));
            }
            AttachOptionIcon(button, MonsterSurvivorArtLibrary.SkillIcon(option.type));
        }
    }

    public string FormatUpgradeCardText(UpgradeOption option)
    {
        if (option == null) return "";
        if (playerSkillCollection == null) playerSkillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        int current = playerSkillCollection == null ? 0 : playerSkillCollection.GetLevel(option.type);
        int next = Mathf.Clamp(current + 1, 1, 3);
        string levelLine = current <= 0 ? "Lv 1" : (current >= 3 ? "Lv 3  MAX" : "Lv " + current + " → " + next);
        string shortText = VesselSkillCatalog.IsVesselSkill(option.type)
            ? VesselSkillCatalog.ShortDescriptionFor(option.type)
            : option.description;
        if (string.IsNullOrEmpty(shortText)) shortText = "战斗强化";
        return "<b><size=24>" + option.displayName + "</size></b>\n" +
            "<size=18><color=#7EC8E8>" + levelLine + "</color></size>\n" +
            "<size=17><color=#D8E8F4>" + shortText + "</color></size>";
    }

    public void AttachOptionIcon(Button button, Sprite sprite)
    {
        if (button == null) return;
        Transform existing = button.transform.Find("MonsterSurvivorIcon");
        Transform existingBacking = button.transform.Find("IconBacking");
        // Fallback: if sprite is null, create a colored circle as placeholder.
        if (sprite == null)
        {
            sprite = CreateFallbackIcon(button.GetComponent<Image>().color);
        }
        if (sprite == null)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            if (existingBacking != null) existingBacking.gameObject.SetActive(false);
            return;
        }
        GameObject backingObject = existingBacking == null ? new GameObject("IconBacking", typeof(RectTransform), typeof(Image)) : existingBacking.gameObject;
        backingObject.transform.SetParent(button.transform, false);
        Image backingImage = backingObject.GetComponent<Image>();
        MonsterSurvivorArtLibrary.ApplySliced(backingImage, "ui_square_rounded_frame", new Color(0.05f, 0.12f, 0.20f, 0.85f));
        backingImage.raycastTarget = false;
        RectTransform backingRect = backingImage.rectTransform;
        backingRect.anchorMin = backingRect.anchorMax = new Vector2(0.5f, 1f);
        backingRect.pivot = new Vector2(0.5f, 1f);
        backingRect.sizeDelta = new Vector2(128f, 128f);
        backingRect.anchoredPosition = new Vector2(0f, -52f);
        backingObject.SetActive(true);
        backingObject.transform.SetSiblingIndex(0);

        GameObject iconObject = existing == null ? new GameObject("MonsterSurvivorIcon", typeof(RectTransform), typeof(Image)) : existing.gameObject;
        iconObject.transform.SetParent(button.transform, false);
        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(104f, 104f);
        rect.anchoredPosition = new Vector2(0f, -64f);
        iconObject.SetActive(true);
        iconObject.transform.SetSiblingIndex(1);
    }

    public static Sprite CreateFallbackIcon(Color accent)
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.4f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - dist) / radius);
                pixels[y * size + x] = new Color(accent.r, accent.g, accent.b, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "FallbackIcon";
        return sprite;
    }

    public void Choose(int index)
    {
        if (levelUpManager == null || levelUpManager.currentChoices == null) return;
        if (index < 0 || index >= levelUpManager.currentChoices.Length) return;

        levelUpManager.ApplyUpgrade(levelUpManager.currentChoices[index]);
    }

    public void HideChoices()
    {
        if (LevelUpPanel != null) LevelUpPanel.SetActive(false);
        if (!isPausedByMenu && pauseButton != null && (gameManager == null || !gameManager.gameEnded))
            pauseButton.gameObject.SetActive(true);
        if (!isPausedByMenu && (gameManager == null || !gameManager.gameEnded)) SetTestToolsVisible(true);
        if (SkillCodexManager.Instance != null && SkillCodexManager.Instance.codexButton != null)
            SkillCodexManager.Instance.codexButton.gameObject.SetActive(true);
    }

    public void ShowGameOver(bool won)
    {
        isPausedByMenu = false;
        if (PausePanel != null) PausePanel.SetActive(false);
        HideChoices();
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        SetTestToolsVisible(false);
        if (won)
        {
            if (GameOverPanel != null) GameOverPanel.SetActive(false);
            ShowVictoryPanel();
        }
        else
        {
            if (VictoryPanel != null) VictoryPanel.SetActive(false);
            bool timeout = gameManager != null && gameManager.elapsedTime >= gameManager.gameDuration && !gameManager.bossDefeated;
            if (ResultText != null)
                ResultText.text = timeout ? "时间耗尽\n血管结节未被清除" : "守卫结束\n再试一次吧";
            if (GameOverPanel != null) GameOverPanel.SetActive(true);
        }
    }

    public void EnsureVictoryView()
    {
        if (canvas == null || VictoryPanel != null) return;
        VictoryPanel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(Image));
        VictoryPanel.transform.SetParent(canvas.transform, false);
        RectTransform full = VictoryPanel.GetComponent<RectTransform>();
        full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
        VictoryPanel.GetComponent<Image>().color = new Color(0.008f, 0.015f, 0.028f, 0.94f);

        GameObject card = new GameObject("VictoryCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(VictoryPanel.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(720f, 560f);
        MonsterSurvivorArtLibrary.ApplySliced(card.GetComponent<Image>(), "ui_window_background",
            new Color(0.035f, 0.075f, 0.12f, 0.99f));

        GameObject accent = new GameObject("GoldAccent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(card.transform, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f); accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f); accentRect.sizeDelta = new Vector2(0f, 8f); accentRect.anchoredPosition = Vector2.zero;
        accent.GetComponent<Image>().color = new Color(0.95f, 0.75f, 0.28f, 1f);

        VictoryTitle = CreateText(card.transform, "VictoryTitle", "胜利！", 54, TextAnchor.MiddleCenter,
            new Vector2(0f, 188f), new Vector2(620f, 78f));
        VictoryTitle.color = new Color(0.95f, 0.78f, 0.32f, 1f);
        VictorySubtitle = CreateText(card.transform, "VictorySubtitle", "血管守卫完成", 25, TextAnchor.MiddleCenter,
            new Vector2(0f, 132f), new Vector2(620f, 42f));
        VictorySubtitle.color = new Color(0.72f, 0.86f, 0.96f, 1f);

        // Separate header for the stats section.
        Text statsHeader = CreateText(card.transform, "StatsHeader", "结算报告", 22, TextAnchor.MiddleCenter,
            new Vector2(0f, 80f), new Vector2(600f, 32f));
        statsHeader.color = new Color(0.68f, 0.82f, 0.94f, 1f);

        VictoryStats = CreateText(card.transform, "VictoryStats", "", 20, TextAnchor.UpperCenter,
            new Vector2(0f, 60f), new Vector2(600f, 240f));
        VictoryStats.color = new Color(0.82f, 0.90f, 0.96f, 1f);
        VictoryStats.horizontalOverflow = HorizontalWrapMode.Wrap;

        VictoryRestartButton = CreateButton(card.transform, "VictoryRestartButton", "再来一局",
            new Vector2(-150f, -190f), new Vector2(240f, 58f), new Color(0.12f, 0.52f, 0.68f, 1f));
        VictoryRestartButton.onClick.AddListener(RestartGameFromResult);
        VictoryExitButton = CreateButton(card.transform, "VictoryExitButton", "返回主界面",
            new Vector2(150f, -190f), new Vector2(240f, 58f), new Color(0.25f, 0.15f, 0.22f, 1f));
        VictoryExitButton.onClick.AddListener(ExitToMainMenu);
        Text hint = CreateText(card.transform, "VictoryHint", "Enter / Space：再来一局",
            17, TextAnchor.MiddleCenter, new Vector2(0f, -246f), new Vector2(600f, 30f));
        hint.color = new Color(0.52f, 0.66f, 0.76f, 1f);
        VictoryPanel.SetActive(false);
    }

    public void EnsureSurvivorHud()
    {
        if (canvas == null || RunHudRoot != null) return;
        GameObject root = new GameObject("SurvivorRunHud", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        RunHudRoot = root.GetComponent<RectTransform>();
        RunHudRoot.anchorMin = RunHudRoot.anchorMax = new Vector2(0.5f, 1f);
        RunHudRoot.pivot = new Vector2(0.5f, 1f);
        RunHudRoot.sizeDelta = new Vector2(560f, 42f);
        RunHudRoot.anchoredPosition = new Vector2(0f, -18f);
        Image background = root.GetComponent<Image>();
        MonsterSurvivorArtLibrary.ApplySliced(background, "ui_square_rounded_frame",
            new Color(0.015f, 0.035f, 0.065f, 0.9f));
        background.raycastTarget = false;
        RunStatsText = CreateText(root.transform, "RunStatsText", "击败 000   ·   波次 01   ·   经验 000 / 005",
            19, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(530f, 36f));
        RunStatsText.color = new Color(0.72f, 0.86f, 0.96f, 1f);
        Outline hudOutline = RunStatsText.GetComponent<Outline>();
        if (hudOutline == null) hudOutline = RunStatsText.gameObject.AddComponent<Outline>();
        hudOutline.effectColor = new Color(0f, 0f, 0f, 0.4f);
        hudOutline.effectDistance = new Vector2(0.5f, 0.5f);
        hudOutline.useGraphicAlpha = false;
    }

    public void UpdateRunHud()
    {
        if (RunStatsText == null) return;
        GameManager run = GameManager.Instance;
        int kills = run == null ? 0 : run.enemiesDefeated;
        string wave = run == null ? "波次 01" : run.GetWaveLabel();
        int currentXp = playerStats == null ? 0 : playerStats.currentXP;
        int needed = playerStats == null ? 5 : playerStats.GetXPToNextLevel();
        RunStatsText.text = "击败 " + kills.ToString("000") + "   ·   " + wave +
            "   ·   经验 " + currentXp.ToString("000") + " / " + needed.ToString("000");
    }

    public void ShowVictoryPanel()
    {
        EnsureVictoryView();
        if (VictoryPanel == null) return;
        if (VictoryTitle != null) VictoryTitle.text = "胜利！";
        if (VictorySubtitle != null) VictorySubtitle.text = (gameManager == null ? "血管守卫完成" : gameManager.stageSummary + " · 血管守卫完成");
        int level = playerStats == null ? 1 : playerStats.level;
        int hp = playerStats == null ? 0 : playerStats.currentHP;
        int maxHp = playerStats == null ? 0 : playerStats.maxHP;
        string role = characterCombat == null ? "守卫者" : CharacterCatalog.Name(characterCombat.role);
        int skills = 0;
        if (playerSkillCollection != null)
            for (int i = 0; i < playerSkillCollection.skills.Count; i++)
                if (playerSkillCollection.skills[i] != null && playerSkillCollection.skills[i].acquired) skills++;
        int defeated = gameManager == null ? 0 : gameManager.enemiesDefeated;
        int collectedXp = gameManager == null ? 0 : gameManager.experienceCollected;
        string waveLabel = gameManager == null ? "波次 01" : gameManager.GetWaveLabel();
        float elapsed = gameManager == null ? 0f : gameManager.elapsedTime;
        string timeText = Mathf.FloorToInt(elapsed / 60f).ToString("00") + ":" + Mathf.FloorToInt(elapsed % 60f).ToString("00");
        if (VictoryStats != null)
            VictoryStats.text =
                "通关用时    " + timeText + "\n" +
                "Boss 讨伐   血管结节已清除\n" +
                "守卫角色    " + role + "\n" +
                "最终等级    Lv." + level + "\n" +
                "生命状态    " + hp + " / " + maxHp + "\n" +
                "击败敌人    " + defeated.ToString("000") + "\n" +
                "收集经验    " + collectedXp.ToString("000") + "   " + waveLabel + "\n" +
                "技能收集    " + skills + " / 10\n" +
                "关卡评价    血管防线已稳定";
        VictoryPanel.SetActive(true);
        VictoryPanel.transform.SetAsLastSibling();
    }

    public void RestartGameFromResult()
    {
        Time.timeScale = 1f;
        if (VictoryPanel != null) VictoryPanel.SetActive(false);
        if (gameManager != null) gameManager.RestartGame();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void EnsurePauseView()
    {
        if (canvas == null) return;

        pauseButton = FindChild<Button>(canvas.transform, "PauseButton");
        PausePanel = FindChild<GameObject>(canvas.transform, "PausePanel");

        if (pauseButton == null)
        {
            pauseButton = CreateButton(canvas.transform, "PauseButton", "暂停",
                new Vector2(-74f, -38f), new Vector2(116f, 52f),
                new Color(0.08f, 0.18f, 0.28f, 0.95f), new Vector2(1f, 1f));
        }
        pauseButton.onClick.RemoveListener(TogglePauseMenu);
        pauseButton.onClick.AddListener(TogglePauseMenu);

        if (PausePanel == null)
        {
            PausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            PausePanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = PausePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 320f);
            panelRect.anchoredPosition = Vector2.zero;
            PausePanel.GetComponent<Image>().color = new Color(0.015f, 0.04f, 0.08f, 0.97f);

            PauseTitle = CreateText(PausePanel.transform, "PauseTitle", "游戏暂停", 34,
                TextAnchor.MiddleCenter, new Vector2(0f, 96f), new Vector2(420f, 58f));
            PauseTitle.color = new Color(0.72f, 0.88f, 0.96f, 1f);
            ResumeButton = CreateButton(PausePanel.transform, "ResumeButton", "继续游戏",
                new Vector2(0f, 28f), new Vector2(300f, 58f),
                new Color(0.12f, 0.44f, 0.58f, 1f));
            ExitButton = CreateButton(PausePanel.transform, "ExitButton", "退出到主界面",
                new Vector2(0f, -58f), new Vector2(300f, 58f),
                new Color(0.38f, 0.16f, 0.22f, 1f));
            ResumeButton.onClick.AddListener(ResumeGame);
            ExitButton.onClick.AddListener(ExitToMainMenu);
        }

        if (PauseTitle == null) PauseTitle = FindChild<Text>(PausePanel.transform, "PauseTitle");
        if (ResumeButton == null) ResumeButton = FindChild<Button>(PausePanel.transform, "ResumeButton");
        if (ExitButton == null) ExitButton = FindChild<Button>(PausePanel.transform, "ExitButton");
        if (ResumeButton != null)
        {
            ResumeButton.onClick.RemoveListener(ResumeGame);
            ResumeButton.onClick.AddListener(ResumeGame);
        }
        if (ExitButton != null)
        {
            ExitButton.onClick.RemoveListener(ExitToMainMenu);
            ExitButton.onClick.AddListener(ExitToMainMenu);
        }

        PausePanel.SetActive(false);
    }

    public void TogglePauseMenu()
    {
        if (gameManager != null && gameManager.gameEnded) return;
        if (levelUpManager != null && levelUpManager.isChoosing) return;
        if (isPausedByMenu) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (PausePanel == null) EnsurePauseView();
        if (PausePanel == null) return;
        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        isPausedByMenu = true;
        Time.timeScale = 0f;
        PausePanel.SetActive(true);
        PausePanel.transform.SetAsLastSibling();
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
    }

    public void ResumeGame()
    {
        isPausedByMenu = false;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (pauseButton != null && (gameManager == null || !gameManager.gameEnded))
            pauseButton.gameObject.SetActive(true);
        if (gameManager == null || !gameManager.gameEnded)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    public void ExitToMainMenu()
    {
        isPausedByMenu = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void EnsureTestTools()
    {
        if (canvas == null) return;
        speedTestButton = FindChild<Button>(canvas.transform, "SpeedTestButton");
        invincibleTestButton = FindChild<Button>(canvas.transform, "InvincibleTestButton");

        if (speedTestButton == null)
        {
            speedTestButton = CreateButton(canvas.transform, "SpeedTestButton", "加速 ×6",
                new Vector2(-220f, -38f), new Vector2(126f, 52f),
                new Color(0.16f, 0.3f, 0.2f, 0.95f), new Vector2(1f, 1f));
        }
        if (invincibleTestButton == null)
        {
            invincibleTestButton = CreateButton(canvas.transform, "InvincibleTestButton", "无敌：关",
                new Vector2(-365f, -38f), new Vector2(126f, 52f),
                new Color(0.3f, 0.18f, 0.08f, 0.95f), new Vector2(1f, 1f));
        }

        speedTestLabel = speedTestButton.GetComponentInChildren<Text>(true);
        invincibleTestLabel = invincibleTestButton.GetComponentInChildren<Text>(true);
        speedTestButton.onClick.RemoveListener(ToggleSixXSpeed);
        speedTestButton.onClick.AddListener(ToggleSixXSpeed);
        invincibleTestButton.onClick.RemoveListener(ToggleInvincibility);
        invincibleTestButton.onClick.AddListener(ToggleInvincibility);
        UpdateTestLabels();
    }

    public void SetTestToolsVisible(bool visible)
    {
        if (speedTestButton != null) speedTestButton.gameObject.SetActive(visible);
        if (invincibleTestButton != null) invincibleTestButton.gameObject.SetActive(visible);
    }

    public void UpdateTestLabels()
    {
        if (speedTestLabel != null) speedTestLabel.text = speedSixX ? "速度：×6" : "加速 ×6";
        if (invincibleTestLabel != null)
        {
            bool enabled = playerStats != null && playerStats.invincible;
            invincibleTestLabel.text = enabled ? "无敌：开" : "无敌：关";
        }
    }

    public void ToggleSixXSpeed()
    {
        if (gameManager != null && gameManager.gameEnded) return;
        if (levelUpManager != null && levelUpManager.isChoosing) return;
        speedSixX = !speedSixX;
        Time.timeScale = speedSixX ? testSpeedMultiplier : 1f;
        UpdateTestLabels();
    }

    public void ToggleInvincibility()
    {
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats == null) return;
        playerStats.invincible = !playerStats.invincible;
        UpdateTestLabels();
    }

    public Button CreateButton(Transform parent, string objectName, string caption, Vector2 position,
        Vector2 size, Color color, Vector2? anchor = null)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        Vector2 chosenAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = chosenAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = obj.GetComponent<Image>();
        string buttonSkin = color.r > 0.72f && color.g > 0.5f ? "ui_button_yellow" : "ui_button_blue";
        MonsterSurvivorArtLibrary.ApplySliced(image, buttonSkin, color);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(Mathf.Min(1f, color.r + 0.14f), Mathf.Min(1f, color.g + 0.14f), Mathf.Min(1f, color.b + 0.14f), 1f);
        colors.pressedColor = new Color(Mathf.Max(0f, color.r - 0.08f), Mathf.Max(0f, color.g - 0.08f), Mathf.Max(0f, color.b - 0.08f), 1f);
        button.colors = colors;
        Text label = CreateText(obj.transform, "Label", caption, 22, TextAnchor.MiddleCenter, Vector2.zero, size - Vector2.one * 10f);
        label.color = Color.white;
        return button;
    }

    public void ApplyProgressSkin(Slider slider, Color fillColor)
    {
        if (slider == null) return;
        Image background = slider.GetComponent<Image>();
        MonsterSurvivorArtLibrary.ApplySliced(background, "ui_progress_big", new Color(0.08f, 0.12f, 0.18f, 0.92f));
        if (slider.fillRect == null) return;
        Image fill = slider.fillRect.GetComponent<Image>();
        if (fill == null) return;
        fill.sprite = MonsterSurvivorArtLibrary.LoadSingle("ui_progress_big_fill");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.color = fillColor;
    }

    public Text CreateText(Transform parent, string objectName, string value, int fontSize,
        TextAnchor alignment, Vector2 position, Vector2 size)
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
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    void OnDestroy() { if (displayFont != null) Destroy(displayFont); }

    public string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }
}

