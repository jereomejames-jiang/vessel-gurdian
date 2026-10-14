using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KnowledgeTicker : MonoBehaviour
{
    public static KnowledgeTicker Instance;
    public Canvas canvas;
    public GameObject root;
    public RectTransform movingLabel;
    public Text label;
    public CanvasGroup group;
    // Keep existing public fields compatible with scenes and rebuilding tools.
    public float tickerDuration = 10f;
    public float visibleSeconds = 12f;
    public float edgePadding = 1100f;
    public float elapsed;
    public float visibleTimer;
    public bool scrolling;

    public float enterDuration = 0.38f;
    public float exitDuration = 0.45f;
    public float minimumReadSeconds = 5f;
    public float scrollSpeed = 55f;
    public float bottomMargin = 32f;
    public float panelWidth = 880f;
    public RectTransform plate;
    public RectTransform viewport;
    public Text skillTitle;
    public Text categoryText;
    public KnowledgePlateGraphic emblem;
    public Image flash;
    public Font displayFont;
    public float activeHoldSeconds;
    public float overflowWidth;
    public float renderedWidth;
    [System.NonSerialized]
    public Queue<KnowledgeNotice> pending = new Queue<KnowledgeNotice>();

    [System.Serializable]
    public class KnowledgeNotice
    {
        public string title;
        public string body;
        public Color accent;
        public KnowledgeNotice(string title, string body, Color accent)
        {
            this.title = title; this.body = body; this.accent = accent;
        }
    }

    void Awake() { Instance = this; EnsureView(); }
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.gameEnded) { Clear(); return; }
        // A paused upgrade/codex screen neither consumes messages nor shows a competing banner.
        if (Time.timeScale <= 0f) { if (group != null) group.alpha = 0f; return; }
        Tick(Time.deltaTime);
    }

    public void ShowForUpgrade(UpgradeOption option)
    {
        if (option == null) return;
        SkillCardDefinition card = SkillCodexManager.Instance == null ? option.skillCard
            : SkillCodexManager.Instance.GetCardFor(option);
        Color accent = card != null ? card.cardColor : new Color(0.16f, 0.65f, 0.85f);
        if (card != null && accent == Color.white && SkillCodexManager.Instance != null)
            accent = SkillCodexManager.Instance.ColorFor(card.linkedUpgradeType);
        pending.Enqueue(new KnowledgeNotice("已获得  ·  " + option.displayName,
            card != null && !string.IsNullOrEmpty(card.educationText) ? card.educationText : DefaultEducation(option.type),
            accent));
    }

    public void ShowMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        pending.Enqueue(new KnowledgeNotice("血管守卫  ·  知识卡片", message, new Color(0.16f, 0.65f, 0.85f)));
    }

    public void Tick(float deltaTime)
    {
        EnsureView();
        if (group == null || deltaTime <= 0f) return;
        if (!scrolling)
        {
            if (pending.Count == 0) { group.alpha = 0f; return; }
            BeginNotice(pending.Dequeue());
        }
        elapsed += deltaTime;
        float enter = Mathf.Max(0.01f, enterDuration);
        float exit = Mathf.Max(0.01f, exitDuration);
        float total = enter + activeHoldSeconds + exit;
        visibleTimer = Mathf.Max(0f, total - elapsed);
        ApplyAppearance();
        if (elapsed >= total)
        {
            scrolling = false;
            group.alpha = 0f;
        }
    }

    public void BeginNotice(KnowledgeNotice notice)
    {
        EnsureView();
        if (label == null) return;
        skillTitle.text = notice.title;
        label.text = notice.body;
        emblem.accent = notice.accent;
        emblem.SetVerticesDirty();
        elapsed = 0f;
        scrolling = true;
        LayoutPlate();
        overflowWidth = Mathf.Max(0f, label.preferredWidth - viewport.rect.width);
        movingLabel.sizeDelta = new Vector2(Mathf.Max(viewport.rect.width, label.preferredWidth + 4f), 38f);
        movingLabel.anchoredPosition = Vector2.zero;
        // Read short text in place. Long text scrolls only after the entrance and a reading pause.
        activeHoldSeconds = Mathf.Max(minimumReadSeconds,
            Mathf.Min(Mathf.Max(1f, tickerDuration), Mathf.Max(1f, visibleSeconds)));
        if (overflowWidth > 0f)
            activeHoldSeconds = Mathf.Max(activeHoldSeconds, 2.4f + overflowWidth / Mathf.Max(10f, scrollSpeed));
    }

    public void ApplyAppearance()
    {
        float enter = Mathf.Max(0.01f, enterDuration);
        float exit = Mathf.Max(0.01f, exitDuration);
        float p = Mathf.Clamp01(elapsed / enter);
        float end = Mathf.Clamp01((elapsed - enter - activeHoldSeconds) / exit);
        float eased = 1f - Mathf.Pow(1f - p, 3f);
        float pop = 1f + 2.4f * Mathf.Pow(p - 1f, 3f) + 1.4f * Mathf.Pow(p - 1f, 2f);
        group.alpha = Mathf.SmoothStep(0f, 1f, p) * (1f - end);
        plate.anchoredPosition = new Vector2(0f, bottomMargin - 24f * (1f - eased) - 12f * end);
        plate.localScale = Vector3.one * (Mathf.LerpUnclamped(0.92f, 1f, pop) - 0.025f * end);
        float bodyTime = Mathf.Max(0f, elapsed - enter - 1.5f);
        float scroll = Mathf.Min(overflowWidth, bodyTime * Mathf.Max(10f, scrollSpeed));
        movingLabel.anchoredPosition = new Vector2(-scroll, -4f * (1f - eased));
        Color flashColor = flash.color;
        flashColor.a = 0.24f * Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.85f), 2f);
        flash.color = flashColor;
    }

    public void Clear()
    {
        pending.Clear(); scrolling = false;
        if (group != null) group.alpha = 0f;
    }

    public string DefaultEducation(UpgradeType type)
    {
        return "示例科普：选择健康的生活习惯，让每一次行动成为守护血管的一小步。";
    }

    public void EnsureView()
    {
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        if (plate != null) return;
        // Migrate the previous ticker view if an existing object was explicitly assigned.
        if (root != null) { root.SetActive(false); Destroy(root); }
        displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        root = new GameObject("KnowledgeTicker", typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasRenderer));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsFirstSibling();
        plate = root.GetComponent<RectTransform>();
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.sizeDelta = new Vector2(panelWidth, 124f);
        plate.anchoredPosition = new Vector2(0f, bottomMargin);
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;
        KnowledgePlateGraphic frame = root.AddComponent<KnowledgePlateGraphic>();
        frame.raycastTarget = false;
        RectTransform badge = MakeRect("SkillSeal", plate, new Vector2(70f, 78f), new Vector2(51f, 65f));
        badge.anchorMin = badge.anchorMax = new Vector2(0f, 0f);
        badge.gameObject.AddComponent<CanvasRenderer>();
        emblem = badge.gameObject.AddComponent<KnowledgePlateGraphic>();
        emblem.medallion = true; emblem.raycastTarget = false;
        Text seal = MakeText("SealLabel", badge, "知", 25, new Color(1f, 0.92f, 0.66f));
        seal.alignment = TextAnchor.MiddleCenter;
        seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = Vector2.one * 0.5f;
        seal.rectTransform.sizeDelta = new Vector2(50f, 60f);

        skillTitle = MakeText("SkillTitle", plate, "", 23, new Color(1f, 0.86f, 0.52f));
        categoryText = MakeText("Category", plate, "血管知识 · 示例", 17, new Color(0.8f, 0.68f, 0.47f));
        categoryText.alignment = TextAnchor.MiddleRight;

        viewport = MakeRect("ParchmentViewport", plate, new Vector2(750f, 38f), new Vector2(100f, 28f));
        viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 0f);
        viewport.pivot = new Vector2(0f, 0.5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        label = MakeText("TickerText", viewport, "", 23, new Color(0.22f, 0.13f, 0.07f));
        movingLabel = label.rectTransform;
        movingLabel.anchorMin = movingLabel.anchorMax = new Vector2(0f, 0.5f);
        movingLabel.pivot = new Vector2(0f, 0.5f);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform gleam = MakeRect("SelectionGlow", plate, new Vector2(panelWidth - 24f, 42f), new Vector2(0f, 88f));
        flash = gleam.gameObject.AddComponent<Image>();
        flash.color = new Color(1f, 0.85f, 0.48f, 0f);
        flash.raycastTarget = false;
        LayoutPlate();
    }

    public void LayoutPlate()
    {
        float canvasWidth = ((RectTransform)canvas.transform).rect.width;
        renderedWidth = Mathf.Max(240f, Mathf.Min(panelWidth, canvasWidth - 32f));
        plate.sizeDelta = new Vector2(renderedWidth, 124f);
        viewport.sizeDelta = new Vector2(Mathf.Max(100f, renderedWidth - 135f), 38f);
        bool showCategory = renderedWidth >= 620f;
        categoryText.gameObject.SetActive(showCategory);
        SetTextRect(skillTitle, new Vector2(100f, 88f), new Vector2(Mathf.Max(80f, renderedWidth - (showCategory ? 340f : 135f)), 38f));
        SetTextRect(categoryText, new Vector2(renderedWidth - 221f, 88f), new Vector2(185f, 32f));
        flash.rectTransform.sizeDelta = new Vector2(renderedWidth - 24f, 40f);
    }

    public void SetTextRect(Text text, Vector2 position, Vector2 size)
    {
        RectTransform r = text.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = position; r.sizeDelta = size;
    }

    public RectTransform MakeRect(string objectName, Transform parent, Vector2 size, Vector2 position)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.anchoredPosition = position; r.sizeDelta = size;
        return r;
    }

    public Text MakeText(string objectName, Transform parent, string text, int size, Color tint)
    {
        RectTransform r = MakeRect(objectName, parent, new Vector2(600f, 38f), Vector2.zero);
        Text result = r.gameObject.AddComponent<Text>();
        result.font = displayFont; result.fontSize = size; result.text = text; result.color = tint;
        result.raycastTarget = false; result.alignment = TextAnchor.MiddleLeft;
        result.verticalOverflow = VerticalWrapMode.Truncate;
        return result;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (displayFont != null) Destroy(displayFont);
    }
}
