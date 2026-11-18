using System.Collections.Generic;
using UnityEngine;

// 设定集(3) §6 主动技能 / §7 被动技能 / §8 进化技能 闭集：
// 6 主动 + 12 被动 + 9 进化 = 27 项。
// 旧版多出的 弹簧圈(16)/血液回收钳(22)/体外循环泵(26) 移出掉落与选择池（枚举与行为代码保留，不再产出）。
// 全部文字（名称/图标字/短描述/描述/科普/Lv1-3效应）在 Resources/skill_text.csv，经 SkillText 读取，F8 热重载。
public static class VesselSkillCatalog
{
    public static readonly UpgradeType[] Types =
    {
        // 主动（§6，表序）
        UpgradeType.FlushNeedle, UpgradeType.DissectionScissors,
        UpgradeType.Suture, UpgradeType.MicroscopicHemostat,
        UpgradeType.ThrombusCatheter, UpgradeType.AblationBomb,
        // 被动（§7，表序）
        UpgradeType.AdvancedTraining, UpgradeType.MajorSurgery,
        UpgradeType.HappyWater, UpgradeType.DeepResearch,
        UpgradeType.FreeClinic, UpgradeType.SuctionTube,
        UpgradeType.HeartAmulet, UpgradeType.PartnerSupport,
        UpgradeType.EquipmentUpdate, UpgradeType.SidewallForceps,
        UpgradeType.MembraneStent, UpgradeType.RapidDisposal,
        // 进化（§8，表序）
        UpgradeType.ContinuousFlush, UpgradeType.UltrasonicScissors,
        UpgradeType.StapleGun, UpgradeType.EmbolismHemostatNet,
        UpgradeType.VascularSpasmRing, UpgradeType.DirectedAblationRain,
        UpgradeType.HighFrequencyElectroknife, UpgradeType.MicroInfusionPump,
        UpgradeType.AtomizationDrugMachine
    };

    public static string DescriptionFor(UpgradeType type) { return SkillText.Description(type); }

    public static string LevelEffectFor(UpgradeType active, int level)
    {
        return SkillText.LevelEffect(active, level);
    }

    public static bool IsVesselSkill(UpgradeType type)
    {
        int t = (int)type;
        return t >= 10 && t <= 39;
    }

