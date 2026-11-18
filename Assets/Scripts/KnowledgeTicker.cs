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

    public float enterDuration = 0.30f;
    public float exitDuration = 0.35f;
    public float minimumReadSeconds = 5f;
    public float scrollSpeed = 55f;
    public float bottomMargin = 54f;
    public float panelWidth = 880f;
    public RectTransform plate;
    public RectTransform viewport;
    public Text skillTitle;
    public KnowledgePlateGraphic emblem;
    public KnowledgePlateGraphic frame;
    public Text seal;
    public Image skillIconImage;
    public Image tickerPulse;
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
        public Sprite icon;
        public KnowledgeNotice(string title, string body, Color accent)
        {
            this.title = title; this.body = body; this.accent = accent;
        }
        public KnowledgeNotice(string title, string body, Color accent, Sprite icon)
        {
            this.title = title; this.body = body; this.accent = accent; this.icon = icon;
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
            accent, MonsterSurvivorArtLibrary.SkillIcon(option.type)));
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
        if (skillIconImage != null)
        {
            skillIconImage.sprite = notice.icon;
            skillIconImage.gameObject.SetActive(notice.icon != null);
        }
        if (seal != null) seal.gameObject.SetActive(notice.icon == null);
        emblem.accent = notice.accent;
        emblem.SetVerticesDirty();
        elapsed = 0f;
        scrolling = true;
        LayoutPlate();
        // 多行包裹展示，不再横向滚动；停留时长按文字量计算，保证可读。
        overflowWidth = 0f;
        movingLabel.anchoredPosition = Vector2.zero;
        int chars = string.IsNullOrEmpty(notice.body) ? 0 : notice.body.Length;
        activeHoldSeconds = Mathf.Clamp(chars * 0.18f, minimumReadSeconds, 14f);
    }

    public void ApplyAppearance()
    {
        float enter = Mathf.Max(0.01f, enterDuration);
        float exit = Mathf.Max(0.01f, exitDuration);
        float p = Mathf.Clamp01(elapsed / enter);
        float end = Mathf.Clamp01((elapsed - enter - activeHoldSeconds) / exit);
        float eased = 1f - Mathf.Pow(1f - p, 3f);
        group.alpha = eased * (1f - end) * (1f - end);
        plate.anchoredPosition = new Vector2(0f, bottomMargin - 24f * (1f - eased) - 12f * end);
        plate.localScale = Vector3.one * Mathf.Lerp(0.98f, 1f, eased);
        // 图标盖章：1.25→0.95→1，落定在病历井里。
        float u = Mathf.Clamp01((elapsed - 0.05f) / 0.35f);
        float s = elapsed >= 0.4f ? 1f
            : u < 0.6f ? Mathf.Lerp(1.25f, 0.95f, u / 0.6f)
            : Mathf.Lerp(0.95f, 1f, (u - 0.6f) / 0.4f);
        if (skillIconImage != null) skillIconImage.rectTransform.localScale = Vector3.one * s;
        if (seal != null) seal.rectTransform.localScale = Vector3.one * s;
        // 分隔线左→右画出，驱动框体 dividerT。
        float du = Mathf.Clamp01((elapsed - 0.15f) / 0.35f);
        if (frame != null)
        {
            frame.dividerT = du;
            if (du < 1f) frame.SetVerticesDirty();
        }
        // 扫光：入场 0.7s 内一条竖向光带左→右掠过。
        if (flash != null)
        {
            float sw = Mathf.Clamp01(elapsed / 0.7f);
            flash.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-70f, renderedWidth + 10f, sw), 105f);
            Color fc = UITheme.WarmSoft;
            fc.a = elapsed < 0.7f ? 0.10f : 0f;
            flash.color = fc;
        }
    }

    public void Clear()
    {
        pending.Clear(); scrolling = false;
        if (group != null) group.alpha = 0f;
    }

    public string DefaultEducation(UpgradeType type)
    {
        if (VesselSkillCatalog.IsVesselSkill(type)) return VesselSkillCatalog.EducationFor(type);
        return "示例科普：选择健康的生活习惯，让每一次行动成为守护血管的一小步。";
    }

    public void EnsureView()
    {
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        if (plate != null) return;
        // Migrate the previous ticker view if an existing object was explicitly assigned.
        if (root != null) { root.SetActive(false); Destroy(root); }
        // WebGL 无系统字体，统一使用随包分发的 simhei
        displayFont = RuntimeFont.Get();
        root = new GameObject("KnowledgeTicker", typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasRenderer));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsFirstSibling();
        plate = root.GetComponent<RectTransform>();
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.sizeDelta = new Vector2(panelWidth, 210f);
        plate.anchoredPosition = new Vector2(0f, bottomMargin);
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;
        frame = root.AddComponent<KnowledgePlateGraphic>();
        frame.raycastTarget = false;
        RectTransform badge = MakeRect("SkillSeal", plate, new Vector2(84f, 84f), new Vector2(54f, 105f));
        badge.anchorMin = badge.anchorMax = new Vector2(0f, 0f);
        badge.gameObject.AddComponent<CanvasRenderer>();
        emblem = badge.gameObject.AddComponent<KnowledgePlateGraphic>();
        emblem.medallion = true; emblem.raycastTarget = false;
        seal = MakeText("SealLabel", badge, "知", 25, UITheme.InkOnWarm);
        seal.font = RuntimeFont.GetTitle();
        seal.alignment = TextAnchor.MiddleCenter;
        seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = Vector2.one * 0.5f;
        seal.rectTransform.sizeDelta = new Vector2(56f, 66f);
        GameObject iconObj = new GameObject("SkillIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(badge, false);
        skillIconImage = iconObj.GetComponent<Image>();
        skillIconImage.preserveAspect = true;
        skillIconImage.raycastTarget = false;
        RectTransform iconRect = skillIconImage.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = Vector2.one * 0.5f;
        iconRect.sizeDelta = new Vector2(64f, 64f);
        iconRect.anchoredPosition = Vector2.zero;
        skillIconImage.gameObject.SetActive(false);

        skillTitle = MakeText("SkillTitle", plate, "", 26, UITheme.WarmSoft);
        skillTitle.font = RuntimeFont.GetTitle();
        GameObject pulseObj = new GameObject("TickerPulse", typeof(RectTransform), typeof(Image));
        pulseObj.transform.SetParent(plate, false);
        tickerPulse = pulseObj.GetComponent<Image>();
        tickerPulse.sprite = TickerPulseSprite();
        tickerPulse.raycastTarget = false;

        viewport = MakeRect("ParchmentViewport", plate, new Vector2(744f, 116f), new Vector2(116f, 64f));
        viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 0f);
        viewport.pivot = new Vector2(0f, 0.5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        // 烘焙字体无 fontStyle.Bold，正文用 medium 字面 + 加大字号撑重量。
        label = MakeText("TickerText", viewport, "", 22, UITheme.TextPrimary);
        label.font = RuntimeFont.GetTitle();
        label.lineSpacing = 1.25f;
        movingLabel = label.rectTransform;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        movingLabel.anchorMin = Vector2.zero; movingLabel.anchorMax = Vector2.one;
        movingLabel.pivot = new Vector2(0.5f, 0.5f);
        movingLabel.offsetMin = new Vector2(6f, 4f); movingLabel.offsetMax = new Vector2(-6f, -4f);

        RectTransform gleam = MakeRect("SelectionGlow", plate, new Vector2(60f, 190f), new Vector2(0f, 105f));
        gleam.anchorMin = gleam.anchorMax = new Vector2(0f, 0f);
        gleam.pivot = new Vector2(0f, 0.5f);
        flash = gleam.gameObject.AddComponent<Image>();
        Color flashBase = UITheme.WarmSoft; flashBase.a = 0f;
        flash.color = flashBase;
        flash.raycastTarget = false;
        LayoutPlate();
    }

    public void LayoutPlate()
    {
        float canvasWidth = ((RectTransform)canvas.transform).rect.width;
        renderedWidth = Mathf.Max(240f, Mathf.Min(panelWidth, canvasWidth - 32f));
        plate.sizeDelta = new Vector2(renderedWidth, 210f);
        viewport.sizeDelta = new Vector2(Mathf.Max(100f, renderedWidth - 136f), 116f);
        SetTextRect(skillTitle, new Vector2(116f, 172f), new Vector2(Mathf.Max(80f, renderedWidth - 392f), 40f));
        if (tickerPulse != null)
        {
            tickerPulse.gameObject.SetActive(renderedWidth >= 620f);
            RectTransform pr = tickerPulse.rectTransform;
            pr.anchorMin = pr.anchorMax = Vector2.zero;
            pr.pivot = new Vector2(1f, 0.5f);
            pr.sizeDelta = new Vector2(220f, 28f);
            pr.anchoredPosition = new Vector2(renderedWidth - 20f, 172f);
        }
        flash.rectTransform.sizeDelta = new Vector2(60f, 190f);
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
        // displayFont 是共享资源（RuntimeFont），不能销毁
    }

    static Sprite tickerPulseSprite;
    static Sprite TickerPulseSprite()
    {
        if (tickerPulseSprite != null) return tickerPulseSprite;
        int w = 220, h = 28;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[w * h];
        Color baseline = UITheme.HexA("#7FB5AD", 0.55f);
        for (int x = 0; x < w; x++) { px[13 * w + x] = baseline; px[14 * w + x] = baseline; }
        Vector2[] pts = { new Vector2(80, 14), new Vector2(86, 22), new Vector2(92, 4), new Vector2(98, 24), new Vector2(104, 10), new Vector2(110, 14) };
        for (int i = 0; i < pts.Length - 1; i++) PlotLine(px, w, h, pts[i], pts[i + 1], UITheme.WarmAccent, 2);
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = 98 + dx, y = 24 + dy;
                if (dx * dx + dy * dy <= 5 && x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = UITheme.WarmAccent;
            }
        tex.SetPixels(px); tex.Apply();
        tickerPulseSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
        return tickerPulseSprite;
    }

    static void PlotLine(Color[] px, int w, int h, Vector2 a, Vector2 b, Color c, int thick)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * 2f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
            for (int dy = 0; dy < thick; dy++)
                for (int dx = 0; dx < thick; dx++)
                {
                    int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                    if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c;
                }
        }
    }
}
