using System.Collections.Generic;
using UnityEngine;

public enum CharacterEffectKind { Sector, Beam, Burst, Pool, Projectile, Flash, ScissorsShot, Lightning, SpasmRing }

// Lightweight shared geometry; no per-enemy physics query or temporary material.
public class CharacterAttackEffect : MonoBehaviour
{
    public CharacterCombat source;
    public CharacterEffectKind kind;
    public Vector3 origin;
    public Vector3 destination;
    public Vector2 direction;
    public float radius;
    public float angle;
    public float length;
    public float width;
    public int damage;
    public float lifetime;
    public float elapsed;
    public float tickTimer;
    public float tickInterval;
    public bool missile;
    public bool arcWave;
    public bool ripple;
    public bool retired;
    // R1-05：tick 减速按来源开关——药剂师药剂滩 true（§5 有依据）；止血夹池/导管光束/超声剪滞留 false。
    public bool tickSlow;
    // 解剖剪飞射弹体（v3 §6：每5s发射，路径碰撞伤害；进化超声解剖剪：旋转滞留，十字二剪42°）。
    public bool lingering;
    public bool impactStar;
    public bool hemostatMark;
    public bool netWave;
    public bool pixelBeam;
    public bool bombFuse;
    public float fuseDelay;
    public bool pixelBurst;
    public bool mistPuff;
    public bool pixelPool;
    public bool pixelAmpoule;
    public bool pixelSplash;
    public bool rainDrop;
    public SpriteRenderer trail;
    public float flightTime;
    public float spinAngle;
    public HashSet<Enemy> shotHits = new HashSet<Enemy>();
    public SpriteRenderer spriteRenderer;
    public SpriteRenderer swoosh;
    public List<LineRenderer> lines = new List<LineRenderer>(3);
    public Color color;
    public static Sprite whiteSprite;
    public static Material effectMaterial;

