using UnityEngine;

public enum UpgradeType
{
    Damage,
    AttackSpeed,
    MoveSpeed,
    MaxHP,
    BulletSpeed,
    MembraneStent = 10,
    FlushNeedle = 11,
    DissectionScissors = 12,
    SidewallForceps = 13,
    Suture = 14,
    MicroscopicHemostat = 15,
    SpringCoil = 16,
    ThrombusCatheter = 17,
    SuctionTube = 18,
    AblationBomb = 19
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
