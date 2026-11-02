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
    public const int SkillMaxLevel = VesselSkillCatalog.StandardMaxLevel;

    // Evolution 203 is only injected once, after both recipe levels are present.
    public UpgradeOption evolutionOption;
    public bool evolutionOffered;
    public bool evolutionApplied;

    void OnEnable() { Subscribe(); }
    void Start() { Subscribe(); }

    public void Subscribe()
    {
        choicesPerLevel = GameBalance.GetInt("game.choices_per_level", choicesPerLevel);
        // Always use the catalog from the design sheet. This also migrates scenes
        // that still serialize the five legacy prototype upgrades or an older
        // ten-skill array without the new active/passive samples.
        if (!MatchesCatalog(allUpgrades))
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

    bool MatchesCatalog(UpgradeOption[] options)
    {
        if (options == null || options.Length != VesselSkillCatalog.Types.Length) return false;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null || options[i].type != VesselSkillCatalog.Types[i]) return false;
        }
        return true;
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
            GetSkillLevel(option.type) >= VesselSkillCatalog.MaxLevelFor(option.type);
    }

    public bool CanOfferBloodRecycle()
    {
        if (evolutionApplied || evolutionOffered) return false;
        return GetSkillLevel(UpgradeType.SidewallForceps) >=
                VesselSkillCatalog.MaxLevelFor(UpgradeType.SidewallForceps) &&
            GetSkillLevel(UpgradeType.HeartAmulet) >= 1;
    }

    // Once the player has invested in either half of the 203 recipe, keep one of
    // the two required skills in the visible choices until the recipe is complete.
    // Without this, a 12-skill pool and three random choices make reaching
    // Sidewall Lv5 + Heart Lv1 extremely unlikely in a normal run.
    public UpgradeOption GetRecipeFocusOption()
    {
        if (evolutionApplied || evolutionOffered) return null;
        if (GameBalance.GetInt("game.evolution_recipe_guarantee", 1) == 0) return null;
        int sidewall = GetSkillLevel(UpgradeType.SidewallForceps);
        int heart = GetSkillLevel(UpgradeType.HeartAmulet);
        if (sidewall <= 0 && heart <= 0) return null;
        if (sidewall < VesselSkillCatalog.MaxLevelFor(UpgradeType.SidewallForceps))
            return FindUpgradeOption(UpgradeType.SidewallForceps);
        if (heart < 1) return FindUpgradeOption(UpgradeType.HeartAmulet);
        return null;
    }

    UpgradeOption FindUpgradeOption(UpgradeType type)
    {
        if (allUpgrades == null) return null;
        for (int i = 0; i < allUpgrades.Length; i++)
            if (allUpgrades[i] != null && allUpgrades[i].type == type) return allUpgrades[i];
        return null;
    }

    void EnsureEvolutionOption()
    {
        if (evolutionOption == null)
            evolutionOption = VesselSkillCatalog.CreateOption(UpgradeType.BloodRecycleForceps);
    }

    public void ShowNextChoices()
    {
        // Deduplicate by skill type and exclude skills already at their cap.
        List<UpgradeOption> pool = new List<UpgradeOption>();
        HashSet<UpgradeType> seen = new HashSet<UpgradeType>();
        if (allUpgrades != null)
            foreach (UpgradeOption option in allUpgrades)
            {
                if (option == null || !seen.Add(option.type)) continue;
                if (IsSkillMaxed(option)) continue;
                pool.Add(option);
            }

        bool offerEvolution = CanOfferBloodRecycle();
        if (offerEvolution)
        {
            EnsureEvolutionOption();
            if (evolutionOption != null && seen.Add(evolutionOption.type)) pool.Add(evolutionOption);
        }

        UpgradeOption recipeFocus = GetRecipeFocusOption();
        if (recipeFocus != null && !IsSkillMaxed(recipeFocus) && !pool.Contains(recipeFocus))
            pool.Add(recipeFocus);

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

        int slotCount = Mathf.Clamp(choicesPerLevel, 1, pool.Count);
        // The recipe promises the evolution in the very next choice. Keep it inside
        // the visible slice even after the random shuffle; otherwise a 3-of-12 draw
        // could mark it offered without ever showing it.
        if (offerEvolution && evolutionOption != null && slotCount > 0)
        {
            int evolutionIndex = pool.IndexOf(evolutionOption);
            if (evolutionIndex >= slotCount)
            {
                int swapIndex = UnityEngine.Random.Range(0, slotCount);
                UpgradeOption temp = pool[evolutionIndex];
                pool[evolutionIndex] = pool[swapIndex];
                pool[swapIndex] = temp;
            }
        }
        // Same protection for the recipe focus card: it must be pickable now, not
        // merely present in the hidden part of the shuffled pool.
        if (recipeFocus != null && slotCount > 0)
        {
            int focusIndex = pool.IndexOf(recipeFocus);
            if (focusIndex >= slotCount)
            {
                int swapIndex = UnityEngine.Random.Range(0, slotCount);
                UpgradeOption temp = pool[focusIndex];
                pool[focusIndex] = pool[swapIndex];
                pool[swapIndex] = temp;
            }
        }

        currentChoices = pool.GetRange(0, slotCount).ToArray();
        evolutionOffered = offerEvolution;
        isChoosing = true;
        if (GameManager.Instance != null) GameManager.Instance.SetPause(PauseReason.CardChoice, true);
        OnShowChoices?.Invoke(currentChoices);
    }

    // All eligible skills are maxed: spend the reward on a 10% max-HP heal instead of
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
        int maxLevel = VesselSkillCatalog.MaxLevelFor(option.type);

        // Second eligibility gate: stale buttons or double clicks can never push
        // a vessel skill past its cap. The reward is consumed and the queue advances.
        if (VesselSkillCatalog.IsVesselSkill(option.type) && previousSkillLevel >= maxLevel)
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
        if (option.type == UpgradeType.BloodRecycleForceps) evolutionApplied = true;
        int nextSkillLevel = Mathf.Clamp(GetSkillLevel(option.type), 1, maxLevel);
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
