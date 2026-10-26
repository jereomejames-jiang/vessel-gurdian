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
    public PlayerSkillCollection playerSkillCollection;
    public bool subscribed;
    public bool isChoosing;
    public int pendingLevels;
    public const int SkillMaxLevel = 3;

    void OnEnable() { Subscribe(); }
    void Start() { Subscribe(); }

    public void Subscribe()
    {
        choicesPerLevel = GameBalance.GetInt("game.choices_per_level", choicesPerLevel);
        // Always use the ten skills from the design sheet. This also migrates
        // scenes that still serialize the five legacy prototype upgrades.
        if (allUpgrades == null || allUpgrades.Length == 0 || !ContainsVesselSkill(allUpgrades))
            allUpgrades = VesselSkillCatalog.CreateOptions();
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (autoAttack == null) autoAttack = FindAnyObjectByType<AutoAttack>();
        if (playerSkillCollection == null) playerSkillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        if (playerSkillCollection != null) playerSkillCollection.Initialize(allUpgrades);
        if (!subscribed && playerStats != null)
        {
            playerStats.OnLevelUp += HandleLevelUp;
            subscribed = true;
        }
    }

    public bool ContainsVesselSkill(UpgradeOption[] options)
    {
        if (options == null) return false;
        for (int i = 0; i < options.Length; i++)
            if (options[i] != null && VesselSkillCatalog.IsVesselSkill(options[i].type)) return true;
        return false;
    }

    void OnDisable()
    {
        if (subscribed && playerStats != null) playerStats.OnLevelUp -= HandleLevelUp;
        subscribed = false;
        // Tearing down mid-choice (scene switch, restart) must not strand the
        // CardChoice pause into the next run.
        if (isChoosing)
        {
            isChoosing = false;
            pendingLevels = 0;
            currentChoices = Array.Empty<UpgradeOption>();
            if (GameManager.Instance != null) GameManager.Instance.SetPause(PauseReason.CardChoice, false);
        }
    }

    public void HandleLevelUp(int newLevel)
    {
        if (GameManager.Instance != null && GameManager.Instance.gameEnded) return;
        pendingLevels++;
        if (!isChoosing) ShowNextChoices();
    }

    public int GetSkillLevel(UpgradeType type)
    {
        return playerSkillCollection == null ? 0 : playerSkillCollection.GetLevel(type);
    }

    public bool IsSkillMaxed(UpgradeOption option)
    {
        return option != null && VesselSkillCatalog.IsVesselSkill(option.type) &&
            GetSkillLevel(option.type) >= SkillMaxLevel;
    }

    public void ShowNextChoices()
    {
        // Deduplicate by skill type and exclude skills already at Lv3.
        List<UpgradeOption> pool = new List<UpgradeOption>();
        HashSet<UpgradeType> seen = new HashSet<UpgradeType>();
        if (allUpgrades != null)
            foreach (UpgradeOption option in allUpgrades)
            {
                if (option == null || !seen.Add(option.type)) continue;
                if (IsSkillMaxed(option)) continue;
                pool.Add(option);
            }

        if (pool.Count == 0)
        {
            ConsumeRewardWithoutChoice();
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
        if (GameManager.Instance != null) GameManager.Instance.SetPause(PauseReason.CardChoice, true);
        OnShowChoices?.Invoke(currentChoices);
    }

    // All ten skills are maxed: spend the reward on a 10% max-HP heal instead of
    // showing an empty panel. Full HP still consumes the reward without blocking.
    public void ConsumeRewardWithoutChoice()
    {
        pendingLevels = Mathf.Max(0, pendingLevels - 1);
        isChoosing = false;
        currentChoices = Array.Empty<UpgradeOption>();
        if (playerStats != null)
            playerStats.Heal(playerStats.maxHP * GameBalance.Get("game.maxed_skill_heal_percent", 0.1f));
        if (pendingLevels > 0)
        {
            ShowNextChoices();
            return;
        }
        OnChoicesClosed?.Invoke();
        if (GameManager.Instance != null) GameManager.Instance.SetPause(PauseReason.CardChoice, false);
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
        if (!isChoosing || option == null || currentChoices == null ||
            Array.IndexOf(currentChoices, option) < 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;

        Subscribe();
        int previousSkillLevel = GetSkillLevel(option.type);

        // Second eligibility gate: stale buttons or double clicks can never push
        // a vessel skill past Lv3. The reward is consumed and the queue advances.
        if (previousSkillLevel >= SkillMaxLevel && VesselSkillCatalog.IsVesselSkill(option.type))
        {
            isChoosing = false;
            currentChoices = Array.Empty<UpgradeOption>();
            pendingLevels = Mathf.Max(0, pendingLevels - 1);
            if (pendingLevels > 0) ShowNextChoices();
            else
            {
                OnChoicesClosed?.Invoke();
                // Pause release goes through the central entry; the stored base
                // speed (normal or x6) is what resumes.
                if (GameManager.Instance != null) GameManager.Instance.SetPause(PauseReason.CardChoice, false);
            }
            return;
        }

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
                if (playerStats != null) playerStats.IncreaseMaxHP(option.value);
                break;
            case UpgradeType.BulletSpeed:
                if (autoAttack != null) autoAttack.IncreaseBulletSpeed(option.value);
                break;
        }
        if (playerSkillCollection == null) playerSkillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        if (playerSkillCollection != null) playerSkillCollection.RecordSelection(option);
        int nextSkillLevel = Mathf.Clamp(GetSkillLevel(option.type), 1, SkillMaxLevel);
        CharacterCombat combat = FindAnyObjectByType<CharacterCombat>();
        if (combat != null && VesselSkillCatalog.IsVesselSkill(option.type))
            combat.ApplyVesselSkill(option.type, GetSkillLevel(option.type));
        pendingLevels = Mathf.Max(0, pendingLevels - 1);
        if (pendingLevels > 0) ShowNextChoices();
        else { OnChoicesClosed?.Invoke(); }
        OnUpgradeApplied?.Invoke(option);

        // Finish queued choices before showing a reveal or releasing the pause.
        if (isChoosing) return;

        // The choice is over either way, so the CardChoice reason is released HERE,
        // before anything else can hold a pause. Previously the reveal branch never
        // released it: the card auto-closed, CardReveal was cleared, and the leftover
        // CardChoice froze the run with no popup left to close.
        if (GameManager.Instance != null) GameManager.Instance.SetPause(PauseReason.CardChoice, false);

        SkillCardDefinition card = null;
        if (option.skillCard != null) card = option.skillCard;
        else if (SkillCodexManager.Instance != null) card = SkillCodexManager.Instance.GetCardFor(option);
        // The reveal holds only its own reason, so releasing it later resumes play.
        if (card != null && CardRevealUI.Instance != null && CardRevealUI.Instance.revealPanel != null)
            CardRevealUI.Instance.Show(card, previousSkillLevel, nextSkillLevel);
    }
}
