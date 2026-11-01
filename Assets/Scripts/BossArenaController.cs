using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Boss 出场后把战场封成一块与摄像机视野等大的竞技场：
// 四周生成墙体，锁定摄像机，玩家与 Boss 只能在墙内周旋。
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
    GameObject bannerRoot;

    public void ShowBanner(string title)
    {
        HideBanner();
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        bannerRoot = new GameObject("BossBanner", typeof(RectTransform), typeof(CanvasGroup));
        bannerRoot.transform.SetParent(canvas.transform, false);
        bannerRoot.transform.SetAsLastSibling();
        RectTransform rect = bannerRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(bannerRoot.transform, false);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0.02f, 0f, 0.03f, 0.6f);
        dim.GetComponent<Image>().raycastTarget = false;

        GameObject textObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(bannerRoot.transform, false);
        Text text = textObj.GetComponent<Text>();
        text.font = RuntimeFont.Get();
        text.text = title;
        text.fontSize = 64;
        text.color = new Color(1f, 0.32f, 0.26f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = Vector2.one * 0.5f;
        textRect.sizeDelta = new Vector2(1200f, 120f);
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    public void HideBanner()
    {
        if (bannerRoot != null) Destroy(bannerRoot);
        bannerRoot = null;
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

        if (camFollow != null) camFollow.enabled = false;
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

    void LockCamera()
    {
        if (viewCamera == null) return;
        viewCamera.orthographic = true;
        viewCamera.orthographicSize = cameraOrtho;
        viewCamera.transform.position = new Vector3(center.x, center.y, -10f);
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
        HideBanner();
        if (camFollow != null) camFollow.enabled = true;
    }
}