using UnityEngine;

[CreateAssetMenu(fileName = "SkillCardDefinition", menuName = "VesselGuard/Skill Card")]
public class SkillCardDefinition : ScriptableObject
{
    public string cardId;
    public string displayName;
    public string description;
    public string educationText;
    public UpgradeType linkedUpgradeType;
    public Color cardColor = Color.white;
    public bool IsUnlocked { get { return PlayerPrefs.GetInt("VesselGuard.Codex.v1." + cardId, 0) == 1; } }

    public void Unlock()
    {
        PlayerPrefs.SetInt("VesselGuard.Codex.v1." + cardId, 1);
        PlayerPrefs.Save();
    }
}