    public static bool IsPassive(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.AdvancedTraining:
            case UpgradeType.MajorSurgery:
            case UpgradeType.HappyWater:
            case UpgradeType.DeepResearch:
            case UpgradeType.FreeClinic:
            case UpgradeType.SuctionTube:
            case UpgradeType.HeartAmulet:
            case UpgradeType.PartnerSupport:
            case UpgradeType.EquipmentUpdate:
            case UpgradeType.SidewallForceps:
            case UpgradeType.MembraneStent:
            case UpgradeType.RapidDisposal:
                return true;
            default:
                return false;
        }
    }

    public static bool IsEvolution(UpgradeType type)
    {
        int t = (int)type;
        return t >= 20 && t <= 30 && !IsRetired(type);
    }

    // 旧版残留：保留枚举/行为代码，但不再进入掉落与选择池（口径规则3）。
    public static bool IsRetired(UpgradeType type)
    {
        return type == UpgradeType.SpringCoil
            || type == UpgradeType.BloodRecoveryForceps
            || type == UpgradeType.ExtracorporealPump;
    }

    public static bool IsActiveSkill(UpgradeType type)
    {
        return IsVesselSkill(type) && !IsPassive(type) && !IsEvolution(type) && !IsRetired(type);
    }

    public static int MaxLevel(UpgradeType type)
    {
        // 被动无等级：选取一次性立即生效（§7 扁平值，策划确认）；进化一次性；主动三段 lv1–lv3（§6）。
        if (IsEvolution(type) || IsPassive(type)) return 1;
        return 3;
    }

    // 进化后替换的原主动技能（§8 表头：去除原主动技能效果，更换为下述技能）。
    // 基础攻击系进化（电刀/输液泵/雾化机）无对应主动技能项，返回自身；
    // 其替换语义落在基础攻击行为上（R1-08/F58）：医生普攻→ElectroknifeArc、护士普攻间隔→极短冷却（CharacterCombat/AutoAttack），
    // 药剂师雾化机仅改药剂滩上限，基础攻击链不动。
    public static UpgradeType BaseActiveFor(UpgradeType evolution)
    {
        switch (evolution)
        {
            case UpgradeType.ContinuousFlush: return UpgradeType.FlushNeedle;
            case UpgradeType.UltrasonicScissors: return UpgradeType.DissectionScissors;
            case UpgradeType.StapleGun: return UpgradeType.Suture;
            case UpgradeType.EmbolismHemostatNet: return UpgradeType.MicroscopicHemostat;
            case UpgradeType.VascularSpasmRing: return UpgradeType.ThrombusCatheter;
            case UpgradeType.DirectedAblationRain: return UpgradeType.AblationBomb;
            default: return evolution;
        }
    }

    // 进化条件（§8）拆为「基础满级」与「对应被动」两半，供选卡金辉预览复用。
    // 页裁等级按 Lv1 处理并标旗：栓塞止血网/血管痉挛环/微量输液泵。
    public static UpgradeType PassiveForEvolution(UpgradeType evolution)
    {
        switch (evolution)
        {
            case UpgradeType.ContinuousFlush: return UpgradeType.EquipmentUpdate;
            case UpgradeType.UltrasonicScissors: return UpgradeType.DeepResearch;
            case UpgradeType.StapleGun: return UpgradeType.MembraneStent;
            case UpgradeType.EmbolismHemostatNet: return UpgradeType.SuctionTube;
            case UpgradeType.VascularSpasmRing: return UpgradeType.MajorSurgery;
            case UpgradeType.DirectedAblationRain: return UpgradeType.FreeClinic;
            case UpgradeType.HighFrequencyElectroknife: return UpgradeType.AdvancedTraining;
            case UpgradeType.MicroInfusionPump: return UpgradeType.PartnerSupport;
            case UpgradeType.AtomizationDrugMachine: return UpgradeType.RapidDisposal;
            default: return evolution;
        }
    }

    public static bool EvolutionBaseReady(UpgradeType evolution, PlayerSkillCollection skills, CharacterCombat combat)
    {
        if (skills == null || combat == null) return false;
        switch (evolution)
        {
            case UpgradeType.ContinuousFlush: return skills.GetLevel(UpgradeType.FlushNeedle) >= 3;
            case UpgradeType.UltrasonicScissors: return skills.GetLevel(UpgradeType.DissectionScissors) >= 3;
            case UpgradeType.StapleGun: return skills.GetLevel(UpgradeType.Suture) >= 3;
            case UpgradeType.EmbolismHemostatNet: return skills.GetLevel(UpgradeType.MicroscopicHemostat) >= 3;
            case UpgradeType.VascularSpasmRing: return skills.GetLevel(UpgradeType.ThrombusCatheter) >= 3;
            case UpgradeType.DirectedAblationRain: return skills.GetLevel(UpgradeType.AblationBomb) >= 3;
            case UpgradeType.HighFrequencyElectroknife: return combat.role == CharacterRole.Doctor && combat.rank >= 3;
            case UpgradeType.MicroInfusionPump: return combat.role == CharacterRole.Nurse && combat.rank >= 3;
            case UpgradeType.AtomizationDrugMachine: return combat.role == CharacterRole.Pharmacist && combat.rank >= 3;
            default: return false;
        }
    }

    public static bool EvolutionReady(UpgradeType evolution, PlayerSkillCollection skills, CharacterCombat combat)
    {
        if (skills == null || combat == null) return false;
        return EvolutionBaseReady(evolution, skills, combat) && skills.GetLevel(PassiveForEvolution(evolution)) >= 1;
    }

    public static UpgradeOption[] CreateOptions()
    {
        List<UpgradeOption> result = new List<UpgradeOption>();
        for (int i = 0; i < Types.Length; i++)
        {
            UpgradeOption option = ScriptableObject.CreateInstance<UpgradeOption>();
            option.name = SkillText.Name(Types[i]);
            option.displayName = SkillText.Name(Types[i]);
            option.description = SkillText.Description(Types[i]);
            option.type = Types[i];
            option.value = 1f;
            option.skillCard = CreateCard(i);
            result.Add(option);
        }
        return result.ToArray();
    }

    public static SkillCardDefinition CreateCard(int index)
    {
        index = Mathf.Clamp(index, 0, Types.Length - 1);
        UpgradeType type = Types[index];
        SkillCardDefinition card = ScriptableObject.CreateInstance<SkillCardDefinition>();
        card.name = SkillText.Name(type) + "卡";
        card.cardId = "VesselSkill_" + ((int)type);
        card.displayName = SkillText.Name(type);
        card.description = SkillText.Description(type);
        card.educationText = SkillText.Education(type);
        card.linkedUpgradeType = type;
        card.iconGlyph = SkillText.Glyph(type);
        card.cardColor = ColorFor(type);
        return card;
    }

    public static int IndexOf(UpgradeType type)
    {
        for (int i = 0; i < Types.Length; i++) if (Types[i] == type) return i;
        return 0;
    }

    public static string NameFor(UpgradeType type) { return SkillText.Name(type); }
    public static string EducationFor(UpgradeType type) { return SkillText.Education(type); }
    public static string GlyphFor(UpgradeType type) { return SkillText.Glyph(type); }
    public static string ShortDescriptionFor(UpgradeType type) { return SkillText.ShortDesc(type); }

    public static Color ColorFor(UpgradeType type)
    {
        Color raw;
        switch (type)
        {
            case UpgradeType.FlushNeedle: raw = new Color(0.35f, 0.78f, 0.86f); break;
            case UpgradeType.DissectionScissors: raw = new Color(0.72f, 0.42f, 0.95f); break;
            case UpgradeType.Suture: raw = new Color(0.92f, 0.38f, 0.50f); break;
            case UpgradeType.MicroscopicHemostat: raw = new Color(0.96f, 0.62f, 0.23f); break;
            case UpgradeType.ThrombusCatheter: raw = new Color(0.27f, 0.82f, 0.70f); break;
            case UpgradeType.AblationBomb: raw = new Color(1.00f, 0.55f, 0.20f); break;
            case UpgradeType.AdvancedTraining: raw = new Color(0.95f, 0.75f, 0.30f); break;
            case UpgradeType.MajorSurgery: raw = new Color(0.90f, 0.45f, 0.35f); break;
            case UpgradeType.HappyWater: raw = new Color(0.45f, 0.80f, 0.95f); break;
            case UpgradeType.DeepResearch: raw = new Color(0.55f, 0.65f, 0.95f); break;
            case UpgradeType.FreeClinic: raw = new Color(0.55f, 0.85f, 0.55f); break;
            case UpgradeType.SuctionTube: raw = new Color(0.55f, 0.85f, 0.95f); break;
            case UpgradeType.HeartAmulet: raw = new Color(0.95f, 0.45f, 0.60f); break;
            case UpgradeType.PartnerSupport: raw = new Color(0.85f, 0.70f, 0.45f); break;
            case UpgradeType.EquipmentUpdate: raw = new Color(0.60f, 0.80f, 0.85f); break;
            case UpgradeType.SidewallForceps: raw = new Color(0.88f, 0.24f, 0.30f); break;
            case UpgradeType.MembraneStent: raw = new Color(0.24f, 0.68f, 0.92f); break;
            case UpgradeType.RapidDisposal: raw = new Color(0.98f, 0.80f, 0.35f); break;
            case UpgradeType.ContinuousFlush: raw = new Color(0.30f, 0.85f, 0.95f); break;
            case UpgradeType.UltrasonicScissors: raw = new Color(0.80f, 0.50f, 1.00f); break;
            case UpgradeType.StapleGun: raw = new Color(0.98f, 0.70f, 0.30f); break;
            case UpgradeType.EmbolismHemostatNet: raw = new Color(0.50f, 0.80f, 0.40f); break;
            case UpgradeType.VascularSpasmRing: raw = new Color(0.30f, 0.85f, 0.75f); break;
            case UpgradeType.DirectedAblationRain: raw = new Color(1.00f, 0.60f, 0.25f); break;
            case UpgradeType.HighFrequencyElectroknife: raw = new Color(0.75f, 0.45f, 0.98f); break;
            case UpgradeType.MicroInfusionPump: raw = new Color(0.40f, 0.88f, 0.98f); break;
            case UpgradeType.AtomizationDrugMachine: raw = new Color(0.85f, 0.60f, 0.95f); break;
            default: raw = new Color(0.60f, 0.90f, 1.00f); break;
        }
        // 色相表同时决定卡框发光色，统一降饱和：保留稀有度辨识度，但去掉霓虹感。
        float hue, sat, val;
        Color.RGBToHSV(raw, out hue, out sat, out val);
        return Color.HSVToRGB(hue, Mathf.Clamp(sat * 0.62f, 0.18f, 0.58f), Mathf.Clamp(val, 0.72f, 0.94f));
    }
}