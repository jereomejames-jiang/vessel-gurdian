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

    public event Action<int, int> OnHPChanged;
    public event Action<int, int, int> OnXPChanged;
    public event Action<int> OnLevelUp;
    public event Action OnDeath;

    void Awake()
    {
        currentHP = maxHP;
        currentXP = 0;
        level = Mathf.Max(1, level);
    }

    void Start()
    {
        NotifyAll();
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

        while (currentXP >= needed)
        {
            currentXP -= needed;
            level++;
            OnLevelUp?.Invoke(level);
            needed = GetXPToNextLevel();
        }

        OnXPChanged?.Invoke(currentXP, needed, level);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0 || Time.timeScale <= 0f) return;

        currentHP = Mathf.Max(0, currentHP - amount);
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

    void NotifyAll()
    {
        OnHPChanged?.Invoke(currentHP, maxHP);
        OnXPChanged?.Invoke(currentXP, GetXPToNextLevel(), level);
    }
}