    public static void EnsureEffectMaterial()
    {
        if (effectMaterial != null) return;
        // URP 下内置 Sprites/Default 不走 SRP Batcher：共享材质会串贴图（最后绑定的纹理覆盖所有渲染器）。
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            effectMaterial = new Material(shader);
            effectMaterial.hideFlags = HideFlags.DontSave;
            return;
        }
        // Ultimate fallback: borrow any live SpriteRenderer's material, guaranteed renderable.
        SpriteRenderer fallback = FindAnyObjectByType<SpriteRenderer>();
        if (fallback != null && fallback.sharedMaterial != null)
            effectMaterial = fallback.sharedMaterial;
        if (effectMaterial == null)
            Debug.LogWarning("[CharacterAttackEffect] No shader available; lines will use default material.");
    }

    public static CharacterAttackEffect Create(CharacterCombat owner, Vector3 position,
        CharacterEffectKind effectKind, float duration)
    {
        GameObject obj = new GameObject("Skill_" + effectKind);
        obj.transform.position = position;
        CharacterAttackEffect effect = obj.AddComponent<CharacterAttackEffect>();
        effect.source = owner;
        effect.origin = position;
        effect.kind = effectKind;
        effect.lifetime = duration;
        effect.color = CharacterCatalog.Colors[(int)owner.role];
        if (owner.GetVesselSkillLevel(UpgradeType.SidewallForceps) > 0)
            effect.color = new Color(0.95f, 0.16f, 0.22f, 1f);
        EnsureEffectMaterial();
        if (effectMaterial == null)
        {
            SpriteRenderer playerRenderer = owner.GetComponentInChildren<SpriteRenderer>();
            if (playerRenderer != null) effectMaterial = playerRenderer.sharedMaterial;
            if (effectMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) effectMaterial = new Material(shader);
            }
        }
        return effect;
    }

    public static void Sector(CharacterCombat owner, Vector3 position, Vector2 aim, float radius,
        float degrees, int hitDamage, bool arcWave = false)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Sector, 0.28f);
        effect.direction = aim;
        effect.radius = radius;
        effect.angle = degrees;
        effect.damage = hitDamage;
        effect.arcWave = arcWave;
        effect.DrawSector();
        effect.DamageArea(false);
    }

    public static void Beam(CharacterCombat owner, Vector3 position, Vector2 aim, float length,
        float width, int hitDamage, float duration)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Beam, duration);
        effect.direction = aim;
        effect.length = length;
        effect.width = width;
        effect.damage = hitDamage;
        effect.tickInterval = GameBalance.Get("effect.beam_tick", 0.08f);
        effect.tickTimer = 0f;
        LineRenderer line = effect.NewLine(width * 2.5f);
        line.positionCount = 2;
        line.SetPosition(0, position);
        line.SetPosition(1, position + (Vector3)(aim * length));
        LineRenderer glow = effect.NewLine(width * 4f);
        glow.positionCount = 2;
        glow.SetPosition(0, position);
        glow.SetPosition(1, position + (Vector3)(aim * length));
        Color glowColor = effect.color;
        glowColor.a = 0.3f;
        glow.startColor = glow.endColor = glowColor;
        effect.DamageArea(false);
    }

    // 技能17 导管激光：2 帧平铺像素束，Tiled 保持方像素不随距离拉伸。
    public static void CatheterLaser(CharacterCombat owner, Vector3 position, Vector2 aim, float length,
        float width, int hitDamage, float duration)
    {
        CharacterAttackEffect effect = Create(owner, position + (Vector3)(aim * length * 0.5f),
            CharacterEffectKind.Beam, duration);
        effect.origin = position;
        effect.direction = aim;
        effect.length = length;
        effect.width = width;
        effect.damage = hitDamage;
        effect.tickInterval = GameBalance.Get("effect.beam_tick", 0.08f);
        effect.tickTimer = 0f;
        effect.pixelBeam = true;
        effect.AddSprite();
        effect.spriteRenderer.color = Color.white;
        effect.spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        effect.spriteRenderer.size = new Vector2(length, width);
        effect.spriteRenderer.sortingOrder = 8;
        effect.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        effect.DamageArea(false);
    }

    // 技能25 痉挛环：触发时播 5s 收缩脉冲后隐藏（用户确认口径），数值效果常驻 + 左上角图标指示。
    public static void SpasmRing(CharacterCombat owner, float radius, float duration)
    {
        CharacterAttackEffect effect = Create(owner, owner.transform.position, CharacterEffectKind.SpasmRing, duration);
        effect.transform.SetParent(owner.transform, true);
        effect.transform.localPosition = Vector3.zero;
        effect.radius = radius;
        effect.AddSprite();
        Sprite ring = MonsterSurvivorArtLibrary.LoadVfx("vfx_spasm_ring_f0", 32f);
        effect.spriteRenderer.sprite = ring;
        effect.spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
        effect.spriteRenderer.sortingOrder = 2;
        // 环直径 200px@ppu32=6.25 世界；缩到 2*radius，父级 lossyScale 需除回。
        effect.transform.localScale = Vector3.one * (radius * 2f / (ring.bounds.size.x * owner.transform.lossyScale.x));
    }

    // 技能19 消融弹：抛物线飞行→落地引信闪烁→fuseDelay→单次爆炸（修旧版飞行结束误触发药剂师爆炸的双重爆炸）。
    public static void BombThrow(CharacterCombat owner, Vector3 landing, float fuseDelay, float radius, int hitDamage)
    {
        CharacterAttackEffect effect = Create(owner, owner.transform.position, CharacterEffectKind.Projectile,
            GameBalance.Get("skill.ablation.flight", 0.35f));
        effect.destination = landing;
        effect.bombFuse = true;
        effect.fuseDelay = fuseDelay;
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.AddSprite();
        Sprite bomb = MonsterSurvivorArtLibrary.LoadVfx("vfx_bomb_f0");
        effect.spriteRenderer.sprite = bomb;
        effect.spriteRenderer.color = Color.white;
        effect.spriteRenderer.sortingOrder = 7;
        // 弹体 20px→0.625 世界（px/32 约定）；弹体无父级，lossy=1。
        effect.transform.localScale = Vector3.one * (0.625f / bomb.bounds.size.y);
    }

    // 技能27 弹雨：垂直下落+拖尾，落地引信后复用 PixelBurst（半径按 csv 0.99）。
    public static void RainBomb(CharacterCombat owner, Vector3 landing, float fall, float fuseDelay, float radius, int hitDamage)
    {
        CharacterAttackEffect effect = Create(owner, landing + Vector3.up * 6.5f, CharacterEffectKind.Projectile, fall);
        effect.destination = landing;
        effect.bombFuse = true;
        effect.rainDrop = true;
        effect.fuseDelay = fuseDelay;
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.AddSprite();
        Sprite bomb = MonsterSurvivorArtLibrary.LoadVfx("vfx_bomb_f0");
        effect.spriteRenderer.sprite = bomb;
        effect.spriteRenderer.color = Color.white;
        effect.spriteRenderer.sortingOrder = 7;
        effect.transform.localScale = Vector3.one * (0.625f / bomb.bounds.size.y);
        GameObject trailObj = new GameObject("弹雨拖尾");
        trailObj.transform.SetParent(effect.transform, false);
        effect.trail = trailObj.AddComponent<SpriteRenderer>();
        Sprite tr = MonsterSurvivorArtLibrary.LoadVfx("vfx_rain_trail_f0");
        effect.trail.sprite = tr;
        effect.trail.color = Color.white;
        effect.trail.sortingOrder = 6;
        // 拖尾 36px→1.125 世界；贴在弹体顶上方，父级 lossyScale 需除回。
        float parent = effect.transform.lossyScale.y;
        effect.trail.transform.localScale = Vector3.one * (1.125f / (tr.bounds.size.y * parent));
        effect.trail.transform.localPosition = new Vector3(0f, (0.625f + 1.125f) * 0.5f / parent, 0f);
    }

    public static void PixelBurst(CharacterCombat owner, Vector3 position, float radius, int hitDamage)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Burst, 0.45f);
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.pixelBurst = true;
        effect.AddSprite();
        Sprite f0 = MonsterSurvivorArtLibrary.LoadVfx("vfx_blast_f0", 32f);
        effect.spriteRenderer.sprite = f0;
        effect.spriteRenderer.color = Color.white;
        effect.spriteRenderer.sortingOrder = 6;
        effect.transform.localScale = Vector3.one * (radius * 2f / f0.bounds.size.x);
        effect.DamageArea(false);
    }

    public static void Burst(CharacterCombat owner, Vector3 position, float radius, int hitDamage, bool ripple)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Burst, ripple ? 0.7f : 0.35f);
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.ripple = ripple;
        effect.NewLine(0.15f);
        if (ripple) { effect.NewLine(0.1f); effect.NewLine(0.06f); }
        effect.DamageArea(false);
    }

    public static CharacterAttackEffect Pool(CharacterCombat owner, Vector3 position, float radius,
        float duration, int hitDamage, float interval, bool slowOnTick, bool mist = false)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Pool, duration);
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.tickInterval = interval;
        effect.tickTimer = interval;
        effect.tickSlow = slowOnTick;
        effect.AddSprite();
        if (mist)
        {
            // 144px@ppu32=4.5 世界宽；雾滩直径=2*radius，地面贴花排序低于角色。
            effect.pixelPool = true;
            effect.spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_mist_pool_f0", 32f);
            effect.spriteRenderer.color = Color.white;
            effect.spriteRenderer.sortingOrder = 2;
            effect.transform.localScale = Vector3.one * (radius * 2f / 4.5f);
        }
        else
        {
            effect.spriteRenderer.sprite = GetWhiteSprite();
            effect.spriteRenderer.color = new Color(effect.color.r, effect.color.g, effect.color.b, 0.15f);
            effect.transform.localScale = Vector3.one * radius * 1.5f;
            effect.NewLine(0.09f);
        }
        return effect;
    }

    // 药剂师药剂瓶落地：像素液滴溅射3帧，直径=2*radius。
    public static void PixelSplash(CharacterCombat owner, Vector3 position, float radius, int hitDamage)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Burst, 0.45f);
        effect.radius = radius;
        effect.damage = hitDamage;
        effect.pixelSplash = true;
        effect.AddSprite();
        effect.spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_splash_f0", 32f);
        effect.spriteRenderer.color = Color.white;
        effect.spriteRenderer.sortingOrder = 6;
        effect.transform.localScale = Vector3.one * (radius * 2f / 1.5f);
        effect.DamageArea(false);
    }

    // 技能30 雾化给药机：药剂落点雾化雾团，三帧膨胀消散，纯视觉无伤害。
    public static void MistPuff(CharacterCombat owner, Vector3 position)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Burst, 0.45f);
        effect.mistPuff = true;
        effect.AddSprite();
        effect.spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_mist_puff_f0", 32f);
        effect.spriteRenderer.color = Color.white;
        effect.spriteRenderer.sortingOrder = 6;
        effect.transform.localScale = Vector3.one * 2f;
    }

    public static void Throw(CharacterCombat owner, Vector3 landing, bool missile)
    {
        CharacterAttackEffect effect = Create(owner, owner.transform.position, CharacterEffectKind.Projectile,
            missile ? GameBalance.Get("nurse.missile_flight", 0.35f) : GameBalance.Get("pharmacist.ampoule_flight", 0.35f));
        effect.destination = landing;
        effect.missile = missile;
        effect.AddSprite();
        if (missile)
        {
            effect.transform.localScale = new Vector3(0.2f, 0.55f, 1f);
        }
        else
        {
            // 12px@ppu32=0.375 世界；药剂瓶翻滚2帧替代白色方块。
            effect.pixelAmpoule = true;
            effect.spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_ampoule_f0", 32f);
            effect.spriteRenderer.color = Color.white;
            effect.spriteRenderer.sortingOrder = 7;
            effect.transform.localScale = Vector3.one;
        }
    }

    public static void Flash(CharacterCombat owner, Vector3 position, float radius, bool slow)
    {
        CharacterAttackEffect effect = Create(owner, position, CharacterEffectKind.Flash, 0.7f);
        effect.radius = radius;
        effect.color = slow ? new Color(0.8f, 0.95f, 1f) : CharacterCatalog.Colors[2];
        effect.NewLine(0.16f);
    }

    // 进化·栓塞止血网：推远冲击波，像素网环三帧扩张淡出。
    public static CharacterAttackEffect PushWave(CharacterCombat owner, float radius)
    {
        CharacterAttackEffect effect = Create(owner, owner.transform.position, CharacterEffectKind.Burst, 0.45f);
        effect.radius = radius;
        effect.netWave = true;
        effect.AddSprite();
        effect.spriteRenderer.sortingOrder = 8;
        // 96px@ppu96=1 世界；环最大帧半径 44px 对应推远半径。
        effect.transform.localScale = Vector3.one * (radius * 2f * (48f / 44f));
        return effect;
    }

    // 解剖剪：飞射剪刃，路径碰撞伤害；linger=true 时到位后旋转滞留（超声解剖剪，十字二剪 crossAngle°）。
    public static CharacterAttackEffect ScissorsShot(CharacterCombat owner, Vector3 origin, Vector2 aim,
        float speed, float range, float bladeWidth, int hitDamage, float sizeMult, bool linger, float crossAngle)
    {
        float flight = Mathf.Max(0.05f, range / Mathf.Max(0.1f, speed));
        float lifetime = flight + (linger ? GameBalance.Get("evolution.scissors_ultrasonic.linger", 2f) : 0f);
        CharacterAttackEffect effect = Create(owner, origin, CharacterEffectKind.ScissorsShot, lifetime);
        effect.direction = aim.normalized;
        effect.length = range;
        effect.width = bladeWidth * sizeMult;
        effect.radius = effect.width;
        effect.angle = crossAngle;
        effect.damage = hitDamage;
        effect.lingering = linger;
        effect.flightTime = flight;
        effect.tickInterval = GameBalance.Get("evolution.scissors_ultrasonic.tick", 0.25f);
        effect.tickTimer = effect.tickInterval;
        effect.AddSprite();
        Sprite bladeSprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_scissors");
        if (bladeSprite != null) effect.spriteRenderer.sprite = bladeSprite;
        effect.spriteRenderer.color = Color.white;
        // 48px@ppu96=0.5 世界；视觉直径与旧双刃跨度 width*3.2 一致。
        effect.transform.localScale = Vector3.one * (effect.width * 6.4f);
        return effect;
    }

    // 缝线·红闪电（v3 §6 红线）：像素闪电 4 帧劈落 + 落点星芒 2 帧；enhanced 加一道分叉短闪电。
    public static void SutureLightning(CharacterCombat owner, Vector3 target, bool enhanced)
    {
        if (owner == null) return;
        float height = enhanced ? 4.2f : 3.5f;
        float life = enhanced ? 0.25f : 0.18f;
        SpawnBolt(owner, target + Vector3.up * (height * 0.5f), height, life, 0f);
        if (enhanced) SpawnBolt(owner, target + Vector3.up * 0.75f + Vector3.left * 0.5f, 1.5f, life, 0.05f);
        CharacterAttackEffect star = Create(owner, target, CharacterEffectKind.Lightning, 0.12f);
        star.impactStar = true;
        star.AddSprite();
        star.spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_suture_impact_f0");
        star.spriteRenderer.color = Color.white;
        star.spriteRenderer.sortingOrder = 11;
        star.transform.localScale = Vector3.one * (enhanced ? 2.4f : 1.8f);
    }

    static void SpawnBolt(CharacterCombat owner, Vector3 pos, float height, float life, float phase)
    {
        CharacterAttackEffect bolt = Create(owner, pos, CharacterEffectKind.Lightning, life);
        bolt.AddSprite();
        bolt.spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_suture_f0");
        bolt.spriteRenderer.color = Color.white;
        bolt.spriteRenderer.sortingOrder = 10;
        bolt.elapsed = phase;
        // 32x128@ppu96=0.333x1.333 世界；按劈落高度等比缩放保持方像素。
        bolt.transform.localScale = Vector3.one * (height / 1.3333f);
    }

    public static void HitBlock(Vector3 position, float size)
    {
        GameObject obj = new GameObject("弹簧圈命中特效");
        obj.transform.position = position;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = new Color(0.72f, 1f, 0.55f, 0.85f);
        renderer.sortingOrder = 9;
        obj.transform.localScale = Vector3.one * size * 1.2f;
        Destroy(obj, 0.22f);
    }

    public void Update()
    {
        if (retired || Time.timeScale <= 0f) return;
        if (source == null || source.stats == null || source.stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) { Retire(); return; }
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        if (kind == CharacterEffectKind.Projectile)
        {
            if (bombFuse && elapsed >= lifetime)
            {
                transform.position = destination;
                transform.rotation = Quaternion.identity;
                if (trail != null) trail.gameObject.SetActive(false);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                    Mathf.FloorToInt(elapsed / 0.12f) % 2 == 0 ? "vfx_bomb_f0" : "vfx_bomb_f1");
                if (elapsed >= lifetime + fuseDelay)
                {
                    PixelBurst(source, destination, radius, damage);
                    Retire();
                }
            }
            else
            {
                if (rainDrop)
                {
                    transform.position = Vector3.Lerp(origin, destination, t);
                    if (trail != null)
                        trail.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                            Mathf.FloorToInt(elapsed / 0.08f) % 2 == 0 ? "vfx_rain_trail_f0" : "vfx_rain_trail_f1");
                }
                else if (pixelAmpoule)
                {
                    transform.position = Vector3.Lerp(origin, destination, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                    spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                        Mathf.FloorToInt(elapsed / 0.08f) % 2 == 0 ? "vfx_ampoule_f0" : "vfx_ampoule_f1", 32f);
                }
                else
                {
                    transform.position = Vector3.Lerp(origin, destination, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                    transform.Rotate(0f, 0f, Time.deltaTime * 400f);
                }
                if (elapsed >= lifetime)
                {
                    if (missile) Burst(source, destination, GameBalance.Get("nurse.missile_radius", 1.7f),
                        source.weapon.bulletDamage * GameBalance.GetInt("nurse.missile_damage_mult", 4) +
                        GameBalance.GetInt("nurse.missile_damage_add", 2), false);
                    else source.AmpouleImpact(destination);
                }
            }
        }
        else if (kind == CharacterEffectKind.Pool)
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f) { tickTimer = tickInterval; DamageArea(tickSlow); }
            if (hemostatMark)
            {
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                    Mathf.FloorToInt(elapsed / 0.4f) % 2 == 0 ? "vfx_clamp_zone_f0" : "vfx_clamp_zone_f1");
                Color c = Color.white;
                c.a = Mathf.Clamp01((lifetime - elapsed) / 0.3f);
                spriteRenderer.color = c;
                // 64px@ppu96=0.667 世界；zone 直径=2*radius。
                transform.localScale = Vector3.one * (radius * 3f);
                lines[0].enabled = false;
            }
            else if (pixelPool)
            {
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                    Mathf.FloorToInt(elapsed / 0.4f) % 2 == 0 ? "vfx_mist_pool_f0" : "vfx_mist_pool_f1", 32f);
                Color c = Color.white;
                c.a = Mathf.Clamp01(elapsed / 0.15f) * Mathf.Clamp01((lifetime - elapsed) / 0.3f);
                spriteRenderer.color = c;
            }
            else DrawRing(lines[0], radius, 0.45f);
        }
        else if (kind == CharacterEffectKind.Beam)
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f) { tickTimer = tickInterval; DamageArea(tickSlow); }
            if (pixelBeam)
            {
                // ppu32：单 tile 世界高 0.25 = 激光宽度，Tiled 只横铺不竖叠，方像素不变形。
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                    Mathf.FloorToInt(elapsed / 0.08f) % 2 == 0 ? "vfx_catheter_beam_f0" : "vfx_catheter_beam_f1", 32f);
                Color c = Color.white;
                c.a = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f) * 0.7f;
                spriteRenderer.color = c;
            }
        }
        else if (kind == CharacterEffectKind.SpasmRing)
        {
            int idx = Mathf.FloorToInt(elapsed / 0.233f) % 3;
            spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_spasm_ring_f" + idx, 32f);
            Color c = Color.white;
            c.a = 0.55f * Mathf.Clamp01(elapsed / 0.15f) * (1f - Mathf.Clamp01((elapsed - (lifetime - 0.6f)) / 0.6f));
            spriteRenderer.color = c;
        }
        else if (kind == CharacterEffectKind.Burst || kind == CharacterEffectKind.Flash)
        {
            if (pixelSplash)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed / 0.15f), 0, 2);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_splash_f" + idx, 32f);
                Color c = Color.white;
                c.a = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f) * 0.8f;
                spriteRenderer.color = c;
            }
            else if (mistPuff)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed / 0.15f), 0, 2);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_mist_puff_f" + idx, 32f);
                Color c = Color.white;
                c.a = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.9f;
                spriteRenderer.color = c;
                transform.position = origin + Vector3.up * (t * 0.4f);
            }
            else if (pixelBurst)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed / 0.1125f), 0, 3);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_blast_f" + idx, 32f);
                Color c = Color.white;
                c.a = 1f - Mathf.Clamp01((t - 0.7f) / 0.3f) * 0.8f;
                spriteRenderer.color = c;
                for (int i = 0; i < lines.Count; i++) lines[i].enabled = false;
            }
            else if (netWave)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed / 0.15f), 0, 2);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_net_wave_f" + idx);
                Color c = Color.white;
                c.a = 1f - t * 0.5f;
                spriteRenderer.color = c;
                for (int i = 0; i < lines.Count; i++) lines[i].enabled = false;
            }
            else
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    float progress = Mathf.Clamp01(t * (1f + i * 0.3f) - i * 0.18f);
                    DrawRing(lines[i], Mathf.Lerp(0.15f, radius, progress), 1f - t);
                }
            }
        }
        else if (kind == CharacterEffectKind.Sector)
        {
            if (arcWave)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed / 0.093f), 0, 2);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_arc_f" + idx, 32f);
                Color c = Color.white;
                c.a = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.9f;
                spriteRenderer.color = c;
            }
            else if (swoosh != null)
            {
                // 月牙长轴贴扇区外弧切向、凹面朝向玩家，从 -angle/2 扫到 +angle/2。
                float lead = -angle * 0.5f + angle * t;
                Vector2 ray = Quaternion.Euler(0f, 0f, lead) * direction;
                float arcLen = radius * angle * Mathf.Deg2Rad;
                swoosh.transform.position = origin + (Vector3)(ray * radius * 0.82f);
                swoosh.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(ray.y, ray.x) * Mathf.Rad2Deg + 180f);
                swoosh.transform.localScale = new Vector3(arcLen * 0.34f, arcLen * 0.6f, 1f);
                Color c = color; c.a = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.85f;
                swoosh.color = c;
            }
        }
        else if (kind == CharacterEffectKind.ScissorsShot)
        {
            spinAngle += Time.deltaTime * (lingering ? 540f : 720f);
            Vector3 pos;
            if (elapsed <= flightTime)
            {
                pos = origin + (Vector3)(direction * (length * Mathf.Clamp01(elapsed / flightTime)));
                transform.position = pos;
                source.RefreshTargets();
                float hitRadius = width + 0.35f;
                for (int i = 0; i < source.targets.Count; i++)
                {
                    Enemy enemy = source.targets[i];
                    if (enemy == null || enemy.currentHP <= 0 || enemy.deathPending || shotHits.Contains(enemy)) continue;
                    if ((enemy.transform.position - pos).sqrMagnitude > hitRadius * hitRadius) continue;
                    shotHits.Add(enemy);
                    enemy.TakeDamage(damage);
                    // R1-03：侧壁钳吸血仅限普攻子弹命中（Bullet.cs），剪刀技能路径不触发。
                }
            }
            else
            {
                pos = transform.position;
                origin = pos;
                tickTimer -= Time.deltaTime;
                if (tickTimer <= 0f) { tickTimer = tickInterval; DamageArea(tickSlow); }
            }
            float fade = elapsed > flightTime ? Mathf.Clamp01(1f - (elapsed - flightTime) / Mathf.Max(0.01f, lifetime - flightTime)) : 1f;
            transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
            if (spriteRenderer != null)
            {
                Color c = Color.white; c.a = fade;
                spriteRenderer.color = c;
            }
        }
        else if (kind == CharacterEffectKind.Lightning)
        {
            if (impactStar)
            {
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(elapsed < 0.06f ? "vfx_suture_impact_f0" : "vfx_suture_impact_f1");
            }
            else
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(t * 4f), 0, 3);
                spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_suture_f" + idx);
                Color c = Color.white;
                c.a = 1f - Mathf.Clamp01((t - 0.7f) / 0.3f) * 0.6f;
                spriteRenderer.color = c;
            }
        }
        if (elapsed >= lifetime && !bombFuse) Retire();
    }

    public void DamageArea(bool slow)
    {
        source.RefreshTargets();
        // Damage can remove enemies from the spawner; this snapshot remains stable during the loop.
        for (int i = 0; i < source.targets.Count; i++)
        {
            Enemy enemy = source.targets[i];
            if (enemy == null || enemy.currentHP <= 0 || enemy.deathPending) continue;
            Vector2 offset = enemy.transform.position - origin;
            bool inside;
            if (kind == CharacterEffectKind.Beam)
            {
                float along = Vector2.Dot(offset, direction);
                float across = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                inside = along >= 0f && along <= length && across <= width;
            }
            else
            {
                inside = offset.sqrMagnitude <= radius * radius;
                if (inside && kind == CharacterEffectKind.Sector)
                    inside = offset.sqrMagnitude < 0.01f || Vector2.Angle(direction, offset) <= angle * 0.5f;
            }
            if (!inside) continue;
            if (slow) enemy.ApplySlow(GameBalance.Get("effect.tick_slow_mult", 0.65f),
                tickInterval + GameBalance.Get("effect.tick_slow_extra", 0.2f));
            enemy.TakeDamage(damage);
            // R1-03：侧壁钳吸血仅限普攻子弹命中（Bullet.cs），技能伤害路径不触发。
            source.ApplySpringCoil(enemy);
        }
    }

    public LineRenderer NewLine(float lineWidth)
    {
        GameObject obj = new GameObject("SkillLine");
        obj.transform.SetParent(transform, false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        EnsureEffectMaterial();
        // A null sharedMaterial is legal (renders with the default material); never crash over it.
        if (effectMaterial != null) line.sharedMaterial = effectMaterial;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = lineWidth;
        line.startColor = line.endColor = color;
        line.sortingOrder = 8;
        lines.Add(line);
        return line;
    }

    public void DrawRing(LineRenderer line, float ringRadius, float alpha)
    {
        line.positionCount = 65;
        Color tint = color;
        tint.a = alpha;
        line.startColor = line.endColor = tint;
        for (int i = 0; i <= 64; i++)
        {
            float radians = i * Mathf.PI * 2f / 64f;
            line.SetPosition(i, origin + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * ringRadius);
        }
    }

    public void DrawSector()
    {
        if (arcWave)
        {
            AddSprite();
            spriteRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_arc_f0", 32f);
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 8;
            // 弧外半径 112px@ppu32=3.5 世界；等比缩到 radius，方像素不变形。
            transform.localScale = Vector3.one * (radius / 3.5f);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return;
        }
        // 挥刀光带：抠出的月牙贴图沿判定扇区扫过，所见即所伤；不再叠程序化扇形/外缘弧。
        Sprite slash = MonsterSurvivorArtLibrary.LoadSlash();
        if (slash == null) return;
        GameObject swooshObj = new GameObject("BladeSwoosh");
        swooshObj.transform.SetParent(transform, false);
        swoosh = swooshObj.AddComponent<SpriteRenderer>();
        swoosh.sprite = slash;
        if (effectMaterial != null) swoosh.sharedMaterial = effectMaterial;
        swoosh.sortingOrder = 8;
        swoosh.color = new Color(color.r, color.g, color.b, 0f);
    }

    public void AddSprite()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        if (effectMaterial != null) spriteRenderer.sharedMaterial = effectMaterial;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 9;
    }

    public static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width,
            Texture2D.whiteTexture.height), Vector2.one * 0.5f, Texture2D.whiteTexture.width);
        return whiteSprite;
    }

    public void Retire()
    {
        if (retired) return;
        retired = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
