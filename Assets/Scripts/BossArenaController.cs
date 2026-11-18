using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Boss 出场后把战场封成一块比摄像机视野更大的竞技场：
// 四周生成墙体，摄像机恢复跟随但被夹在竞技场窗口内，玩家与 Boss 只能在墙内周旋。
[DefaultExecutionOrder(101)]
public class BossArenaController : MonoBehaviour
{
    public bool active;
    public Vector2 center;
    public Vector2 halfExtents;
    public float cameraOrtho;
    public float playerMargin = 0.45f;
    public float bossMargin = 0.7f;
    public float wallThickness = 0.6f;

    public Camera viewCamera;
    public CameraFollow camFollow;
    public Transform player;
    public Rigidbody2D playerRb;
    public VesselNodeBoss boss;

    readonly List<GameObject> walls = new List<GameObject>();
    Sprite wallSprite;

    // 警告演出：实时时间驱动（Boss _intro 时停期间也播放）。
    GameObject bannerRoot;
    CanvasGroup bannerGroup;
    RectTransform topBar, bottomBar, titleRect;
    Image dimImage, ecgImage;
    Text tagText, titleText, subText;
    float bannerStart = -1f;
    float bannerOutStart = -1f;
    const float BarHeight = 192f;

    public void ShowBanner(string title)
    {
        if (bannerRoot != null) Destroy(bannerRoot);
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        bannerRoot = new GameObject("BossBanner", typeof(RectTransform), typeof(CanvasGroup));
        bannerRoot.transform.SetParent(canvas.transform, false);
        bannerRoot.transform.SetAsLastSibling();
        RectTransform rect = bannerRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        bannerGroup = bannerRoot.GetComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(bannerRoot.transform, false);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
        dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0.039f, 0.031f, 0.024f, 0f);
        dimImage.raycastTarget = false;

        topBar = MakeBar("TopBar", true);
        bottomBar = MakeBar("BottomBar", false);
        tagText = MakeText("Tag", "VESSEL NODE REACTION · 血管节点反应", 30, UITheme.Clinical,
            new Vector2(0f, 116f), new Vector2(1400f, 44f), false);
        titleText = MakeText("Title", title, 76, UITheme.WarmSoft,
            new Vector2(0f, 40f), new Vector2(1700f, 110f), true);
        titleRect = titleText.rectTransform;

        GameObject ecgObj = new GameObject("Ecg", typeof(RectTransform), typeof(Image));
        ecgObj.transform.SetParent(bannerRoot.transform, false);
        RectTransform ecgRect = ecgObj.GetComponent<RectTransform>();
        ecgRect.anchorMin = ecgRect.anchorMax = Vector2.one * 0.5f;
        ecgRect.sizeDelta = new Vector2(960f, 100f);
        ecgRect.anchoredPosition = new Vector2(0f, -60f);
        ecgImage = ecgObj.GetComponent<Image>();
        ecgImage.sprite = BannerEcgSprite();
        ecgImage.color = Color.white;
        ecgImage.raycastTarget = false;
        ecgImage.type = Image.Type.Filled;
        ecgImage.fillMethod = Image.FillMethod.Horizontal;
        ecgImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        ecgImage.fillAmount = 0f;

        subText = MakeText("Sub", "歼灭节点以解除封锁 · 战场边界已扩展", 28, UITheme.TextMuted,
            new Vector2(0f, -150f), new Vector2(1400f, 40f), false);

