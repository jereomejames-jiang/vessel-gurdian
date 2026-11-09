using UnityEngine;
using UnityEngine.UI;

// 受击反馈：红色边缘晕影 + 镜头震动 + 无敌帧期间角色闪烁，
// 让"被打到"这件事在画面上立刻可感知，同时把无敌帧可视化。
public class PlayerHitFeedback : MonoBehaviour
{
    PlayerStats stats;
    SpriteRenderer selfRenderer;
    Color baseColor;
    CameraFollow cameraFollow;
    Image vignette;
    float vignetteAlpha;
    // 顿帧状态：用真实时间(unscaledTime)计时，保证 timeScale=0 时也能恢复，绝不卡死。
    float hitStopUntil;
    float resumeScale = 1f;
    bool hitStopActive;

    void Start()
    {
        stats = GetComponent<PlayerStats>();
        selfRenderer = GetComponent<SpriteRenderer>();
        if (selfRenderer != null) baseColor = selfRenderer.color;
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        if (stats != null) stats.OnDamaged += HandleDamaged;
        BuildVignette();
    }

    void HandleDamaged(int amount)
    {
        float peak = GameBalance.Get("player.hit_vignette", 0.45f);
        vignetteAlpha = Mathf.Clamp(peak * (0.6f + amount * 0.12f), peak * 0.5f, peak);
        if (cameraFollow != null)
            cameraFollow.Shake(GameBalance.Get("player.hit_shake", 0.55f),
                               GameBalance.Get("player.hit_shake_time", 0.28f));
        TryHitStop(amount);
    }

    // 顿帧：命中瞬间把 timeScale 压到 0，制造"打在身上"的顿挫感。
    // 只在正常游玩时触发；死亡/暂停/Boss出场/升级卡已经在管 timeScale 时让位。
    void TryHitStop(int amount)
    {
        if (stats == null || stats.currentHP <= 0) return;
        if (GameManager.Instance != null && GameManager.Instance.gameEnded) return;
        if (Time.timeScale <= 0f) return;
        float dur = GameBalance.Get("player.hit_stop", 0.06f)
                  + amount * GameBalance.Get("player.hit_stop_per_dmg", 0.012f);
        dur = Mathf.Clamp(dur, 0f, GameBalance.Get("player.hit_stop_max", 0.12f));
        if (dur <= 0f) return;
        resumeScale = Time.timeScale;
        hitStopUntil = Time.unscaledTime + dur;
        hitStopActive = true;
        Time.timeScale = 0f;
    }

    void Update()
    {
        // 顿帧恢复走真实时间，即使 timeScale=0 也要执行，避免卡死。
        if (hitStopActive && Time.unscaledTime >= hitStopUntil)
        {
            hitStopActive = false;
            // 若期间死亡或游戏结束，交由死亡/结算系统继续持有 timeScale=0，不要抢回。
            bool ownedByOther = (stats != null && stats.currentHP <= 0)
                || (GameManager.Instance != null && GameManager.Instance.gameEnded);
            if (!ownedByOther) Time.timeScale = resumeScale;
        }

        if (Time.timeScale <= 0f) return;

        if (vignette != null && vignetteAlpha > 0f)
        {
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f,
                Time.deltaTime * GameBalance.Get("player.hit_vignette_fade", 2f));
            Color c = vignette.color; c.a = vignetteAlpha; vignette.color = c;
        }

        if (selfRenderer != null && stats != null)
        {
            if (Time.time < stats.invulnerableUntil)
            {
                float blink = Mathf.Sin(Time.time * 32f) * 0.5f + 0.5f;
                Color c = baseColor;
                c.a = Mathf.Lerp(0.28f, 1f, blink);
                selfRenderer.color = c;
            }
            else if (selfRenderer.color != baseColor)
            {
                selfRenderer.color = baseColor;
            }
        }
    }

    void BuildVignette()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        GameObject go = new GameObject("DamageVignette", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        vignette = go.AddComponent<Image>();
        vignette.raycastTarget = false;
        Texture2D tex = MakeVignetteTexture(128);
        vignette.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        vignette.color = new Color(1f, 1f, 1f, 0f);
    }

    // 中心透明、四周发红的径向晕影，避免遮挡战场中央视野。
    Texture2D MakeVignetteTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421356f;
                float a = Mathf.Pow(Mathf.Clamp01((d - 0.55f) / 0.45f), 2f);
                px[y * size + x] = new Color(0.85f, 0.05f, 0.08f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnDamaged -= HandleDamaged;
    }
}
