using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SkillCodexManager : MonoBehaviour
{
    public static SkillCodexManager Instance;
    public Canvas canvas;
    public GameObject codexPanel;
    public Button codexButton;
    public Button closeButton;
    public Text titleText;
    public Text detailText;
    public Font displayFont;
    public SkillCardDefinition[] cards;
    public List<Button> cardButtons = new List<Button>();
    public int selectedIndex;
    public bool panelOpen;
    public bool isGameplay;
    public bool ownsPause;

    // 敌人图鉴
    public EnemyCodexEntry[] enemyEntries;
    public List<Button> enemyButtons = new List<Button>();
    public int selectedEnemyIndex;
    public int currentTab = 0; // 0=技能, 1=敌人
    public Button skillTabButton;
    public Button enemyTabButton;
    public GameObject skillContent;
    public GameObject enemyContent;
    CanvasGroup contentGroup;
    Coroutine contentMotion;
    // R2-07：tab 切换渐入句柄 + 下划线插值句柄字典，重入先 Stop，防协程叠加。
    CanvasGroup skillGroup;
    CanvasGroup enemyGroup;
    Coroutine tabMotion;
    readonly Dictionary<Button, Coroutine> tabUnderlineMotions = new Dictionary<Button, Coroutine>();
    // T_Select：tile 选中框 0.15s 插值句柄，重入先 Stop 再启动。
    readonly Dictionary<Button, Coroutine> tileSelectMotions = new Dictionary<Button, Coroutine>();
    GameObject focusedTile;

    void TintTileBorder(Button button, bool selected)
    {
        if (button == null) return;
        Transform border = button.transform.Find("Border");
        if (border == null) return;
        Image img = border.GetComponent<Image>();
        if (img == null) return;
        // R3-03：键盘焦点环用临床青，与鼠标选中的琥珀区分；选中优先。
        Color target = selected ? UITheme.HexA("#D9A05B", 0.9f)
            : (focusedTile == button.gameObject ? UITheme.HexA("#7FB5AD", 0.7f) : UITheme.HexA("#7A8894", 0.35f));
        if (img.color == target) return;
        Coroutine old;
        if (tileSelectMotions.TryGetValue(button, out old) && old != null) StopCoroutine(old);
        tileSelectMotions[button] = StartCoroutine(UIMotion.Tint(img, img.color, target, UIMotion.SelectDur));
    }

    void Awake() { Instance = this; }
    void Start() { EnsureView(); RefreshCards(); RefreshEnemies(); }
    void Update()
    {
        if (panelOpen && Input.GetKeyDown(KeyCode.Escape)) SetPanel(false);
        // 布局完成后持续校准渐隐（颜色相同则不触发重建，开销可忽略）。
        if (panelOpen) SyncScrollFades();
        SyncFocusRing();
    }

    // R3-03：EventSystem 选中变化时重估两网格边框，焦点环随方向键移动。
    void SyncFocusRing()
    {
        GameObject sel = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (sel == focusedTile) return;
        focusedTile = sel;
        for (int j = 0; j < cardButtons.Count; j++) TintTileBorder(cardButtons[j], j == selectedIndex);
        for (int j = 0; j < enemyButtons.Count; j++) TintTileBorder(enemyButtons[j], j == selectedEnemyIndex);
    }

    public void EnsureCards()
    {
        // 设定集(3) 闭集：图鉴卡必须与 27 项闭集一致，旧版残留（弹簧圈/血液回收钳/体外循环泵）一律重建。
        if (cards != null && cards.Length == VesselSkillCatalog.Types.Length &&
            System.Array.TrueForAll(cards, c => c != null && VesselSkillCatalog.IsVesselSkill(c.linkedUpgradeType) && !VesselSkillCatalog.IsRetired(c.linkedUpgradeType))) return;
        LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
        if (levels != null) levels.Subscribe();
        List<SkillCardDefinition> runtimeCards = new List<SkillCardDefinition>();
        if (levels != null && levels.allUpgrades != null)
        {
            foreach (UpgradeOption option in levels.allUpgrades)
            {
                if (option == null || !VesselSkillCatalog.IsVesselSkill(option.type) || VesselSkillCatalog.IsRetired(option.type)) continue;
                SkillCardDefinition card = option.skillCard;
                if (card == null) card = VesselSkillCatalog.CreateCard(VesselSkillCatalog.IndexOf(option.type));
                runtimeCards.Add(card);
            }
        }
        cards = runtimeCards.Count == 0 ? CreateFallbackCards() : runtimeCards.ToArray();
        System.Array.Sort(cards, (a, b) => a.linkedUpgradeType.CompareTo(b.linkedUpgradeType));
    }

    public SkillCardDefinition[] CreateFallbackCards()
    {
        List<SkillCardDefinition> result = new List<SkillCardDefinition>();
        for (int i = 0; i < VesselSkillCatalog.Types.Length; i++) result.Add(VesselSkillCatalog.CreateCard(i));
        return result.ToArray();
    }

    public SkillCardDefinition GetCardFor(UpgradeOption option)
    {
        if (option == null) return null;
        if (option.skillCard != null) return option.skillCard;
        EnsureCards();
        foreach (SkillCardDefinition card in cards)
            if (card.linkedUpgradeType == option.type) return card;
        return null;
    }

    public void UnlockForUpgrade(UpgradeOption option)
    {
        SkillCardDefinition card = GetCardFor(option);
        if (card == null) return;
        card.Unlock();
        RefreshCards();
    }

    public void TogglePanel() { SetPanel(!panelOpen); }
    public void SetPanel(bool open)
    {
        if (panelOpen == open) return;
        if (open)
        {
            LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
            if (isGameplay && ((levels != null && levels.isChoosing) ||
                (CardRevealUI.Instance != null && CardRevealUI.Instance.isShowing) ||
                (GameManager.Instance != null && GameManager.Instance.gameEnded))) return;
            EnsureView();
            ownsPause = isGameplay && Time.timeScale > 0f;
            if (ownsPause) Time.timeScale = 0f;
            RefreshCards();
            RefreshEnemies();
            SyncScrollFades();
            codexPanel.transform.SetAsLastSibling();
            if (contentMotion != null) StopCoroutine(contentMotion);
            if (contentGroup != null)
            {
                contentGroup.alpha = 0f;
                contentMotion = StartCoroutine(UIMotion.Fade(contentGroup, 0f, 1f, 0.15f, UIMotion.EaseOutQuad));
            }
        }
        else
        {
            if (contentMotion != null) StopCoroutine(contentMotion);
            if (contentGroup != null) contentGroup.alpha = 1f;
            if (ownsPause)
            {
                ownsPause = false;
                LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
                if ((levels == null || !levels.isChoosing) &&
                    (GameManager.Instance == null || !GameManager.Instance.gameEnded))
                    Time.timeScale = 1f;
            }
        }
        panelOpen = open;
        if (codexPanel != null) codexPanel.SetActive(open);
    }

    public void RefreshCards()
    {
        EnsureCards();
        int count = 0;
        foreach (SkillCardDefinition card in cards) if (card != null && card.IsUnlocked) count++;
        if (titleText != null && currentTab == 0) titleText.text = "技能图鉴 · " + UITheme.Wrap(count + "/" + cards.Length, UITheme.Clinical);
        for (int i = 0; i < cardButtons.Count; i++)
        {
            if (i >= cards.Length || cards[i] == null) { cardButtons[i].gameObject.SetActive(false); continue; }
            SkillCardDefinition card = cards[i];
            Text text = cardButtons[i].GetComponentInChildren<Text>();
            Sprite icon = MonsterSurvivorArtLibrary.SkillIcon(card.linkedUpgradeType);
            string lines = TileLines(card.IsUnlocked ? "已点亮" : "尚未点亮", card.displayName, card.IsUnlocked);
            text.text = icon == null && !string.IsNullOrEmpty(card.iconGlyph) ? card.iconGlyph + "  " + lines : lines;
            StyleTileLabel(text);
            AttachCardIcon(cardButtons[i], icon, card.IsUnlocked);
            Color cardColor = card.cardColor == Color.white ? ColorFor(card.linkedUpgradeType) : card.cardColor;
            SetTileBody(cardButtons[i], card.IsUnlocked ? UITheme.Mute(cardColor) : UITheme.Surface);
        }
        if (currentTab == 0) ShowCardDetail(selectedIndex);
    }

    public void RefreshEnemies()
    {
        if (enemyEntries == null || enemyEntries.Length == 0)
            enemyEntries = EnemyCodexCatalog.CreateAllEntries();
        int count = 0;
        foreach (EnemyCodexEntry entry in enemyEntries) if (entry != null && entry.isUnlocked) count++;
        if (titleText != null && currentTab == 1) titleText.text = "敌人图鉴 · " + UITheme.Wrap(count + "/" + enemyEntries.Length, UITheme.Clinical);
        for (int i = 0; i < enemyButtons.Count; i++)
        {
            if (i >= enemyEntries.Length || enemyEntries[i] == null) { enemyButtons[i].gameObject.SetActive(false); continue; }
            EnemyCodexEntry entry = enemyEntries[i];
            Text text = enemyButtons[i].GetComponentInChildren<Text>();
            Sprite icon = MonsterSurvivorArtLibrary.FirstFrame(MonsterSurvivorArtLibrary.EnemySheet(entry.kind));
            text.text = TileLines(entry.isUnlocked ? "已发现" : "尚未发现", entry.displayName, entry.isUnlocked);
            StyleTileLabel(text);
            AttachCardIcon(enemyButtons[i], icon, entry.isUnlocked);
            SetTileBody(enemyButtons[i], entry.isUnlocked ? UITheme.Mute(entry.cardColor) : UITheme.Surface);
        }
        if (currentTab == 1) ShowEnemyDetail(selectedEnemyIndex);
    }

    public void ShowEnemyDetail(int index)
    {
        if (detailText == null || enemyEntries == null || index < 0 || index >= enemyEntries.Length) return;
        selectedEnemyIndex = index;
        EnemyCodexEntry entry = enemyEntries[index];
        detailText.text = entry.isUnlocked
            ? UITheme.Wrap(entry.displayName + " · " + entry.medicalName, UITheme.TextPrimary, UITheme.Value) + "\n" + entry.description
                + "\n" + UITheme.Wrap("【医学科普】", UITheme.WarmSoft) + "\n" + entry.educationText
                + "\n" + UITheme.Wrap("【健康提示】", UITheme.WarmSoft) + "\n" + entry.healthTip
            : UITheme.Wrap(entry.displayName, UITheme.TextPrimary, UITheme.Value) + "\n在战斗中遭遇该敌人后，即可解锁详细医学科普信息。";
        for (int j = 0; j < enemyButtons.Count; j++)
            TintTileBorder(enemyButtons[j], j == index);
    }

    public void SwitchTab(int tab)
    {
        currentTab = tab;
        if (skillContent != null) skillContent.SetActive(tab == 0);
        if (enemyContent != null) enemyContent.SetActive(tab == 1);
        StyleTab(skillTabButton, tab == 0);
        StyleTab(enemyTabButton, tab == 1);
        if (tab == 0) RefreshCards();
        else RefreshEnemies();
        // R2-07：新激活页 0.15s 渐入（unscaled），旧页 alpha 复位；句柄重入先 Stop。
        if (tabMotion != null) { StopCoroutine(tabMotion); tabMotion = null; }
        CanvasGroup incoming = tab == 0 ? skillGroup : enemyGroup;
        CanvasGroup outgoing = tab == 0 ? enemyGroup : skillGroup;
        if (outgoing != null) outgoing.alpha = 1f;
        if (incoming != null) tabMotion = StartCoroutine(UIMotion.Fade(incoming, 0f, 1f, 0.15f, UIMotion.EaseOutQuad));
        SyncScrollFades();
    }

    // tab 状态：active=抬升体+暖纸白字+2px 琥珀下划线；inactive=沉底体+暖沙灰字。
    void StyleTab(Button button, bool active)
    {
        if (button == null) return;
        ColorBlock cb = button.colors;
        cb.normalColor = active ? UITheme.SurfaceRaised : UITheme.Surface;
        cb.highlightedColor = UITheme.Hex("#3A3128");
        cb.pressedColor = UITheme.Hex("#241D17");
        cb.selectedColor = cb.normalColor;
        cb.fadeDuration = 0.12f;
        button.colors = cb;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.font = active ? RuntimeFont.GetTitle() : RuntimeFont.Get();
            label.fontSize = 18;
            label.color = active ? UITheme.TextPrimary : UITheme.TextMuted;
        }
        Transform underline = button.transform.Find("TabUnderline");
        if (underline != null)
        {
            Image underlineImage = underline.GetComponent<Image>();
            Color target = UITheme.WarmAccent; target.a = active ? 1f : 0f;
            Coroutine old;
            if (tabUnderlineMotions.TryGetValue(button, out old) && old != null) StopCoroutine(old);
            tabUnderlineMotions[button] = StartCoroutine(UIMotion.Tint(underlineImage, underlineImage.color, target, UIMotion.SelectDur));
        }
        Transform border = button.transform.Find("Border");
        if (border != null) border.GetComponent<Image>().color = UITheme.HexA("#7A8894", 0.35f);
    }

    // R2-01：hover 抬升 / press 压暗，恢复 T_Hover 反馈；fadeDuration 保持 0.12。
    static void SetTileBody(Button button, Color body)
    {
        ColorBlock cb = button.colors;
        cb.normalColor = body;
        cb.highlightedColor = Color.Lerp(body, Color.white, 0.10f);
        cb.pressedColor = Color.Lerp(body, Color.black, 0.15f);
        cb.selectedColor = body;
        cb.fadeDuration = 0.12f;
        button.colors = cb;
    }

    // R2-01：状态行 14 Clinical / 名字行 16 暖纸白；文案内容与行序不变，只包 rich 标签。
    static string TileLines(string status, string name, bool unlocked)
    {
        string s = UITheme.Wrap(status, UITheme.Clinical, 14);
        string n = UITheme.Wrap(name, UITheme.TextPrimary, 16);
        return unlocked ? s + "\n" + n : n + "\n" + s;
    }

    static void StyleTileLabel(Text label)
    {
        if (label == null) return;
        label.fontSize = 16;
        label.lineSpacing = 1.1f;
        label.color = UITheme.TextPrimary;
        label.alignment = TextAnchor.MiddleCenter;
        label.supportRichText = true;
    }

    public void UnlockEnemy(EnemyKind kind)
    {
        bool known = false;
        foreach (EnemyKind t in EnemyCodexCatalog.Types) if (t == kind) { known = true; break; }
        if (!known) return;
        if (enemyEntries == null) enemyEntries = EnemyCodexCatalog.CreateAllEntries();
        int index = EnemyCodexCatalog.IndexOf(kind);
        if (index >= 0 && index < enemyEntries.Length && !enemyEntries[index].isUnlocked)
        {
            enemyEntries[index].Unlock();
            RefreshEnemies();
        }
    }

    public void ShowCardDetail(int index)
    {
        if (detailText == null || cards == null || index < 0 || index >= cards.Length) return;
        selectedIndex = index;
        SkillCardDefinition card = cards[index];
        string typeLine = VesselSkillCatalog.IsPassive(card.linkedUpgradeType) ? "被动 · 选取后立即生效（无等级）"
            : VesselSkillCatalog.IsEvolution(card.linkedUpgradeType) ? "进化 · 替换原技能效果（一次性）"
            : "主动 · 三次选取 Lv1→Lv3";
        string firstLine = (string.IsNullOrEmpty(card.iconGlyph) ? "" : card.iconGlyph + "  ") + card.displayName + "  ·  " + typeLine;
        detailText.text = card.IsUnlocked
            ? UITheme.Wrap(firstLine, UITheme.TextPrimary, UITheme.Value) + "\n" + card.description + "\n" + card.educationText
            : UITheme.Wrap(card.displayName, UITheme.TextPrimary, UITheme.Value) + "\n在战斗升级时选择对应技能，即可永久点亮这张卡牌。";
        for (int j = 0; j < cardButtons.Count; j++)
            TintTileBorder(cardButtons[j], j == index);
    }

    public void EnsureView()
    {
        EnsureCards();
        if (enemyEntries == null || enemyEntries.Length == 0) enemyEntries = EnemyCodexCatalog.CreateAllEntries();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null || codexPanel != null) return;
        // WebGL 无系统字体，统一使用随包分发的 simhei
        displayFont = RuntimeFont.Get();
        if (isGameplay && codexButton == null)
        {
            codexButton = MakeButton(canvas.transform, "CodexButton", "血管图鉴", new Vector2(-130f, -110f), new Vector2(190f, 50f));
            RectTransform buttonRect = codexButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
            ColorBlock cb = codexButton.colors;
            cb.normalColor = UITheme.Surface;
            cb.highlightedColor = UITheme.SurfaceRaised;
            cb.pressedColor = UITheme.Hex("#241D17");
            cb.selectedColor = UITheme.Surface;
            cb.fadeDuration = 0.12f;
            codexButton.colors = cb;
            Text codexLabel = codexButton.GetComponentInChildren<Text>();
            if (codexLabel != null) { codexLabel.fontSize = 16; codexLabel.color = UITheme.Clinical; }
            Transform codexBorder = codexButton.transform.Find("Border");
            if (codexBorder != null) codexBorder.GetComponent<Image>().color = UITheme.LineCool;
            codexButton.onClick.AddListener(TogglePanel);
        }

        codexPanel = new GameObject("CodexPanel", typeof(RectTransform), typeof(Image));
        codexPanel.transform.SetParent(canvas.transform, false);
        RectTransform rect = codexPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image scrim = codexPanel.GetComponent<Image>();
        scrim.sprite = null; scrim.type = Image.Type.Simple;
        scrim.color = UITheme.Scrim;

        GameObject content = new GameObject("CodexContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(codexPanel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.sizeDelta = new Vector2(1000f, 720f);
        Image contentBody = content.GetComponent<Image>();
        contentBody.sprite = MonsterSurvivorArtLibrary.ChamferFill(1000, 720, 16f);
        contentBody.type = Image.Type.Simple;
        contentBody.color = UITheme.Overlay;
        GameObject gridField = new GameObject("ContentGrid", typeof(RectTransform), typeof(Image));
        gridField.transform.SetParent(content.transform, false);
        Image gridImage = gridField.GetComponent<Image>();
        gridImage.sprite = MonsterSurvivorArtLibrary.GridField();
        gridImage.type = Image.Type.Tiled;
        gridImage.color = new Color(1f, 1f, 1f, 0.03f);
        gridImage.raycastTarget = false;
        RectTransform gridFieldRect = gridField.GetComponent<RectTransform>();
        gridFieldRect.anchorMin = Vector2.zero; gridFieldRect.anchorMax = Vector2.one;
        gridFieldRect.offsetMin = new Vector2(16f, 16f); gridFieldRect.offsetMax = new Vector2(-16f, -16f);
        GameObject frame = new GameObject("ContentFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(content.transform, false);
        Image frameImage = frame.GetComponent<Image>();
        frameImage.sprite = MonsterSurvivorArtLibrary.ChamferLine(1000, 720, 16f, 1f);
        frameImage.type = Image.Type.Simple;
        frameImage.color = UITheme.LineCool;
        frameImage.raycastTarget = false;
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        contentGroup = UIMotion.EnsureGroup(content);

        titleText = MakeText(content.transform, "CodexTitle", "", UITheme.CardTitle, TextAnchor.MiddleLeft,
            new Vector2(-270f, 300f), new Vector2(380f, 46f));
        titleText.font = RuntimeFont.GetTitle();
        titleText.color = UITheme.TextPrimary;
        titleText.supportRichText = true;
        skillTabButton = MakeButton(content.transform, "SkillTab", "技能图鉴", new Vector2(70f, 300f), new Vector2(150f, 46f));
        MakeTabUnderline(skillTabButton);
        skillTabButton.onClick.AddListener(() => SwitchTab(0));
        enemyTabButton = MakeButton(content.transform, "EnemyTab", "敌人图鉴", new Vector2(230f, 300f), new Vector2(150f, 46f));
        MakeTabUnderline(enemyTabButton);
        enemyTabButton.onClick.AddListener(() => SwitchTab(1));
        closeButton = MakeButton(content.transform, "CodexCloseButton", "返回", new Vector2(410f, 300f), new Vector2(120f, 46f));
        Text closeLabel = closeButton.GetComponentInChildren<Text>();
        if (closeLabel != null) { closeLabel.fontSize = 18; closeLabel.color = UITheme.TextMuted; }
        Transform closeBorder = closeButton.transform.Find("Border");
        if (closeBorder != null) closeBorder.GetComponent<Image>().color = UITheme.HexA("#7A8894", 0.35f);
        closeButton.onClick.AddListener(() => SetPanel(false));

        detailText = MakeText(content.transform, "CodexDetail", "", UITheme.Body, TextAnchor.UpperLeft,
            new Vector2(0f, -210f), new Vector2(940f, 260f));
        detailText.color = UITheme.TextMuted;
        detailText.lineSpacing = 1.15f;
        detailText.supportRichText = true;

        // 技能页：可滚动卡牌网格（顶原点锚点，行距 116，顶边距 12）
        RectTransform skillGrid = MakeScrollGrid(content.transform, "SkillScroll", new Vector2(0f, 110f), new Vector2(940f, 320f));
        skillContent = skillGrid.parent.parent.gameObject;
        int columns = 5; float cardW = 176f, cardH = 104f, gapX = 8f, gapY = 12f;
        float totalW = columns * cardW + (columns - 1) * gapX;
        float startX = -totalW * 0.5f + cardW * 0.5f;
        for (int i = 0; i < cards.Length; i++)
        {
            Vector2 pos = new Vector2(startX + (i % columns) * (cardW + gapX), -(gapY + (i / columns) * (cardH + gapY)));
            Button button = MakeButton(skillGrid, "SkillCard_" + cards[i].cardId, "", pos, new Vector2(cardW, cardH));
            RectTransform tileRect = button.GetComponent<RectTransform>();
            tileRect.anchorMin = tileRect.anchorMax = new Vector2(0.5f, 1f);
            tileRect.pivot = new Vector2(0.5f, 1f);
            tileRect.anchoredPosition = pos;
            int index = i;
            button.onClick.AddListener(() => ShowCardDetail(index));
            cardButtons.Add(button);
        }
        int skillRows = Mathf.CeilToInt(cards.Length / (float)columns);
        skillGrid.sizeDelta = new Vector2(940f, skillRows * (cardH + gapY) + gapY);
        SetScrollChromeActive(skillContent, skillRows * 116 + 12 > 320);

        // 敌人页：可滚动卡牌网格
        RectTransform enemyGrid = MakeScrollGrid(content.transform, "EnemyScroll", new Vector2(0f, 110f), new Vector2(940f, 320f));
        enemyContent = enemyGrid.parent.parent.gameObject;
        for (int i = 0; i < enemyEntries.Length; i++)
        {
            Vector2 pos = new Vector2(startX + (i % columns) * (cardW + gapX), -(gapY + (i / columns) * (cardH + gapY)));
            Button button = MakeButton(enemyGrid, "EnemyCard_" + (int)enemyEntries[i].kind, "", pos, new Vector2(cardW, cardH));
            RectTransform tileRect = button.GetComponent<RectTransform>();
            tileRect.anchorMin = tileRect.anchorMax = new Vector2(0.5f, 1f);
            tileRect.pivot = new Vector2(0.5f, 1f);
            tileRect.anchoredPosition = pos;
            int index = i;
            button.onClick.AddListener(() => ShowEnemyDetail(index));
            enemyButtons.Add(button);
        }
        int enemyRows = Mathf.CeilToInt(enemyEntries.Length / (float)columns);
        enemyGrid.sizeDelta = new Vector2(940f, Mathf.Max(320f, enemyRows * (cardH + gapY) + gapY));
        SetScrollChromeActive(enemyContent, enemyGrid.sizeDelta.y > 320.5f);

        skillGroup = UIMotion.EnsureGroup(skillContent);
        enemyGroup = UIMotion.EnsureGroup(enemyContent);
        WireGridNav(cardButtons, columns, skillTabButton);
        WireGridNav(enemyButtons, columns, enemyTabButton);
        WireTabNav();
        codexPanel.SetActive(false);
        SwitchTab(currentTab);
    }

    // R3-03：Explicit 网格导航：方向键在表内移动，首行向上到 tab，越界为 null。
    static void WireGridNav(List<Button> tiles, int columns, Button upTarget)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null) continue;
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;
            int col = i % columns;
            nav.selectOnLeft = col > 0 ? tiles[i - 1] : null;
            nav.selectOnRight = col < columns - 1 && i + 1 < tiles.Count ? tiles[i + 1] : null;
            nav.selectOnUp = i - columns >= 0 ? tiles[i - columns] : upTarget;
            nav.selectOnDown = i + columns < tiles.Count ? tiles[i + columns] : null;
            tiles[i].navigation = nav;
        }
    }

    void WireTabNav()
    {
        Navigation skill = skillTabButton.navigation;
        skill.mode = Navigation.Mode.Explicit;
        skill.selectOnRight = enemyTabButton;
        skill.selectOnDown = cardButtons.Count > 0 ? cardButtons[0] : null;
        skillTabButton.navigation = skill;
        Navigation enemy = enemyTabButton.navigation;
        enemy.mode = Navigation.Mode.Explicit;
        enemy.selectOnLeft = skillTabButton;
        enemy.selectOnRight = closeButton;
        enemy.selectOnDown = enemyButtons.Count > 0 ? enemyButtons[0] : null;
        enemyTabButton.navigation = enemy;
        Navigation close = closeButton.navigation;
        close.mode = Navigation.Mode.Explicit;
        close.selectOnLeft = enemyTabButton;
        closeButton.navigation = close;
    }

    // R2-02：滚动条与边缘渐隐的激活开关（内容不超出 viewport 时全部隐藏，无指示残留）。
    static void SetScrollChromeActive(GameObject scrollGo, bool active)
    {
        if (scrollGo == null) return;
        Transform t = scrollGo.transform;
        Transform bar = t.Find("VScrollbar");
        if (bar != null) bar.gameObject.SetActive(active);
        Transform top = t.Find("FadeTop");
        if (top != null) top.gameObject.SetActive(active);
        Transform bottom = t.Find("FadeBottom");
        if (bottom != null) bottom.gameObject.SetActive(active);
    }

    // R2-02：以 normalizedPosition 初始化/同步顶底渐隐（只写 fade alpha，不写 scroll 位置）。
    void SyncScrollFades()
    {
        SyncGridFades(skillContent);
        SyncGridFades(enemyContent);
    }

    static void SyncGridFades(GameObject scrollGo)
    {
        if (scrollGo == null || !scrollGo.activeSelf) return;
        ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
        if (sr == null) return;
        Transform t = scrollGo.transform;
        Transform topT = t.Find("FadeTop");
        Transform bottomT = t.Find("FadeBottom");
        UpdateEdgeFades(topT == null ? null : topT.GetComponent<Image>(),
            bottomT == null ? null : bottomT.GetComponent<Image>(), sr.normalizedPosition.y);
    }

    static void UpdateEdgeFades(Image topFade, Image bottomFade, float y)
    {
        // y=1 内容在顶（底缘裁切→底渐隐），y=0 滚到底（顶缘裁切→顶渐隐）。
        if (topFade != null) { Color c = topFade.color; c.a = 0.95f * (1f - y); topFade.color = c; }
        if (bottomFade != null) { Color c = bottomFade.color; c.a = 0.95f * y; bottomFade.color = c; }
    }

    static void MakeTabUnderline(Button button)
    {
        GameObject obj = new GameObject("TabUnderline", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(button.transform, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = null; image.type = Image.Type.Simple;
        Color c = UITheme.WarmAccent; c.a = 0f;
        image.color = c;
        image.raycastTarget = false;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(0f, 2f);
        r.anchoredPosition = Vector2.zero;
    }

    RectTransform MakeScrollGrid(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject scroll = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
        scroll.transform.SetParent(parent, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = scrollRect.anchorMax = Vector2.one * 0.5f;
        scrollRect.sizeDelta = size; scrollRect.anchoredPosition = position;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(scroll.transform, false);
        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        GameObject grid = new GameObject("Content", typeof(RectTransform));
        grid.transform.SetParent(viewport.transform, false);
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = size;

        ScrollRect sr = scroll.GetComponent<ScrollRect>();
        sr.content = gridRect;
        sr.viewport = vpRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // R2-02：顶/底 28px 边缘渐隐（sibling 在 viewport 之后，不拦射线）。
        Image topFade = MakeEdgeFade(scroll.transform, "FadeTop", false);
        Image bottomFade = MakeEdgeFade(scroll.transform, "FadeBottom", true);
        sr.onValueChanged.AddListener(v => UpdateEdgeFades(topFade, bottomFade, v.y));

        // R2-02：自绘 6px 滚动条，落在 viewport 右缘留白内；轨/拇指/hover 全走 UITheme。
        GameObject barObj = new GameObject("VScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barObj.transform.SetParent(scroll.transform, false);
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f); barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.sizeDelta = new Vector2(6f, -8f);
        barRect.anchoredPosition = new Vector2(-5f, 0f);
        Image track = barObj.GetComponent<Image>();
        track.sprite = null; track.type = Image.Type.Simple;
        track.color = UITheme.HexA("#161210", 0.6f);
        track.raycastTarget = true;
        GameObject areaObj = new GameObject("SlidingArea", typeof(RectTransform));
        areaObj.transform.SetParent(barObj.transform, false);
        RectTransform areaRect = areaObj.GetComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero; areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = areaRect.offsetMax = Vector2.zero;
        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(areaObj.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = new Vector2(1f, 0f);
        handleRect.sizeDelta = Vector2.zero;
        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.sprite = null; handleImage.type = Image.Type.Simple;
        handleImage.raycastTarget = true;
        Scrollbar scrollbar = barObj.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.numberOfSteps = 0;
        ColorBlock sbc = scrollbar.colors;
        sbc.normalColor = UITheme.HexA("#7A8894", 0.8f);
        sbc.highlightedColor = UITheme.WarmAccent;
        sbc.pressedColor = UITheme.WarmPress;
        sbc.fadeDuration = 0.12f;
        scrollbar.colors = sbc;
        sr.verticalScrollbar = scrollbar;
        // AutoHide：Permanent 会在每次 OnEnable 强制点亮滚动条（敌人页内容不溢出也残留）；
        // 交给 ScrollRect 按内容溢出自管理，与 SetScrollChromeActive 的渐隐开关同向。
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        sr.verticalScrollbarSpacing = 0f;
        UpdateEdgeFades(topFade, bottomFade, sr.normalizedPosition.y);
        return gridRect;
    }

    static Image MakeEdgeFade(Transform parent, string name, bool flip)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = MonsterSurvivorArtLibrary.EdgeFade(flip);
        image.type = Image.Type.Simple;
        Color c = UITheme.Overlay; c.a = 0f;
        image.color = c;
        image.raycastTarget = false;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, flip ? 0f : 1f);
        r.anchorMax = new Vector2(1f, flip ? 0f : 1f);
        r.pivot = new Vector2(0.5f, flip ? 0f : 1f);
        r.sizeDelta = new Vector2(0f, 28f);
        r.anchoredPosition = Vector2.zero;
        return image;
    }

    public void AttachCardIcon(Button button, Sprite sprite, bool unlocked)
    {
        if (button == null) return;
        Transform label = button.transform.Find("Label");
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (label != null)
        {
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, -24f);
            labelRect.sizeDelta = new Vector2(buttonRect.sizeDelta.x - 16f, 52f);
        }
        Transform existing = button.transform.Find("SkillIcon");
        if (sprite == null)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        GameObject iconObject = existing == null ? new GameObject("SkillIcon", typeof(RectTransform), typeof(Image)) : existing.gameObject;
        iconObject.transform.SetParent(button.transform, false);
        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = unlocked ? Color.white : new Color(0.55f, 0.62f, 0.68f, 1f);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(36f, 36f);
        rect.anchoredPosition = new Vector2(0f, -8f);
        iconObject.SetActive(true);
    }

    public Button MakeButton(Transform parent, string name, string caption, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        int w = Mathf.RoundToInt(size.x), h = Mathf.RoundToInt(size.y);
        image.sprite = MonsterSurvivorArtLibrary.ChamferFill(w, h, 8f);
        image.type = Image.Type.Simple;
        image.color = Color.white;
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor = UITheme.Surface;
        cb.highlightedColor = UITheme.SurfaceRaised;
        cb.pressedColor = UITheme.Hex("#241D17");
        cb.selectedColor = UITheme.Surface;
        cb.fadeDuration = 0.12f;
        button.colors = cb;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = size; rect.anchoredPosition = position;
        // 2px 切角框：选中/未选中只换颜色，不重生成贴图。
        GameObject border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(obj.transform, false);
        Image borderImage = border.GetComponent<Image>();
        borderImage.sprite = MonsterSurvivorArtLibrary.ChamferLine(w + 4, h + 4, 8f, 2f);
        borderImage.type = Image.Type.Simple;
        borderImage.color = UITheme.HexA("#7A8894", 0.35f);
        borderImage.raycastTarget = false;
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2f, -2f);
        borderRect.offsetMax = new Vector2(2f, 2f);
        MakeText(obj.transform, "Label", caption, 16, TextAnchor.MiddleCenter, Vector2.zero, size - Vector2.one * 12f);
        return button;
    }

    public Text MakeText(Transform parent, string name, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = displayFont; text.fontSize = size; text.color = Color.white;
        text.alignment = alignment; text.text = value; text.raycastTarget = false;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = dimensions; rect.anchoredPosition = position;
        return text;
    }

    public string EducationFor(UpgradeType type)
    {
        if (VesselSkillCatalog.IsVesselSkill(type)) return VesselSkillCatalog.EducationFor(type);
        switch (type)
        {
            case UpgradeType.Damage: return "减少烟草暴露、控制血脂，有助于保护血管内皮。";
            case UpgradeType.AttackSpeed: return "规律监测血压，保持稳定作息，帮助心脏更轻松地工作。";
            case UpgradeType.MoveSpeed: return "循序渐进的有氧活动能促进循环，运动要量力而行。";
            case UpgradeType.MaxHP: return "少盐少油、多蔬果，配合体检可以更早发现风险。";
            case UpgradeType.BulletSpeed: return "出现持续胸痛、呼吸困难等情况，应及时寻求专业帮助。";
        }
        return "保持均衡饮食、规律运动和定期检查。";
    }

    public Color ColorFor(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Damage: return new Color(0.78f, 0.24f, 0.25f, 1f);
            case UpgradeType.AttackSpeed: return new Color(0.93f, 0.66f, 0.18f, 1f);
            case UpgradeType.MoveSpeed: return new Color(0.23f, 0.65f, 0.9f, 1f);
            case UpgradeType.MaxHP: return new Color(0.25f, 0.75f, 0.45f, 1f);
            case UpgradeType.BulletSpeed: return new Color(0.67f, 0.42f, 0.91f, 1f);
        }
        return Color.white;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // displayFont 是共享资源（RuntimeFont），不能销毁
    }
}
