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

    public SkillInstance GetSkill(UpgradeOption option)
    {
        return skills.Find(skill => skill != null && skill.definition == option);
    }

    public void RecordSelection(UpgradeOption option)
    {
        if (option == null) return;
        Initialize(new[] { option });
        GetSkill(option).RecordSelection();
    }

    public int GetLevel(UpgradeType type)
    {
        SkillInstance instance = skills.Find(skill => skill != null && skill.definition != null && skill.definition.type == type);
        return instance == null ? 0 : instance.selectedCount;
    }
}
