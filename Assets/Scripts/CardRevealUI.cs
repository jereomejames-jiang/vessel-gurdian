using UnityEngine;
using UnityEngine.UI;

public class CardRevealUI : MonoBehaviour
{
    public static CardRevealUI Instance;

    public GameObject revealPanel;
    public Image cardBackground;
    public Text cardTitle;
    public Text cardDescription;
    public Text educationText;
    public Image glowBorder;
    public Image cardIcon;
    public RectTransform cardRect;

    public float showDuration = 2.2f;
    public float timer;
    public bool isShowing;
    public bool isFlipping;
    public bool showingEducation;
    public float flipTimer;
    public int previousLevel;
    public int nextLevel;
    public Color frameAccent = new Color(0.62f, 0.74f, 0.84f, 1f);
    public float frontHold = 0.6f;
    public float flipDuration = 0.5f;
    public SkillCardDefinition activeCard;
    public Font displayFont;

    void Awake()
    {
        Instance = this;
        // WebGL 无系统字体，统一使用随包分发的 simhei
        displayFont = RuntimeFont.Get();
        if (revealPanel != null && displayFont != null)
            foreach (Text label in revealPanel.GetComponentsInChildren<Text>(true)) label.font = displayFont;
        EnsureCardRect();
        if (revealPanel != null) revealPanel.SetActive(false);
    }

    public void EnsureCardRect()
    {
        if (revealPanel == null) return;
        if (cardRect == null)
        {
            Transform frame = revealPanel.transform.Find("CardFrame");
            if (frame != null) cardRect = frame as RectTransform;
        }
        if (cardRect != null && !isShowing) cardRect.localScale = Vector3.one;
    }

    public void Show(SkillCardDefinition card)
    {
        Show(card, 0, 1);
    }

    public void Show(SkillCardDefinition card, int fromLevel, int toLevel)
    {
        if (card == null || revealPanel == null) return;

        EnsureCardRect();
        EnsureCardIcon();
        revealPanel.SetActive(true);
        revealPanel.transform.SetAsLastSibling();
        // Keep the dimmer stable; only the card itself should pop in.
        revealPanel.transform.localScale = Vector3.one;

        previousLevel = Mathf.Max(0, fromLevel);
        nextLevel = Mathf.Clamp(toLevel, 1, 3);
        activeCard = card;
        SetFrontFace(card);
        frameAccent = Color.Lerp(Color.Lerp(card.cardColor, Color.white, 0.42f), new Color(0.80f, 0.88f, 0.96f), 0.30f);
        if (cardBackground != null)
        {
            // 与三选一卡同一套玻璃语言：底色接近背景，卡色只留一点色相倾向。
            MonsterSurvivorArtLibrary.ApplySliced(cardBackground, "ui_button_blue",
                Color.Lerp(new Color(0.03f, 0.06f, 0.10f, 0.88f), card.cardColor, 0.08f));
        }
        if (glowBorder != null)
        {
            glowBorder.sprite = MonsterSurvivorArtLibrary.CardFrame();
            glowBorder.type = Image.Type.Sliced;
            glowBorder.color = frameAccent;
        }
        if (cardIcon != null)
        {
            cardIcon.sprite = MonsterSurvivorArtLibrary.SkillIcon(card.linkedUpgradeType);
            cardIcon.gameObject.SetActive(cardIcon.sprite != null);
        }

        if (cardRect != null)
        {
            cardRect.localScale = Vector3.zero;
            cardRect.localRotation = Quaternion.identity;
        }

        isShowing = true;
        isFlipping = false;
        showingEducation = false;
        flipTimer = 0f;
        timer = 0f;
        Time.timeScale = 0f;
    }