        bannerStart = Time.realtimeSinceStartup;
        bannerOutStart = -1f;
        ApplyBanner(0f);
    }

    RectTransform MakeBar(string name, bool top)
    {
        GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(bannerRoot.transform, false);
        RectTransform r = bar.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, top ? 1f : 0f);
        r.anchorMax = new Vector2(1f, top ? 1f : 0f);
        r.pivot = new Vector2(0.5f, top ? 1f : 0f);
        r.sizeDelta = new Vector2(0f, BarHeight);
        r.anchoredPosition = new Vector2(0f, top ? BarHeight : -BarHeight);
        Image img = bar.GetComponent<Image>();
        img.color = new Color(0.039f, 0.031f, 0.024f, 0.94f);
        img.raycastTarget = false;

        GameObject edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        edge.transform.SetParent(bar.transform, false);
        RectTransform er = edge.GetComponent<RectTransform>();
        er.anchorMin = new Vector2(0f, top ? 0f : 1f);
        er.anchorMax = new Vector2(1f, top ? 0f : 1f);
        er.pivot = new Vector2(0.5f, top ? 0f : 1f);
        er.sizeDelta = new Vector2(0f, 4f);
        er.anchoredPosition = Vector2.zero;
        Image ei = edge.GetComponent<Image>();
        ei.color = UITheme.HexA("#D9A05B", 0.5f);
        ei.raycastTarget = false;
        return r;
    }

    Text MakeText(string name, string content, int size, Color color, Vector2 pos, Vector2 box, bool title)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(bannerRoot.transform, false);
        Text t = obj.GetComponent<Text>();
        t.font = title ? RuntimeFont.GetTitle() : RuntimeFont.Get();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = Vector2.one * 0.5f;
        r.sizeDelta = box;
        r.anchoredPosition = pos;
        if (title)
        {
            Outline o = obj.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.7f);
            o.effectDistance = new Vector2(2f, -2f);
        }
        return t;
    }

    static void SetAlpha(Text t, float a)
    {
        if (t == null) return;
        Color c = t.color; c.a = a; t.color = c;
    }

    void ApplyBanner(float t)
    {
        float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.3f), 3f);
        float barOff = BarHeight * (1f - ease);
        topBar.anchoredPosition = new Vector2(0f, barOff);
        bottomBar.anchoredPosition = new Vector2(0f, -barOff);
        Color dim = dimImage.color; dim.a = 0.55f * ease; dimImage.color = dim;
        SetAlpha(tagText, Mathf.Clamp01((t - 0.15f) / 0.3f));
        float tu = Mathf.Clamp01((t - 0.25f) / 0.35f);
        SetAlpha(titleText, tu);
        titleRect.anchoredPosition = new Vector2(0f, 40f + 36f * (1f - tu));
        ecgImage.fillAmount = Mathf.Clamp01((t - 0.4f) / 0.7f);
        SetAlpha(subText, 0.9f * Mathf.Clamp01((t - 0.6f) / 0.3f));
    }

    public void HideBanner()
    {
        if (bannerRoot == null) return;
        if (bannerOutStart < 0f) bannerOutStart = Time.realtimeSinceStartup;
    }

    void Update()
    {
        if (bannerRoot == null) return;
        float now = Time.realtimeSinceStartup;
        if (bannerOutStart >= 0f)
        {
            float u = Mathf.Clamp01((now - bannerOutStart) / 0.3f);
            bannerGroup.alpha = 1f - u;
            float off = BarHeight * u * u;
            topBar.anchoredPosition = new Vector2(0f, off);
            bottomBar.anchoredPosition = new Vector2(0f, -off);
            if (u >= 1f) { Destroy(bannerRoot); bannerRoot = null; }
            return;
        }
        ApplyBanner(now - bannerStart);
    }

    static Sprite bannerEcgSprite;
    static Sprite BannerEcgSprite()
    {
        if (bannerEcgSprite != null) return bannerEcgSprite;
        int w = 960, h = 100;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);
        tex.SetPixels(px);
        Color teal = UITheme.Clinical; teal.a = 0.55f;
        DrawSeg(tex, 0, 50, w - 1, 50, teal, 4);
        Color amber = UITheme.WarmAccent;
        Vector2Int[] pts = {
            new Vector2Int(400, 50), new Vector2Int(430, 50), new Vector2Int(444, 78),
            new Vector2Int(460, 26), new Vector2Int(476, 94), new Vector2Int(492, 38),
            new Vector2Int(508, 50), new Vector2Int(560, 50)
        };
        for (int i = 0; i < pts.Length - 1; i++)
            DrawSeg(tex, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, amber, 5);
        for (int dy = -6; dy <= 6; dy++)
            for (int dx = -6; dx <= 6; dx++)
                if (dx * dx + dy * dy <= 36 && 476 + dx >= 0 && 476 + dx < w && 94 + dy >= 0 && 94 + dy < h)
                    tex.SetPixel(476 + dx, 94 + dy, amber);
        tex.Apply();
        bannerEcgSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
        return bannerEcgSprite;
    }

    static void DrawSeg(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thick)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        int half = thick / 2;
        for (int i = 0; i <= steps; i++)
        {
            float u = steps == 0 ? 0f : (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, u));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, u));
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    if (x + dx >= 0 && x + dx < tex.width && y + dy >= 0 && y + dy < tex.height)
                        tex.SetPixel(x + dx, y + dy, c);
        }
    }

    public void BuildArena(Vector2 arenaCenter, Vector2 arenaHalfExtents, float ortho, Transform playerTransform, VesselNodeBoss bossRef)
    {
        center = arenaCenter;
        halfExtents = arenaHalfExtents;
        cameraOrtho = Mathf.Max(1f, ortho);
        player = playerTransform;
        boss = bossRef;
        if (player != null) playerRb = player.GetComponent<Rigidbody2D>();

        camFollow = FindAnyObjectByType<CameraFollow>();
        viewCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        wallThickness = GameBalance.Get("boss.wall_thickness", wallThickness);

        EnsureWallSprite();
        float t = wallThickness;
        float w = halfExtents.x, h = halfExtents.y;
        CreateWall(new Vector2(center.x, center.y + h + t * 0.5f), new Vector2(2f * w + 2f * t, t));
        CreateWall(new Vector2(center.x, center.y - h - t * 0.5f), new Vector2(2f * w + 2f * t, t));
        CreateWall(new Vector2(center.x - w - t * 0.5f, center.y), new Vector2(t, 2f * h));
        CreateWall(new Vector2(center.x + w + t * 0.5f, center.y), new Vector2(t, 2f * h));

        LockCamera();
        active = true;
    }

    void EnsureWallSprite()
    {
        if (wallSprite != null) return;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        wallSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void CreateWall(Vector2 position, Vector2 size)
    {
        GameObject wall = new GameObject("ArenaWall", typeof(SpriteRenderer), typeof(BoxCollider2D));
        wall.transform.SetParent(transform, false);
        wall.transform.position = position;
        wall.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
        renderer.sprite = wallSprite;
        renderer.color = new Color(0.5f, 0.1f, 0.14f, 0.95f);
        renderer.sortingOrder = 40;
        BoxCollider2D collider = wall.GetComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        walls.Add(wall);
    }

    // 摄像机跟随主角，但视野窗口不能越出竞技场：夹在 center±(halfExtents−viewHalf)。
    // 主角贴近墙边时再反向推回，保证角色完整在屏内（此时露出墙体作为边界反馈）。
    // 执行顺序在 CameraFollow(100) 之后，顺带冻结升级缩放。
    void LockCamera()
    {
        if (viewCamera == null) return;
        viewCamera.orthographic = true;
        viewCamera.orthographicSize = cameraOrtho;
        Vector2 viewHalf = new Vector2(cameraOrtho * viewCamera.aspect, cameraOrtho);
        Vector2 slack = new Vector2(Mathf.Max(0f, halfExtents.x - viewHalf.x), Mathf.Max(0f, halfExtents.y - viewHalf.y));
        Vector3 p = viewCamera.transform.position;
        p.x = Mathf.Clamp(p.x, center.x - slack.x, center.x + slack.x);
        p.y = Mathf.Clamp(p.y, center.y - slack.y, center.y + slack.y);
        if (player != null)
        {
            // 垂直 padding 额外留出顶部 HUD 高度，贴边时角色不被血条面板遮住。
            Vector2 pad = new Vector2(0.6f, 2.6f);
            Vector3 pp = player.position;
            p.x = Mathf.Clamp(p.x, pp.x - (viewHalf.x - pad.x), pp.x + (viewHalf.x - pad.x));
            p.y = Mathf.Clamp(p.y, pp.y - (viewHalf.y - pad.y), pp.y + (viewHalf.y - pad.y));
        }
        p.z = -10f;
        viewCamera.transform.position = p;
    }

    void LateUpdate()
    {
        if (!active) return;
        if (boss == null || boss.defeated || (GameManager.Instance != null && GameManager.Instance.gameEnded))
        {
            Release();
            return;
        }

        LockCamera();

        if (playerRb != null)
        {
            Vector2 p = playerRb.position;
            p.x = Mathf.Clamp(p.x, center.x - halfExtents.x + playerMargin, center.x + halfExtents.x - playerMargin);
            p.y = Mathf.Clamp(p.y, center.y - halfExtents.y + playerMargin, center.y + halfExtents.y - playerMargin);
            playerRb.position = p;
        }
        if (boss != null)
        {
            Vector3 bp = boss.transform.position;
            bp.x = Mathf.Clamp(bp.x, center.x - halfExtents.x + bossMargin, center.x + halfExtents.x - bossMargin);
            bp.y = Mathf.Clamp(bp.y, center.y - halfExtents.y + bossMargin, center.y + halfExtents.y - bossMargin);
            boss.transform.position = bp;
        }
    }

    public void Release()
    {
        if (!active) return;
        active = false;
        for (int i = 0; i < walls.Count; i++) if (walls[i] != null) Destroy(walls[i]);
        walls.Clear();
        HideBanner();
        if (camFollow != null)
        {
            camFollow.enabled = true;
            camFollow.SnapToTarget();
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < walls.Count; i++) if (walls[i] != null) Destroy(walls[i]);
        walls.Clear();
        if (bannerRoot != null) Destroy(bannerRoot);
        bannerRoot = null;
        if (camFollow != null) camFollow.enabled = true;
    }
}