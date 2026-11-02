using UnityEngine;

[CreateAssetMenu(fileName = "SkillCardDefinition", menuName = "VesselGuard/Skill Card")]
public class SkillCardDefinition : ScriptableObject
{
    public string cardId;
    // Stable workbook ID. cardId is derived from it, while legacyCardId keeps old
    // saves (VesselSkill_10..19) unlocking after the key migration.
    public int skillId;
    public string legacyCardId;
    public string displayName;
    public string description;
    public string educationText;
    public string iconGlyph;
    public UpgradeType linkedUpgradeType;
    public Color cardColor = Color.white;

    public bool IsUnlocked
    {
        get
        {
            if (PlayerPrefs.GetInt("VesselGuard.Codex.v1." + cardId, 0) == 1) return true;
            return !string.IsNullOrEmpty(legacyCardId) &&
                PlayerPrefs.GetInt("VesselGuard.Codex.v1." + legacyCardId, 0) == 1;
        }
    }

    public void Unlock()
    {
        PlayerPrefs.SetInt("VesselGuard.Codex.v1." + cardId, 1);
        if (!string.IsNullOrEmpty(legacyCardId))
            PlayerPrefs.SetInt("VesselGuard.Codex.v1." + legacyCardId, 1);
        PlayerPrefs.Save();
    }
}
