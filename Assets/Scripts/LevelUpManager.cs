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
    // Boss 出场演出期间累计升级但不弹卡，演出结束后再统一展示。
    public bool deferChoices;

    void OnEnable() { Subscribe(); }
    void Start() { Subscribe(); }

    public void Subscribe()
    {
        choicesPerLevel = GameBalance.GetInt("game.choices_per_level", choicesPerLevel);
        // 设定集(3) 闭集：6 主动 + 12 被动 + 9 进化。场景序列化的旧池（含弹簧圈等残留项）一律重建。
        if (allUpgrades == null || allUpgrades.Length == 0 || !MatchesClosedSet(allUpgrades))
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

    // 池必须与设定集(3)闭集一致：27 项齐全、无旧版残留、无原型数值项。
    public bool MatchesClosedSet(UpgradeOption[] options)
    {
        if (options == null || options.Length != VesselSkillCatalog.Types.Length) return false;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null) return false;
            UpgradeType t = options[i].type;
            if (VesselSkillCatalog.IsRetired(t) || !VesselSkillCatalog.IsVesselSkill(t)) return false;
        }
        for (int i = 0; i < VesselSkillCatalog.Types.Length; i++)
        {
            bool found = false;
            for (int j = 0; j < options.Length; j++)
                if (options[j].type == VesselSkillCatalog.Types[i]) { found = true; break; }
            if (!found) return false;
        }
        return true;
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
        if (!isChoosing && !deferChoices) ShowNextChoices();
    }

    public void ResumeDeferredChoices()
    {
        if (!isChoosing && pendingLevels > 0) ShowNextChoices();
    }

    // Boss 节点结算：免费随机升级。设定集(3)「免费升级次数」⸢页裁⸥→保留现值（无免费升级，次数由 game.boss_free_upgrades 控制，默认 0，标旗）。
    public void GrantFreeChoices(int count)
    {
        if (count <= 0) return;
        pendingLevels += count;
        if (!isChoosing && !deferChoices) ShowNextChoices();
    }

    public void ShowNextChoices()
    {
        CharacterCombat combat = FindAnyObjectByType<CharacterCombat>();
        List<UpgradeOption> pool = new List<UpgradeOption>();
        if (allUpgrades != null)
            foreach (UpgradeOption option in allUpgrades)
            {
                if (option == null || pool.Contains(option)) continue;
                UpgradeType t = option.type;
                if (VesselSkillCatalog.IsRetired(t) || !VesselSkillCatalog.IsVesselSkill(t)) continue;
                int level = playerSkillCollection == null ? 0 : playerSkillCollection.GetLevel(t);
                if (VesselSkillCatalog.IsEvolution(t))
                {
                    // 进化一次性；条件未满足不进入候选（§8 进化条件列）。
                    if (level >= 1) continue;
                    if (!VesselSkillCatalog.EvolutionReady(t, playerSkillCollection, combat)) continue;
                }
                else
                {
                    if (level >= VesselSkillCatalog.MaxLevel(t)) continue;
                    // §8 表头：进化后去除原主动技能效果 → 已进化的基础主动不再出现。
                    if (VesselSkillCatalog.IsActiveSkill(t) && EvolutionOwnedFor(t)) continue;
                }
                pool.Add(option);
            }

        if (pool.Count == 0)
        {
            pendingLevels = 0;
            isChoosing = false;
            currentChoices = Array.Empty<UpgradeOption>();
            OnChoicesClosed?.Invoke();
            if (GameManager.Instance == null || !GameManager.Instance.gameEnded) Time.timeScale = 1f;
            Debug.LogWarning("VesselGuard: 当前没有可用的升级选项（可能已全部满级）。");
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

    bool EvolutionOwnedFor(UpgradeType active)
    {
        if (playerSkillCollection == null) return false;
        for (int i = 0; i < VesselSkillCatalog.Types.Length; i++)
        {
            UpgradeType evo = VesselSkillCatalog.Types[i];
            if (!VesselSkillCatalog.IsEvolution(evo)) continue;
            if (VesselSkillCatalog.BaseActiveFor(evo) == active && playerSkillCollection.GetLevel(evo) > 0) return true;
        }
        return false;
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
        if (!isChoosing || option == null || currentChoices == null ||
            Array.IndexOf(currentChoices, option) < 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;

        Subscribe();
        int previousSkillLevel = playerSkillCollection == null ? 0 : playerSkillCollection.GetLevel(option.type);
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
        if (playerSkillCollection == null) playerSkillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        if (playerSkillCollection != null) playerSkillCollection.RecordSelection(option);
        int nextSkillLevel = playerSkillCollection == null ? Mathf.Clamp(previousSkillLevel + 1, 1, VesselSkillCatalog.MaxLevel(option.type)) :
            Mathf.Clamp(playerSkillCollection.GetLevel(option.type), 1, VesselSkillCatalog.MaxLevel(option.type));
        CharacterCombat combat = FindAnyObjectByType<CharacterCombat>();
        if (combat != null && VesselSkillCatalog.IsVesselSkill(option.type))
            combat.ApplyVesselSkill(option.type, playerSkillCollection == null ? 1 : playerSkillCollection.GetLevel(option.type));
        pendingLevels = Mathf.Max(0, pendingLevels - 1);
        if (pendingLevels > 0) ShowNextChoices();
        else { OnChoicesClosed?.Invoke(); }
        OnUpgradeApplied?.Invoke(option);

        // 取消选卡牌后摇：直接恢复游戏，不显示卡牌翻转动画
        if (GameManager.Instance == null || !GameManager.Instance.gameEnded)
        {
            UIManager testUI = FindAnyObjectByType<UIManager>();
            Time.timeScale = testUI != null && testUI.speedSixX ? testUI.testSpeedMultiplier : 1f;
        }
    }
}


