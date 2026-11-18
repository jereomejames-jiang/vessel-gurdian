using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int maxHP = 5;
    public int xpPerLevel = 5;
    public float xpGrowth = 1.5f;
    public int currentHP;
    public int level = 1;
    public int currentXP;
    public bool invincible;
    [HideInInspector] public float incomingDamageMultiplier = 1f;
    [HideInInspector] public int temporaryShield;
    [HideInInspector] public float recoveryUntil;
    [HideInInspector] public float recoveryInterval = 2.5f;
    [HideInInspector] public bool membraneRegen;
    [HideInInspector] public int stasisStacks;
    [HideInInspector] public float rootUntil;
    // v3 被动：心形护符（最大生命倍率+50%/级）、对口支援（经验倍率+20%/级）、磁铁拾取物窗口。
    [HideInInspector] public int amuletLevel;
    [HideInInspector] public float xpMultiplier = 1f;
    [HideInInspector] public float magnetBoostUntil;
    int baseMaxHP;
    int flatMaxHPBonus;
    float stasisDecayAt;
    float recoveryTimer;
    float lastDamageTime;
    float membraneRegenTimer;

    public bool MagnetBoostActive { get { return Time.time < magnetBoostUntil; } }

    public event Action<int, int> OnHPChanged;
    public event Action<int> OnDamaged;
    public event Action<int, int, int> OnXPChanged;
    public event Action<int> OnLevelUp;
    public event Action OnDeath;

    void Awake()
    {
        maxHP = GameBalance.GetInt("player.max_hp", maxHP);
        baseMaxHP = maxHP;
        xpPerLevel = GameBalance.GetInt("xp.base_require", xpPerLevel);
        xpGrowth = GameBalance.Get("xp.growth", xpGrowth);
        recoveryInterval = GameBalance.Get("skill.stent.shield_heal_interval", recoveryInterval);
        currentHP = maxHP;
        currentXP = 0;
        level = Mathf.Max(1, level);
    }

    void Start()
    {
        NotifyAll();
    }

    void Update()
    {
        if (currentHP <= 0 || Time.timeScale <= 0f) return;
        // 淤滞层数随时间衰减（设定集(3) Boss2「持续移动清除淤滞」；衰减节奏未给数值→1 层/秒并标旗）。
        if (stasisStacks > 0 && Time.time >= stasisDecayAt)
        {
            stasisStacks--;
            stasisDecayAt = Time.time + GameBalance.Get("player.stasis_decay_interval", 1f);
        }
        if (temporaryShield > 0)
        {
            recoveryTimer -= Time.deltaTime;
            if (recoveryTimer <= 0f)
            {
                recoveryTimer = recoveryInterval;
                if (currentHP < maxHP) { currentHP = Mathf.Min(maxHP, currentHP + GameBalance.GetInt("skill.stent.shield_heal_amount", 1)); OnHPChanged?.Invoke(currentHP, maxHP); }
            }
        }
        if (membraneRegen && currentHP < maxHP && Time.time - lastDamageTime >= GameBalance.Get("skill.stent.regen_delay", 5f))
        {
            membraneRegenTimer -= Time.deltaTime;
            if (membraneRegenTimer <= 0f)
            {
                membraneRegenTimer = GameBalance.Get("skill.stent.regen_tick", 1f);
                int regenAmount = Mathf.Max(1, Mathf.CeilToInt(maxHP * GameBalance.Get("skill.stent.regen_percent", 0.035f)));
                currentHP = Mathf.Min(maxHP, currentHP + regenAmount);
                OnHPChanged?.Invoke(currentHP, maxHP);
            }
        }
    }

    public int GetXPToNextLevel()
    {
        float req = xpPerLevel * Mathf.Pow(xpGrowth, level - 1);
        // 前期升级减速：1-5 级需求放大；倍率上限受 lv6 原值约束，避免曲线回凹。
        if (level <= 5) req *= GameBalance.Get("xp.early_mult", 1.5f);
        return Mathf.Max(1, Mathf.CeilToInt(req));
    }

    public void GainXP(int amount)
    {
        if (amount <= 0 || currentHP <= 0 || (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        // 对口支援：经验获取倍率 +20%/级。
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * xpMultiplier));
        currentXP += amount;
        int needed = GetXPToNextLevel();

        // One level per GainXP call: overflow is banked but capped just below
        // the next threshold so the HUD never jumps two levels at once.
        if (currentXP >= needed)
        {
            currentXP -= needed;
            level++;
            OnLevelUp?.Invoke(level);
            needed = GetXPToNextLevel();
            currentXP = Mathf.Min(currentXP, needed - 1);
        }

        OnXPChanged?.Invoke(currentXP, needed, level);
    }

    public float invulnerableUntil;

    public void TakeDamage(int amount)
    {
        if (invincible || amount <= 0 || currentHP <= 0 || Time.timeScale <= 0f) return;
        // Short i-frames: without them a crowd drains all 5 HP in under a second,
        // which reads as unfair rather than difficult.
        if (Time.time < invulnerableUntil) return;
        invulnerableUntil = Time.time + GameBalance.Get("player.iframes", 0.7f);

        lastDamageTime = Time.time;
        membraneRegenTimer = 0f;
        int reduced = Mathf.Max(1, Mathf.CeilToInt(amount * Mathf.Clamp(incomingDamageMultiplier, GameBalance.Get("player.damage_taken_min_mult", 0.05f), 1f)));
        if (temporaryShield > 0)
        {
            int absorbed = Mathf.Min(temporaryShield, reduced);
            temporaryShield -= absorbed;
            reduced -= absorbed;
            recoveryTimer = recoveryInterval;
        }
        currentHP = Mathf.Max(0, currentHP - reduced);
        OnHPChanged?.Invoke(currentHP, maxHP);
        OnDamaged?.Invoke(reduced);

        if (currentHP == 0)
        {
            OnDeath?.Invoke();
            Time.timeScale = 0f;
        }
    }

    public void IncreaseMaxHP(int amount)
    {
        if (amount <= 0) return;
        flatMaxHPBonus += amount;
        RecalculateMaxHP();
    }

    // 心形护符：最大生命倍率 +50%/级（加法叠加，叠加口径未指定→标旗）。
    public void ConfigureHeartAmulet(int level)
    {
        amuletLevel = Mathf.Max(0, level);
        RecalculateMaxHP();
    }

    void RecalculateMaxHP()
    {
        int newMax = Mathf.Max(1, Mathf.RoundToInt((baseMaxHP + flatMaxHPBonus) *
            (1f + GameBalance.Get("passive.amulet.hp", 0.5f) * amuletLevel)));
        int gained = Mathf.Max(0, newMax - maxHP);
        maxHP = newMax;
        currentHP = Mathf.Min(maxHP, currentHP + gained);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void HealToFull()
    {
        currentHP = maxHP;
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // 覆膜支架（v3 §7）：降低自身受到的伤害 30%，各级同值；旧版分级减伤/临时护盾/脱战回血不再产出。
    public void ConfigureMembraneStent(int level)
    {
        incomingDamageMultiplier = level > 0 ? GameBalance.Get("skill.stent.mult", 0.7f) : 1f;
    }

    public void AddStasisStack(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;
        stasisStacks = Mathf.Max(0, stasisStacks + amount);
        stasisDecayAt = Time.time + GameBalance.Get("player.stasis_decay_grace", 1.5f);
    }
    
    public void RootPlayer(float duration)
    {
        if (duration <= 0f || currentHP <= 0) return;
        rootUntil = Time.time + duration;
    }
    
    public bool IsRooted()
    {
        return Time.time < rootUntil;
    }

    void NotifyAll()
    {
        OnHPChanged?.Invoke(currentHP, maxHP);
        OnXPChanged?.Invoke(currentXP, GetXPToNextLevel(), level);
    }
}
