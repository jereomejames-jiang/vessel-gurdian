using System.Collections.Generic;
using UnityEngine;

public class PlayerSkillCollection : MonoBehaviour
{
    public List<SkillInstance> skills = new List<SkillInstance>();
    public Transform skillRoot;

    public void Initialize(UpgradeOption[] options)
    {
        if (skillRoot == null)
        {
            skillRoot = new GameObject("Skills").transform;
            skillRoot.SetParent(transform, false);
        }
        if (options == null) return;
        foreach (UpgradeOption option in options)
        {
            if (option == null || GetSkill(option) != null) continue;
            GameObject obj = new GameObject(option.type + "Skill");
            obj.transform.SetParent(skillRoot, false);
            SkillInstance instance = obj.AddComponent<SkillInstance>();
            instance.definition = option;
            skills.Add(instance);
        }
    }

    // Match by skill TYPE, not by the option asset reference: LevelUpManager may
    // rebuild its option array (VesselSkillCatalog.CreateOptions), and a reference
    // match then created a second entry for the same skill while GetLevel kept
    // reading the empty first one, so the picked level never took effect.
    public SkillInstance GetSkill(UpgradeOption option)
    {
        if (option == null) return null;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillInstance skill = skills[i];
            if (skill == null || skill.definition == null) continue;
            if (skill.definition == option || skill.definition.type == option.type) return skill;
        }
        return null;
    }

    public void RecordSelection(UpgradeOption option)
    {
        if (option == null) return;
        Initialize(new[] { option });
        SkillInstance instance = GetSkill(option);
        if (instance != null) instance.RecordSelection();
    }

    public int GetLevel(UpgradeType type)
    {
        SkillInstance instance = null;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillInstance skill = skills[i];
            if (skill == null || skill.definition == null || skill.definition.type != type) continue;
            // Take the highest recorded count so a duplicated entry can never mask
            // the real level; effective level still clamps to 0..3.
            if (instance == null || skill.selectedCount > instance.selectedCount) instance = skill;
        }
        return instance == null ? 0 : Mathf.Clamp(instance.selectedCount, 0, LevelUpManager.SkillMaxLevel);
    }
}
