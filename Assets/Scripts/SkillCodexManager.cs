using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    void Awake() { Instance = this; }
    void Start() { EnsureView(); RefreshCards(); }
    void Update()
    {
        if (panelOpen && Input.GetKeyDown(KeyCode.Escape)) SetPanel(false);
    }

    public void EnsureCards()
    {
        if (cards != null && cards.Length > 0) return;
        cards = Resources.LoadAll<SkillCardDefinition>("");
        if (cards.Length == 0)
        {
            LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
            if (levels != null && levels.allUpgrades != null)
            {
                List<SkillCardDefinition> runtimeCards = new List<SkillCardDefinition>();
                foreach (UpgradeOption option in levels.allUpgrades)
                {
                    if (option == null) continue;
                    SkillCardDefinition card = option.skillCard;
                    if (card == null) card = ScriptableObject.CreateInstance<SkillCardDefinition>();
                    card.cardId = option.type.ToString();
                    card.displayName = option.displayName;
                    card.description = option.description;
                    card.educationText = EducationFor(option.type);
                    card.linkedUpgradeType = option.type;
                    card.cardColor = ColorFor(option.type);
                    runtimeCards.Add(card);
                }
                cards = runtimeCards.ToArray();
            }
        }
        if (cards.Length == 0) cards = CreateFallbackCards();
        System.Array.Sort(cards, (a, b) => a.linkedUpgradeType.CompareTo(b.linkedUpgradeType));
    }

    public SkillCardDefinition[] CreateFallbackCards()
    {
        UpgradeType[] types = { UpgradeType.Damage, UpgradeType.AttackSpeed, UpgradeType.MoveSpeed, UpgradeType.MaxHP, UpgradeType.BulletSpeed };
        string[] names = { "伤害 +1", "攻速 +10%", "移速 +15%", "血量 +2", "弹速 +20%" };
        string[] descriptions = { "子弹伤害增加 1", "攻击间隔缩短 10%", "玩家移动速度增加 15%", "最大生命值增加 2", "子弹速度增加 20%" };
        List<SkillCardDefinition> result = new List<SkillCardDefinition>();
        for (int i = 0; i < types.Length; i++)
        {
            SkillCardDefinition card = ScriptableObject.CreateInstance<SkillCardDefinition>();
            card.cardId = types[i].ToString(); card.displayName = names[i]; card.description = descriptions[i];
            card.linkedUpgradeType = types[i]; card.educationText = EducationFor(types[i]); card.cardColor = ColorFor(types[i]);
            result.Add(card);
        }
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
            codexPanel.transform.SetAsLastSibling();
        }
        else if (ownsPause)
        {
            ownsPause = false;
            LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
            if ((levels == null || !levels.isChoosing) &&
                (GameManager.Instance == null || !GameManager.Instance.gameEnded))
                Time.timeScale = 1f;
        }
        panelOpen = open;
        if (codexPanel != null) codexPanel.SetActive(open);
    }

    public void RefreshCards()
    {
        EnsureCards();
        int count = 0;
        foreach (SkillCardDefinition card in cards) if (card != null && card.IsUnlocked) count++;
        if (titleText != null) titleText.text = "血管图鉴  ·  技能卡牌   " + count + "/" + cards.Length;
        for (int i = 0; i < cardButtons.Count; i++)
        {
            if (i >= cards.Length || cards[i] == null) { cardButtons[i].gameObject.SetActive(false); continue; }
            SkillCardDefinition card = cards[i];
            Text text = cardButtons[i].GetComponentInChildren<Text>();
            text.text = card.IsUnlocked ? "已点亮\n" + card.displayName : card.displayName + "\n尚未点亮";
            Color cardColor = card.cardColor == Color.white ? ColorFor(card.linkedUpgradeType) : card.cardColor;
            cardButtons[i].GetComponent<Image>().color = card.IsUnlocked
                ? cardColor : new Color(0.13f, 0.16f, 0.21f);
        }
        ShowCardDetail(selectedIndex);
    }

    public void ShowCardDetail(int index)
    {
        if (detailText == null || cards == null || index < 0 || index >= cards.Length) return;
        selectedIndex = index;
        SkillCardDefinition card = cards[index];
        detailText.text = card.IsUnlocked
            ? card.displayName + "  ·  " + card.description + "\n\n" + card.educationText
            : card.displayName + "\n\n在战斗升级时选择对应技能，即可永久点亮这张卡牌。";
    }

    public void EnsureView()
    {
        EnsureCards();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null || codexPanel != null) return;
        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        if (isGameplay && codexButton == null)
        {
            codexButton = MakeButton(canvas.transform, "CodexButton", "血管图鉴", new Vector2(-130f, -110f), new Vector2(190f, 50f));
            RectTransform buttonRect = codexButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
            codexButton.onClick.AddListener(TogglePanel);
        }

        codexPanel = new GameObject("CodexPanel", typeof(RectTransform), typeof(Image));
        codexPanel.transform.SetParent(canvas.transform, false);
        RectTransform rect = codexPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        codexPanel.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.04f, 0.95f);

        GameObject content = new GameObject("CodexContent", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(codexPanel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.sizeDelta = new Vector2(1000f, 640f);
        content.GetComponent<Image>().color = new Color(0.025f, 0.05f, 0.09f);
        titleText = MakeText(content.transform, "CodexTitle", "", 30, TextAnchor.MiddleLeft,
            new Vector2(-60f, 270f), new Vector2(760f, 50f));
        closeButton = MakeButton(content.transform, "CodexCloseButton", "返回", new Vector2(410f, 270f), new Vector2(120f, 48f));
        closeButton.onClick.AddListener(() => SetPanel(false));
        detailText = MakeText(content.transform, "CodexDetail", "", 22, TextAnchor.UpperLeft,
            new Vector2(0f, -220f), new Vector2(900f, 150f));
        for (int i = 0; i < cards.Length; i++)
        {
            Button button = MakeButton(content.transform, "SkillCard_" + cards[i].cardId, "",
                new Vector2(-310f + (i % 3) * 310f, 120f - (i / 3) * 155f), new Vector2(280f, 125f));
            int index = i;
            button.onClick.AddListener(() => ShowCardDetail(index));
            cardButtons.Add(button);
        }
        codexPanel.SetActive(false);
    }

    public Button MakeButton(Transform parent, string name, string caption, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.12f, 0.30f, 0.40f);
        obj.GetComponent<Button>().transition = Selectable.Transition.None;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = size; rect.anchoredPosition = position;
        MakeText(obj.transform, "Label", caption, 23, TextAnchor.MiddleCenter, Vector2.zero, size - Vector2.one * 12f);
        return obj.GetComponent<Button>();
    }

    public Text MakeText(Transform parent, string name, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = displayFont; text.fontSize = size; text.color = Color.white;
        text.alignment = alignment; text.text = value; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = dimensions; rect.anchoredPosition = position;
        return text;
    }

    public string EducationFor(UpgradeType type)
    {
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
        if (displayFont != null) Destroy(displayFont);
    }
}
