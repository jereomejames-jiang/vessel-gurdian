using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelUpManager : MonoBehaviour
{
    public UpgradeOption[] allUpgrades;
    public int choicesPerLevel = 3;
    public event Action<UpgradeOption[]> OnShowChoices;
    public event Action OnChoicesClosed;
    public event Action<UpgradeOption> OnUpgradeApplied;
    public UpgradeOption[] currentChoices;
    public PlayerStats playerStats;
    public PlayerController playerController;
    public AutoAttack autoAttack;
    public bool subscribed;
    public bool isChoosing;
    public int pendingLevels;

    void OnEnable() { Subscribe(); }
    void Start() { Subscribe(); }

    public void Subscribe()
    {
        if (allUpgrades == null || allUpgrades.Length == 0)
            allUpgrades = Resources.LoadAll<UpgradeOption>("");
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (autoAttack == null) autoAttack = FindAnyObjectByType<AutoAttack>();
        if (!subscribed && playerStats != null)
        {
            playerStats.OnLevelUp += HandleLevelUp;
            subscribed = true;
        }
    }

    void OnDisable()
    {
        if (subscribed && playerStats != null) playerStats.OnLevelUp -= HandleLevelUp;
        subscribed = false;
    }

    public void HandleLevelUp(int newLevel)
    {
        if (GameManager.Instance != null && GameManager.Instance.gameEnded) return;
        pendingLevels++;
        if (!isChoosing) ShowNextChoices();
    }

    public void ShowNextChoices()
    {
        List<UpgradeOption> pool = new List<UpgradeOption>();
        if (allUpgrades != null)
            foreach (UpgradeOption option in allUpgrades)
                if (option != null && !pool.Contains(option)) pool.Add(option);

        if (pool.Count == 0)
        {
            pendingLevels = 0;
            isChoosing = false;
            currentChoices = Array.Empty<UpgradeOption>();
            OnChoicesClosed?.Invoke();
            if (GameManager.Instance == null || !GameManager.Instance.gameEnded) Time.timeScale = 1f;
            Debug.LogWarning("VesselGuard: Resources 中没有可用的升级选项。");
            return;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            int index = UnityEngine.Random.Range(i, pool.Count);
            UpgradeOption temp = pool[i];
            pool[i] = pool[index];
            pool[index] = temp;
        }
        currentChoices = pool.GetRange(0, Mathf.Clamp(choicesPerLevel, 1, pool.Count)).ToArray();
        isChoosing = true;
        Time.timeScale = 0f;
        OnShowChoices?.Invoke(currentChoices);
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
        if (!isChoosing || option == null || currentChoices == null ||
            Array.IndexOf(currentChoices, option) < 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;

        Subscribe();
        // Clear before callbacks so the same choice cannot be applied twice.
        isChoosing = false;
        currentChoices = Array.Empty<UpgradeOption>();
        switch (option.type)
        {
            case UpgradeType.Damage:
                if (autoAttack != null) autoAttack.IncreaseDamage(Mathf.RoundToInt(option.value));
                break;
            case UpgradeType.AttackSpeed:
                if (autoAttack != null) autoAttack.ReduceInterval(option.value);
                break;
            case UpgradeType.MoveSpeed:
                if (playerController != null) playerController.moveSpeed *= 1f + option.value;
                break;
            case UpgradeType.MaxHP:
                if (playerStats != null) playerStats.IncreaseMaxHP(Mathf.RoundToInt(option.value));
                break;
            case UpgradeType.BulletSpeed:
                if (autoAttack != null) autoAttack.IncreaseBulletSpeed(option.value);
                break;
        }
        pendingLevels = Mathf.Max(0, pendingLevels - 1);
        if (pendingLevels > 0) ShowNextChoices();
        else { OnChoicesClosed?.Invoke(); }
        OnUpgradeApplied?.Invoke(option);

        // Finish queued choices before showing a reveal or releasing the pause.
        if (isChoosing) return;

        SkillCardDefinition card = null;
        if (option.skillCard != null) card = option.skillCard;
        else if (SkillCodexManager.Instance != null) card = SkillCodexManager.Instance.GetCardFor(option);
        if (card != null && CardRevealUI.Instance != null && CardRevealUI.Instance.revealPanel != null)
            CardRevealUI.Instance.Show(card);
        else if (GameManager.Instance == null || !GameManager.Instance.gameEnded) Time.timeScale = 1f;
    }
}


