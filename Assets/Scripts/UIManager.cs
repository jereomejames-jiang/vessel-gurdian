using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    // 温暖理性体系：暖炭底 + 琥珀/杏点缀 + 临床青数据色，令牌统一在 UITheme。
    // 质感靠构件（切角、网格、斜纹、条码）撑；渐变/噪点只铺无文字区（菜单背景层、模态顶带下 40px），
    // 等效 alpha ≤0.04——之前铺进文字区把字糊了四轮。
    const float CardChamfer = 16f;
    const float CardInset = 24f;

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
    // R2-03：HP 条右端数值（纯显示 current/max）。
    Text hpValueText;
    // R2-05：失败结算三行数据。
    Text overStatsText;
    // R2-06：胜利结算双栏 9 组 label/value（旧 VictoryStats 物体隐藏但字段保持非 null）。
    readonly Text[] victoryLabels = new Text[9];
    readonly Text[] victoryValues = new Text[9];

    // 动效句柄：重入先 Stop 再启动，防协程叠加。
    Coroutine choicesMotion;
    Coroutine dimmerMotion;
    readonly Coroutine[] cardMotions = new Coroutine[3];
    readonly Coroutine[] cardHoverMotions = new Coroutine[3];
    int lastHp = -1, lastXp = -1;
    Coroutine hpPulse, xpPulse;
    Coroutine pauseMotion;
    Coroutine overMotion;
    Coroutine victoryMotion;
    RectTransform pauseCard;
    RectTransform overCard;
    RectTransform victoryCard;
    CanvasGroup pauseGroup;
    CanvasGroup overGroup;
    CanvasGroup victoryGroup;

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
        EnsureOverView();

        // WebGL 无系统字体，统一使用随包分发的 simhei
        displayFont = RuntimeFont.Get();
        if (displayFont != null)
            foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.font = displayFont;
        EnsureVictoryView();
        EnsureSurvivorHud();
        EnsureHudPlates();
        EnsurePauseView();
        EnsureTestTools();
        ApplyProgressSkin(XPSlider, UITheme.Clinical);
        ApplyProgressSkin(HPSlider, UITheme.Danger);
        EnsureHPValue();

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
        ApplyGoldenHudLayout();
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
            string head = "Lv." + playerStats.level;
            if (characterCombat != null)
                head += "  " + CharacterCatalog.Name(characterCombat.role);
            string tail = characterCombat != null ? "· " + characterCombat.rank : "";
            LevelText.text = head + (tail.Length == 0 ? "" : "  " + UITheme.Wrap(tail, UITheme.TextMuted, UITheme.Label));
        }

        if (TimerText != null && GameManager.Instance != null)
        {
            TimerText.text = UITheme.Wrap(GameManager.Instance.selectedLevelName, UITheme.TextMuted, UITheme.Label) +
                "  " + UITheme.Wrap(FormatTime(GameManager.Instance.GetRemaining()), UITheme.TextPrimary, 26);
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
        if (hpValueText != null) hpValueText.text = current + " / " + max;
        if (current != lastHp)
        {
            lastHp = current;
            PulseBar(HPSlider, UITheme.Danger, ref hpPulse);
        }
    }

    // R2-03：HP 数值托底牌 + 右对齐数值，随 UpdateHP 同步；不碰 Slider 功能引用。
    void EnsureHPValue()
    {
        if (HPSlider == null) return;
        Transform existingPlate = HPSlider.transform.Find("HPValuePlate");
        GameObject plateObj = existingPlate == null
            ? new GameObject("HPValuePlate", typeof(RectTransform), typeof(Image)) : existingPlate.gameObject;
        plateObj.transform.SetParent(HPSlider.transform, false);
        Image plate = plateObj.GetComponent<Image>();
        plate.sprite = null; plate.type = Image.Type.Simple;
        plate.color = UITheme.HexA("#161210", 0.55f);
        plate.raycastTarget = false;
        RectTransform pr = plateObj.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(1f, 0.5f);
        pr.pivot = new Vector2(1f, 0.5f);
        pr.sizeDelta = new Vector2(118f, 20f);
        pr.anchoredPosition = new Vector2(-6f, 0f);
        Transform existingText = plateObj.transform.Find("HPValue");
        GameObject textObj = existingText == null
            ? new GameObject("HPValue", typeof(RectTransform), typeof(Text)) : existingText.gameObject;
        textObj.transform.SetParent(plateObj.transform, false);
        hpValueText = textObj.GetComponent<Text>();
        hpValueText.font = RuntimeFont.GetTitle();
        hpValueText.fontSize = 16;
        Color ink = UITheme.TextPrimary; ink.a = 0.95f;
        hpValueText.color = ink;
        hpValueText.alignment = TextAnchor.MiddleRight;
        hpValueText.raycastTarget = false;
        // 牌高 20 小于 16 号字行高：Truncate 会整行丢弃，数值必须 Overflow。
        hpValueText.verticalOverflow = VerticalWrapMode.Overflow;
        hpValueText.text = Mathf.RoundToInt(HPSlider.value) + " / " + Mathf.RoundToInt(HPSlider.maxValue);
        RectTransform tr = hpValueText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(1f, 0.5f);
        tr.pivot = new Vector2(1f, 0.5f);
        tr.sizeDelta = new Vector2(106f, 20f);
        tr.anchoredPosition = new Vector2(-12f, 0f);
    }

    public void UpdateXP(int current, int needed, int level)
    {
        if (XPSlider == null) return;
        XPSlider.maxValue = Mathf.Max(1, needed);
        XPSlider.value = current;
        if (current != lastXp)
        {
            lastXp = current;
            PulseBar(XPSlider, UITheme.Clinical, ref xpPulse);
        }
        if (LevelText != null) LevelText.text = "Lv." + level;
    }

    public void ShowChoices(UpgradeOption[] choices)
    {
        if (LevelUpPanel == null) return;

        LevelUpPanel.SetActive(true);
        EnsureLevelUpDimmer();
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
                buttonRect.sizeDelta = new Vector2(300f, 486f);
                buttonRect.anchoredPosition = new Vector2((i - 1) * 340f, 0f);
            }

            UpgradeOption option = choices[i];
            Color accent = option.skillCard == null ? new Color(0.34f, 0.56f, 0.62f, 1f) : option.skillCard.cardColor;
            EnsureCardTexts(button, out Text cardTitle, out Text cardLevel, out Text cardDesc);
            cardTitle.text = option.displayName;
            cardLevel.text = CardLevelLine(option);
            cardDesc.text = CardShortText(option);
            AttachOptionIcon(button, MonsterSurvivorArtLibrary.SkillIcon(option.type), accent);
            StyleCard(button, CurrentLevel(option), (int)option.type);
            OrderCardChildren(button);
        }
        StartChoicesMotion();
    }

    // T_ScrimIn + T_Stagger：暗场 0.16s 渐入，三卡间隔 0.06s 各自 0.24s 浮起（unscaled）。
    void StartChoicesMotion()
    {
        if (choicesMotion != null) StopCoroutine(choicesMotion);
        if (dimmerMotion != null) StopCoroutine(dimmerMotion);
        for (int i = 0; i < cardMotions.Length; i++)
            if (cardMotions[i] != null) StopCoroutine(cardMotions[i]);
        choicesMotion = StartCoroutine(ChoicesMotion());
    }

    System.Collections.IEnumerator ChoicesMotion()
    {
        Image dimmer = LevelUpPanel == null ? null : LevelUpPanel.GetComponent<Image>();
        if (dimmer != null) dimmerMotion = StartCoroutine(UIMotion.FadeImage(dimmer, 0f, UITheme.Scrim.a, UIMotion.ScrimIn));
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button b = choiceButtons[i];
            if (b == null || !b.gameObject.activeSelf) continue;
            CanvasGroup g = UIMotion.EnsureGroup(b.gameObject);
            g.alpha = 0f;
        }
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button b = choiceButtons[i];
            if (b == null || !b.gameObject.activeSelf) continue;
            if (i > 0) yield return new WaitForSecondsRealtime(UIMotion.StaggerStep);
            CanvasGroup g = UIMotion.EnsureGroup(b.gameObject);
            cardMotions[i] = StartCoroutine(UIMotion.ModalIn(b.GetComponent<RectTransform>(), g, UIMotion.CardIn, 16f, 0.94f));
        }
        choicesMotion = null;
    }

    void StopChoicesMotion()
    {
        if (choicesMotion != null) { StopCoroutine(choicesMotion); choicesMotion = null; }
        if (dimmerMotion != null) { StopCoroutine(dimmerMotion); dimmerMotion = null; }
        for (int i = 0; i < cardMotions.Length; i++)
        {
            if (cardMotions[i] != null) { StopCoroutine(cardMotions[i]); cardMotions[i] = null; }
            if (cardHoverMotions[i] != null) { StopCoroutine(cardHoverMotions[i]); cardHoverMotions[i] = null; }
            if (choiceButtons != null && i < choiceButtons.Length && choiceButtons[i] != null)
                UIMotion.Reset(choiceButtons[i].GetComponent<RectTransform>(), choiceButtons[i].GetComponent<CanvasGroup>());
        }
        if (LevelUpPanel != null)
        {
            Image dimmer = LevelUpPanel.GetComponent<Image>();
            if (dimmer != null) dimmer.color = UITheme.Scrim;
        }
    }

    // 场景里 LevelUpPanel 只有 620x300，卡各 300x486 会戳到暗场之外直接压在砖墙上。
    // 粗野主义的克制来自"整屏压到近黑 + 卡片只用硬边浮起来"，所以先把暗场铺满。
    void EnsureLevelUpDimmer()
    {
        if (LevelUpPanel == null) return;
        Image dimmer = LevelUpPanel.GetComponent<Image>();
        if (dimmer == null) dimmer = LevelUpPanel.AddComponent<Image>();
        dimmer.sprite = null;
        dimmer.type = Image.Type.Simple;
        dimmer.color = UITheme.Scrim;
        dimmer.raycastTarget = false;
        RectTransform rect = LevelUpPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // 温暖理性奖励卡：暖炭切角底 + 32px 硬网格 + 冷钢 1px 边 + 琥珀编号牌/等级条 + 暗琥珀斜纹。
    public void StyleCard(Button button, int currentLevel, int skillSerial)
    {
        RectTransform cardRect = button.GetComponent<RectTransform>();
        // 中心锚点的卡，sizeDelta 就是真实像素尺寸；此时 rect 可能还是上一帧布局的旧值。
        bool stretched = cardRect != null &&
            (cardRect.anchorMax.x - cardRect.anchorMin.x > 0.01f || cardRect.anchorMax.y - cardRect.anchorMin.y > 0.01f);
        float width = cardRect == null ? 300f : Mathf.Max(8f, stretched ? cardRect.rect.width : cardRect.sizeDelta.x);
        float height = cardRect == null ? 486f : Mathf.Max(8f, stretched ? cardRect.rect.height : cardRect.sizeDelta.y);
        float content = width - CardInset * 2f;

        // 卡体：切角实心面板。贴图按卡的像素尺寸 1:1 生成，切角才是真的 45°；
        // 之前 9-slice 的 DiagonalFrame 在 300x486 上被拉成 47x76 的钝角。
        Image body = button.GetComponent<Image>();
        if (body != null)
        {
            body.sprite = MonsterSurvivorArtLibrary.ChamferFill(Mathf.RoundToInt(width), Mathf.RoundToInt(height), CardChamfer);
            body.type = Image.Type.Simple;
            body.color = UITheme.Surface;
            body.raycastTarget = true;
        }

        // 几何网格：只在直边以内铺，越出切角会在暗场上露出方角。
        Image grid = EnsureImageChild(button.transform, "Grid");
        grid.sprite = MonsterSurvivorArtLibrary.GridField();
        grid.type = Image.Type.Tiled;
        grid.color = new Color(1f, 1f, 1f, 0.04f);
        grid.raycastTarget = false;
        StretchInset(grid.rectTransform, CardChamfer);

        // 编号牌：琥珀实心块 + 深墨字，"病历编号"的理性标注感。
        Image tag = EnsureImageChild(button.transform, "CardTag");
        Place(tag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, 22f), new Vector2(CardChamfer, -1f));
        tag.color = UITheme.WarmAccent;

        Text tagCode = EnsureCardText(button, "CardTagCode", UITheme.Tag, UITheme.InkOnWarm, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardChamfer + 8f, -1f), new Vector2(96f, 22f), RuntimeFont.GetTitle());
        tagCode.text = "SK-" + Mathf.Abs(skillSerial).ToString("00");

        // 中部硬分隔线
        Image divider = EnsureImageChild(button.transform, "CardDivider");
        Place(divider, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(content, 1f), new Vector2(0f, -272f));
        divider.color = UITheme.LineCool;

        // 底部硬分隔线
        Image footer = EnsureImageChild(button.transform, "CardFooter");
        Place(footer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(content, 1f), new Vector2(0f, -(height - 68f)));
        footer.color = UITheme.LineCool;

        // 三段等级条：filled 琥珀、满级苔绿，空槽冷钢压暗。
        for (int p = 0; p < 3; p++)
        {
            Image pip = EnsureImageChild(button.transform, "Pip" + p);
            Place(pip, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, 6f),
                new Vector2(CardInset + p * 34f, -(height - 58f)));
            bool filled = p < Mathf.Clamp(currentLevel, 0, 3);
            Color fillTone = currentLevel >= 3 ? UITheme.Success : UITheme.WarmAccent;
            pip.color = filled ? new Color(fillTone.r, fillTone.g, fillTone.b, 0.95f)
                               : new Color(UITheme.LineCool.r, UITheme.LineCool.g, UITheme.LineCool.b, 0.35f);
        }

        // 技术条码：seed 来自技能编号，每张卡的条纹都不同但恒定。先定位再贴图，Place 会清掉旧 sprite。
        Image barcode = EnsureImageChild(button.transform, "Barcode");
        Place(barcode, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(96f, 14f),
            new Vector2(-CardInset, -(height - 58f)));
        barcode.sprite = MonsterSurvivorArtLibrary.Barcode(skillSerial + 1);
        barcode.color = new Color(UITheme.LineCool.r, UITheme.LineCool.g, UITheme.LineCool.b, 0.85f);

        // 警示斜纹：暗琥珀低透明度，压在卡底作"注意"语义，不抢文字。
        Image hazard = EnsureImageChild(button.transform, "HazardBand");
        Place(hazard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(content, 12f), new Vector2(0f, -(height - 28f)));
        hazard.sprite = MonsterSurvivorArtLibrary.HazardStripes();
        hazard.type = Image.Type.Tiled;
        hazard.color = UITheme.HexA("#C9873E", 0.20f);

        // 1px 冷钢边框
        Image frame = EnsureImageChild(button.transform, "CardFrame");
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(Mathf.RoundToInt(width), Mathf.RoundToInt(height), CardChamfer, 1f);
        frame.type = Image.Type.Simple;
        StretchToParent(frame.rectTransform);
        frame.color = UITheme.LineCool;

        // 悬停：整条边框换成 2px 琥珀，按压暗琥珀；fadeDuration 统一 0.12。
        Image hover = EnsureImageChild(button.transform, "CardHover");
        hover.sprite = MonsterSurvivorArtLibrary.ChamferLine(Mathf.RoundToInt(width), Mathf.RoundToInt(height), CardChamfer, 2f);
        hover.type = Image.Type.Simple;
        StretchToParent(hover.rectTransform);
        hover.raycastTarget = false;
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = hover;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = UITheme.WarmAccent;
        colors.pressedColor = UITheme.WarmPress;
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        // R3-05：hover 微缩放 1→1.015，"拿起来"的手感；只动 scale，不碰回调。
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        EventTrigger.Entry enter = new EventTrigger.Entry();
        enter.eventID = EventTriggerType.PointerEnter;
        enter.callback.AddListener(_ => CardHoverScale(button, 1.015f));
        trigger.triggers.Add(enter);
        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener(_ => CardHoverScale(button, 1f));
        trigger.triggers.Add(exit);
    }

    void CardHoverScale(Button button, float target)
    {
        int i = choiceButtons == null ? -1 : System.Array.IndexOf(choiceButtons, button);
        if (i < 0 || i >= cardHoverMotions.Length) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null) return;
        // 入场动画还在跑时用户已 hover：入场让位于用户意图，直接落到终态再缩放。
        if (cardMotions[i] != null)
        {
            StopCoroutine(cardMotions[i]);
            cardMotions[i] = null;
            UIMotion.Reset(rect, button.GetComponent<CanvasGroup>());
        }
        if (cardHoverMotions[i] != null) StopCoroutine(cardHoverMotions[i]);
        cardHoverMotions[i] = StartCoroutine(ScaleTo(rect, target));
    }

    System.Collections.IEnumerator ScaleTo(RectTransform rect, float target)
    {
        float from = rect.localScale.x;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(from, target, UIMotion.EaseOutQuad(Mathf.Clamp01(t / 0.12f)));
            rect.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rect.localScale = new Vector3(target, target, 1f);
    }

    // 无贴图子块默认就是硬边实心矩形，只需要位置和尺寸。
    void Place(Image image, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    void StretchInset(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    public Image EnsureImageChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image existingImage = existing.GetComponent<Image>();
            if (existingImage != null)
            {
                existing.gameObject.SetActive(true);
                return existingImage;
            }
            existing.gameObject.SetActive(true);
            return existing.gameObject.AddComponent<Image>();
        }
        GameObject created = new GameObject(name, typeof(RectTransform), typeof(Image));
        created.transform.SetParent(parent, false);
        return created.GetComponent<Image>();
    }

    public static readonly string[] cardChildOrder =
    {
        "Grid", "CardTag", "CardTagCode", "HazardBand",
        "CardDivider", "CardFooter", "Pip0", "Pip1", "Pip2", "Barcode",
        "MonsterSurvivorIcon", "IconVeil", "CardTitle", "CardLevel", "CardDesc",
        "CardFrame", "CardHover"
    };

    // 上一版留下的软装饰层：噪点、渐变、扫描线、角标、图标柔光。
    // 粗野主义不认这些，直接关掉而不是留着——留着会在同一帧里和硬边构件叠在一起。
    public static readonly string[] staleCardChildren =
    {
        "FrostedNoise", "SubtleGradient", "ScanLines", "TechTop",
        "BracketTL", "BracketTR", "BracketBL", "BracketBR",
        "IconGlow", "IconBacking", "Label"
    };

    public void OrderCardChildren(Button button)
    {
        for (int i = 0; i < staleCardChildren.Length; i++)
        {
            Transform stale = button.transform.Find(staleCardChildren[i]);
            if (stale != null) stale.gameObject.SetActive(false);
        }
        for (int i = 0; i < cardChildOrder.Length; i++)
        {
            Transform child = button.transform.Find(cardChildOrder[i]);
            if (child != null) child.SetSiblingIndex(i);
        }
    }

    // 温暖理性排版：中轴对称，图标—标题—等级—描述自上而下；标题 26 Medium、等级 16 临床青、描述 18 暖沙灰。
    public void EnsureCardTexts(Button button, out Text title, out Text level, out Text desc)
    {
        title = EnsureCardText(button, "CardTitle", UITheme.CardTitle, UITheme.TextPrimary, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(252f, 40f), RuntimeFont.GetTitle());
        title.horizontalOverflow = HorizontalWrapMode.Wrap;

        level = EnsureCardText(button, "CardLevel", 16, UITheme.Clinical, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(252f, 24f));

        desc = EnsureCardText(button, "CardDesc", UITheme.Body, UITheme.TextMuted, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardInset, -288f), new Vector2(252f, 112f));
        desc.lineSpacing = 1.2f;
    }

    public Text EnsureCardText(Button button, string name, int fontSize, Color color, TextAnchor alignment,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Font font = null)
    {
        Transform existing = button.transform.Find(name);
        Text text = existing == null ? null : existing.GetComponent<Text>();
        if (text == null)
        {
            text = CreateText(button.transform, name, "", fontSize, alignment, Vector2.zero, size);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.35f);
            outline.effectDistance = new Vector2(0.5f, 0.5f);
            outline.useGraphicAlpha = false;
            outline.enabled = false; // 卡体即托底，描边只会糊字
        }
        text.gameObject.SetActive(true);
        text.supportRichText = true;
        text.font = font != null ? font : (displayFont == null ? RuntimeFont.Get() : displayFont);
        if (text.font == null)
            Debug.LogError("VesselGuard: 升级卡文字缺少字体，RuntimeFont.Get() 返回 null");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    public int CurrentLevel(UpgradeOption option)
    {
        if (playerSkillCollection == null) playerSkillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        return playerSkillCollection == null ? 0 : playerSkillCollection.GetLevel(option.type);
    }

    public string CardLevelLine(UpgradeOption option)
    {
        int current = CurrentLevel(option);
        if (current <= 0) return "Lv 1";
        if (current >= 3) return "Lv 3  MAX";
        return "Lv " + current + " → " + Mathf.Clamp(current + 1, 1, 3);
    }

    public string CardShortText(UpgradeOption option)
    {
        string shortText = VesselSkillCatalog.IsVesselSkill(option.type)
            ? VesselSkillCatalog.ShortDescriptionFor(option.type)
            : option.description;
        return string.IsNullOrEmpty(shortText) ? "战斗强化" : shortText;
    }

    public void AttachOptionIcon(Button button, Sprite sprite, Color accent)
    {
        if (button == null) return;
        Transform existing = button.transform.Find("MonsterSurvivorIcon");
        Transform staleBacking = button.transform.Find("IconBacking");
        Transform staleGlow = button.transform.Find("IconGlow");
        if (staleBacking != null) staleBacking.gameObject.SetActive(false);
        if (staleGlow != null) staleGlow.gameObject.SetActive(false);
        if (sprite == null) sprite = CreateFallbackIcon(UITheme.Clinical);
        if (sprite == null)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }

        // 极简中置图标：120px，正下方接标题。IconGlow 那圈柔光属于上一版的软装饰，已经进 stale 名单。
        GameObject iconObject = existing == null ? new GameObject("MonsterSurvivorIcon", typeof(RectTransform), typeof(Image)) : existing.gameObject;
        iconObject.transform.SetParent(button.transform, false);
        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        // R2-11：图标暖压——先乘暖纸色，再叠 IconVeil 降饱和；绿十字→苔绿、蓝轮→钢青。
        image.color = new Color(0.95f, 0.90f, 0.82f, 1f);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(120f, 120f);
        rect.anchoredPosition = new Vector2(0f, -52f);
        iconObject.SetActive(true);
        // veil 仅覆盖图标盒（sibling 在 icon 之后、文字之前），不压标题/描述对比。
        Image veil = EnsureImageChild(button.transform, "IconVeil");
        veil.sprite = null; veil.type = Image.Type.Simple;
        veil.color = UITheme.HexA("#221B16", 0.28f);
        veil.raycastTarget = false;
        RectTransform veilRect = veil.rectTransform;
        veilRect.anchorMin = veilRect.anchorMax = new Vector2(0.5f, 1f);
        veilRect.pivot = new Vector2(0.5f, 1f);
        veilRect.sizeDelta = new Vector2(120f, 120f);
        veilRect.anchoredPosition = new Vector2(0f, -52f);
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
        StopChoicesMotion();
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
                ResultText.text = timeout
                    ? "时间耗尽\n" + UITheme.Wrap("血管结节未被清除", UITheme.TextMuted, UITheme.Body)
                    : "守卫结束\n" + UITheme.Wrap("我们与你一起，再试一次吧", UITheme.WarmSoft, UITheme.Body);
            // R2-05：三行结算数据填掉鼓励语与 CTA 之间的死区（读现有字段，纯显示）。
            if (overStatsText != null)
            {
                int kills = gameManager == null ? 0 : gameManager.enemiesDefeated;
                float elapsed = gameManager == null ? 0f : gameManager.elapsedTime;
                int level = playerStats == null ? 1 : playerStats.level;
                overStatsText.text =
                    UITheme.Wrap("击败", UITheme.TextMuted, 18) + "  " + UITheme.Wrap(kills.ToString("000"), UITheme.TextPrimary, 20) + "\n" +
                    UITheme.Wrap("用时", UITheme.TextMuted, 18) + "  " + UITheme.Wrap(FormatTime(elapsed), UITheme.TextPrimary, 20) + "\n" +
                    UITheme.Wrap("等级", UITheme.TextMuted, 18) + "  " + UITheme.Wrap("Lv." + level, UITheme.TextPrimary, 20);
            }
            if (GameOverPanel != null)
            {
                GameOverPanel.SetActive(true);
                GameOverPanel.transform.SetAsLastSibling();
                StartModalMotion(GameOverPanel.GetComponent<Image>(), overCard, overGroup, ref overMotion, UIMotion.ModalInDur);
            }
        }
    }

    // 失败结算：全屏暖暗场 + 切角模态卡 + 砖红顶带（失败语义）+ 鼓励语气副行。
    void EnsureOverView()
    {
        if (canvas == null || GameOverPanel == null) return;
        RectTransform panelRect = GameOverPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        }
        Image scrim = GameOverPanel.GetComponent<Image>();
        if (scrim == null) scrim = GameOverPanel.AddComponent<Image>();
        scrim.sprite = null; scrim.type = Image.Type.Simple;
        scrim.color = UITheme.Scrim; scrim.raycastTarget = true;

        overCard = EnsureModalCard(GameOverPanel.transform, "OverCard", 680f, 400f, UITheme.Danger);
        overGroup = UIMotion.EnsureGroup(overCard.gameObject);

        if (ResultText != null)
        {
            ResultText.transform.SetParent(overCard, false);
            ResultText.font = RuntimeFont.GetTitle();
            ResultText.fontSize = UITheme.Title;
            ResultText.color = UITheme.TextPrimary;
            ResultText.supportRichText = true;
            ResultText.alignment = TextAnchor.MiddleCenter;
            ResultText.lineSpacing = 1.2f;
            RectTransform rr = ResultText.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = new Vector2(600f, 120f);
            rr.anchoredPosition = new Vector2(0f, 90f);
            Outline ro = ResultText.GetComponent<Outline>();
            if (ro != null) ro.enabled = false;
        }
        // R2-05：OverStats 三行数据列，位于 ResultText 与 RestartButton 之间。
        Transform existingStats = overCard.Find("OverStats");
        GameObject statsObj = existingStats == null
            ? new GameObject("OverStats", typeof(RectTransform), typeof(Text)) : existingStats.gameObject;
        statsObj.transform.SetParent(overCard, false);
        overStatsText = statsObj.GetComponent<Text>();
        overStatsText.font = RuntimeFont.Get();
        overStatsText.fontSize = 18;
        overStatsText.color = UITheme.TextMuted;
        overStatsText.alignment = TextAnchor.MiddleCenter;
        overStatsText.lineSpacing = 1.2f;
        overStatsText.supportRichText = true;
        overStatsText.raycastTarget = false;
        // 三行总高约 106 > 牌高 100：Overflow 保证第三行不被 Truncate 裁掉。
        overStatsText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform sr = overStatsText.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.sizeDelta = new Vector2(520f, 100f);
        sr.anchoredPosition = new Vector2(0f, -24f);
        if (RestartButton != null)
        {
            RestartButton.transform.SetParent(overCard, false);
            RectTransform br = RestartButton.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(260f, 58f);
            br.anchoredPosition = new Vector2(0f, -140f);
            StyleWarmButton(RestartButton, 22);
        }
    }

    // 模态卡统一构件：切角 Overlay 体 + 1px 冷钢框 + 4px 语义顶带。
    RectTransform EnsureModalCard(Transform parent, string name, float width, float height, Color bandColor)
    {
        Transform existing = parent.Find(name);
        GameObject card = existing == null ? new GameObject(name, typeof(RectTransform), typeof(Image)) : existing.gameObject;
        card.transform.SetParent(parent, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(width, height);
        cardRect.anchoredPosition = Vector2.zero;
        Image body = card.GetComponent<Image>();
        body.sprite = MonsterSurvivorArtLibrary.ChamferFill(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 16f);
        body.type = Image.Type.Simple;
        body.color = UITheme.Overlay;
        body.raycastTarget = true;
        Image frame = EnsureImageChild(card.transform, name + "Frame");
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 16f, 1f);
        frame.type = Image.Type.Simple;
        StretchToParent(frame.rectTransform);
        frame.color = UITheme.LineCool;
        frame.raycastTarget = false;
        Image band = EnsureImageChild(card.transform, name + "TopBand");
        band.sprite = null; band.type = Image.Type.Simple;
        band.color = bandColor; band.raycastTarget = false;
        RectTransform bandRect = band.rectTransform;
        bandRect.anchorMin = new Vector2(0f, 1f); bandRect.anchorMax = new Vector2(1f, 1f);
        bandRect.pivot = new Vector2(0.5f, 1f);
        bandRect.sizeDelta = new Vector2(-32f, 4f);
        bandRect.anchoredPosition = Vector2.zero;
        // R3-02：顶带下 40px 无文字区的受光微渐变（等效 alpha≈0.04），给模态一点深度。
        Image glow = EnsureImageChild(card.transform, name + "HeadGlow");
        glow.sprite = MonsterSurvivorArtLibrary.SubtleGradient();
        glow.type = Image.Type.Simple;
        glow.color = new Color(1f, 1f, 1f, 0.33f);
        glow.raycastTarget = false;
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = new Vector2(0f, 1f); glowRect.anchorMax = new Vector2(1f, 1f);
        glowRect.pivot = new Vector2(0.5f, 1f);
        glowRect.sizeDelta = new Vector2(-32f, 40f);
        glowRect.anchoredPosition = new Vector2(0f, -4f);
        return cardRect;
    }

    // T_ScrimIn + T_ModalIn（unscaled：暂停/结算时 timeScale 可能为 0）。
    void StartModalMotion(Image scrim, RectTransform card, CanvasGroup group, ref Coroutine handle, float dur)
    {
        if (handle != null) StopCoroutine(handle);
        if (scrim != null) StartCoroutine(UIMotion.FadeImage(scrim, 0f, UITheme.Scrim.a, UIMotion.ScrimIn));
        UIMotion.Reset(card, group);
        handle = StartCoroutine(UIMotion.ModalIn(card, group, dur, 12f));
    }

    // 主 CTA：琥珀体 + 深墨字；ColorTint 用 normal 承载体色，hover/press 才是令牌色。
    public static void StyleWarmButton(Button b, int labelSize)
    {
        if (b == null) return;
        Image img = b.GetComponent<Image>();
        RectTransform r = b.GetComponent<RectTransform>();
        if (img != null)
        {
            img.sprite = MonsterSurvivorArtLibrary.ChamferFill(Mathf.RoundToInt(r.sizeDelta.x), Mathf.RoundToInt(r.sizeDelta.y), 10f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = b.colors;
        cb.normalColor = UITheme.WarmAccent;
        cb.highlightedColor = UITheme.WarmHover;
        cb.pressedColor = UITheme.WarmPress;
        cb.selectedColor = UITheme.WarmAccent;
        cb.disabledColor = UITheme.HexA("#D9A05B", 0.4f);
        cb.fadeDuration = 0.12f;
        b.colors = cb;
        Text label = b.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.font = RuntimeFont.GetTitle();
            label.fontSize = labelSize;
            label.color = UITheme.InkOnWarm;
            label.supportRichText = true;
        }
    }

    // 次按钮：暖炭抬升体 + 1px 冷钢切角框 + 暖沙灰字。
    public static void StyleQuietButton(Button b, int labelSize, Color labelColor)
    {
        if (b == null) return;
        Image img = b.GetComponent<Image>();
        RectTransform r = b.GetComponent<RectTransform>();
        if (img != null)
        {
            img.sprite = MonsterSurvivorArtLibrary.ChamferFill(Mathf.RoundToInt(r.sizeDelta.x), Mathf.RoundToInt(r.sizeDelta.y), 10f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = b.colors;
        cb.normalColor = UITheme.SurfaceRaised;
        cb.highlightedColor = UITheme.Hex("#3A3128");
        cb.pressedColor = UITheme.Hex("#241D17");
        cb.selectedColor = UITheme.SurfaceRaised;
        cb.disabledColor = UITheme.HexA("#2C241D", 0.4f);
        cb.fadeDuration = 0.12f;
        b.colors = cb;
        Transform frameT = b.transform.Find("QuietFrame");
        Image frame = frameT == null ? null : frameT.GetComponent<Image>();
        if (frame == null)
        {
            GameObject fo = new GameObject("QuietFrame", typeof(RectTransform), typeof(Image));
            fo.transform.SetParent(b.transform, false);
            frame = fo.GetComponent<Image>();
        }
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(Mathf.RoundToInt(r.sizeDelta.x), Mathf.RoundToInt(r.sizeDelta.y), 10f, 1f);
        frame.type = Image.Type.Simple;
        frame.color = UITheme.LineCool;
        frame.raycastTarget = false;
        RectTransform fr = frame.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        frame.transform.SetAsLastSibling();
        Text label = b.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.font = RuntimeFont.Get();
            label.fontSize = labelSize;
            label.color = labelColor;
            label.supportRichText = true;
        }
    }

    public void EnsureVictoryView()
    {
        if (canvas == null || VictoryPanel != null) return;
        VictoryPanel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(Image));
        VictoryPanel.transform.SetParent(canvas.transform, false);
        RectTransform full = VictoryPanel.GetComponent<RectTransform>();
        full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
        Image scrim = VictoryPanel.GetComponent<Image>();
        scrim.sprite = null; scrim.type = Image.Type.Simple;
        scrim.color = UITheme.Scrim; scrim.raycastTarget = true;

        // 切角暖模态取代圆角窗贴；顶带 4px 琥珀。
        victoryCard = EnsureModalCard(VictoryPanel.transform, "VictoryCard", 840f, 520f, UITheme.WarmAccent);
        victoryGroup = UIMotion.EnsureGroup(victoryCard.gameObject);
        Transform card = victoryCard;

        VictoryTitle = CreateText(card, "VictoryTitle", "胜利！", 44, TextAnchor.MiddleCenter,
            new Vector2(0f, 206f), new Vector2(700f, 66f));
        VictoryTitle.font = RuntimeFont.GetTitle();
        VictoryTitle.color = UITheme.WarmSoft;
        VictorySubtitle = CreateText(card, "VictorySubtitle", "血管守卫完成", 20, TextAnchor.MiddleCenter,
            new Vector2(0f, 156f), new Vector2(700f, 30f));
        VictorySubtitle.color = UITheme.TextMuted;

        Text statsHeader = CreateText(card, "StatsHeader", "结算报告", 16, TextAnchor.MiddleCenter,
            new Vector2(0f, 116f), new Vector2(600f, 24f));
        statsHeader.font = RuntimeFont.GetTitle();
        statsHeader.color = UITheme.Clinical;
        Image headerLine = EnsureImageChild(card, "StatsHeaderLine");
        headerLine.sprite = null; headerLine.type = Image.Type.Simple;
        headerLine.color = UITheme.LineCool; headerLine.raycastTarget = false;
        PlaceHudRect(headerLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(600f, 1f));

        VictoryStats = CreateText(card, "VictoryStats", "", UITheme.Body, TextAnchor.UpperCenter,
            new Vector2(0f, -33f), new Vector2(600f, 262f));
        VictoryStats.color = UITheme.TextMuted;
        VictoryStats.lineSpacing = 1.1f;
        VictoryStats.supportRichText = true;
        VictoryStats.horizontalOverflow = HorizontalWrapMode.Wrap;
        // R2-06：旧单栏物体隐藏但字段保持非 null（防外部引用）；改用双栏 StatsRows。
        VictoryStats.gameObject.SetActive(false);
        GameObject rowsObj = new GameObject("StatsRows", typeof(RectTransform));
        rowsObj.transform.SetParent(card, false);
        StretchToParent(rowsObj.GetComponent<RectTransform>());
        string[] rowNames = { "通关用时", "Boss 讨伐", "守卫角色", "最终等级", "生命状态", "击败敌人", "收集经验", "技能收集", "关卡评价" };
        for (int i = 0; i < 9; i++)
        {
            float y = 76f - i * 25f;
            victoryLabels[i] = CreateText(rowsObj.transform, "StatsLabel" + i, rowNames[i], 18, TextAnchor.MiddleLeft,
                new Vector2(-170f, y), new Vector2(260f, 24f));
            victoryLabels[i].font = RuntimeFont.Get();
            victoryLabels[i].color = UITheme.TextMuted;
            // 行牌 24 小于 18/20 号字行高：Truncate 会整行丢弃。
            victoryLabels[i].verticalOverflow = VerticalWrapMode.Overflow;
            victoryValues[i] = CreateText(rowsObj.transform, "StatsValue" + i, "", 20, TextAnchor.MiddleRight,
                new Vector2(170f, y), new Vector2(260f, 24f));
            victoryValues[i].font = RuntimeFont.GetTitle();
            victoryValues[i].color = UITheme.TextPrimary;
            victoryValues[i].supportRichText = true;
            victoryValues[i].verticalOverflow = VerticalWrapMode.Overflow;
        }

        VictoryRestartButton = CreateButton(card, "VictoryRestartButton", "再来一局",
            new Vector2(-150f, -198f), new Vector2(240f, 56f), Color.white);
        StyleWarmButton(VictoryRestartButton, 22);
        VictoryRestartButton.onClick.AddListener(RestartGameFromResult);
        VictoryExitButton = CreateButton(card, "VictoryExitButton", "返回主界面",
            new Vector2(150f, -198f), new Vector2(240f, 56f), Color.white);
        StyleQuietButton(VictoryExitButton, 20, UITheme.TextMuted);
        VictoryExitButton.onClick.AddListener(ExitToMainMenu);
        Text hint = CreateText(card, "VictoryHint", "Enter / Space：再来一局",
            14, TextAnchor.MiddleCenter, new Vector2(0f, -244f), new Vector2(600f, 22f));
        hint.color = UITheme.TextMuted;
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
        // 状态条是信息密度最高的 HUD：一块暖炭硬板 + 1px 冷钢边，不用大圆角胶囊。
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.color = UITheme.HexA("#161210", 0.92f);
        background.raycastTarget = false;
        Image hudFrame = EnsureImageChild(root.transform, "HudFrame");
        // HUD 固定 618x48（ApplyGoldenHudLayout 里定的），贴图按同尺寸生成，切角才不变形。
        hudFrame.sprite = MonsterSurvivorArtLibrary.ChamferLine(618, 48, 10f, 1f);
        hudFrame.type = Image.Type.Simple;
        hudFrame.color = UITheme.LineCool;
        hudFrame.raycastTarget = false;
        StretchToParent(hudFrame.rectTransform);
        Image hudAccent = EnsureImageChild(root.transform, "HudAccent");
        hudAccent.sprite = null;
        hudAccent.type = Image.Type.Simple;
        hudAccent.color = new Color(UITheme.Clinical.r, UITheme.Clinical.g, UITheme.Clinical.b, 0.85f);
        hudAccent.raycastTarget = false;
        RectTransform accentRect = hudAccent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(3f, -8f);
        accentRect.anchoredPosition = new Vector2(5f, 0f);
        RunStatsText = CreateText(root.transform, "RunStatsText", "击败 000   ·   波次 01   ·   经验 000 / 005",
            UITheme.Label, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(530f, 36f));
        RunStatsText.color = UITheme.TextMuted;
        RunStatsText.supportRichText = true;
        Outline hudOutline = RunStatsText.GetComponent<Outline>();
        if (hudOutline != null) hudOutline.enabled = false;
    }

    // HUD 浮字托底：LevelPlate 的 sibling 顺序在 LevelText 之前，暖炭底托住字；
    // 有托底后删黑 Outline（描边曾经糊字）。
    void EnsureHudPlates()
    {
        if (LevelText != null)
        {
            LevelText.font = RuntimeFont.GetTitle();
            LevelText.fontSize = 22;
            LevelText.color = UITheme.TextPrimary;
            LevelText.supportRichText = true;
            LevelText.alignment = TextAnchor.MiddleLeft;
            Outline lo = LevelText.GetComponent<Outline>();
            if (lo != null) lo.enabled = false;
            Transform parent = LevelText.transform.parent;
            if (parent != null)
            {
                Transform plate = parent.Find("LevelPlate");
                Image plateImage;
                if (plate == null)
                {
                    GameObject go = new GameObject("LevelPlate", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(parent, false);
                    go.transform.SetSiblingIndex(LevelText.transform.GetSiblingIndex());
                    plateImage = go.GetComponent<Image>();
                }
                else plateImage = plate.GetComponent<Image>();
                if (plateImage != null)
                {
                    plateImage.sprite = MonsterSurvivorArtLibrary.ChamferFill(232, 44, 8f);
                    plateImage.type = Image.Type.Simple;
                    plateImage.color = UITheme.HexA("#221B16", 0.85f);
                    plateImage.raycastTarget = false;
                    RectTransform pr = plateImage.rectTransform;
                    pr.anchorMin = pr.anchorMax = new Vector2(0f, 1f);
                    pr.pivot = new Vector2(0f, 1f);
                    pr.sizeDelta = new Vector2(232f, 44f);
                    pr.anchoredPosition = new Vector2(24f, -22f);
                }
            }
        }
        if (TimerText != null)
        {
            TimerText.font = RuntimeFont.Get();
            TimerText.fontSize = UITheme.Label;
            TimerText.color = UITheme.TextMuted;
            TimerText.supportRichText = true;
            TimerText.alignment = TextAnchor.MiddleCenter;
            Outline to = TimerText.GetComponent<Outline>();
            if (to != null) to.enabled = false;
        }
    }

    void ApplyGoldenHudLayout()
    {
        if (canvas == null) return;
        const float phi = 1.618034f;

        PlaceHudRect(LevelText == null ? null : LevelText.rectTransform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(232f, 44f));
        PlaceHudRect(RunHudRoot,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(618f, 48f));
        PlaceHudRect(HPSlider == null ? null : HPSlider.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(618f, 16f));
        PlaceHudRect(TimerText == null ? null : TimerText.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(340f, 40f));

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float referenceWidth = canvasRect == null ? 1920f : canvasRect.rect.width;
        if (referenceWidth < 100f) referenceWidth = 1920f;
        float bottomBarWidth = Mathf.Min(1180f, referenceWidth * 0.618034f);
        PlaceHudRect(XPSlider == null ? null : XPSlider.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(bottomBarWidth, 16f));

        PlaceHudRect(pauseButton == null ? null : pauseButton.GetComponent<RectTransform>(),
            Vector2.one, Vector2.one, new Vector2(-82f, -38f), new Vector2(126f, 52f));
        PlaceHudRect(speedTestButton == null ? null : speedTestButton.GetComponent<RectTransform>(),
            Vector2.one, Vector2.one, new Vector2(-222f, -38f), new Vector2(126f, 52f));
        PlaceHudRect(invincibleTestButton == null ? null : invincibleTestButton.GetComponent<RectTransform>(),
            Vector2.one, Vector2.one, new Vector2(-362f, -38f), new Vector2(126f, 52f));
        Button codex = FindChild<Button>(canvas.transform, "CodexButton");
        PlaceHudRect(codex == null ? null : codex.GetComponent<RectTransform>(),
            Vector2.one, Vector2.one, new Vector2(-126f, -116f), new Vector2(220f, 54f));

        // Upgrade cards keep a 1:φ portrait silhouette; the result card uses φ landscape.
        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                RectTransform rect = choiceButtons[i].GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = new Vector2(300f, 300f * phi);
            }
        }
    }

    void PlaceHudRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    public void UpdateRunHud()
    {
        if (RunStatsText == null) return;
        GameManager run = GameManager.Instance;
        int kills = run == null ? 0 : run.enemiesDefeated;
        string wave = run == null ? "波次 01" : run.GetWaveLabel();
        int currentXp = playerStats == null ? 0 : playerStats.currentXP;
        int needed = playerStats == null ? 5 : playerStats.GetXPToNextLevel();
        // 数据层级：字段名 15 暖沙灰 / 数值 20 暖纸白 / 分隔符压到 LineCool 叠 Deep 的等效实色，不抢字段名。
        string sep = UITheme.Wrap("  ·  ", UITheme.Hex("#4D5359"));
        string waveRich;
        int space = wave.IndexOf(' ');
        if (space > 0 && space < wave.Length - 1)
            waveRich = UITheme.Wrap(wave.Substring(0, space), UITheme.TextMuted, UITheme.Label) + " " +
                       UITheme.Wrap(wave.Substring(space + 1), UITheme.TextPrimary, UITheme.Value);
        else waveRich = UITheme.Wrap(wave, UITheme.TextPrimary, UITheme.Value);
        RunStatsText.text =
            UITheme.Wrap("击败", UITheme.TextMuted, UITheme.Label) + " " + UITheme.Wrap(kills.ToString("000"), UITheme.TextPrimary, UITheme.Value) + sep +
            waveRich + sep +
            UITheme.Wrap("经验", UITheme.TextMuted, UITheme.Label) + " " + UITheme.Wrap(currentXp.ToString("000"), UITheme.TextPrimary, UITheme.Value) +
            UITheme.Wrap(" / ", UITheme.TextMuted, UITheme.Label) + UITheme.Wrap(needed.ToString("000"), UITheme.TextPrimary, UITheme.Value);
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
        // R2-06：双栏 9 组，文案与数值格式逐字不变（含 "x / 10" 与 Clinical/WarmSoft 包裹）。
        if (victoryValues[0] != null)
        {
            victoryValues[0].text = UITheme.Wrap(timeText, UITheme.TextPrimary);
            victoryValues[1].text = UITheme.Wrap("血管结节已清除", UITheme.TextPrimary);
            victoryValues[2].text = UITheme.Wrap(role, UITheme.TextPrimary);
            victoryValues[3].text = UITheme.Wrap("Lv." + level, UITheme.TextPrimary);
            victoryValues[4].text = UITheme.Wrap(hp + " / " + maxHp, UITheme.TextPrimary);
            victoryValues[5].text = UITheme.Wrap(defeated.ToString("000"), UITheme.TextPrimary);
            victoryValues[6].text = UITheme.Wrap(collectedXp.ToString("000"), UITheme.TextPrimary) + "   " + UITheme.Wrap(waveLabel, UITheme.Clinical);
            victoryValues[7].text = UITheme.Wrap(skills + " / 10", UITheme.TextPrimary);
            victoryValues[8].text = UITheme.Wrap("血管防线已稳定", UITheme.WarmSoft);
        }
        VictoryPanel.SetActive(true);
        VictoryPanel.transform.SetAsLastSibling();
        StartModalMotion(VictoryPanel.GetComponent<Image>(), victoryCard, victoryGroup, ref victoryMotion, UIMotion.ModalInVictory);
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
        StyleQuietButton(pauseButton, 20, UITheme.TextPrimary);

        if (PausePanel == null)
        {
            PausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            PausePanel.transform.SetParent(canvas.transform, false);
        }
        // 暂停模态：全屏暖暗场（四周 gameplay 压暗）+ 居中切角卡。
        RectTransform panelRect = PausePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        Image scrim = PausePanel.GetComponent<Image>();
        if (scrim == null) scrim = PausePanel.AddComponent<Image>();
        scrim.sprite = null; scrim.type = Image.Type.Simple;
        scrim.color = UITheme.Scrim; scrim.raycastTarget = true;

        pauseCard = EnsureModalCard(PausePanel.transform, "PauseCard", 520f, 360f, UITheme.WarmAccent);
        pauseGroup = UIMotion.EnsureGroup(pauseCard.gameObject);

        PauseTitle = FindChild<Text>(PausePanel.transform, "PauseTitle");
        if (PauseTitle == null)
            PauseTitle = CreateText(pauseCard, "PauseTitle", "游戏暂停", UITheme.Title,
                TextAnchor.MiddleCenter, new Vector2(0f, 120f), new Vector2(420f, 44f));
        else PauseTitle.transform.SetParent(pauseCard, false);
        PauseTitle.font = RuntimeFont.GetTitle();
        PauseTitle.fontSize = UITheme.Title;
        PauseTitle.color = UITheme.TextPrimary;
        PauseTitle.supportRichText = true;
        PlaceHudRect(PauseTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(420f, 44f));
        Outline ptOutline = PauseTitle.GetComponent<Outline>();
        if (ptOutline != null) ptOutline.enabled = false;

        Text subtitle = FindChild<Text>(pauseCard, "PauseSubtitle");
        if (subtitle == null)
            subtitle = CreateText(pauseCard, "PauseSubtitle", "深呼吸，慢慢来", UITheme.Body,
                TextAnchor.MiddleCenter, new Vector2(0f, 84f), new Vector2(420f, 30f));
        subtitle.font = RuntimeFont.GetTitle();
        subtitle.fontSize = UITheme.Body;
        subtitle.color = UITheme.WarmSoft;
        PlaceHudRect(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 84f), new Vector2(420f, 30f));

        ResumeButton = FindChild<Button>(PausePanel.transform, "ResumeButton");
        if (ResumeButton == null)
            ResumeButton = CreateButton(pauseCard, "ResumeButton", "继续游戏",
                new Vector2(0f, 10f), new Vector2(300f, 58f), Color.white);
        else ResumeButton.transform.SetParent(pauseCard, false);
        PlaceHudRect(ResumeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(300f, 58f));
        StyleWarmButton(ResumeButton, 22);

        ExitButton = FindChild<Button>(PausePanel.transform, "ExitButton");
        if (ExitButton == null)
            ExitButton = CreateButton(pauseCard, "ExitButton", "退出到主界面",
                new Vector2(0f, -76f), new Vector2(300f, 58f), Color.white);
        else ExitButton.transform.SetParent(pauseCard, false);
        PlaceHudRect(ExitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(300f, 58f));
        StyleQuietButton(ExitButton, 20, UITheme.TextMuted);
        // R2-05：Exit 危险语义——砖红半透明细框，字色保持 TextMuted 不升级。
        Transform exitFrame = ExitButton.transform.Find("QuietFrame");
        if (exitFrame != null) exitFrame.GetComponent<Image>().color = UITheme.HexA("#C4574E", 0.5f);

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
        StartModalMotion(PausePanel.GetComponent<Image>(), pauseCard, pauseGroup, ref pauseMotion, UIMotion.ModalInDur);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
    }

    public void ResumeGame()
    {
        isPausedByMenu = false;
        if (pauseMotion != null) { StopCoroutine(pauseMotion); pauseMotion = null; }
        UIMotion.Reset(pauseCard, pauseGroup);
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

        // R2-03：测试按钮统一 Quiet 切角 chip（16px 标注），再压成半透明体；几何 126×52 不变。
        StyleQuietButton(speedTestButton, 16, UITheme.TextMuted);
        StyleQuietButton(invincibleTestButton, 16, UITheme.TextMuted);
        StyleTestChip(speedTestButton);
        StyleTestChip(invincibleTestButton);

        speedTestLabel = speedTestButton.GetComponentInChildren<Text>(true);
        invincibleTestLabel = invincibleTestButton.GetComponentInChildren<Text>(true);
        speedTestButton.onClick.RemoveListener(ToggleSixXSpeed);
        speedTestButton.onClick.AddListener(ToggleSixXSpeed);
        invincibleTestButton.onClick.RemoveListener(ToggleInvincibility);
        invincibleTestButton.onClick.AddListener(ToggleInvincibility);
        UpdateTestLabels();
    }

    static void StyleTestChip(Button b)
    {
        if (b == null) return;
        ColorBlock cb = b.colors;
        cb.normalColor = UITheme.HexA("#221B16", 0.6f);
        cb.highlightedColor = UITheme.HexA("#2C241D", 0.75f);
        cb.pressedColor = UITheme.HexA("#241D17", 0.8f);
        cb.selectedColor = cb.normalColor;
        cb.fadeDuration = 0.12f;
        b.colors = cb;
        Transform frameT = b.transform.Find("QuietFrame");
        if (frameT != null) frameT.GetComponent<Image>().color = UITheme.HexA("#7A8894", 0.55f);
    }

    public void SetTestToolsVisible(bool visible)
    {
        if (speedTestButton != null) speedTestButton.gameObject.SetActive(visible);
        if (invincibleTestButton != null) invincibleTestButton.gameObject.SetActive(visible);
    }

    public void UpdateTestLabels()
    {
        // R2-03：状态语义色——加速开=临床青、无敌开=琥珀警示；文案字符串不变。
        if (speedTestLabel != null)
        {
            speedTestLabel.text = speedSixX ? "速度：×6" : "加速 ×6";
            speedTestLabel.color = speedSixX ? UITheme.Clinical : UITheme.TextMuted;
        }
        if (speedTestButton != null)
        {
            Transform frameT = speedTestButton.transform.Find("QuietFrame");
            if (frameT != null)
                frameT.GetComponent<Image>().color = speedSixX ? UITheme.HexA("#7FB5AD", 0.8f) : UITheme.HexA("#7A8894", 0.55f);
        }
        if (invincibleTestLabel != null)
        {
            bool enabled = playerStats != null && playerStats.invincible;
            invincibleTestLabel.text = enabled ? "无敌：开" : "无敌：关";
            invincibleTestLabel.color = enabled ? UITheme.Warning : UITheme.TextMuted;
        }
        if (invincibleTestButton != null)
        {
            bool enabled = playerStats != null && playerStats.invincible;
            Transform frameT = invincibleTestButton.transform.Find("QuietFrame");
            if (frameT != null)
                frameT.GetComponent<Image>().color = enabled ? UITheme.HexA("#C9873E", 0.8f) : UITheme.HexA("#7A8894", 0.55f);
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
        // R2-03 根因：默认皮肤统一暖炭切角 chip，删除旧 ui_button_yellow/blue sliced 与派生色。
        image.sprite = MonsterSurvivorArtLibrary.ChamferFill(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 10f);
        image.type = Image.Type.Simple;
        image.color = Color.white;
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = UITheme.SurfaceRaised;
        colors.highlightedColor = UITheme.Hex("#3A3128");
        colors.pressedColor = UITheme.Hex("#241D17");
        colors.selectedColor = UITheme.SurfaceRaised;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        Text label = CreateText(obj.transform, "Label", caption, 22, TextAnchor.MiddleCenter, Vector2.zero, size - Vector2.one * 10f);
        label.color = UITheme.TextPrimary;
        return button;
    }

    public void ApplyProgressSkin(Slider slider, Color fillColor)
    {
        if (slider == null) return;
        // 场景里的 HPSlider/XPSlider 自己身上没有 Image，轨道是子物体 Background；
        // 老代码把 null 传给 ApplySliced，被它内部的判空静默吞掉了，所以一直没炸。
        Image track = slider.GetComponent<Image>();
        if (track == null)
        {
            Transform trackChild = slider.transform.Find("Background");
            if (trackChild != null) track = trackChild.GetComponent<Image>();
        }
        if (track != null)
        {
            // 原来的 ui_progress_big 是 s/7 大圆角面板，放在 16px 高的条上就成了胶囊，像十年前的手游。
            // Slider 会自己缩放 fillRect，所以空槽直接由这块直边深色底当轨道即可。
            track.sprite = null;
            track.type = Image.Type.Simple;
            track.color = UITheme.HexA("#161210", 0.9f);
        }
        Image fill = slider.fillRect == null ? null : slider.fillRect.GetComponent<Image>();
        if (fill == null) return;
        fill.sprite = MonsterSurvivorArtLibrary.BarFill();
        fill.type = Image.Type.Simple;
        fill.color = fillColor;
        fill.raycastTarget = false;
        // 顶边 1px 亮线：把整块条从"色块"变成"构件"。
        Image edge = EnsureImageChild(slider.transform, "BarTopLine");
        edge.sprite = null;
        edge.type = Image.Type.Simple;
        edge.color = UITheme.LineCool;
        edge.raycastTarget = false;
        RectTransform edgeRect = edge.rectTransform;
        edgeRect.anchorMin = new Vector2(0f, 1f);
        edgeRect.anchorMax = new Vector2(1f, 1f);
        edgeRect.pivot = new Vector2(0.5f, 1f);
        edgeRect.sizeDelta = new Vector2(0f, 1f);
        edgeRect.anchoredPosition = Vector2.zero;
    }

    // R3-04：数值变化时顶线闪一下填充色再落回钢灰，给"条在回应你"的确认感；静止不闪。
    void PulseBar(Slider slider, Color fillColor, ref Coroutine handle)
    {
        if (slider == null) return;
        Transform edgeT = slider.transform.Find("BarTopLine");
        Image edge = edgeT == null ? null : edgeT.GetComponent<Image>();
        if (edge == null) return;
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(PulseBarTop(edge, fillColor));
    }

    System.Collections.IEnumerator PulseBarTop(Image edge, Color fillColor)
    {
        Color from = new Color(fillColor.r, fillColor.g, fillColor.b, 0.9f);
        Color to = UITheme.LineCool;
        float t = 0f;
        edge.color = from;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            edge.color = Color.Lerp(from, to, UIMotion.EaseOutQuad(Mathf.Clamp01(t / 0.3f)));
            yield return null;
        }
        edge.color = to;
    }

    public Text CreateText(Transform parent, string objectName, string value, int fontSize,
        TextAnchor alignment, Vector2 position, Vector2 size)
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
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return text;
    }

    // displayFont 是共享资源（RuntimeFont），不能销毁

    public string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }
}

