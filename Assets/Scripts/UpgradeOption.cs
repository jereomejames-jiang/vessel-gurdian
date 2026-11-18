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
    // 进化技能
    ContinuousFlush = 20,
    UltrasonicScissors = 21,
    BloodRecoveryForceps = 22,
    StapleGun = 23,
    EmbolismHemostatNet = 24,
    VascularSpasmRing = 25,
    ExtracorporealPump = 26,
    DirectedAblationRain = 27,
    HighFrequencyElectroknife = 28,
    MicroInfusionPump = 29,
    AtomizationDrugMachine = 30,
    // v3 被动技能表（设定集3 §7，12 项被动中的 9 项新增；
    // 覆膜支架/侧壁钳/吸液管沿用 10/13/18，弹簧圈等旧项保留枚举但移出掉落池）
    AdvancedTraining = 31,   // 进修培训
    MajorSurgery = 32,       // 参与重大手术
    HappyWater = 33,         // 快乐水
    DeepResearch = 34,       // 深耕科研
    FreeClinic = 35,         // 义诊科普
    HeartAmulet = 36,        // 心形护符
    PartnerSupport = 37,     // 对口支援
    EquipmentUpdate = 38,    // 设备更新
    RapidDisposal = 39       // 快速处置
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
