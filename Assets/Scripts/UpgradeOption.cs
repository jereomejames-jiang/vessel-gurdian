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
    AblationBomb = 19,
    // Appended after the legacy block so existing serialized enum values stay put.
    NeedleBullet = 20,
    HeartAmulet = 21,
    BloodRecycleForceps = 22
}

[CreateAssetMenu(fileName = "UpgradeOption", menuName = "VesselGuard/Upgrade Option")]
public class UpgradeOption : ScriptableObject
{
    public string displayName;
    public string description;
    public UpgradeType type;
    // Workbook ID (101/1001/1010/109/203...). Stable across enum edits.
    public int skillId;
    public float value;
    public SkillCardDefinition skillCard;
}
