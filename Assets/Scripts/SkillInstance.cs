using UnityEngine;

// One scene object per skill; definitions and permanent codex progress are separate.
public class SkillInstance : MonoBehaviour
{
    public UpgradeOption definition;
    public int selectedCount;
    public bool acquired;

    public void RecordSelection()
    {
        if (selectedCount < LevelUpManager.SkillMaxLevel) selectedCount++;
        acquired = true;
    }
}
