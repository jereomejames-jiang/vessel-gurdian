using UnityEngine;

public enum UpgradeType
{
    Damage,
    AttackSpeed,
    MoveSpeed,
    MaxHP,
    BulletSpeed
}

[CreateAssetMenu(fileName = "UpgradeOption", menuName = "VesselGuard/Upgrade Option")]
public class UpgradeOption : ScriptableObject
{
    public string displayName;
    public string description;
    public UpgradeType type;
    public float value;
    public SkillCardDefinition skillCard;
}
