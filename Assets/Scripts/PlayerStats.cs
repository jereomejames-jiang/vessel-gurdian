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
    float recoveryTimer;
    float lastDamageTime;
    float membraneRegenTimer;

    public event Action<int, int> OnHPChanged;
    public event Action<int, int, int> OnXPChanged;
    public event Action<int> OnLevelUp;
    public event Action OnDeath;

    void Awake()
    {
        maxHP = GameBalance.GetInt("player.max_hp", maxHP);
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
        return Mathf.Max(1, Mathf.CeilToInt(xpPerLevel * Mathf.Pow(xpGrowth, level - 1)));
    }

    public void GainXP(int amount)
    {
        if (amount <= 0 || currentHP <= 0 || (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;

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

    public void TakeDamage(int amount)
    {
        if (invincible || amount <= 0 || currentHP <= 0 || Time.timeScale <= 0f) return;

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

        if (currentHP == 0)
        {
            OnDeath?.Invoke();
            Time.timeScale = 0f;
        }
    }

    public void IncreaseMaxHP(int amount)
    {
        if (amount <= 0) return;

        maxHP += amount;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
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

    public void ConfigureMembraneStent(int level)
    {
        incomingDamageMultiplier = level >= 3 ? GameBalance.Get("skill.stent.mult_lv3", 0.65f) : (level == 2 ? GameBalance.Get("skill.stent.mult_lv2", 0.75f) : GameBalance.Get("skill.stent.mult_lv1", 0.85f));
        if (level >= 3)
        {
            temporaryShield = Mathf.Max(temporaryShield, GameBalance.GetInt("skill.stent.shield", 3));
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
