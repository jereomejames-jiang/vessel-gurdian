using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public float maxHP = 100f;
    // Optional hard ceiling (0 = uncapped). Keeps percentage-based max-HP gains
    // from pushing current HP past a designed limit when a config sets one.
    public float maxHPCap;
    public int xpPerLevel = 5;
    public float xpGrowth = 1.5f;
    public float currentHP;
    public int level = 1;
    public int currentXP;
    public bool invincible;
    [HideInInspector] public float incomingDamageMultiplier = 1f;
    [HideInInspector] public float temporaryShield;
    [HideInInspector] public float recoveryUntil;
    [HideInInspector] public float recoveryInterval = 2.5f;
    [HideInInspector] public bool membraneRegen;
    [HideInInspector] public float heartAmuletBaseMaxHP;
    [HideInInspector] public float heartAmuletBonus;
    bool heartAmuletInitialized;
    float recoveryTimer;
    float lastDamageTime;
    float membraneRegenTimer;
    bool deathEventFired;

    public event Action<float, float> OnHPChanged;
    public event Action<int, int, int> OnXPChanged;
    public event Action<int> OnLevelUp;
    public event Action OnDeath;

    void Awake()
    {
        maxHP = GameBalance.Get("player.max_hp", maxHP);
        maxHPCap = GameBalance.Get("player.max_hp_cap", maxHPCap);
        xpPerLevel = GameBalance.GetInt("xp.base_require", xpPerLevel);
        xpGrowth = GameBalance.Get("xp.growth", xpGrowth);
        recoveryInterval = GameBalance.Get("skill.stent.shield_heal_interval", recoveryInterval);
        currentHP = maxHP;
        currentXP = 0;
        level = Mathf.Max(1, level);
        deathEventFired = false;
    }

    void Start()
    {
        NotifyAll();
    }

    void Update()
    {
        // Keep the 203 overflow shield bounded even for saves/runs created before
        // the cap existed. A paused run still clamps because this runs before the
        // timeScale gate.
        ClampShield();
        if (currentHP <= 0f || Time.timeScale <= 0f) return;
        if (temporaryShield > 0f)
        {
            recoveryTimer -= Time.deltaTime;
            if (recoveryTimer <= 0f)
            {
                recoveryTimer = recoveryInterval;
                if (currentHP < maxHP)
                {
                    currentHP = Mathf.Min(maxHP, currentHP + GameBalance.Get("skill.stent.shield_heal_amount", 20f));
                    OnHPChanged?.Invoke(currentHP, maxHP);
                }
            }
        }
        if (membraneRegen && currentHP < maxHP && Time.time - lastDamageTime >= GameBalance.Get("skill.stent.regen_delay", 5f))
        {
            membraneRegenTimer -= Time.deltaTime;
            if (membraneRegenTimer <= 0f)
            {
                membraneRegenTimer = GameBalance.Get("skill.stent.regen_tick", 1f);
                // True proportional regen: no rounding up, no minimum 1.
                float regenAmount = maxHP * GameBalance.Get("skill.stent.regen_percent", 0.035f);
                currentHP = Mathf.Min(maxHP, currentHP + regenAmount);
                OnHPChanged?.Invoke(currentHP, maxHP);
            }
        }
    }

    public int GetXPToNextLevel()
    {
        return Mathf.Max(1, Mathf.CeilToInt(xpPerLevel * Mathf.Pow(xpGrowth, level - 1)));
    }

    public void GainXP(int amount)
    {
        if (amount <= 0 || currentHP <= 0f || (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;

        currentXP += amount;

        // Consume every threshold the XP actually crosses; keep the true remainder.
        int levelsGained = 0;
        int needed = GetXPToNextLevel();
        while (currentXP >= needed)
        {
            currentXP -= needed;
            level++;
            levelsGained++;
            needed = GetXPToNextLevel();
        }

        OnXPChanged?.Invoke(currentXP, needed, level);

        // One event per level gained, in ascending order, so character rank-ups
        // and queued level-up rewards never get skipped.
        for (int i = levelsGained; i > 0; i--)
            OnLevelUp?.Invoke(level - i + 1);
    }

    public void TakeDamage(float amount)
    {
        if (invincible || amount <= 0f || currentHP <= 0f || Time.timeScale <= 0f) return;
        if (GameManager.Instance != null && GameManager.Instance.gameEnded) return;

        lastDamageTime = Time.time;
        membraneRegenTimer = 0f;
        // Exact proportional mitigation: no rounding up, no minimum 1 damage.
        float reduced = amount * Mathf.Clamp(incomingDamageMultiplier,
            GameBalance.Get("player.damage_taken_min_mult", 0.05f), 1f);
        if (temporaryShield > 0f)
        {
            float absorbed = Mathf.Min(temporaryShield, reduced);
            temporaryShield -= absorbed;
            reduced -= absorbed;
            recoveryTimer = recoveryInterval;
        }
        currentHP = Mathf.Max(0f, currentHP - reduced);
        OnHPChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0f && !deathEventFired)
        {
            deathEventFired = true;
            OnDeath?.Invoke();
            // The GameManager owns every timeScale change; death resolves through
            // HandleDeath -> FinishGame, which freezes the run itself.
            if (GameManager.Instance == null) Time.timeScale = 0f;
        }
    }

    public void IncreaseMaxHP(float amount)
    {
        if (amount <= 0f) return;

        float targetMax = maxHP + amount;
        if (maxHPCap > 0f) targetMax = Mathf.Min(targetMax, maxHPCap);
        float actualIncrease = Mathf.Max(0f, targetMax - maxHP);
        maxHP = targetMax;
        currentHP = Mathf.Min(maxHP, currentHP + actualIncrease);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // 109 Heart Amulet. The multiplier always applies to the max HP captured when
    // the first level is taken, so Lv1 -> Lv2 only adds the difference instead of
    // multiplying the previous result again. The added amount follows
    // IncreaseMaxHP semantics (current HP rises by the same amount, capped by max).
    public void ApplyHeartAmulet(int level)
    {
        if (level <= 0) return;
        if (!heartAmuletInitialized)
        {
            heartAmuletBaseMaxHP = maxHP;
            heartAmuletBonus = 0f;
            heartAmuletInitialized = true;
        }
        float multiplier = HeartAmuletMultiplier(level);
        float targetBonus = heartAmuletBaseMaxHP * (multiplier - 1f);
        float delta = targetBonus - heartAmuletBonus;
        if (delta <= 0f) return;
        heartAmuletBonus = targetBonus;
        IncreaseMaxHP(delta);
    }

    public static float HeartAmuletMultiplier(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.heart.mult_lv1", 1.2f);
            case 2: return GameBalance.Get("skill.heart.mult_lv2", 1.4f);
            case 3: return GameBalance.Get("skill.heart.mult_lv3", 1.6f);
            case 4: return GameBalance.Get("skill.heart.mult_lv4", 1.8f);
            default: return GameBalance.Get("skill.heart.mult_lv5", 2f);
        }
    }

    // 203 Blood Recycle Forceps: healing past full health becomes a temporary shield.
    // Base stent shield is always allowed; 203 overflow may add up to the larger
    // of that shield and a percentage of max HP. Without a cap, repeated full-HP
    // lifesteal stacked thousands of shield and made the HP bar appear frozen.
    public float ShieldCap
    {
        get
        {
            float stentShield = GameBalance.Get("skill.stent.shield", 60f);
            float ratioCap = maxHP * GameBalance.Get("skill.bloodforceps.shield_cap_percent", 0.5f);
            return Mathf.Max(stentShield, ratioCap);
        }
    }

    public void ClampShield()
    {
        if (temporaryShield <= 0f) return;
        float cap = ShieldCap;
        if (temporaryShield > cap) temporaryShield = cap;
    }

    public void HealWithOverflowToShield(float amount)
    {
        if (amount <= 0f || currentHP <= 0f) return;
        float missing = Mathf.Max(0f, maxHP - currentHP);
        float healed = Mathf.Min(missing, amount);
        currentHP = Mathf.Min(maxHP, currentHP + healed);
        float overflow = amount - healed;
        if (overflow > 0f) temporaryShield = Mathf.Min(ShieldCap, temporaryShield + overflow);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void HealToFull()
    {
        currentHP = maxHP;
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || currentHP <= 0f) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void ConfigureMembraneStent(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: incomingDamageMultiplier = GameBalance.Get("skill.stent.mult_lv1", 0.85f); break;
            case 2: incomingDamageMultiplier = GameBalance.Get("skill.stent.mult_lv2", 0.75f); break;
            case 3: incomingDamageMultiplier = GameBalance.Get("skill.stent.mult_lv3", 0.65f); break;
            case 4: incomingDamageMultiplier = GameBalance.Get("skill.stent.mult_lv4", 0.55f); break;
            default: incomingDamageMultiplier = GameBalance.Get("skill.stent.mult_lv5", 0.45f); break;
        }
        if (level >= 3)
        {
            temporaryShield = Mathf.Min(ShieldCap,
                Mathf.Max(temporaryShield, GameBalance.Get("skill.stent.shield", 60f)));
            membraneRegen = true;
            lastDamageTime = Time.time;
        }
    }

    void NotifyAll()
    {
        OnHPChanged?.Invoke(currentHP, maxHP);
        OnXPChanged?.Invoke(currentXP, GetXPToNextLevel(), level);
    }
}