    public void EnsureCardIcon()
    {
        if (cardRect == null) return;
        if (cardIcon == null)
        {
            Transform existing = cardRect.Find("RevealSkillIcon");
            if (existing != null) cardIcon = existing.GetComponent<Image>();
        }
        if (cardIcon == null)
        {
            GameObject iconObject = new GameObject("RevealSkillIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(cardRect, false);
            cardIcon = iconObject.GetComponent<Image>();
        }
        cardIcon.raycastTarget = false;
        RectTransform rect = cardIcon.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(66f, 66f);
        rect.anchoredPosition = new Vector2(0f, -22f);
    }

    public void SetFrontFace(SkillCardDefinition card)
    {
        if (cardTitle != null)
        {
            cardTitle.text = card.displayName;
            cardTitle.fontSize = 34;
        }
        if (cardDescription != null)
        {
            cardDescription.text = LevelLine() + "\n\n" + ShortDescription(card);
            cardDescription.fontSize = 22;
            cardDescription.alignment = TextAnchor.MiddleCenter;
        }
        if (educationText != null)
        {
            educationText.text = "翻牌查看科普";
            educationText.fontSize = 15;
            educationText.alignment = TextAnchor.MiddleCenter;
            educationText.color = new Color(0.62f, 0.78f, 0.86f, 1f);
        }
    }

    public void SetEducationFace(SkillCardDefinition card)
    {
        if (cardTitle != null)
        {
            cardTitle.text = "科普";
            cardTitle.fontSize = 28;
        }
        if (cardDescription != null)
        {
            cardDescription.text = card.educationText;
            cardDescription.fontSize = 21;
            cardDescription.alignment = TextAnchor.MiddleCenter;
        }
        if (educationText != null)
        {
            educationText.text = card.displayName + " · 医学提示";
            educationText.fontSize = 15;
            educationText.alignment = TextAnchor.MiddleCenter;
            educationText.color = new Color(1f, 0.82f, 0.38f, 1f);
        }
        if (cardBackground != null)
            MonsterSurvivorArtLibrary.ApplySliced(cardBackground, "ui_button_blue",
                Color.Lerp(new Color(0.03f, 0.06f, 0.10f, 0.94f), frameAccent, 0.05f));
    }

    public string LevelLine()
    {
        return previousLevel <= 0 ? "Lv 1" : (previousLevel >= 3 ? "Lv 3  MAX" : "Lv " + previousLevel + " → " + nextLevel);
    }

    public string ShortDescription(SkillCardDefinition card)
    {
        if (card == null) return "选择后获得新的战斗强化";
        if (VesselSkillCatalog.IsVesselSkill(card.linkedUpgradeType)) return VesselSkillCatalog.ShortDescriptionFor(card.linkedUpgradeType);
        string text = card.description == null ? "" : card.description;
        return text.Length > 46 ? text.Substring(0, 46) + "…" : text;
    }

    void Update()
    {
        if (!isShowing || revealPanel == null) return;

        timer += Time.unscaledDeltaTime;
        EnsureCardRect();
        if (cardRect == null) return;

        float t = Mathf.Clamp01(timer / 0.35f);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float scale = eased * 1.04f;
        if (t >= 1f) scale = 1f;
        cardRect.localScale = Vector3.one * scale;

        if (!isFlipping && !showingEducation && timer >= frontHold)
        {
            isFlipping = true;
            flipTimer = 0f;
        }
        if (isFlipping)
        {
            flipTimer += Time.unscaledDeltaTime / Mathf.Max(0.1f, flipDuration);
            // uGUI Images are back-face culled at 180 degrees. A horizontal
            // squash keeps the flip illusion while guaranteeing the education
            // face remains visible after the midpoint.
            float flipAngle = Mathf.Lerp(0f, 180f, Mathf.Clamp01(flipTimer));
            float width = Mathf.Max(0.025f, Mathf.Abs(Mathf.Cos(flipAngle * Mathf.Deg2Rad)));
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = new Vector3(width, 1f, 1f);
            if (!showingEducation && flipTimer >= 0.5f)
            {
                showingEducation = true;
                SkillCardDefinition card = CurrentCard();
                if (card != null) SetEducationFace(card);
            }
            if (flipTimer >= 1f)
            {
                isFlipping = false;
                cardRect.localRotation = Quaternion.identity;
                cardRect.localScale = Vector3.one;
            }
        }

        if (glowBorder != null)
        {
            // 边框呼吸，替代原来钉死的金色描边：细发光线比整块高饱和色更像"奖励卡"。
            float breathe = 0.5f + 0.5f * Mathf.Sin(timer * 3.2f);
            glowBorder.color = new Color(frameAccent.r, frameAccent.g, frameAccent.b,
                Mathf.Lerp(0.60f, 0.95f, breathe));
        }

        if (showingEducation && !isFlipping && timer > frontHold + flipDuration + 0.45f &&
            (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            Dismiss();
            return;
        }

        float educationDuration = Mathf.Max(1.2f, showDuration - frontHold - flipDuration);
        if (showingEducation && !isFlipping && timer >= frontHold + flipDuration + educationDuration) Dismiss();
    }

    public SkillCardDefinition CurrentCard()
    {
        return activeCard;
    }

    public void Dismiss()
    {
        isShowing = false;
        if (revealPanel != null) revealPanel.SetActive(false);
        if (cardRect != null)
        {
            cardRect.localScale = Vector3.one;
            cardRect.localRotation = Quaternion.identity;
        }
        activeCard = null;

        LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
        if ((levels == null || !levels.isChoosing) &&
            (SkillCodexManager.Instance == null || !SkillCodexManager.Instance.panelOpen) &&
            (GameManager.Instance == null || !GameManager.Instance.gameEnded))
        {
            UIManager testUI = FindAnyObjectByType<UIManager>();
            Time.timeScale = testUI != null && testUI.speedSixX ? testUI.testSpeedMultiplier : 1f;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // displayFont 是共享资源（RuntimeFont），不能销毁
    }
}
