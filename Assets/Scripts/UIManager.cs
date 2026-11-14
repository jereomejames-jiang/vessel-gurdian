using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    // 粗野主义单青体系：近黑底 + 钢灰硬边 + 一条青色强调。
    // 质感全靠构件（切角、网格、斜纹、条码）撑，不再靠渐变和噪点——那两层之前把文字糊了四轮。
    static readonly Color ArkBodyColor = new Color(0.055f, 0.071f, 0.094f, 0.96f);   // #0E1218
    static readonly Color ArkDimmerColor = new Color(0.020f, 0.027f, 0.047f, 0.92f); // #05070C
    static readonly Color ArkCyanAccent = new Color(0.0f, 0.82f, 0.92f, 1f);
    static readonly Color ArkRedAccent = new Color(0.92f, 0.22f, 0.18f, 1f);
    static readonly Color ArkTextPrimary = new Color(0.969f, 0.980f, 1.0f, 1f);      // #F7FAFF
    static readonly Color ArkTextMuted = new Color(0.761f, 0.808f, 0.855f, 1f);      // #C2CEDA
    static readonly Color ArkFrameLine = new Color(0.431f, 0.498f, 0.569f, 0.70f);   // #6E7F91
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

        // WebGL 无系统字体，统一使用随包分发的 simhei
        displayFont = RuntimeFont.Get();
        if (displayFont != null)
            foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.font = displayFont;
        EnsureVictoryView();
        EnsureSurvivorHud();
        EnsurePauseView();
        EnsureTestTools();
        ApplyProgressSkin(XPSlider, ArkCyanAccent);
        ApplyProgressSkin(HPSlider, ArkRedAccent);

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
        dimmer.color = ArkDimmerColor;
        dimmer.raycastTarget = false;
        RectTransform rect = LevelUpPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // 粗野主义奖励卡：切角黑底 + 32px 硬网格 + 钢灰 1px 边 + 编号牌/斜纹/条码等技术标记，全程单青。
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
            body.color = ArkBodyColor;
            body.raycastTarget = true;
        }

        // 几何网格：只在直边以内铺，越出切角会在暗场上露出方角。
        Image grid = EnsureImageChild(button.transform, "Grid");
        grid.sprite = MonsterSurvivorArtLibrary.GridField();
        grid.type = Image.Type.Tiled;
        grid.color = new Color(1f, 1f, 1f, 0.05f);
        grid.raycastTarget = false;
        StretchInset(grid.rectTransform, CardChamfer);

        // 编号牌：实心青块 + 抠出底色当字，方舟那种"设备编号"的味道。
        Image tag = EnsureImageChild(button.transform, "CardTag");
        Place(tag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, 22f), new Vector2(CardChamfer, -1f));
        tag.color = ArkCyanAccent;

        Text tagCode = EnsureCardText(button, "CardTagCode", 11, TagInk, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardChamfer + 8f, -1f), new Vector2(96f, 22f));
        tagCode.text = "SK-" + Mathf.Abs(skillSerial).ToString("00");

        // 中部硬分隔线
        Image divider = EnsureImageChild(button.transform, "CardDivider");
        Place(divider, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(content, 1f), new Vector2(0f, -272f));
        divider.color = ArkFrameLine;

        // 底部硬分隔线
        Image footer = EnsureImageChild(button.transform, "CardFooter");
        Place(footer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(content, 1f), new Vector2(0f, -(height - 68f)));
        footer.color = ArkFrameLine;

        // 三段等级条：硬边小方块，filled 用青，空槽用钢灰压暗。
        for (int p = 0; p < 3; p++)
        {
            Image pip = EnsureImageChild(button.transform, "Pip" + p);
            Place(pip, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, 6f),
                new Vector2(CardInset + p * 34f, -(height - 58f)));
            bool filled = p < Mathf.Clamp(currentLevel, 0, 3);
            pip.color = filled ? new Color(ArkCyanAccent.r, ArkCyanAccent.g, ArkCyanAccent.b, 0.95f)
                               : new Color(ArkFrameLine.r, ArkFrameLine.g, ArkFrameLine.b, 0.35f);
        }

        // 技术条码：seed 来自技能编号，每张卡的条纹都不同但恒定。先定位再贴图，Place 会清掉旧 sprite。
        Image barcode = EnsureImageChild(button.transform, "Barcode");
        Place(barcode, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(96f, 14f),
            new Vector2(-CardInset, -(height - 58f)));
        barcode.sprite = MonsterSurvivorArtLibrary.Barcode(skillSerial + 1);
        barcode.color = new Color(ArkFrameLine.r, ArkFrameLine.g, ArkFrameLine.b, 0.85f);

        // 警示斜纹：压在卡底，和青边同色系，不再引入第二种强调色。
        Image hazard = EnsureImageChild(button.transform, "HazardBand");
        Place(hazard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(content, 12f), new Vector2(0f, -(height - 28f)));
        hazard.sprite = MonsterSurvivorArtLibrary.HazardStripes();
        hazard.type = Image.Type.Tiled;
        hazard.color = new Color(ArkCyanAccent.r, ArkCyanAccent.g, ArkCyanAccent.b, 0.32f);

        // 1px 钢灰边框
        Image frame = EnsureImageChild(button.transform, "CardFrame");
        frame.sprite = MonsterSurvivorArtLibrary.ChamferLine(Mathf.RoundToInt(width), Mathf.RoundToInt(height), CardChamfer, 1f);
        frame.type = Image.Type.Simple;
        StretchToParent(frame.rectTransform);
        frame.color = ArkFrameLine;

        // 悬停：整条边框换成 2px 亮青，比原来那层"渐显青色玻璃"更硬也更清楚。
        Image hover = EnsureImageChild(button.transform, "CardHover");
        hover.sprite = MonsterSurvivorArtLibrary.ChamferLine(Mathf.RoundToInt(width), Mathf.RoundToInt(height), CardChamfer, 2f);
        hover.type = Image.Type.Simple;
        StretchToParent(hover.rectTransform);
        hover.raycastTarget = false;
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = hover;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(ArkCyanAccent.r, ArkCyanAccent.g, ArkCyanAccent.b, 1f);
        colors.pressedColor = new Color(0.55f, 0.95f, 1f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    static readonly Color TagInk = new Color(0.055f, 0.071f, 0.094f, 1f);

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
        "MonsterSurvivorIcon", "CardTitle", "CardLevel", "CardDesc",
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

    // 粗野主义排版：中轴对称，图标—标题—等级—描述自上而下，层级差靠字号（28/15≈1.9x）和字重拉开。
    public void EnsureCardTexts(Button button, out Text title, out Text level, out Text desc)
    {
        title = EnsureCardText(button, "CardTitle", 28, ArkTextPrimary, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(252f, 40f), RuntimeFont.GetTitle());
        title.horizontalOverflow = HorizontalWrapMode.Wrap;

        level = EnsureCardText(button, "CardLevel", 15, ArkCyanAccent, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(252f, 24f));

        desc = EnsureCardText(button, "CardDesc", 15, ArkTextMuted, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardInset, -288f), new Vector2(252f, 112f));
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
        }
        text.gameObject.SetActive(true);
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
        if (sprite == null) sprite = CreateFallbackIcon(ArkCyanAccent);
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
        // 图标是扁平矢量，保持原色压平即可；再叠色就会和卡体一起发灰。
        image.color = Color.white;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(120f, 120f);
        rect.anchoredPosition = new Vector2(0f, -52f);
        iconObject.SetActive(true);
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
        cardRect.sizeDelta = new Vector2(840f, 520f);
        MonsterSurvivorArtLibrary.ApplySliced(card.GetComponent<Image>(), "ui_window_background",
            new Color(0.035f, 0.075f, 0.12f, 0.99f));

        GameObject accent = new GameObject("GoldAccent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(card.transform, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f); accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f); accentRect.sizeDelta = new Vector2(0f, 8f); accentRect.anchoredPosition = Vector2.zero;
        accent.GetComponent<Image>().color = new Color(0.72f, 0.60f, 0.36f, 1f);

        VictoryTitle = CreateText(card.transform, "VictoryTitle", "胜利！", 54, TextAnchor.MiddleCenter,
            new Vector2(0f, 188f), new Vector2(620f, 78f));
        VictoryTitle.color = new Color(0.84f, 0.74f, 0.48f, 1f);
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
        // 状态条是信息密度最高的 HUD：一块近黑硬板 + 1px 钢灰边，不用大圆角胶囊。
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.color = ArkDimmerColor;
        background.raycastTarget = false;
        Image hudFrame = EnsureImageChild(root.transform, "HudFrame");
        // HUD 固定 618x48（ApplyGoldenHudLayout 里定的），贴图按同尺寸生成，切角才不变形。
        hudFrame.sprite = MonsterSurvivorArtLibrary.ChamferLine(618, 48, 10f, 1f);
        hudFrame.type = Image.Type.Simple;
        hudFrame.color = ArkFrameLine;
        hudFrame.raycastTarget = false;
        StretchToParent(hudFrame.rectTransform);
        Image hudAccent = EnsureImageChild(root.transform, "HudAccent");
        hudAccent.sprite = null;
        hudAccent.type = Image.Type.Simple;
        hudAccent.color = new Color(ArkCyanAccent.r, ArkCyanAccent.g, ArkCyanAccent.b, 0.85f);
        hudAccent.raycastTarget = false;
        RectTransform accentRect = hudAccent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(3f, -8f);
        accentRect.anchoredPosition = new Vector2(5f, 0f);
        RunStatsText = CreateText(root.transform, "RunStatsText", "击败 000   ·   波次 01   ·   经验 000 / 005",
            19, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(530f, 36f));
        RunStatsText.color = ArkTextMuted;
        Outline hudOutline = RunStatsText.GetComponent<Outline>();
        if (hudOutline == null) hudOutline = RunStatsText.gameObject.AddComponent<Outline>();
        hudOutline.effectColor = new Color(0f, 0f, 0f, 0.4f);
        hudOutline.effectDistance = new Vector2(0.5f, 0.5f);
        hudOutline.useGraphicAlpha = false;
    }

    void ApplyGoldenHudLayout()
    {
        if (canvas == null) return;
        const float phi = 1.618034f;

        PlaceHudRect(LevelText == null ? null : LevelText.rectTransform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(300f, 72f));
        PlaceHudRect(RunHudRoot,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(618f, 48f));
        PlaceHudRect(HPSlider == null ? null : HPSlider.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(618f, 16f));
        PlaceHudRect(TimerText == null ? null : TimerText.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(340f, 34f));

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
            track.color = new Color(0.03f, 0.05f, 0.08f, 0.9f);
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
        edge.color = new Color(ArkFrameLine.r, ArkFrameLine.g, ArkFrameLine.b, 0.9f);
        edge.raycastTarget = false;
        RectTransform edgeRect = edge.rectTransform;
        edgeRect.anchorMin = new Vector2(0f, 1f);
        edgeRect.anchorMax = new Vector2(1f, 1f);
        edgeRect.pivot = new Vector2(0.5f, 1f);
        edgeRect.sizeDelta = new Vector2(0f, 1f);
        edgeRect.anchoredPosition = Vector2.zero;
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

